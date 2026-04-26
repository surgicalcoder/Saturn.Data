using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using Saturn.Data.MongoDb.Tests.Entities;

namespace Saturn.Data.MongoDb.Tests;

/// <summary>
/// Tests that verify Ref&lt;T&gt;.Id comparisons in LINQ expressions are correctly translated
/// to MongoDB queries.
///
/// Root cause:
///   RefSerializer&lt;T&gt;.TryGetMemberSerializationInfo("Id", ...) returns element name "_____"
///   as a sentinel. Without any fix this causes the LINQ provider to generate the field path
///   "DependentTasks._____" which does not exist in MongoDB, so every query returns nothing.
///
/// Current fix in this repo:
///   RefExpressionRewriter.NormalizeForRef() rewrites  d.Id == someId
///   into  d == new Ref&lt;T&gt;(someId)  BEFORE the expression is handed to the MongoDB driver,
///   so the driver never needs to introspect Ref&lt;T&gt;.Id at all.
///
/// The *_Without_Normalization tests deliberately bypass NormalizeForRef() and are
/// therefore expected to FAIL – that is the point. They are the "red" half of the pair.
/// The *_With_Normalization twins of each test are the "green" half and must pass.
/// </summary>
public class RefIdSerializationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableMongoDbRepository repo = fixture.Repository;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await repo.Delete<BackgroundTask>(e => true);
    }

    // -------------------------------------------------------------------------
    // Any() tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// WITHOUT NormalizeForRef the MongoDB driver translates d.Id == searchId into
    /// { "DependentTasks._____": searchId } which never matches → result is empty.
    /// This test is expected to FAIL (it's the "red" test that motivates the fix).
    /// </summary>
    [Fact]
    public async Task Any_By_RefId_Without_Normalization_Fails()
    {
        var completedTask = new BackgroundTask { Id = "68bdd5525324ff2610c44001", Name = "Completed", Status = BackgroundTaskStatus.Completed };
        var waitingTask  = new BackgroundTask { Id = "68bdd5525324ff2610c44002", Name = "Waiting",   Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                DependentTasks = new List<Ref<BackgroundTask>> { new(completedTask.Id) } };

        await repo.Insert(completedTask);
        await repo.Insert(waitingTask);

        var searchId = completedTask.Id;

        // Bypass NormalizeForRef – the driver will try to resolve d.Id via
        // RefSerializer.TryGetMemberSerializationInfo which returns "_____" as the element name.
        var results = await repo.ManyWithoutNormalization<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == searchId));

        // This assertion FAILS without the fix: results will be empty.
        Assert.Single(results);
    }

    /// <summary>
    /// WITH NormalizeForRef the expression is rewritten to d == new Ref&lt;T&gt;(searchId) before
    /// the driver sees it → matches the stored ObjectId → correct result returned.
    /// This test must PASS.
    /// </summary>
    [Fact]
    public async Task Any_By_RefId_With_Normalization_Passes()
    {
        var completedTask = new BackgroundTask { Id = "68bdd5525324ff2610c44001", Name = "Completed", Status = BackgroundTaskStatus.Completed };
        var waitingTask  = new BackgroundTask { Id = "68bdd5525324ff2610c44002", Name = "Waiting",   Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                DependentTasks = new List<Ref<BackgroundTask>> { new(completedTask.Id) } };

        await repo.Insert(completedTask);
        await repo.Insert(waitingTask);

        var searchId = completedTask.Id;

        var results = await (await repo.Many<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == searchId))).ToListAsync();

        Assert.Single(results);
        Assert.Equal(waitingTask.Id, results[0].Id);
    }

    // -------------------------------------------------------------------------
    // Count() tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Same failure mode via Count() instead of Any() — expected to FAIL.
    /// </summary>
    [Fact]
    public async Task Count_By_RefId_Without_Normalization_Fails()
    {
        var completedTask = new BackgroundTask { Id = "68bdd5525324ff2610c44003", Name = "Completed 2", Status = BackgroundTaskStatus.Completed };
        var waitingTask  = new BackgroundTask { Id = "68bdd5525324ff2610c44004", Name = "Waiting 2",   Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                DependentTasks = new List<Ref<BackgroundTask>> { new(completedTask.Id) } };

        await repo.Insert(completedTask);
        await repo.Insert(waitingTask);

        var searchId = completedTask.Id;

        var results = await repo.ManyWithoutNormalization<BackgroundTask>(
            t => t.DependentTasks.Count(d => d.Id == searchId) > 0);

        // This assertion FAILS without the fix.
        Assert.Single(results);
    }

    /// <summary>
    /// WITH normalization Count() also works correctly — must PASS.
    /// </summary>
    [Fact]
    public async Task Count_By_RefId_With_Normalization_Passes()
    {
        var completedTask = new BackgroundTask { Id = "68bdd5525324ff2610c44003", Name = "Completed 2", Status = BackgroundTaskStatus.Completed };
        var waitingTask  = new BackgroundTask { Id = "68bdd5525324ff2610c44004", Name = "Waiting 2",   Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                DependentTasks = new List<Ref<BackgroundTask>> { new(completedTask.Id) } };

        await repo.Insert(completedTask);
        await repo.Insert(waitingTask);

        var searchId = completedTask.Id;

        var results = await (await repo.Many<BackgroundTask>(
            t => t.DependentTasks.Count(d => d.Id == searchId) > 0)).ToListAsync();

        Assert.Single(results);
        Assert.Equal(waitingTask.Id, results[0].Id);
    }

    // -------------------------------------------------------------------------
    // Precision test: wrong item must NOT be returned
    // -------------------------------------------------------------------------

    /// <summary>
    /// Even if Any() somehow returns documents when it should not (false positives),
    /// we need to verify that only the CORRECT document is returned.
    /// Without normalization the filter generates no matches at all → empty → FAILS.
    /// </summary>
    [Fact]
    public async Task Any_By_RefId_Without_Normalization_Does_Not_Wrongly_Return_Unrelated_Task()
    {
        var targetTask = new BackgroundTask  { Id = "68bdd5525324ff2610c44005", Name = "Target",    Status = BackgroundTaskStatus.Completed };
        var referringTask = new BackgroundTask { Id = "68bdd5525324ff2610c44006", Name = "Referring", Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                  DependentTasks = new List<Ref<BackgroundTask>> { new(targetTask.Id) } };
        var unrelatedTask = new BackgroundTask { Id = "68bdd5525324ff2610c44007", Name = "Unrelated", Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                  DependentTasks = new List<Ref<BackgroundTask>> { new("68bdd5525324ff2610c44099") } };

        await repo.Insert(targetTask);
        await repo.Insert(referringTask);
        await repo.Insert(unrelatedTask);

        var searchId = targetTask.Id;

        var results = await repo.ManyWithoutNormalization<BackgroundTask>(
            t => t.DependentTasks.Any(d => d.Id == searchId));

        // FAILS: result is empty instead of containing exactly referringTask.
        Assert.Single(results);
        Assert.Equal(referringTask.Id, results[0].Id);
    }

    // -------------------------------------------------------------------------
    // Raw BSON shape assertion (always passes — documents the expected storage format)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that Ref&lt;T&gt; is stored as a plain ObjectId in MongoDB (not a sub-document).
    /// This documents the storage contract the query rewriter relies on.
    /// This test always passes because the serializer is correct; it is the *query translation*
    /// (TryGetMemberSerializationInfo returning "_____") that is wrong.
    /// </summary>
    [Fact]
    public async Task Ref_Is_Serialized_As_Plain_ObjectId_Not_Subdocument()
    {
        var dep = new BackgroundTask  { Id = "68bdd5525324ff2610c44010", Name = "Dep",   Status = BackgroundTaskStatus.Completed };
        var owner = new BackgroundTask { Id = "68bdd5525324ff2610c44011", Name = "Owner", Status = BackgroundTaskStatus.WaitingOnDependencies,
                                         DependentTasks = new List<Ref<BackgroundTask>> { new(dep.Id) } };

        await repo.Insert(dep);
        await repo.Insert(owner);

        var rawCollection = repo.GetRawCollection<BackgroundTask>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(owner.Id));
        var rawDoc = await rawCollection.Find(filter).FirstOrDefaultAsync();

        Assert.NotNull(rawDoc);

        var array = rawDoc["DependentTasks"].AsBsonArray;
        Assert.Single(array);

        var element = array[0];
        string bsonTypeName = element.BsonType.ToString();
        Assert.True(element.BsonType == BsonType.ObjectId,
            $"Expected element to be a plain ObjectId, but was {bsonTypeName}. " +
            "If this is a sub-document, the serializer is broken too.");

        string storedId = element.AsObjectId.ToString();
        Assert.Equal(dep.Id, storedId);
    }
}
