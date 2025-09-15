using GoLive.Saturn.Data.Abstractions;
using LiteDB.Async;

namespace Saturn.Data.LiteDb.Tests;

public class UnitTestableLiteDb(RepositoryOptions repositoryOptions, LiteDBRepositoryOptions liteDbRepositoryOptions)
    : LiteDbRepository(repositoryOptions, liteDbRepositoryOptions)
{
    public void DropRecreateDatabase()
    {
        database.Dispose();
        File.Delete("e:\\_scratch\\litedb-unit-tests.db");
        database = new LiteDatabaseAsync(liteDbOptions.ConnectionString, mapper);
    }
};