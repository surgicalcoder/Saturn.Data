using GoLive.Saturn.Data.Abstractions;

namespace Saturn.Data.LiteDb.Tests;

public class DatabaseFixture : IDisposable
{
    public UnitTestableLiteDb Repository { get; }

    public DatabaseFixture()
    {
        Saturn.Data.LiteDb.RuntimePatcher.Patcher.PatchLiteDB();
        Repository = new UnitTestableLiteDb(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name
        }, new ()
        {
            ConnectionString = "Filename=\"e:\\_scratch\\litedb-unit-tests.db\";Connection=Shared",
        });
        
        // Initialize database once
        Repository.DropRecreateDatabase();
    }

    public void Dispose()
    {
        Repository.DropRecreateDatabase();
    }
}
