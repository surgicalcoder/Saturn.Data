using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.ChangeTracking;
using Saturn.Data.Testing.Shared.ChangeFeed;
using Xunit;

namespace Saturn.Data.Testing.Shared.ChangeTracking;

public abstract class ChangeSetPatchContractTests<TFixture, TRepository> : IAsyncLifetime
    where TFixture : IRepositoryTestFixture<TRepository>
    where TRepository : IRepository
{
    protected ChangeSetPatchContractTests(TFixture fixture)
    {
        Fixture = fixture;
    }

    protected TFixture Fixture { get; }

    protected TRepository Repository => Fixture.Repository;

    public Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        await Repository.HardDelete<ChangeFeedEntity>(entity => true);
    }

    [Fact]
    public async Task PatchChanges_Applies_Set_And_Increment()
    {
        var entity = new ChangeFeedEntity { Name = "before", Count = 1 };
        await Repository.Insert(entity);

        var changeSet = ChangeSetFactory.Set(entity,
            ChangeSetFactory.Set("Name", "after"),
            ChangeSetFactory.Increment("Count", 4));

        await Repository.PatchChanges<ChangeFeedEntity>(entity.Id, entity.Version, changeSet);

        var reloaded = await Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("after", reloaded.Name);
        Assert.Equal(5, reloaded.Count);
    }

    [Fact]
    public async Task PatchChanges_Applies_Unset()
    {
        var entity = new ChangeFeedEntity { Name = "unset", Count = 9 };
        await Repository.Insert(entity);

        var changeSet = ChangeSetFactory.Set(entity, ChangeSetFactory.Unset("Count"));

        await Repository.PatchChanges<ChangeFeedEntity>(entity.Id, null, changeSet);

        var reloaded = await Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Count);
    }

    [Fact]
    public async Task PatchChanges_Bumps_Version()
    {
        var entity = new ChangeFeedEntity { Name = "version", Count = 1 };
        await Repository.Insert(entity);

        var changeSet = ChangeSetFactory.Set(entity, ChangeSetFactory.Set("Name", "bumped"));

        await Repository.PatchChanges<ChangeFeedEntity>(entity.Id, entity.Version, changeSet);

        var reloaded = await Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.Version > (entity.Version ?? 0));
    }

    [Fact]
    public async Task PatchChanges_With_Stale_Version_Throws()
    {
        var entity = new ChangeFeedEntity { Name = "stale", Count = 1 };
        await Repository.Insert(entity);

        await Repository.PatchChanges<ChangeFeedEntity>(entity.Id, entity.Version, ChangeSetFactory.Set(entity, ChangeSetFactory.Set("Name", "first")));

        var stale = ChangeSetFactory.Set(entity, ChangeSetFactory.Set("Name", "second"));

        await Assert.ThrowsAsync<FailedToUpdateException>(() =>
            Repository.PatchChanges<ChangeFeedEntity>(entity.Id, 0, stale));
    }
}
