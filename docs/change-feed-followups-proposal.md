# Data Change Feed — Follow-up Proposals

Target repo: `D:\Work\Saturn.Data`
Status: design proposal, not approved.
Supersedes: deferred items from `docs/data-change-feed-proposal.md` §14.

---

## 15. `OnWriteFailed` Hook

### 15.1 Problem

Today, when a write fails (e.g. `FailedToUpdateException`, `FailedToUpsertException`, provider exceptions), the
exception propagates directly to the caller. There is no hook point for behaviors to react to failures — invalidating a
cache on a failed update, alerting, metrics, dead-lettering, etc. The `After*` hooks only fire on success.

### 15.2 Design

Add `OnWriteFailed` to `IRepositoryWriteBehavior`. Single generic method, NOT per-operation. Default no-op.

```csharp
ValueTask OnWriteFailed<TItem>(RepositoryWriteContext<TItem> context, Exception exception)
    where TItem : Entity => ValueTask.CompletedTask;
```

**Why single method, not `OnInsertFailed` / `OnUpdateFailed` / ...?**

- Failure is exceptional — consumers rarely need per-operation failure handling.
- One method keeps surface small, dispatcher simple.
- `context.Operation` enum is available if consumer needs to switch.
- `Before*` / `After*` split by operation makes sense (hot path, distinct semantics). Failure is cold path.

### 15.3 Dispatcher changes

`BehaviorDispatcher` gains:

```csharp
public static async ValueTask DispatchOnWriteFailedAsync<TItem>(
    IList<IRepositoryWriteBehavior> behaviors,
    RepositoryWriteContext<TItem> context,
    Exception exception)
    where TItem : Entity
{
    if (behaviors == null || behaviors.Count == 0 || context.Suppress)
    {
        return;
    }

    foreach (var behavior in behaviors)
    {
        try
        {
            await behavior.OnWriteFailed(context, exception);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnWriteFailed hook '{behavior.GetType().Name}' threw: {ex}");
        }
    }
}
```

**Key semantics:**

- `Suppress` gates `OnWriteFailed` (cascade-internal writes not surfaced).
- Hook exceptions swallowed + logged (same as `After*`).
- Fires **after** exception caught, **before** it propagates to caller.
- Receives original `Exception`, not a wrapper.

### 15.4 Provider wiring

Each provider wraps writes in try/catch:

```csharp
try
{
    await ApplyWriteBehaviors(operation, context);
    var result = await WriteAsync(...);
    await ApplyAfterBehaviors(operation, context, result);
}
catch (Exception ex)
{
    await ApplyOnWriteFailed(context, ex);
    throw;
}
```

Helper on each provider base:

```csharp
protected virtual async ValueTask ApplyOnWriteFailed<TItem>(
    RepositoryWriteContext<TItem> context,
    Exception exception)
    where TItem : Entity
{
    await BehaviorDispatcher.DispatchOnWriteFailedAsync(options.WriteBehaviors, context, exception);
}
```

**Scope:** Fires ONLY for exceptions from the write itself (provider op or ack/throw checks). Does NOT fire for
`Before*` hook failures (pre-write — write never ran). Does NOT fire for `After*` hook failures (swallowed
separately).

### 15.5 `RepositoryWriteContext` — no changes

`OnWriteFailed` does not need `RepositoryWriteResult` — the write failed, no result to carry. Context provides
everything the hook needs (operation, items, ids, filter, etc.).

### 15.6 Transaction interaction

| Provider          | OnWriteFailed fires                                 | Tx state                                          |
|-------------------|-----------------------------------------------------|---------------------------------------------------|
| Mongo, tx present | Inside session, before rollback                     | Tx active; hook can read but NOT write to same tx |
| Mongo, no tx      | Immediately after exception caught                  | N/A                                               |
| LiteDbX           | After op throws, before tx rollback (if tx present) | Tx active                                         |
| Stellar           | Immediately after exception caught                  | N/A                                               |

**Decision:** Hooks run inside tx scope when present. Hooks can read state but should NOT write to the same store
(those would fail or corrupt rollback). External writes (dead-letter, log) should be fire-and-forget or deferred.

### 15.7 Tests

