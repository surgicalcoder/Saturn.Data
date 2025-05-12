using System;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Callbacks;
using MongoDB.Driver.Core.Events;

namespace GoLive.Saturn.Data;

public class MongoDBRepositoryOptions
{
    public bool EnableDiagnostics { get; set; }
    public bool CaptureCommandText { get; set; }
    public Func<CommandStartedEvent, bool> ShouldStartActivity { get; set; }
    
    public Func<MongoCommandFailedEvent, Task> CommandFailedCallback { get; set; }
    public Func<MongoCommandStartedEvent, Task> CommandStartedCallback { get; set; }
    public Func<MongoCommandSucceededEvent, Task> CommandSucceededCallback { get; set; }
}