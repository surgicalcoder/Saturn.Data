using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.DocumentDb.Tests;

public class SmokeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Store_Roundtrips_An_Entity()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "smoke" };
        await fixture.Repository.Store.Insert(entity);

        var fetched = await fixture.Repository.Store.Get<BasicEntity>(entity.Id);

        Assert.NotNull(fetched);
        Assert.Equal("smoke", fetched!.Name);
    }

    [Fact]
    public void Capabilities_Probe_Reads()
    {
        Assert.False(string.IsNullOrWhiteSpace(fixture.Repository.Capabilities.BackendName));
        Assert.True(fixture.Repository.Capabilities.SupportsTransactions);
    }
}
