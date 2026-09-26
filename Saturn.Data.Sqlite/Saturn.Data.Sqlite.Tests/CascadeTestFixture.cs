using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared.Cascade;

namespace Saturn.Data.Sqlite.Tests;

public class CascadeTestFixture : IDisposable, ICascadeTestFixture<UnitTestableSqliteRepository>
{
    public UnitTestableSqliteRepository Repository { get; }

    public CascadeTestFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-sqlite-cascade-{Guid.NewGuid():N}.db");

        Repository = new UnitTestableSqliteRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new SqliteRepositoryOptions { DataSource = path });

        Repository.DropRecreateDatabase();
    }

    public void Dispose() => Repository.Dispose();
}
