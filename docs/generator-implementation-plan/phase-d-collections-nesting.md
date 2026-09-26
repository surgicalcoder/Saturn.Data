# Phase D — Collections and Nesting

**Goal:** Make collection tracking work on entities that are loaded/deserialized, capture structured list operations, support per-member serialization strategies, and propagate changes from embedded children.

**Prerequisite:** Phase C complete and its tests green.

**Plain (non-observable) collections are specified in detail in [`c10-collection-instrumentation-plan.md`](c10-collection-instrumentation-plan.md)** (finding C10). This phase covers the observable-collection lifecycle and embedded nesting; the C10 plan covers `List<T>`/arrays/`HashSet<T>`/dictionaries.

**Exit criteria:**
- Assigning a new collection after `BeginTracking()` subscribes to the new collection and unsubscribes from the old.
- Add / Remove / Replace / Move / Clear produce `FieldChange` entries with the correct `ChangeKind` and index.
- Collection strategy is configurable per member (`WholeArray` default; `IndexedOps`; `SetOps`).
- A mutation inside an embedded child element is journaled on the root with a dotted path.
- Plain `List<T>`/arrays track replacement; in-place mutation is captured via baseline diff (default) or via `[CollectionTracking(Instrument = true)]` wrappers (decision 9).

---

## Task D.1 — Collection attributes (already created in Phase C/F)

`CollectionStrategy` and `[CollectionTracking]` are created in `GoLive.Saturn.Data.ChangeTracking` (Phase C.2 and Phase F.1), not in `Saturn.Generator.Entities.Resources`. Do not duplicate them here.

Add `CollectionStrategy Strategy` and `bool Instrument` to `MemberToGenerate`, read from `[CollectionTracking]` in `Scanner.ConvertToMembers` (default `WholeArray`, `Instrument = false`). The generator matches the attribute by metadata name (`GoLive.Saturn.Data.ChangeTracking.CollectionTrackingAttribute`) and therefore has no compile reference to the tracking project.

---

## Task D.2 — List journaling (already provided in Phase C.2)

`ChangeTracker.RecordList` already provides index/strategy/visibility-aware journaling. Do **not** add a `RecordChange` overload to `Entity`; `Entity` stays free of tracking types. Verify the signature:

```csharp
public void RecordList(ITrackable owner, string propertyName, object oldValue, object newValue, ChangeKind kind, int? index, CollectionStrategy strategy, ChangeVisibility visibility)
```

The generated collection handler calls `tracker.RecordList(...)` (D.3).

---

## Task D.3 — Fix collection subscription (assessment C2)

**Problem:** Collections are subscribed only in the generated constructor. Assigning a new list after construction (including during deserialization/population) leaves the old list subscribed and the new list untracked.

**Fix:** Generate a named handler per collection and subscribe/unsubscribe in the setter.

Constructor (replace inline lambdas):

```csharp
public MainItem()
{
    Lines = new();
    Lines.CollectionChanged += OnLinesChanged;
}

private void OnLinesChanged(in global::ObservableCollections.NotifyCollectionChangedEventArgs<Line> eventArgs)
{
    if (!IsTracking || IsHydrating)
    {
        return;
    }

    var strategy = StrategyFor(nameof(Lines));
    var visibility = VisibilityFor(nameof(Lines));

    switch (eventArgs.Action)
    {
        case global::System.Collections.Specialized.NotifyCollectionChangedAction.Add:
            tracker.RecordList(this, nameof(Lines), null, eventArgs.NewItem, global::GoLive.Saturn.Data.ChangeTracking.ChangeKind.ListAdd, eventArgs.NewStartingIndex, strategy, visibility);
            break;
        case global::System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
            tracker.RecordList(this, nameof(Lines), eventArgs.OldItem, null, global::GoLive.Saturn.Data.ChangeTracking.ChangeKind.ListRemove, eventArgs.OldStartingIndex, strategy, visibility);
            break;
        case global::System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
            tracker.RecordList(this, nameof(Lines), eventArgs.OldItem, eventArgs.NewItem, global::GoLive.Saturn.Data.ChangeTracking.ChangeKind.ListReplace, eventArgs.NewStartingIndex, strategy, visibility);
            break;
        case global::System.Collections.Specialized.NotifyCollectionChangedAction.Move:
            tracker.RecordList(this, nameof(Lines), eventArgs.OldItem, eventArgs.NewItem, global::GoLive.Saturn.Data.ChangeTracking.ChangeKind.ListMove, eventArgs.NewStartingIndex, strategy, visibility);
            break;
        case global::System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
            tracker.RecordList(this, nameof(Lines), null, new global::System.Collections.Generic.List<Line>(Lines), global::GoLive.Saturn.Data.ChangeTracking.ChangeKind.ListClear, null, strategy, visibility);
            break;
    }
}
```

Setter:

```csharp
public global::ObservableCollections.ObservableList<Line> Lines
{
    get => lines;
    set
    {
        if (ReferenceEquals(lines, value))
        {
            return;
        }

        if (lines is not null)
        {
            lines.CollectionChanged -= OnLinesChanged;
        }

        SetField(ref this.lines, value);

        if (lines is not null)
        {
            lines.CollectionChanged += OnLinesChanged;
        }
    }
}
```

