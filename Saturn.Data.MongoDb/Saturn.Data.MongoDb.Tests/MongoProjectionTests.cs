using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.MongoDb.Tests.Entities;
using System.Linq.Expressions;

namespace Saturn.Data.MongoDb.Tests;

public class MongoProjectionTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableMongoDbRepository repo = fixture.Repository;

    private static readonly BasicEntity Alpha = new()
    {
        Id = "68c0aa2d5324ff2610c43691",
        Name = "Alpha"
    };

    private static readonly BasicEntity Bravo = new()
    {
        Id = "68c0aa2d5324ff2610c43692",
        Name = "Bravo"
    };

    private static readonly BasicEntity Charlie = new()
    {
        Id = "68c0aa2d5324ff2610c43693",
        Name = "Charlie"
    };

    private static readonly SoftProjectionEntity SoftActive = new()
    {
        Id = "68c0aa2d5324ff2610c43694",
        Name = "Soft-Active"
    };

    private static readonly SoftProjectionEntity SoftDeleted = new()
    {
        Id = "68c0aa2d5324ff2610c43695",
        Name = "Soft-Deleted",
        IsDeleted = true
    };

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.Delete<BasicEntity>(entity => entity.Id == Alpha.Id || entity.Id == Bravo.Id || entity.Id == Charlie.Id);
        await repo.HardDelete<SoftProjectionEntity>(entity => entity.Id == SoftActive.Id || entity.Id == SoftDeleted.Id);
    }

    [Fact]
    public async Task All_Projection_Should_Return_Selected_Values()
    {
        await SeedAsync();

        var results = await repo.All<BasicEntity, string>(entity => entity.Name);
        var names = await results.ToListAsync();

        Assert.Contains("Alpha", names);
        Assert.Contains("Bravo", names);
        Assert.Contains("Charlie", names);
    }

    [Fact]
    public async Task ById_Projection_Should_Return_Selected_Value()
    {
        await SeedAsync();

        var result = await repo.ById<BasicEntity, string>(Bravo.Id, entity => entity.Name);

        Assert.Equal("Bravo", result);
    }

    [Fact]
    public async Task Many_Projection_Should_Respect_Filter_Sort_And_Page_Size()
    {
        await SeedAsync();

        var results = await repo.Many<BasicEntity, string>(
            entity => entity.Id == Alpha.Id || entity.Id == Bravo.Id || entity.Id == Charlie.Id,
            entity => entity.Name,
            pageSize: 2,
            sortOrders: [SortOrder<BasicEntity>.Descending(entity => entity.Name)]);
        var names = await results.ToListAsync();

        Assert.Equal(["Charlie", "Bravo"], names);
    }

    [Fact]
    public async Task Many_Projection_With_WhereClause_Should_Return_Selected_Values()
    {
        await SeedAsync();

        var results = await repo.Many<BasicEntity, string>(
            new Dictionary<string, object>
            {
                [nameof(BasicEntity.Name)] = "Bravo"
            },
            entity => entity.Name);
        var names = await results.ToListAsync();

        Assert.Equal(["Bravo"], names);
    }

    [Fact]
    public async Task One_Projection_Should_Return_First_Selected_Value()
    {
        await SeedAsync();

        var result = await repo.One<BasicEntity, string>(
            entity => entity.Id == Alpha.Id || entity.Id == Bravo.Id || entity.Id == Charlie.Id,
            entity => entity.Name,
            sortOrders: [SortOrder<BasicEntity>.Ascending(entity => entity.Name)]);

        Assert.Equal("Alpha", result);
    }

    [Fact]
    public async Task Many_Projection_Should_Work_With_Static_Selector()
    {
        await SeedAsync();

        var results = await repo.Many<BasicEntity, BasicEntityNameView>(
            entity => entity.Id == Alpha.Id || entity.Id == Bravo.Id || entity.Id == Charlie.Id,
            BasicEntityNameView.Selector,
            sortOrders: [SortOrder<BasicEntity>.Ascending(entity => entity.Name)]);
        var names = (await results.ToListAsync()).Select(item => item.Name).ToList();

        Assert.Equal(["Alpha", "Bravo", "Charlie"], names);
    }

    [Fact]
    public async Task All_Projection_Should_Exclude_SoftDeleted_By_Default()
    {
        await SeedSoftDeleteAsync();

        var results = await repo.All<SoftProjectionEntity, SoftProjectionView>(SoftProjectionView.Selector);
        var names = (await results.ToListAsync()).Select(item => item.Name).ToList();

        Assert.Equal([SoftActive.Name], names);
    }

    [Fact]
    public async Task All_Projection_Should_Include_SoftDeleted_When_Requested()
    {
        await SeedSoftDeleteAsync();

        var results = await repo.All<SoftProjectionEntity, SoftProjectionView>(SoftProjectionView.Selector, includeDeleted: true);
        var names = (await results.ToListAsync()).Select(item => item.Name).OrderBy(name => name).ToList();

        Assert.Equal([SoftActive.Name, SoftDeleted.Name], names);
    }

    private async Task SeedAsync()
    {
        await repo.Insert([Alpha, Bravo, Charlie]);
    }

    private async Task SeedSoftDeleteAsync()
    {
        await repo.Insert([SoftActive, SoftDeleted]);
    }

    private sealed class BasicEntityNameView
    {
        public string Name { get; set; } = string.Empty;

        public static Expression<Func<BasicEntity, BasicEntityNameView>> Selector => source => new BasicEntityNameView
        {
            Name = source.Name
        };
    }

    private sealed class SoftProjectionView
    {
        public string Name { get; set; } = string.Empty;

        public static Expression<Func<SoftProjectionEntity, SoftProjectionView>> Selector => source => new SoftProjectionView
        {
            Name = source.Name
        };
    }

    private sealed class SoftProjectionEntity : BasicEntity, ISoftDeletable
    {
        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public string DeletedBy { get; set; } = string.Empty;
    }
}

