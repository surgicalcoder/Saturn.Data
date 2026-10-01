# Generator Features: DTO Generation, Change Tracking & Collection Instrumentation

**Status:** Implemented (2026-09-26 → 2026-09-27)
**Author:** Axiom
**Scope:** Everything the source generator (`Saturn.Generator.Entities`) and its companion runtime (`GoLive.Saturn.Data.ChangeTracking`) gained over the last few days: opt-in DTO generation, rich change tracking, collection instrumentation, the provider patch bridge, and the Phase-A hardening of the generator itself.
**Source plans:** `docs/generator-dto-mapping-change-tracking-assessment.md`, `docs/generator-implementation-plan/` (phases A–F + C10), `docs/generator-implementation-plan/gap-remediation.md`.

---

## 1. What changed, at a glance

| Area | Delivered |
| --- | --- |
| **Generator hardening** | NRE/null-safety fixes in generated views, attribute/annotation preservation, string `_runAfterSet` hook fix, generated-file hygiene, and a new generator test harness |
| **DTO generation** | Opt-in `[GenerateDto]` (plus MSBuild default), unified DTO emitter, projection `Selector`, mapping (`FromEntity`/`ToEntity`/`ApplyTo`/`UpdateFrom`), ref handling, recursive nested DTOs, `IncludeProperties` |
| **Change-tracking runtime** | New `GoLive.Saturn.Data.ChangeTracking` project: journal + baseline diff, `EntityChangeSet`/`FieldChange`, update-document builder, validator, observer |
| **Change-tracking codegen** | Per-entity/per-DTO tracking members (`BeginTracking`, `GetChangeSet`, `AcceptChanges`, …), baseline capture/restore, visibility & strategy metadata |
| **Collection instrumentation** | `TrackedList<T>`/`TrackedSet<T>`/`TrackedDictionary<K,V>` wrappers, `[CollectionTracking(Instrument=true)]`, baseline diff for plain collections |
| **Patch bridge** | `PatchChanges` extension, `ToUpdateDocument()` → `{ $set, $unset, $inc, $addToSet, $pull }`, cross-provider contract tests |
| **Security & telemetry** | Per-view and per-DTO `PatchableMembers`, `VisibilityChangeSetFilter`, `IChangeTrackingObserver`, `ISuppressTracking` hydration suppression |
| **Provider normalisation** | LiteDbX/Stellar now throw `FailedToUpdateException` on version mismatch (was `ApplicationException`), making the patch contract provider-agnostic |

Commit map: `eb90d6c`, `172efbd`, `032300e`, `91f802a`, `4970239`, `1761b2a`, `1b6dd9c`, `87c81a2`, `9db0876`, `e882d9d`, `d00c616`, `fbf75f0`, `c5aba5b`.

---

## 2. Architecture

Three cooperating pieces, deliberately kept separable:

1. **`Saturn.Generator.Entities.Resources`** (netstandard2.0) — attributes/interfaces the consumer declares: `[GenerateDto]`, `[NoGenerateDto]`, `[ExcludeFromDto]`, `[DoNotTrackChanges]`, `[Embedded]`, `ICreatableFrom<T>`, `PopulationExtensions`.
2. **`Saturn.Generator.Entities`** (netstandard2.0 Roslyn generator) — scanner + emitters (`SourceCodeGenerator`, `DtoGenerator`, `TrackingGenerator`) + 3 analyzers/code fixes.
3. **`GoLive.Saturn.Data.ChangeTracking`** (net10.0 runtime) — the tracking model, collection wrappers, update-document builder, validator, observer, and the `PatchChanges` repository extension.

Key separations (design rule 1/7): `GoLive.Saturn.Data.Entities` does **not** reference the ChangeTracking project; the base `Entity` carries only type-free hooks (`OnFieldChanged`, `CaptureBaseline`, `RestoreBaseline`, `ISuppressTracking`), and the generator emits tracking code only into opted-in types. `GoLive.Saturn.Data.Abstractions` stays free of ChangeTracking; `PatchChanges` lives in the tracking package.

