using GoLive.Saturn.Data.Migrations;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IMigrationStoreSource
{
    public IMigrationStore CreateMigrationStore()
        => new SqliteMigrationStore(connectionFactory, serializer.JsonOptions, EnsureTableAsync, knownTables);
}
