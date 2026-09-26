using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class SqliteIndexManagerTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<IndexedEntity>(entity => true);
    }

    [Fact]
    public async Task EnsureIndexes_Creates_Index()
    {
        await fixture.Repository.Insert(new IndexedEntity { Id = EntityIdGenerator.GenerateNewId(), Email = "a@b.com", Age = 30 });

        var definitions = new[]
        {
            new TestIndexDefinition<IndexedEntity>("ix_indexed_email", new IndexKey<IndexedEntity>(entity => entity.Email))
        };

        await fixture.Repository.EnsureIndexes(definitions);

        var indexes = await fixture.Repository.ListIndexesAsync("IndexedEntity");
        Assert.Contains("ix_indexed_email", indexes);
    }

    [Fact]
    public async Task Unique_Index_Raises_On_Duplicate()
    {
        await fixture.Repository.Insert(new IndexedEntity { Id = EntityIdGenerator.GenerateNewId(), Email = "dup@x.com", Age = 1 });

        var definitions = new[]
        {
            new TestIndexDefinition<IndexedEntity>("ix_indexed_email_unique", new IndexKey<IndexedEntity>(entity => entity.Email))
            {
                Options = new IndexOptions { Unique = true }
            }
        };

        await fixture.Repository.EnsureIndexes(definitions);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            fixture.Repository.Insert(new IndexedEntity { Id = EntityIdGenerator.GenerateNewId(), Email = "dup@x.com", Age = 2 }));
    }

    [Fact]
    public async Task ExpireAfter_Is_Reported_Unsupported()
    {
        var warnings = new List<string>();

        using var repository = new UnitTestableSqliteRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new SqliteRepositoryOptions
            {
                DataSource = Path.Combine(Path.GetTempPath(), $"saturn-sqlite-idx-{Guid.NewGuid():N}.db"),
                OnUnsupportedIndexOption = warnings.Add
            });

        repository.DropRecreateDatabase();

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
