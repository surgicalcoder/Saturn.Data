using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

public enum SqliteSynchronousMode
{
    Off,
    Normal,
    Full
}

public class SqliteRepositoryOptions
{
    public string DataSource { get; set; } = "saturn.db";

    public SqliteOpenMode Mode { get; set; } = SqliteOpenMode.ReadWriteCreate;

    public SqliteCacheMode Cache { get; set; } = SqliteCacheMode.Default;

    public bool Pooling { get; set; } = true;

    public string? EncryptionPassword { get; set; }

    public bool UseJsonB { get; set; }

    public bool StrictTranslation { get; set; } = true;

    public bool UseJsonMergePatch { get; set; }

    public bool EnableWal { get; set; } = true;

    public bool RequireWal { get; set; } = true;

    public int BusyTimeoutSeconds { get; set; } = 30;

    public int BusyTimeoutMs { get; set; } = 5000;

    public SqliteSynchronousMode Synchronous { get; set; } = SqliteSynchronousMode.Normal;

    public int WalAutoCheckpointPages { get; set; } = 1000;

    public long JournalSizeLimitBytes { get; set; } = 67108864;

    public bool AllowNestedTransactions { get; set; }

    public int BusyRetryCount { get; set; } = 5;

    public int BusyRetryBaseDelayMs { get; set; } = 25;

    public bool ServerSideProjection { get; set; }

    public bool EnableFullTextSearch { get; set; }

    public Action<SqliteConnection>? ConfigureConnection { get; set; }

    public Action<string>? OnUnsupportedIndexOption { get; set; }
}
