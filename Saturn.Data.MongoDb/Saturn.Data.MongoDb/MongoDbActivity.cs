using System.Collections.Concurrent;
using System.Diagnostics;
using MongoDB.Driver.Core.Events;

namespace Saturn.Data.MongoDb;

internal static class MongoDbActivity
{
    internal static readonly ActivitySource Source = new("Saturn.Data.MongoDb");

    private static readonly ConcurrentDictionary<ActivityKey, Activity> Activities = new();

    internal static void Start(CommandStartedEvent e)
    {
        if (!Source.HasListeners())
        {
            return;
        }

        var activity = Source.StartActivity("mongodb.command", ActivityKind.Client);

        if (activity == null)
        {
            return;
        }

        activity.SetTag("db.system", "mongodb");
        activity.SetTag("db.operation", e.CommandName);

        if (e.DatabaseNamespace != null)
        {
            activity.SetTag("db.name", e.DatabaseNamespace.DatabaseName);
            activity.SetTag("db.mongodb.namespace", e.DatabaseNamespace.ToString());
        }

        if (e.ConnectionId?.ServerId?.EndPoint != null)
        {
            activity.SetTag("server.address", e.ConnectionId.ServerId.EndPoint.ToString());
        }

        var key = new ActivityKey(e.ConnectionId?.ToString(), e.OperationId, e.RequestId);
        Activities.TryAdd(key, activity);
    }

    internal static void Stop(CommandSucceededEvent e)
    {
        var key = new ActivityKey(e.ConnectionId?.ToString(), e.OperationId, e.RequestId);

        if (Activities.TryRemove(key, out var activity))
        {
            activity.Dispose();
        }
    }

    internal static void Fail(CommandFailedEvent e)
    {
        var key = new ActivityKey(e.ConnectionId?.ToString(), e.OperationId, e.RequestId);

        if (Activities.TryRemove(key, out var activity))
        {
            activity.SetStatus(ActivityStatusCode.Error);

            if (e.Failure != null)
            {
                activity.AddException(e.Failure);
            }

            activity.Dispose();
        }
    }

    internal static (string TraceId, string SpanId) GetTraceInfo(string? connectionId, long? operationId, int requestId)
    {
        var key = new ActivityKey(connectionId, operationId, requestId);

        if (Activities.TryGetValue(key, out var activity))
        {
            return (activity.TraceId.ToString(), activity.SpanId.ToString());
        }

        return (string.Empty, string.Empty);
    }

    private readonly record struct ActivityKey(string? ConnectionId, long? OperationId, int RequestId);
}
