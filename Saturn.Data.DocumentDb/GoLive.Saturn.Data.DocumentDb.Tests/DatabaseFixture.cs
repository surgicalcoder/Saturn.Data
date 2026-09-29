using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;
using Shiny.DocumentDb;
using Shiny.DocumentDb.Sqlite;

namespace Saturn.Data.DocumentDb.Tests;

public class DatabaseFixture : IDisposable, IRepositoryTestFixture<UnitTestableDocumentDbRepository>
{
    public UnitTestableDocumentDbRepository Repository { get; }

    public bool SupportsTransactions => Repository.Capabilities.SupportsTransactions;

    public DatabaseFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-{Guid.NewGuid():N}.db");

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
