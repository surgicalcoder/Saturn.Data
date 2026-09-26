using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class ContinuationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
    }

    [Fact]
    public async Task Many_With_ContinueFrom_Id_Ascending_Returns_Next_Page()
    {
        var entities = Enumerable.Range(1, 12)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"item-{index:D2}" })
            .ToList();

        await fixture.Repository.Insert(entities);

        var sortOrders = new[] { new SortOrder<BasicEntity>(entity => entity.Id, SortDirection.Ascending) };

        var firstPage = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        var secondPage = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, continueFrom: firstPage[^1].Id, pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        Assert.Equal(5, firstPage.Count);
        Assert.Equal(5, secondPage.Count);
        Assert.Empty(firstPage.Select(entity => entity.Id).Intersect(secondPage.Select(entity => entity.Id)));

        var orderedIds = entities.Select(entity => entity.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.Equal(orderedIds.Skip(5).Take(5), secondPage.Select(entity => entity.Id));
    }

    [Fact]
    public async Task Many_With_Non_Id_Sort_Ignores_ContinueFrom()
    {
        var entities = Enumerable.Range(1, 6)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"item-{index:D2}" })
            .ToList();

        await fixture.Repository.Insert(entities);

        var sortOrders = new[] { new SortOrder<BasicEntity>(entity => entity.Name, SortDirection.Ascending) };

        var firstPage = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, pageSize: 3, sortOrders: sortOrders)).ToListAsync();

        var withToken = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, continueFrom: firstPage[^1].Id, pageSize: 3, sortOrders: sortOrders)).ToListAsync();

        Assert.Equal(3, withToken.Count);
        Assert.NotEmpty(withToken);
    }

    [Fact]
    public async Task Many_With_Malformed_ContinueFrom_Is_Ignored()
    {
        var entities = Enumerable.Range(1, 5)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"item-{index:D2}" })
            .ToList();

        await fixture.Repository.Insert(entities);

        var sortOrders = new[] { new SortOrder<BasicEntity>(entity => entity.Id, SortDirection.Ascending) };

        var expected = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        var actual = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, continueFrom: "not-a-valid-object-id", pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        Assert.Equal(expected.Select(entity => entity.Id), actual.Select(entity => entity.Id));
    }

    [Fact]
    public async Task Count_With_ContinueFrom_Applies_Id_Boundary()
    {
        var entities = Enumerable.Range(1, 10)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"item-{index:D2}" })
            .ToList();

        await fixture.Repository.Insert(entities);

        var orderedIds = entities.Select(entity => entity.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var token = orderedIds[4];

        var count = await fixture.Repository.Count<BasicEntity>(entity => true, continueFrom: token);

        Assert.Equal(5, count);
    }
}
