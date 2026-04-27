using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Stellar.Tests;

public class BasicProviderSpecificTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableDb repo = fixture.Repository;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.Delete<BasicEntity>(e => true);
        await repo.Delete<ChildEntity>(e => true);
        await repo.Delete<ParentScope>(e => true);
    }

    [Fact]
    public async Task AddGeneratesId()
    {
        var item = new BasicEntity { Name = "Test item with empty id" };
        await repo.Insert(item);
        Assert.False(string.IsNullOrWhiteSpace(item.Id));
    }

    [Fact]
    public async Task BulkInsertAndRetrieve()
    {
        var entities = new List<BasicEntity>
        {
            WellKnownData.BasicEntity1,
            WellKnownData.BasicEntity2,
            WellKnownData.BasicEntity3
        };

        await repo.Insert(entities);

        var allEntities = await repo.ById<BasicEntity>(new List<string>
        {
            WellKnownData.BasicEntity1.Id,
            WellKnownData.BasicEntity2.Id,
            WellKnownData.BasicEntity3.Id
        });

        Assert.Equal(3, await allEntities.CountAsync());
    }
}


