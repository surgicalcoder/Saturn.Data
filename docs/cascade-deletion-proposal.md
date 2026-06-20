# Cascade Deletion — Proposal

Target repo: `D:\Work\Saturn.Data`
Status: design proposal, not approved.

---

## 1. Goals & Non-Goals

### Goals
- One call deletes a parent and its declared children atomically per provider.
- Discoverable: the source generator inspects entities and emits a relation table at build time. No reflection, no manual wiring, no `Scan → Wire` step at startup.
- Per-relation policy: each child link declares its own mode (`Archive | SoftDelete | HardDelete | None`) and depth (`Single | Transitive`).
- Two trigger surfaces: a default-on `CascadeWriteBehavior` (fires inside `BeforeDelete` / `BeforeHardDelete`) **and** explicit `DeleteCascade<T>` / `HardDeleteCascade<T>` methods on `IRepository`.
- Provider-correct semantics: Mongo (session + bulk), LiteDbX (in-proc transaction + per-item or batched), Stellar (no tx → best-effort + compensation log).
- Reversible for the soft/archive modes: existing `Restore` keeps working; archived children get a new `Restore` path.
- Cycle-safe: a `User → Account → User` graph does not loop forever.

### Non-Goals
- Cross-aggregate transactional consistency outside the single parent deletion (no two-phase, no saga).
- Eventing/callbacks on cascade (no `OnParentDeleted` notifications).
- A runtime DSL or builder — the generator is the single source of truth.
- Fixing every provider inconsistency (write-behaviors on LiteDbX/Stellar) outside the cascade path. Phase 3 touches the minimum needed.

---

## 2. High-Level Architecture

Three layers, top down:

1. **Metadata layer** — `CascadeDeleteAttribute` on properties + a Roslyn generator that emits a static `CascadeRelations.For<T>()` table per entity type.
2. **Engine layer** — `CascadeEngine` is provider-agnostic. It takes a parent id, walks the static table, builds a `CascadePlan` (ordered batches per child type), and hands each batch to the right `ICascadeExecutor`.
3. **Provider layer** — `MongoCascadeExecutor`, `LiteDbCascadeExecutor`, `StellarCascadeExecutor` own the actual bulk operations and transaction scope.

The engine and executors live in `GoLive.Saturn.Data.Abstractions` (engine + interfaces) and `GoLive.Saturn.Data.{MongoDb,LiteDbX,Stellar}` (executors).

---

## 3. Source Generator Design

**Project:** extend the existing `Saturn.Generator.Entities` (no new analyzer project). Add a partial to `SaturnGenerator` plus a new `CascadeScanner` + `CascadeEmitter`.

### 3.1 Scan rules

The cascade scanner walks every class that:

- Inherits `Entity` (directly or transitively), and
- Is declared `partial`.

For each instance property on that class that carries `[CascadeDelete]`, the scanner categorises it by shape and direction:

| Property shape | Relation kind | Where attribute lives | Direction |
|---|---|---|---|
| `Scope` (override of `ScopedEntity<T>.Scope`) | `ChildOf` | Child class | child → parent; cascade fires on parent delete |
| `Scope` on a hand-written `ScopedEntity<T>` (no override) | `ChildOf` | Generator emits the override partial if missing | same |
| `WeakRef` / `WeakRef<T>` (override of `WeakScopedEntity.Scope`) | `ChildOf` | Child class | same |
| `SecondScope` (override of `SecondScopedEntity<,>.SecondScope`) | `ChildOf` (secondary) | Child class | same |
| `Ref<TParent>` (hand-written, points to a parent) | `ChildOf` | Child class, on the property | same |
| `List<Ref<TChild>>` / `IList<Ref<TChild>>` (points to children) | `HasMany` | Parent class, on the property | parent → child; cascade fires on parent delete |
| `[CascadeDelete(ChildType = typeof(TChild))]` on a hand-written non-`Ref` property (e.g., a `string` foreign key) | `HasMany` | Parent class, on the property; child type explicit | same |

**Override emission.** When a `ScopedEntity<T>` (or any of the `Weak*` / `SecondScoped*` variants) carries `[CascadeDelete]` on its `Scope`/`SecondScope` property and the user has not declared an override, the generator emits a partial-property block in a generated partial class:

```csharp
public partial class Account
{
    [CascadeDelete(CascadeMode.Archive, CascadeDepth.Transitive)]
    public override Ref<User> Scope { get => base.Scope; set => base.Scope = value; }
}
```

The user does not duplicate the property. The override is auto-generated. The scan still finds the attribute via the base class's symbol.

