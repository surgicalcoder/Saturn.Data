using System;
using MongoDB.Bson;

namespace GoLive.Saturn.Data.Callbacks;

public struct MongoCommandStartedEvent
{
    public string Command { get; private set; }
    public string CommandName { get; private set; }
    public string ConnectionId { get; private set; }
    public string DatabaseNamespace { get; private set; }
    public long? OperationId { get; private set; }
    public int RequestId { get; private set; }
    public string ServiceId { get; private set; }
    public DateTime Timestamp { get; private set; }

    internal static MongoCommandStartedEvent FromMongoEvent(MongoDB.Driver.Core.Events.CommandStartedEvent mongoEvent, string Command = "")
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