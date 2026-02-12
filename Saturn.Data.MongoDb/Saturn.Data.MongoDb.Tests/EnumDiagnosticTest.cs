using MongoDB.Bson;
using MongoDB.Driver;
using Saturn.Data.MongoDb.Tests.Entities;

namespace Saturn.Data.MongoDb.Tests;

/// <summary>
/// Diagnostic test to help debug the enum serialization issue
/// </summary>
public class EnumDiagnosticTest(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableMongoDbRepository repo = fixture.Repository;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.Delete<WorkflowTask>(e => true);
    }

    [Fact]
    public async Task Debug_Insert_And_Query_Default_Enum()
    {
        // Insert a task with default status
        var task1 = new WorkflowTask
        {
            Id = "68bdd5525324ff2610999991",
            Name = "Debug Task 1",
            Status = WorkflowTaskStatus.Pending
        };
        
        await repo.Insert(task1);

        // Check what's in the database
        var collection = repo.GetRawCollection<WorkflowTask>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(task1.Id));
        var bsonDoc = await collection.Find(filter).FirstOrDefaultAsync();

        // Output what we found
        var hasStatusField = bsonDoc.Contains("Status");
        var statusValue = hasStatusField ? bsonDoc["Status"].AsInt32 : -999;

        // Now try to query for it
        var queryResult = await repo.Many<WorkflowTask>(t => t.Status == WorkflowTaskStatus.Pending);
        var results = await queryResult.ToListAsync();

        // Asserts with detailed information
        Assert.True(hasStatusField, $"Status field should be present in BSON document");
        Assert.Equal(0, statusValue);
        Assert.NotEmpty(results);
        Assert.Single(results);
    }
}

