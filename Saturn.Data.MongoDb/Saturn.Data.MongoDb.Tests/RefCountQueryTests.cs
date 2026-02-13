using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.MongoDb.Tests.Entities;

namespace Saturn.Data.MongoDb.Tests;

/// <summary>
/// Test to reproduce the issue with querying List&lt;Ref&lt;T&gt;&gt; using Count in LINQ expressions
/// </summary>
public class RefCountQueryTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableMongoDbRepository repo = fixture.Repository;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.Delete<BackgroundTask>(e => true);
    }

    [Fact]
    public async Task Query_With_Count_On_Ref_List_Should_Not_Throw_InvalidCastException()
    {
        // Arrange: Insert a completed task and a task waiting on it
        await repo.Insert(WELL_KNOWN.BackgroundTask_Completed);
        await repo.Insert(WELL_KNOWN.BackgroundTask_WaitingOnDependency);

        var completedTask = WELL_KNOWN.BackgroundTask_Completed;

        // Act: This should not throw InvalidCastException
        var dependentTasks = await repo.Many<BackgroundTask>(
            t => t.Status == BackgroundTaskStatus.WaitingOnDependencies &&
                 t.DependentTasks.Count(d => d.Id == completedTask.Id) > 0);

        // Materialize to list
        var dependentTasksList = await dependentTasks.ToListAsync();

        // Assert: Should find the waiting task
        Assert.Single(dependentTasksList);
        Assert.Equal(WELL_KNOWN.BackgroundTask_WaitingOnDependency.Id, dependentTasksList[0].Id);
    }

    [Fact]
    public async Task Query_With_Any_On_Ref_List_Should_Work()
    {
        // Arrange: Insert a completed task and a task waiting on it
        await repo.Insert(WELL_KNOWN.BackgroundTask_Completed);
        await repo.Insert(WELL_KNOWN.BackgroundTask_WaitingOnDependency);

        var completedTask = WELL_KNOWN.BackgroundTask_Completed;

        // Act: Use Any instead of Count (which might work as a workaround)
        var dependentTasks = await repo.Many<BackgroundTask>(
            t => t.Status == BackgroundTaskStatus.WaitingOnDependencies &&
                 t.DependentTasks.Any(d => d.Id == completedTask.Id));

        // Materialize to list to avoid double enumeration
        var dependentTasksList = await dependentTasks.ToListAsync();

        // Assert: Should find the waiting task
        Assert.Single(dependentTasksList);
        Assert.Equal(WELL_KNOWN.BackgroundTask_WaitingOnDependency.Id, dependentTasksList[0].Id);
    }
}

