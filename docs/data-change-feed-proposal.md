# Data Change Feed & AfterWrite Hooks — Proposal

Target repo: `D:\Work\Saturn.Data`
Status: design proposal, not approved.

---

## 0. Phase Tracker

Progress log. Tick a phase off only when its **done** gate passes (tests green, build clean, files landed). Each phase
is a self-contained commit unit on `master`.

| # | Phase                | Status                | Done gate                                                                                            |
|---|----------------------|-----------------------|------------------------------------------------------------------------------------------------------|
| 0 | Chokepoint           | `[x]` done 2026-08-03 | `BehaviorDispatcher` in abstractions; LiteDbX + Stellar dispatch `Before*`; existing suites green    |
| 1 | After\* contract     | `[x]` done 2026-08-03 | `After*` members + `RepositoryWriteResult`; Mongo dispatches after-write; Mongo tests 1–8 green      |
| 2 | Provider after-hooks | `[x]` done 2026-08-03 | LiteDbX + Stellar `buildResult()` + `After*` dispatch; their tests 1–8 green                         |
| 3 | Feed core            | `[x]` done 2026-08-04 | `DataChangeEvent`, `IChangeFeedSink`, `OutboxChangeFeedSink`, `ChangeFeedBehavior`; tests 9–12 green |
| 4 | Delivery             | `[x]` done 2026-08-04 | `ChangeFeedPoller`, `IChangeFeed<TItem>`, DI extensions; tests 11–13 green                           |
| 5 | Docs                 | `[ ]` not started     | This tracker ticked; `docs/data-change-feed.md` usage guide written                                  |

Per-phase task list (detailed below in §11). Sub-items tick as completed:

- [x] **Phase 0** — dispatcher + provider `Before*` wiring
- [x] **Phase 1** — `After*` contract + Mongo after-dispatch
- [x] **Phase 2** — LiteDbX + Stellar after-hooks
- [x] **Phase 3** — feed core
- [x] **Phase 4** — delivery / poller
- [ ] **Phase 5** — docs

---

## 1. Goals & Non-Goals

### Goals

- Fire a notification **after** a write succeeds so consumers know the write actually landed — closing the desync window
  where a `BeforeWrite` hook assumed success.
- Cover every write shape: `Insert`, `Update`, `Upsert`, `Save`, `Delete`, `HardDelete`, `Restore`, `Patch`,
  `Increment` — single and bulk.
- Build a durable, replayable **data change feed** on top of those after-hooks (outbox-style), so downstream systems (
  search index, cache, projections, integrations) converge with the primary store.
- Keep the existing `IRepositoryWriteBehavior` contract non-breaking: adding members must not require existing
  implementors (e.g. `CascadeWriteBehavior`) to change.
- Provider-correct semantics: Mongo (session + tx), LiteDbX (in-proc tx), Stellar (no tx → best-effort).

### Non-Goals

- Exactly-once delivery. The feed is **at-least-once** with idempotency keys.
- Cross-database distributed transactions or sagas for feed consumers.
- A `WriteFailed` hook in v1. Failure is surfaced by the existing exceptions (`FailedToUpdateException`,
  `FailedToUpsertException`, provider exceptions). An `OnWriteFailed` hook is listed as a follow-up in §14.
- Event sourcing or full audit-log capture. The feed records *that a change happened and what it was*, not a per-field
  diff of every write (a diff/audit layer is a consumer of this feed).
- Backfill/replay of historical data through the live feed. Replay is a separate sweep (§7.6).

---

## 2. Current State & The Gap

### 2.1 What exists today

- `IRepositoryWriteBehavior` (`Saturn.Data.Abstractions`) declares only `Before*` hooks — `BeforeInsert`,
  `BeforeUpdate`, `BeforeUpsert`, `BeforeSave`, `BeforeDelete`, `BeforeHardDelete`, `BeforeRestore`, `BeforePatch`,
  `BeforeIncrement`. All are default-implemented `ValueTask`s.
- `RepositoryWriteContext<TItem>` is an immutable carrier: `Operation`, `Id`, `Ids`, `Items`, `Filter`,
  `ExpectedVersion`, `JsonDocument`, `UpdateDefinition`, `IncrementField`, `IncrementDelta`, `Transaction`,
  `CancellationToken`, `Suppress`.
