# C10 — Collection Instrumentation (Detailed Plan)

**Finding C10:** Only `ObservableCollections.ObservableList<T>` is instrumented. `List<T>`, arrays, `HashSet<T>`, and dictionaries are not tracked, so in-place mutations of those members are lost from patches.

**Goal:** Track every supported collection type with correct, minimal update documents, without forcing a hard dependency on `ObservableCollections`.

**Prerequisite:** Phase C complete (`GoLive.Saturn.Data.ChangeTracking`, `ITrackable`, `ChangeTracker`, base `OnFieldChanged` hook). Phase D's subscribe-on-set fix is a prerequisite for the observable path.

**Exit criteria:**
- `List<T>`, `IList<T>`, `T[]`, `HashSet<T>`/`ISet<T>`, and `Dictionary<TKey,TValue>` mutations are detected and appear in `ToUpdateDocument()`.
- Default strategy `WholeArray` (decision 2); `IndexedOps`/`SetOps` available where they are correct.
- Works on entities that were loaded (not just constructed) and had `BeginTracking()` called.
- `Ref<T>`/`Entity` elements normalize to ids/values correctly.
- No `ObservableCollections` dependency is required for plain-collection tracking.

---

## 1. Why plain collections need a different mechanism

`ObservableList<T>` raises `CollectionChanged`, so mutations can be journaled as they happen. A raw `List<T>`, array, `HashSet<T>`, or dictionary has no notification hook. Once a caller holds the collection instance, in-place mutations cannot be intercepted without:

