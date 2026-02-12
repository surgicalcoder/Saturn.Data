using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.MongoDb.Tests.Entities;

/// <summary>
/// Represents a workflow task entity for testing enum serialization
/// </summary>
[System.Diagnostics.DebuggerDisplay("Name = {Name}, Status = {Status}")]
public class WorkflowTask : Entity
{
    public string Name { get; set; } = string.Empty;
    public WorkflowTaskStatus Status { get; set; }
    public string? Description { get; set; }
}

