using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.MongoDb.Tests.Entities;

public enum BackgroundTaskStatus
{
    Pending = 0,
    WaitingOnDependencies = 1,
    Completed = 3,
}

[System.Diagnostics.DebuggerDisplay("Name = {Name}, Status = {Status}")]
public class BackgroundTask : Entity
{
    public string Name { get; set; } = string.Empty;
    public BackgroundTaskStatus Status { get; set; }
    public List<Ref<BackgroundTask>> DependentTasks { get; set; } = new();
}

