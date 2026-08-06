# Data Change Feed — Follow-up Features Guide

Companion to [data-change-feed.md](data-change-feed.md). This guide covers two new features
shipped as follow-ups to the core change feed: the `OnWriteFailed` hook and the
`QueueChangeFeedSink` push-based transport.

---

## 1. `OnWriteFailed` Hook

### 1.1 What it is

A single hook point on `IRepositoryWriteBehavior` that fires **only** when a repository write
fails with an exception. Useful for invalidating caches on failed updates, alerting, metrics,
dead-lettering, or logging — any reaction to a write that did not land.

```csharp
ValueTask OnWriteFailed<TItem>(RepositoryWriteContext<TItem> context, Exception exception)
    where TItem : Entity;
```

### 1.2 When it fires

| Scenario                                            | `OnWriteFailed` fires?                                    |
|-----------------------------------------------------|-----------------------------------------------------------|
| `Update` with no match (`FailedToUpdateException`)  | **Yes**                                                   |
| Provider exception (network, constraint, etc.)      | **Yes**                                                   |
| `Insert` / `Save` / `Upsert` succeeds               | No — `After*` fires instead                               |
| Partial bulk failure (some rows succeed, some fail) | **No** — reported via `result.PartialFailure` on `After*` |
| `Before*` hook throws (write never runs)            | No — write never happened                                 |
| `After*` hook throws                                | No — separate error path                                  |
| `Suppress = true` (cascade-internal write)          | No — suppressed writes are invisible                      |

### 1.3 Key behaviors

- **Single method, not per-operation.** `context.Operation` tells you what failed if you need
  to branch.
- **Default no-op.** Existing behavior implementations compile unchanged.
- **`Suppress` gates it.** Cascade-internal writes (`Suppress = true`) do not trigger
  `OnWriteFailed`.
- **Hook exceptions swallowed.** If your `OnWriteFailed` implementation throws, the exception
  is logged (`Debug.WriteLine`) and swallowed. The original write exception still propagates to
  the caller.
- **Fires before the exception propagates.** The hook runs inside the catch block, after the
  exception is caught but before it rethrows.

### 1.4 Implementation pattern

```csharp
public class MetricsBehavior : IRepositoryWriteBehavior
{
    private readonly IMetricsCollector metrics;

    public MetricsBehavior(IMetricsCollector metrics)
    {
        this.metrics = metrics;
    }

    public ValueTask OnWriteFailed<TItem>(
        RepositoryWriteContext<TItem> context,
        Exception exception)
        where TItem : Entity
    {
        metrics.Increment(
            $"write.failed.{context.Operation.ToString().ToLower()}",
            new { EntityType = typeof(TItem).Name, Error = exception.GetType().Name });

        return ValueTask.CompletedTask;
    }
}
```

### 1.5 Transaction interaction

| Provider        | When hook runs                                      | Transaction state                                                  |
|-----------------|-----------------------------------------------------|--------------------------------------------------------------------|
| Mongo (with tx) | Inside session, before rollback                     | Tx active — hook can read but **must not** write to the same store |
| Mongo (no tx)   | Immediately after exception caught                  | N/A                                                                |
| LiteDbX         | After op throws, before tx rollback (if tx present) | Tx active                                                          |
| Stellar         | Immediately after exception caught                  | N/A                                                                |

**Guideline:** Keep `OnWriteFailed` implementations side-effect-free with respect to the
source database. External writes (dead-letter queue, log sink, metrics) are fine. Writes to
the same database will fail or corrupt rollback.

### 1.6 Full example

```csharp
public class CacheInvalidationBehavior : IRepositoryWriteBehavior
{
    private readonly ICache cache;
    private readonly ILogger<CacheInvalidationBehavior> logger;

    public CacheInvalidationBehavior(ICache cache, ILogger<CacheInvalidationBehavior> logger)
    {
        this.cache = cache;
        this.logger = logger;
    }

    // On success: invalidate cache
    public ValueTask AfterUpdate<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
    {
        foreach (var id in result.EntityIds)
        {
            cache.Remove($"{typeof(TItem).Name}:{id}");
        }
        return ValueTask.CompletedTask;
    }

    // On failure: log + alert
    public ValueTask OnWriteFailed<TItem>(RepositoryWriteContext<TItem> context, Exception exception)
        where TItem : Entity
    {
        logger.LogWarning(exception,
            "Write {Operation} on {EntityType} failed: {Error}",
            context.Operation, typeof(TItem).Name, exception.Message);

        // Fire-and-forget alert (don't await in production)
        _ = AlertAsync(context.Operation, typeof(TItem).Name, exception);
        return ValueTask.CompletedTask;
    }

    private async ValueTask AlertAsync(RepositoryWriteOperation op, Type entityType, Exception ex)
    {
        // Send to your alerting system
    }
}
```

