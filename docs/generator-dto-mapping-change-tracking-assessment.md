# Saturn.Generator.Entities — DTO Generation, Mapping, and Extensive Change Tracking → PATCH

**Status:** Assessment + design proposal for review
**Author:** Axiom
**Date:** 2026-09-25
**Scope:** `Saturn.Generator.Entities`, `Saturn.Generator.Entities.Resources`, `GoLive.Saturn.Data.Entities`, and the repository `Patch`/`JsonUpdate` surface across Mongo, LiteDbX, Stellar, and the proposed SQLite provider.

---

## 1. Executive summary

`Saturn.Generator.Entities` is an incremental Roslyn generator that, for every `partial` class deriving from `Entity`, emits a second half that does three things:

1. **Change tracking on scalar and collection members** via `Entity.SetField` and a `Changes` dictionary (`Dictionary<string, object>`).
2. **"Limited view" DTO classes** (`{Entity}_{ViewName}`) selected by `[AddToLimitedView]`, with two-way mapping to/from the entity.
3. **Mapping surface**: `UpdateFrom`, `Generate`, implicit operators, `Create`, and an `Expression` `Selector` for server-side projection.

The repository layer already exposes everything needed to *consume* a patch: `IRepository.Patch<TItem>(id, expectedVersion, jsonDocument, updateDefinition)`, `JsonUpdate<TItem>(id, version, json)`, and a provider-neutral `IDataUpdateDefinition<TItem>` marker. Mongo parses a MongoDB-style update document (`$set`, `$inc`, `$unset`, …) through `JsonUpdateDefinition<TItem>`; LiteDbX and Stellar apply an in-memory mutation; SQLite (see the separate proposal) applies `json_set`/`json_patch`.

**The missing bridge is the change-tracking model itself.** Today:

- Tracking is a flat `Dictionary<string,object>` of *new values only* — no operation kind, no old value, no baseline, no nesting.
- Tracking is **not wired for loaded entities**: collections are subscribed only in the generated constructor, so a list assigned during deserialization/population is never tracked.
- Nested entities, `Ref<T>.Item` mutations, and child→parent propagation are **not** tracked.
- `Changes`/`EnableChangeTracking` are written but **never read anywhere in the repository layer** (verified by grep) — there is no code path from a mutated entity to a `Patch` call.
- Limited views (the current "DTOs") are authorization projections, not general DTOs. There is no "generate a DTO for this entity" mode, collection mapping is incomplete, and `implicit operator View(Ref<T>)` throws `NullReferenceException` for unpopulated refs.

**Recommendation.** Split the work into two deliverable parts:

- **Part 1 — DTO generation & mapping:** unify view/DTO emission, add a `[GenerateDto]` default DTO, generate null-safe recursive maps (scalars, refs, embedded entities, collections), preserve attributes/nullability, and generate projection selectors.
- **Part 2 — Extensive change tracking:** replace the flat journal with a path-based, operation-typed change set that captures old/new values, propagates child→parent, wires collections on every set (not just construction), normalizes refs to ids, and renders a canonical update document consumed by `IRepository.Patch` across all providers.

The rest of this document assesses the current implementation in detail, lists defects with evidence, then specifies both parts and a phased implementation plan.

### 1.1 Decision log (resolved)

| # | Question | Decision |
| --- | --- | --- |
| 1 | Where does change tracking live? | New **`GoLive.Saturn.Data.ChangeTracking`** project. Not all entities need tracking, so it is a separate assembly referenced only when opted in. |
| 2 | Default collection strategy | **`WholeArray`**. |
| 3 | DTO opt-in vs repository-wide default | **Both**: an assembly-level default (MSBuild property) with per-class opt-out, *and* per-class opt-in. |
| 4 | DTO `Id` shape | **`ShortId`** (`_shortId`) by default. |
| 5 | `Ref<T>` in DTOs | **Both**: id string by default, with an **auto-expand** option to embed the nested DTO for display. |
| 6 | Do DTOs track changes? | **Both**: entities and DTOs both generate change tracking. |
| 7 | Wire format | **MongoDB-style update document**, verified working on MongoDB, StellarDB, LiteDbX, and SQLite (coming soon). |
| 8 | Entities version drift | **All intra-repo references become `ProjectReference`; no NuGet.** |
| 9 | `ObservableList<T>` vs plain `List<T>` | **Both**: `ObservableList<T>` for event-driven fidelity; plain collections supported via baseline diff (and opt-in wrappers). |

The following findings are **not defects to fix**:

- **D1** — general DTO generation is delivered as a Part 1 **feature**, not fixed as a defect.
- **D8** — views do not all need an `Id`; this is deliberate.
- **D12** — attributes contain source code by design; arbitrary injection is intended.
- **C1** — how consumers use `Changes` is out of this repo's scope; it is a consumer implementation detail.

Everything else in the defect register is in scope.

---

## 2. Goals / non-goals

**Goals**

- G1. Generate DTO/view types for entities automatically, with explicit, allocation-light, null-safe mapping in both directions. DTO generation is available as an assembly-level default with per-class opt-out, and as per-class opt-in.
- G2. Generate a rich change set for entities **and** DTOs: path, operation, old value, new value, visibility. Change tracking is generated for both, and lives in the separate `GoLive.Saturn.Data.ChangeTracking` assembly.
- G3. Produce a PATCH payload from the change set that is directly consumable by every repository provider's `Patch`/`JsonUpdate`, using the MongoDB-style update document verified across MongoDB, StellarDB, LiteDbX, and SQLite.
- G4. Correctly handle nested embedded entities, `Ref<T>`, `WeakRef`, and all supported collections (`ObservableList<T>`, `List<T>`, arrays, `HashSet<T>`, dictionaries). Default strategy is `WholeArray`.
- G5. Provide optimistic concurrency via `Version` and a clean `BeginTracking`/`AcceptChanges`/`RejectChanges` lifecycle.
- G6. Never leak `WriteOnly`/server-managed fields into client-facing DTOs or patches.
- G7. Preserve the existing generated surface (`To_ViewName`, `Generate`, `UpdateFrom`, `Selector`) so current consumers keep working.
- G8. Use `ProjectReference` for all intra-repo dependencies (no NuGet for Saturn assemblies).
- G9. Use `_shortId` as the default DTO identity, with a full-id option.

**Non-goals (initially)**

- General-purpose ORM mapping to arbitrary external models.
- Expression-compiled mapping (generated direct assignment is the target).
- Multi-entity transactional patch batching (can layer on later).
- RFC 6902 JSON Patch. The MongoDB-style update document is confirmed as the canonical wire format.
- Changing the MongoDB-style wire format used by `MongoDbRepository.Patch`; the new payload targets it across all providers.

Baseline diff **is** in scope: it is the default mechanism for detecting in-place mutation of plain (non-observable) collections (`List<T>`, arrays, `HashSet<T>`, dictionaries). See the detailed C10 plan in `generator-implementation-plan/c10-collection-instrumentation-plan.md`.

---

## 3. How the generator works today

### 3.1 Pipeline

`SaturnGenerator` (`Saturn.Generator.Entities/SaturnGenerator.cs`):

