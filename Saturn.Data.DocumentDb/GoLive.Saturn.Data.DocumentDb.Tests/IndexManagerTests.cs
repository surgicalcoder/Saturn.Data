using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb.Tests;

public class IndexManagerTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<IndexedEntity>(entity => true);
    }

    [Fact]
    public async Task EnsureIndexes_Creates_Non_Unique_Index()
    {
        await fixture.Repository.Insert(new IndexedEntity { Id = EntityIdGenerator.GenerateNewId(), Email = "a@b.com", Age = 30 });

        var definitions = new[]
        {
            new TestIndexDefinition<IndexedEntity>("ix_indexed_email", new IndexKey<IndexedEntity>(entity => entity.Email))
        };

        await fixture.Repository.EnsureIndexes(definitions);

        var reloaded = await fixture.Repository.ById<IndexedEntity>(EntityIdGenerator.GenerateNewId());
        Assert.Null(reloaded);

        var found = await fixture.Repository.Many<IndexedEntity>(entity => entity.Email == "a@b.com", pageSize: 10);
        var list = await found.ToListAsync();

        Assert.Single(list);
    }

    [Fact]
    public async Task Unique_Index_Is_Reported_Unsupported()
    {
        var warnings = new List<string>();

        using var repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new DocumentDbRepositoryOptions
            {
                BackendName = "SqliteDatabaseProvider",
                DatabaseProvider = new Shiny.DocumentDb.Sqlite.SqliteDatabaseProvider($"Data Source={Path.Combine(Path.GetTempPath(), $"saturn-docdb-idx-{Guid.NewGuid():N}.db")}"),
                OnUnsupportedIndexOption = warnings.Add
            });

        repository.InitializeAsync().GetAwaiter().GetResult();

        var definitions = new[]
        {
            new TestIndexDefinition<IndexedEntity>("ix_unique_email", new IndexKey<IndexedEntity>(entity => entity.Email))
            {
                Options = new IndexOptions { Unique = true }
            }
        };

        await repository.EnsureIndexes(definitions);

        Assert.Single(warnings);
    }

    [Fact]
    public async Task ExpireAfter_Is_Reported_Unsupported()
    {
        var warnings = new List<string>();

        using var repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new DocumentDbRepositoryOptions
            {
                BackendName = "SqliteDatabaseProvider",
                DatabaseProvider = new Shiny.DocumentDb.Sqlite.SqliteDatabaseProvider($"Data Source={Path.Combine(Path.GetTempPath(), $"saturn-docdb-ttl-{Guid.NewGuid():N}.db")}"),
                OnUnsupportedIndexOption = warnings.Add
            });

        repository.InitializeAsync().GetAwaiter().GetResult();

        var definitions = new[]
        {
            new TestIndexDefinition<IndexedEntity>("ix_indexed_age", new IndexKey<IndexedEntity>(entity => entity.Age))
            {
                Options = new IndexOptions { HasExpireAfter = true, ExpireAfter = TimeSpan.FromMinutes(5) }
            }
        };

        await repository.EnsureIndexes(definitions);

        Assert.Single(warnings);
    }
}

public sealed class IndexedEntity : Entity
{
    public string Email { get; set; } = string.Empty;

    public int Age { get; set; }
}

public sealed class TestIndexDefinition<TItem> : IIndexDefinition<TItem> where TItem : Entity
{
    public TestIndexDefinition(string name, params IndexKey<TItem>[] keys)
    {
        Name = name;
        Keys = keys;
    }

    public string Name { get; }

    public IReadOnlyCollection<IIndexKey<TItem>> Keys { get; }

    public IndexOptions Options { get; init; } = new();
}
