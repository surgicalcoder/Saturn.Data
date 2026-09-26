using System.Collections.Concurrent;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IDisposable
{
    private readonly RepositoryOptions options;
    private readonly SqliteRepositoryOptions sqliteOptions;
    private readonly SqliteConnectionFactory connectionFactory;
    private readonly ConcurrentDictionary<string, byte> knownTables = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim writeGate = new(1, 1);

    protected bool initialized;
    private bool disposed;

    public SqliteRepository(RepositoryOptions options, SqliteRepositoryOptions sqliteOptions)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.sqliteOptions = sqliteOptions ?? throw new ArgumentNullException(nameof(sqliteOptions));
        connectionFactory = new SqliteConnectionFactory(sqliteOptions);
    }

    internal RepositoryOptions Options => options;

    internal SqliteRepositoryOptions SqliteOptions => sqliteOptions;

    internal SqliteConnectionFactory ConnectionFactory => connectionFactory;

    protected string GetCollectionNameForType<TItem>() where TItem : Entity => GetCollectionNameForType(typeof(TItem));

    protected string GetCollectionNameForType(Type type) => options.GetCollectionName(type);

    internal string CollectionNameForType(Type type) => GetCollectionNameForType(type);

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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        writeGate.Dispose();
        SqliteConnection.ClearAllPools();
        GC.SuppressFinalize(this);
    }
}
