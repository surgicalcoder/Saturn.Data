using System;
using MongoDB.Driver.Core.Events;

namespace GoLive.Saturn.Data;

public class MongoDBRepositoryOptions
{
    public bool EnableDiagnostics { get; set; }
    public bool CaptureCommandText { get; set; }
    public Func<CommandStartedEvent, bool> ShouldStartActivity { get; set; }
}