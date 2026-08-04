# Data Change Feed — Usage Guide

How to register, configure, and consume the data change feed. Companion to
[docs/data-change-feed-proposal.md](data-change-feed-proposal.md) — the proposal holds design rationale (§7), decisions
(§13), and the phased rollout (§11); this guide is the operator/developer reference for the shipped API.

## 1. What it gives you

A durable, replayable **change feed** for every write that succeeds through a repository:

- After-write events are appended to an **outbox** in the same database as the source data
  (`__change_feed`), with a monotonic per-`(Source, EntityType)` sequence assigned by a counter
  (`__change_feed_counters`).
- A `ChangeFeedPoller` drains the outbox in sequence order and delivers typed
  `DataChangeEvent<TItem>` to your subscriber.
- Delivery is **at-least-once** with a `ChangeId` idempotency key — safe to replay.

Typical consumers: search indexes, caches, projections, and integrations that must converge with the
primary store.

## 2. Concepts

| Term           | Meaning                                                                                                   |
|----------------|-----------------------------------------------------------------------------------------------------------|
| `Source`       | Logical app/database name for the feed. Distinguishes writes from different databases served by one sink. |
| `ChangeId`     | Per-event `Guid` (N format). Dedupe key; the same event always replays with the same `ChangeId`.          |
| `Sequence`     | Monotonic `long` per `(Source, EntityType)`, assigned by the counter outbox.                              |
| `IsPartial`    | `true` for `Patch`/`Increment` — event carries ids only, no full entity exists.                           |
| `HasFullItems` | `false` in `IdOnly` payload mode or for partial events.                                                   |
| Outbox         | `__change_feed` (+ `__change_feed_counters`) collections created lazily in the same database.             |

Core types (namespace `GoLive.Saturn.Data.Abstractions.ChangeFeed`):

- `DataChangeEvent` / `DataChangeEvent<TItem>`
- `IChangeFeedSink`, `OutboxChangeFeedSink`
- `ChangeFeedBehavior`, `ChangeFeedBehaviorOptions`, `FeedPayloadMode`
- `ChangeFeedPoller`, `IChangeFeed<TItem>`

## 3. Setup

Two steps: register the feed (one sink per app), then attach `ChangeFeedBehavior` to each repository whose
writes you want captured.

### 3.1 Register the feed

One `IChangeFeedSink` per application, shared by all repositories. The `Source` parameter is the logical
name you will also pass to subscriptions.

```csharp
using Saturn.Data.MongoDb;               // Mongo extension

var database = new MongoClient("mongodb://localhost:27017").GetDatabase("app");
services.AddMongoChangeFeed(database, "main");
```

```csharp
using Saturn.Data.LiteDbX.ChangeFeed;    // LiteDbX extension

var database = LiteDatabase.Open(@"Filename=""app.db"";Connection=Shared");
services.AddLiteDbChangeFeed(database, "main");
```

```csharp
using Saturn.Data.Stellar.ChangeFeed;    // Stellar extension

var database = new FastDB(new FastDbOptions { BaseDirectory = @"data\", DatabaseName = "app" });
services.AddStellarChangeFeed(database, "main");
```

Each extension registers `IChangeFeedSink` and a `ChangeFeedPoller` as singletons.

### 3.2 Wire the behavior into a repository

`ChangeFeedBehavior` is an `IRepositoryWriteBehavior`; add it to `RepositoryOptions.WriteBehaviors`.
The `source` must match the feed's `Source`.

```csharp
var sink = serviceProvider.GetRequiredService<IChangeFeedSink>();

var options = new RepositoryOptions
{
    GetCollectionName = type => type.Name,
    WriteBehaviors = new List<IRepositoryWriteBehavior>
    {
        new CascadeWriteBehavior(),                       // existing behaviors, order preserved
        new ChangeFeedBehavior(sink, "main")              // source must match registration
    }
};

var repo = new MongoDbRepository(options, new MongoDbRepositoryOptions { ConnectionString = "mongodb://localhost:27017/app" });
```

Without DI, construct everything directly:

```csharp
var sink = new MongoOutboxChangeFeedSink(database, "main");
repo.Options.WriteBehaviors.Add(new ChangeFeedBehavior(sink, "main"));
```

## 4. Options

```csharp
var options = new ChangeFeedBehaviorOptions
{
    PayloadMode = FeedPayloadMode.Item,   // or FeedPayloadMode.IdOnly
    FeedPartialOps = true                 // emit Patch/Increment events (default true)
};

new ChangeFeedBehavior(sink, "main", options);
```

- **`PayloadMode.Item`** (default): events carry post-write entity snapshots in `Items` /
  `TypedItems` (`HasFullItems = true`) whenever the write materialized the items.
- **`PayloadMode.IdOnly`**: `HasFullItems = false`, `Items` empty — consumers re-read on demand.
  Cheaper for large bulk writes.
- **`Patch` / `Increment`** always emit id-only events (`IsPartial = true`, `HasFullItems = false`)
  regardless of mode: no full entity exists. Set `FeedPartialOps = false` to suppress them entirely.

