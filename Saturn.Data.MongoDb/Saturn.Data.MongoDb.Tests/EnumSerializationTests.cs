using MongoDB.Bson;
using MongoDB.Driver;
using Saturn.Data.MongoDb.Tests.Entities;

namespace Saturn.Data.MongoDb.Tests;

/// <summary>
/// Tests to verify that enum properties with default values are properly serialized/deserialized
/// and that queries against enum fields work as expected
/// </summary>
public class EnumSerializationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
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
    public async Task Enum_With_Default_Value_Should_Be_Serialized_To_Database()
    {
        // Arrange: Create a task with the default enum value (Pending = 0)
        var task = new WorkflowTask
        {
            Id = WELL_KNOWN.WorkflowTask_Pending.Id,
            Name = WELL_KNOWN.WorkflowTask_Pending.Name,
            Status = WorkflowTaskStatus.Pending // This is the default value (0)
        };

        // Act: Insert the entity
        await repo.Insert(task);

        // Assert: Query the raw BSON document to verify the Status field IS present
        // Thanks to AlwaysSerializeEnumsConvention, enums are always serialized even with default values
        var collection = repo.GetRawCollection<WorkflowTask>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(task.Id));
        var bsonDoc = await collection.Find(filter).FirstOrDefaultAsync();

        Assert.NotNull(bsonDoc);
        Assert.True(bsonDoc.Contains("Status"), 
            "Status field SHOULD be serialized even when it has the default value (Pending = 0) thanks to AlwaysSerializeEnumsConvention");
        Assert.Equal((int)WorkflowTaskStatus.Pending, bsonDoc["Status"].AsInt32);
    }

    [Fact]
    public async Task Enum_With_Non_Default_Value_Should_Be_Serialized_To_Database()
    {
        // Arrange: Create a task with a non-default enum value
        var task = new WorkflowTask
        {
            Id = WELL_KNOWN.WorkflowTask_InProgress.Id,
            Name = WELL_KNOWN.WorkflowTask_InProgress.Name,
            Status = WorkflowTaskStatus.InProgress // Non-default value (2)
        };

        // Act: Insert the entity
        await repo.Insert(task);

        // Assert: Query the raw BSON document to verify the Status field IS present
        var collection = repo.GetRawCollection<WorkflowTask>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(task.Id));
        var bsonDoc = await collection.Find(filter).FirstOrDefaultAsync();

        Assert.NotNull(bsonDoc);
        Assert.True(bsonDoc.Contains("Status"), 
            "Status field should be serialized when it has a non-default value");
        Assert.Equal((int)WorkflowTaskStatus.InProgress, bsonDoc["Status"].AsInt32);
    }

    [Fact]
    public async Task Query_For_Entity_With_Default_Enum_Value_Should_Return_Correct_Results()
    {
        // Arrange: Insert multiple tasks with different statuses
        await repo.Insert(new WorkflowTask
        {
            Id = WELL_KNOWN.WorkflowTask_Pending.Id,
            Name = WELL_KNOWN.WorkflowTask_Pending.Name,
            Status = WorkflowTaskStatus.Pending
        });
        
        await repo.Insert(new WorkflowTask
        {
            Id = WELL_KNOWN.WorkflowTask_InProgress.Id,
            Name = WELL_KNOWN.WorkflowTask_InProgress.Name,
            Status = WorkflowTaskStatus.InProgress
        });
        
        await repo.Insert(new WorkflowTask
        {
            Id = WELL_KNOWN.WorkflowTask_Completed.Id,
            Name = WELL_KNOWN.WorkflowTask_Completed.Name,
            Status = WorkflowTaskStatus.Completed
        });

        // Act: Query for tasks with Pending status (default value)
        var pendingTasksEnumerable = await repo.Many<WorkflowTask>(t => t.Status == WorkflowTaskStatus.Pending);
        var pendingTasks = await pendingTasksEnumerable.ToListAsync();

        // Assert: Should find the pending task even though the field wasn't serialized
        Assert.Single(pendingTasks);
        Assert.Equal(WELL_KNOWN.WorkflowTask_Pending.Name, pendingTasks[0].Name);
        Assert.Equal(WorkflowTaskStatus.Pending, pendingTasks[0].Status);
    }

    [Fact]
    public async Task Query_For_Entity_With_Non_Default_Enum_Value_Should_Return_Correct_Results()
    {
        // Arrange: Insert multiple tasks
        await repo.Insert(WELL_KNOWN.WorkflowTask_Pending);
        await repo.Insert(WELL_KNOWN.WorkflowTask_InProgress);
        await repo.Insert(WELL_KNOWN.WorkflowTask_Completed);

        // Act: Query for tasks with InProgress status
        var inProgressTasksEnumerable = await repo.Many<WorkflowTask>(t => t.Status == WorkflowTaskStatus.InProgress);
        var inProgressTasks = await inProgressTasksEnumerable.ToListAsync();

        // Assert
        Assert.Single(inProgressTasks);
        Assert.Equal(WELL_KNOWN.WorkflowTask_InProgress.Name, inProgressTasks[0].Name);
        Assert.Equal(WorkflowTaskStatus.InProgress, inProgressTasks[0].Status);
    }

    [Fact]
    public async Task Entity_Retrieved_Without_Status_Field_Should_Have_Default_Enum_Value()
    {
        // Arrange: Insert a task with default status
        var task = new WorkflowTask
        {
            Id = WELL_KNOWN.WorkflowTask_Pending.Id,
            Name = WELL_KNOWN.WorkflowTask_Pending.Name,
            Status = WorkflowTaskStatus.Pending
        };
        await repo.Insert(task);

        // Act: Retrieve the entity by ID
        var retrieved = await repo.ById<WorkflowTask>(task.Id);

        // Assert: The Status should be the default value
        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowTaskStatus.Pending, retrieved.Status);
        Assert.Equal(0, (int)retrieved.Status);
    }

    [Fact]
    public async Task Enum_With_Negative_Value_Should_Be_Serialized()
    {
        // Arrange: Create a task with a negative enum value
        var task = new WorkflowTask
        {
            Id = WELL_KNOWN.WorkflowTask_RequiresApproval.Id,
            Name = WELL_KNOWN.WorkflowTask_RequiresApproval.Name,
            Status = WorkflowTaskStatus.RequiresApproval // Negative value (-1)
        };

        // Act: Insert the entity
        await repo.Insert(task);

        // Assert: Query the raw BSON document to verify the Status field IS present
        var collection = repo.GetRawCollection<WorkflowTask>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(task.Id));
        var bsonDoc = await collection.Find(filter).FirstOrDefaultAsync();

        Assert.NotNull(bsonDoc);
        Assert.True(bsonDoc.Contains("Status"), 
            "Status field should be serialized when it has a negative value");
        Assert.Equal((int)WorkflowTaskStatus.RequiresApproval, bsonDoc["Status"].AsInt32);

        // Also verify we can query for it
        var retrieved = await repo.ById<WorkflowTask>(task.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowTaskStatus.RequiresApproval, retrieved.Status);
    }

    [Fact]
    public async Task Update_Enum_From_Default_To_Non_Default_Should_Update_Field()
    {
        // Arrange: Insert a task with default status
        var task = new WorkflowTask
        {
            Id = WELL_KNOWN.WorkflowTask_Pending.Id,
            Name = WELL_KNOWN.WorkflowTask_Pending.Name,
            Status = WorkflowTaskStatus.Pending
        };
        await repo.Insert(task);

        // Verify Status field IS in the database with Pending value (because of AlwaysSerializeEnumsConvention)
        var collection = repo.GetRawCollection<WorkflowTask>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(task.Id));
        var bsonDocBefore = await collection.Find(filter).FirstOrDefaultAsync();
        Assert.True(bsonDocBefore.Contains("Status"));
        Assert.Equal((int)WorkflowTaskStatus.Pending, bsonDocBefore["Status"].AsInt32);

        // Act: Update to a non-default status
        task.Status = WorkflowTaskStatus.InProgress;
        await repo.Update(task);

        // Assert: Status field should still be present with updated value
        var bsonDocAfter = await collection.Find(filter).FirstOrDefaultAsync();
        Assert.True(bsonDocAfter.Contains("Status"));
        Assert.Equal((int)WorkflowTaskStatus.InProgress, bsonDocAfter["Status"].AsInt32);
    }

    [Fact]
    public async Task Update_Enum_From_Non_Default_To_Default_Should_Keep_Field()
    {
        // Arrange: Insert a task with non-default status
        var task = new WorkflowTask
        {
            Id = WELL_KNOWN.WorkflowTask_InProgress.Id,
            Name = WELL_KNOWN.WorkflowTask_InProgress.Name,
            Status = WorkflowTaskStatus.InProgress
        };
        await repo.Insert(task);

        // Verify Status field is in the database
        var collection = repo.GetRawCollection<WorkflowTask>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(task.Id));
        var bsonDocBefore = await collection.Find(filter).FirstOrDefaultAsync();
        Assert.True(bsonDocBefore.Contains("Status"));

        // Act: Update to the default status
        task.Status = WorkflowTaskStatus.Pending;
        await repo.Update(task);

        // Assert: Status field should still be present with the default value
        // AlwaysSerializeEnumsConvention ensures enums are always serialized
        var bsonDocAfter = await collection.Find(filter).FirstOrDefaultAsync();
        Assert.True(bsonDocAfter.Contains("Status"), 
            "Status field should remain even when updated to the default value");
        Assert.Equal((int)WorkflowTaskStatus.Pending, bsonDocAfter["Status"].AsInt32);
    }
}

