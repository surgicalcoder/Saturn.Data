using GoLive.Saturn.Data.ChangeTracking;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class ChangeSetPatchTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<ChangeFeedEntity>(entity => true);
    }

    [Fact]
    public async Task PatchChanges_Applies_Set_And_Increment()
    {
        var entity = new ChangeFeedEntity { Name = "before", Count = 1 };
        await fixture.Repository.Insert(entity);

        var changeSet = new EntityChangeSet
        {
            EntityType = nameof(ChangeFeedEntity),
            Id = entity.Id,
            ExpectedVersion = entity.Version,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Fields = new[]
            {
                new FieldChange { Path = "Name", Kind = ChangeKind.Set, NewValue = "after" },
                new FieldChange { Path = "Count", Kind = ChangeKind.Increment, NewValue = 4 }
            }
        };

        await fixture.Repository.PatchChanges<ChangeFeedEntity>(entity.Id, entity.Version, changeSet);

        var reloaded = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("after", reloaded.Name);
        Assert.Equal(5, reloaded.Count);
        Assert.Equal(1, reloaded.Version);
    }

    [Fact]
    public async Task PatchChanges_Applies_Unset()
    {
        var entity = new ChangeFeedEntity { Name = "unset-me", Count = 9 };
        await fixture.Repository.Insert(entity);

        var changeSet = new EntityChangeSet
        {
            EntityType = nameof(ChangeFeedEntity),
            Id = entity.Id,
            Fields = new[] { new FieldChange { Path = "Count", Kind = ChangeKind.Unset } }
        };

        await fixture.Repository.PatchChanges<ChangeFeedEntity>(entity.Id, null, changeSet);

        var reloaded = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Count);
    }
}