## 5. What fires for each operation

| Repository call           | `Operation`  | `Outcome`     | Notes                                                         |
|---------------------------|--------------|---------------|---------------------------------------------------------------|
| `Insert` / `Insert(bulk)` | `Insert`     | `Inserted`    | Single call per bulk insert; `EntityIds` holds all ids        |
| `Save` / `Upsert`         | `Upsert`     | `Merged`      | `WasCreated` tells insert-vs-replace                          |
| `Update`                  | `Update`     | `Updated`     | No-match throws `FailedToUpdateException`; nothing is emitted |
| `Delete` (soft)           | `Delete`     | `Deleted`     |                                                               |
| `HardDelete`              | `HardDelete` | `Deleted`     |                                                               |
| `Restore`                 | `Restore`    | `Restored`    |                                                               |
| `Patch`                   | `Patch`      | `Patched`     | Partial: id-only                                              |
| `Increment`               | `Increment`  | `Incremented` | Partial: id-only                                              |

Partial bulk writes report **honest** counts: `AffectedCount` / `FailedCount`, `EntityIds` /
`FailedIds`. Events are emitted for what actually landed, not for the whole request.

## 6. Consuming

Subscribe, then drive the poller from a timer or background service.

```csharp
var poller = serviceProvider.GetRequiredService<ChangeFeedPoller>();

await poller.For<Order>("main").SubscribeAsync(async (change, ct) =>
{
    if (change.IsPartial)
    {
        // Patch/Increment: id + delta only, re-read the entity if needed
        await cache.RefreshAsync(change.EntityIds.First());
        return;
    }

    if (change.HasFullItems)
    {
        foreach (var order in change.TypedItems)
        {
            await search.IndexAsync(order);
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

// periodic drain, e.g. from a BackgroundService timer
await poller.DrainAsync();
```

- `DrainAsync()` loops until the current backlog is empty (bounded batches), advancing the
  subscription watermark past every read event.
- Ordering: **strict ascending sequence per `(Source, EntityType)`** within a drain. Cross-type
  ordering is not guaranteed.
- At-least-once: outbox rows are never deleted on read. Replays deliver the same events with the
  same `ChangeId` — make handlers idempotent (dedupe on `ChangeId`).
- Start mid-stream: `SubscribeAsync(handler, afterSequence: 42L)`.

Replay a bounded range without a subscription:

```csharp
var feed = poller.For<Order>("main");
var events = await feed.ReadAsync(afterSequence: 0, take: 100);
```

## 7. Watermark & durability

- Watermarks are held **in memory** on the poller. A process restart re-reads from the head of the
  feed — safe only if handlers dedupe on `ChangeId`. Persist a watermark yourself if you need
  exactly-once-style forward progress across restarts.
- **Mongo**: the feed row is written in the caller's session/transaction when one is present
  (gapless sequences).
- **LiteDbX**: the feed row participates in the same in-process transaction; an aborted
  transaction removes its feed row.
- **Stellar**: no transactions → best-effort append; sequence gaps are possible (bounded by the
  write-then-feed desync window).

## 8. Cascade suppression

Writes executed with `RepositoryWriteContext.Suppress = true` skip `After*` dispatch entirely — no
feed event is emitted. Use this for cascade-internal writes so cascades don't self-feed.

## 9. Error isolation

`After*` hooks are isolated: if `ChangeFeedBehavior` (or any after-hook) throws, the exception is
swallowed and logged (`Debug.WriteLine`); the write result is unaffected. A feed outage never fails
a write.

## 10. Release notes / behavior changes

- **LiteDbX and Stellar now dispatch write behaviors** (`Before*` and `After*`) — previously only
  Mongo did. Existing `Before*`-only implementations (e.g. `CascadeWriteBehavior`) are unaffected:
  new `After*` members are default no-op `ValueTask`s.
- **`Update` with no match now throws `FailedToUpdateException`** and emits no feed event.
- **`Patch`** normalizes the JSON document (`$set` unwrap, `_id` skipped, `Version` bumped).
- New DI helpers `AddMongoChangeFeed` / `AddLiteDbChangeFeed` / `AddStellarChangeFeed` register
  `IChangeFeedSink` + `ChangeFeedPoller` singletons. Providers now depend on
  `Microsoft.Extensions.DependencyInjection` (10.0.8).

## 11. Storage

Outbox collections (`__change_feed`, `__change_feed_counters`) are created lazily in the source
database and are safe to leave in place; the feed is the source of truth for consumers, not a
cache. Sequence counters are keyed by `Source|EntityType.FullName`.

## 12. References

- Proposal: [docs/data-change-feed-proposal.md](data-change-feed-proposal.md)
- Contract tests (13 per provider): `Saturn.Data.Testing.Shared/ChangeFeed/ChangeFeedContractTests.cs`
- Poller tests (4 per provider): `Saturn.Data.Testing.Shared/ChangeFeed/ChangeFeedPollerTests.cs`
- DI smoke tests: `{MongoDb,LiteDbX,Stellar}.Tests/ChangeFeedDiTests.cs`
