using System.Text.Json;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository
{
    public async Task Patch<TItem>(string id, long? expectedVersion = null, string jsonDocument = null!,
        IDataUpdateDefinition<TItem> updateDefinition = null!, IDatabaseTransaction transaction = null!,
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
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

            var affected = !string.IsNullOrWhiteSpace(jsonDocument)
                ? await ExecutePatchAsync<TItem>(lease.Connection, normalized, expectedVersion, jsonDocument, cancellationToken).ConfigureAwait(false)
                : await ExecuteUpdateDefinitionPatchAsync(lease.Connection, normalized, expectedVersion, updateDefinition, cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, affected, new[] { normalized })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        var normalized = NormalizeId(id) ?? throw new FailedToUpdateException();

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Patch, id: normalized, jsonDocument: json,
            transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Patch, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

            var affected = await ExecutePatchAsync<TItem>(lease.Connection, normalized, expectedVersion: null, json, cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, affected, new[] { normalized })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<int> ExecutePatchAsync<TItem>(SqliteConnection connection, string id, long? expectedVersion, string jsonDocument,
        CancellationToken cancellationToken) where TItem : Entity
    {
        using var document = JsonDocument.Parse(jsonDocument);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Patch JSON must be an object.", nameof(jsonDocument));
        }

        var parameters = new List<SqliteParameter>();
        var current = BuildPatchExpression(root, parameters);
        var table = Quote(GetCollectionNameForType<TItem>());
        var versionFilter = expectedVersion.HasValue ? " AND COALESCE(_v,0) = @expectedVersion" : string.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {table}
            SET _doc = json_set({current}, '$.Version', COALESCE(_v,0) + 1),
                _v = COALESCE(_v,0) + 1,
                _deleted = COALESCE(json_extract({current},'$.IsDeleted'),0),
                _scope = json_extract({current},'$.Scope'),
                _scope2 = json_extract({current},'$.SecondScope'),
                _archived = COALESCE(json_extract({current},'$.IsArchived'),0)
            WHERE _id = @id{versionFilter};
            """;

        command.Parameters.AddWithValue("@id", id);

        if (expectedVersion.HasValue)
        {
            command.Parameters.AddWithValue("@expectedVersion", expectedVersion.Value);
        }

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        return await command.ExecuteNonQueryWithRetryAsync(sqliteOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildPatchExpression(JsonElement root, List<SqliteParameter> parameters)
    {
        var hasOperator = root.TryGetProperty("$set", out _) || root.TryGetProperty("$inc", out _) || root.TryGetProperty("$unset", out _);
        var current = "_doc";

        if (hasOperator)
        {
            if (root.TryGetProperty("$set", out var setElement) && setElement.ValueKind == JsonValueKind.Object)
            {
                current = AppendSet(current, setElement, parameters);
            }

            if (root.TryGetProperty("$inc", out var incElement) && incElement.ValueKind == JsonValueKind.Object)
            {
                current = AppendInc(current, incElement, parameters);
            }

            if (root.TryGetProperty("$unset", out var unsetElement) && unsetElement.ValueKind == JsonValueKind.Object)
            {
                current = AppendUnset(current, unsetElement);
            }

            if (current == "_doc")
            {
                throw new ArgumentException("Patch JSON contained update operators but no supported operations.");
            }

            return current;
        }

        return AppendSet(current, root, parameters);
    }

    private static string AppendSet(string current, JsonElement setElement, List<SqliteParameter> parameters)
    {
        foreach (var property in setElement.EnumerateObject())
        {
            var path = BuildJsonPath(property.Name);
            var parameter = new SqliteParameter($"@patch{parameters.Count}", property.Value.GetRawText());
            parameters.Add(parameter);
            current = $"json_set({current}, '{path}', json({parameter.ParameterName}))";
        }

        return current;
    }

    private static string AppendInc(string current, JsonElement incElement, List<SqliteParameter> parameters)
    {
        foreach (var property in incElement.EnumerateObject())
        {
            var path = BuildJsonPath(property.Name);
            var parameter = new SqliteParameter($"@patch{parameters.Count}", property.Value.GetRawText());
            parameters.Add(parameter);
            current = $"json_set({current}, '{path}', COALESCE(json_extract(_doc,'{path}'),0) + CAST({parameter.ParameterName} AS NUMERIC))";
        }

        return current;
    }

    private static string AppendUnset(string current, JsonElement unsetElement)
    {
        foreach (var property in unsetElement.EnumerateObject())
        {
            current = $"json_remove({current}, '{BuildJsonPath(property.Name)}')";
        }

        return current;
    }

    private static string BuildJsonPath(string key)
        => key.All(character => char.IsLetterOrDigit(character) || character == '_') ? $"$.{key}" : $"$.\"{key}\"";

    private async Task<int> ExecuteUpdateDefinitionPatchAsync<TItem>(SqliteConnection connection, string id, long? expectedVersion,
        IDataUpdateDefinition<TItem> updateDefinition, CancellationToken cancellationToken) where TItem : Entity
    {
        if (updateDefinition is not SqliteDataUpdateDefinition<TItem> sqliteDefinition)
        {
            throw new NotSupportedException("Only SqliteDataUpdateDefinition<TItem> is supported.");
        }

        var existing = await ReadDocumentAsync<TItem>(connection, id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return 0;
        }

        if (expectedVersion.HasValue && existing.Version != expectedVersion.Value)
        {
            return 0;
        }

        sqliteDefinition.Apply(existing);
        existing.Id = id;
        existing.Version = (existing.Version ?? 0) + 1;

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {Quote(GetCollectionNameForType<TItem>())}
            SET _doc = @doc,
                _v = json_extract(@doc,'$.Version'),
                _deleted = COALESCE(json_extract(@doc,'$.IsDeleted'),0),
                _scope = json_extract(@doc,'$.Scope'),
                _scope2 = json_extract(@doc,'$.SecondScope'),
                _archived = COALESCE(json_extract(@doc,'$.IsArchived'),0)
            WHERE _id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@doc", serializer.Serialize(existing));
        return await command.ExecuteNonQueryWithRetryAsync(sqliteOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TItem> ReadDocumentAsync<TItem>(SqliteConnection connection, string id, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT _doc FROM {Quote(GetCollectionNameForType<TItem>())} WHERE _id = @id;";
        command.Parameters.AddWithValue("@id", id);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is string document ? serializer.Deserialize<TItem>(document) : null!;
    }
}