- `RepositoryOptions.WriteBehaviors` / `ReadBehaviors` hold the registered behaviors.
- **Only MongoDb dispatches write behaviors**, and only *before* the operation, via
  `MongoDbRepository.ApplyWriteBehaviors` (MongoDbRepository.cs:306). It is called before `InsertOneAsync`/
  `UpdateOneAsync`/`BulkWriteAsync` etc. There is **no after-write dispatch anywhere**.
- **LiteDbX and Stellar do not dispatch write behaviors at all.** `Before*` hooks silently never fire for those
  providers. This is a latent gap (already flagged in `docs/cascade-deletion-proposal.md` §6.2).
- `IRepositoryReadBehavior.AfterMaterialization` is the precedent for an "after" hook on the read side.

### 2.2 The desync

A `BeforeInsert`/`BeforeUpdate` hook cannot know whether the write succeeded. If a consumer reacts to `Before*` (e.g.
invalidating a cache, enqueuing a projection, sending an event), a failed or partially-failed write leaves the consumer
ahead of the store — the desync the user is experiencing. The fix is a hook that fires **only after the provider
confirms the write**, carrying the outcome.

---

## 3. High-Level Architecture

Two layers:

1. **Hook layer** — `After*` methods on `IRepositoryWriteBehavior` + a `RepositoryWriteResult` outcome carrier.
   Dispatched by a shared `BehaviorDispatcher` (lifted from Mongo) that every provider runs as:
   `Before* → write → After*`.
2. **Feed layer** — `IChangeFeedBehavior : IRepositoryWriteBehavior` converts the post-write outcome into a
   `DataChangeEvent<TItem>` and hands it to an `IChangeFeedSink` (outbox table, queue, event bus). A poller drains the
   sink in sequence order; consumers subscribe via `IChangeFeed<TItem>`.

Both layers live in `GoLive.Saturn.Data.Abstractions` (interfaces, engine) with provider implementations in
`GoLive.Saturn.Data.{MongoDb,LiteDbX,Stellar}`.

```
Repo method ──▶ BehaviorDispatcher
                  │  foreach behavior: Before{Op}(ctx)
                  ▼
              provider write (await, ack/throw checks)
                  │  success?
                  ▼  yes
                  foreach behavior: After{Op}(ctx, result)
                          │
                          ▼
                 IChangeFeedBehavior ──▶ IChangeFeedSink (outbox / queue)
                                             │ poller (ordered, dedupe)
                                             ▼
                                        IChangeFeed<TItem> subscribers
```

---

## 4. Interface: AfterWrite Hooks

### 4.1 `IRepositoryWriteBehavior` additions

Mirror the existing `Before*` surface exactly. All members are default no-ops so existing implementors remain source-
and binary-compatible.

```csharp
public interface IRepositoryWriteBehavior
{
    // ... existing Before* methods unchanged ...

    ValueTask AfterInsert<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity => ValueTask.CompletedTask;

    ValueTask AfterUpdate<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity => ValueTask.CompletedTask;

    ValueTask AfterUpsert<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity => ValueTask.CompletedTask;

    ValueTask AfterSave<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity => ValueTask.CompletedTask;

    ValueTask AfterDelete<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity => ValueTask.CompletedTask;

    ValueTask AfterHardDelete<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity => ValueTask.CompletedTask;

    ValueTask AfterRestore<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity => ValueTask.CompletedTask;

    ValueTask AfterPatch<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity => ValueTask.CompletedTask;

    ValueTask AfterIncrement<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity => ValueTask.CompletedTask;
}
```

### 4.2 Why the signature takes `(context, result)`

- `context` is the **same instance** that `Before*` received, so `Transaction`, `CancellationToken`, `Suppress`,
  `Filter`, `Items` etc. flow through unchanged.
- `result` is a new, non-generic outcome carrier (the provider knows the outcome, the consumer should not have to
  reverse-engineer it). Making it non-generic keeps the dispatch helper uniform across providers and lets filter-based
  deletes (which never materialize `TItem`) report cleanly.