1. **Wrapping** the collection in an observing type (changes the member's runtime type), or
2. **Diffing** the collection against a captured baseline at patch time (no type change; requires a baseline).

Decision 9 requires **both**: keep `ObservableList<T>` for event-driven fidelity, and support plain collections via baseline diff with an opt-in wrapper.

---

## 2. Collection classification

Extend `Scanner.ConvertToMembers` to classify every member whose type implements `IEnumerable` (excluding `string`, `byte[]` treated as a scalar blob, and `Entity` graphs):

| Member type | `CollectionKind` | Default strategy | Observable? |
| --- | --- | --- | --- |
| `ObservableCollections.ObservableList<T>` | `ObservableList` | `WholeArray` | yes |
| `List<T>`, `IList<T>`, `IReadOnlyList<T>`, `Collection<T>` | `List` | `WholeArray` | no |
| `T[]` | `Array` | `WholeArray` | no |
| `HashSet<T>`, `ISet<T>` | `Set` | `WholeArray` | no |
| `Dictionary<TKey,TValue>`, `IDictionary<,>`, `IReadOnlyDictionary<,>` | `Dictionary` | `WholeArray` | no |
| any other `IEnumerable<T>` | `Other` | `WholeArray` | no |

Add to `MemberToGenerate`:

```csharp
public CollectionKind CollectionKind { get; set; }

public string ElementTypeName { get; set; }

public string KeyTypeName { get; set; }

public string ValueTypeName { get; set; }

public CollectionStrategy Strategy { get; set; } = CollectionStrategy.WholeArray;

public bool Instrument { get; set; }
```

`Strategy`/`Instrument` come from `[CollectionTracking]` (in `GoLive.Saturn.Data.ChangeTracking`), matched by metadata name; defaults `WholeArray`/`false`.

`byte[]` is treated as a scalar (whole-value `$set`), not a collection element list.

---

## 3. Detection by baseline diff (default, no type change)

### 3.1 Capture

Plain-collection members are **always** baselined, regardless of `[ChangeTracking(Mode = ...)]`, because baseline diff is the only detection mechanism:

```csharp
protected override void CaptureBaseline()
{
    tracker.CaptureValue("Tags", Tags is null ? null : new List<string>(Tags));
    tracker.CaptureValue("Scores", Scores is null ? null : new Dictionary<string, int>(Scores));
    tracker.CaptureValue("Roles", Roles is null ? null : Roles.ToArray());
}
```

Rules:

- Copy the collection, never store a live reference.
- `List<T>`/`IList<T>` → `new List<T>(collection)`.
- `T[]` → `(T[])collection.Clone()`.
- `HashSet<T>`/`ISet<T>` → `new HashSet<T>(collection)` (preserve comparer if available via `HashSet<T>.Comparer`; otherwise default).
- `Dictionary<TKey,TValue>` → `new Dictionary<TKey,TValue>(collection)` (preserve comparer when the source is a `Dictionary<,>`).
- Null stays null (null vs empty must remain distinguishable: `null` means "not set", empty means "set to empty").

### 3.2 Diff

Generate a diff per kind. `ChangeTracker` provides the helpers; the generated class supplies member reads.

**List / Array** (order-sensitive):

```csharp
var baseline = (List<string>)tracker.BaselineValue("Tags");
var current = Tags is null ? null : new List<string>(Tags);

if (!CollectionDiff.ListEqual(baseline, current, StringComparer.Ordinal))
{
    changes.Add(new FieldChange
    {
        Path = "Tags",
        Kind = changeKindForCollection,
        OldValue = baseline,
        NewValue = current,
        Strategy = CollectionStrategy.WholeArray
    });
}
```

- Default `WholeArray` → one `Set` of the entire array.
- With `[CollectionTracking(Instrument = true)]` → `IndexedOps` diffs are produced from the wrapper's events instead (see §5).

**Set** (order-insensitive):

```csharp
var baseline = (HashSet<string>)tracker.BaselineValue("Tags");
var current = Tags is null ? null : new HashSet<string>(Tags);

var added = current.Except(baseline).ToList();
var removed = baseline.Except(current).ToList();

if (added.Count > 0 || removed.Count > 0)
{
    if (strategy == CollectionStrategy.SetOps)
    {
        foreach (var item in added) changes.Add(new FieldChange { Path = "Tags", Kind = ChangeKind.ListAdd, NewValue = item, Strategy = CollectionStrategy.SetOps });
        foreach (var item in removed) changes.Add(new FieldChange { Path = "Tags", Kind = ChangeKind.ListRemove, OldValue = item, Strategy = CollectionStrategy.SetOps });
    }
    else
    {
        changes.Add(new FieldChange { Path = "Tags", Kind = ChangeKind.Set, OldValue = baseline, NewValue = current, Strategy = CollectionStrategy.WholeArray });
    }
}
```

**Dictionary** (key-based):

```csharp
var baseline = (Dictionary<string, int>)tracker.BaselineValue("Scores");
var current = Scores is null ? null : new Dictionary<string, int>(Scores);

foreach (var key in current.Keys.Union(baseline.Keys))
{
    var had = baseline.TryGetValue(key, out var oldValue);
    var has = current.TryGetValue(key, out var newValue);

    if (had && !has)
    {
        changes.Add(new FieldChange { Path = "Scores." + key, Kind = ChangeKind.Unset, OldValue = oldValue });
    }
    else if (!had && has)
    {
        changes.Add(new FieldChange { Path = "Scores." + key, Kind = ChangeKind.Set, NewValue = newValue });
    }
    else if (!EqualityComparer<int>.Default.Equals(oldValue, newValue))
    {
        changes.Add(new FieldChange { Path = "Scores." + key, Kind = ChangeKind.Set, OldValue = oldValue, NewValue = newValue });
    }
}
```

- Default `WholeArray` may instead emit a single `$set` of the whole dictionary when `DictionaryPathOps` is off. Recommended default: **per-key paths** for dictionaries (avoids clobbering concurrent edits) with a fallback whole-object `$set` when keys contain dots (JSON path ambiguity).
- Keys containing `.` cannot be represented as a Mongo dotted path; emit the whole dictionary `$set` in that case and document the limitation. Optionally support escaping later.

### 3.3 Element equality

Add `CollectionDiff` to the tracking project:

```csharp
public static class CollectionDiff
{
    public static bool ListEqual<T>(IList<T> left, IList<T> right, IEqualityComparer<T> comparer)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Count != right.Count) return false;

        for (var index = 0; index < left.Count; index++)
        {
            if (!comparer.Equals(left[index], right[index])) return false;
        }

        return true;
    }
}
```

Element comparer selection at generation time:

- Scalar/string/enum/`DateTime`/`decimal`/`Guid` → `EqualityComparer<T>.Default`.
- `Ref<T>`/`WeakRef` → compare `RefId` (a `RefEqualityComparer<T>` in the tracking project).
- `Entity` element → compare `Id` (`EntityIdEqualityComparer<T>`).
- Sub-entity/value objects → default record/struct equality; document that reference-type value objects must implement equality for change detection.

### 3.4 Merging with the journal

`GetChangeSet` merges:

1. Journal entries (scalars and observable-collection events).
2. Baseline diffs for plain-collection members.

If the same path appears in both (e.g., an `ObservableList` was also baselined — it should not be), the baseline diff wins and the journal entries for that path are dropped. Ensure plain-collection members are **not** also emitted as `SetField`-journaled scalars by the observable path.

`ChangeTracker.Build`:

```csharp
var fields = new List<FieldChange>();
fields.AddRange(journal.Where(change => !plainCollectionPaths.Contains(RootPath(change.Path))));
fields.AddRange(owner.ComputeCollectionBaselineDiff());
```

Where `ComputeCollectionBaselineDiff()` is generated and returns only the plain-collection diffs.

---

## 4. Null, empty, and ordering semantics

- `null → empty` and `empty → null` are changes (emit `$set` with the new value).
- Reordering a `List<T>` is a change under `WholeArray` (arrays differ by position).
- Reordering a `HashSet<T>` is never a change (set equality).
- `Dictionary` key order is irrelevant.
- Duplicate elements in `List<T>` are preserved and diffed positionally.
- `byte[]` is a scalar; a changed blob emits `$set` of the base64/JSON representation (provider-dependent serialization already handles this).

---

## 5. Opt-in instrumented wrappers (event-driven, no `ObservableCollections`)

Provide wrappers in `GoLive.Saturn.Data.ChangeTracking\Collections\`:

### 5.1 `TrackedList<T>`

```csharp
public sealed class TrackedList<T> : IList<T>, IReadOnlyList<T>
{
    private readonly List<T> inner;
    private readonly Action<TrackedCollectionChange<T>> onChanged;

    public TrackedList(Action<TrackedCollectionChange<T>> onChanged) { ... }
    public TrackedList(IEnumerable<T> items, Action<TrackedCollectionChange<T>> onChanged) { ... }

    // IList<T> members forward to inner and raise onChanged with
    // (ChangeKind.ListAdd/ListRemove/ListReplace/ListMove/ListClear, index, oldValue, newValue).
}
```

### 5.2 `TrackedSet<T>`

```csharp
public sealed class TrackedSet<T> : ISet<T>, IReadOnlyCollection<T>
```

Raises `ListAdd` / `ListRemove` (mapped to `$addToSet` / `$pull` under `SetOps`).

### 5.3 `TrackedDictionary<TKey,TValue>`

```csharp
public sealed class TrackedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
```

Raises `Set` / `Unset` with path `Member.key`.

### 5.4 Contract

```csharp
public readonly struct TrackedCollectionChange<T>
{
    public ChangeKind Kind { get; init; }
    public int Index { get; init; }
    public T OldValue { get; init; }
    public T NewValue { get; init; }
}
```

Wrappers accept a `string memberName` or an `Action` bound by the generated code to the tracker, so paths and visibility/strategy are applied consistently.

### 5.5 Emission when `Instrument = true`

- `List<T>`/`IList<T>` member → emitted property type becomes `TrackedList<T>` (implements `IList<T>`, so most call sites keep working; explicit `List<T>` declarations require a change).
- `T[]` member → emitted type becomes `TrackedList<T>`; `ToUpdateDocument` still emits a JSON array; baseline/restore converts to/from `T[]`.
- `HashSet<T>`/`ISet<T>` → `TrackedSet<T>`.
- `Dictionary<TKey,TValue>`/`IDictionary<,>` → `TrackedDictionary<TKey,TValue>`.

`Instrument = false` (default) keeps the original property type and uses §3 baseline diff.

**Both modes coexist** (decision 9): `ObservableList<T>` members are event-driven via `CollectionChanged`; plain members are baseline-diffed; instrumented members are event-driven via wrappers. Baseline/restore is emitted only for members that need it.

---

## 6. Update-document output per kind

| Kind | `WholeArray` (default) | `IndexedOps` (Instrument only) | `SetOps` |
| --- | --- | --- | --- |
| `List<T>` / `T[]` | `$set: { "M": [ ... ] }` | `$set: { "M.3": v }`, `$unset: { "M.2": true }` (descending) | n/a |
| `HashSet<T>` | `$set: { "M": [ ... ] }` | n/a | `$addToSet`/`$pull` |
| `Dictionary` | per-key `$set`/`$unset` (default) or whole-object `$set` | n/a | n/a |
| `ObservableList<T>` | `$set: { "M": [ ... ] }` | `$set`/`$unset` by index | `$addToSet`/`$pull` |

Provider applicability (Phase E): Mongo supports all; StellarDB/LiteDbX/SQLite support `$set`/`$unset`/`$inc` and whole-array `$set` in v1. `$addToSet`/`$pull` are Mongo-only v1; for other providers, `SetOps` falls back to `WholeArray` unless the provider is extended.

---

## 7. Nested elements and refs

- `Ref<T>`/`WeakRef` elements normalize to their id on capture and diff; an element whose id changed is a replace.
- `Entity` elements diff by `Id`; if the same-id element's fields changed, the element is treated as replaced for `WholeArray` (whole-array `$set`). For `IndexedOps`, emit `M.<index>.<field>` paths.
- Embedded elements set `ChangeTrackingParent`/`ChangeTrackingPathSegment` when added (Phase D.5), so a field-level mutation inside an element records `M.<index>.<field>`.
- For plain collections, element parent wiring happens at `CaptureBaseline` and diff time, not at mutation time (no events). Nested element mutations are therefore captured by the collection diff as a whole-element change; document this and recommend `Instrument = true` or `ObservableList<T>` when per-element paths are required.

---

## 8. Performance and allocation

- Baseline copies cost one extra collection per tracked member per `BeginTracking`/`AcceptChanges`. For large collections, this doubles transient memory.
- Provide `[CollectionTracking(Track = false)]` (a member opt-out) and `ChangeTrackingOptions.MaxBaselineElements` (int?) to skip baselining collections above a threshold; skipped collections then rely on replacement (`SetField`) only and log a warning through the observer.
- `WholeArray` diff is O(n); dictionary/set diff is O(n) with hash lookups.
- Instrumented wrappers add one delegate call per mutation and no baseline copy.
- Avoid LINQ in generated diffs; emit explicit loops in `CollectionDiff` helpers.

---

## 9. Interaction with serialization and providers

- Dictionaries and sets must serialize to a deterministic shape for `$set` of whole values. The entity serializers already handle `Dictionary<string,object>` and collections; verify `HashSet<T>` serializes as a JSON array (order: insertion/enumeration order; document that order is not semantically significant for sets).
- For `WholeArray` `$set`, the bound value is the **serialized array**, so `Ref<T>` elements become id strings via the existing converters.
- For SQLite (separate plan), `$set` of an array is a `json_set` of the whole array; per-key dictionary ops map to `json_set`/`json_remove`; `$addToSet`/`$pull` are unsupported in v1 → fall back to `WholeArray`.
- Mongo `$set` of `M.3` on an array requires the array to exist at that index; `IndexedOps` therefore relies on `ExpectedVersion` to guarantee the index is valid.

---

## 10. Edge cases and known limitations

| Case | Behavior / limitation |
| --- | --- |
| In-place mutation without `BeginTracking` | Not detected. Plain collections require `BeginTracking(acceptCurrentState: true)`. |
| Mutation after `AcceptChanges` | Detected relative to the new baseline. Correct. |
| External code retains the old collection instance and mutates it after replacement | The old instance is no longer the member value; its mutations are ignored. Correct. |
| Dictionary key containing `.` | Mongo dotted path ambiguity → whole-object `$set` fallback. |
| `HashSet<T>` element mutated in place | Not detectable by set equality; treat as replace of the element (document). |
| Duplicate elements in a `List<T>` | Positional diff handles them; `SetOps` on a `List<T>` is disallowed. |
| Very large collections | `MaxBaselineElements` skips baselining; replacement-only tracking. |
| Read-only collection properties (get-only) | Not emitted as settable members; not tracked. Document. |
| `ImmutableArray<T>`/`FrozenSet<T>` | Treated as `Other`; replacement-only via `SetField`; no baseline diff (immutable, so replacement is the only change). |

---

## 11. Tests

Generator/golden tests (`Saturn.Generator.Entities.Tests`):

- `Plain_List_Emits_Baseline_Capture`
- `Plain_Array_Emits_Baseline_Capture`
- `Plain_Set_Emits_Baseline_Capture`
- `Plain_Dictionary_Emits_Baseline_Capture`
- `Instrument_List_Emits_TrackedList_Property`
- `Instrument_Dictionary_Emits_TrackedDictionary_Property`
- `ByteArray_Is_Treated_As_Scalar`

Runtime tests (`Saturn.Data.ChangeTracking.Tests`):

- `List_Add_Detected_By_Baseline_Diff`
- `List_Remove_Detected_By_Baseline_Diff`
- `List_Reorder_Detected_As_WholeArray_Set`
- `List_InPlace_Edit_Detected`
- `Array_Replace_Detected`
- `Set_Add_And_Remove_Detected_SetOps`
- `Set_Order_Change_Is_Not_A_Change`
- `Dictionary_Add_Update_Remove_Detected_As_Key_Paths`
- `Dictionary_Key_With_Dot_Falls_Back_To_WholeObject`
- `Null_To_Empty_Is_A_Change`
- `Ref_Element_Normalized_To_Id`
- `Entity_Element_Diffed_ById`
- `TrackedList_Raises_Indexed_Ops`
- `TrackedDictionary_Raises_Key_Ops`
- `MaxBaselineElements_Skips_Large_Collections`

Provider tests (Phase E): patch a document containing each collection kind through Mongo/StellarDB/LiteDbX/SQLite and assert the persisted result.

---

## 12. Acceptance criteria

- Every supported collection kind appears in `ToUpdateDocument()` after an in-place mutation, for loaded entities.
- `WholeArray` is the default; `IndexedOps`/`SetOps` are opt-in and only used where providers/observability support them.
- No `ObservableCollections` dependency is required for plain-collection tracking.
- `Ref`/`Entity` elements normalize correctly.
- Large-collection guard and member opt-out exist and are documented.
- Cross-provider tests pass.

---

## 13. Do NOT

- Do not change the default strategy away from `WholeArray`.
- Do not change a member's emitted type unless `Instrument = true` is set.
- Do not attempt to observe in-place mutation of a raw `List<T>` without a baseline or a wrapper.
- Do not treat `byte[]` as an element list.
- Do not put wrappers or diff helpers in `GoLive.Saturn.Data.Entities`.
- Do not add comments to `.cs` files.