| #  | Case                                             | Asserts                                                                                   |
|----|--------------------------------------------------|-------------------------------------------------------------------------------------------|
| 15 | `Update` throws → `OnWriteFailed` fires          | Exception propagates; hook called with same exception and context                         |
| 16 | `Insert` succeeds → `OnWriteFailed` NOT fired    | Only `AfterInsert` fires                                                                  |
| 17 | `OnWriteFailed` hook throwing → swallowed        | Original exception still propagates; hook exception logged, not thrown                    |
| 18 | `Suppress=true` → `OnWriteFailed` NOT fired      | Cascade-internal writes do not trigger failure hooks                                      |
| 19 | Bulk partial failure → `OnWriteFailed` NOT fired | Partial failures report via `result.PartialFailure`; they don't throw, so no failure hook |

---

## 16. `QueueChangeFeedSink` — Generic Push-Based Sink

### 16.1 Problem

`OutboxChangeFeedSink` is pull-based: events land in a table, `ChangeFeedPoller` drains sequentially. Consumers that
want push delivery (real-time notifications, in-process pub/sub like MessagePipe, Azure Service Bus, RabbitMQ) must
poll. A generic push sink behind `IChangeFeedSink` decouples the `ChangeFeedBehavior` from the delivery mechanism.

### 16.2 Design

Split into two concerns:

1. **`IChangeFeedSink`** — unchanged. `AppendAsync` publishes; `ReadAsync` returns empty.
2. **`IChangeFeedPublisher`** — pluggable transport abstraction. `QueueChangeFeedSink` delegates to it.

```csharp
public interface IChangeFeedPublisher
{
    ValueTask PublishAsync(DataChangeEvent change, CancellationToken ct);
}
```

```csharp
public sealed class QueueChangeFeedSink : IChangeFeedSink
{
    private readonly IChangeFeedPublisher publisher;

    public QueueChangeFeedSink(IChangeFeedPublisher publisher)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public ValueTask AppendAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
        => publisher.PublishAsync(change, ct);

    public ValueTask<IReadOnlyList<DataChangeEvent>> ReadAsync(
        string source, long afterSequence, int take, CancellationToken ct)
        => new ValueTask<IReadOnlyList<DataChangeEvent>>(Array.Empty<DataChangeEvent>());
}
```

**`ReadAsync` returns empty.** Push sinks have no stored rows — consumers receive events via the publisher, not
polling. `ChangeFeedPoller` drains zero events. Consumers using `QueueChangeFeedSink` should NOT register the poller;
they subscribe directly through the publisher.

### 16.3 MessagePipe adapter

```csharp
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

Consumer subscription via `ISubscriber`:

```csharp
// Registration
services.AddMessagePipe();
services.AddSingleton<IChangeFeedPublisher, MessagePipeChangeFeedPublisher>();
services.AddSingleton<IChangeFeedSink, QueueChangeFeedSink>();

// Subscription
var sub = serviceProvider.GetRequiredService<ISubscriber>();
sub.Subscribe<DataChangeEvent>(async (message, ct) =>
{
    // filter by EntityType, Operation, etc.
});
```

**Why not `IChangeFeedPublisher<TMessage>`?** The publisher sends `DataChangeEvent` (the envelope). Consumers filter
by `EntityType` or `Operation` at the handler level. Keeps publisher simple, avoids generic type-per-entity explosion.

### 16.4 Transaction interaction

`QueueChangeFeedSink.AppendAsync` ignores the `transaction` parameter — push sinks cannot participate in database
transactions. Acceptable because:

- Write already succeeded when `After*` fires (and thus when sink is called).
- Push delivery is inherently at-least-once; crash between write and publish loses the event. Same trade-off as
  Stellar's outbox sink (original proposal §6.3).
- Consumers needing transactional guarantees should use `OutboxChangeFeedSink` instead.

### 16.5 Ordering

MessagePipe delivers events in publication order within a single publisher. Cross-entity-type ordering depends on
transport (MessagePipe guarantees FIFO within a channel). For external transports (ASB, RabbitMQ), ordering depends on
broker configuration (partition key = `Source`).

### 16.6 DI registration

```csharp
// Generic — user provides their own publisher
services.AddSingleton<IChangeFeedPublisher, TConcretePublisher>();
services.AddSingleton<IChangeFeedSink, QueueChangeFeedSink>();