```csharp
public sealed class RepositoryWriteResult
{
    public RepositoryWriteOperation Operation { get; init; }
    public bool Succeeded { get; init; }                       // true when After* fires (may be partial)
    public bool PartialFailure { get; init; }                  // true when some rows failed but others succeeded
    public WriteOutcome Outcome { get; init; }                 // Inserted | Updated | Merged (upsert/save created-or-replaced) | Deleted | Restored | Patched | Incremented
    public int AffectedCount { get; init; }                    // rows actually written (bulk-aware)
    public int FailedCount { get; init; }                      // rows that failed (bulk partial failure)
    public IReadOnlyCollection<string> EntityIds { get; init; } // final ids (ids are assigned before write)
    public IReadOnlyCollection<string> FailedIds { get; init; } // ids that failed, when known
    public IReadOnlyCollection<string> MatchedIds { get; init; } // for filter-based Delete/HardDelete/Restore
    public bool WasCreated { get; init; }                      // Upsert/Save: true if insert path taken
    public object? RawResult { get; init; }                    // provider-specific (Mongo BulkWriteResult etc.)
    public DateTimeOffset CompletedAtUtc { get; init; }
}
```

`RepositoryWriteContext<TItem>` needs **no new members** for the basic feed. Optional fields (`Outcome`,
`AffectedCount`) are deliberately on `result`, not on `context`, to keep `Before*` semantics untouched.

---

## 5. Dispatch Semantics

### 5.1 The shared chokepoint

Lift Mongo's `ApplyWriteBehaviors` into a shared helper in abstractions so all three providers run the identical
dispatch sequence:

```csharp
protected static async ValueTask RunWriteBehaviorsAsync<TItem>(
    RepositoryWriteOperation operation,
    RepositoryWriteContext<TItem> context,
    Func<ValueTask> write,
    Func<RepositoryWriteResult> buildResult,
    IList<IRepositoryWriteBehavior> behaviors,
    ILogger? logger)
    where TItem : Entity
{
    foreach (var b in behaviors) await DispatchBeforeAsync(b, operation, context);
    await write();                       // provider op; throws on failure
    var result = buildResult();          // provider builds outcome from ack/throw checks
    foreach (var b in behaviors) await DispatchAfterAsync(b, operation, context, result);
}
```

- `write()` is a `Func<ValueTask>` so the dispatcher is provider-agnostic; the provider closure owns
  `ExecuteWithTransaction` (Mongo), the LiteDB call, or the FastDB call.
- `buildResult()` runs **after** the write and the provider's own ack/throw checks (Mongo `IsAcknowledged`/
  `MatchedCount`, LiteDbX `bool Update`, Stellar completed `Task`), so `Succeeded` is authoritative.
- `After*` never fires when `write()` throws.

### 5.2 Granularity

One `After*` call per repository method invocation, **not** per item. A bulk `Insert(List<T>)` of 1000 rows → one
`AfterInsert` with `Items` (1000) and `EntityIds` (1000). The feed behavior batches these forward. Consumers that need
per-row processing iterate `result.EntityIds`.

### 5.3 Fire order relative to transactions

This is the decision that prevents desync.

| Provider          | After* fires                                                              | Feed durability                                                               |
|-------------------|---------------------------------------------------------------------------|-------------------------------------------------------------------------------|
| Mongo, tx present | Inside the session, **before commit**                                     | Feed row written in the same session → atomic with the write                  |
| Mongo, no tx      | Immediately after the write returns                                       | Feed row written immediately; small crash window between write and feed write |
| LiteDbX           | After the op, before `tx.CommitAsync()` when tx present; else immediately | In-proc tx covers both when tx used                                           |
| Stellar           | Immediately after the op completes                                        | No tx → best-effort; feed write is a second, compensating write               |

When a transaction is in flight, `After*` runs inside it so an outbox-style feed row commits atomically with the data
write. When no transaction exists, `After*` runs before the method returns — same ordering, no atomicity guarantee (
Stellar is inherently best-effort).

### 5.4 Error isolation

The primary write already happened when `After*` runs, so an `After*` failure **must not** be able to corrupt it.

- **Decision: exceptions from `After*` hooks are swallowed and logged, universally, including in-tx.** If a hook throws
  inside a transaction the write is **not** rolled back — a notification failure is not our scope to propagate. The
  repository method returns success; the feed is eventually reconciled by replay (§7.6).
- Hooks that need their own failure handling (retry, dead-letter) implement it internally.
- Partial bulk failures do not throw; they surface as `result.PartialFailure = true` with `AffectedCount` /
  `FailedCount` / `FailedIds` populated (§6.1).

### 5.5 `Suppress` interaction

`RepositoryWriteContext.Suppress` currently gates cascade recursion (cascade-internal writes re-enter the repo with
`Suppress=true`). **Decision: `Suppress` also gates the feed** — cascade-internal writes are real data changes but are
considered internal bookkeeping, so `After*` hooks (including the change-feed behavior) **do not run** when
`Suppress=true`. The dispatcher checks `context.Suppress` before invoking `After*`. Feed consumers see application-level
writes only; a separate option to feed cascade traffic is deferred (§13.5).