**Child type resolution.** For `ChildOf` relations (the override path), the parent type is the generic argument of `ScopedEntity<T>` / `SecondScopedEntity<TSecond, TPrimary>` / `WeakScopedEntity<T>`. The relation lands in `__Cascade.For<TParent>()` automatically.

Properties without `[CascadeDelete]` are **not** relations. The generator only emits a relation entry when the attribute is present.

### 3.2 Emitted output

For `class User : Entity` with one `[CascadeDelete]` property `Accounts` of type `List<Ref<Account>>`, the generator emits a partial extension:

```csharp
public partial class User
{
    public static class __Cascade
    {
        public static global::GoLive.Saturn.Data.Abstractions.CascadeRelationTable For =>
            global::GoLive.Saturn.Data.Abstractions.CascadeRelationTable.Build(typeof(User))
                .Child<Account>(
                    filter: e => e.Scope.Id == parentId,
                    mode: global::GoLive.Saturn.Data.Abstractions.CascadeMode.SoftDelete,
                    depth: global::GoLive.Saturn.Data.Abstractions.CascadeDepth.Single)
                .Build();
    }
}
```

The static `For` is memoized at first access. `CascadeRelationTable` is a sealed builder returned by `Build(typeof(T))` and stored in a `ConcurrentDictionary<Type, CascadeRelationTable>`.

**Standalone project.** The cascade generator is a separate `IIncrementalGenerator` in its own project, packaged as its own NuGet. Cascade is **completely optional** — projects that do not reference the generator get no relation tables and the runtime engine falls back to "no relations registered, nothing to cascade." This avoids polluting the existing `Saturn.Generator.Entities` and keeps the cascade feature independently versioned.

**New solution structure:**

```
src/
  Saturn.Generator.Cascade/                          <- new generator project
    Saturn.Generator.Cascade.csproj                   (IsPackable=true, OutputType=Analyzer)
    CascadeGenerator.cs                              (IIncrementalGenerator entry)
    CascadeScanner.cs
    CascadeEmitter.cs
  Saturn.Data.Abstractions/
    ...                                              (existing; gains Cascade namespace)
  Saturn.Data.Entities/
    ...                                              (existing; gains Cascade attribute folder)
  Saturn.Generator.Entities/
    ...                                              (existing; UNCHANGED by this work)
```

**Files added:**
- `src/Saturn.Generator.Cascade/Saturn.Generator.Cascade.csproj`
- `src/Saturn.Generator.Cascade/CascadeGenerator.cs`
- `src/Saturn.Generator.Cascade/CascadeScanner.cs`
- `src/Saturn.Generator.Cascade/CascadeEmitter.cs`

**NuGet package:** `GoLive.Saturn.Data.Generator.Cascade` (versioned independently of the runtime packages). Projects that want cascading add:

```xml
<PackageReference Include="GoLive.Saturn.Data.Generator.Cascade" Version="*" />
```

Projects that do not add it still get the runtime types (`CascadeDeleteAttribute`, `CascadeEngine`, etc.) but the `__Cascade.For` partial is never emitted. The runtime engine treats a missing `__Cascade` as an empty relation table — no exception, just no cascade.

### 3.3 Roslyn attributes the generator reads

The generator only consumes attributes from the same `GoLive.Saturn.Data.Entities` namespace it already loads. No new dependencies on Roslyn workspaces or external analyzer packages.

---

## 4. Attribute Design

All attributes live in `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/Cascade/`.

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class CascadeDeleteAttribute : Attribute
{
    public CascadeDeleteAttribute(
        CascadeMode mode = CascadeMode.Default,
        CascadeDepth depth = CascadeDepth.Single) { Mode = mode; Depth = depth; }

    public CascadeMode Mode { get; }
    public CascadeDepth Depth { get; }
    public Type ChildType { get; set; } = null!;   // optional explicit child type for Ref-less fields
}

public enum CascadeMode
{
    Default = 0,    // IArchivable → Archive, ISoftDeletable → SoftDelete, else HardDelete
    Archive = 1,    // set IsArchived, ArchivedAt, ArchivedBy
    SoftDelete = 2, // set IsDeleted, DeletedAt, DeletedBy
    HardDelete = 3, // physical remove
    None = 4,       // no-op (caller manages this child)
}