// Convenience extension for MessagePipe (in abstractions or a new package)
public static class ChangeFeedQueueExtensions
{
    public static IServiceCollection AddMessagePipeChangeFeed(this IServiceCollection services)
    {
        services.AddMessagePipe();
        services.AddSingleton<IChangeFeedPublisher, MessagePipeChangeFeedPublisher>();
        services.AddSingleton<IChangeFeedSink, QueueChangeFeedSink>();
        return services;
    }
}
```

### 16.7 `ChangeFeedPoller` interaction

When `QueueChangeFeedSink` is registered, `ChangeFeedPoller` drains nothing (every `ReadAsync` returns empty).
Harmless but wasteful.

- **Option A (v1):** Document that push-sink consumers should not register the poller.
- **Option B (future):** Add `bool SupportsPolling` to `IChangeFeedSink`; poller skips sinks returning `false`.

Recommendation: Option A for v1. Poller is a separate registration.

### 16.8 Tests

| #  | Case                                       | Asserts                                                                   |
|----|--------------------------------------------|---------------------------------------------------------------------------|
| 20 | `AppendAsync` publishes to publisher       | `IChangeFeedPublisher.PublishAsync` called with correct `DataChangeEvent` |
| 21 | `ReadAsync` returns empty                  | Poller drains nothing; no exception                                       |
| 22 | Publisher throwing → swallowed by `After*` | Exception caught in `DispatchAfterAsync`; write succeeds                  |
| 23 | Multiple sinks (outbox + queue) coexist    | Both `AppendAsync` called; both receive same event                        |

---

## 17. Diff / Audit Capture

### 17.1 Problem

The change feed records *that a change happened and what the final state is*, but not *what changed at the field
level*. An audit log needs before/after images and per-field diffs. The feed alone does not provide enough data:

| Operation  | Feed provides              | Missing for diff             |
|------------|----------------------------|------------------------------|
| Insert     | After-image (new entity)   | Nothing (no before-image)    |
| Update     | After-image (new entity)   | Before-image (old entity)    |
| Upsert     | After-image + `WasCreated` | Before-image when replacing  |
| Save       | After-image + `WasCreated` | Before-image when replacing  |
| Patch      | EntityIds + delta          | Before-image + after-image   |
| Increment  | EntityIds + delta          | Before-image + after-image   |
| Delete     | EntityIds                  | Before-image (entity state)  |
| HardDelete | EntityIds                  | Before-image (entity state)  |
| Restore    | EntityIds                  | Before-image (deleted state) |

### 17.2 Architecture

Two-layer design:

1. **`DiffCaptureBehavior : IRepositoryWriteBehavior`** — captures before-images in `Before*`, computes diffs in
   `After*`, emits `AuditEntry` records.
2. **`IAuditSink`** — stores audit entries (table, external system, append-only log).

```
Repo write
  │
  ▼ Before* → DiffCaptureBehavior reads entities, stores before-images
  │
  ▼ write (provider op)
  │
  ▼ After* → DiffCaptureBehavior reads entities again, computes diffs → IAuditSink
