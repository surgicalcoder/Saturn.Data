using System;
using MongoDB.Driver.Core.Events;

namespace GoLive.Saturn.Data.Callbacks;

public struct MongoCommandSucceededEvent
{
    public string CommandName { get; private set; }
    public string ConnectionId { get; private set; }
    public string DatabaseNamespace { get; private set; }
    public TimeSpan Duration { get; private set; }
    public long? OperationId { get; private set; }
    public int RequestId { get; private set; }
    public string ServiceId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string Reply { get; private set; }

    internal static MongoCommandSucceededEvent FromMongoEvent(CommandSucceededEvent mongoEvent)
    {
        return new MongoCommandSucceededEvent
        {
            CommandName = mongoEvent.CommandName,
            ConnectionId = mongoEvent.ConnectionId?.ToString(),
            DatabaseNamespace = mongoEvent.DatabaseNamespace?.ToString(),
            Duration = mongoEvent.Duration,
            OperationId = mongoEvent.OperationId,
            RequestId = mongoEvent.RequestId,
            ServiceId = mongoEvent.ServiceId?.ToString(),
            Timestamp = mongoEvent.Timestamp,
            Reply = mongoEvent.Reply?.ToString()
        };
    }
}