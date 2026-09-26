using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class ScopedReadonlyContinuationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string Scope1 = "68bdd5525324ff2610c4361d";
    private const string Scope2 = "68bdd5525324ff2610c4361e";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<ChildEntity>(entity => true);
    }

    [Fact]
    public async Task Many_With_ContinueFrom_Id_Ascending_Stays_Within_Scope()
    {
        var scope1 = Enumerable.Range(1, 8)
            .Select(index => new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"Scope1 Child {index:D2}" })
            .ToList();

        var scope2 = Enumerable.Range(1, 4)
            .Select(index => new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"Scope2 Child {index:D2}" })
            .ToList();

        await fixture.Repository.Insert<ChildEntity, ParentScope>(Scope1, scope1);
        await fixture.Repository.Insert<ChildEntity, ParentScope>(Scope2, scope2);

        var sortOrders = new[] { new SortOrder<ChildEntity>(entity => entity.Id, SortDirection.Ascending) };

        var firstPage = await (await fixture.Repository.Many<ChildEntity, ParentScope>(
            Scope1, entity => true, pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        var secondPage = await (await fixture.Repository.Many<ChildEntity, ParentScope>(
            Scope1, entity => true, continueFrom: firstPage[^1].Id, pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        Assert.Equal(5, firstPage.Count);
        Assert.Equal(3, secondPage.Count);
        Assert.All(firstPage.Concat(secondPage), entity => Assert.Equal(Scope1, entity.ScopeId));
        Assert.Empty(firstPage.Select(entity => entity.Id).Intersect(secondPage.Select(entity => entity.Id)));

        var orderedIds = scope1.Select(entity => entity.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.Equal(orderedIds.Skip(5).Take(3), secondPage.Select(entity => entity.Id));
    }

    [Fact]
    public async Task Many_With_Non_Id_Sort_Ignores_ContinueFrom()
    {
        var scope1 = Enumerable.Range(1, 6)
            .Select(index => new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"Scope1 Child {index:D2}" })
            .ToList();

        await fixture.Repository.Insert<ChildEntity, ParentScope>(Scope1, scope1);

        var sortOrders = new[] { new SortOrder<ChildEntity>(entity => entity.Name, SortDirection.Ascending) };

        var firstPage = await (await fixture.Repository.Many<ChildEntity, ParentScope>(
            Scope1, entity => true, pageSize: 3, sortOrders: sortOrders)).ToListAsync();

        var withToken = await (await fixture.Repository.Many<ChildEntity, ParentScope>(
            Scope1, entity => true, continueFrom: firstPage[^1].Id, pageSize: 3, sortOrders: sortOrders)).ToListAsync();

        Assert.Equal(3, withToken.Count);
        Assert.All(withToken, entity => Assert.Equal(Scope1, entity.ScopeId));
    }

    [Fact]
    public async Task Count_With_ContinueFrom_Respects_Scope_And_Id()
    {
        var scope1 = Enumerable.Range(1, 10)
            .Select(index => new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"Scope1 Child {index:D2}" })
            .ToList();

        var scope2 = Enumerable.Range(1, 5)
            .Select(index => new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"Scope2 Child {index:D2}" })
            .ToList();

        await fixture.Repository.Insert<ChildEntity, ParentScope>(Scope1, scope1);
        await fixture.Repository.Insert<ChildEntity, ParentScope>(Scope2, scope2);

        var orderedIds = scope1.Select(entity => entity.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var token = orderedIds[4];

        var scopedCount = await fixture.Repository.Count<ChildEntity, ParentScope>(Scope1, entity => true, continueFrom: token);

        Assert.Equal(5, scopedCount);
    }
}