---

## 6. Provider-by-Provider

### 6.1 MongoDb

- `MongoDbRepository.ApplyWriteBehaviors` is replaced by the shared dispatcher. Every write method (`Repository.cs`
  lines 14–495) wraps its `ExecuteWithTransaction` closure in `RunWriteBehaviorsAsync`.
- `buildResult()` derives counts from `BulkWriteResult` (`InsertedCount`, `ModifiedCount`, `Upserts`),
  `ReplaceOneResult`, `UpdateResult`, `DeleteResult`. `WasCreated` = `UpsertedId != null` / `Upserts.Count > 0` for
  upsert/save.
- Partial bulk failure: `BulkWriteResult` reports per-op success, so `result.PartialFailure`, `FailedCount`, and
  `FailedIds` are populated accurately (e.g. 7 inserted, 3 failed). `After*` fires whenever ≥ 1 row succeeded; the
  event carries the honest `AffectedCount`/`FailedCount` (§5.4, §13.3).
- When a transaction session is passed, the dispatcher's `After*` (and the feed row write) run inside that session
  before `CommitAsync`.

### 6.2 LiteDbX

- No write-behavior dispatch exists today. Wiring the shared dispatcher into `LiteDbRepository.Repository.cs` fixes the
  latent `Before*` gap **and** adds `After*` in one pass.
- `buildResult()`: `Insert` returns `BsonValue`/count; `Update` returns `bool` (false → `FailedToUpdateException`, so
  `After*` never fires with `Succeeded=false`); `Upsert` returns the created/updated flag → `WasCreated`.
- LiteDB is single-in-flight-transaction; feed row goes into the same `LiteDbXTransactionWrapper` when a tx is present.

### 6.3 Stellar

- No write-behavior dispatch today; same wiring pass as LiteDbX into `StellarRepository.Repository.cs`.
- `buildResult()`: FastDB ops (`AddAsync`, `UpdateAsync`, `RemoveBulkAsync`, `AddBulkAsync`) return `Task`, not counts.
  `AffectedCount` = `Items.Count` for item-based ops, `Ids.Count` for filter deletes; `MatchedIds` requires the same
  materialize-then-delete pattern `Delete` already uses.
- `CreateTransaction` throws `NotImplementedException` → no tx path. The change-feed behavior writes feed rows as a
  second write. **Sequence assignment uses the shared monotonic counter (§7.3), not max+1** — this removes the
  concurrent max-read race that caused duplicates, at the cost of possible sequence gaps (a gap = the desync window we
  bound here). The feed row write is issued immediately after the data write in the same async method (no `await` gap
  between them beyond the normal continuation).

---

## 7. Change Feed Design

### 7.1 Event shape

```csharp
public sealed record DataChangeEvent
{
    public string ChangeId { get; init; }                  // Guid N — dedupe key
    public long Sequence { get; init; }                    // per (source, entityType) monotonic
    public RepositoryWriteOperation Operation { get; init; }
    public Type EntityType { get; init; }
    public string CollectionName { get; init; }
    public IReadOnlyCollection<string> EntityIds { get; init; }
    public IReadOnlyCollection<object?> Items { get; init; } // post-write snapshots, subject to payload mode (§7.2)
    public bool IsPartial { get; init; }                   // Patch/Increment: true — no full entity, id+delta only
    public bool HasFullItems { get; init; }                // false when IncludeItems=IdOnly or IsPartial
    public string Source { get; init; }                    // provider + database name
    public string? TransactionId { get; init; }
    public string? CorrelationId { get; init; }            // forwarded from caller if present
    public DateTimeOffset TimestampUtc { get; init; }
}

public sealed record DataChangeEvent<TItem> : DataChangeEvent
    where TItem : Entity
{
    public IReadOnlyCollection<TItem> TypedItems { get; init; } = Array.Empty<TItem>();
}
```

### 7.2 Feed behavior + sink