public enum CascadeDepth
{
    Single = 0,     // one hop only
    Transitive = 1, // BFS until depth budget exhausted or cycle detected
}
```

Shared-scope policy on `[CascadeDelete]`:

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class CascadeDeleteAttribute : Attribute
{
    public CascadeDeleteAttribute(
        CascadeMode mode = CascadeMode.Default,
        CascadeDepth depth = CascadeDepth.Single,
        SharedScopePolicy sharedScope = SharedScopePolicy.Allow) { ... }

    public CascadeMode Mode { get; }
    public CascadeDepth Depth { get; }
    public SharedScopePolicy SharedScope { get; }
    public Type ChildType { get; set; } = null!;
}

public enum SharedScopePolicy
{
    Allow  = 0,  // delete the shared child; record a warning
    Refuse = 1,  // throw CascadeException if any child is shared with another live parent
    Skip   = 2,  // leave shared children untouched; record them in SkippedSharedChildren
}
```

**Files added:**
- `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/Cascade/CascadeDeleteAttribute.cs`
- `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/Cascade/CascadeMode.cs`
- `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/Cascade/CascadeDepth.cs`
- `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/Cascade/SharedScopePolicy.cs`

---

## 5. Runtime Cascade Engine

### 5.1 Types (all in `GoLive.Saturn.Data.Abstractions`)

```csharp
public sealed class CascadeRelationTable
{
    public static CascadeRelationTable Build(Type parentType) { /* ... */ }
    public CascadeRelationTableBuilder<T> Child<T>(...) { /* ... */ }
    public IReadOnlyList<CascadeRelation> Relations { get; }
}

public sealed record CascadeRelation(
    Type ChildType,
    LambdaExpression Filter,
    CascadeMode Mode,
    CascadeDepth Depth,
    SharedScopePolicy SharedScope);

public interface ICascadeExecutor
{
    Task ExecuteAsync(CascadePlanStep step, IDatabaseTransaction? tx, CancellationToken ct);
}

public sealed class CascadePlanStep
{
    public Type ChildType { get; init; }
    public string ParentId { get; init; }
    public IReadOnlyList<string> ChildIds { get; init; }   // materialized ids
    public CascadeMode Mode { get; init; }
    public SharedScopePolicy SharedScope { get; init; }
}
```

### 5.2 `CascadeEngine` flow

```
public sealed class CascadeEngine
{
    public CascadeEngine(IEnumerable<ICascadeExecutor> executors, CascadeMode defaultMode);

    public async Task<CascadeReport> DeleteAsync<TParent>(
        TParent parent,
        CascadeMode mode,
        CascadeDepth depth,
        IDatabaseTransaction? tx,
        CancellationToken ct);
}
```

Algorithm:

1. Read `CascadeRelationTable.For<TParent>()` (or `.For` via the `__Cascade` static).
2. Build an initial `Queue<CascadeWorkItem>` of `(childType, parentId, mode, depth, sharedScope)`.
3. For each item, dispatch to `ICascadeExecutor` keyed by child type's collection namespace.
4. Executor materializes the child ids, runs the bulk op, returns the list of deleted/archived ids.
5. **Shared-scope filter (only for `MultiscopedEntity<>` children):** for each candidate child, query the child's `Scopes` list. If any other id in `Scopes` references a still-live entity:
   - `Refuse` → abort cascade, throw `CascadeException` with `AffectedChildren`.
   - `Skip` → remove from the delete set, append to `SkippedSharedChildren`.
   - `Allow` → keep in the delete set, append to `SharedScopeDeletions` warning.
6. If `depth == Transitive` and the executor found children, enqueue each child id as a new parent for its own declared children (read from its `CascadeRelationTable.For`).
7. Track `visited: HashSet<(Type, string)>` to prevent cycles.
8. Return `CascadeReport` with counts per type, the visited set, `SharedScopeDeletions`, `SkippedSharedChildren`, and `SkippedCycles`.

```csharp
public sealed class CascadeReport
{
    public IReadOnlyDictionary<Type, int> DeletedPerType { get; init; }
    public IReadOnlyDictionary<Type, int> ArchivedPerType { get; init; }
    public IReadOnlyList<(Type ChildType, string ChildId, IReadOnlyList<string> OtherParentIds)> SharedScopeDeletions { get; init; }
    public IReadOnlyList<(Type ChildType, string ChildId, IReadOnlyList<string> OtherParentIds)> SkippedSharedChildren { get; init; }
    public IReadOnlyList<(Type ParentType, string ParentId)> SkippedCycles { get; init; }
    public IReadOnlyList<string> Warnings { get; init; }
    public bool Aborted { get; init; }
}
```

