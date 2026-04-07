using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;
using Saturn.Data.LiteDbX.Tests.Entities;

namespace Saturn.Data.LiteDbX.Tests;

public class LiteDbXQueryableAsyncEnumerationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableLiteDb repo = fixture.Repository;
    private IReadonlyRepository ReadonlyRepo => repo;
    private IScopedRepository ScopedRepo => repo;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await repo.Delete<BasicEntity>(e => true);
        await repo.Delete<ChildEntity>(e => true);
        await repo.Delete<ParentScope>(e => true);
    }

    [Fact]
    public async Task IQueryable_ToAsyncEnumerable_Should_Stream_Without_Sync_Fallback()
    {
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });

        var scope1Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Scope1 A" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Scope1 B" }
        };

        var scope2Entity = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Scope2 A" };

        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entity);

        var results = await repo.IQueryable<ChildEntity>()
            .Where(e => e.Scope == WELL_KNOWN.Parent_Scope_1.Id)
            .OrderBy(e => e.Id)
            .ToAsyncEnumerable()
            .ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.All(results, entity => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, entity.Scope));
    }

    [Fact]
    public async Task Readonly_Many_Should_Materialize_With_ToListAsync()
    {
        var entities = new List<BasicEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Alpha" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Bravo" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Charlie" }
        };

        await repo.Insert(entities);

        var results = await (await ReadonlyRepo.Many(
            e => e.Name.Contains("a"),
            sortOrders: new[] { new SortOrder<BasicEntity>(e => e.Id, SortDirection.Ascending) }))
            .ToListAsync();

        Assert.Equal(3, results.Count);
        Assert.Equal(entities.Select(e => e.Id).OrderBy(id => id, StringComparer.Ordinal), results.Select(e => e.Id));
    }
}

