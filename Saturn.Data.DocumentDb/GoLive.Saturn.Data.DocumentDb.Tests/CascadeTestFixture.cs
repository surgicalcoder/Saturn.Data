using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared.Cascade;
using Shiny.DocumentDb.Sqlite;

namespace Saturn.Data.DocumentDb.Tests;

public class CascadeTestFixture : IDisposable, ICascadeTestFixture<UnitTestableDocumentDbRepository>
{
    public UnitTestableDocumentDbRepository Repository { get; }

    public CascadeTestFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-cascade-{Guid.NewGuid():N}.db");

        Repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new DocumentDbRepositoryOptions
            {
                BackendName = nameof(SqliteDatabaseProvider),
                DatabaseProvider = new SqliteDatabaseProvider($"Data Source={path}")
            });

        Repository.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => Repository.Dispose();
}