1. `CreateSyntaxProvider` filters candidate classes with `Scanner.CanBeEntity` — must be a `partial class` **with a base list** (`Scanner.cs:29-32`).
2. Semantic filter `Scanner.IsEntity` — must inherit `GoLive.Saturn.Data.Entities.Entity` (`Scanner.cs:34-56`).
3. Transform to `ClassToGenerate` via `Scanner.ConvertToMapping` (`Scanner.cs:77-163`).
4. `.Collect()` then `Execute` emits one `{ClassName}.g.cs` per class (`SaturnGenerator.cs:60-77`).

`ClassToGenerate` carries `Members`, `ParentItemToGenerate`, limited-view inheritance flags, and `HasInitMethod`/`IsMultiscopedEntity`.

### 3.2 Member scanning

`Scanner.ConvertToMembers` (`Scanner.cs:308-461`) selects members and builds `MemberToGenerate`:

- Skips static, `readonly` fields, `const` fields, and read-only properties (get-only) (`Scanner.cs:312`).
- Properties that are **partial definitions** become `IsPartialProperty = true` and are renamed to `lowerCamelCase` (the generated backing field name). Non-partial properties are marked `UseOnlyForLimited = true` and emitted early (they are not re-declared and **not change-tracked**) (`Scanner.cs:335-352`).
- Recognizes `[DoNotTrackChanges]`, `[AddRefToScope]`, `[Readonly]`, `[WriteOnly]` (`Scanner.cs:366-383`).
- Detects a companion `{name}_runAfterSet` method and classifies its parameter as simple, ref-item, or string (`Scanner.cs:385-413`).
- Reads `[AddToLimitedView]`, `[ReadonlyInView]`, `[ExcludeFromLimitedView]` (`Scanner.cs:473-565`).
- `ObservableCollections.ObservableList<T>` is the only collection type recognized (`Scanner.cs:447-457`).

### 3.3 Emission

`SourceCodeGenerator.Generate` (`SourceCodeGenerator.cs:10-263`) emits:

- `partial class : INotifyPropertyChanged` plus `IUpdatableFrom<{Class}_{View}>`/`ICreatableFrom<...>` for two-way views.
- Backing fields for partial properties (`:40-43`).
- A constructor (only when there are collections or a user `_init`) that instantiates `ObservableList<T>` and subscribes to `CollectionChanged` (`:45-102`).
- One public property per trackable member, with `SetField` setter, a no-tracking setter for `[DoNotTrackChanges]`, or a scope-aware setter for `[AddRefToScope]` (`:104-114`, `:496-621`).
- `To_{View}()` methods, and `{Class}_{View}` classes with `UpdateFrom`, `Generate`, `Selector`, two-way `UpdateParent`, and `Create`.

### 3.4 Runtime types involved

- `Entity` (`GoLive.Saturn.Data.Entities/Entity.cs`):
  - `Id` setter normalizes to 24-hex via `TryParseId` and calls `SetField` (`:178-199`).
  - `Version`, `Changes`, `EnableChangeTracking`, `Properties` (`:206-216`).
  - `SetField<T>` compares, assigns, raises `PropertyChanged`, and records `Changes[propertyName] = newValue` when `EnableChangeTracking && Changes != null` (`:233-246`).
- `Ref<T>` (`Ref.cs`, `Ref.operators.cs`, `Ref.fetch.cs`): id/item pair, implicit conversions, `PropertyChanged`.
- `IUpdatableFrom<in T>` (`IUpdatableFrom.cs`), `ICreatableFrom<T>` (`Resources/ICreatableFrom.cs`, `static abstract`), `IUniquelyIdentifiable` (`IUniquelyIdentifiable.cs`).
- `ChangedEntity<T>` (`ChangedEntity.cs`) and `ChangeOperation` (`ChangeOperation.cs`) exist but are used only by Mongo's change-stream `Watch`, not by field-level tracking.

### 3.5 Repository patch surface

- `IDataUpdateDefinition<TItem>` is an empty marker (`IDataUpdateDefinition.cs`).
- `IRepository.Patch<TItem>(id, expectedVersion, jsonDocument, updateDefinition)` default implementation calls `JsonUpdate` when `jsonDocument` is present and throws for a custom `updateDefinition` (`IRepository.cs:79-98`).
- Mongo `Patch` combines `JsonUpdateDefinition<TItem>(jsonDocument)`, the provider update definition, and `Inc("_v",1)` (`MongoDbRepository.Repository.cs:277-329`).
- LiteDbX `Patch` is a read-modify-write merge (`LiteDbRepository.Repository.cs:589+`); Stellar similar; SQLite (proposed) applies `$set`/`$inc`/`$unset` server-side.

---

## 4. Assessment — Part 1: DTO generation and mapping

### 4.1 Strengths

- Zero-reflection, compile-time mapping: `UpdateFrom`/`Generate` are direct assignments.
- Server-side projection: `Selector` is a real expression usable by Mongo's `.Project(...)`.
- Two-way views make round-tripping UI edits practical.
- View inheritance/flattening and `[AddParentItemToLimitedView]` cover authorization-shaped payloads.
- `IUniquelyIdentifiable` + `_shortId` gives compact, URL-safe ids in views.

### 4.2 Defects and gaps (severity: C=critical, H=high, M=medium, L=low)

| # | Sev | Finding | Evidence | Impact |
| --- | --- | --- | --- | --- |
| D1 | N/A | No general DTO mode. Views exist only where `[AddToLimitedView]` appears. | `Scanner.cs:473-527`, `SourceCodeGenerator.cs:136-262` | **Not a defect.** Delivered as a Part 1 feature (Phase B): assembly-level default plus per-class opt-in/opt-out. |
| D2 | C | `implicit operator View(Ref<T>)` calls `UpdateFrom(source?.Item)`; `UpdateFrom` dereferences the source without a null check. | Generated `FourthItem.g.cs:161-165`, `:182-191`; `SourceCodeGenerator.cs:209-214` | `NullReferenceException` whenever a ref is not populated (the normal case after a plain load). |
| D3 | H | Collections are not mapped to view collections; a `List<Ref<T>>`/`ObservableList<Ref<T>>` view property stays `List<Ref<T>>`. | `FourthItem.g.cs:380`, `:394`, `:423` | DTOs leak `Ref<T>` instead of nested DTOs; no recursive mapping. |
| D4 | H | Property-level attributes are not copied to views: `AdditionalAttributes` is populated only when `member is not IPropertySymbol`. | `Scanner.cs:419-445` | `[Required]`, `[JsonIgnore]`, `[MaxLength]`, custom validation lost on the DTO. |
| D5 | M | Nullability is erased in generated views (`string` instead of `string?`). | `MainItem.g.cs:127`, `:188`; `SourceCodeGenerator.cs:227` | Nullable flow analysis and client contracts degrade. |
| D6 | M | `UpdateParent` for a ref-with-override-view writes only the id (`parent.Fifth = this.Fifth.Id`), silently discarding the populated item. | `FourthItem.g.cs:229-230`; `SourceCodeGenerator.cs:316-318` | Round-trip view→entity loses populated nested data. |
| D7 | M | `Selector` projects nested views by assigning a `Ref<T>` to a view property, relying on implicit conversion inside an expression tree. | `FourthItem.g.cs:220-221`; `SourceCodeGenerator.cs:404` | Mongo/LINQ translation may reject user-defined conversions; runtime `InvalidOperationException` risk. |
| D8 | N/A | Views have no `Id` unless `InheritFromIUniquelyIdentifiable`/parent-item config adds it, so `Create` cannot restore identity. | `SourceCodeGenerator.cs:127-131`, `:174-197` | **Won't fix (deliberate):** not all views need an `Id`. General DTOs from `[GenerateDto]` always include `Id` (`_shortId`). |
| D9 | M | `Create` sets `item.Id` only when the input is `IUniquelyIdentifiable`; `_shortId` (base64url) is accepted by `TryParseId`, but any other id shape is not. | `Entity.cs:18-33`; `SourceCodeGenerator.cs:122-132` | Identity round-trip depends on `_shortId` being used consistently. |
| D10 | L | Generated constructor is emitted when collections exist; a user-declared parameterless constructor collides (CS0111). | `SourceCodeGenerator.cs:45-47` | Compile failure for otherwise valid entities. |
| D11 | L | `Filename` is computed but unused; `Name`+`Namespace` key the output, so two same-named classes in different files/partials can collide. | `ClassToGenerate.cs:9,14`; `SaturnGenerator.cs:74` | Rare source-name collisions. |
| D12 | N/A | `ComputedExpression`/`ComputedSelectorExpression`/`Initializer` are injected verbatim. | `SourceCodeGenerator.cs:222, 275-300` | **Won't fix (deliberate):** attributes are source code; treated as trusted input. |
| D13 | M | `Populate` extensions are `async` without `await` and do no I/O; matching is `FirstOrDefault` over in-memory lists. | `Resources/PopulationExtensions.cs:11,28,45` | Misleading API; O(n²) population; CA/CS1998 warnings. |
| D14 | M | Flattened/inherited views and `ParentOnlyViewNames` are complex and untested in-repo; `new` shadowing can change resolution. | `SourceCodeGenerator.cs:142-146, 159-167, 385` | Fragile inheritance scenarios. |