```

**Separate from the change feed.** The change feed records *what happened*; audit capture records *what changed at the
field level*. Both can be registered simultaneously.

### 17.3 Audit entry shape

```csharp
public sealed record AuditEntry
{
    public string AuditId { get; init; }                       // Guid N — unique key
    public string Source { get; init; }                        // provider + database
    public Type EntityType { get; init; }
    public RepositoryWriteOperation Operation { get; init; }
    public IReadOnlyCollection<EntityAuditSnapshot> Snapshots { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed record EntityAuditSnapshot
{
    public string EntityId { get; init; }
    public IReadOnlyList<FieldDiff> FieldDiffs { get; init; }
    public object? Before { get; init; }                      // full before-image (null for Insert)
    public object? After { get; init; }                       // full after-image (null for HardDelete)
}

public sealed record FieldDiff
{
    public string FieldPath { get; init; }                    // "Address.Street", "_v", etc.
    public object? Before { get; init; }
    public object? After { get; init; }
    public DiffKind Kind { get; init; }
}

public enum DiffKind
{
    Added,       // Before null/default → After has value
    Removed,     // Before has value → After null/default
    Modified,    // Both non-null, values differ
    Unchanged    // Both equal (filtered out by default)
}
```

### 17.4 `DiffCaptureBehavior` implementation

```csharp
public sealed class DiffCaptureBehavior : IRepositoryWriteBehavior
{
    private readonly IAuditSink auditSink;
    private readonly DiffCaptureOptions options;
    private readonly IEntityReader entityReader;  // reads entities from the store
    private readonly ConcurrentDictionary<string, object> beforeImages = new();

    public DiffCaptureBehavior(
        IAuditSink auditSink,
        IEntityReader entityReader,
        DiffCaptureOptions options = null) { ... }

    // Before* hooks: materialize entities, store before-images keyed by id
    // After* hooks: re-read entities, compute diffs, emit AuditEntry
}
```

**`IEntityReader`** — provider-specific interface for reading entities by id:

```csharp
public interface IEntityReader
{
    ValueTask<TItem?> ReadByIdAsync<TItem>(string id, CancellationToken ct)
        where TItem : Entity;

    ValueTask<IReadOnlyList<TItem>> ReadByIdsAsync<TItem>(
        IEnumerable<string> ids, CancellationToken ct)
        where TItem : Entity;
}
```

Each provider implements `IEntityReader` wrapping its collection access. This avoids `DiffCaptureBehavior` depending
on any specific provider — clean DI.

**Before-image capture (Before\*):**

For item-based operations (`context.Items` is populated):

- Store each entity's current state keyed by `entity.Id.ToString()`.
- `Insert`: no before-image needed (entity is new).
- `Update`/`Upsert`/`Save`/`Patch`/`Increment`: capture before the write.
- `Delete`/`HardDelete`/`Restore`: capture before the operation.

For filter-based operations (`context.Filter` is set, `context.Items` is null):

- **Cannot capture before-images in `Before*`** — affected entities unknown.
- Emit metadata-only `AuditEntry` with `Before = null`, `FieldDiffs = empty`.
- Known limitation; filter-based deletes are opaque to before-image capture.

**After\* diff computation:**

For item-based operations:

- Re-read entities via `IEntityReader` (if not `HardDelete`).
- Compare before vs after using reflection or configurable comparer.
- Build `FieldDiff` list, filter out `Unchanged` unless `IncludeUnchanged` is set.
- Emit `AuditEntry` with full snapshots and diffs.

For filter-based operations:

- Emit metadata-only `AuditEntry` (operation, entity type, timestamp, matched count from `result.MatchedIds`).

**Diff computation:**

```csharp
private static IReadOnlyList<FieldDiff> ComputeDiffs(
    object before, object after, DiffCaptureOptions options)
{
    var diffs = new List<FieldDiff>();
    var properties = before.GetType().GetProperties(
        BindingFlags.Public | BindingFlags.Instance);

    foreach (var prop in properties)
    {
        if (options.ExcludedFields.Contains(prop.Name))
            continue;

        var beforeVal = prop.GetValue(before);
        var afterVal = prop.GetValue(after);

        if (Equals(beforeVal, afterVal) && !options.IncludeUnchanged)
            continue;

        var kind = (beforeVal, afterVal) switch
        {
            (null, not null) => DiffKind.Added,
            (not null, null) => DiffKind.Removed,
            _ => DiffKind.Modified
        };

        diffs.Add(new FieldDiff
        {
            FieldPath = prop.Name,
            Before = beforeVal,
            After = afterVal,
            Kind = kind
        });
    }

    return diffs;
}
```

**Performance:**

- Reflection-based diff acceptable for audit workloads (not hot path). For high-throughput: `IFieldDiffStrategy`
  interface for compiled expression trees or source-generated comparers.
- Before-images held in `ConcurrentDictionary` keyed by entity id, released after `After*` completes. Memory bounded
  by batch size.
- Filter-based operations skip before-image capture — no extra reads.

### 17.5 `IAuditSink` interface

```csharp
public interface IAuditSink
{
    ValueTask AppendAsync(AuditEntry entry, CancellationToken ct);
}
```

**Implementations:**

- **`TableAuditSink`** — writes `AuditEntry` as JSON to an audit table (same DB or external). Stores `AuditId`,
  `EntityType`, `Operation`, `TimestampUtc`, `SnapshotsJson`, `CorrelationId`.
- **`ExternalAuditSink`** — pushes to external audit system (Seq, Elastic, etc.) via `IHttpClientFactory`.
- **`CompositeAuditSink`** — fans out to multiple sinks.

### 17.6 DI registration

```csharp
// Mongo with audit capture
services.AddMongoChangeFeed(db, "main");
services.AddSingleton<IAuditSink, TableAuditSink>();
services.AddSingleton<IEntityReader, MongoEntityReader>();
services.AddSingleton<IRepositoryWriteBehavior>(sp =>
    new DiffCaptureBehavior(
        sp.GetRequiredService<IAuditSink>(),
        sp.GetRequiredService<IEntityReader>(),
        new DiffCaptureOptions { ExcludedFields = ["_v", "UpdatedAt"] }));
```

`DiffCaptureBehavior` registered alongside `ChangeFeedBehavior`. Both fire independently:

- `ChangeFeedBehavior.After*` emits to change feed (what happened).
- `DiffCaptureBehavior.After*` emits to audit sink (what changed at field level).

### 17.7 `DiffCaptureOptions`

```csharp
public sealed class DiffCaptureOptions
{
    public HashSet<string> ExcludedFields { get; init; } = new() { "_v" };
    public bool IncludeUnchanged { get; init; } = false;
    public bool CaptureInsertDiffs { get; init; } = false;   // Insert: diff against null (all = Added)
    public bool CaptureDeleteDiffs { get; init; } = true;    // Delete: re-read before delete
    public Func<Type, IEnumerable<PropertyInfo>>? PropertyFilter { get; init; }
}
```

### 17.8 Behavior ordering

- `DiffCaptureBehavior.Before*` runs before the write (captures before-images).
- `ChangeFeedBehavior.After*` runs after the write (emits to change feed).
- `DiffCaptureBehavior.After*` runs after the write (computes diffs, emits to audit sink).

Registration order in `RepositoryOptions.WriteBehaviors` determines execution order.

**Recommended:** `DiffCaptureBehavior` first, then `ChangeFeedBehavior`. This ensures before-images are captured before
the feed behavior runs.

### 17.9 Interaction with `Suppress`

`Suppress` gates both `Before*` and `After*` dispatch. Cascade-internal writes produce neither change-feed events nor
audit entries.

### 17.10 Tests

| #  | Case                                                  | Asserts                                                                     |
|----|-------------------------------------------------------|-----------------------------------------------------------------------------|
| 24 | `Update` → before/after images captured               | `AuditEntry.Snapshots` has before + after; `FieldDiff` shows changed fields |
| 25 | `Insert` → before = null, after = entity              | All fields marked `Added` when `CaptureInsertDiffs = true`                  |
| 26 | `Delete` → before = entity, after = null              | All fields marked `Removed`                                                 |
| 27 | `Patch` → before + delta applied → after              | `FieldDiff` shows patched fields only                                       |
| 28 | Filter-based `Delete` → metadata-only entry           | `Snapshots` empty; operation + timestamp present                            |
| 29 | `ExcludedFields` honored                              | `_v` and configured fields not in `FieldDiffs`                              |
| 30 | `Suppress=true` → no audit entry                      | Cascade-internal writes produce no audit entries                            |
| 31 | Audit sink throwing → swallowed (does not fail write) | Write succeeds; sink exception logged                                       |

---

## 18. Implementation Phases

### Phase 6 — OnWriteFailed

- [ ] Add `OnWriteFailed` to `IRepositoryWriteBehavior` (default no-op)
- [ ] Add `DispatchOnWriteFailedAsync` to `BehaviorDispatcher`
- [ ] Wire try/catch + `ApplyOnWriteFailed` into Mongo, LiteDbX, Stellar providers
- [ ] Tests 15–19

### Phase 7 — QueueChangeFeedSink

- [ ] Add `IChangeFeedPublisher` interface in abstractions
- [ ] Add `QueueChangeFeedSink` in abstractions
- [ ] Add `MessagePipeChangeFeedPublisher` adapter (separate package or same abstractions)
- [ ] Add `AddMessagePipeChangeFeed` DI extension
- [ ] Tests 20–23

### Phase 8 — Diff/Audit Capture

- [ ] Add `IAuditSink`, `IEntityReader`, `AuditEntry`, `EntityAuditSnapshot`, `FieldDiff`, `DiffKind`,
  `DiffCaptureOptions` in abstractions
- [ ] Add `DiffCaptureBehavior` in abstractions
- [ ] Add `TableAuditSink` provider implementation (per-provider or shared)
- [ ] Add `IEntityReader` provider implementations
- [ ] Tests 24–31

Phase 6 → `patch` bump. Phase 7 → `minor` bump. Phase 8 → `minor` bump.