---

## 3. Attribute reference

### Opt-in / opt-out (assembly default + per-class)

Two MSBuild switches are read via `AnalyzerConfigOptionsProvider.GlobalOptions` (`SaturnGenerator.cs:61-63`):

```xml
<SaturnGenerateDtos>true</SaturnGenerateDtos>
<SaturnChangeTracking>true</SaturnChangeTracking>
```

Resolution (`SaturnGenerator.cs:73-74`):

- `GenerateDto = ([GenerateDto] present || (default && ![NoGenerateDto])) && !DtoAlreadyExists`
- `TrackChanges = [ChangeTracking] present || (default && ![NoChangeTracking])`

### Class-level

| Attribute | Options (defaults) | Meaning |
| --- | --- | --- |
| `[GenerateDto]` | `Name` (null → `{Class}Dto`), `IncludeProperties` (false), `TrackChanges` (false), `ExpandRefs` (false), `UseFullId` (false) | Generate a DTO for this entity |
| `[NoGenerateDto]` | — | Opt out of the assembly/global DTO default |
| `[ChangeTracking]` (in ChangeTracking package) | `Mode` (`Journal`), `TrackRefItemChanges` (false) | Opt this class into tracking — **note:** `Mode`/`TrackRefItemChanges` are parsed but not yet honoured (see §11) |
| `[NoChangeTracking]` | — | Opt out of the global tracking default |
| `[AddParentItemsLimitedViews]` | `bool Flatten` | Inherit or flatten parent limited views |
| `[AddParentItemToLimitedView]` | ctor `(string ViewName, string ParentField)`; `ChildField`, `LimitedViewType`, `TwoWay`, `InheritFromIUniquelyIdentifiable` | Surface a parent's field in a child view |

### Member-level

| Attribute | Applies to | Meaning |
| --- | --- | --- |
| `[ExcludeFromDto]` | field/property | Omit member from the DTO |
| `[Embedded]` | field/property | Parsed into `IsEmbedded` but **no emitter consumes it yet** (aspirational) |
| `[DoNotTrackChanges]` | field/property | Setter bypasses `SetField`; member excluded from the journal (and from DTOs) |
| `[CollectionTracking]` | field/property | `Strategy` (`WholeArray` default, `IndexedOps`, `SetOps`), `Instrument` (false) |
| `[AddToLimitedView]` | field/property | `ctor(string ViewName, bool TwoWay=false)`; `LimitedViewType`, `Initializer`, `ComputedProperty`, `ComputedExpression`, `ComputedSelectorExpression`, … |
| `[ExcludeFromLimitedView]` | field/property | Remove member from view(s); `"*"` clears all |
| `[ReadonlyInView]` | field/property | View property is getter-only |
| `[ReadOnly]` | field (`AttributeUsage(Field)`) | Entity property getter-only; excluded from patch |
| `[WriteOnly]` | field (`AttributeUsage(Field)`) | Entity property setter-only; maps to `ChangeVisibility.WriteOnly`; excluded from DTO/patch |
| `[AddRefToScope]` | field/property | Scope-aware setter maintaining `Scopes` (must be `Ref<T>` on `MultiscopedEntity<T>` or `SATURN005`) |

`{member}_runAfterSet` is a convention (not an attribute): a one-parameter method invoked after the setter. Three shapes are recognised — same type, `Ref<T>` member with `T` parameter, and `string`.

---

## 4. Emitted API — entities and limited views

For an entity opted into tracking, the generator (`SourceCodeGenerator`) emits into `{Name}.g.cs` (header `// <auto-generated/>`, `[GeneratedCode("Saturn.Generator.Entities", …)]`):