### 4.3 Part 1 recommendation

1. Introduce `[GenerateDto]` (per-class opt-in) and an assembly-level `SaturnGenerateDtos` default with `[NoGenerateDto]` opt-out (decision 3); the DTO contains **all** eligible members (opt-out via `[ExcludeFromDto]`), with no authorization assumptions.
2. Keep limited views for authorization; unify both behind one emission model (`ViewDescriptor`) so mapping logic is written once.
3. Rewrite the mapping emitter to be **null-safe and recursive**, with explicit handling for: scalar, string, enum, `DateTime`, `HashedString`/`EncryptedString`, `Entity`-derived (embedded), `Ref<T>`/`WeakRef` (id + optional nested DTO), `IEnumerable<T>` and `ObservableList<T>`.
4. Copy member attributes (subject to an allow-list) and nullability annotations onto DTO properties.
5. Always generate an `Id` on DTOs (default `_shortId`, decision 4) and generate `Selector` only from shapes that translate (avoid implicit conversions in expressions; project `source.Fifth.Id` and map locally). Support `Ref<T>` as an id string by default and auto-expanding a nested DTO for display (decision 5).
6. Add golden-file tests over generated output (snapshot the `.g.cs`).

---

## 5. Assessment — Part 2: change tracking

### 5.1 What exists

- Scalar tracking in `Entity.SetField` recording `Changes[propertyName] = newValue` (`Entity.cs:240-243`).
- Generated setters call `SetField` for tracked members (`SourceCodeGenerator.cs:600`).
- `[DoNotTrackChanges]` generates a setter that only does equality + `OnPropertyChanged` (`SourceCodeGenerator.cs:464-469, 577-589`).
- Collection tracking via `ObservableList<T>.CollectionChanged` mutating `Changes` (`SourceCodeGenerator.cs:67-98`).
- `[AddRefToScope]` setter maintains `Scopes` on `MultiscopedEntity` (`SourceCodeGenerator.cs:537-574`).
- `_runAfterSet` hooks fire after `SetField` (`SourceCodeGenerator.cs:583-596, 606-620`).

### 5.2 Defects and gaps

| # | Sev | Finding | Evidence | Impact |
| --- | --- | --- | --- | --- |
| C1 | N/A | Change tracking is never consumed. No repository reads `Changes` or `EnableChangeTracking`. | grep: only `Entity.SetField`, generators, and provider *ignore* maps reference them | **Won't fix (out of scope):** consumption is a consumer responsibility. This repo provides the change set + update-document bridge. |
| C2 | C | Collections are subscribed only in the generated constructor. Assigning a new list (including deserialization/population) loses the subscription; the old list stays subscribed. | `SourceCodeGenerator.cs:55-99` vs setter `:512` | Loaded entities never track collection edits — the primary patch use case. |
| C3 | C | No child→parent propagation. Mutating a nested `Entity` or a `Ref<T>.Item` does not mark the parent dirty. | `Entity.SetField` has no parent link (`Entity.cs:233-246`) | Nested document edits are silently lost from the patch. |
| C4 | H | `Changes` stores only the new value; no operation kind or old value. | `Entity.cs:242` | Cannot emit `$unset`, cannot revert, cannot distinguish add/remove/replace, cannot compute diffs. |
| C5 | H | Keys are flat property names (or `Collection.{id}`), not JSON paths. | `Entity.cs:242`; `SourceCodeGenerator.cs:74-83` | Nested patches (`Customer.Name`, `Lines.3.Title`) are impossible. |
| C6 | H | Refs are stored as the whole `Ref<T>` object, which may carry a populated `Item`. | `Entity.cs:242` with `Ref<T>` values | Patch payloads bloat and can recurse/serialize unexpectedly; `$set` of a ref must be its id. |
| C7 | H | Collection keys use item identity (`Id`/`ToString`); duplicates collide, key mutation leaves stale entries, `Move` is ignored, `Reset` clears wholesale. | `SourceCodeGenerator.cs:74-94, 443-462` | Reordering/duplicates/keyed-object edits produce incorrect patches. |
| C8 | H | `Id` and `Version` changes are tracked like any other field. | `Entity.cs:197,209` + `SetField` | Patches can attempt to change identity/version; version is repository-managed. |
| C9 | H | No hydration guard. If `EnableChangeTracking` is true while loading/populating, deserialization itself pollutes the journal. | `Entity.cs:240`; no suppress scope anywhere | Patches contain the entire hydrated document. |
| C10 | H | Only `ObservableList<T>` from `ObservableCollections` is instrumented; `List<T>`, arrays, `HashSet<T>`, dictionaries are not. | `Scanner.cs:447-457`; `FourthItem.g.cs:84` (`List<Ref<FifthItem>> Roles` untracked) | Inconsistent coverage. **Detailed plan:** `generator-implementation-plan/c10-collection-instrumentation-plan.md`. |
| C11 | M | `WriteOnly`/`HashedString`/`EncryptedString` values are recorded verbatim into `Changes`. | `Entity.SetField`; `SourceCodeGenerator.cs:532` produces no getter for write-only but the field is still set | Sensitive data can leak into logs/patches/clients. |
| C12 | M | No `AcceptChanges`/`RejectChanges`/`HasChanges`; journal grows for the object's lifetime. | `Entity.Changes` never cleared automatically | Memory and correctness drift; no revert. |
| C13 | M | `Changes` is a plain `Dictionary<string,object>` (unordered); change ordering is not preserved. | `Entity.cs:212` | Ordered operations (array index mutations) cannot be sequenced reliably. |
| C14 | M | `_runAfterSet` string-parameter detection sets the wrong flag (`HasRunAfterSetMethodIsRefItem` instead of `HasRunAfterSetMethodIsString`), so string hooks are never invoked. | `Scanner.cs:408-411` vs `SourceCodeGenerator.cs:612-620` | Silent feature failure. |
| C15 | M | `EnableChangeTracking` defaults `false` and there is no enforcement/diagnostic. | `Entity.cs:214` | Easy to forget; tracking silently off. |
| C16 | L | `Changes`/`EnableChangeTracking` are `virtual` and would serialize if a provider forgets to ignore them. | `Entity.cs:212-214`; Mongo/LiteDbX/Stellar explicitly ignore | New providers can leak internal state. |
| C17 | L | The scoped `[AddRefToScope]` setter bypasses `_runAfterSet` handling. | `SourceCodeGenerator.cs:537-574` | Inconsistent hook behavior. |

