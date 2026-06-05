using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb.Tests;

public class MongoIndexManagerTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableMongoDbRepository repo = fixture.Repository;
    private IRepositoryIndexManager IndexManager => repo;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.Delete<IndexedEntity>(e => true);
    }

    [Fact]
    public async Task EnsureIndexes_Should_Create_Unique_Single_Field_Index()
    {
        await IndexManager.EnsureIndexes(new[]
        {
            new TestIndexDefinition<IndexedEntity>(
                "ix-indexed-entity-name",
                [new IndexKey<IndexedEntity>(entity => entity.Name)],
                new IndexOptions
                {
                    Unique = true,
                    Sparse = true
                })
        });

        var indexes = await (await repo.GetRawCollection<IndexedEntity>().Indexes.ListAsync()).ToListAsync();
        var index = indexes.Single(document => document["name"] == "ix-indexed-entity-name");
        var key = index["key"].AsBsonDocument;

        Assert.Equal(1, key[nameof(IndexedEntity.Name)].AsInt32);
        Assert.True((bool)index["unique"].AsBoolean);
        Assert.True((bool)index["sparse"].AsBoolean);
    }

    [Fact]
    public async Task EnsureIndexes_Should_Create_Compound_Index_With_Configured_Directions()
    {
        await IndexManager.EnsureIndexes(new[]
        {
            new TestIndexDefinition<IndexedEntity>(
                "ix-indexed-entity-name-priority",
                [
                    new IndexKey<IndexedEntity>(entity => entity.Name),
                    new IndexKey<IndexedEntity>(entity => entity.Priority, IndexSortDirection.Descending)
                ],
                new IndexOptions())
        });

        var indexes = await (await repo.GetRawCollection<IndexedEntity>().Indexes.ListAsync()).ToListAsync();
        var index = indexes.Single(document => document["name"] == "ix-indexed-entity-name-priority");
        var key = index["key"].AsBsonDocument;

        Assert.Equal(1, key[nameof(IndexedEntity.Name)].AsInt32);
        Assert.Equal(-1, key[nameof(IndexedEntity.Priority)].AsInt32);
    }

    [Fact]
    public async Task EnsureIndexes_With_Empty_Definitions_Should_Do_Nothing()
    {
        await repo.Insert(new IndexedEntity
        {
            Id = EntityIdGenerator.GenerateNewId(),
            Name = "seed",
            Priority = 1
        });

        await IndexManager.EnsureIndexes<IndexedEntity>([]);

        var indexes = await (await repo.GetRawCollection<IndexedEntity>().Indexes.ListAsync()).ToListAsync();

        Assert.Single(indexes);
        Assert.Equal("_id_", (string)indexes[0]["name"].AsString);
    }

    private sealed class IndexedEntity : Entity
    {
        public string Name { get; set; } = string.Empty;

        public int Priority { get; set; }
    }

    private sealed class TestIndexDefinition<TItem> : IIndexDefinition<TItem> where TItem : Entity
    {
        public TestIndexDefinition(string name, IReadOnlyCollection<IIndexKey<TItem>> keys, IndexOptions options)
        {
            Name = name;
            Keys = keys;
            Options = options;
        }

        public string Name { get; }

        public IReadOnlyCollection<IIndexKey<TItem>> Keys { get; }

        public IndexOptions Options { get; }
    }
}



