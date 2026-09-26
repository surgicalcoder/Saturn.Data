using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class IncrementWritesTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<ChangeFeedEntity>(entity => true);
    }

    [Fact]
    public async Task Increment_Adds_Delta()
    {
        var entity = new ChangeFeedEntity { Name = "inc", Count = 0 };
        await fixture.Repository.Insert(entity);

        await fixture.Repository.Increment<ChangeFeedEntity>(entity.Id, item => item.Count, 5);

        var reloaded = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(5, reloaded.Count);
        Assert.Equal(1, reloaded.Version);
    }

    [Fact]
    public async Task Increment_Twice_Accumulates()
    {
        var entity = new ChangeFeedEntity { Name = "inc-twice", Count = 2 };
        await fixture.Repository.Insert(entity);

        await fixture.Repository.Increment<ChangeFeedEntity>(entity.Id, item => item.Count, 3);
        await fixture.Repository.Increment<ChangeFeedEntity>(entity.Id, item => item.Count, 4);

        var reloaded = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(9, reloaded.Count);
    }

    [Fact]
    public async Task Increment_Unknown_Id_Throws()
    {
        await Assert.ThrowsAsync<FailedToUpdateException>(() =>
            fixture.Repository.Increment<ChangeFeedEntity>("000000000000000000000000", item => item.Count, 5));
    }
}
