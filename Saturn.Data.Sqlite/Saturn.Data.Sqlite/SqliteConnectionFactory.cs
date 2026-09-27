using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

internal sealed class SqliteConnectionFactory
{
    private readonly SqliteRepositoryOptions options;
    private readonly string connectionString;

    public SqliteConnectionFactory(SqliteRepositoryOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = options.DataSource,
            Mode = options.Mode,
            Cache = options.Cache,
            Pooling = options.Pooling,
            DefaultTimeout = options.BusyTimeoutSeconds
        };

        if (!string.IsNullOrWhiteSpace(options.EncryptionPassword))
        {
            builder.Password = options.EncryptionPassword;
        }

        connectionString = builder.ToString();
    }

    public bool IsMemory => string.Equals(options.DataSource, ":memory:", StringComparison.Ordinal);

    public void ClearPool()
    {
        using var connection = new SqliteConnection(connectionString);
        SqliteConnection.ClearPool(connection);
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        ApplyPragmas(connection);
        return connection;
    }

    public void ApplyPragmas(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, $"PRAGMA busy_timeout={options.BusyTimeoutMs};");
        ExecuteNonQuery(connection, $"PRAGMA synchronous={MapSynchronous(options.Synchronous)};");
        ExecuteNonQuery(connection, $"PRAGMA wal_autocheckpoint={options.WalAutoCheckpointPages};");
        ExecuteNonQuery(connection, $"PRAGMA journal_size_limit={options.JournalSizeLimitBytes};");
        options.ConfigureConnection?.Invoke(connection);
    }

    private static string MapSynchronous(SqliteSynchronousMode mode) => mode switch
    {
        SqliteSynchronousMode.Off => "OFF",
        SqliteSynchronousMode.Normal => "NORMAL",
        SqliteSynchronousMode.Full => "FULL",
        _ => "NORMAL"
    };

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