```csharp
public enum FeedPayloadMode
{
    Item,     // event carries full post-write entity snapshots (default)
    IdOnly,   // event carries ids only — consumers re-read when needed
}

public sealed class ChangeFeedBehaviorOptions
{
    public FeedPayloadMode PayloadMode { get; init; } = FeedPayloadMode.Item;
    public bool FeedPartialOps { get; init; } = true;      // Patch/Increment emit id-only events with IsPartial=true
}

public sealed class ChangeFeedBehavior : IRepositoryWriteBehavior
{
    public ChangeFeedBehavior(IChangeFeedSink sink, ChangeFeedBehaviorOptions options);
    // implements every After*: maps context+result → DataChangeEvent → sink.AppendAsync(event, tx)
}

public interface IChangeFeedSink
{
    ValueTask AppendAsync(DataChangeEvent change, IDatabaseTransaction? transaction, CancellationToken ct);
    ValueTask<IReadOnlyList<DataChangeEvent>> ReadAsync(string source, long afterSequence, int take, CancellationToken ct);
}
```

- `PayloadMode = IdOnly`: event still sets `HasFullItems = false`; snapshots are skipped (cheap for large bulk writes).
  Default is `Item` per §13.7.
- `Patch` / `Increment` never materialize an entity, so they always emit id-only events with `IsPartial = true` and
  `HasFullItems = false` regardless of `PayloadMode` (§13.8).

### 7.3 Default outbox sink + sequence counter

`OutboxChangeFeedSink` stores events in the same database as the source:

- **Mongo**: `__change_feed` collection, indexed `(Source, Sequence)` unique, `ChangeId` unique. Written in the caller
  session when a tx is present.
- **LiteDbX**: `__change_feed` collection in the same LiteDB file/tx.
- **Stellar**: `__change_feed` FastDB collection.

**Sequence assignment (all providers): a dedicated monotonic counter.** Each `(Source, EntityType)` has a counter row
(`__change_feed_counters`); `AppendAsync` does read-increment-write on it and stamps the event with the resulting
value. This removes the concurrent max-read race (§13.4) — Mongo/LiteDbX run the counter update inside the caller's tx
for gapless sequence; Stellar has no tx, so gaps are still possible (gap = the desync window bounded in §6.3).

Dedicated outbox table rather than an event-bus push in v1: the table survives process restarts, is trivially
replayable, and does not require broker infrastructure. A `QueueChangeFeedSink` (Azure Service Bus / RabbitMQ) can be
added later behind the same interface.

### 7.4 One sink per app

**Decision: a single app-level `IChangeFeedSink` shared by all repositories** (§13.6). Registration:

```csharp
services.AddMongoChangeFeed(db, "main");   // one sink for the whole app
services.AddMongoRepository<Order>(db, o => o.AddWriteBehavior(ChangeFeedBehavior.From(sink)));
```

`Source` distinguishes which database a change came from, so one sink serving multiple databases still produces
correct per-source ordering. A per-repository sink remains possible (construct the behavior with a dedicated sink)
but is not the default.

### 7.5 Poller & ordering

`ChangeFeedPoller` drains `ReadAsync(afterSequence: lastSeen)` per `(Source, EntityType)`, in `Sequence` order,
delivering via `IChangeFeed<TItem>`:

```csharp
public interface IChangeFeed<TItem> where TItem : Entity
{
    ValueTask SubscribeAsync(Func<DataChangeEvent<TItem>, CancellationToken, ValueTask> handler,
        string? afterSequence = null, CancellationToken ct = default);
    ValueTask<IReadOnlyList<DataChangeEvent<TItem>>> ReadAsync(long afterSequence, int take, CancellationToken ct = default);
}
```

Ordering guarantees:

- Strict per `(Source, EntityType)` sequence order within a drain pass.
- Cross-type ordering is **not** guaranteed (a delete of a parent and a write to a child are independent sequences).
  Consumers that need cross-type causality must use `TransactionId` / `CorrelationId`.
- Idempotent delivery: handlers dedupe on `ChangeId` (outbox rows are never deleted on read, only marked/watermarked),
  giving at-least-once with safe replay.

### 7.6 Replay / backfill

Not part of the live feed. `OutboxChangeFeedSink.ReadAsync` supports bounded re-reads for repairing a lagging consumer.
Full backfill of historical data is a query-based sweep over the source collections (out of scope for v1).

---

## 8. Consumer API Surface (v1)

- `ChangeFeedBehavior` — register in `RepositoryOptions.WriteBehaviors`.
- `IChangeFeedSink` + `OutboxChangeFeedSink` — durable default.
- `ChangeFeedPoller` + `IChangeFeed<TItem>` — subscribe / read.
- `RepositoryOptions.WriteBehaviors` — unchanged shape; the feed behavior is just another entry.
- No changes to `IRepository` or `IRepositoryReadOnly` method signatures.

