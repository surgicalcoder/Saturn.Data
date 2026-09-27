# Generator Change Tracking — Gap Remediation Suggestions

Companion to `../generator-implementation-plan/` (phases A–F + C10). Each section states the gap, the concrete fix, the files affected, tests, and rough effort. Ordered by value.

## Status (implemented except Gap 6)

| Gap | Status | Commit |
| --- | --- | --- |
| 3 — Observer telemetry | done | `87c81a2` |
| 4 — View `PatchableMembers` | done | `87c81a2` |
| 7 — `ISuppressTracking` + population suppression | done | `9db0876` |
| 5 — Shared cross-provider change-set patch contract tests | done | `1761b2a` |
| 2 — Recursive DTO mapping + `IncludeProperties` | done | `e882d9d` |
| 1 — `TrackedSet<T>` / `TrackedDictionary<K,V>` + `Instrument = true` | done | `d00c616` |
| 6 — Generated nullable context | **not implemented (deliberate)** | — |
| 8 — honour `ChangeTrackingMode` | **not implemented (excluded by request)** | — |

Notes on the implemented work:

- Gap 5 also normalised LiteDbX and Stellar to throw `FailedToUpdateException` on version mismatch (they previously threw `ApplicationException`), so the shared contract test is provider-agnostic. Mongo consumption is added and compiles; it needs a live MongoDB instance to run.
- Gap 1 emits instrumented members with their own wrapper-typed backing field (the user's plain field cannot be the property type) and initialises it in the generated constructor. The user's original field is unused once `Instrument = true` is set; declare the field as the instrumented type or ignore the warning.
- Gap 2 resolves nested DTOs with a per-compilation type→DTO map built in `SaturnGenerator.Execute`; nested members are excluded from `Selector` so projections stay translation-safe.
- Gap 3 adds an optional `IChangeTrackingObserver` parameter to `PatchChanges` plus a `ChangeTracker.Observer` for capture metrics; no telemetry package was added.

---

## Gap 1 — `TrackedSet<T>` / `TrackedDictionary<K,V>` and `Instrument = true` emission

**Problem:** plain sets and dictionaries are only tracked via baseline diff (`WholeArray`). The `[CollectionTracking(Instrument = true)]` path (event-driven, no baseline cost) is declared but not emitted, and only `TrackedList<T>` exists.

**Why it matters:** baseline diff requires `BeginTracking(acceptCurrentState: true)` and copies the collection; instrumented wrappers give per-operation paths and no baseline cost.

**Fix**

1. Add wrappers in `Saturn.Data.ChangeTracking/Collections/`, mirroring `TrackedList<T>` (which is an `IList<T>` **wrapper**, because `List<T>` mutators are not virtual):
   - `TrackedSet<T> : ISet<T>, IReadOnlyCollection<T>` over an inner `HashSet<T>`; raise `ListAdd`/`ListRemove` (`ListAdd`→`$addToSet`, `ListRemove`→`$pull` under `SetOps`).
   - `TrackedDictionary<TKey,TValue> : IDictionary<TKey,TValue>, IReadOnlyDictionary<TKey,TValue>` over an inner `Dictionary<,>`; raise `Set`/`Unset` with path `Member.key`.
   - Reuse `TrackedCollectionChange<T>`; for dictionaries add `TrackedDictionaryChange<TKey,TValue>` or encode `OldValue/NewValue` + key in the existing struct's `Index` slot (prefer a dedicated struct for clarity).
2. Emit the wrapper only when `member.InstrumentCollection` is true and the member is a **field or partial property** (a non-partial user property cannot change type without breaking the user's declaration):
   - `RepositorySide.Generator` (`SourceCodeGenerator`/`TrackingGenerator`): choose the emitted property type by `PlainCollectionKind` → `TrackedList<T>` / `TrackedSet<T>` / `TrackedDictionary<K,V>`.
   - Emit a named handler per member and subscribe in the constructor; in the setter, unsubscribe the old and subscribe the new (same shape as the ObservableList path added in Phase D).
   - **Coerce on assignment** so hydration/population does not drop instrumentation:
     ```csharp
     set
     {
         if (ReferenceEquals(field, value)) return;
         if (field is not null) field.Changed -= OnXChanged;
         SetField(ref this.field, value is TrackedList<T> ? value : new TrackedList<T>(value, OnXChanged));
         if (field is not null) field.Changed += OnXChanged;
     }
     ```
3. Keep baseline capture for instrumented members too: it is the safety net for code that mutates the underlying collection through a non-wrapper reference (the wrapper only sees calls routed through it).

**Tests**
- `TrackingGenerationTests`: `Instrument_List_Emits_TrackedList_Property`, `Instrument_Set_Emits_TrackedSet_Property`, `Instrument_Dictionary_Emits_TrackedDictionary_Property`, `Untracked_Plain_Collection_Keeps_Original_Type`.
- Runtime: add/remove/replace raise the right kinds; `SetOps` maps to `$addToSet`/`$pull`; dictionary key ops map to per-key `$set`/`$unset`.

**Effort:** medium (2 wrappers + emission branch + tests). **Risk:** the property type change is a breaking shape change; keep it strictly opt-in and document.

---

## Gap 2 — Recursive DTO mapping and `IncludeProperties`

**Problem:** DTO collections map to `List<element type>` with no nested DTO resolution, and `[GenerateDto(IncludeProperties)]` is parsed but unused.

**Fix — nested DTOs (two-pass generation)**

1. In `SaturnGenerator.Initialize`, build a second provider that collects the set of types which will have DTOs:
   ```csharp
   var dtoTypes = context.SyntaxProvider.CreateSyntaxProvider(...)
       .Where(...)                       // class with [GenerateDto] OR (assembly default && !NoGenerateDto)
       .Select((c, _) => (TypeName: c.Symbol.ToDisplayString(), DtoName: ResolveDtoName(c.Symbol)));
   ```
   Combine it: `classDeclarations.Collect().Combine(options).Combine(dtoTypes.Collect())`.
2. Pass the resolved `IReadOnlyDictionary<string,string>` (type → DTO name) into `DtoGenerator.Generate`.
3. In `DtoGenerator`, for a member whose category is `EmbeddedEntity` or whose collection element is an `Entity`:
   - if the type has a DTO → emit `{Element}Dto` and map with `{Element}Dto.FromEntity(item)` / `item.ToEntity()`;
   - else → keep today's behaviour (entity type directly, or ref id).
4. `Ref<T>` expansion: `ExpandRefs = true` currently embeds the **entity**; switch to `{T}Dto?` when `T` has a DTO, falling back to the entity type otherwise. `ToEntity` becomes `new Ref<T>(value?.Id)`.
5. **Keep nested DTO expansion out of `Selector`** (already the case): `Expression` trees cannot contain the `FromEntity` calls, and Mongo/LiteDbX cannot translate them. Document that expanded DTOs are materialised client-side or via a follow-up projection of ids.

**Fix — `IncludeProperties`**

`Entity.Properties` is declared on `Entity`, so `classSymbol.GetMembers()` (declared-only) never sees it. Either:
- define `IncludeProperties` as "include the inherited `Properties` bag" and emit
  ```csharp
  public Dictionary<string, object?>? Properties { get; set; }
  ```
  mapping `source.Properties` (public, inherited) in `UpdateFrom`/`ApplyTo`; or
- rename/repurpose it to "members to include" (a names array) — but the attribute already has positional/named shape; prefer the first (bag inclusion) and rename to `IncludePropertyBag` to remove ambiguity.

**Tests:** `Dto_Maps_Nested_Entity_To_Dto`, `Dto_Maps_Collection_Of_Entities_To_Dto_List`, `Dto_Expands_Ref_To_Dto_When_Type_Has_Dto`, `Dto_Includes_Property_Bag_When_Configured`, `Dto_Selector_Still_Contains_No_Conversions`.

**Effort:** medium–high (two-pass plumbing + emitter branching + tests).

---

## Gap 3 — Wire `IChangeTrackingObserver` (telemetry)

**Problem:** `IChangeTrackingObserver` exists but nothing calls it; there is no capture/conflict telemetry.

**Fix — keep it out of Abstractions**

`GoLive.Saturn.Data.Abstractions` must not reference ChangeTracking (decision 1), so do not add the observer to `RepositoryOptions`. Wire it where the ChangeTracking types already live:

1. `ChangeTracker` gains an optional `IChangeTrackingObserver? Observer` (constructor arg or settable property). `Build` reports:
   - `OnChangeSetCaptured(owner.GetType().Name, fields.Count, writeOnlyExcluded)`.
2. `RepositoryPatchExtensions.PatchChanges` gains an optional observer parameter and wraps the call:
   ```csharp
   public static async Task PatchChanges<TEntity>(this IRepository repository, string id, long? expectedVersion,
       EntityChangeSet changes, IChangeTrackingObserver? observer = null,
       IDatabaseTransaction? transaction = null, CancellationToken cancellationToken = default)
       where TEntity : Entity
   {
       try
       {
           await repository.Patch<TEntity>(id, expectedVersion, changes.ToUpdateDocument(), null, transaction, cancellationToken);
       }
       catch (FailedToUpdateException)
       {
           observer?.OnPatchConflict(typeof(TEntity).Name, id, expectedVersion);
           throw;
       }
   }
   ```
3. For per-provider capture metrics, the generated `GetChangeSet()`/`ToUpdateDocument()` can route through a generated `tracker.Observer` if the consumer sets `ChangeTracker.Observer` via an ambient default (`ChangeTrackingTelemetry.Observer`) — recommend an explicit parameter first, ambient only if a DI story demands it.

**Tests:** `Observer_Receives_ChangeSet`, `Observer_Receives_Patch_Conflict` (use a fake repository throwing `FailedToUpdateException`), `Observer_Is_Null_Safe`.

**Effort:** small. **Note:** do not add a telemetry package; keep the interface dependency-free.

---

## Gap 4 — `PatchableMembers` for limited views (not just DTOs)

**Problem:** the allow-list is emitted for generated DTOs only.

**Fix**

1. Emit `PatchableMembers` in the view class body in `SourceCodeGenerator` (the same place `GeneratedCodeAttribute` and view members are emitted), built from the view's member set (`item.Select(r => r.classDef)`), excluding `Id`, `Version`, `[Readonly]` and `[WriteOnly]` members, and excluding members only present via `AddParentItemToLimitedView` unless they are intended to be patchable.
2. For inherited views (`InheritsParentLimitedViews && !FlattenParentLimitedViews`), union with `{ParentClass}_{View}.PatchableMembers` so the allow-list matches the effective surface.
3. Use `ChangeSetValidator.ValidatePatch(changes, View.PatchableMembers)` at the API/controller boundary (already implemented in ChangeTracking).

**Tests:** `View_Emits_PatchableMembers`, `View_PatchableMembers_Excludes_Id_And_ReadOnly`, `Inherited_View_PatchableMembers_Include_Parent_Members`.

**Effort:** small.

---

## Gap 5 — Provider parity end-to-end tests (Mongo included)

**Problem:** SQLite is verified end-to-end; LiteDbX/Stellar are build-only; Mongo is not exercised.

**Fix — shared contract test**

1. Add `ChangeSetPatchContractTests<TFixture, TRepository>` to `Saturn.Data.Testing.Shared`, constrained to `TRepository : IRepository`, with a fixture that can insert/read and `SupportsTransactions`.
2. Cases (top-level paths only, since path grammar is provider-dependent):
   - `$set` scalar + version bump;
   - `$unset` removes a field;
   - `$inc` adds a delta;
   - whole-array `$set` replaces a collection;
   - stale `expectedVersion` throws `FailedToUpdateException`.
3. Consume it in each provider test project: `Saturn.Data.Sqlite.Tests` (already has an equivalent — refactor to the shared base), `Saturn.Data.LiteDbX.Tests`, `Saturn.Data.Stellar.Tests`, `Saturn.Data.MongoDb.Tests`.
4. Add a `ChangeSetFactory` helper (in Testing.Shared) so providers build `EntityChangeSet` instances without duplicating JSON.
5. Mark **dotted/array-index paths** (`Customer.Name`, `Lines.3`) as a separate provider-capability test: Mongo native, SQLite via `json_set`, LiteDbX/Stellar need a `SetPath`/`UnsetPath` implementation (currently top-level only). If you want parity, add a small dotted-path helper in each provider's `Patch`.

**Effort:** medium (shared base + 4 consumers); Mongo needs a running instance, which the repo already assumes.

---

## Gap 6 — Generated code nullable context

**Problem:** generated files use `#nullable enable annotations` + `#nullable disable warnings`, not the plan's plain `#nullable enable`.

**Options**

- **Keep as-is (recommended).** Generated code is oblivious by design; enabling the warning context floods consumers with CS8618/CS8600 warnings from generated bodies. Document the choice in the generator README.
- **Full `#nullable enable`:** would require nullable-annotating every generated member (backing fields `private T? x;`, `source` parameters `T?`, `List<T>?`), which is a large mechanical change with high regression risk and little consumer value.
- Middle ground: `#nullable enable annotations` **plus** nullable annotations on the public surface only (already done for property types) and `#nullable disable warnings` for bodies. That is exactly the current state — so record it as the intended design rather than a gap.

**Effort:** zero if accepted as intended; significant only if you want full warnings-clean generated code.

---

## Gap 7 — Hydration suppression inside providers

**Problem:** providers set properties during deserialization; tracking is safe only because it starts off (`tracker.IsTracking == false`). A caller who enables tracking and then rehydrates the same instance will journal the hydration writes.

**Fix — a type-free interface in Entities**

`GoLive.Saturn.Data.Entities` cannot reference ChangeTracking, so:

1. Add to Entities:
   ```csharp
   namespace GoLive.Saturn.Data.Entities;
   public interface ISuppressTracking : IDisposable { }
   ```
   (or `void Suppress()` returning `IDisposable`), and have generated tracked types implement it by returning `tracker.Suppress()`. This mirrors the existing `IEntityReference` pattern.
2. Providers (which reference Entities) wrap population:
   ```csharp
   using var scope = item is ISuppressTracking suppress ? suppress.SuppressTracking() : null;
   ```
   Apply in Mongo's materialisation, LiteDbX/Stellar/SQLite deserialization helpers, and in `PopulationExtensions.Populate` / generated `UpdateFrom` entry points.
3. Keep the "tracking starts off" default as the primary safety mechanism; this interface only hardens the re-track case.

**Tests:** `Hydration_Inside_Provider_Does_Not_Journal` per provider; `Populate_Does_Not_Journal`.

**Effort:** small–medium (one interface + provider call sites).

---

## Gap 8 — `ChangeTrackingMode` is parsed but not honoured

**Problem:** `[ChangeTracking(Mode = ...)]` is read into `ClassToGenerate.TrackingMode`, but `ChangeTracker.Build` always merges the journal **and** the baseline diff.

**Fix**

Pass the mode into the tracker (generated `BeginTracking` already calls `tracker.Begin(this, acceptCurrentState)`), and branch in `Build`:

- `Journal` → `journal` only;
- `Baseline` → `owner.ComputeBaselineDiff()` only (scalars must also be diffed against baseline in this mode — extend the generated `ComputeBaselineDiff` to emit scalar diffs when mode is Baseline);
- `JournalWithBaseline` → merge (current behaviour, the sensible default).

Implementation detail: `ChangeTracker` holds `ChangeTrackingMode Mode`; generated code sets it in the `tracker` field initialiser or in `BeginTracking`:
```csharp
public void BeginTracking(bool acceptCurrentState = true) { tracker.Mode = global::...ChangeTrackingMode.Baseline; tracker.Begin(this, acceptCurrentState); }
```
Simplest: emit `private readonly ChangeTracker tracker = new() { Mode = ChangeTrackingMode.Baseline };` using the class attribute value, and default `JournalWithBaseline` when collections are present, `Journal` for scalar-only entities.

**Tests:** `Baseline_Mode_Reports_Diff_Against_Baseline`, `Baseline_Mode_Elides_Reverted_Field`, `Journal_Mode_Reports_Journal_Only`, `JournalWithBaseline_Merges`.

**Effort:** small–medium. **This is arguably the most important remaining correctness gap**, because it changes which fields appear in a patch.

---

## Suggested order

1. **Gap 8** (correctness of what gets patched — smallest, highest impact).
2. **Gap 3** and **Gap 4** (telemetry + allow-lists — small, completeness).
3. **Gap 7** (hydration hardening — small, prevents a real failure mode).
4. **Gap 5** (shared provider contract tests — catches regressions across providers).
5. **Gap 2** (recursive DTOs + `IncludeProperties` — feature completeness).
6. **Gap 1** (instrumented set/dictionary wrappers — largest; baseline diff already covers the default path).
7. **Gap 6** (decide and document; no work unless you want full nullable warnings in generated code).

## Cross-cutting note

Every gap above is additive and testable through the existing `CSharpGeneratorDriver` harness (`GeneratorTestHarness`) for generation and through the ChangeTracking/SQLite test projects for runtime behaviour. None require changing the abstraction contracts, and none require adding a NuGet dependency.
