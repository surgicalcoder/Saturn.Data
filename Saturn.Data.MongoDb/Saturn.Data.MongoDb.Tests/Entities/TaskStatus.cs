namespace Saturn.Data.MongoDb.Tests.Entities;

/// <summary>
/// Status enumeration for workflow tasks
/// </summary>
public enum WorkflowTaskStatus
{
    /// <summary>
    /// Task requires manual approval before proceeding
    /// </summary>
    RequiresApproval = -1,
    
    /// <summary>
    /// Task is pending execution (default value)
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Task is waiting on dependencies to complete
    /// </summary>
    WaitingOnDependencies = 1,
    
    /// <summary>
    /// Task is currently in progress
    /// </summary>
    InProgress = 2,
    
    /// <summary>
    /// Task has been completed successfully
    /// </summary>
    Completed = 3,
    
    /// <summary>
    /// Task has failed
    /// </summary>
    Failed = 4
}

