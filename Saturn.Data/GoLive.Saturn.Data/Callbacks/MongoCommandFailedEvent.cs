using System;

namespace GoLive.Saturn.Data.Callbacks;

public struct MongoCommandFailedEvent
{
    public string CommandName { get; private set; }
    public string ConnectionId { get; private set; }
    public string DatabaseNamespace { get; private set; }
    public TimeSpan Duration { get; private set; }
    public Exception Failure { get; private set; }
    public long? OperationId { get; private set; }
    public int RequestId { get; private set; }
    public string ServiceId { get; private set; }
    public DateTime Timestamp { get; private set; }
    
    internal static MongoCommandFailedEvent FromMongoEvent(MongoDB.Driver.Core.Events.CommandFailedEvent mongoEvent)
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