### 1.7 Registration

Register alongside your other behaviors. Order does not matter for `OnWriteFailed` — it fires
independently of `Before*`/`After*` dispatch order.

```csharp
var options = new RepositoryOptions
{
    GetCollectionName = type => type.Name,
    WriteBehaviors = new List<IRepositoryWriteBehavior>
    {
        new ChangeFeedBehavior(sink, "main",
            new ChangeFeedBehaviorOptions { Enabled = true }),
        new CacheInvalidationBehavior(cache, logger),
        new MetricsBehavior(metrics)
    }
};
```

---

## 2. `QueueChangeFeedSink` — Push-Based Delivery

### 2.1 What it is

A generic `IChangeFeedSink` that delegates event publication to an `IChangeFeedPublisher`
abstraction. Instead of storing events in an outbox table for polling, events are pushed
immediately to whatever transport you wire up: MessagePipe, Azure Service Bus, RabbitMQ,
in-process pub/sub, or any custom channel.

### 2.2 How it differs from `OutboxChangeFeedSink`

|                 | `OutboxChangeFeedSink`                     | `QueueChangeFeedSink`                          |
|-----------------|--------------------------------------------|------------------------------------------------|
| Storage         | `__change_feed` table in same DB           | None — events pushed immediately               |
| Delivery        | Pull via `ChangeFeedPoller`                | Push via `IChangeFeedPublisher`                |
| Ordering        | Strict per `(Source, EntityType)` sequence | Depends on transport (FIFO for MessagePipe)    |
| Durability      | Durable (survives restart)                 | Transport-dependent (transient for in-memory)  |
| Transactions    | Participates in DB tx when available       | Ignores `transaction` parameter                |
| Poller required | Yes                                        | No — consumers subscribe through the publisher |

### 2.3 The `IChangeFeedPublisher` interface

```csharp
public interface IChangeFeedPublisher
{
    ValueTask PublishAsync(DataChangeEvent change, CancellationToken ct);
}
```

Implement this interface to bridge any transport. The `DataChangeEvent` envelope carries all
metadata: operation, entity type, entity ids, payload (if any), sequence, source, and
timestamps.

### 2.4 MessagePipe adapter

The most common in-process scenario. Install `MessagePipe` then implement the publisher:

```csharp
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using MessagePipe;

public sealed class MessagePipeChangeFeedPublisher : IChangeFeedPublisher
{
    private readonly IPublisher publisher;

    public MessagePipeChangeFeedPublisher(IPublisher publisher)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public ValueTask PublishAsync(DataChangeEvent change, CancellationToken ct)
    {
        publisher.Publish(change);
        return ValueTask.CompletedTask;
    }
}
```

### 2.5 Registration

```csharp
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using MessagePipe;

// Register MessagePipe
services.AddMessagePipe();

// Register the publisher + sink
services.AddSingleton<IChangeFeedPublisher, MessagePipeChangeFeedPublisher>();
services.AddSingleton<IChangeFeedSink, QueueChangeFeedSink>();

// Wire the behavior into repositories (same as outbox)
services.AddSingleton<IRepositoryWriteBehavior>(sp =>
    new ChangeFeedBehavior(
        sp.GetRequiredService<IChangeFeedSink>(),
        "main",
        new ChangeFeedBehaviorOptions { Enabled = true }));
```

**Do NOT register `ChangeFeedPoller`** when using `QueueChangeFeedSink`. The poller drains
zero events (push sinks return empty from `ReadAsync`). It is harmless but wasteful.

### 2.6 Consuming events

Subscribe via MessagePipe's `ISubscriber`:

```csharp
var sub = serviceProvider.GetRequiredService<ISubscriber>();

sub.Subscribe<DataChangeEvent>(async (change, ct) =>
{
    // Filter by entity type
    if (change.EntityType != typeof(Order))
        return;

    // Filter by operation
    if (change.Operation == RepositoryWriteOperation.Insert)
    {
        foreach (var id in change.EntityIds)
        {
            await search.IndexAsync(id);
        }
    }
    else if (change.Operation == RepositoryWriteOperation.Delete)
    {
        foreach (var id in change.EntityIds)
        {
            await search.RemoveAsync(id);
        }
    }
});
```

### 2.7 Consuming typed events

Use `DataChangeEvent<TItem>` for strongly-typed access:

```csharp
sub.Subscribe<DataChangeEvent<Order>>(async (change, ct) =>
{
    if (change.HasFullItems)
    {
        foreach (var order in change.TypedItems)
        {
            await cache.PutAsync(order.Id, order);
        }
    }
    else
    {
        foreach (var id in change.EntityIds)
        {
            var order = await repo.ById<Order>(id);
            await cache.PutAsync(id, order);
        }
    }
});
```

