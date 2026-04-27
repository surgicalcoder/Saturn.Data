using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;
using Saturn.Data.MongoDb.Tests.Entities;

namespace Saturn.Data.MongoDb.Tests;

/// <summary>
/// Comprehensive tests for Ref&lt;T&gt;.Id query patterns.
///
/// Core invariant:
///   Ref&lt;T&gt; is stored as a plain scalar ObjectId in MongoDB. Querying via the C# property
///   `ref.Id` is handled by RefExpressionRewriter.NormalizeForRef(), which rewrites
///   `d.Id == someId` → `d == new Ref&lt;T&gt;(someId)` before the LINQ3 provider processes it.
///
/// These tests verify that:
///   1. Read operations (Many, One, Count) correctly resolve Ref&lt;T&gt;.Id predicates.
///   2. Write operations (Delete, Update) also normalize Ref&lt;T&gt;.Id predicates.
///   3. Collection operations (Any, Count on nested lists) work with Ref&lt;T&gt;.Id.
///   4. Multiple Ref&lt;T&gt;.Id conditions in a single predicate all work.
///   5. No Harmony runtime patcher is required.
/// </summary>
public class RefQueryTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableMongoDbRepository _repo = fixture.Repository;

    // Fixed IDs so tests are deterministic and don't clash across parallel fixtures
    private static readonly string CompletedId   = "aabbcc112233445566778801";
    private static readonly string Waiting1Id    = "aabbcc112233445566778802";
    private static readonly string Waiting2Id    = "aabbcc112233445566778803";
    private static readonly string UnrelatedId   = "aabbcc112233445566778804";
    private static readonly string OtherId       = "aabbcc112233445566778899";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up only the IDs this test class owns
        await _repo.Delete<BackgroundTask>(e =>
            e.Id == CompletedId  ||
            e.Id == Waiting1Id   ||
            e.Id == Waiting2Id   ||
            e.Id == UnrelatedId  ||
            e.Id == OtherId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private BackgroundTask Completed   => new() { Id = CompletedId,  Name = "Completed",   Status = BackgroundTaskStatus.Completed };
    private BackgroundTask Waiting1    => new() { Id = Waiting1Id,   Name = "Waiting1",    Status = BackgroundTaskStatus.WaitingOnDependencies, DependentTasks = [new(CompletedId)] };
    private BackgroundTask Waiting2    => new() { Id = Waiting2Id,   Name = "Waiting2",    Status = BackgroundTaskStatus.WaitingOnDependencies, DependentTasks = [new(CompletedId), new(Waiting1Id)] };
    private BackgroundTask Unrelated   => new() { Id = UnrelatedId,  Name = "Unrelated",   Status = BackgroundTaskStatus.WaitingOnDependencies, DependentTasks = [new(OtherId)] };

    private async Task SeedAsync()
    {
        await _repo.Insert(Completed);
        await _repo.Insert(Waiting1);
        await _repo.Insert(Waiting2);
        await _repo.Insert(Unrelated);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Many (read)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Many_AnyByRefId_ReturnsCorrectDocuments()
    {
        await SeedAsync();
        var searchId = CompletedId;

        var results = await (await _repo.Many<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == searchId))).ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Id == Waiting1Id);
        Assert.Contains(results, r => r.Id == Waiting2Id);
    }

    [Fact]
    public async Task Many_AnyByRefId_ExcludesDocumentsWithDifferentRef()
    {
        await SeedAsync();
        var searchId = OtherId; // only Unrelated has this

        var results = await (await _repo.Many<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == searchId))).ToListAsync();

        Assert.Single(results);
        Assert.Equal(UnrelatedId, results[0].Id);
    }

    [Fact]
    public async Task Many_CountByRefId_ReturnsDocumentsWhereCountGreaterThanZero()
    {
        await SeedAsync();
        var searchId = CompletedId;

        var results = await (await _repo.Many<BackgroundTask>(
            t => t.DependentTasks.Count(d => d.Id == searchId) > 0)).ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Id == Waiting1Id);
        Assert.Contains(results, r => r.Id == Waiting2Id);
    }

    [Fact]
    public async Task Many_DirectRefEquality_WorksWithoutDotId()
    {
        await SeedAsync();
        // Querying via direct Ref<T> equality (no .Id) — baseline that must always work
        var searchRef = new Ref<BackgroundTask>(CompletedId);

        var results = await (await _repo.Many<BackgroundTask>(
            t => t.DependentTasks.Any(d => d == searchRef))).ToListAsync();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Many_CombinedRefIdAndScalarPredicate_Works()
    {
        await SeedAsync();
        var searchId = CompletedId;

        var results = await (await _repo.Many<BackgroundTask>(
            t => t.Status == BackgroundTaskStatus.WaitingOnDependencies &&
                 t.DependentTasks.Any(d => d.Id == searchId))).ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(BackgroundTaskStatus.WaitingOnDependencies, r.Status));
    }

    [Fact]
    public async Task Many_MultipleRefIdConditions_Works()
    {
        await SeedAsync();
        var id1 = CompletedId;
        var id2 = Waiting1Id;

        // Only Waiting2 has BOTH CompletedId and Waiting1Id in its DependentTasks
        var results = await (await _repo.Many<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == id1) &&
                 t.DependentTasks.Any(d => d.Id == id2))).ToListAsync();

        Assert.Single(results);
        Assert.Equal(Waiting2Id, results[0].Id);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // One (read)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task One_ByRefId_ReturnsFirstMatch()
    {
        await SeedAsync();
        var searchId = CompletedId;

        var result = await _repo.One<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == searchId));

        Assert.NotNull(result);
        Assert.Contains(new[] { Waiting1Id, Waiting2Id }, id => id == result.Id);
    }

    [Fact]
    public async Task One_ByRefId_ReturnsNullWhenNoMatch()
    {
        await SeedAsync();
        var nonExistentId = "000000000000000000000000";

        var result = await _repo.One<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == nonExistentId));

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Count (read)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Count_ByRefId_ReturnsCorrectCount()
    {
        await SeedAsync();
        var searchId = CompletedId;

        var count = await _repo.Count<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == searchId));

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Count_ByRefId_ReturnsZeroWhenNoMatch()
    {
        await SeedAsync();
        var nonExistentId = "000000000000000000000000";

        var count = await _repo.Count<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == nonExistentId));

        Assert.Equal(0, count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Delete (write) — verifies NormalizeForRef is applied on the write path
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ByRefId_RemovesOnlyMatchingDocuments()
    {
        await SeedAsync();
        var searchId = CompletedId;

        // Delete all tasks that depend on the completed task
        await _repo.Delete<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == searchId));

        // Completed and Unrelated should still exist
        var remaining = await (await _repo.All<BackgroundTask>()).ToListAsync();
        Assert.DoesNotContain(remaining, r => r.Id == Waiting1Id);
        Assert.DoesNotContain(remaining, r => r.Id == Waiting2Id);
        Assert.Contains(remaining, r => r.Id == CompletedId);
        Assert.Contains(remaining, r => r.Id == UnrelatedId);
    }

    [Fact]
    public async Task Delete_ByRefId_WithNoMatch_DeletesNothing()
    {
        await SeedAsync();
        var nonExistentId = "000000000000000000000000";

        await _repo.Delete<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == nonExistentId));

        var count = await _repo.Count<BackgroundTask>(e =>
            e.Id == CompletedId || e.Id == Waiting1Id || e.Id == Waiting2Id || e.Id == UnrelatedId);
        Assert.Equal(4, count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update with condition predicate (write)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithRefIdCondition_OnlyUpdatesMatchingDocument()
    {
        await SeedAsync();
        var searchId = CompletedId;

        // Update Waiting1 (which depends on Completed) — condition checks the dependency
        var waiting1 = await _repo.ById<BackgroundTask>(Waiting1Id);
        Assert.NotNull(waiting1);
        waiting1.Name = "Updated Name";

        await _repo.Update<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == searchId),
            waiting1);

        var updated = await _repo.ById<BackgroundTask>(Waiting1Id);
        Assert.Equal("Updated Name", updated!.Name);

        // Waiting2 should be unmodified
        var notUpdated = await _repo.ById<BackgroundTask>(Waiting2Id);
        Assert.Equal("Waiting2", notUpdated!.Name);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Raw storage shape — documents the on-disk contract
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StorageShape_RefIsPersisted_AsScalarObjectId()
    {
        await _repo.Insert(Completed);
        await _repo.Insert(Waiting1);

        var rawCollection = _repo.GetRawCollection<BackgroundTask>();
        var doc = await rawCollection
            .Find(MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", new MongoDB.Bson.ObjectId(Waiting1Id)))
            .FirstOrDefaultAsync();

        Assert.NotNull(doc);
        var array = doc["DependentTasks"].AsBsonArray;
        Assert.Single(array);
        Assert.Equal(MongoDB.Bson.BsonType.ObjectId, array[0].BsonType);
        Assert.Equal(CompletedId, array[0].AsObjectId.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // "Without normalization" red tests — confirm the problem still exists
    // when NormalizeForRef is bypassed (proving the rewriter is doing the work)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WithoutNormalization_AnyByRefId_ThrowsSerializerTypeMismatch()
    {
        await SeedAsync();
        var searchId = CompletedId;

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _repo.ManyWithoutNormalization<BackgroundTask>(
                t => t.DependentTasks.Any(d => d.Id == searchId)));

        Assert.Contains("incompatible with expression value type", ex.Message);
    }

    [Fact]
    public async Task WithoutNormalization_CountByRefId_ThrowsSerializerTypeMismatch()
    {
        await SeedAsync();
        var searchId = CompletedId;

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _repo.ManyWithoutNormalization<BackgroundTask>(
                t => t.DependentTasks.Count(d => d.Id == searchId) > 0));

        Assert.Contains("incompatible with expression value type", ex.Message);
    }

    [Fact]
    public async Task WithoutNormalization_DeleteByRefId_ThrowsSerializerTypeMismatch()
    {
        await SeedAsync();
        var searchId = CompletedId;

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _repo.DeleteWithoutNormalization<BackgroundTask>(
                t => t.DependentTasks.Any(d => d.Id == searchId)));

        Assert.Contains("incompatible with expression value type", ex.Message);
    }
}