### 5.3 Semantics required for a correct PATCH

To "send just the PATCH changes" the change set must answer:

1. **Which entity** (type + id) and **which version** it was loaded at.
2. **Which paths** changed (dotted JSON paths, array-aware).
3. **What operation** per path: set, unset, increment, list add/remove/replace/move/clear.
4. **Old and new values** (for revert, audit, concurrency conflict UX, and no-op elision).
5. **Visibility** (read-write / read-only / write-only / server-managed) for allow-listing.
6. **Whether tracking was active** and over what window (hydration excluded).

### 5.4 Collection coverage (finding C10)

Only `ObservableList<T>` is instrumented today. The full remediation — classification, baseline diff for plain collections, opt-in `TrackedList<T>`/`TrackedSet<T>`/`TrackedDictionary<TKey,TValue>` wrappers, per-kind diff algorithms, update-document output, and edge cases — is specified in `generator-implementation-plan/c10-collection-instrumentation-plan.md`.

---

## 6. Proposed design — Part 1: DTO generation and mapping

### 6.1 Unified descriptor model

Replace the ad-hoc `MemberToGenerate`→`SourceCodeGenerator` coupling with an explicit intermediate model:

```csharp
public sealed class ViewDescriptor {
    public string Name;                      // "Dto" or "View1"
    public bool IsDefaultDto;                // distinguishes [GenerateDto] from limited views
    public List<ViewMember> Members;
    public bool TwoWay;
    public bool GenerateChangeTracking;      // DTOs can track too
}

public sealed class ViewMember {
    public string EntityMember;              // "Name"
    public string ViewMember;                // "Name"
    public MemberCategory Category;          // Scalar | Ref | EmbeddedEntity | Collection
    public string? ElementViewType;          // for nested DTOs
    public bool ReadOnly;
    public bool WriteOnly;
    public bool Nullable;
    public IReadOnlyList<AttributeSpec> Attributes;
}
```

Both `[GenerateDto]` and `[AddToLimitedView]` produce `ViewDescriptor`s; one emitter handles both.

### 6.2 Default DTO (`[GenerateDto]`)

```csharp
[GenerateDto]                  // optional: Name = "...", IncludeProperties = false, TrackChanges = true
public partial class Order : Entity { ... }
```

Generated:

```csharp
public partial class OrderDto : INotifyPropertyChanged, IUpdatableFrom<Order>, ICreatableFrom<Order>, IUniquelyIdentifiable {
    public string Id { get; set; }
    public string? CustomerName { get; set; }
    public decimal Total { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();

    public static implicit operator OrderDto(Order source) => FromEntity(source);
    public static OrderDto FromEntity(Order? source) { ... }
    public static OrderDto? FromRef(Ref<Order>? source) => source?.Item is null ? null : FromEntity(source.Item);
    public Order ToEntity() => ...
    public static Expression<Func<Order, OrderDto>> Selector => ...;
    public void ApplyTo(Order target) { ... }
}
```

Rules:

- Include all public get/set members except `Changes`, `EnableChangeTracking`, `Properties` (opt-in), `[ExcludeFromDto]`, and get-only members.
- `Id` maps `_shortId` by default; option `UseFullId` keeps 24-hex.
- `Ref<T>` → `string? Id` by default; option `ExpandRefs` emits `TDto?` for members decorated `[Expand]`.
- Collections → `List<TElementDto>`; nullable-safe; never emit `Ref<T>` in a DTO unless explicitly requested.
- Embedded `Entity`-typed members → nested DTO.
- Copy nullability (`string?`) and an attribute allow-list (`[Required]`, `[StringLength]`, `[Range]`, `[JsonIgnore]`, `[JsonPropertyName]`, custom validation).
- Emit `INotifyPropertyChanged` and (optionally) change tracking on the DTO so client edits can also be turned into patches.

### 6.3 Mapping generation (both directions)

Generate direct assignments, no reflection:

```csharp
public static OrderDto FromEntity(Order source) {
    if (source is null) throw new ArgumentNullException(nameof(source));
    var dto = new OrderDto {
        Id = source._shortId,
        CustomerName = source.CustomerName,
        Total = source.Total,
        Lines = source.Lines is null ? new() : source.Lines.Select(OrderLineDto.FromEntity).ToList(),
    };
    return dto;
}

public void ApplyTo(Order target) {
    if (target is null) throw new ArgumentNullException(nameof(target));
    target.CustomerName = CustomerName;
    target.Total = Total;
    target.Lines = Lines is null ? new() : Lines.Select(line => line.ToEntity()).ToList();
}
```

Specific fixes implied:

- Ref conversion is null-safe (`source?.Item is null → null`, or id-only).
- No implicit conversion inside lambda expressions used as `Selector`; instead project ids/values that translate, and expand nested DTOs client-side after materialization, or emit a dedicated projection expression using `source.Fifth.Id` plus a second pass.
- `Selector` is only generated for translation-safe member categories; otherwise the DTO is materialized and mapped. A `Projectable` flag is computed at generation time.
- Collections map recursively.
- `Create` always preserves identity (DTO always has `Id`).

### 6.4 Naming and versioning

- Default DTO name: `{Entity}Dto`; overridable via `[GenerateDto(Name = "...")]`.
- Views unchanged: `{Entity}_{View}`.
- Emit a stable `[GeneratedCode("Saturn.Generator.Entities", version)]` attribute and an optional `DtoSchemaVersion` const to support contract evolution.
- Emit `// <auto-generated/>` header (the cascade generator already does; the entities generator currently does not).

---

## 7. Proposed design — Part 2: extensive change tracking

### 7.1 Change set data model (runtime)

Place in the new **`GoLive.Saturn.Data.ChangeTracking`** project (decision 1). It is referenced only by consumers that opt into tracking; `GoLive.Saturn.Data.Abstractions` does not depend on it (the `PatchChanges` bridge is an extension method in this project). The model is **non-generic** so it serves both entities and DTOs:

```csharp
public enum ChangeKind { Set, Unset, Increment, ListAdd, ListRemove, ListReplace, ListMove, ListClear }
public enum ChangeVisibility { ReadWrite, ReadOnly, WriteOnly, ServerManaged }
public enum CollectionStrategy { WholeArray, IndexedOps, SetOps }

public interface IChangeTracked {
    string Id { get; }
    long? Version { get; }
}

public sealed class FieldChange {
    public string Path { get; init; }                 // "Customer.Name", "Lines.3.Title"
    public ChangeKind Kind { get; init; }
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    public ChangeVisibility Visibility { get; init; }
    public int? Index { get; init; }                  // for list ops
    public CollectionStrategy Strategy { get; init; } // for list ops
}

public sealed class EntityChangeSet {
    public string EntityType { get; init; }
    public string Id { get; init; }
    public long? ExpectedVersion { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; }
    public IReadOnlyList<FieldChange> Fields { get; init; }
    public bool IsEmpty => Fields.Count == 0;
    public string ToUpdateDocument();                 // Mongo-style $set/$unset/$inc
}

public interface ITrackable {
    bool IsTracking { get; }
    bool HasChanges { get; }
    void BeginTracking(bool acceptCurrentState = true);
    void AcceptChanges();
    void RejectChanges();                             // uses captured old values
    EntityChangeSet GetChangeSet();
}
```