- `public partial class {Name} : INotifyPropertyChanged` + (`ITrackable`, `ITrackableMetadata`, `ISuppressTracking` when tracking) + (`IUpdatableFrom<{Name}_{View}>`, `ICreatableFrom<{Name}_{View}>` per two-way view).
- Backing field per partial property; `ObservableList<T>` initialisation and `CollectionChanged` subscription in the constructor.
- One property per member, with a setter shape chosen by attribute: scope-aware (`[AddRefToScope]`), no-tracking (`[DoNotTrackChanges]`), `runAfterSet`-aware, `ReadOnly` (no setter), or default `SetField`.
- `To_{View}()` factory methods; `Create(...)`; `UpdateFrom(...)` per two-way view (now null-guarded and wrapped in `SuppressTracking()`).

Per **limited view** `{Name}_{View}`:

- `implicit operator {Name}_{View}({Name})`, `implicit operator {Name}_{View}?(Ref<{Name}>)`.
- View properties (get-only for `[ReadonlyInView]`); parent-item properties.
- `protected virtual OnUpdateFromStart/End`, `OnGenerateStart/End`.
- `static Generate`, `static Selector` (`Expression<Func<{Name},{Name}_{View}>>`), `Create`.
- Two-way only: `UpdateFrom({View})`, `UpdateParent({Name})`.
- `public static readonly HashSet<string> PatchableMembers` — writable members only (skips `Id`, `Version`, `[ReadOnly]`, `[WriteOnly]`); unions the parent view's list when inheriting.

View inheritance works two ways: **inherit** (derive from `{Parent}_{View}`, `new` `Generate`, `base.UpdateFrom`, unioned `PatchableMembers`) or **flatten** (parent members merged, no base class).

---

## 5. Emitted API — DTOs

Generated only when `GenerateDto && !DtoAlreadyExists`. Shape:

```csharp
public partial class {Entity}Dto
    : ICreatableFrom<{Entity}>, IUpdatableFrom<{Entity}>
    // + ITrackable, ITrackableMetadata, ISuppressTracking when [GenerateDto(TrackChanges=true)]
{
    public const string DtoSchemaVersion = "1";
    public string? Id { get; set; }                     // source._shortId; UseFullId=true -> source.Id
}
```

Mapping surface:

- `static implicit operator {Entity}Dto({Entity})`, `static implicit operator {Entity}Dto?(Ref<{Entity}>)`
- `static {Entity}Dto FromEntity({Entity})`, `static {Entity}Dto? FromRef(Ref<{Entity}>?)`
- `static ICreatableFrom<{Entity}> Create({Entity})`
- `{Entity} ToEntity()`, `void ApplyTo({Entity} target)` (ignores `Id`/`Version`), `void UpdateFrom({Entity} source)`
- `static Expression<Func<{Entity},{Entity}Dto>>? Selector` — set to `null` when nothing is projectable
- `public static readonly HashSet<string> PatchableMembers`

Member mapping rules:

- `[ExcludeFromDto]` and `[WriteOnly]` members are skipped.
- Collections → `List<{element}>`.
- `Ref<T>`/`WeakRef<T>` → `string?` (the id) by default; with `ExpandRefs` (or when `T` has a DTO) → `{T}Dto?`.
- Nested entities with a known DTO → `{Entity}Dto` (resolved through a compilation-wide type→DTO map built in `SaturnGenerator`, so **nested DTO conversion is kept out of `Selector`** to stay translation-safe).
- `[GenerateDto(IncludeProperties=true)]` → `Dictionary<string, object>? Properties` copied from the inherited bag.
- `HashedString`/`EncryptedString` keep their runtime type in the DTO (not flattened).

---

## 6. Change-tracking runtime

New project `GoLive.Saturn.Data.ChangeTracking`.

**Enums**

| Enum | Values |
| --- | --- |
| `ChangeKind` | `Set`, `Unset`, `Increment`, `ListAdd`, `ListRemove`, `ListReplace`, `ListMove`, `ListClear` |
| `ChangeVisibility` | `ReadWrite`, `ReadOnly`, `WriteOnly`, `ServerManaged` |
| `CollectionStrategy` | `WholeArray` (default), `IndexedOps`, `SetOps` |
| `ChangeTrackingMode` | `Journal` (default), `Baseline`, `JournalWithBaseline` |

