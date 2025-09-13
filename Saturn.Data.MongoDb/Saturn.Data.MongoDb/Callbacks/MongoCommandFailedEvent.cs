using MongoDB.Driver.Core.Events;

namespace Saturn.Data.MongoDb.Callbacks;

public struct MongoCommandFailedEvent
{
    public string CommandName { get; internal set; }
    public string ConnectionId { get; internal set; }
    public string DatabaseNamespace { get; internal set; }
    public TimeSpan Duration { get; internal set; }
    public Exception Failure { get; internal set; }
    public long? OperationId { get; internal set; }
    public int RequestId { get; internal set; }
    public string ServiceId { get; internal set; }
    public DateTime Timestamp { get; internal set; }

    internal static MongoCommandFailedEvent FromMongoEvent(CommandFailedEvent mongoEvent)
    {
        return new MongoCommandFailedEvent
        {
            CommandName = mongoEvent.CommandName,
            ConnectionId = mongoEvent.ConnectionId?.ToString(),
            DatabaseNamespace = mongoEvent.DatabaseNamespace?.ToString(),
            Duration = mongoEvent.Duration,
            Failure = mongoEvent.Failure,
            OperationId = mongoEvent.OperationId,
            RequestId = mongoEvent.RequestId,
            ServiceId = mongoEvent.ServiceId?.ToString(),
            Timestamp = mongoEvent.Timestamp
        };
    }
}