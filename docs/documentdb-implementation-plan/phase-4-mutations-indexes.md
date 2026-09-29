# Phase 4 — Patch, JsonUpdate, Increment and Indexes

**Goal:** Implement the mutation surface (`Patch`, `JsonUpdate`, `Increment`) with Saturn's version semantics, and `IRepositoryIndexManager.EnsureIndexes`.

**Prerequisite:** Phase 3 complete; scoped tests green.

**Exit criteria:**
- `PatchWritesTests` green: `{"$set":{"Count":5}}` sets the field and bumps `Version`; unknown id and stale `expectedVersion` throw `FailedToUpdateException`.
- `IncrementWritesTests` green.
- `IndexManagerTests` green: a non-unique JSON index is created; `Unique`/`Sparse`/`ExpireAfter` are reported as unsupported through `OnUnsupportedIndexOption` and do not throw.

**Strategy note (read this first).** Decision 6 wants atomic patches. Shiny.DocumentDb's `ExecuteUpdate` is atomic where supported, but it cannot (a) compute `value = current + delta` for `$inc`, or (b) report affected rows for a version check. This phase therefore implements **read-modify-write with an explicit version check** as the universal path — correct on every backend — and mentions the `ExecuteUpdate` fast path as a Phase 7 optimization. Do not attempt the fast path now; correctness first.

---

## Task 4.1 — Create `DocumentDbDataUpdateDefinition.cs`

`...\GoLive.Saturn.Data.DocumentDb\DocumentDbDataUpdateDefinition.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public sealed class DocumentDbDataUpdateDefinition<TItem> : IDataUpdateDefinition<TItem> where TItem : Entity
{
    public DocumentDbDataUpdateDefinition(Action<TItem> apply)
    {
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    internal Action<TItem> Apply { get; }
}
```

---

## Task 4.2 — Create `DocumentDbRepository.Patch.cs`

`...\GoLive.Saturn.Data.DocumentDb\DocumentDbRepository.Patch.cs`:

```csharp
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository
{
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

            var entity = await Store.Get<TItem>(normalized).ConfigureAwait(false);

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
            await Store.Update(entity).ConfigureAwait(false);

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

        var patchJson = hasOperators
            ? json
            : $"{{\"$set\":{json}}}";

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

    private enum PatchValueMode
    {
        Set,
        Unset,
        Increment
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
                    ApplyIncrement(entity, target, property.Value);
                    break;
            }
        }
    }

    private static void ApplyIncrement(object entity, PropertyInfo property, JsonElement delta)
    {
        var current = property.GetValue(entity);
        var currentValue = current is null ? 0m : Convert.ToDecimal(current, CultureInfo.InvariantCulture);
        var deltaValue = Convert.ToDecimal(delta.GetDouble(), CultureInfo.InvariantCulture);
        var result = currentValue + deltaValue;

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        property.SetValue(entity, Convert.ChangeType(result, targetType, CultureInfo.InvariantCulture));
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
```

> Dotted paths walk the property chain for lookup but only the **final** property is assigned; Phase 4 does not support setting a nested path on a fresh object. That matches the SQLite provider's behaviour closely enough for the shared patch tests, which use top-level fields. Note any divergence in the README.

---

## Task 4.3 — Create `DocumentDbRepository.Increment.cs`