Example registration (Mongo) — one app-level sink shared by all repositories (§7.4):

```csharp
services.AddMongoChangeFeed(databaseName);                       // singleton OutboxChangeFeedSink + poller
services.AddMongoRepository<Order>(db, o => o.AddWriteBehavior(
    new ChangeFeedBehavior(sp.GetRequiredService<IChangeFeedSink>(), new ChangeFeedBehaviorOptions())));
```

---

## 9. Testing Strategy

Add `Saturn.Data.Testing.Shared/ChangeFeed/ChangeFeedContractTests.cs`. All three provider test projects inherit.

| #  | Case                                           | Asserts                                                                                                     |
|----|------------------------------------------------|-------------------------------------------------------------------------------------------------------------|
| 1  | Single `Insert` fires `AfterInsert`            | Exactly one `AfterInsert`; `result.EntityIds[0]` equals the persisted id; `Outcome == Inserted`             |
| 2  | Bulk `Insert` fires once, not per-row          | One event with 10 `EntityIds`; `AffectedCount == 10`                                                        |
| 3  | `Update` that throws `FailedToUpdateException` | `AfterUpdate` **not** fired; exception propagates                                                           |
| 4  | `Upsert` creates vs replaces                   | `WasCreated == true` on first, `false` on second                                                            |
| 5  | Filter-based `Delete` (soft)                   | `AfterDelete` fires with `MatchedIds` populated; feed event `Operation == Delete`                           |
| 6  | `HardDelete`                                   | `AfterHardDelete` fires; row gone from store                                                                |
| 7  | `Restore`                                      | `AfterRestore` fires; item readable again                                                                   |
| 8  | `Patch` / `Increment`                          | Correct operation mapped; version bumped before `After*` reads it                                           |
| 9  | Transaction atomicity (Mongo/LiteDbX)          | Feed row written inside tx; aborting tx removes feed row too                                                |
| 10 | Feed error isolation                           | Throwing `After*` hook does not fail the repository method                                                  |
| 11 | Outbox ordering                                | Drain returns events strictly ascending per `(Source, EntityType)`                                          |
| 12 | Replay idempotency                             | Same `ChangeId` delivered twice → handler dedupes                                                           |
| 13 | `Suppress=true` (cascade) does **not** feed    | Cascade-internal delete produces **no** feed event (§5.5)                                                   |
| 14 | Stellar crash-window bound                     | Feed row appended immediately after data write; sequence gap allowed but no loss of already-appended events |

Test entities reuse existing `Saturn.Data.Testing.Shared` fixtures plus a `__change_feed` clean-up per test.

---

## 10. Migration & Breaking Changes

**Non-breaking additions:**

- `IRepositoryWriteBehavior` gains `After*` members — all default no-ops. Existing implementors (`CascadeWriteBehavior`)
  compile and run unchanged.
- `RepositoryWriteResult` — new type.
- `ChangeFeed*`, `DataChangeEvent*`, `IChangeFeed*` — new types.

**Behavioral changes (release notes):**

- LiteDbX and Stellar begin dispatching write behaviors. Any `IRepositoryWriteBehavior` registered against those
  providers that previously never fired will now fire `Before*` (and `After*`). Desirable, but call it out.
- Mongo: `ApplyWriteBehaviors` becomes the shared dispatcher; ordering is preserved (`Before*` before write, then
  `After*`).

**No removals, no renames.** `Suppress` semantics unchanged for `Before*`.

---

## 11. Phased Rollout

| Phase                   | Output                                                                                         | Test gate                                         |
|-------------------------|------------------------------------------------------------------------------------------------|---------------------------------------------------|
| 0. Chokepoint           | `BehaviorDispatcher` in abstractions; wire LiteDbX + Stellar write paths to dispatch `Before*` | Existing provider suites pass (before-gap closed) |
| 1. After* contract      | `After*` members + `RepositoryWriteResult`; Mongo dispatches after-write                       | Mongo tests 1–8                                   |
| 2. Provider after-hooks | LiteDbX + Stellar `buildResult()` and `After*` dispatch                                        | LiteDbX + Stellar tests 1–8                       |
| 3. Feed core            | `DataChangeEvent`, `IChangeFeedSink`, `OutboxChangeFeedSink`, `ChangeFeedBehavior`             | Tests 9–12                                        |
| 4. Delivery             | `ChangeFeedPoller`, `IChangeFeed<TItem>`                                                       | Tests 11–13                                       |
| 5. Docs                 | This file + `docs/data-change-feed.md` usage guide                                             | —                                                 |

