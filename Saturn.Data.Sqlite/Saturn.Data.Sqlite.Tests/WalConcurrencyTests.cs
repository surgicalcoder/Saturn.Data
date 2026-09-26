using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class WalConcurrencyTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
    }

    [Fact]
    public async Task Committed_Writes_Are_Visible_On_A_Second_Connection()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "wal-visible" };
        await fixture.Repository.Insert(entity);

        var documents = await fixture.Repository.ReadAllDocumentsAsync("BasicEntity");
        Assert.Contains(documents, document => document.Contains(entity.Id));
    }

    [Fact]
    public async Task Rebuild_Completes_And_Keeps_Data()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "rebuild" };
        await fixture.Repository.Insert(entity);

        await fixture.Repository.RebuildAsync();

        Assert.NotNull(await fixture.Repository.ById<BasicEntity>(entity.Id));
    }

    [Fact]
    public async Task Journal_Mode_Is_Wal()
    {
        Assert.Equal("wal", await fixture.Repository.ReadJournalModeAsync());
    }

    [Fact]
    public async Task Concurrent_Writers_Serialize_Without_Losing_Data()
    {
        var entities = Enumerable.Range(1, 25)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"concurrent-{index:D2}" })
            .ToList();

        await Task.WhenAll(entities.Select(entity => fixture.Repository.Insert(entity)));

        var count = await fixture.Repository.Count<BasicEntity>(entity => true);
        Assert.Equal(25, count);
    }
}