`...\GoLive.Saturn.Data.DocumentDb\DocumentDbRepository.Increment.cs`:

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository
{
    public Task Increment<TItem>(string id, Expression<Func<TItem, int>> field, int delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, long>> field, long delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, double>> field, double delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, decimal>> field, decimal delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, (double)delta, expectedVersion, transaction, cancellationToken);

    private async Task IncrementCore<TItem>(string id, LambdaExpression field, object delta, long? expectedVersion,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity
    {
        if (!Query.MemberPathResolver.TryResolve(field, out var path, out _) || path.Contains('.'))
        {
            throw new NotSupportedException($"Cannot increment field '{field.Body}'. Only top-level document fields are supported.");
        }

        var normalized = NormalizeId(id) ?? throw new FailedToUpdateException();

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Increment, id: normalized, expectedVersion: expectedVersion,
            incrementField: field, incrementDelta: delta, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Increment, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var entity = await Store.Get<TItem>(normalized).ConfigureAwait(false);

            if (entity is null)
            {
                throw new FailedToUpdateException();
            }

            if (expectedVersion.HasValue && entity.Version != expectedVersion.Value)
            {
                throw new FailedToUpdateException();
            }

            var property = typeof(TItem).GetProperty(path, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                           ?? throw new NotSupportedException($"No property '{path}' on '{typeof(TItem).Name}'.");

            var current = property.GetValue(entity);
            var currentValue = current is null ? 0m : Convert.ToDecimal(current, System.Globalization.CultureInfo.InvariantCulture);
            var deltaValue = Convert.ToDecimal(delta, System.Globalization.CultureInfo.InvariantCulture);
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            property.SetValue(entity, Convert.ChangeType(currentValue + deltaValue, targetType, System.Globalization.CultureInfo.InvariantCulture));

            entity.Version = (entity.Version ?? 0) + 1;
            await Store.Update(entity).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Increment, context,
                BuildWriteResult(context, WriteOutcome.Incremented, 1, new[] { normalized })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }
}
```

> `BuildWriteContext` must already accept `incrementField`/`incrementDelta` — it does, because it was copied verbatim from the SQLite provider in Phase 1.

---

## Task 4.4 — Create `DocumentDbRepository.Indexes.cs`

`...\GoLive.Saturn.Data.DocumentDb\DocumentDbRepository.Indexes.cs`:

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : IRepositoryIndexManager
{
    public async Task EnsureIndexes<TItem>(IEnumerable<IIndexDefinition<TItem>> definitions, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        foreach (var definition in definitions)
        {
            if (definition.Options.Unique)
            {
                documentDbOptions.OnUnsupportedIndexOption?.Invoke($"Unique index '{definition.Name}' requires per-type configuration (MapUniqueIndex) and was not created.");
                continue;
            }

            if (definition.Options.Sparse)
            {
                documentDbOptions.OnUnsupportedIndexOption?.Invoke($"Sparse index '{definition.Name}' is not supported and was created without a filter.");
            }

            if (definition.Options.HasExpireAfter)
            {
                documentDbOptions.OnUnsupportedIndexOption?.Invoke($"ExpireAfter is not supported for index '{definition.Name}'. The index was created without expiry.");
            }

            foreach (var key in definition.Keys)
            {
                await CreateIndexForAsync<TItem>(key.Field, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task CreateIndexForAsync<TItem>(Expression<Func<TItem, object>> field, CancellationToken cancellationToken) where TItem : Entity
    {
        var method = typeof(Shiny.DocumentDb.IDocumentStore)
            .GetMethods()
            .FirstOrDefault(candidate => candidate.Name == "CreateIndexAsync" && candidate.IsGenericMethodDefinition && candidate.GetParameters().Length == 1);

        if (method is null)
        {
            documentDbOptions.OnUnsupportedIndexOption?.Invoke($"CreateIndexAsync was not found on IDocumentStore; index for '{field}' was skipped.");
            return;
        }

        var body = field.Body;

        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        var parameter = field.Parameters[0];
        var keyType = body.Type;
        var keyLambda = Expression.Lambda(Expression.Convert(body, keyType), parameter);

        var generic = method.MakeGenericMethod(typeof(TItem), keyType);
        var result = generic.Invoke(Store, new object[] { keyLambda });

        if (result is Task task)
        {
            await task.ConfigureAwait(false);
        }
    }
}
```

> `CreateIndexAsync`'s exact arity and parameter list are unverified. The reflection above assumes `CreateIndexAsync<TItem, TKey>(Expression<Func<TItem, TKey>>)` with one parameter. Phase 0's probe records the real signature; **if it differs, replace this reflection with a direct typed call for the single-key case** — do not guess. If reflection becomes unreasonable, reduce `EnsureIndexes` to a documented no-op that reports through `OnUnsupportedIndexOption` and defer index creation to Phase 7.

---

## Task 4.5 — Tests

Port from the SQLite provider's test project, changing namespace and repository type:

| Source | Target |
| --- | --- |
| `PatchWritesTests.cs` | `...\PatchWritesTests.cs` |
| `IncrementWritesTests.cs` | `...\IncrementWritesTests.cs` |
| `SqliteIndexManagerTests.cs` | `...\IndexManagerTests.cs` |

In `IndexManagerTests`, the "index exists" assertion used `PRAGMA index_list`; there is no equivalent here. Replace that assertion with "`EnsureIndexes` completes without throwing and invokes `OnUnsupportedIndexOption` for a TTL definition". Keep the unique-index test but assert the *warning callback fires* rather than a duplicate-key exception, because unique indexes are reported unsupported in this phase.

---

## Task 4.6 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "...\GoLive.Saturn.Data.DocumentDb.Tests.csproj"
```

---

## Do NOT

- Do not implement `ExecuteUpdate`-based patching in this phase.
- Do not use `cfg.MapUniqueIndex` or any per-type configuration.
- Do not attempt nested-path patching of a missing intermediate object.
- Do not implement transactions, cascade or change feed here.
- Do not add comments to `.cs` files.