**Files added (abstractions):**
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeRelationTable.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeRelation.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadePlanStep.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeReport.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/ICascadeExecutor.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeEngine.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeException.cs`

### 5.3 Engine composition

`CascadeEngine` takes `IEnumerable<ICascadeExecutor>`. Each provider registers its own executor via DI. The engine picks by `step.ChildType`'s assembly or by an explicit `ICascadeExecutor.Supports(Type)` predicate.

---

## 6. CascadeWriteBehavior (Mongo-first hook)

### 6.1 Why behavior-first

The behavior hook is the path of least resistance because:

- Mongo already invokes `BeforeDelete` / `BeforeHardDelete` correctly.
- The behavior is registered once in `RepositoryOptions.WriteBehaviors`.
- It can be turned off per-call by setting a new `RepositoryWriteContext.Suppress` flag (added in §6.3).

### 6.2 LiteDbX + Stellar gap fix

`LiteDbRepository.Repository.cs` and `StellarRepository.Repository.cs` do **not** call `ApplyWriteBehaviors` on Delete. Phase 3 introduces a small chokepoint in each provider:

```csharp
// LiteDbX: extract a helper
private async Task RunWriteBehaviorsAsync<TItem>(RepositoryWriteOperation op, RepositoryWriteContext<TItem> ctx, CancellationToken ct) where TItem : Entity
{
    foreach (var b in options.WriteBehaviors) await DispatchAsync(b, op, ctx);
}
```

The Mongo `ApplyWriteBehaviors` is lifted into the abstractions as a `protected static` helper so all three providers share it. The fix is two lines per Delete overload in LiteDbX and Stellar. This also benefits the future Restore / Patch paths.

**Files modified:**
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX/LiteDbRepository.Repository.cs` (wrap Delete + HardDelete + Restore)
- `Saturn.Data.Stellar/Saturn.Data.Stellar/StellarRepository.Repository.cs` (same)

### 6.3 `RepositoryWriteContext.Suppress` flag