Each phase lands on `master` behind the existing `publish-changed-nugets.yml` workflow with a `minor` bump for Phases
3–4, `patch` for Phases 0–2.

### Phase 0 — Chokepoint

- [ ] `BehaviorDispatcher` (abstractions): `RunWriteBehaviorsAsync<TItem>` lifting Mongo's `ApplyWriteBehaviors`;
  `DispatchBeforeAsync`/`DispatchAfterAsync` helpers
- [ ] Mongo: swap `ApplyWriteBehaviors` → dispatcher, keep `Before*` ordering identical
- [ ] LiteDbX: call `RunWriteBehaviorsAsync` on every write op in `LiteDbRepository.Repository.cs`
- [ ] Stellar: same in `StellarRepository.Repository.cs`
- [ ] Run full existing provider suites → green (before-gap closed, no regressions)

### Phase 1 — After\* contract

- [x] Add 9 `After*` default no-op members to `IRepositoryWriteBehavior`
- [x] Add `RepositoryWriteResult` + `WriteOutcome` in abstractions
- [x] Mongo `BuildWriteResult()` from `BulkWriteResult`/`ReplaceOneResult`/`UpdateResult`/`DeleteResult` (incl.
  `PartialFailure`/`FailedCount`/`FailedIds`)
- [x] Mongo dispatches `After*` in-tx / immediately (§5.3)
- [x] `After*` swallow+log isolation (§5.4); `Suppress` gates `After*` (§5.5)
- [x] `ChangeFeedContractTests` Mongo tests 1–8 green

### Phase 2 — Provider after-hooks

- [x] LiteDbX `buildResult()` (`bool Update`, upsert flag → `WasCreated`)
- [x] Stellar `buildResult()` (derived counts; `RawResult = null`; no tx)
- [x] LiteDbX + Stellar dispatch `After*` + `Suppress` gating
- [x] LiteDbX + Stellar tests 1–8 green

### Phase 3 — Feed core

- [x] `DataChangeEvent` + `DataChangeEvent<TItem>` (incl. `IsPartial`, `HasFullItems`)
- [x] `IChangeFeedSink` (`AppendAsync`, `ReadAsync`)
- [x] `ChangeFeedBehavior` implementing all `After*` → sink, honoring `PayloadMode` (§7.2)
- [x] `OutboxChangeFeedSink` base + provider subclasses (Mongo/LiteDbX/Stellar) with `__change_feed` collections
- [x] `__change_feed_counters` monotonic sequence counter (§7.3)
- [x] Tests 9–12 green (tx atomicity, isolation, ordering, dedupe)

### Phase 4 — Delivery

- [x] `ChangeFeedPoller` (per-`(Source, EntityType)` drain, `ChangeId` watermark)
- [x] `IChangeFeed<TItem>` subscribe/read
- [x] One-sink-per-app registration extensions (`AddMongoChangeFeed` etc., §7.4)
- [x] Tests 11–13 green

### Phase 5 — Docs

- [ ] Phase tracker in §0 all ticked
- [ ] `docs/data-change-feed.md` usage guide (register, subscribe, payload modes, partial events, cascade suppression)
- [ ] Release notes: LiteDbX/Stellar now dispatch behaviors; partial-failure semantics

---

## 12. Files to Modify / Add

### Modify — abstractions

- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/IRepositoryWriteBehavior.cs` — add `After*` members
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/RepositoryOptions.cs` — no change required (behavior list
  already exists)

### Add — abstractions

- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/BehaviorDispatcher.cs` — shared `RunWriteBehaviorsAsync` (
  lifted from Mongo)
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/RepositoryWriteResult.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/WriteOutcome.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/ChangeFeed/DataChangeEvent.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/ChangeFeed/IChangeFeedSink.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/ChangeFeed/IChangeFeed.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/ChangeFeed/ChangeFeedBehavior.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/ChangeFeed/ChangeFeedBehaviorOptions.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/ChangeFeed/ChangeFeedPoller.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/ChangeFeed/OutboxChangeFeedSink.cs` (shared contract;
  provider subclasses own the storage)

### Modify — providers