**Model**

- `FieldChange` — `Path`, `Kind`, `OldValue`, `NewValue`, `Visibility`, `Index?`, `Strategy`.
- `EntityChangeSet` — `EntityType`, `Id`, `ExpectedVersion`, `CapturedAtUtc`, `IReadOnlyList<FieldChange> Fields`, `IsEmpty`, `ToUpdateDocument()` / `ToUpdateDocument(IChangeSetFilter)`.
- `ChangeTracker` — `Begin/Accept/Reject/Record/RecordList/Build`, `CaptureValue/BaselineValue`, `Suppress()`; `IsTracking`, `HasChanges`, `Observer`, `Mode`. Non-tracked members: `Id`, `Version`, `Changes`, `EnableChangeTracking`, `_shortId`, `ChangeTrackingParent`, `ChangeTrackingPathSegment`. Paths compose recursively through parent links; `IEntityReference` values normalise to `RefId`.
- `ITrackable` / `ITrackableMetadata` / `IChangeTracked`; `IChangeSetFilter` + `VisibilityChangeSetFilter`; `IChangeTrackingObserver`.
- `ChangeSetValidator.ValidatePatch(changes, allowed)` — root-path allow-list enforcement.
- `UpdateDocumentBuilder` — `FieldChange[]` → JSON (see §8).
- `RepositoryPatchExtensions.PatchChanges<TEntity>(...)` — repository bridge (see §9).

---

## 7. Change-tracking codegen

Emitted per opted-in entity/DTO (`TrackingGenerator`):

- `private readonly ChangeTracker tracker = new();`
- `IsTracking`, `HasChanges`, `BeginTracking(bool acceptCurrentState = true)`, `AcceptChanges()`, `RejectChanges()`, `GetChangeSet()`, `ToUpdateDocument()`, `GetTracker()`, `SuppressTracking()`.
- `OnFieldChanged(...)` (skip when hydrating), `CaptureBaseline()`, `RestoreBaseline()`, `ComputeBaselineDiff()`, `VisibilityFor(string)`, `StrategyFor(string)`.
- Per `ObservableList<T>` member: `On{Member}Changed(in NotifyCollectionChangedEventArgs<T>)` mapping Add/Remove/Replace/Move/Reset to `RecordList`, plus child parent-link wiring.
- Per instrumented collection: `On{Member}Changed` (list/set) or `On{Member}KeyChanged` (dictionary) → `RecordList(..., strategy, visibility)`.

---

## 8. Collection instrumentation

Classification (all default to `WholeArray`):

| Declared type | Kind | Default strategy |
| --- | --- | --- |
| `ObservableList<T>` | observable | `WholeArray` |
| `List<T>`, `IList<T>`, `Collection<T>` | list | `WholeArray` |
| `T[]` | array | `WholeArray` |
| `HashSet<T>`, `ISet<T>` | set | `WholeArray` |
| `Dictionary<K,V>`, `IDictionary<,>` | dictionary | `WholeArray` |
| other `IEnumerable<T>` | other | whole-value `$set` |

`[CollectionTracking(Instrument=true)]` upgrades a plain list/set/dictionary to a wrapper-typed property:

- `TrackedList<T> : IList<T>, IReadOnlyList<T>`
- `TrackedSet<T> : ISet<T>, IReadOnlyCollection<T>`
- `TrackedDictionary<K,V> : IDictionary<K,V>, IReadOnlyDictionary<K,V>`

The setter coercively wraps assigned values and re-subscribes handlers; baseline capture is retained as a safety net for mutations made through a non-wrapper reference.

**Operator mapping**

