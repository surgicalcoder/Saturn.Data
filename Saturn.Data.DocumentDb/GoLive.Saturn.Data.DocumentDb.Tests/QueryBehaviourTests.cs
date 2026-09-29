using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.DocumentDb.Tests;

public class QueryBehaviourTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
    }

    [Fact]
    public async Task String_Contains_Translates()
    {
        await fixture.Repository.Insert(
        [
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "alpha-one" },
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "beta-two" }
        ]);

        var matches = await (await fixture.Repository.Many<BasicEntity>(entity => entity.Name.Contains("alpha"), pageSize: 10)).ToListAsync();

        Assert.Single(matches);
    }

    [Fact]
    public async Task Id_List_Contains_Translates()
    {
        var first = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "one" };
        var second = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "two" };
        await fixture.Repository.Insert([first, second]);

        var ids = new List<string> { first.Id, second.Id };
        var matches = await (await fixture.Repository.ById<BasicEntity>(ids)).ToListAsync();

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public async Task Count_And_Sorting_Work()
    {
        await fixture.Repository.Insert(
        [
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "c" },
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "a" },
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "b" }
        ]);

        var count = await fixture.Repository.Count<BasicEntity>(entity => true);
        Assert.Equal(3, count);

        var sorted = await (await fixture.Repository.Many<BasicEntity>(
            entity => true,
            pageSize: 3,
            sortOrders: new[] { new SortOrder<BasicEntity>(entity => entity.Name, SortDirection.Ascending) })).ToListAsync();

        Assert.Equal(new[] { "a", "b", "c" }, sorted.Select(entity => entity.Name));
    }

    [Fact]
    public async Task Paging_Returns_Disjoint_Pages()
    {
        await fixture.Repository.Insert(Enumerable.Range(0, 10)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"page-{index:D2}" })
            .ToList());

        var sortOrders = new[] { new SortOrder<BasicEntity>(entity => entity.Id, SortDirection.Ascending) };

        var page1 = await (await fixture.Repository.Many<BasicEntity>(entity => true, pageSize: 4, pageNumber: 1, sortOrders: sortOrders)).ToListAsync();
        var page2 = await (await fixture.Repository.Many<BasicEntity>(entity => true, pageSize: 4, pageNumber: 2, sortOrders: sortOrders)).ToListAsync();

        Assert.Equal(4, page1.Count);
        Assert.Equal(4, page2.Count);
        Assert.Empty(page1.Select(entity => entity.Id).Intersect(page2.Select(entity => entity.Id)));
    }

    [Fact]
    public async Task Random_Returns_Requested_Count()
    {
        await fixture.Repository.Insert(Enumerable.Range(0, 10)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"rand-{index:D2}" })
            .ToList());

        var random = await (await fixture.Repository.Random<BasicEntity>(entity => true, count: 3)).ToListAsync();

        Assert.Equal(3, random.Count);
    }
}
