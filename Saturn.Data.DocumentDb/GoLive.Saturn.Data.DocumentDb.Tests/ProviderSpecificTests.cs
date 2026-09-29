using System.Text.Json;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.ChangeFeed;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.DocumentDb.Tests;

public class ProviderSpecificTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
        await fixture.Repository.HardDelete<ChangeFeedEntity>(entity => true);
    }

    [Fact]
    public void Serialized_Document_Excludes_Transient_Members()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "doc-shape" };

        var json = fixture.Repository.SerializerForTests.Serialize(entity);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("Changes", out _));
        Assert.False(root.TryGetProperty("EnableChangeTracking", out _));
        Assert.False(root.TryGetProperty("_shortId", out _));
        Assert.True(root.TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task Id_Is_Stored_As_The_Original_24Hex_String()
    {
        var id = EntityIdGenerator.GenerateNewId();
        await fixture.Repository.Insert(new BasicEntity { Id = id, Name = "id-shape" });

        var fetched = await fixture.Repository.ById<BasicEntity>(id);

        Assert.NotNull(fetched);
        Assert.Equal(id, fetched!.Id);
        Assert.Equal("id-shape", fetched.Name);
    }

    [Fact]
    public async Task SoftDelete_Hides_Then_Restores()
    {
        var entity = new ChangeFeedEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "soft" };
        await fixture.Repository.Insert(entity);

        await fixture.Repository.Delete<ChangeFeedEntity>(entity.Id);

        Assert.Null(await fixture.Repository.ById<ChangeFeedEntity>(entity.Id));

        var included = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id, includeDeleted: true);
        Assert.NotNull(included);
        Assert.True(((ISoftDeletable)included!).IsDeleted);

        await fixture.Repository.Restore<ChangeFeedEntity>(entity.Id);

        Assert.NotNull(await fixture.Repository.ById<ChangeFeedEntity>(entity.Id));
    }

    [Fact]
    public async Task Count_Respects_SoftDelete_Filter()
    {
        var entity = new ChangeFeedEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "counted" };
        await fixture.Repository.Insert(entity);
        await fixture.Repository.Delete<ChangeFeedEntity>(entity.Id);

        var visible = await fixture.Repository.Count<ChangeFeedEntity>(item => item.Name == "counted");
        var all = await fixture.Repository.Count<ChangeFeedEntity>(item => item.Name == "counted", continueFrom: null, includeDeleted: true);

        Assert.Equal(0, visible);
        Assert.Equal(1, all);
    }
}
