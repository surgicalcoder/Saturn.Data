using System;
using System.Linq;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using Xunit;

namespace Saturn.Data.Testing.Shared.ChangeFeed;

public abstract class ChangeFeedContractTests<TFixture, TRepository>(TFixture fixture) : IAsyncLifetime
    where TFixture : IRepositoryTestFixture<TRepository>, IChangeFeedTestFixture
    where TRepository : IRepository
{
    protected TRepository Repo { get; } = fixture.Repository;

    protected RecordingWriteBehavior Recorder { get; } = fixture.Recorder;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Repo.HardDelete<ChangeFeedEntity>(e => true);
        Recorder.Clear();
    }

    [Fact]
    public async Task Single_Insert_Fires_AfterInsert()
    {
        var entity = new ChangeFeedEntity { Name = "one" };
        await Repo.Insert(entity);

        var call = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Insert));
        Assert.Equal(WriteOutcome.Inserted, call.Outcome);
        Assert.Equal(entity.Id, Assert.Single(call.EntityIds));
    }

    [Fact]
    public async Task Bulk_Insert_Fires_Once()
    {
        var entities = Enumerable.Range(0, 10).Select(i => new ChangeFeedEntity { Name = $"bulk{i}" }).ToList();
        await Repo.Insert(entities);

        var call = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Insert));
        Assert.Equal(10, call.AffectedCount);
        Assert.Equal(10, call.EntityIds.Count);
    }

    [Fact]
    public async Task Update_With_No_Match_Throws_And_Does_Not_Fire_AfterUpdate()
    {
        var entity = new ChangeFeedEntity { Name = "x" };
        await Repo.Insert(entity);
        Recorder.Clear();

        await Assert.ThrowsAsync<FailedToUpdateException>(() =>
            Repo.Update<ChangeFeedEntity>(e => e.Id == "000000000000000000000000", new ChangeFeedEntity { Id = entity.Id, Name = "y" }));

        Assert.Empty(Recorder.AfterResults(RepositoryWriteOperation.Update));
    }

    [Fact]
    public async Task Upsert_Reports_WasCreated()
    {
        var entity = new ChangeFeedEntity { Name = "upsert" };
        await Repo.Upsert(entity);

        var created = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Upsert));
        Assert.True(created.WasCreated);

        Recorder.Clear();
        entity.Name = "upsert-again";
        await Repo.Upsert(entity);

        var replaced = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Upsert));
        Assert.False(replaced.WasCreated);
    }

    [Fact]
    public async Task Filter_Soft_Delete_Fires_AfterDelete_With_MatchedIds()
    {
        var entity = new ChangeFeedEntity { Name = "softdel" };
        await Repo.Insert(entity);
        Recorder.Clear();

        await Repo.Delete<ChangeFeedEntity>(e => e.Id == entity.Id);

        var call = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Delete));
        Assert.Contains(entity.Id, call.MatchedIds);
    }

    [Fact]
    public async Task HardDelete_Fires_AfterHardDelete_And_Removes()
    {
        var entity = new ChangeFeedEntity { Name = "harddel" };
        await Repo.Insert(entity);
        Recorder.Clear();

        await Repo.HardDelete<ChangeFeedEntity>(entity.Id);

        var call = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.HardDelete));
        Assert.Equal(entity.Id, Assert.Single(call.EntityIds));
        Assert.Null(await Repo.ById<ChangeFeedEntity>(entity.Id));
    }

    [Fact]
    public async Task Restore_Fires_AfterRestore_And_Restores()
    {
        var entity = new ChangeFeedEntity { Name = "restore" };
        await Repo.Insert(entity);
        await Repo.Delete<ChangeFeedEntity>(entity.Id);
        Assert.Null(await Repo.ById<ChangeFeedEntity>(entity.Id));
        Recorder.Clear();

        await Repo.Restore<ChangeFeedEntity>(entity.Id);

        var call = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Restore));
        Assert.Contains(entity.Id, call.MatchedIds);
        Assert.NotNull(await Repo.ById<ChangeFeedEntity>(entity.Id));
    }

    [Fact]
    public async Task Patch_And_Increment_Map_Operations_And_Bump_Version()
    {
        var entity = new ChangeFeedEntity { Name = "patch" };
        await Repo.Insert(entity);
        var before = await Repo.ById<ChangeFeedEntity>(entity.Id);
        Recorder.Clear();

        await Repo.Patch<ChangeFeedEntity>(entity.Id, jsonDocument: "{\"$set\":{\"Name\":\"patched\"}}");

        var patch = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Patch));
        Assert.Equal(WriteOutcome.Patched, patch.Outcome);
        var afterPatch = await Repo.ById<ChangeFeedEntity>(entity.Id);
        Assert.Equal("patched", afterPatch.Name);
        Assert.Equal((before.Version ?? 0) + 1, afterPatch.Version);

        Recorder.Clear();
        await Repo.Increment<ChangeFeedEntity>(entity.Id, e => e.Count, 5);

        var increment = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Increment));
        Assert.Equal(WriteOutcome.Incremented, increment.Outcome);
        var afterIncrement = await Repo.ById<ChangeFeedEntity>(entity.Id);
        Assert.Equal(5, afterIncrement.Count);
    }
}
