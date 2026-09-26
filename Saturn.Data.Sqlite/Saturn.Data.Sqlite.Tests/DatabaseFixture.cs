using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;

namespace Saturn.Data.Sqlite.Tests;

public class DatabaseFixture : IDisposable, IRepositoryTestFixture<UnitTestableSqliteRepository>
{
    public UnitTestableSqliteRepository Repository { get; }

    public DatabaseFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-sqlite-{Guid.NewGuid():N}.db");

        Repository = new UnitTestableSqliteRepository(
            new RepositoryOptions
            {
                GetCollectionName = type => type.Name
            },
            new SqliteRepositoryOptions
            {
                DataSource = path
            });

        Repository.DropRecreateDatabase();
    }

    public void Dispose()
    {
        Repository.Dispose();
    }
}
