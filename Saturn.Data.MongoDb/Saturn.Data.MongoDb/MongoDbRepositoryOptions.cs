using MongoDB.Driver.Core.Events;
using Saturn.Data.MongoDb.Callbacks;

namespace Saturn.Data.MongoDb;

public class MongoDbRepositoryOptions
{
    public bool EnableDiagnostics { get; set; }
    public bool CaptureCommandText { get; set; }
    public Func<CommandStartedEvent, bool> ShouldStartActivity { get; set; }

    public Func<MongoCommandFailedEvent, Task> CommandFailedCallback { get; set; }
    public Func<MongoCommandStartedEvent, Task> CommandStartedCallback { get; set; }
    public Func<MongoCommandSucceededEvent, Task> CommandSucceededCallback { get; set; }
    
    public string ConnectionString { get; set; }
    
    public bool DebugMode { get; set; }
    
    public Func<Type, bool> ObjectSerializerConfiguration { get; set; } = type => true;
    
    public Dictionary<Type, Type> GenericSerializers { get; set; } = new();
    public Dictionary<Type, object> DiscriminatorConventions { get; set; } = new();
    public Dictionary<string, Type> Discriminators { get; set; } = new();
    public Dictionary<Type, object> Serializers { get; set; } = new();
}