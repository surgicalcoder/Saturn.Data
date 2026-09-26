# Phase F — Baseline Mode, Security Filters, and Telemetry

**Goal:** Add an optional baseline diff mode (which also powers C10 plain collections), visibility-based filtering for client-facing patches, per-DTO/view patchable allow-lists, and lightweight telemetry.

**Prerequisite:** Phase E complete and all tests green.

**Exit criteria:**
- `[ChangeTracking(Mode = Baseline)]` computes changes by diffing against a captured baseline.
- Baseline diff is the default detection mechanism for plain collections (`List<T>`, arrays, `HashSet<T>`, dictionaries) per the C10 plan.
- `IChangeSetFilter` excludes `WriteOnly`/server-managed fields from client-facing documents by default.
- Generated `PatchableMembers` allow-lists exist per DTO/view and are used to validate incoming patches.
- Telemetry records change-set size and conflict counts through an injectable observer.
- `Journal` remains the default mode.

**Placement:** all runtime types in this phase live in `GoLive.Saturn.Data.ChangeTracking`; all attributes live in the same project (generator matches them by metadata name).

---

## Task F.1 — Change-tracking opt-in attributes

`ChangeTrackingMode` and `CollectionStrategy` already exist in `GoLive.Saturn.Data.ChangeTracking` (Phase C). Create the attributes in the same project:

Create `D:\Work\Saturn.Data\Saturn.Data.ChangeTracking\GoLive.Saturn.Data.ChangeTracking\Attributes\ChangeTrackingAttribute.cs`:

```csharp
using System;

namespace GoLive.Saturn.Data.ChangeTracking;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ChangeTrackingAttribute : Attribute
{
    public ChangeTrackingMode Mode { get; set; } = ChangeTrackingMode.Journal;

    public bool TrackRefItemChanges { get; set; }
}
```

Create `Attributes\NoChangeTrackingAttribute.cs`:

```csharp
using System;

namespace GoLive.Saturn.Data.ChangeTracking;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class NoChangeTrackingAttribute : Attribute { }
```

Create `Attributes\CollectionTrackingAttribute.cs` (move it here from the generator Resources so it shares the enums):

```csharp
using System;

namespace GoLive.Saturn.Data.ChangeTracking;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class CollectionTrackingAttribute : Attribute
{
    public CollectionStrategy Strategy { get; set; } = CollectionStrategy.WholeArray;

    public bool Instrument { get; set; }
}
```

**Opt-in resolution** (assembly default + per-class override, decision 3):

| Assembly default (`SaturnChangeTracking`) | Class attribute | Result |
| --- | --- | --- |
| absent / `false` | `[ChangeTracking]` | track |
| absent / `false` | none | do not track |
| `true` | `[NoChangeTracking]` | do not track |
| `true` | `[ChangeTracking]` or none | track |

Read the MSBuild property in the generator pipeline via `AnalyzerConfigOptionsProvider.GlobalOptions` (`build_property.SaturnChangeTracking`) as in Phase B.

Add `ChangeTrackingMode TrackingMode` and `bool TrackChanges` to `ClassToGenerate`; read `[ChangeTracking]`/`[NoChangeTracking]` by metadata name.

The generator emits `ITrackable` members (Phase C.4) only for tracked classes. `TrackingMode` drives whether `GetChangeSet` uses the journal or `ComputeBaselineDiff()`.

---

## Task F.2 — Baseline diff

For entities with `Baseline` or `JournalWithBaseline`, emit a diff override. `ChangeTracker` (tracking project) owns the baseline store; the generated class supplies per-member capture/restore and diff:

```csharp
protected override void CaptureBaseline()
{
    tracker.CaptureValue("CustomerName", CustomerName);
    tracker.CaptureValue("Total", Total);
    tracker.CaptureValue("Lines", Lines is null ? null : new List<Line>(Lines));
}

protected override void RestoreBaseline()
{
    CustomerName = (string)tracker.BaselineValue("CustomerName");
    Total = (decimal)tracker.BaselineValue("Total");
    var lines = (List<Line>)tracker.BaselineValue("Lines");
    Lines = lines is null ? null : new ObservableList<Line>(lines);
}

protected override IEnumerable<FieldChange> ComputeBaselineDiff()
{
    if (!EqualityComparer<string>.Default.Equals((string)tracker.BaselineValue("CustomerName"), CustomerName))
    {
        yield return new FieldChange
        {
            Path = "CustomerName",
            Kind = ChangeKind.Set,
            OldValue = tracker.BaselineValue("CustomerName"),
            NewValue = CustomerName
        };
    }

    var baselineLines = (List<Line>)tracker.BaselineValue("Lines");
    if (!SequenceEqual(baselineLines ?? new List<Line>(), Lines ?? new List<Line>()))
    {
        yield return new FieldChange
        {
            Path = "Lines",
            Kind = ChangeKind.Set,
            OldValue = baselineLines,
            NewValue = Lines is null ? null : new List<Line>(Lines)
        };
    }
}
```

Add to `ChangeTracker`:

```csharp
    private readonly Dictionary<string, object> baselineValues = new();

    public void CaptureValue(string memberName, object value) => baselineValues[memberName] = value;

    public object BaselineValue(string memberName) => baselineValues.TryGetValue(memberName, out var value) ? value : null;

    public void ClearBaseline() => baselineValues.Clear();
```

`ITrackable` gains `IEnumerable<FieldChange> ComputeBaselineDiff();`; `Entity` gains the corresponding `protected virtual` hook so the generated partial can override it. `ChangeTracker.Build` uses:

```csharp
var fields = mode == ChangeTrackingMode.Baseline
    ? owner.ComputeBaselineDiff().ToList()
    : journal.ToList();
```

Pass the mode into `Build` or store it on the tracker when `Begin` is called.

`RejectChanges` uses captured `OldValue`s (journal) or the baseline values (baseline mode).

---

## Task F.3 — Visibility filter

Create `D:\Work\Saturn.Data\Saturn.Data.ChangeTracking\GoLive.Saturn.Data.ChangeTracking\IChangeSetFilter.cs`:

```csharp
namespace GoLive.Saturn.Data.ChangeTracking;

public interface IChangeSetFilter
{
    FieldChange Filter(FieldChange change);
}

public sealed class VisibilityChangeSetFilter : IChangeSetFilter
{
    public bool IncludeWriteOnly { get; set; }

    public bool IncludeServerManaged { get; set; }

    public FieldChange Filter(FieldChange change)
    {
        if (change.Visibility == ChangeVisibility.WriteOnly && !IncludeWriteOnly)
        {
            return null;
        }

        if (change.Visibility == ChangeVisibility.ServerManaged && !IncludeServerManaged)
        {
            return null;
        }

        return change;
    }
}
```

`UpdateDocumentBuilder.Build(changes, filter)` (Phase E.1) already accepts a filter. `PatchChanges` uses the default `VisibilityChangeSetFilter` (server-side, excludes `WriteOnly` only when the caller opts into the client-facing filter; document that server-side application may pass `IncludeWriteOnly = true`).

Populate `FieldChange.Visibility`: `ChangeTracker.Record`/`RecordList` read `((ITrackableMetadata)owner).VisibilityFor(memberName)` and default to `ReadWrite`. The generator emits `VisibilityFor` from `[ReadOnly]`/`[WriteOnly]`/`[DoNotTrackChanges]` metadata.

Add `ITrackableMetadata` to the tracking project (Phase C.4):

```csharp
public interface ITrackableMetadata
{
    ChangeVisibility VisibilityFor(string memberName);

    CollectionStrategy StrategyFor(string memberName);
}
```

---

## Task F.4 — Per-DTO/view patchable allow-list

For each generated DTO/view, emit:

```csharp
public static readonly global::System.Collections.Generic.HashSet<string> PatchableMembers = new(global::System.StringComparer.Ordinal)
{
    "CustomerName",
    "Total",
    "Lines"
};
```

Exclude `Id`, `Version`, read-only members, and members not present in the DTO/view.

Provide a validator in the tracking project (used by API/controller layers, not the repository):

```csharp
public static class ChangeSetValidator
{
    public static void ValidatePatch(IEnumerable<FieldChange> changes, IReadOnlySet<string> allowed)
    {
        foreach (var change in changes)
        {
            var root = change.Path;
            var dot = root.IndexOf('.');

            if (dot >= 0)
            {
                root = root[..dot];
            }

            if (!allowed.Contains(root))
            {
                throw new InvalidOperationException($"Field '{change.Path}' is not patchable for this view.");
            }
        }
    }
}
```

Document that the repository trusts the caller.

---

## Task F.5 — Telemetry

Add an optional observer to the tracking project:

```csharp
namespace GoLive.Saturn.Data.ChangeTracking;

public interface IChangeTrackingObserver
{
    void OnChangeSetCaptured(string entityType, int fieldCount, int writeOnlyExcluded);

    void OnPatchConflict(string entityType, string id, long? expectedVersion);
}
```

Accept it on `ChangeTracker` (optional constructor/ambient default). The repository's `Patch` path calls `OnPatchConflict` on `FailedToUpdateException` when an observer is configured. No external telemetry package.

---

## Task F.6 — Tests

`Saturn.Data.ChangeTracking.Tests`:

- `Baseline_Mode_Reports_Diff_Against_Baseline`
- `Baseline_Mode_Elides_Reverted_Field`
- `Baseline_Mode_Detects_Plain_List_InPlace_Mutation` (C10)
- `Journal_Mode_Is_Default`
- `Filter_Excludes_WriteOnly_By_Default`
- `Filter_Includes_WriteOnly_When_Enabled`
- `PatchableMembers_Excludes_Id_And_ReadOnly`
- `ValidatePatch_Rejects_Unknown_Field`
- `Observer_Receives_ChangeSet_And_Conflict`

`Saturn.Generator.Entities.Tests`:

- `Assembly_Default_Enables_Tracking_And_NoChangeTracking_Opts_Out`
- `PatchableMembers_Emitted_Per_View`

---

## Task F.7 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.ChangeTracking\GoLive.Saturn.Data.ChangeTracking.Tests\GoLive.Saturn.Data.ChangeTracking.Tests.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Tests\Saturn.Generator.Entities.Tests.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Data.MongoDb\Saturn.Data.MongoDb.Tests\Saturn.Data.MongoDb.Tests.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Data.LiteDbX\Saturn.Data.LiteDbX.Tests\Saturn.Data.LiteDbX.Tests.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Stellar\Saturn.Data.Stellar.Tests\Saturn.Data.Stellar.Tests.csproj"
```

---

## Do NOT

- Do not change the default tracking mode; `Journal` remains default.
- Do not put tracking runtime or attributes in `GoLive.Saturn.Data.Entities` or `Saturn.Generator.Entities.Resources`.
- Do not enforce patch allow-lists inside the repository; that is a caller/API concern.
- Do not add external telemetry dependencies.
- Do not log decrypted `EncryptedString`/`HashedString` values.
- Do not add comments to `.cs` files.