- `Saturn.Data.MongoDb/Saturn.Data.MongoDb/MongoDbRepository.cs` — replace `ApplyWriteBehaviors` with dispatcher; add
  `BuildWriteResult`
- `Saturn.Data.MongoDb/Saturn.Data.MongoDb/MongoDbRepository.Repository.cs` — wrap each write in
  `RunWriteBehaviorsAsync`
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX/LiteDbRepository.Repository.cs` — same
- `Saturn.Data.Stellar/Saturn.Data.Stellar/StellarRepository.Repository.cs` — same

### Add — provider outbox sinks

- `Saturn.Data.MongoDb/Saturn.Data.MongoDb/ChangeFeed/MongoOutboxChangeFeedSink.cs`
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX/ChangeFeed/LiteDbOutboxChangeFeedSink.cs`
- `Saturn.Data.Stellar/Saturn.Data.Stellar/ChangeFeed/StellarOutboxChangeFeedSink.cs`

### Add — tests

- `Saturn.Data.Testing.Shared/ChangeFeed/ChangeFeedContractTests.cs`
- Provider test projects add `ChangeFeedTestFixture.cs` each

---

## 13. Risks & Decisions

All eight design questions resolved on 2026-08-03:

1. **`After*` throwing inside a transaction → swallow.** Exceptions from `After*` hooks are caught, logged, and
   swallowed universally, including in-tx — the write is **not** rolled back. A notification failure is not our scope to
   propagate; the feed catches up via replay (§5.4). Hooks that need retry/dead-letter implement it internally.

2. **Stellar has no transaction and no write-result counts — accepted.** `AffectedCount`/`MatchedIds` for Stellar are
   derived, not authoritative. FastDB `AddAsync`/`UpdateAsync` do not report matched counts. Document that `RawResult`
   is `null` on Stellar.

3. **Partial bulk failures → honest counts.** Mongo bulk writes use `IsOrdered=false` (Save path) — a failed row does
   not abort siblings. `buildResult()` surfaces per-row success via `BulkWriteResult`: report **7 succeeded, 3 failed**
   rather than a blanket result. `After*` fires whenever ≥ 1 row succeeded; the event carries `AffectedCount`,
   `FailedCount`, and `FailedIds` (§6.1).

4. **Sequence generation → dedicated monotonic counter.** A `__change_feed_counters` collection holds a per
   `(Source, EntityType)` counter; `AppendAsync` does read-increment-write and stamps the event. Mongo/LiteDbX run the
   counter update inside the caller's tx (gapless); Stellar has no tx, so gaps remain possible — acceptable, ordering
   within a single process is what matters (§7.3).

5. **`Suppress` gates the feed.** Cascade-internal writes (`Suppress=true`) do **not** emit feed events. Feed consumers
   see application-level writes only (§5.5). A `SuppressFeed`-style option to opt cascade traffic back in is deferred.

6. **DI shape → one sink per app.** A single app-level `IChangeFeedSink` shared by all repositories, registered via a
   per-provider extension (`AddMongoChangeFeed(db, name)`); `Source` keeps per-database ordering correct. A
   per-repository sink remains possible but is not the default (§7.4).

7. **`Items` snapshot cost → `PayloadMode` option.** `ChangeFeedBehaviorOptions.PayloadMode` ∈ {`Item` (default),
   `IdOnly`}. Id-only is the lean option for high-volume bulk feeds; events always set `HasFullItems` accordingly
   (§7.2).

8. **`Patch`/`Increment` have no entity instance → id-only partial events.** These ops always emit events with
   `EntityIds` + `IsPartial = true`, `HasFullItems = false`, carrying `JsonDocument`/`IncrementDelta` from `context`.
   Feed consumers must handle id-only events (§7.2).

---

## 14. Follow-ups (deferred)

- **`OnWriteFailed` hook** — a companion to the `After*` surface for failed writes, with the provider exception. Not in
  v1; failures surface via existing exceptions today.
- **Feed cascade traffic option** — opt-in flag to emit feed events for `Suppress=true` (cascade-internal) writes, in
  case a consumer genuinely needs them (§5.5).
- **`QueueChangeFeedSink`** — Azure Service Bus / RabbitMQ sink behind `IChangeFeedSink`, for consumers that want push
  rather than poller-based drain (§7.3).
- **Diff/audit capture** — a feed consumer computing per-field diffs for full audit logging; the feed records "what
  changed", not the diff (§1).
- **Full backfill of historical data** — query-based sweep over source collections, out of scope for v1 (§7.6).