`Entity.Changes` (`Dictionary<string,object>`) is retained for back-compat and still populated with new values; the rich model is additive. `Changes` can be marked `[Obsolete]` later.

### 7.2 Tracking modes

- **Journal (default):** on each mutation, record `FieldChange`; capture `OldValue` lazily on the **first** mutation of a path (thin, no full snapshot). Supports patch building and coarse revert.
- **Baseline (opt-in):** `BeginTracking` snapshots eligible member values; `GetChangeSet` diffs current vs baseline. Supports true revert and elision (a field changed and changed back disappears). Higher memory for large graphs.
- **Journal + baseline:** recommended default for entities under edit; baseline for collections and embedded entities, journal for scalars.

Configuration via `[ChangeTracking(Mode = ...)]` on the entity, or `RepositoryOptions`/`EntityChangeSetOptions`.

### 7.3 Dependency-free base hooks + opt-in emission

`GoLive.Saturn.Data.Entities` must **not** reference `GoLive.Saturn.Data.ChangeTracking` (decision 1: not every consumer wants tracking). Therefore the base `Entity` only gains minimal, type-free hooks, and the generator emits the tracking implementation into the opted-in class, where the tracking assembly is available.

`Entity` gains:

```csharp
protected virtual void OnFieldChanged(string propertyName, object? oldValue, object? newValue) { }

internal object? TrackableParent { get; set; }
internal string? ParentPathSegment { get; set; }
```

`SetField` calls `OnFieldChanged(propertyName, oldValue, newValue)` after the existing `Changes` write. `[DoNotTrackChanges]` members keep using the no-`SetField` setter, so they never raise it.

The generator emits, only into opted-in classes (entity or DTO):

```csharp
private readonly global::GoLive.Saturn.Data.ChangeTracking.ChangeTracker tracker = new();

protected override void OnFieldChanged(string propertyName, object? oldValue, object? newValue)
{
    tracker.Record(this, propertyName, oldValue, newValue);
}

public bool IsTracking => tracker.IsTracking;
public bool HasChanges => tracker.HasChanges;
public void BeginTracking(bool acceptCurrentState = true) => tracker.Begin(this, acceptCurrentState);
public void AcceptChanges() => tracker.Accept(this);
public void RejectChanges() => tracker.Reject(this);
public global::GoLive.Saturn.Data.ChangeTracking.EntityChangeSet GetChangeSet() => tracker.Build(this);
public string ToUpdateDocument() => tracker.Build(this).ToUpdateDocument();

protected override void CaptureBaseline() => tracker.Capture(this);
protected override void RestoreBaseline() => tracker.Restore(this);
```

`ChangeTracker` (in `GoLive.Saturn.Data.ChangeTracking`) owns the journal, baseline, path composition (walking `TrackableParent`/`ParentPathSegment`), ref normalization, and strategy lookup. The generator emits a `StrategyFor`/`VisibilityFor` map as needed. DTOs get the same generated members but expose `Id`/`Version` so they can implement `IChangeTracked`.

This preserves the dependency direction: entities → no tracking dependency; opted-in classes → tracking assembly referenced by the consumer.

### 7.4 Paths and ref normalization

- `Id`, `Version`, `Changes`, `EnableChangeTracking` are **never** journaled (explicit exclude list in `SetField`/generator).
- `Ref<T>`/`WeakRef` members journal the **id string**, not the object (generator emits a normalize hook or `RecordChange` detects ref types and extracts `.Id`).
- Embedded `Entity` members set the child's `Parent` and `ParentPathSegment`, so a child's `SetField("Name")` records `Customer.Name` on the root journal.
- `Ref<T>.Item` is a *reference*: mutating it does not change the persisted document (providers store only the id), so by default it is **not** journaled. Add explicit `WrappedEntity<T>`/`[Embedded]` semantics for genuinely embedded sub-documents.
- Collections journal `List*` operations with indices; see §7.5.

### 7.5 Collections

Fix the lifecycle first: subscribe in the **setter** (unsubscribe from the old list, subscribe to the new), not only in the constructor. This makes loaded entities trackable.

```csharp
public ObservableList<Line> Lines {
    get => lines;
    set {
        if (ReferenceEquals(lines, value)) return;
        if (lines is not null) lines.CollectionChanged -= OnLinesChanged;
        SetField(ref lines, value);
        if (lines is not null) lines.CollectionChanged += OnLinesChanged;
    }
}
```

Record structured ops: `Add(index,item)`, `Remove(index,item)`, `Replace(index,old,new)`, `Move(from,to)`, `Clear`.

Serialization strategies, selectable globally or per member:

- **WholeArray (default, portable):** emit `$set: { "Lines": [ ...current array... ] }`. Works on every provider, no index math. Best when arrays are small or order is meaningful.
- **IndexedOps:** emit `$set: { "Lines.3.Title": ... }` / `$unset` for removals; apply removals in descending index order. Requires stable indices between capture and server apply (optimistic concurrency guards drift).
- **SetOps:** `$addToSet`/`$pull` for identity-keyed collections where order does not matter (Mongo-native; other providers must synthesize).

Plain collections (`List<T>`, arrays, `HashSet<T>`, `Dictionary<TKey,TValue>`) are detected by **baseline diff** by default (`WholeArray`), which captures in-place mutation without changing the property type. An opt-in `[CollectionTracking(Instrument = true)]` changes the emitted property type to a `TrackedList<T>`/`TrackedSet<T>`/`TrackedDictionary<TKey,TValue>` wrapper for event-driven fidelity. Full algorithm and emission steps: `generator-implementation-plan/c10-collection-instrumentation-plan.md` (finding C10).

### 7.6 Hydration guard

Tracking during hydration is the most common corruption source. Provide:

```csharp
using (entity.SuppressTracking()) { /* deserialize/populate */ }
```

and have providers call it around materialization/population. Because providers set properties via reflection/serializers, the cleanest approach is:

- Add `Entity.EnableChangeTracking` as `false` by default and never enable during hydration.
- Add `Entity.IsHydrating` set by the serializer/`UpdateFrom`/`Populate` paths; `SetField` records only when `!IsHydrating`.
- Provide `BeginTracking()` that clears the journal and establishes the baseline **after** load.

### 7.7 Canonical patch wire format