### 2.8 Custom transport example — Azure Service Bus

```csharp
public sealed class ServiceBusChangeFeedPublisher : IChangeFeedPublisher
{
    private readonly ServiceBusSender sender;

    public ServiceBusChangeFeedPublisher(ServiceBusSender sender)
    {
        this.sender = sender;
    }

    public async ValueTask PublishAsync(DataChangeEvent change, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(change);
        await sender.SendMessageAsync(new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            Subject = $"{change.Source}.{change.EntityType.Name}",
            // PartitionKey for ordering within source
            PartitionKey = change.Source
        }, ct);
    }
}
```

### 2.9 Transaction interaction

`QueueChangeFeedSink.AppendAsync` ignores the `transaction` parameter. Push sinks cannot
participate in database transactions. This is acceptable because:

- The write already succeeded when `After*` fires (and thus when the sink is called).
- Push delivery is inherently at-least-once; a crash between write and publish loses the
  event. This is the same trade-off as Stellar's outbox sink.
- If you need transactional guarantees, use `OutboxChangeFeedSink` instead.

### 2.10 Multiple sinks

You can register both an outbox sink and a push sink simultaneously. Both receive the same
events:

```csharp
// Outbox for durable polling
services.AddSingleton<IChangeFeedSink>(sp =>
    new MongoOutboxChangeFeedSink(database, "main"));

// ... but this doesn't work with the current single-sink model.
// For dual delivery, use a composite sink:
public sealed class CompositeChangeFeedSink : IChangeFeedSink
{
    private readonly IChangeFeedSink[] sinks;

    public CompositeChangeFeedSink(params IChangeFeedSink[] sinks)
    {
        this.sinks = sinks;
    }

    public async ValueTask AppendAsync(
        DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        foreach (var sink in sinks)
        {
            await sink.AppendAsync(change, transaction, ct);
        }
    }

    public ValueTask<IReadOnlyList<DataChangeEvent>> ReadAsync(
        string source, long afterSequence, int take, CancellationToken ct)
    {
        // Delegate to the outbox sink for polling
        return sinks[0].ReadAsync(source, afterSequence, take, ct);
    }
}
```

### 2.11 When to use which

| Use case                                           | Recommended sink                             |
|----------------------------------------------------|----------------------------------------------|
| Search index sync, cache invalidation, projections | `OutboxChangeFeedSink` (durable, replayable) |
| Real-time in-process notifications                 | `QueueChangeFeedSink` + MessagePipe          |
| Cross-service event propagation                    | `QueueChangeFeedSink` + ASB/RabbitMQ         |
| Audit logging                                      | `OutboxChangeFeedSink` (durable record)      |
| Both durability and real-time                      | Composite (outbox + queue)                   |

---

## 3. Combining Both Features

`OnWriteFailed` and `QueueChangeFeedSink` work independently but complement each other:

```csharp
// Publisher for real-time notifications
services.AddSingleton<IChangeFeedPublisher, MessagePipeChangeFeedPublisher>();
services.AddSingleton<IChangeFeedSink, QueueChangeFeedSink>();

// Behaviors
var options = new RepositoryOptions
{
    GetCollectionName = type => type.Name,
    WriteBehaviors = new List<IRepositoryWriteBehavior>
    {
        new ChangeFeedBehavior(sp.GetRequiredService<IChangeFeedSink>(), "main",
            new ChangeFeedBehaviorOptions { Enabled = true }),
        new MetricsBehavior(metrics),
        new CacheInvalidationBehavior(cache, logger)
    }
};
```

Flow for a successful write:

1. `Before*` hooks fire (validation, pre-checks)
2. Write executes
3. `After*` hooks fire — `ChangeFeedBehavior` pushes event, `CacheInvalidationBehavior`
   invalidates cache, `MetricsBehavior` records success

Flow for a failed write:

1. `Before*` hooks fire
2. Write throws exception
3. `OnWriteFailed` hooks fire — `MetricsBehavior` records failure, alerting fires
4. Exception propagates to caller
5. `After*` hooks do **not** fire

---

## 4. Migration Notes

### From `OutboxChangeFeedSink` to `QueueChangeFeedSink`

- Replace `IChangeFeedSink` registration with `QueueChangeFeedSink` + your publisher.
- Remove `ChangeFeedPoller` registration (not needed for push sinks).
- Update consumers to subscribe through the publisher instead of polling.
- Events are the same `DataChangeEvent` shape — no consumer code changes needed beyond
  subscription mechanism.

### Adding `OnWriteFailed`

- Add `IRepositoryWriteBehavior` implementations with `OnWriteFailed` — no interface changes
  needed (default no-op already exists).
- Register alongside existing behaviors.
- Existing behaviors that only implement `Before*`/`After*` continue to work unchanged.
