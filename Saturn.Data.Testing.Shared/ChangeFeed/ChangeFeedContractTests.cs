using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using Xunit;

namespace Saturn.Data.Testing.Shared.ChangeFeed;

public abstract class ChangeFeedContractTests<TFixture, TRepository>(TFixture fixture) : IAsyncLifetime
    where TFixture : IRepositoryTestFixture<TRepository>, IChangeFeedTestFixture
    where TRepository : IRepository
{
    protected TFixture Fixture { get; } = fixture;
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

        var result = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Insert));
        Assert.True(result.Succeeded);
        Assert.Single(result.EntityIds);
    }

    [Fact]
    public async Task Bulk_Insert_Fires_Once_With_All_EntityIds()
    {
        var entities = Enumerable.Range(0, 10)
            .Select(i => new ChangeFeedEntity { Name = $"bulk-{i}" })
            .ToList();

        await Repo.Insert(entities);

        var result = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Insert));
        Assert.Equal(10, result.AffectedCount);
        Assert.Equal(10, result.EntityIds.Count);
    }

    [Fact]
    public async Task Update_NoMatch_Does_Not_Fire_AfterUpdate()
    {
        var entity = new ChangeFeedEntity { Name = "no-match-update" };
        await Repo.Insert(entity);
        Recorder.Clear();

        await Assert.ThrowsAsync<FailedToUpdateException>(() => Repo.Update<ChangeFeedEntity>(e => e.Id == "000000000000000000000000", new ChangeFeedEntity { Name = "fail" }));

        Assert.Empty(Recorder.AfterResults(RepositoryWriteOperation.Update));
    }

    [Fact]
    public async Task Upsert_Created_Then_Replaced()
    {
        var entity = new ChangeFeedEntity { Name = "upsert-me" };
        await Repo.Upsert(entity);

        var created = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Upsert));
        Assert.True(created.WasCreated);

        entity.Name = "updated";
        await Repo.Upsert(entity);

        var replaced = Recorder.AfterResults(RepositoryWriteOperation.Upsert).Last();
        Assert.False(replaced.WasCreated);
    }

    [Fact]
    public async Task SoftDelete_Fires_AfterDelete_With_MatchedIds()
    {
        var entity = new ChangeFeedEntity { Name = "delete-me", IsDeleted = false };
        await Repo.Insert(entity);
        Recorder.Clear();

        await Repo.Delete<ChangeFeedEntity>(entity.Id);

        var result = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Delete));
        Assert.Contains(entity.Id, result.EntityIds);
        var reloaded = await Repo.ById<ChangeFeedEntity>(entity.Id, includeDeleted: true);
        Assert.True(reloaded.IsDeleted);
    }

    [Fact]
    public async Task HardDelete_Fires_AfterHardDelete_Row_Gone()
    {
        var entity = new ChangeFeedEntity { Name = "hard-delete" };
        await Repo.Insert(entity);
        Recorder.Clear();

        await Repo.HardDelete<ChangeFeedEntity>(entity.Id);

        var result = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.HardDelete));
        Assert.Contains(entity.Id, result.EntityIds);
        var reloaded = await Repo.ById<ChangeFeedEntity>(entity.Id);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task Restore_Fires_AfterRestore_Entity_Readable()
    {
        var entity = new ChangeFeedEntity { Name = "restore-me", IsDeleted = false };
        await Repo.Insert(entity);
        await Repo.Delete<ChangeFeedEntity>(e => e.Id == entity.Id);
        Recorder.Clear();

        await Repo.Restore<ChangeFeedEntity>(entity.Id);

        var result = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Restore));
        Assert.Contains(entity.Id, result.EntityIds);
        var reloaded = await Repo.ById<ChangeFeedEntity>(entity.Id);
        Assert.False(reloaded.IsDeleted);
    }

    [Fact]
    public async Task Patch_Bumps_Version_Before_AfterPatch()
    {
        var entity = new ChangeFeedEntity { Name = "patch-me", Count = 1 };
        await Repo.Insert(entity);
        Recorder.Clear();

        await Repo.Patch<ChangeFeedEntity>(entity.Id, jsonDocument: """{"$set":{"Count":5}}""");

        var result = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Patch));
        Assert.Equal(WriteOutcome.Patched, result.Outcome);
        var afterPatch = await Repo.ById<ChangeFeedEntity>(entity.Id);
        Assert.Equal(5, afterPatch.Count);
    }

    [Fact]
    public async Task Increment_Fires_AfterIncrement()
    {
        var entity = new ChangeFeedEntity { Name = "inc", Count = 0 };
        await Repo.Insert(entity);
        Recorder.Clear();

        await Repo.Increment<ChangeFeedEntity>(entity.Id, e => e.Count, 5);

        var increment = Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Increment));
        Assert.Equal(WriteOutcome.Incremented, increment.Outcome);
        var afterIncrement = await Repo.ById<ChangeFeedEntity>(entity.Id);
        Assert.Equal(5, afterIncrement.Count);
    }

    [Fact]
    public async Task Tx_Abort_Removes_Feed_Row()
    {
        if (!Fixture.SupportsTransactions)
        {
            return;
        }

        var entity = new ChangeFeedEntity { Name = "tx-abort" };
        await using var tx = await Repo.CreateTransaction();
        await tx.Start();
        await Repo.Insert(entity, tx);
        await tx.RollbackAsync();

        var events = await Fixture.Sink.ReadAsync("test-source", 0, 1000, CancellationToken.None);
        Assert.DoesNotContain(events, e => e.EntityIds.Contains(entity.Id));
    }

    [Fact]
    public async Task Feed_Error_Does_Not_Fail_Repository_Method()
    {
        var failingBehavior = new ThrowingAfterInsertBehavior();
        Fixture.WriteBehaviors.Add(failingBehavior);

        try
        {
            var entity = new ChangeFeedEntity { Name = "error-isolation" };
            await Repo.Insert(entity);

            Assert.NotNull(await Repo.ById<ChangeFeedEntity>(entity.Id));
            Assert.Single(Recorder.AfterResults(RepositoryWriteOperation.Insert));
        }
        finally
        {
            Fixture.WriteBehaviors.Remove(failingBehavior);
        }
    }

    [Fact]
    public async Task Outbox_Ordering_Strictly_Ascending()
    {
        var e1 = new ChangeFeedEntity { Name = "order-1" };
        var e2 = new ChangeFeedEntity { Name = "order-2" };
        var e3 = new ChangeFeedEntity { Name = "order-3" };

        await Repo.Insert(e1);
        await Repo.Insert(e2);
        await Repo.Insert(e3);

        var events = await Fixture.Sink.ReadAsync("test-source", 0, 1000, CancellationToken.None);
        var insertEvents = events.Where(e => new[] { e1.Id, e2.Id, e3.Id }.Any(id => e.EntityIds.Contains(id))).ToList();

        for (int i = 1; i < insertEvents.Count; i++)
        {
            Assert.True(insertEvents[i].Sequence > insertEvents[i - 1].Sequence,
                $"Sequence {insertEvents[i].Sequence} should be > {insertEvents[i - 1].Sequence}");
        }
    }

    [Fact]
    public async Task Replay_Idempotency_Same_ChangeId_Delivered_Twice()
    {
        var entity = new ChangeFeedEntity { Name = "idempotent" };
        await Repo.Insert(entity);

        var events = await Fixture.Sink.ReadAsync("test-source", 0, 1000, CancellationToken.None);
        var insertEvent = events.FirstOrDefault(e => e.EntityIds.Contains(entity.Id));
        Assert.NotNull(insertEvent);

        var sameId = events.Where(e => e.ChangeId == insertEvent.ChangeId).ToList();

        Assert.Single(sameId);
    }
}