using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class PatchWritesTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<ChangeFeedEntity>(entity => true);
    }

    [Fact]
    public async Task Patch_Set_Updates_Field_And_Bumps_Version()
    {
        var entity = new ChangeFeedEntity { Name = "patch", Count = 1 };
        await fixture.Repository.Insert(entity);

        await fixture.Repository.Patch<ChangeFeedEntity>(entity.Id, jsonDocument: """{"$set":{"Count":5}}""");

        var reloaded = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(5, reloaded.Count);
        Assert.Equal(1, reloaded.Version);
    }

    [Fact]
    public async Task Patch_Unset_Removes_Field()
    {
        var entity = new ChangeFeedEntity { Name = "patch-unset", Count = 7 };
        await fixture.Repository.Insert(entity);

        await fixture.Repository.Patch<ChangeFeedEntity>(entity.Id, jsonDocument: """{"$unset":{"Count":true}}""");

        var reloaded = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(0, reloaded.Count);
    }

    [Fact]
    public async Task Patch_Unknown_Id_Throws()
    {
        await Assert.ThrowsAsync<FailedToUpdateException>(() =>
            fixture.Repository.Patch<ChangeFeedEntity>("000000000000000000000000", jsonDocument: """{"$set":{"Count":5}}"""));
    }

    [Fact]
    public async Task Patch_With_ExpectedVersion_Mismatch_Throws()
    {
        var entity = new ChangeFeedEntity { Name = "patch-version", Count = 1 };
        await fixture.Repository.Insert(entity);
        await fixture.Repository.Patch<ChangeFeedEntity>(entity.Id, jsonDocument: """{"$set":{"Count":2}}""");

        await Assert.ThrowsAsync<FailedToUpdateException>(() =>
            fixture.Repository.Patch<ChangeFeedEntity>(entity.Id, expectedVersion: 0, jsonDocument: """{"$set":{"Count":9}}"""));
    }

    [Fact]
    public async Task Patch_Plain_Object_Treats_Keys_As_Set()
    {
        var entity = new ChangeFeedEntity { Name = "patch-plain", Count = 1 };
        await fixture.Repository.Insert(entity);

        await fixture.Repository.Patch<ChangeFeedEntity>(entity.Id, jsonDocument: """{"Name":"renamed"}""");

        var reloaded = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("renamed", reloaded.Name);
    }
}