Add a single `bool Suppress { get; init; }` property to `RepositoryWriteContext<TItem>`. When the cascade engine sets it, the cascade behavior is a no-op (prevents recursion when explicit methods call into the engine which then needs the engine's own deletes to be raw, not double-cascaded).

**Files modified:**
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/RepositoryWriteContext.cs`

### 6.4 Behavior class

`GoLive.Saturn.Data.Abstractions/Cascade/CascadeWriteBehavior.cs`:

```csharp
public sealed class CascadeWriteBehavior : IRepositoryWriteBehavior
{
    private readonly CascadeEngine engine;
    public CascadeWriteBehavior(CascadeEngine engine) { this.engine = engine; }

    public async ValueTask BeforeDelete<TItem>(RepositoryWriteContext<TItem> ctx) where TItem : Entity
    {
        if (ctx.Suppress) return;
        var ids = ResolveIds(ctx);
        foreach (var id in ids) await engine.DeleteAsync(parent: id, parentType: typeof(TItem), tx: ctx.Transaction, ct: ctx.CancellationToken);
    }

    public async ValueTask BeforeHardDelete<TItem>(RepositoryWriteContext<TItem> ctx) where TItem : Entity
    {
        if (ctx.Suppress) return;
        // identical to BeforeDelete but uses HardDelete mode
    }
}
```

---

## 7. Explicit API

`IRepository` gains two new methods (default no-op impl, providers override):

```csharp
Task<CascadeReport> DeleteCascade<TItem>(
    string id,
    CascadeMode mode = CascadeMode.Default,
    CascadeDepth depth = CascadeDepth.Single,
    IDatabaseTransaction? tx = null,
    CancellationToken ct = default) where TItem : Entity;

Task<CascadeReport> HardDeleteCascade<TItem>(
    string id,
    CascadeMode mode = CascadeMode.Default,
    CascadeDepth depth = CascadeDepth.Single,
    IDatabaseTransaction? tx = null,
    CancellationToken ct = default) where TItem : Entity;
```

`IRepository` provides the default: open transaction, run `HardDelete<TItem>(id, tx, ct)` (or `Delete<TItem>(id, tx, ct)`) with `RepositoryWriteContext.Suppress = true`, then invoke `CascadeEngine` directly, then commit. Providers can override to use a session-backed transaction.

**Files modified:**
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/IRepository.cs`

---

## 8. Provider-by-Provider Strategy

### 8.1 MongoDb (`MongoCascadeExecutor`)

**Collection namespace:** matches `GetCollectionName<TChild>()` from `MongoDbRepository`.

**SoftDelete:** `IMongoCollection<TChild>.UpdateManyAsync(session, filter, Builders<TChild>.Update.Set("IsDeleted", true).Set("DeletedAt", now).Set("DeletedBy", parentId).Inc("_v", 1))`.

**HardDelete:** `DeleteManyAsync(session, filter)`.

**Archive:** same as SoftDelete but on the `IArchivable` properties (`IsArchived`, `ArchivedAt`, `ArchivedBy`).

**Id materialization:** `collection.Find(session, filter).Project(r => r.Id).ToListAsync(ct)`. The engine then re-uses these ids for transitive children.

**Transaction:** uses the `IClientSessionHandle` from the passed `IDatabaseTransaction`. If no transaction is passed, the executor creates a session internally, runs the plan, commits, and disposes. Single transaction per cascade.

**Files added:**
- `Saturn.Data.MongoDb/Saturn.Data.MongoDb/Cascade/MongoCascadeExecutor.cs`
- `Saturn.Data.MongoDb/Saturn.Data.MongoDb/Cascade/MongoCascadeRegistration.cs` (DI extension)

### 8.2 LiteDbX (`LiteDbCascadeExecutor`)

**SoftDelete:** for each id in the materialized list, `collection.Update(id, mutate)` (mutate sets `IsDeleted`, `DeletedAt`, `DeletedBy`, bumps `_v`). Per-item matches existing `LiteDbRepository.Repository.cs` `Delete` pattern.

**HardDelete:** `collection.DeleteMany(filter)`.

**Archive:** per-item `Update` on the `IArchivable` fields.

**Transaction:** `LiteDbXTransactionWrapper` is already returned by `CreateTransaction`. The executor awaits `tx.Start()`, runs the plan, awaits `tx.CommitAsync()`. LiteDB's `BeginTransaction` is process-local so cross-collection atomicity holds.

**Id materialization:** `collection.Query().Where(BsonMapper.Global.GetExpression(filter)).Select(r => r.Id).ToList()`.

**Files added:**
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX/Cascade/LiteDbCascadeExecutor.cs`
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX/Cascade/LiteDbCascadeRegistration.cs`

### 8.3 Stellar (`StellarCascadeExecutor`)

**No transactions.** Stellar throws `NotImplementedException` from `CreateTransaction`. The executor sequences deletes with a per-step try/catch and a compensation log.

**SoftDelete / Archive:** mirror the per-item `UpdateAsync` pattern in `StellarRepository.Repository.cs` `SoftDelete`. The executor wraps the work in a per-type try block.

**HardDelete:** `RemoveBulkAsync(materializedEntityIds)`.

**Id materialization:** `collection.AsQueryable().Where(filter).Select(r => new EntityId(r.Id)).ToList()`.

**Compensation log:** for each completed step, append `(childType, id, mode)` to a `List<CascadeCompensationEntry>`. On failure, the executor iterates the log in reverse and replays a compensating op (Restore semantics on archive/soft, no-op for hard). The final exception is wrapped in `CascadeException` with the log attached.

**Files added:**
- `Saturn.Data.Stellar/Saturn.Data.Stellar/Cascade/StellarCascadeExecutor.cs`
- `Saturn.Data.Stellar/Saturn.Data.Stellar/Cascade/StellarCascadeRegistration.cs`
- `Saturn.Data.Stellar/Saturn.Data.Stellar/Cascade/CascadeCompensationEntry.cs`

---

## 9. Archive Flow

1. `CascadeMode.Archive` requires the child to be `IArchivable`. If the child does not implement it, the engine **escalates** to `CascadeMode.SoftDelete` if `ISoftDeletable`, else `HardDelete`. Escalation is logged on the `CascadeReport.Warnings`.
2. Archive mutation: `IsArchived = true; ArchivedAt = utcNow; ArchivedBy = parentId; _v = (_v ?? 0) + 1`.
3. Restore for archived items: extend `IRepository.Restore<T>` to flip both `IsDeleted` and `IsArchived`. No new method — same call.
4. The `IArchivable` marker is now consumed for the first time. Its fields map to BSON/MessagePack the same way as `ISoftDeletable` (provider serializers already handle them as ordinary bool/DateTime/string).

---

## 10. Cycle Detection

The engine maintains a `HashSet<(Type ParentType, string ParentId)>` per `DeleteAsync` call.

- On entry: if `(parentType, parentId)` is already in the set, skip. This is a hard stop, not a "skip this child type" — the same id being reached by two paths is fine, but two different cascades hitting the same node is the cycle signal.
- The set is cleared per call (per top-level delete).
- For `Transitive` depth: BFS queue, mark visited on enqueue, not on dequeue, to prevent re-enqueueing a node that is still being processed.

This is enough to break `User → Account → Profile → User` loops.

---

## 11. Transactions

| Provider | Strategy | Atomicity |
|---|---|---|
| MongoDb | Single `IClientSessionHandle` per cascade. `tx.Start` → plan → `tx.CommitAsync`. | Yes (cross-collection, cross-doc) |
| LiteDbX | `LiteDbXTransactionWrapper` per cascade (one in-flight transaction per process, which is the LiteDB model). | Yes (single-process, cross-collection) |
| Stellar | None. Compensation log + `CascadeException`. | Best-effort |

`CascadeEngine` is tx-agnostic. The executor receives the `IDatabaseTransaction?` and decides whether to use it, open its own, or run without.

---

## 12. Error Handling

Single exception type:

```csharp
public sealed class CascadeException : Exception
{
    public CascadeReport PartialReport { get; }
    public IReadOnlyList<CascadeCompensationEntry> CompensationLog { get; }
}
```

- Mongo + LiteDb: transaction rolls back. `PartialReport` is empty. No compensation needed.
- Stellar: compensation log is replayed. The exception surfaces the original failure plus the log.
- Cycle stops never throw; they are recorded in `CascadeReport.SkippedCycles`.

---

## 13. Performance

- **Zero reflection at runtime.** All relation metadata is a `LambdaExpression` per child, generated at build time and frozen into the static `__Cascade.For` table.
- **Mongo:** single `BulkWrite` per child type per mode. The plan groups steps by `(ChildType, Mode)` so the executor can issue one `BulkWrite` per group, not one per id.
- **LiteDbX:** `DeleteMany` for hard; per-item `Update` for soft/archive (matches current code; future optimization is out of scope).
- **Stellar:** `RemoveBulkAsync` for hard; per-item `UpdateAsync` for soft/archive.
- **Memory:** the engine streams child id materialization. The `CascadePlanStep` carries materialized ids only when the transitive pass needs them. Single-mode cascades do not pre-materialize the full id set; the executor fetches and writes in one pass.

---

## 14. Testing Strategy

Add `Saturn.Data.Testing.Shared/Cascade/CascadeContractTests.cs`. Base class with 13 cases. All three provider test projects inherit.

| # | Case | Asserts |
|---|---|---|
| 1 | Single-mode SoftDelete: delete parent | Parent soft-deleted, all `[CascadeDelete]` children soft-deleted |
| 2 | Single-mode HardDelete: delete parent | Parent + children physically removed |
| 3 | Single-mode Archive: IArchivable children | Parent hard-deleted, children archived with `ArchivedBy = parentId` |
| 4 | Archive fallback: child not IArchivable | Engine escalates to SoftDelete; warning recorded |
| 5 | Transitive depth 2 | Grandchildren also deleted |
| 6 | Transitive cycle | `SkippedCycles` contains the revisit; no infinite loop |
| 7 | `CascadeMode.None` opt-out | Children untouched |
| 8 | `DeleteCascade<T>(id)` explicit API | Same result as the behavior-triggered path |
| 9 | `HardDeleteCascade<T>(id)` explicit API | All physical |
| 10 | No `[CascadeDelete]` on property | Property's children untouched |
| 11 | Mongo tx rollback on failure (simulated) | Mid-plan failure → nothing persisted |
| 12 | Stellar compensation on failure | Failed step → earlier steps restored; `CascadeException.CompensationLog` populated |
| 13 | Multiscoped child (`SecondScope`) | Deleting primary parent cascades children whose `Scopes` contains the primary id |
| 14 | `SharedScopePolicy.Refuse` on shared child | Throws `CascadeException`; nothing deleted |
| 15 | `SharedScopePolicy.Skip` on shared child | Shared child left alive; reported in `SkippedSharedChildren` |
| 16 | `SharedScopePolicy.Allow` (default) on shared child | Child deleted; recorded in `SharedScopeDeletions` |

Test entities live next to the contract base:

- `Saturn.Data.Testing.Shared/Cascade/Entities/UserCascadeEntity.cs`
- `Saturn.Data.Testing.Shared/Cascade/Entities/AccountCascadeEntity.cs` (ScopedEntity<User>, IArchivable + ISoftDeletable)
- `Saturn.Data.Testing.Shared/Cascade/Entities/PostCascadeEntity.cs` (ScopedEntity<Account>)
- `Saturn.Data.Testing.Shared/Cascade/Entities/TagCascadeEntity.cs` (no cascade marker → should survive)

---

## 15. Migration & Breaking Changes

**New public surface:**
- `GoLive.Saturn.Data.Entities.Cascade.{CascadeDeleteAttribute, CascadeMode, CascadeDepth, SharedScopePolicy}`
- `GoLive.Saturn.Data.Abstractions.Cascade.*` (engine + interfaces)
- `GoLive.Saturn.Data.Generator.Cascade` (separate analyzer NuGet — opt-in via `<PackageReference>`)
- `IRepository.DeleteCascade<T>`, `IRepository.HardDeleteCascade<T>` (default impl in interface, providers override)
- `IRepositoryWriteBehavior` is **not** changed at the member level. The `CascadeWriteBehavior` is a new implementation, not a new contract.
- `RepositoryWriteContext.Suppress` (init-only, default `false`)

**Consumed for the first time:**
- `IArchivable` (`IsArchived`, `ArchivedAt`, `ArchivedBy`)

**Internal-only changes (not breaking):**
- `LiteDbRepository.Repository.cs` Delete paths now invoke `RunWriteBehaviorsAsync` (the chokepoint in §6.2). If anyone wired a custom `IRepositoryWriteBehavior` against the old, behavior-less LiteDbX path, it will start firing. This is desirable but worth a release note.
- `StellarRepository.Repository.cs` same.

**No removals.** No renames. The hard-delete default in `IRepository` (`HardDelete => Delete`) stays as-is. Cascade methods are additions, not replacements.

---

## 16. Phased Rollout

| Phase | Output | Test gate |
|---|---|---|
| 1. Metadata | `CascadeDeleteAttribute`, `CascadeMode`, `CascadeDepth`, `ScopedByAttribute`, `CascadeRelationTable` | Compile clean, no provider changes |
| 2. Generator | `src/Saturn.Generator.Cascade` project: `CascadeGenerator`, `CascadeScanner`, `CascadeEmitter` | Generator emits `__Cascade.For` for sample entities in a new `Saturn.Generator.Cascade.Playground` |
| 3. Provider chokepoints | `RunWriteBehaviorsAsync` helper in abstractions, wired into LiteDbX + Stellar Delete/HardDelete/Restore | Existing provider test suites pass |
| 4. Engine + Mongo executor | `CascadeEngine`, `ICascadeExecutor`, `MongoCascadeExecutor`, `CascadeWriteBehavior` | Mongo contract tests 1–10 pass |
| 5. LiteDbX + Stellar executors | `LiteDbCascadeExecutor`, `StellarCascadeExecutor` + compensation log | All 13 contract tests pass on all three providers |
| 6. Explicit API | `DeleteCascade<T>`, `HardDeleteCascade<T>` on `IRepository` | Tests 8 + 9 pass |
| 7. Archive flow | Wire `IArchivable` consumers in all three executors | Test 3 + 4 pass |
| 8. Docs | This file, plus a `docs/cascade-deletion.md` usage guide (separate, post-implementation) | — |

Each phase lands on `master` behind the existing `publish-changed-nugets.yml` workflow with a `minor` bump for Phase 4 onward, `patch` for Phases 1–3.

---

## 17. Files to Modify / Add

### Add — metadata (Phase 1)
- `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/Cascade/CascadeDeleteAttribute.cs`
- `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/Cascade/CascadeMode.cs`
- `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/Cascade/CascadeDepth.cs`
- `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/Cascade/SharedScopePolicy.cs`

### Add — engine (Phase 4)
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeRelationTable.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeRelation.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadePlanStep.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeReport.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeException.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/ICascadeExecutor.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeEngine.cs`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/Cascade/CascadeWriteBehavior.cs`

### Modify — abstractions (Phases 1, 4, 6)
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/RepositoryWriteContext.cs` — add `Suppress`
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/IRepository.cs` — add `DeleteCascade<T>`, `HardDeleteCascade<T>` default impls
- `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/BehaviorDispatcher.cs` (new) — shared `ApplyWriteBehaviors` lifted from Mongo

### Add — generator (Phase 2)
- `src/Saturn.Generator.Cascade/Saturn.Generator.Cascade.csproj` (new `IIncrementalGenerator` project, `OutputType=Analyzer`, `IsPackable=true`)
- `src/Saturn.Generator.Cascade/CascadeGenerator.cs`
- `src/Saturn.Generator.Cascade/CascadeScanner.cs`
- `src/Saturn.Generator.Cascade/CascadeEmitter.cs`
- `src/Saturn.Generator.Cascade/CascadeEmitter.Templates/` (partial-class + override templates)

`Saturn.Generator.Entities` is **not** modified. Cascade is an independent analyzer, independently versioned as `GoLive.Saturn.Data.Generator.Cascade`.

### Add — provider executors (Phase 5)
- `Saturn.Data.MongoDb/Saturn.Data.MongoDb/Cascade/MongoCascadeExecutor.cs`
- `Saturn.Data.MongoDb/Saturn.Data.MongoDb/Cascade/MongoCascadeRegistration.cs`
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX/Cascade/LiteDbCascadeExecutor.cs`
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX/Cascade/LiteDbCascadeRegistration.cs`
- `Saturn.Data.Stellar/Saturn.Data.Stellar/Cascade/StellarCascadeExecutor.cs`
- `Saturn.Data.Stellar/Saturn.Data.Stellar/Cascade/StellarCascadeRegistration.cs`
- `Saturn.Data.Stellar/Saturn.Data.Stellar/Cascade/CascadeCompensationEntry.cs`

### Modify — provider chokepoints (Phase 3)
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX/LiteDbRepository.Repository.cs`
- `Saturn.Data.Stellar/Saturn.Data.Stellar/StellarRepository.Repository.cs`

### Add — tests (Phase 4–5)
- `Saturn.Data.Testing.Shared/Cascade/CascadeContractTests.cs`
- `Saturn.Data.Testing.Shared/Cascade/Entities/UserCascadeEntity.cs`
- `Saturn.Data.Testing.Shared/Cascade/Entities/AccountCascadeEntity.cs`
- `Saturn.Data.Testing.Shared/Cascade/Entities/PostCascadeEntity.cs`
- `Saturn.Data.Testing.Shared/Cascade/Entities/TagCascadeEntity.cs`
- `Saturn.Data.Testing.Shared/Cascade/ICascadeTestFixture.cs` (interface each provider implements)

### Modify — provider test projects
- `Saturn.Data.MongoDb/Saturn.Data.MongoDb.Tests/` (add `MongoCascadeTestFixture.cs`)
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX.Tests/` (add `LiteDbCascadeTestFixture.cs`)
- `Saturn.Data.Stellar/Saturn.Data.Stellar.Tests/` (add `StellarCascadeTestFixture.cs`)

---

## 18. Risks & Open Questions

1. **Generator's view of `Ref<T>` properties.** The generator sees the declared type. If a user wraps `Ref<T>` in a custom property, the scanner may miss it. Mitigation: generator only reads `[CascadeDelete]`-decorated properties, so a missed type results in a compile-time warning (`SATURN_CASCADE_001`) rather than a silent miss.

2. **`IArchivable` is currently an unused marker.** Adding consumers is a soft breaking change for any downstream code that, by coincidence, already populated those fields. Low risk — nothing in the repo or tests references them.

3. **Stellar compensation log replay** must be idempotent. If a restore fails partway, the engine must surface the failure and stop, not loop. Mitigation: each compensation entry records the pre-failure snapshot, and the engine replays in a single pass.

4. **Transitive depth in MongoDB with a single `IClientSessionHandle`.** Mongo transactions have a 16MB operation cap and a 60-second default lifetime. Very wide cascades (10k+ nodes) may exceed these. Mitigation: the engine groups the plan by child type and commits per group with a new session when the operation count exceeds a configurable threshold (default 1000).

5. **DI shape for `CascadeEngine` + executors.** Three providers need a single `CascadeEngine` that can see all three executors. This requires a shared DI registration point or the engine must be registered per provider. Open question: do we add a `ISaturnDataServices` aggregate or require per-provider registration of the engine? Lean toward per-provider registration with the engine constructed against the union of `ICascadeExecutor` registered in the same scope.

6. **`DeleteCascade` should not race with an in-flight `Delete` on the same id.** The engine is not a lock manager. Callers must serialize. Document this loudly; do not add a provider-level mutex.

7. **Multiscoped children (shared-scope).** Resolved in v1 via per-relation `SharedScopePolicy` (see §4). Before executing a child step, the executor counts how many distinct parent ids reference each child by querying the child's `Scopes` list (denormalized in `MultiscopedEntity<T>.Scopes`):
   - `Allow` (default): child is deleted regardless of other live parents. A `SharedScopeDeletions` entry is added to `CascadeReport.Warnings`.
   - `Refuse`: if any other live parent id appears in `child.Scopes`, throw `CascadeException` with `AffectedChildren` populated. Atomic providers roll back; Stellar replays the compensation log.
   - `Skip`: shared children are excluded from the plan. Reported in `CascadeReport.SkippedSharedChildren`. The cascade continues with unshared children.
   - The "other live parents" check is a cheap second query per candidate child — one `Exists` call per other id in `Scopes`. Acceptable cost given cascade is rare and not hot-path.

8. **The generator scans every partial class inheriting `Entity`.** Existing entities that do not declare `[CascadeDelete]` still get a `__Cascade` class with an empty relation set. Cost is one `ConcurrentDictionary` slot per type. Acceptable.