| Kind | WholeArray | IndexedOps | SetOps |
| --- | --- | --- | --- |
| List/Array add | `$set: { "M": [...] }` | `$set: { "M.N": v }` | — |
| List/Array remove | `$set: { "M": [...] }` | `$unset: { "M.N": true }` (descending index order) | — |
| List replace/move | `$set` whole | `$set: { "M.N": v }` | — |
| Set add/remove | `$set` whole | — | `$addToSet` / `$pull` |
| Dictionary set/unset | per-key `$set`/`$unset` (whole-object fallback for dotted keys) | — | — |

Semantics: null ≠ empty; list diff is positional; set diff is order-insensitive; dictionary diff ignores key order; `Ref<T>`/entity elements compare by id; journal entries merge with baseline diffs (baseline wins on path collision). `$addToSet`/`$pull` are Mongo-only in v1; other providers fall back to `WholeArray` or throw `NotSupportedException`.

---

## 9. Patch bridge & provider parity

- `EntityChangeSet.ToUpdateDocument()` renders `{ "$set": { … }, "$unset": { … }, "$inc": { … }, "$addToSet": { … }, "$pull": { … } }` (empty operators omitted; empty set → `{}`). Default `VisibilityChangeSetFilter` drops `WriteOnly` and `ServerManaged`.
- `PatchChanges<TEntity>(this IRepository, string id, long? expectedVersion, EntityChangeSet changes, IChangeTrackingObserver? observer = null, IDatabaseTransaction? transaction = null, CancellationToken = default)` calls `repository.Patch<TEntity>(...)` and, on `FailedToUpdateException`, fires `observer.OnPatchConflict(...)` and rethrows.
- Provider matrix: Mongo native (`JsonUpdateDefinition` + `Inc("_v", 1)`); SQLite server-side `$set`/`$inc`/`$unset`; LiteDbX and Stellar read-modify-write with version bump and `FailedToUpdateException` on mismatch (normalised). Dotted/array-index paths: Mongo native, SQLite via `json_set`; LiteDbX/Stellar currently top-level only.
- Shared contract tests: `ChangeSetPatchContractTests<TFixture, TRepository>` in `Saturn.Data.Testing.Shared` (`$set` scalar + version bump, `$unset`, `$inc`, whole-array `$set`, stale version → `FailedToUpdateException`), consumed by the SQLite/LiteDbX/Stellar/Mongo test projects, with a `ChangeSetFactory` helper.
- Change feed: `FeedPayloadMode.Patch` carries the update document in `ItemsJson` on update/patch/increment.

---

## 10. Security, visibility & telemetry

- **Allow-lists:** `PatchableMembers` emitted for both limited views and DTOs; `ChangeSetValidator.ValidatePatch(changes, View.PatchableMembers)` rejects non-allow-listed roots (`InvalidOperationException`), nested paths permitted when the root is allowed.
- **Visibility:** `[ReadOnly]`/`[WriteOnly]`/`[DoNotTrackChanges]` shape the emitted setter and the patch document; `WriteOnly`/`ServerManaged` are filtered from client-facing documents by default; `Id`/`Version` are always excluded.
- **Telemetry:** `IChangeTrackingObserver.OnChangeSetCaptured(entityType, fieldCount, writeOnlyExcluded)` (fired in `Build`) and `OnPatchConflict(entityType, id, expectedVersion)` (fired on conflict). Wired via `ChangeTracker.Observer`; no telemetry package dependency.
- **Hydration suppression (Gap 7):** `Entity.ISuppressTracking` + generated `SuppressTracking()`; `PopulationExtensions.Populate` and provider materialisation wrap population so re-hydrating a tracked instance does not journal. Tracking still starts off by default.

---

## 11. Diagnostics, analyzers & code fixes

**Generator diagnostics** (`SaturnGenerator.cs:14-52`):

