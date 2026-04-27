using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;

namespace Saturn.Data.LiteDbX.Tests;

public class DatabaseFixture : IDisposable, IRepositoryTestFixture<UnitTestableLiteDb>
{
    public UnitTestableLiteDb Repository { get; }

    public DatabaseFixture()
    {
//        Saturn.Data.LiteDbX.RuntimePatcher.Patcher.PatchLiteDB();
        Repository = new UnitTestableLiteDb(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name
        }, new ()
        {
            ConnectionString = "Filename=\"e:\\_scratch\\litedb-unit-tests.db\";Connection=LockFile",
        });
        
        // Initialize database once
        Repository.DropRecreateDatabase();
    }

    public void Dispose()
    {
        Repository.DropRecreateDatabase();
    }
}
