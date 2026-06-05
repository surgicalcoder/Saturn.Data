using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.MongoDb.Tests.Entities;
using System.Linq.Expressions;

namespace Saturn.Data.MongoDb.Tests;

public class MongoScopedProjectionTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableMongoDbRepository repo = fixture.Repository;

    private const string ScopeId = "68c0aa2d5324ff2610c437f1";
    private static readonly ParentScope Scope = new() { Id = ScopeId };

    private static readonly ChildEntity Alpha = new()
    {
        Id = "68c0aa2d5324ff2610c43701",
        Scope = ScopeId,
        Name = "Alpha"
    };

    private static readonly ChildEntity Bravo = new()
    {
        Id = "68c0aa2d5324ff2610c43702",
        Scope = ScopeId,
        Name = "Bravo"
    };

    private static readonly ChildEntity Charlie = new()
    {
        Id = "68c0aa2d5324ff2610c43703",
        Scope = ScopeId,
        Name = "Charlie"
    };

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.Delete<ChildEntity>(entity => entity.Scope == ScopeId);
        await repo.Delete<ParentScope>(entity => entity.Id == ScopeId);
    }

    [Fact]
    public async Task All_Scoped_Projection_Should_Return_Selected_Values()
    {
        await SeedAsync();

        var results = await repo.All<ChildEntity, ParentScope, string>(ScopeId, entity => entity.Name);
        var names = await results.ToListAsync();

        Assert.Contains("Alpha", names);
        Assert.Contains("Bravo", names);
        Assert.Contains("Charlie", names);
    }

    [Fact]
    public async Task ById_Scoped_Projection_Should_Return_Selected_Value()
    {
        await SeedAsync();

        var result = await repo.ById<ChildEntity, ParentScope, string>(ScopeId, Bravo.Id, entity => entity.Name);

        Assert.Equal("Bravo", result);
    }

    [Fact]
    public async Task ById_Multiple_Scoped_Projection_Should_Return_Selected_Values()
    {
        await SeedAsync();

        var results = await repo.ById<ChildEntity, ParentScope, string>(
            ScopeId,
            [Alpha.Id, Bravo.Id],
            entity => entity.Name);
        var names = await results.ToListAsync();

        Assert.Contains("Alpha", names);
        Assert.Contains("Bravo", names);
        Assert.DoesNotContain("Charlie", names);
    }

    [Fact]
    public async Task Many_Scoped_Projection_Should_Respect_Filter_Sort_And_Page_Size()
    {
        await SeedAsync();

        var results = await repo.Many<ChildEntity, ParentScope, string>(
            ScopeId,
            entity => entity.Name != "",
            entity => entity.Name,
            pageSize: 2,
            sortOrders: [SortOrder<ChildEntity>.Descending(entity => entity.Name)]);
        var names = await results.ToListAsync();

        Assert.Equal(["Charlie", "Bravo"], names);
    }

    [Fact]
    public async Task Many_Scoped_Projection_With_WhereClause_Should_Return_Selected_Values()
    {
        await SeedAsync();

        var results = await repo.Many<ChildEntity, ParentScope, string>(
            ScopeId,
            new Dictionary<string, object>
            {
                [nameof(ChildEntity.Name)] = "Bravo"
            },
            entity => entity.Name);
        var names = await results.ToListAsync();

        Assert.Equal(["Bravo"], names);
    }

    [Fact]
    public async Task One_Scoped_Projection_Should_Return_First_Selected_Value()
    {
        await SeedAsync();

        var result = await repo.One<ChildEntity, ParentScope, string>(
            ScopeId,
            entity => entity.Name != "",
            entity => entity.Name,
            sortOrders: [SortOrder<ChildEntity>.Ascending(entity => entity.Name)]);

        Assert.Equal("Alpha", result);
    }

    [Fact]
    public async Task One_Scoped_Projection_Should_Work_With_Static_Selector()
    {
        await SeedAsync();

        var result = await repo.One<ChildEntity, ParentScope, ChildNameView>(
            ScopeId,
            entity => entity.Name != "",
            ChildNameView.Selector,
            sortOrders: [SortOrder<ChildEntity>.Ascending(entity => entity.Name)]);

        Assert.Equal("Alpha", result.Name);
    }

    private async Task SeedAsync()
    {
        await repo.Insert([Scope]);
        await repo.Insert([Alpha, Bravo, Charlie]);
    }

    private sealed class ChildNameView
    {
        public string Name { get; set; } = string.Empty;

        public static Expression<Func<ChildEntity, ChildNameView>> Selector => source => new ChildNameView
        {
            Name = source.Name
        };
    }

}


