using Saturn.Data.Stellar.Tests.Entities;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar.Tests;

public class ValueTupleSerializationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableDb repo = fixture.Repository;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await repo.Delete<BasicEntity>(e => true);
    }

    [Fact]
    public async Task Insert_MultipleEntities_DoesNotThrow()
    {
        var entries = Enumerable.Range(1, 10)
            .Select(i => new BasicEntity { Name = $"test_{i}" })
            .ToList();

        await repo.Insert(entries);

        var results = new List<BasicEntity>();
        await foreach (var item in await repo.All<BasicEntity>())
        {
            results.Add(item);
        }
        Assert.Equal(10, results.Count);
    }
}
