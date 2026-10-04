using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

internal sealed class SqliteMigrationStore : IMigrationStore
{
    private static readonly string[] CanonicalIndexSuffixes = { "scope", "deleted", "scope2" };

    private readonly SqliteConnectionFactory factory;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly Func<string, SqliteConnection, CancellationToken, Task> ensureTable;
    private readonly ConcurrentDictionary<string, byte> knownTables;

    public SqliteMigrationStore(
        SqliteConnectionFactory factory,
        JsonSerializerOptions jsonOptions,
        Func<string, SqliteConnection, CancellationToken, Task> ensureTable,
        ConcurrentDictionary<string, byte> knownTables)
    {
        this.factory = factory;
        this.jsonOptions = jsonOptions;
        this.ensureTable = ensureTable;
        this.knownTables = knownTables;
    }

    public MigrationStoreCapabilities Capabilities { get; } = new()
    {
        SupportsRawDocuments = true,
        SupportsRebuild = true,
        SupportsRenameCollection = true,
        SupportsIndexEnumeration = true,
        SupportsTransactions = true,
        SupportsObjectIdOnDisk = false,
        SupportsIncludeDeleted = true
    };

    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return reader.GetString(0);
        }
    }

    public bool CollectionExists(string collection)
    {
        using var connection = factory.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name;";
        command.Parameters.AddWithValue("@name", collection);
        return command.ExecuteScalar() != null;
    }

    public IMigrationCollection GetCollection(string collection) => new SqliteMigrationCollection(this, collection);

    public async Task<bool> RenameCollectionAsync(string source, string target, CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, $"ALTER TABLE {SqliteRepository.Quote(source)} RENAME TO {SqliteRepository.Quote(target)};", cancellationToken).ConfigureAwait(false);

        await DropCanonicalIndexesAsync(connection, source, cancellationToken).ConfigureAwait(false);
        await CreateCanonicalIndexesAsync(connection, target, cancellationToken).ConfigureAwait(false);

        knownTables.TryRemove(source, out _);
        knownTables.TryAdd(target, 0);
        return true;
    }

    public async Task<bool> DropCollectionAsync(string collection, CancellationToken cancellationToken = default)
    {
        await using var connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, $"DROP TABLE IF EXISTS {SqliteRepository.Quote(collection)};", cancellationToken).ConfigureAwait(false);
        await DropCanonicalIndexesAsync(connection, collection, cancellationToken).ConfigureAwait(false);

        knownTables.TryRemove(collection, out _);
        return true;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
    }

    internal SqliteConnectionFactory Factory => factory;

    internal JsonSerializerOptions JsonOptions => jsonOptions;

    internal Func<string, SqliteConnection, CancellationToken, Task> EnsureTable => ensureTable;

    internal static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DropCanonicalIndexesAsync(SqliteConnection connection, string collection, CancellationToken cancellationToken)
    {
        foreach (var suffix in CanonicalIndexSuffixes)
        {
            await ExecuteAsync(connection, $"DROP INDEX IF EXISTS {SqliteRepository.Quote($"ix_{collection}__{suffix}")};", cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task CreateCanonicalIndexesAsync(SqliteConnection connection, string collection, CancellationToken cancellationToken)
    {
        var table = SqliteRepository.Quote(collection);
        var columns = new Dictionary<string, string> { ["scope"] = "_scope", ["deleted"] = "_deleted", ["scope2"] = "_scope2" };

        foreach (var column in columns)
        {
            await ExecuteAsync(connection, $"CREATE INDEX IF NOT EXISTS {SqliteRepository.Quote($"ix_{collection}__{column.Key}")} ON {table}({column.Value});", cancellationToken).ConfigureAwait(false);
        }
    }
}

internal sealed class SqliteMigrationCollection : IMigrationCollection
{
    private readonly SqliteMigrationStore store;

    public SqliteMigrationCollection(SqliteMigrationStore store, string name)
    {
        this.store = store;
        Name = name;
    }

    public string Name { get; }

    public string IdFieldName => MigrationIds.IdField;

    public async IAsyncEnumerable<MigrationObject> ScanAsync(bool includeDeleted, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var table = SqliteRepository.Quote(Name);
        var sql = includeDeleted
            ? $"SELECT _id, _doc FROM {table};"
            : $"SELECT _id, _doc FROM {table} WHERE _deleted = 0;";

        await using var connection = await store.Factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return SqliteJsonConverter.ToMigrationObject(reader.GetString(0), reader.GetString(1));
        }
    }

    public Task InsertAsync(MigrationObject document, CancellationToken cancellationToken = default) => UpsertAsync(document, cancellationToken);

    public Task<bool> UpdateAsync(MigrationObject document, CancellationToken cancellationToken = default) => UpsertAsync(document, cancellationToken);

    public async Task<bool> DeleteAsync(MigrationValue id, CancellationToken cancellationToken = default)
    {
        await using var connection = await store.Factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {SqliteRepository.Quote(Name)} WHERE _id = @id;";

        var idText = id == null ? null : id.IsObjectId ? id.AsObjectId.ToString() : id.AsString;
        command.Parameters.AddWithValue("@id", (object)idText ?? DBNull.Value);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    public IReadOnlyList<MigrationIndexDefinition> GetIndexes()
    {
        using var connection = store.Factory.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list({SqliteRepository.Quote(Name)});";

        var indexes = new List<(string Name, bool Unique)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(1);

                if (name.StartsWith("sqlite_", StringComparison.Ordinal))
                {
                    continue;
                }

                indexes.Add((name, reader.GetInt32(2) != 0));
            }
        }

        var definitions = new List<MigrationIndexDefinition>();

        foreach (var index in indexes)
        {
            var columns = ReadIndexColumns(connection, index.Name);

            if (columns.Count == 0)
            {
                continue;
            }

            definitions.Add(new MigrationIndexDefinition(index.Name, string.Join(",", columns), index.Unique));
        }

        return definitions;
    }

    private static List<string> ReadIndexColumns(SqliteConnection connection, string indexName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_info({SqliteRepository.Quote(indexName)});";

        var columns = new List<string>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (!reader.IsDBNull(2))
            {
                columns.Add(reader.GetString(2));
            }
        }

        return columns;
    }

    public async Task EnsureIndexAsync(MigrationIndexDefinition index, CancellationToken cancellationToken = default)
    {
        await using var connection = await store.Factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var unique = index.Unique ? "UNIQUE " : string.Empty;
        var sql = $"CREATE {unique}INDEX IF NOT EXISTS {SqliteRepository.Quote(index.Name)} ON {SqliteRepository.Quote(Name)}({index.Expression});";
        await SqliteMigrationStore.ExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> UpsertAsync(MigrationObject document, CancellationToken cancellationToken)
    {
        await using var connection = await store.Factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await store.EnsureTable(Name, connection, cancellationToken).ConfigureAwait(false);

        var id = SqliteJsonConverter.GetIdText(document) ?? MigrationObjectId.NewObjectId().ToString();
        var json = SqliteJsonConverter.ToJson(document, store.JsonOptions);

        var version = document.TryGetValue("Version", out var versionValue) && versionValue != null && !versionValue.IsNull && versionValue.IsNumber
            ? versionValue.AsInt64
            : (long?)null;
        var deleted = document.TryGetValue("IsDeleted", out var deletedValue) && deletedValue.IsBoolean && deletedValue.AsBoolean ? 1 : 0;
        var scope = document.TryGetValue("Scope", out var scopeValue) && scopeValue != null && !scopeValue.IsNull ? scopeValue.AsString : null;
        var scope2 = document.TryGetValue("SecondScope", out var scope2Value) && scope2Value != null && !scope2Value.IsNull ? scope2Value.AsString : null;
        var archived = document.TryGetValue("IsArchived", out var archivedValue) && archivedValue.IsBoolean && archivedValue.AsBoolean ? 1 : 0;

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {SqliteRepository.Quote(Name)}(_id, _v, _deleted, _scope, _scope2, _archived, _doc)
            VALUES(@id, @v, @deleted, @scope, @scope2, @archived, @doc)
            ON CONFLICT(_id) DO UPDATE SET
                _v = excluded._v,
                _deleted = excluded._deleted,
                _scope = excluded._scope,
                _scope2 = excluded._scope2,
                _archived = excluded._archived,
                _doc = excluded._doc;
            """;

        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@v", (object)version ?? DBNull.Value);
        command.Parameters.AddWithValue("@deleted", deleted);
        command.Parameters.AddWithValue("@scope", (object)scope ?? DBNull.Value);
        command.Parameters.AddWithValue("@scope2", (object)scope2 ?? DBNull.Value);
        command.Parameters.AddWithValue("@archived", archived);
        command.Parameters.AddWithValue("@doc", json);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
