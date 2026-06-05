using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.MongoDb.Tests.Entities;

namespace Saturn.Data.MongoDb.Tests;

public sealed class MongoScopedProjectionParityTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableMongoDbRepository repo = fixture.Repository;

    private const string PrimaryScopeId = "68c0aa2d5324ff2610c43811";
    private const string OtherPrimaryScopeId = "68c0aa2d5324ff2610c43812";
    private const string SecondScopeValue = "68c0aa2d5324ff2610c43821";
    private const string OtherSecondScopeValue = "68c0aa2d5324ff2610c43822";

    private static readonly PrimaryScopeEntity PrimaryScope = new() { Id = PrimaryScopeId, Name = "Primary" };
    private static readonly PrimaryScopeEntity OtherPrimaryScope = new() { Id = OtherPrimaryScopeId, Name = "Other Primary" };
    private static readonly SecondScopeEntity SecondScope = new() { Id = SecondScopeValue, Name = "Second" };
    private static readonly SecondScopeEntity OtherSecondScope = new() { Id = OtherSecondScopeValue, Name = "Other Second" };

    private static readonly StrongSecondScopedEntity StrongMatching = new()
    {
        Id = "68c0aa2d5324ff2610c43831",
        Scope = PrimaryScopeId,
        SecondScope = SecondScopeValue,
        Name = "Strong-Match"
    };

    private static readonly StrongSecondScopedEntity StrongPrimaryMismatch = new()
    {
        Id = "68c0aa2d5324ff2610c43832",
        Scope = OtherPrimaryScopeId,
        SecondScope = SecondScopeValue,
        Name = "Strong-Primary-Mismatch"
    };

    private static readonly StrongSecondScopedEntity StrongSecondMismatch = new()
    {
        Id = "68c0aa2d5324ff2610c43833",
        Scope = PrimaryScopeId,
        SecondScope = OtherSecondScopeValue,
        Name = "Strong-Second-Mismatch"
    };

    private static readonly WeakScopedProjectionEntity WeakMatching = new()
    {
        Id = "68c0aa2d5324ff2610c43841",
        ScopeId = PrimaryScopeId,
        Name = "Weak-Match"
    };

    private static readonly WeakScopedProjectionEntity WeakMismatch = new()
    {
        Id = "68c0aa2d5324ff2610c43842",
        ScopeId = OtherPrimaryScopeId,
        Name = "Weak-Mismatch"
    };

    private static readonly WeakSecondScopedProjectionEntity WeakSecondMatching = new()
    {
        Id = "68c0aa2d5324ff2610c43851",
        ScopeId = PrimaryScopeId,
        SecondScopeId = SecondScopeValue,
        Name = "Weak-Second-Match"
    };

    private static readonly WeakSecondScopedProjectionEntity WeakSecondMismatch = new()
    {
        Id = "68c0aa2d5324ff2610c43852",
        ScopeId = PrimaryScopeId,
        SecondScopeId = OtherSecondScopeValue,
        Name = "Weak-Second-Mismatch"
    };

    private static readonly ParentScope TransparentParentScope = new() { Id = "68c0aa2d5324ff2610c43861", Name = "Transparent Parent" };

    private static readonly ChildEntity TransparentChildMatch = new()
    {
        Id = "68c0aa2d5324ff2610c43862",
        Scope = TransparentParentScope.Id,
        Name = "Transparent-Match"
    };

    private static readonly ChildEntity TransparentChildMismatch = new()
    {
        Id = "68c0aa2d5324ff2610c43863",
        Scope = "68c0aa2d5324ff2610c43864",
        Name = "Transparent-Mismatch"
    };

    public async Task InitializeAsync()
    {
        await CleanupAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupAsync();
    }

    [Fact]
    public async Task Many_SecondScoped_Projection_Should_Filter_By_Both_Scopes()
    {
        await SeedSecondScopedAsync();

        var primary = new Ref<PrimaryScopeEntity>(PrimaryScopeId);
        var secondary = new Ref<SecondScopeEntity>(SecondScopeValue);

        var result = await repo.Many<StrongSecondScopedEntity, SecondScopeEntity, PrimaryScopeEntity, string>(
            primary,
            secondary,
            item => item.Name != "",
            item => item.Name,
            sortOrders: [SortOrder<StrongSecondScopedEntity>.Ascending(item => item.Name)]);
        var names = await result.ToListAsync();

        Assert.Equal([StrongMatching.Name], names);
    }

    [Fact]
    public async Task Many_WeakScoped_Projection_Should_Filter_By_Scope()
    {
        await SeedWeakScopedAsync();

        var result = await repo.Many<WeakScopedProjectionEntity, string>(
            PrimaryScopeId,
            item => item.Name.Contains("Weak"),
            item => item.Name,
            sortOrders: [SortOrder<WeakScopedProjectionEntity>.Ascending(item => item.Name)]);
        var names = await result.ToListAsync();

        Assert.Equal([WeakMatching.Name], names);
    }

    [Fact]
    public async Task ById_WeakSecondScoped_Projection_Should_Respect_Scopes()
    {
        await SeedWeakSecondScopedAsync();

        var result = await repo.ById<WeakSecondScopedProjectionEntity, string>(
            PrimaryScopeId,
            SecondScopeValue,
            WeakSecondMatching.Id,
            item => item.Name);

        Assert.Equal(WeakSecondMatching.Name, result);
    }

    [Fact]
    public async Task All_TransparentScoped_Projection_Should_Use_Configured_Scope()
    {
        await SeedTransparentScopedAsync();

        using var transparentRepo = new UnitTestableMongoDbRepository(
            new RepositoryOptions
            {
                GetCollectionName = type => type.Name,
                TransparentScopeProvider = type => type == typeof(ParentScope) ? TransparentParentScope.Id : PrimaryScopeId
            },
            new MongoDbRepositoryOptions
            {
                ConnectionString = "mongodb://localhost:27017/UnitTests"
            });

        var result = await transparentRepo.All<ChildEntity, ParentScope, string>(item => item.Name);
        var names = await result.ToListAsync();

        Assert.Equal([TransparentChildMatch.Name], names);
    }

    private async Task SeedSecondScopedAsync()
    {
        await repo.Insert([PrimaryScope, OtherPrimaryScope]);
        await repo.Insert([SecondScope, OtherSecondScope]);
        await repo.Insert([StrongMatching, StrongPrimaryMismatch, StrongSecondMismatch]);
    }

    private async Task SeedWeakScopedAsync()
    {
        await repo.Insert([WeakMatching, WeakMismatch]);
    }

    private async Task SeedWeakSecondScopedAsync()
    {
        await repo.Insert([WeakSecondMatching, WeakSecondMismatch]);
    }

    private async Task SeedTransparentScopedAsync()
    {
        await repo.Insert([TransparentParentScope]);
        await repo.Insert([TransparentChildMatch, TransparentChildMismatch]);
    }

    private async Task CleanupAsync()
    {
        await repo.Delete<StrongSecondScopedEntity>(item =>
            item.Id == StrongMatching.Id ||
            item.Id == StrongPrimaryMismatch.Id ||
            item.Id == StrongSecondMismatch.Id);

        await repo.Delete<PrimaryScopeEntity>(item => item.Id == PrimaryScope.Id || item.Id == OtherPrimaryScope.Id);
        await repo.Delete<SecondScopeEntity>(item => item.Id == SecondScope.Id || item.Id == OtherSecondScope.Id);

        await repo.Delete<WeakScopedProjectionEntity>(item => item.Id == WeakMatching.Id || item.Id == WeakMismatch.Id);

        await repo.Delete<WeakSecondScopedProjectionEntity>(item =>
            item.Id == WeakSecondMatching.Id ||
            item.Id == WeakSecondMismatch.Id);

        await repo.Delete<ChildEntity>(item => item.Id == TransparentChildMatch.Id || item.Id == TransparentChildMismatch.Id);
        await repo.Delete<ParentScope>(item => item.Id == TransparentParentScope.Id);
    }

    private sealed class PrimaryScopeEntity : Entity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SecondScopeEntity : Entity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class StrongSecondScopedEntity : SecondScopedEntity<SecondScopeEntity, PrimaryScopeEntity>
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class WeakScopedProjectionEntity : Entity, IScopedById
    {
        public string ScopeId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class WeakSecondScopedProjectionEntity : Entity, ISecondScopedById
    {
        public string ScopeId { get; set; } = string.Empty;

        public string SecondScopeId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}



