using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using Saturn.Data.MongoDb.Tests.Entities;
using Xunit.Abstractions;

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
public class RefIdSerializationTests(DatabaseFixture fixture, ITestOutputHelper output) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableMongoDbRepository repo = fixture.Repository;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await repo.Delete<BackgroundTask>(e => true);
        await repo.Delete<RefEntity>(e => true);
        await repo.Delete<BasicEntity>(e => true);
    }

    // -------------------------------------------------------------------------
    // Any() tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// WITHOUT NormalizeForRef current Mongo LINQ translation fails before execution
    /// with a serializer type mismatch (ObjectId serializer vs string expression value).
    /// This is the expected "red" behavior.
    /// </summary>
    [Fact]
    public async Task Any_By_RefId_Without_Normalization_Throws_Serializer_Type_Mismatch()
    {
        var completedTask = new BackgroundTask { Id = "68bdd5525324ff2610c44001", Name = "Completed", Status = BackgroundTaskStatus.Completed };
        var waitingTask  = new BackgroundTask { Id = "68bdd5525324ff2610c44002", Name = "Waiting",   Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                DependentTasks = new List<Ref<BackgroundTask>> { new(completedTask.Id) } };

        await repo.Insert(completedTask);
        await repo.Insert(waitingTask);

        var searchId = completedTask.Id;

        // Bypass NormalizeForRef – the driver will try to resolve d.Id via
        // RefSerializer.TryGetMemberSerializationInfo which returns "_____" as the element name.
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => repo.ManyWithoutNormalization<BackgroundTask>(
                t => t.DependentTasks.Any(d => d.Id == searchId)));

        Assert.Contains("Serializer value type MongoDB.Bson.ObjectId is incompatible with expression value type System.String", ex.Message);
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
    /// Same failure mode via Count() instead of Any().
    /// </summary>
    [Fact]
    public async Task Count_By_RefId_Without_Normalization_Throws_Serializer_Type_Mismatch()
    {
        var completedTask = new BackgroundTask { Id = "68bdd5525324ff2610c44003", Name = "Completed 2", Status = BackgroundTaskStatus.Completed };
        var waitingTask  = new BackgroundTask { Id = "68bdd5525324ff2610c44004", Name = "Waiting 2",   Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                DependentTasks = new List<Ref<BackgroundTask>> { new(completedTask.Id) } };

        await repo.Insert(completedTask);
        await repo.Insert(waitingTask);

        var searchId = completedTask.Id;

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => repo.ManyWithoutNormalization<BackgroundTask>(
                t => t.DependentTasks.Count(d => d.Id == searchId) > 0));

        Assert.Contains("Serializer value type MongoDB.Bson.ObjectId is incompatible with expression value type System.String", ex.Message);
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
    /// Precision variant of the same red-path failure mode.
    /// Without normalization, query translation throws the same serializer mismatch.
    /// </summary>
    [Fact]
    public async Task Any_By_RefId_Without_Normalization_Precision_Path_Throws_Serializer_Type_Mismatch()
    {
        var targetTask = new BackgroundTask  { Id = "68bdd5525324ff2610c44005", Name = "Target",    Status = BackgroundTaskStatus.Completed };
        var referringTask = new BackgroundTask { Id = "68bdd5525324ff2610c44006", Name = "Referring", Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                  DependentTasks = [new(targetTask.Id)]
        };
        var unrelatedTask = new BackgroundTask { Id = "68bdd5525324ff2610c44007", Name = "Unrelated", Status = BackgroundTaskStatus.WaitingOnDependencies,
                                                  DependentTasks = [new("68bdd5525324ff2610c44099")]
        };

        await repo.Insert(targetTask);
        await repo.Insert(referringTask);
        await repo.Insert(unrelatedTask);

        var searchId = targetTask.Id;

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => repo.ManyWithoutNormalization<BackgroundTask>(
                t => t.DependentTasks.Any(d => d.Id == searchId)));

        Assert.Contains("Serializer value type MongoDB.Bson.ObjectId is incompatible with expression value type System.String", ex.Message);
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

    // -------------------------------------------------------------------------
    // Single Ref<T> field – raw BSON shape assertions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a single (non-list) Ref&lt;T&gt; field with a populated Id is stored as a
    /// plain ObjectId in MongoDB, not as a sub-document.
    /// </summary>
    [Fact]
    public async Task Single_Ref_With_Id_Is_Serialized_As_Plain_ObjectId()
    {
        const string basicId = "68bdd5525324ff2610c44020";
        const string refId   = "68bdd5525324ff2610c44021";

        var basic = new BasicEntity { Id = basicId, Name = "Basic" };
        var refEntity = new RefEntity { Id = refId, Name = "WithRef", BasicEntityItem = new Ref<BasicEntity>(basicId) };

        await repo.Insert(basic);
        await repo.Insert(refEntity);

        var rawCollection = repo.GetRawCollection<RefEntity>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(refId));
        var rawDoc = await rawCollection.Find(filter).FirstOrDefaultAsync();

        Assert.NotNull(rawDoc);

        output.WriteLine("Serialized BSON document (JSON representation):");
        output.WriteLine(rawDoc.ToJson());

        var element = rawDoc["BasicEntityItem"];
        string bsonTypeName = element.BsonType.ToString();
        output.WriteLine($"BasicEntityItem BSON type: {bsonTypeName}");

        Assert.True(element.BsonType == BsonType.ObjectId,
            $"Expected BasicEntityItem to be a plain ObjectId, but was {bsonTypeName}. " +
            "If this is a sub-document, the serializer is broken.");

        Assert.Equal(basicId, element.AsObjectId.ToString());

        // Round-trip: deserialize back and verify Id is preserved, Item is not populated
        var retrieved = await repo.ById<RefEntity>(refId);
        Assert.NotNull(retrieved);
        Assert.Equal(basicId, retrieved.BasicEntityItem.Id);
        Assert.Null(retrieved.BasicEntityItem.Item);
    }

    /// <summary>
    /// Verifies that a null Ref&lt;T&gt; field is omitted or stored as BsonNull (not a sub-document),
    /// and that the round-trip correctly deserializes back to null.
    /// IgnoreIfDefaultConvention omits null reference-type properties entirely; both absent and
    /// BsonNull deserialize identically to null.
    /// </summary>
    [Fact]
    public async Task Single_Null_Ref_Deserializes_As_Null()
    {
        const string refId = "68bdd5525324ff2610c44022";

        var refEntity = new RefEntity { Id = refId, Name = "NullRef", BasicEntityItem = null! };

        await repo.Insert(refEntity);

        var rawCollection = repo.GetRawCollection<RefEntity>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(refId));
        var rawDoc = await rawCollection.Find(filter).FirstOrDefaultAsync();

        Assert.NotNull(rawDoc);

        output.WriteLine("Serialized BSON document (JSON representation):");
        output.WriteLine(rawDoc.ToJson());

        if (rawDoc.TryGetElement("BasicEntityItem", out var element))
        {
            output.WriteLine($"BasicEntityItem BSON type: {element.Value.BsonType}");
            Assert.True(
                element.Value.BsonType == BsonType.Null,
                $"Expected null Ref to be absent or BsonNull, but was {element.Value.BsonType}.");
        }
        else
        {
            output.WriteLine("BasicEntityItem is absent from document (omitted by IgnoreIfDefaultConvention).");
        }

        var retrieved = await repo.ById<RefEntity>(refId);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.BasicEntityItem);
    }
}