If the emitter currently generates anonymous lambdas, replace them with named handlers. Do not change the emitted constructor behavior for collections that were already initialized.

---

## Task D.4 — Plain collections

Do **not** implement plain-collection instrumentation here. Follow [`c10-collection-instrumentation-plan.md`](c10-collection-instrumentation-plan.md): default `WholeArray` via baseline diff, with opt-in `TrackedList<T>`/`TrackedSet<T>`/`TrackedDictionary<TKey,TValue>` wrappers.

---

## Task D.5 — Embedded element propagation

For collections whose element type derives from `Entity` (observable or instrumented only — plain collections cannot hook element mutations, see C10 §7):

- On `Add`/`Replace`, set element parent:
  ```csharp
  if (eventArgs.NewItem is not null)
  {
      eventArgs.NewItem.ChangeTrackingParent = this;
      eventArgs.NewItem.ChangeTrackingPathSegment = nameof(Lines) + "." + eventArgs.NewStartingIndex;
  }
  ```
- On `Remove`/`Replace`, clear the old element's parent.
- On `Move`, update affected elements' `ChangeTrackingPathSegment` (reindex from the move index to the end).
- On `Clear`, clear parents for all elements.

`Entity.ChangeTrackingParent`/`ChangeTrackingPathSegment` are public, type-free properties (Phase C.3), so the generated code compiles in any assembly without referencing the tracking project.

Document the index-drift caveat: with `IndexedOps`, element paths are valid only if the patch is applied against the same collection state (enforced by `ExpectedVersion`). Plain collections are diffed as whole elements and do not produce per-element paths unless `Instrument = true`.

---

## Task D.6 — Strategy-aware collection serialization (capture only)

Phase E renders the update document. In this phase, ensure the journal contains enough information for each strategy:

- `WholeArray`: one `FieldChange` per mutation is sufficient; Phase E collapses all changes for the collection into a single `$set` of the current array.
- `IndexedOps`: retain `Index` and `ChangeKind` so Phase E can emit `Collection.[index]` paths, applying removals in descending index order.
- `SetOps`: retain element identity (normalized via `IEntityReference.RefId` or value) for `$addToSet`/`$pull`.

`FieldChange.Strategy` already exists (Phase C.2). The generated class implements `ITrackableMetadata` (Phase C.4/F.1) and supplies the per-member maps:

```csharp
public global::GoLive.Saturn.Data.ChangeTracking.CollectionStrategy StrategyFor(string memberName)
{
    return memberName switch
    {
        nameof(Lines) => global::GoLive.Saturn.Data.ChangeTracking.CollectionStrategy.WholeArray,
        _ => global::GoLive.Saturn.Data.ChangeTracking.CollectionStrategy.WholeArray
    };
}

public global::GoLive.Saturn.Data.ChangeTracking.ChangeVisibility VisibilityFor(string memberName)
{
    return memberName switch
    {
        _ => global::GoLive.Saturn.Data.ChangeTracking.ChangeVisibility.ReadWrite
    };
}
```

The handler passes `strategy`/`visibility` into `tracker.RecordList` (D.3), so no separate map lookup is needed in `ChangeTracker`.

and have `Entity.AppendChange` call `StrategyFor(propertyName)` when `kind` is a list operation. Add `protected virtual CollectionStrategy StrategyFor(string memberName)` returning `WholeArray` to `Entity`.

---

## Task D.7 — Tests

Add to the generator/runtime tests:

- `Assigning_New_Collection_Subscribes_New_And_Unsubscribes_Old`
- `List_Add_Records_ListAdd_With_Index`
- `List_Remove_Records_ListRemove_With_Index`
- `List_Replace_Records_ListReplace`
- `List_Move_Records_ListMove`
- `List_Clear_Records_ListClear`
- `Mutation_Of_Embedded_Element_Records_Dotted_Path`
- `Raw_List_Replacement_Records_Whole_Set`
- `InPlace_Mutation_Of_Raw_List_Is_Not_Observed` (documents the limitation)
- `Strategy_Is_Captured_Per_Collection`

Runtime example:

```csharp
[Fact]
public void List_Add_Records_ListAdd_With_Index()
{
    var entity = new MainItem { Strings = new ObservableList<string>() };
    entity.BeginTracking();
    entity.Strings.Add("a");

    var change = Assert.Single(entity.ChangeJournal);
    Assert.Equal(ChangeKind.ListAdd, change.Kind);
    Assert.Equal(0, change.Index);
    Assert.Equal("a", change.NewValue);
}
```

---

## Task D.8 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Tests\Saturn.Generator.Entities.Tests.csproj"
dotnet build "D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Playground\Saturn.Generator.Entities.Playground.csproj"
```

---

## Do NOT

- Do not change the emitted property type for plain `List<T>` unless `Instrument = true` is explicitly set.
- Do not keep the old constructor-only subscription while also subscribing in the setter (double subscription).
- Do not attempt to observe in-place mutation of raw `List<T>`; document it.
- Do not add comments to `.cs` files.
