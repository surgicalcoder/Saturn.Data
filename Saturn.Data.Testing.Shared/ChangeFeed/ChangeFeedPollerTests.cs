using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Saturn.Data.Testing.Shared.Entities;
using Xunit;

namespace Saturn.Data.Testing.Shared.ChangeFeed;

public abstract class ChangeFeedPollerTests<TFixture, TRepository>(TFixture fixture) : IAsyncLifetime
    where TFixture : IRepositoryTestFixture<TRepository>, IChangeFeedTestFixture
    where TRepository : IRepository
{
    private const string Source = "test-source";

    protected TFixture Fixture { get; } = fixture;

    protected TRepository Repo { get; } = fixture.Repository;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Repo.HardDelete<ChangeFeedEntity>(e => true);
        await Repo.HardDelete<BasicEntity>(e => true);
    }

    [Fact]
    public async Task Drain_Delivers_Events_In_Sequence_Order()
    {
        var poller = new ChangeFeedPoller(Fixture.Sink);
        var received = new List<DataChangeEvent<ChangeFeedEntity>>();
        await poller.For<ChangeFeedEntity>(Source).SubscribeAsync((e, ct) => { received.Add(e); return ValueTask.CompletedTask; });

        var e1 = new ChangeFeedEntity { Name = "order-1" };
        var e2 = new ChangeFeedEntity { Name = "order-2" };
        var e3 = new ChangeFeedEntity { Name = "order-3" };
        await Repo.Insert(e1);
        await Repo.Insert(e2);
        await Repo.Insert(e3);

        await poller.DrainAsync();

        var mine = received.Where(e => new[] { e1.Id, e2.Id, e3.Id }.Any(id => e.EntityIds.Contains(id))).ToList();
        Assert.Equal(3, mine.Count);
        for (int i = 1; i < mine.Count; i++)
        {
            Assert.True(mine[i].Sequence > mine[i - 1].Sequence);
        }
    }

    [Fact]
    public async Task Drain_Advances_Watermark()
    {
        var poller = new ChangeFeedPoller(Fixture.Sink);
        var received = new List<DataChangeEvent<ChangeFeedEntity>>();
        await poller.For<ChangeFeedEntity>(Source).SubscribeAsync((e, ct) => { received.Add(e); return ValueTask.CompletedTask; });

        var first = new ChangeFeedEntity { Name = "wm-1" };
        await Repo.Insert(first);
        await poller.DrainAsync();

        var second = new ChangeFeedEntity { Name = "wm-2" };
        await Repo.Insert(second);
        await poller.DrainAsync();

        Assert.Contains(received, e => e.EntityIds.Contains(first.Id));
        Assert.Contains(received, e => e.EntityIds.Contains(second.Id));
    }

    [Fact]
    public async Task Drain_Replays_From_Zero_With_Same_ChangeId()
    {
        var entity = new ChangeFeedEntity { Name = "replay" };
        await Repo.Insert(entity);

        var firstPoller = new ChangeFeedPoller(Fixture.Sink);
        var firstDelivered = new List<DataChangeEvent<ChangeFeedEntity>>();
        await firstPoller.For<ChangeFeedEntity>(Source).SubscribeAsync((e, ct) => { firstDelivered.Add(e); return ValueTask.CompletedTask; });
        await firstPoller.DrainAsync();

        var secondPoller = new ChangeFeedPoller(Fixture.Sink);
        var secondDelivered = new List<DataChangeEvent<ChangeFeedEntity>>();
        await secondPoller.For<ChangeFeedEntity>(Source).SubscribeAsync((e, ct) => { secondDelivered.Add(e); return ValueTask.CompletedTask; });
        await secondPoller.DrainAsync();

        var firstEvent = firstDelivered.First(e => e.EntityIds.Contains(entity.Id));
        var secondEvent = secondDelivered.First(e => e.EntityIds.Contains(entity.Id));

        Assert.Equal(firstEvent.ChangeId, secondEvent.ChangeId);
        Assert.Single(firstDelivered, e => e.ChangeId == firstEvent.ChangeId);
        Assert.Single(secondDelivered, e => e.ChangeId == secondEvent.ChangeId);
    }

    [Fact]
    public async Task Drain_Filters_By_EntityType()
    {
        var poller = new ChangeFeedPoller(Fixture.Sink);
        var received = new List<DataChangeEvent<ChangeFeedEntity>>();
        await poller.For<ChangeFeedEntity>(Source).SubscribeAsync((e, ct) => { received.Add(e); return ValueTask.CompletedTask; });

        var feedEntity = new ChangeFeedEntity { Name = "typed-feed" };
        var otherEntity = new BasicEntity { Name = "typed-other" };
        await Repo.Insert(feedEntity);
        await Repo.Insert(otherEntity);

        await poller.DrainAsync();

        Assert.Contains(received, e => e.EntityIds.Contains(feedEntity.Id));
        Assert.DoesNotContain(received, e => e.EntityIds.Contains(otherEntity.Id));
    }
}
