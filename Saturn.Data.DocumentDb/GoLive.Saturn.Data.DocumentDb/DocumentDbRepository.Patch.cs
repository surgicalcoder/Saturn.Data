using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository
{
    private enum PatchValueMode
    {
        Set,
        Unset,
        Increment
    }

    public async Task Patch<TItem>(string id, long? expectedVersion = null, string jsonDocument = null,
        IDataUpdateDefinition<TItem> updateDefinition = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(jsonDocument) && updateDefinition is null)
        {
            throw new ArgumentException("At least one patch input must be supplied.", nameof(jsonDocument));
        }

        var normalized = NormalizeId(id) ?? throw new FailedToUpdateException();

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Patch, id: normalized, expectedVersion: expectedVersion,
            jsonDocument: jsonDocument, updateDefinition: updateDefinition, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Patch, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var entity = await store.Get<TItem>(normalized).ConfigureAwait(false);

            if (entity is null)
            {
                throw new FailedToUpdateException();
            }

            if (expectedVersion.HasValue && entity.Version != expectedVersion.Value)
            {
                throw new FailedToUpdateException();
            }

            if (!string.IsNullOrWhiteSpace(jsonDocument))
            {
                ApplyPatchDocument(entity, jsonDocument);
            }
            else if (updateDefinition is DocumentDbDataUpdateDefinition<TItem> typed)
            {
                typed.Apply(entity);
            }
            else
            {
                throw new NotSupportedException("Only DocumentDbDataUpdateDefinition<TItem> is supported.");
            }

            entity.Version = (entity.Version ?? 0) + 1;
            await UpdateWithTransactionAsync(transaction, entity, cancellationToken).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, 1, new[] { normalized })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        var normalized = NormalizeId(id) ?? throw new FailedToUpdateException();

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("JsonUpdate requires a JSON object.", nameof(json));
        }

        var hasOperators = document.RootElement.TryGetProperty("$set", out _)
                           || document.RootElement.TryGetProperty("$unset", out _)
                           || document.RootElement.TryGetProperty("$inc", out _);

        var patchJson = hasOperators ? json : $"{{\"$set\":{json}}}";

        await Patch<TItem>(normalized, null, patchJson, null, transaction, cancellationToken).ConfigureAwait(false);
    }

    private void ApplyPatchDocument<TItem>(TItem entity, string jsonDocument) where TItem : Entity
    {
        using var document = JsonDocument.Parse(jsonDocument);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Patch JSON must be an object.", nameof(jsonDocument));
        }

        var hasOperators = root.TryGetProperty("$set", out _) || root.TryGetProperty("$unset", out _) || root.TryGetProperty("$inc", out _);

        if (!hasOperators)
        {
            ApplyValues(entity, root, PatchValueMode.Set);
            return;
        }

        if (root.TryGetProperty("$set", out var setElement))
        {
            ApplyValues(entity, setElement, PatchValueMode.Set);
        }

        if (root.TryGetProperty("$unset", out var unsetElement))
        {
            ApplyValues(entity, unsetElement, PatchValueMode.Unset);
        }

        if (root.TryGetProperty("$inc", out var incElement))
        {
            ApplyValues(entity, incElement, PatchValueMode.Increment);
        }
    }

    private void ApplyValues<TItem>(TItem entity, JsonElement element, PatchValueMode mode) where TItem : Entity
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var target = ResolveProperty(typeof(TItem), property.Name);

            if (target is null)
            {
                continue;
            }

            switch (mode)
            {
                case PatchValueMode.Set:
                    target.SetValue(entity, JsonSerializer.Deserialize(property.Value.GetRawText(), target.PropertyType, Serializer.JsonOptions));
                    break;

                case PatchValueMode.Unset:
                    target.SetValue(entity, target.PropertyType.IsValueType ? Activator.CreateInstance(target.PropertyType) : null);
                    break;

                case PatchValueMode.Increment:
                    ApplyNumericDelta(entity, target, property.Value);
                    break;
            }
        }
    }

    private static void ApplyNumericDelta(object entity, PropertyInfo property, JsonElement delta)
    {
        var current = property.GetValue(entity);
        var currentValue = current is null ? 0m : Convert.ToDecimal(current, CultureInfo.InvariantCulture);
        var deltaValue = Convert.ToDecimal(delta.GetDouble(), CultureInfo.InvariantCulture);
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        property.SetValue(entity, Convert.ChangeType(currentValue + deltaValue, targetType, CultureInfo.InvariantCulture));
    }

    private static PropertyInfo ResolveProperty(Type type, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = type;
        PropertyInfo property = null;

        foreach (var segment in segments)
        {
            property = current.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(candidate => string.Equals(candidate.Name, segment, StringComparison.OrdinalIgnoreCase));

            if (property is null)
            {
                return null;
            }

            current = property.PropertyType;
        }

        return property;
    }
}
