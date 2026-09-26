using System.Text.Json;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class ProviderSpecificTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
    }

    [Fact]
    public async Task Document_Excludes_Transient_Members()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "doc-shape" };
        await fixture.Repository.Insert(entity);

        var document = await fixture.Repository.ReadDocumentAsync("BasicEntity", entity.Id);
        using var parsed = JsonDocument.Parse(document);
        var root = parsed.RootElement;

        Assert.False(root.TryGetProperty("Changes", out _));
        Assert.False(root.TryGetProperty("EnableChangeTracking", out _));
        Assert.False(root.TryGetProperty("_shortId", out _));
        Assert.True(root.TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task Document_RoundTrips_Id_And_Version()
    {
        var id = EntityIdGenerator.GenerateNewId();
        var entity = new BasicEntity { Id = id, Name = "roundtrip", Version = 3 };
        await fixture.Repository.Insert(entity);

        var fetched = await fixture.Repository.ById<BasicEntity>(id);

        Assert.NotNull(fetched);
        Assert.Equal(id, fetched.Id);
        Assert.Equal(3, fetched.Version);
        Assert.Equal("roundtrip", fetched.Name);
    }
}
