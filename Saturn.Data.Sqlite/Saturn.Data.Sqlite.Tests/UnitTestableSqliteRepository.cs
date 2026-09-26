using GoLive.Saturn.Data.Abstractions;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite.Tests;

public sealed class UnitTestableSqliteRepository : SqliteRepository
{
    private readonly SqliteRepositoryOptions sqliteOptions;

    public UnitTestableSqliteRepository(RepositoryOptions repositoryOptions, SqliteRepositoryOptions sqliteOptions)
        : base(repositoryOptions, sqliteOptions)
    {
        this.sqliteOptions = sqliteOptions;
    }

    public string DatabasePath => sqliteOptions.DataSource;

    public void DropRecreateDatabase()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(DatabasePath))
        {
            File.Delete(DatabasePath);
        }

        if (File.Exists(DatabasePath + "-wal"))
        {
            File.Delete(DatabasePath + "-wal");
        }

        if (File.Exists(DatabasePath + "-shm"))
        {
            File.Delete(DatabasePath + "-shm");
        }

        initialized = false;
        InitializeAsync().GetAwaiter().GetResult();
    }

    public async Task<long> ProbeJson1Async(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json_extract('{\"a\":1}', '$.a');";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<string> ReadJournalModeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string ?? string.Empty;
    }

    public async Task<string> ReadDocumentAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT _doc FROM {SqliteRepository.Quote(collection)} WHERE _id = @id;";
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }
}