| ID | Severity | Meaning |
| --- | --- | --- |
| `SATURN001` | Warning | `[AddParentItemsLimitedViews]` where the parent defines no views and the child contributes none |
| `SATURN002` | Warning | `[ExcludeFromLimitedView("X")]` references an undefined view |
| `SATURN003` | Warning | `[ReadonlyInView("X")]` references an undefined view |
| `SATURN004` | Error | `[AddParentItemToLimitedView]` references an unresolvable parent field |
| `SATURN005` | Error | `[AddRefToScope]` on a non-`Ref<T>` member or non-`MultiscopedEntity<T>` class |

**Analyzers / code fixes** (`CodeFixes/`; diagnostic IDs are plain identifiers, not `SATURNxxxx`):

| Analyzer | ID | Severity | Enforces | Fix |
| --- | --- | --- | --- | --- |
| `ChangeTrackingAnalyzer` | `ChangeTrackingAnalyzer` | Warning | Non-`partial` property in an `Entity` won't be tracked | Make partial / add exception comment |
| `FieldToPartialPropertyAnalyzer` | `FieldToPartialPropertyAnalyzer` | Info | Private field in an `Entity` isn't a partial property | Convert to partial property |
| `RunAfterSetAnalyzer` | `RunAfterSetAnalyzer` | Info | Partial member could have a `{name}_runAfterSet` method | Create the method |

---

## 12. Hardening / stabilization (Phase A)

- **Null safety:** view `UpdateFrom` null-guards, and `implicit operator {View}(Ref<T>)` no longer dereferences a null ref (was an `NullReferenceException`).
- **String hook fix:** `_runAfterSet` now sets the correct `HasRunAfterSetMethodIsString` flag and is invoked from the setter.
- **Attribute preservation:** user attributes (DataAnnotations, `System.Text.Json.Serialization`, user namespaces) are copied to generated properties; Saturn/Entities/CompilerServices/CodeDom namespaces are excluded.
- **Nullability:** `MemberToGenerate.IsNullable` drives `?` on generated members; generated files use `// <auto-generated/>` + `#nullable enable annotations` and `#nullable disable warnings` (deliberate — see §13 Gap 6).
- **Generated-file hygiene:** `// <auto-generated/>` header and `[GeneratedCode]` attribute.
- **Test harness:** `Saturn.Generator.Entities.Tests` (`GeneratorTestHarness.CreateCompilation/Run/GeneratedFor`) with stabilization tests for the auto-generated header, null-guarded `UpdateFrom`, and null-safe ref operator.

---

## 13. Design decisions & deferred work

**Resolved decisions**

1. New `GoLive.Saturn.Data.ChangeTracking` project; tracking is opt-in; `PatchChanges` lives there; Entities/Abstractions stay clean.
2. Default collection strategy `WholeArray`.
3. Both assembly-level default (MSBuild) and per-class opt-in/opt-out.
4. DTO `Id` uses `_shortId` by default; `UseFullId=true` for the 24-hex id.
5. `Ref<T>` is id-by-default with an `ExpandRefs`/nested-DTO auto-expand option.
6. Both entities and DTOs track changes.
7. MongoDB-style update document verified on Mongo/Stellar/LiteDbX/SQLite.
8. All intra-repo references are `ProjectReference`.
9. Both `ObservableList<T>` and plain collections (baseline diff + opt-in wrappers).

**Fixed (gap-remediation pass, 2026-10-01)**