Target the existing `Patch(jsonDocument)` contract with a MongoDB-style update document (already parsed by Mongo's `JsonUpdateDefinition`):

```json
{
  "$set":    { "Customer.Name": "Ada", "Total": 42.5, "Lines": [ { "Title": "x", "Qty": 1 } ] },
  "$unset":  { "Notes": true },
  "$inc":    { "Count": 5 }
}
```

v1 restricts to `$set`/`$unset`/`$inc` and whole-array `$set` — portable across Mongo, LiteDbX, Stellar, and SQLite. `$addToSet`/`$pull`/`$push` are v2 for Mongo-native set semantics.

Example generated usage:

```csharp
order.BeginTracking();
order.CustomerName = "Ada";
order.Lines[0].Qty = 2;
var patch = order.ToUpdateDocument();          // {"$set":{"CustomerName":"Ada","Lines":[{"Title":"x","Qty":2}]}}
await repo.Patch<Order>(order.Id, order.Version, patch);
order.AcceptChanges();
```

### 7.8 Repository integration

- **v1 (no provider changes):** generate `ToUpdateDocument()`; call `IRepository.Patch<T>(id, expectedVersion, jsonDocument)`. Verify each provider:
  - Mongo: native via `JsonUpdateDefinition` + `Inc("_v",1)`.
  - LiteDbX: `$set`/`$unset`/`$inc` must be supported by its read-modify-write patch parser (verify/extend).
  - Stellar: same.
  - SQLite (proposed): `$set`/`$inc`/`$unset` already specified.
- **v2 (typed):** add `ChangeSetUpdateDefinition<T> : IDataUpdateDefinition<T>` carrying the change set; extend each provider's `Patch` to recognize it (Mongo maps to `JsonUpdateDefinition`; others apply). The `PatchChanges` entry point is an **extension method** in `GoLive.Saturn.Data.ChangeTracking` so `GoLive.Saturn.Data.Abstractions` does not depend on the tracking assembly:
  ```csharp
  public static Task PatchChanges<TEntity>(this IRepository repository, string id, long? expectedVersion, EntityChangeSet changes,
      IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
      where TEntity : Entity
      => repository.Patch<TEntity>(id, expectedVersion, changes.ToUpdateDocument(), null, transaction, cancellationToken);
  ```
  This avoids breaking existing providers and keeps the optional dependency graph intact (decision 1).
- **Concurrency:** always send `ExpectedVersion`; on `FailedToUpdateException`, reload and let the caller re-track.
- **Post-write:** `AcceptChanges()` and set `Version` from the repository result (or reload).

### 7.9 Security and correctness rules

- `ChangeVisibility`: `WriteOnly` fields may be recorded server-side but **must be filtered** before sending to a client; provide `IChangeSetFilter`/options (`IncludeWriteOnly = false` for client payloads).
- `ReadOnly` fields are excluded from patches.
- Never journal/log decrypted `EncryptedString`/`HashedString`; record the raw serialized form only.
- Refs normalized to ids to avoid embedding full objects.
- Path allow-list per DTO/view: generate `PatchableMembers` so a patch can never touch a field the view did not expose.
- Validate the entity type + id + version on the server before applying.

---

## 8. Interaction with existing subsystems

- **Mongo `Watch` / `ChangedEntity<T>` / `ChangeOperation`**: unrelated today (change streams). The new `EntityChangeSet` should not reuse these names to avoid confusion; consider renaming or documenting.
- **Cascade generator (`Saturn.Generator.Cascade`)**: a good template for emitting a static metadata block (`__Cascade`) with `// <auto-generated/>`, `#nullable disable`, fully-qualified names. Reuse that emission style for `__Patchable` metadata.
- **Change feed (`ChangeFeedBehavior`, `OutboxChangeFeedSink`)**: patches are a natural feed payload. `FeedPayloadMode` already supports full/partial; add a `Patch` mode carrying the update document.
- **SQLite provider**: its `Patch` design already accepts `$set`/`$inc`/`$unset`; no changes required for v1.

---

## 9. Alternatives considered

| Alternative | Why not |
| --- | --- |
| Keep `Dictionary<string,object>` and just serialize it as `$set` | No operations, no old values, no paths, no nesting, no revert; cannot express `$unset` or list ops. |
| Use RFC 6902 JSON Patch as the wire format | Mongo's `JsonUpdateDefinition` expects update operators, not JSON Patch; would require custom application in Mongo. |
| Matchbox/Mapster/AutoMapper for DTOs | Reflection/runtime cost; the project's ethos is generated, allocation-light, source-visible mapping. |
| Full change tracking via `INotifyPropertyChanged` interception only (no generated code) | Cannot know paths/types/visibility; generator already owns the properties. |
| Baseline-only (snapshot + deep diff) | Memory heavy for large graphs and cannot capture list add/remove intent. Hybrid is better. |
| Track everything including navigation `Ref.Item` | Refs are stored as ids; tracking nested item state produces patches with no persisted target. |
| Emit `ApplyTo` always (no `TwoWay`) for views | Breaks the existing authorization-oriented view contract; keep views as-is, add DTOs. |

---

## 10. Compatibility and migration

- **Runtime placement (decided):** change tracking runtime lives in a new project **`GoLive.Saturn.Data.ChangeTracking`** (`Saturn.Data.ChangeTracking/GoLive.Saturn.Data.ChangeTracking/`). It contains `IChangeTracked`, `ChangeKind`, `ChangeVisibility`, `CollectionStrategy`, `ChangeTrackingMode`, `FieldChange`, `EntityChangeSet` (non-generic), `ITrackable`, `IChangeSetFilter`/`VisibilityChangeSetFilter`, `UpdateDocumentBuilder`, `IChangeTrackingObserver`, and the opt-in collection wrappers (`TrackedList<T>`, `TrackedSet<T>`, `TrackedDictionary<TKey,TValue>`). The `PatchChanges` bridge is an **extension method** in this project so `GoLive.Saturn.Data.Abstractions` does not depend on it.
- **Attributes:**
  - `Saturn.Generator.Entities.Resources` keeps DTO/view attributes and adds `[GenerateDto]`, `[NoGenerateDto]`, `[ExcludeFromDto]`, `[Embedded]`.
  - `GoLive.Saturn.Data.ChangeTracking` owns `[ChangeTracking]`, `[NoChangeTracking]`, `[CollectionTracking]` (they reference its enums), so consumers who do not want tracking never reference it. The generator matches these by metadata name, so it has no compile dependency on the tracking assembly.
- `Entity.Changes`/`EnableChangeTracking` remain; the rich journal is additive. Mark `Changes` obsolete in a later major.
- Keep generated view names and method shapes; change only their bodies to be null-safe/recursive. Existing `To_View1`/`UpdateFrom`/`Selector` signatures must not change.
- **Dependencies (decided):** replace all intra-repo `<PackageReference>`s with `<ProjectReference>`s. Specifically `Saturn.Generator.Entities.Resources` must project-reference `GoLive.Saturn.Data.Entities` instead of pinning NuGet `3.4.28`. This removes the version drift (assessment F/§10). Third-party packages (`Microsoft.CodeAnalysis.CSharp`, `ObservableCollections`, `xunit`, etc.) remain NuGet.
- The `ObservableCollections` runtime dependency must remain documented/flowed to consumers (see F1 below). Consumers that track plain collections can opt out of `ObservableCollections` by using baseline diff only.

---

## 11. Testing strategy

- **Generator golden tests**: snapshot `.g.cs` output for a fixture matrix (scalars, partial vs non-partial, fields, `[DoNotTrackChanges]`, `[Readonly]`, `[WriteOnly]`, `[AddRefToScope]`, all collection kinds, nested/embedded, refs, views, inheritance/flatten, `_runAfterSet` variants, string/ref/string-item hooks).
- **Mapping tests**: entity→DTO→entity round-trip equality; null refs; null collections; unpopulated refs; populated refs; encrypted/hashed fields; enums; `DateTime`; identity (`Id`/`_shortId`).
- **Tracking tests**: mutate scalar, set twice, revert to original, nested embedded child, change ref id, list add/remove/replace/move/clear, assign a new list after load, hydration does not journal, `AcceptChanges`/`RejectChanges`, `HasChanges`.
- **Collection instrumentation tests (C10)**: in-place mutation of `List<T>`, arrays, `HashSet<T>` (order change is not a change; add/remove under `SetOps`), and dictionaries (per-key add/update/remove, dot-in-key fallback); opt-in `TrackedList<T>`/`TrackedSet<T>`/`TrackedDictionary<TKey,TValue>` events; large-collection baseline guard. Full matrix in the C10 plan.
- **Dependency tests**: a consumer project that references only `GoLive.Saturn.Data.Entities` compiles with tracking disabled; a consumer that opts in adds a `ProjectReference` to `GoLive.Saturn.Data.ChangeTracking`.
- **Patch tests**: generated update document snapshot; apply via Mongo/LiteDbX/Stellar/SQLite; optimistic concurrency mismatch; version bump; write-only filtering.
- **Analyzer tests**: verify diagnostics for non-partial properties/fields and code-fix behavior.
- **Incremental-generator tests**: cacheability (no regeneration when unrelated code changes), determinism, source-output stability (use `GeneratorDriver` with tracked steps).

---

## 12. Phased implementation plan

Each phase is independently shippable and testable.

### Phase A — Stabilize the existing generator (bug fixes, no new features)
- Fix `implicit operator View(Ref<T>)` null dereference (D2).
- Fix string `_runAfterSet` flag (C14).
- Copy property attributes to views (D4).
- Preserve nullability (D5).
- Add `// <auto-generated/>` header.
- Snapshot tests to lock current output.

### Phase B — DTO generation (Part 1)
- Add `[GenerateDto]`, `[NoGenerateDto]`, `[ExcludeFromDto]`, `[Embedded]`.
- Assembly-level default (MSBuild property) plus per-class opt-in/opt-out (decision 3).
- Unified `ViewDescriptor` model + emitter.
- Recursive, null-safe mapping (refs, embedded, collections), identity preservation with `_shortId` default (decision 4).
- Ref handling: id string by default with an **auto-expand** option (decision 5).
- DTO-level change tracking generated (decision 6).
- Null-safe `Selector` (no implicit conversions in expression trees) and `Projectable` analysis.
- Tests + docs.

### Phase B2 (C10) — Collection instrumentation
- Classify `ObservableList<T>`, `List<T>`/`IList<T>`, arrays, `HashSet<T>`/`ISet<T>`, `Dictionary<TKey,TValue>`.
- Baseline-based detection for plain collections (`WholeArray`, default) so in-place mutation is captured.
- Opt-in `TrackedList<T>`/`TrackedSet<T>`/`TrackedDictionary<TKey,TValue>` wrappers for event-driven fidelity.
- Default `WholeArray`, `IndexedOps`/`SetOps` opt-in (decision 2).
- **Detailed plan:** `generator-implementation-plan/c10-collection-instrumentation-plan.md`.

### Phase C — Rich change tracking (Part 2a)
- Create `GoLive.Saturn.Data.ChangeTracking` (decision 1) and add it to `Saturn.Data.slnx`.
- Add `IChangeTracked`, `EntityChangeSet` (non-generic), `FieldChange`, `ITrackable`, filters, `UpdateDocumentBuilder`.
- `RecordChange`, parent links, path composition, ref normalization, exclude list (`Id`/`Version`).
- Generator emits tracking for opted-in entities **and** DTOs.
- Hydration guard and provider integration.
- Tests.

### Phase D — Collections and nesting (Part 2b)
- Fix collection subscription on setter; unsubscribe old.
- Structured list ops and strategies (`WholeArray` default).
- Embedded entity propagation and `[Embedded]` semantics.
- Wire the C10 wrappers/baseline detection.
- Tests.

### Phase E — Patch bridge and providers (Part 2c)
- Generate/extend `ToUpdateDocument()` with `$set`/`$unset`/`$inc`.
- Verify and repair LiteDbX, Stellar, and SQLite patch support for those operators (decision 7).
- Add `ChangeSetUpdateDefinition<T>` and a `PatchChanges` **extension method** in `GoLive.Saturn.Data.ChangeTracking` (keeps Abstractions clean).
- Change-feed `Patch` payload mode.
- End-to-end tests across all providers.

### Phase F — Baseline mode, security filters, analytics
- Baseline diff mode (also underpins C10 plain collections), `IChangeSetFilter`, per-view patchable allow-lists.
- Telemetry: patch size, field counts, conflict rate.

---

## 13. Acceptance criteria

- Generated DTOs exist for `[GenerateDto]` entities (and by assembly default with `[NoGenerateDto]` opt-out) and round-trip all supported member categories without reflection.
- DTO identity uses `_shortId`; refs are id-by-default with an auto-expand option.
- Both entities and DTOs track changes.
- Mutating a loaded entity — including in-place mutation of any supported collection type (`ObservableList<T>`, `List<T>`, arrays, `HashSet<T>`, dictionaries) — produces a correct, minimal update document.
- `repo.PatchChanges<T>(id, version, entity.GetChangeSet())` succeeds on Mongo, StellarDB, LiteDbX, and SQLite; version increments; concurrent mismatch throws.
- `Id`/`Version`/server-managed fields never appear in a patch.
- `WriteOnly`/encrypted fields never appear in a client-facing patch unless explicitly opted in.
- Hydration does not populate the journal.
- Existing generated view API is unchanged (only behavior hardened).
- `GoLive.Saturn.Data.Entities` and `GoLive.Saturn.Data.Abstractions` do not reference `GoLive.Saturn.Data.ChangeTracking`.
- All intra-repo dependencies are `ProjectReference`; no Saturn NuGet pins remain.
- Golden-file and cross-provider tests pass in CI.

---

## 14. Resolved decisions

| # | Decision | Implementation impact |
| --- | --- | --- |
| 1 | New `GoLive.Saturn.Data.ChangeTracking` project; not all entities need tracking. | Runtime types + `PatchChanges` extension live there; generator emission is opt-in; Abstractions stays clean. |
| 2 | Default collection strategy `WholeArray`. | `[CollectionTracking]` defaults to `WholeArray`; `IndexedOps`/`SetOps` are opt-in. |
| 3 | Both assembly-level default and per-class opt-in/opt-out. | MSBuild properties (`SaturnGenerateDtos`, `SaturnChangeTracking`) read via `AnalyzerConfigOptionsProvider.GlobalOptions`; `[GenerateDto]`/`[NoGenerateDto]`, `[ChangeTracking]`/`[NoChangeTracking]`. |
| 4 | DTO `Id` uses `_shortId` by default. | `[GenerateDto(UseFullId = true)]` opts into 24-hex. |
| 5 | `Ref<T>` id-by-default **and** auto-expand option. | `[GenerateDto(ExpandRefs = true)]` or `[Embedded]` per member expands a nested DTO for display. |
| 6 | Both DTOs and entities track changes. | Tracking emission targets both; `IChangeTracked` (Id/Version) implemented by entities and DTOs via generated partials. |
| 7 | MongoDB-style update document, verified on Mongo, StellarDB, LiteDbX, SQLite. | Canonical `{ "$set", "$unset", "$inc" }`; Phase E verifies/repairs each provider; `$addToSet`/`$pull` are Mongo-only v2. |
| 8 | Intra-repo dependencies use `ProjectReference`, not NuGet. | Replace `Saturn.Generator.Entities.Resources` → `GoLive.Saturn.Data.Entities` NuGet pin with a project reference; add the new project(s) to `Saturn.Data.slnx`. |
| 9 | Both `ObservableList<T>` and plain-collection support. | `ObservableList<T>` for event-driven fidelity; plain collections via baseline diff; opt-in `TrackedList<T>`/`TrackedDictionary` wrappers. |

---

## 14a. Detailed C10 plan

The collection instrumentation work is specified separately in `generator-implementation-plan/c10-collection-instrumentation-plan.md`.

---

## 15. Appendix A — Findings index (quick reference)

| Severity | IDs |
| --- | --- |
| Critical | C2, C3, D2 |
| High | C4, C5, C6, C7, C8, C9, C10, D3, D4 |
| Medium | C11–C15, D5, D6, D7, D9, D13, D14 |
| Low | C16, C17, D10, D11 |
| Won't fix (deliberate / out of scope) | C1, D1 (feature), D8, D12 |
| Hygiene | F1–F5 |

C10 has a dedicated plan: `generator-implementation-plan/c10-collection-instrumentation-plan.md`.

Additional generator hygiene finding:

- **F1 (M):** `ObservableCollections` is a `PackageReference` of the generator and a runtime dependency of emitted code, but the package flow to consumers is fragile (`Saturn.Generator.Entities.csproj:51` plus the `AddPackDependencies` target `:58-76`). Consumers must also reference `GoLive.Saturn.Generator.Entities.Resources` and `ObservableCollections`. Document and test a clean consumer project.
- **F2 (M):** The generator project references `Microsoft.CodeAnalysis.CSharp.Workspaces 5.9.0` (`:50`) while analyzers normally reference only `Microsoft.CodeAnalysis.CSharp`; `Workspaces` couples the generator to the IDE layer and risks version conflicts. The cascade generator correctly references only `Microsoft.CodeAnalysis.CSharp` (`Saturn.Generator.Cascade.csproj:37`). Split analyzer and code-fix assemblies, or drop `Workspaces` if the code fixes are packaged separately.
- **F3 (M):** `.Collect()` (`SaturnGenerator.cs:60`) discards incrementality: any edit re-runs emission for all entities. Prefer `ForAttributeWithMetadataName` and per-class `RegisterSourceOutput`, or at least keep per-class models separately instead of collecting.
- **F4 (L):** `Scanner.ConvertToMapping` dereferences `input.symbol.Locations.FirstOrDefault(...).SourceTree.FilePath` without a null guard (`Scanner.cs:81`).
- **F5 (L):** `getFirstGenericParameter` is unused (`Scanner.cs:468-471`).

## 16. Appendix B — Attribute matrix

| Attribute | Target | Emitted effect today | Part 1/2 change |
| --- | --- | --- | --- |
| `AddToLimitedView` | field/property | adds member to `{Class}_{View}` | unchanged; unified emitter |
| `ExcludeFromLimitedView` | field/property | removes member from view(s) | add `ExcludeFromDto` |
| `ReadonlyInView` | field/property | view property getter-only | unchanged |
| `ReadOnly` | field | entity property getter-only | exclude from patch |
| `WriteOnly` | field | entity property setter-only | mark `ChangeVisibility.WriteOnly` |
| `DoNotTrackChanges` | field/property | setter skips `SetField` | exclude from journal |
| `AddRefToScope` | field/property | scope-aware setter | keep; ensure `_runAfterSet` fires |
| `AddParentItemToLimitedView` | class | adds parent fields to view | unchanged |
| `AddParentItemsLimitedViews` | class | inherit/flatten parent views | extend to DTOs if desired |
| `GenerateDto` (new, Resources) | class | — | opt-in: emits `{Entity}Dto` + mapping |
| `NoGenerateDto` (new, Resources) | class | — | opt-out when the assembly default enables DTOs |
| `ExcludeFromDto` (new, Resources) | field/property | — | removes a member from the generated DTO |
| `Embedded` (new, Resources) | field/property | — | maps/journals nested entity paths; auto-expand in DTO |
| `ChangeTracking` (new, ChangeTracking) | class | — | opt-in: journal / baseline / hybrid; also assembly default via MSBuild |
| `NoChangeTracking` (new, ChangeTracking) | class | — | opt-out when the assembly default enables tracking |
| `CollectionTracking` (new, ChangeTracking) | field/property | — | selects collection strategy (`WholeArray` default) and wrapper instrumentation |

Attribute placement: DTO/view attributes stay in `Saturn.Generator.Entities.Resources`; tracking attributes live in `GoLive.Saturn.Data.ChangeTracking` so consumers who do not want tracking never reference it. The generator matches tracking attributes by metadata name and has no compile dependency on the tracking assembly.

## 17. Appendix C — Key files

| Concern | File |
| --- | --- |
| Generator entry / diagnostics | `Saturn.Generator.Entities/SaturnGenerator.cs` |
| Scanning / model building | `Saturn.Generator.Entities/Scanner.cs` |
| Model types | `ClassToGenerate.cs`, `MemberToGenerate.cs`, `LimitedViewToGenerate.cs`, `LimitedViewParentItemToGenerate.cs`, `MemberAttribute.cs` |
| Emitter | `SourceCodeGenerator.cs`, `SourceStringBuilder.cs` |
| Analyzers/code fixes | `CodeFixes/ChangeTrackingAnalyzer.cs`, `FieldToPartialPropertyAnalyzer.cs`, `RunAfterSetAnalyzer.cs`, `*CodeFixProvider.cs` |
| Runtime attributes/extensions | `Saturn.Generator.Entities.Resources/*.cs` |
| Entity runtime tracking | `GoLive.Saturn.Data.Entities/Entity.cs` |
| Ref runtime | `Ref.cs`, `Ref.operators.cs`, `Ref.fetch.cs` |
| Patch contract | `GoLive.Saturn.Data.Abstractions/IRepository.cs`, `IDataUpdateDefinition.cs`, `RepositoryWriteContext.cs` |
| Provider patch impls | `MongoDbRepository.Repository.cs`, `MongoDataUpdateDefinition.cs`; `LiteDbRepository.Repository.cs`, `LiteDbDataUpdateDefinition.cs`; `StellarRepository.Repository.cs`, `StellarDataUpdateDefinition.cs`; (SQLite proposal) |
| Cascade emission template | `Saturn.Generator.Cascade/CascadeEmitter.cs` |
| Generated samples (evidence) | `Saturn.Generator.Entities.Playground/obj/Generated/.../*.g.cs` |
| Change-tracking runtime (new) | `Saturn.Data.ChangeTracking/GoLive.Saturn.Data.ChangeTracking/` |
| Change-tracking attributes (new) | `.../ChangeTracking/Attributes/` (`ChangeTrackingAttribute`, `NoChangeTrackingAttribute`, `CollectionTrackingAttribute`) |
| Change-tracking collection wrappers (new) | `.../Collections/TrackedList.cs`, `TrackedSet.cs`, `TrackedDictionary.cs` |
| Update-document renderer (new) | `.../UpdateDocumentBuilder.cs` |
| Patch bridge extension (new) | `.../RepositoryPatchExtensions.cs` |
| C10 detailed plan | `docs/generator-implementation-plan/c10-collection-instrumentation-plan.md` |
