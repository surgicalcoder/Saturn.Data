using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Extensions.DependencyInjection;
using Saturn.Data.Sqlite.ChangeFeed;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class ChangeFeedDiTests(ChangeFeedTestFixture fixture) : IClassFixture<ChangeFeedTestFixture>
{
    [Fact]
    public async Task AddSqliteChangeFeed_Registers_Sink_And_Poller()
    {
        var services = new ServiceCollection();
        services.AddSqliteChangeFeed(fixture.Repository, "di-source");
        using var provider = services.BuildServiceProvider();

        var sink = provider.GetRequiredService<IChangeFeedSink>();
        var poller = provider.GetRequiredService<ChangeFeedPoller>();

        Assert.IsType<SqliteOutboxChangeFeedSink>(sink);
        Assert.NotNull(poller);

        var entityId = Guid.NewGuid().ToString("N");
        var delivered = new List<DataChangeEvent<ChangeFeedEntity>>();

        await poller.For<ChangeFeedEntity>("di-source").SubscribeAsync((change, ct) =>
        {
            delivered.Add(change);
            return ValueTask.CompletedTask;
        });

        await sink.AppendAsync(new DataChangeEvent
        {
            ChangeId = Guid.NewGuid().ToString("N"),
            Source = "di-source",
            OccuredAtUtc = DateTime.UtcNow,
            EntityType = typeof(ChangeFeedEntity),
            Operation = RepositoryWriteOperation.Insert,
            Outcome = WriteOutcome.Inserted,
            EntityIds = new[] { entityId },
            Version = 1
        }, null!, CancellationToken.None);

        await poller.DrainAsync();

        Assert.Contains(delivered, change => change.EntityIds.Contains(entityId));
    }
}
