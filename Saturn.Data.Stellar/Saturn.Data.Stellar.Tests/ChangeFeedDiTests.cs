using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Extensions.DependencyInjection;
using Saturn.Data.Stellar.ChangeFeed;
using Saturn.Data.Testing.Shared.ChangeFeed;
using Xunit;

namespace Saturn.Data.Stellar.Tests;

public class ChangeFeedDiTests(ChangeFeedTestFixture fixture) : IClassFixture<ChangeFeedTestFixture>
{
    [Fact]
    public async Task AddStellarChangeFeed_Registers_Sink_And_Poller()
    {
        var services = new ServiceCollection();
        services.AddStellarChangeFeed(fixture.Repository.Database, "test-source");
        using var provider = services.BuildServiceProvider();

        var sink = provider.GetRequiredService<IChangeFeedSink>();
        var poller = provider.GetRequiredService<ChangeFeedPoller>();

        Assert.IsType<StellarOutboxChangeFeedSink>(sink);
        Assert.NotNull(poller);

        var entityId = Guid.NewGuid().ToString("N");
        var delivered = new List<DataChangeEvent<ChangeFeedEntity>>();
        await poller.For<ChangeFeedEntity>("test-source").SubscribeAsync((e, ct) => { delivered.Add(e); return ValueTask.CompletedTask; });

        await sink.AppendAsync(new DataChangeEvent
        {
            ChangeId = Guid.NewGuid().ToString("N"),
            Source = "test-source",
            OccuredAtUtc = DateTime.UtcNow,
            EntityType = typeof(ChangeFeedEntity),
            Operation = RepositoryWriteOperation.Insert,
            Outcome = WriteOutcome.Inserted,
            EntityIds = new[] { entityId },
            Version = 1
        }, null, CancellationToken.None);

        await poller.DrainAsync();

        Assert.Contains(delivered, e => e.EntityIds.Contains(entityId));
    }
}