- **Gap 8 — `ChangeTrackingMode` is now honoured.** `ChangeTracker.Mode` (default `JournalWithBaseline`) drives `Build`: `Journal` = journal only, `Baseline` = `ComputeBaselineDiff` only, `JournalWithBaseline` = merged with path de-duplication. The generator emits the mode from `[ChangeTracking(Mode = …)]`; when `Mode` is not set it defaults to `JournalWithBaseline` for entities with plain collections and `Journal` for scalar-only entities. Generated `ComputeBaselineDiff` now emits **scalar** diffs as well as collection diffs.
- **`[Embedded]` is now consumed.** It auto-expands `Ref<T>`/`WeakRef<T>` members to nested DTOs (`DtoGenerator`), and — when tracking is on — wires the embedded child's `ChangeTrackingParent`/`ChangeTrackingPathSegment` in the generated setter so nested mutations journal under `Member.ChildProperty`. `[ChangeTracking(TrackRefItemChanges = true)]` applies the same wiring to all reference members.
- **`ReadonlyAttribute`/`WriteOnlyAttribute`** now declare `AttributeTargets.Field | AttributeTargets.Property`, matching how the scanner reads them.
- **`TrackedSet<T>` bulk ops** (`UnionWith`, `ExceptWith`, `IntersectWith`, `SymmetricExceptWith`) now compute the delta and raise the corresponding add/remove change notifications.
- **`ChangeVisibility.ReadOnly`** is now filtered from client patch documents by default (`VisibilityChangeSetFilter.IncludeReadOnly`, default `false`), alongside `WriteOnly`/`ServerManaged`.
- **Dotted/array-index patch paths and `$addToSet`/`$pull` parity.** New shared `GoLive.Saturn.Data.Abstractions.JsonPatchDocument` applies `$set`/`$unset`/`$inc`/`$addToSet`/`$pull` over dotted paths and array indices. LiteDbX and Stellar now use it (previously top-level only); SQLite builds nested `json_set`/`json_remove` paths and falls back to read-modify-write for `$addToSet`/`$pull`; Mongo already used native `JsonUpdateDefinition`.
- **`DtoAlreadyExists`** no longer enumerates `AppDomain` assemblies; it uses `Compilation.GetTypeByMetadataName` in `SaturnGenerator.Execute` (removes a trimming/AOT hazard in the generator).

**Remaining / deliberate**

- **Gap 6 — generated nullable context** kept as `#nullable enable annotations` + `#nullable disable warnings` (deliberate, to avoid flooding consumers with generated-body warnings).
- The new operator/JSON-path behaviour is unit-tested (`JsonPatchDocumentTests`, `ChangeTrackingModeTests`, `TrackedSet` bulk-op tests, generator mode/embedded tests). An end-to-end provider assertion for `$addToSet`/`$pull` needs a shared entity that carries a collection — tracked as a follow-up.
- Behaviour change to note: a `$set` key containing `.` is now interpreted as a nested path on LiteDbX/Stellar/SQLite (parity with Mongo) rather than a literal dotted property name.

---

## 14. Test coverage

| Project | Coverage |
| --- | --- |
| `Saturn.Generator.Entities.Tests` | `StabilizationTests`, `DtoGenerationTests`, `TrackingGenerationTests`, `CollectionTrackingTests`, `ViewPatchableMembersTests`, `GeneratorTestHarness` |
| `GoLive.Saturn.Data.ChangeTracking.Tests` | `ChangeTrackerTests` (record/normalise/hydration/accept/reject/build), `UpdateDocumentBuilderTests` (operators + visibility filter), `CollectionInstrumentationTests` (wrappers + diff semantics), `ChangeTrackingObserverTests` (capture + conflict + null-safe), `ChangeSetValidatorTests` (allow-list + nested roots) |
| `Saturn.Data.Testing.Shared` | `ChangeSetPatchContractTests` consumed by SQLite/LiteDbX/Stellar/Mongo provider tests |

Build/test:

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet build "D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Playground\Saturn.Generator.Entities.Playground.csproj"
dotnet test  "D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Tests\Saturn.Generator.Entities.Tests.csproj"
```

---

## 15. Known limitations (usage-level)

- In-place mutation of a raw `List<T>` requires baseline mode (`BeginTracking`) or `Instrument=true`; otherwise only the wrapper reference sees the change.
- Mutating a `HashSet<T>` element in place is invisible — replace the element.
- Dictionary keys containing `.` force a whole-object `$set` fallback.
- Very large collections may be skipped by the baseline threshold (`MaxBaselineElements`).
- Nested-DTO expansion is materialised client-side; it is intentionally absent from `Selector` so expression translation stays valid.
