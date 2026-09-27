using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Microsoft.Data.Sqlite;
using Saturn.Data.Sqlite.Serialization;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IDisposable
{
    private readonly RepositoryOptions options;
    private readonly SqliteRepositoryOptions sqliteOptions;
    private readonly SqliteConnectionFactory connectionFactory;
    private readonly ConcurrentDictionary<string, byte> knownTables = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly EntityJsonSerializer serializer;

    protected bool initialized;
    private bool disposed;

    public SqliteRepository(RepositoryOptions options, SqliteRepositoryOptions sqliteOptions)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.sqliteOptions = sqliteOptions ?? throw new ArgumentNullException(nameof(sqliteOptions));
        connectionFactory = new SqliteConnectionFactory(sqliteOptions);
        serializer = new EntityJsonSerializer();
    }

    internal RepositoryOptions Options => options;

    internal SqliteRepositoryOptions SqliteOptions => sqliteOptions;

    internal SqliteConnectionFactory ConnectionFactory => connectionFactory;

    protected EntityJsonSerializer Serializer => serializer;

    protected bool HasWriteBehaviors => options.WriteBehaviors is { Count: > 0 };

    protected string GetCollectionNameForType<TItem>() where TItem : Entity => GetCollectionNameForType(typeof(TItem));

    protected string GetCollectionNameForType(Type type) => options.GetCollectionName(type);

    internal string CollectionNameForType(Type type) => GetCollectionNameForType(type);

    internal static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    protected static string? NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return Entity.TryParseId(id, out var normalized) && normalized is not null ? normalized : null;
    }

    protected static List<string> NormalizeEntityIds(IEnumerable<string> ids)
    {
        var result = new List<string>();

        if (ids is null)
        {
            return result;
        }

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (Entity.TryParseId(id, out var normalized) && normalized is not null && !result.Contains(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    internal static string TranslatePredicateSql<TItem>(Expression<Func<TItem, bool>> predicate) where TItem : Entity
        => new Query.SqliteExpressionTranslator().Translate(predicate).Sql;

    internal static bool SupportsSoftDelete<TItem>() where TItem : Entity => typeof(ISoftDeletable).IsAssignableFrom(typeof(TItem));

    internal static bool SupportsArchivable<TItem>() where TItem : Entity => typeof(IArchivable).IsAssignableFrom(typeof(TItem));

    internal async Task<SqliteConnectionLease> RentConnectionAsync(IDatabaseTransaction? transaction, CancellationToken cancellationToken)
    {
        if (transaction is SqliteTransactionWrapper wrapper)
        {
            return new SqliteConnectionLease(wrapper.Connection, ownsConnection: false);
        }

        var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new SqliteConnectionLease(connection, ownsConnection: true);
    }

    internal Task<SqliteConnectionLease> RentConnectionInternalAsync(IDatabaseTransaction? transaction, CancellationToken cancellationToken)
        => RentConnectionAsync(transaction, cancellationToken);

    protected Task EnsureTableAsync<TItem>(SqliteConnection connection, CancellationToken cancellationToken) where TItem : Entity
        => EnsureTableAsync(GetCollectionNameForType<TItem>(), connection, cancellationToken);

    internal async Task EnsureTableAsync(string collection, SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (knownTables.ContainsKey(collection))
        {
            return;
        }

        var table = Quote(collection);

        var ddl = $"""
            CREATE TABLE IF NOT EXISTS {table} (
                _id TEXT NOT NULL PRIMARY KEY,
                _v INTEGER NULL,
                _deleted INTEGER NOT NULL DEFAULT 0,
                _scope TEXT NULL,
                _scope2 TEXT NULL,
                _archived INTEGER NOT NULL DEFAULT 0,
                _doc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS {Quote($"ix_{collection}__scope")} ON {table}(_scope);
            CREATE INDEX IF NOT EXISTS {Quote($"ix_{collection}__deleted")} ON {table}(_deleted);
            CREATE INDEX IF NOT EXISTS {Quote($"ix_{collection}__scope2")} ON {table}(_scope2);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = ddl;
        await command.ExecuteNonQueryWithRetryAsync(sqliteOptions, cancellationToken).ConfigureAwait(false);

        knownTables.TryAdd(collection, 0);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "SELECT json_extract('{\"a\":1}', '$.a');";
            var result = await probe.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (result is null || Convert.ToInt64(result) != 1)
            {
                throw new InvalidOperationException("SQLite JSON1 extension is not available in this build.");
            }
        }

        if (sqliteOptions.EnableWal && !connectionFactory.IsMemory)
        {
            await using var wal = connection.CreateCommand();
            wal.CommandText = "PRAGMA journal_mode=WAL;";
            var mode = await wal.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;

            if (sqliteOptions.RequireWal && !string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SQLite journal_mode is '{mode}', expected 'wal'.");
            }
        }

        initialized = true;
    }

    protected RepositoryWriteContext<TItem> BuildWriteContext<TItem>(
        RepositoryWriteOperation operation,
        TItem item = default!,
        IEnumerable<TItem> items = default!,
        string id = null!,
        IEnumerable<string> ids = null!,
        Expression<Func<TItem, bool>> filter = default!,
        long? expectedVersion = null,
        string jsonDocument = null!,
        IDataUpdateDefinition<TItem> updateDefinition = default!,
        LambdaExpression incrementField = null!,
        object incrementDelta = null!,
        IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var itemList = items?.ToList() ?? (item is null ? null : new List<TItem> { item });

        return new RepositoryWriteContext<TItem>
        {
            Operation = operation,
            Id = id,
            Ids = ids?.ToList(),
            Items = itemList,
            Filter = filter,
            ExpectedVersion = expectedVersion,
            JsonDocument = jsonDocument,
            UpdateDefinition = updateDefinition,
            IncrementField = incrementField,
            IncrementDelta = incrementDelta,
            Transaction = transaction,
            CancellationToken = cancellationToken
        };
    }

    protected ValueTask DispatchWriteBehaviorsAsync<TItem>(RepositoryWriteOperation operation, RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => BehaviorDispatcher.DispatchBeforeAsync(options.WriteBehaviors, operation, context);

    protected ValueTask ApplyAfterBehaviors<TItem>(RepositoryWriteOperation operation, RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => BehaviorDispatcher.DispatchAfterAsync(options.WriteBehaviors, operation, context, result);

    protected ValueTask ApplyOnWriteFailed<TItem>(RepositoryWriteContext<TItem> context, Exception exception)
        where TItem : Entity
        => BehaviorDispatcher.DispatchOnWriteFailedAsync(options.WriteBehaviors, context, exception);

    protected static RepositoryWriteResult BuildWriteResult<TItem>(
        RepositoryWriteContext<TItem> context,
        WriteOutcome outcome,
        int affected,
        IEnumerable<string>? entityIds = null,
        bool wasCreated = false,
        IReadOnlyCollection<string>? matchedIds = null,
        bool partialFailure = false,
        int failedCount = 0)
        where TItem : Entity
        => new()
        {
            Operation = context.Operation,
            Succeeded = true,
            PartialFailure = partialFailure,
            Outcome = outcome,
            AffectedCount = affected,
            FailedCount = failedCount,
            EntityIds = entityIds is null ? (IReadOnlyCollection<string>)Array.Empty<string>() : entityIds.ToList(),
            MatchedIds = matchedIds ?? Array.Empty<string>(),
            WasCreated = wasCreated,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        writeGate.Dispose();
        connectionFactory.ClearPool();
        GC.SuppressFinalize(this);
    }
}
