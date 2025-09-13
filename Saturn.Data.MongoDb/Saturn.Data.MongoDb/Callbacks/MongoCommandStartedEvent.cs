using MongoDB.Bson;
using MongoDB.Driver.Core.Events;

namespace Saturn.Data.MongoDb.Callbacks;

public struct MongoCommandStartedEvent
{
    public string Command { get; internal set; }
    public string CommandName { get; internal set; }
    public string ConnectionId { get; internal set; }
    public string DatabaseNamespace { get; internal set; }
    public long? OperationId { get; internal set; }
    public int RequestId { get; internal set; }
    public string ServiceId { get; internal set; }
    public DateTime Timestamp { get; internal set; }

    internal static MongoCommandStartedEvent FromMongoEvent(CommandStartedEvent mongoEvent, string Command = "")
    {
        return new MongoCommandStartedEvent
        {
            Command = string.IsNullOrWhiteSpace(Command) ? mongoEvent.Command?.ToJson() : Command,
            CommandName = mongoEvent.CommandName,
            ConnectionId = mongoEvent.ConnectionId?.ToString(),
            DatabaseNamespace = mongoEvent.DatabaseNamespace?.ToString(),
            OperationId = mongoEvent.OperationId,
            RequestId = mongoEvent.RequestId,
            ServiceId = mongoEvent.ServiceId?.ToString(),
            Timestamp = mongoEvent.Timestamp
        };
    }
}