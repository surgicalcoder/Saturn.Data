# Phase C — Rich Change Tracking

**Goal:** Create the separate `GoLive.Saturn.Data.ChangeTracking` project, add type-free hooks to `Entity`, implement a `ChangeTracker`, and emit tracking into opted-in entities **and** DTOs.

**Prerequisite:** Phase B complete and its tests green.

**Exit criteria:**
- `GoLive.Saturn.Data.ChangeTracking` exists, is in `Saturn.Data.slnx`, and has tests.
- `GoLive.Saturn.Data.Entities` has **no** reference to the tracking project.
- `Entity` exposes only type-free hooks (`OnFieldChanged`, `CaptureBaseline`, `RestoreBaseline`, `ChangeTrackingParent`, `ChangeTrackingPathSegment`).
- Opted-in entities and DTOs generate `IChangeTracked`/`ITrackable` members.
- `Id`, `Version`, `Changes`, `EnableChangeTracking`, `_shortId` never appear in the journal.
- Mutating an embedded child records a dotted path on the root.
- `Ref<T>`/`WeakRef` changes journal the id string.
- Hydration/population does not journal.
- Existing `Entity.Changes`/`EnableChangeTracking` behavior remains.

---

## Task C.1 — Create the `GoLive.Saturn.Data.ChangeTracking` project

Create `D:\Work\Saturn.Data\Saturn.Data.ChangeTracking\GoLive.Saturn.Data.ChangeTracking\GoLive.Saturn.Data.ChangeTracking.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>latest</LangVersion>
        <Version>1.0.0</Version>
        <PackageId>GoLive.Saturn.Data.ChangeTracking</PackageId>
        <Authors>SurgicalCoder</Authors>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        <Description>Optional change tracking and PATCH update documents for Saturn.Data.</Description>
        <Copyright>Copyright 2020-2026 - SurgicalCoder</Copyright>
        <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
        <PackageLicenseExpression>MIT</PackageLicenseExpression>
        <PackageProjectUrl>https://github.com/surgicalcoder/Saturn.Data</PackageProjectUrl>
        <RepositoryUrl>https://github.com/surgicalcoder/Saturn.Data</RepositoryUrl>
        <RepositoryType>git</RepositoryType>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions.csproj" />
        <ProjectReference Include="..\..\Saturn.Data.Entities\GoLive.Saturn.Data.Entities\GoLive.Saturn.Data.Entities.csproj" />
    </ItemGroup>

</Project>
```

Create the test project `Saturn.Data.ChangeTracking.Tests` mirroring `Saturn.Data.Sqlite.Tests` (xunit, Microsoft.NET.Test.Sdk 18.10.1, coverlet 10.0.1, project references to ChangeTracking + Entities).

Add both projects to `Saturn.Data.slnx` under a `/ChangeTracking/` folder. Use `ProjectReference` only; no intra-repo NuGet.

---

## Task C.2 — Runtime model (in the tracking project)

Create `Enums.cs`:

```csharp
namespace GoLive.Saturn.Data.ChangeTracking;

public enum ChangeKind { Set, Unset, Increment, ListAdd, ListRemove, ListReplace, ListMove, ListClear }

public enum ChangeVisibility { ReadWrite, ReadOnly, WriteOnly, ServerManaged }

public enum CollectionStrategy { WholeArray, IndexedOps, SetOps }

public enum ChangeTrackingMode { Journal, Baseline, JournalWithBaseline }
```

Create `IChangeTracked.cs`:

```csharp
namespace GoLive.Saturn.Data.ChangeTracking;

public interface IChangeTracked
{
    string Id { get; }

    long? Version { get; }
}
```

Create `ITrackable.cs`:

```csharp
namespace GoLive.Saturn.Data.ChangeTracking;

public interface ITrackable : IChangeTracked
{
    object ChangeTrackingParent { get; set; }

    string ChangeTrackingPathSegment { get; set; }

    bool IsTracking { get; }

    bool HasChanges { get; }

    void BeginTracking(bool acceptCurrentState = true);

    void AcceptChanges();

    void RejectChanges();

    EntityChangeSet GetChangeSet();

    string ToUpdateDocument();

    void CaptureBaseline();

    void RestoreBaseline();
}
```

Create `FieldChange.cs`:

```csharp
namespace GoLive.Saturn.Data.ChangeTracking;

public sealed class FieldChange
{
    public string Path { get; init; }

    public ChangeKind Kind { get; init; }

    public object OldValue { get; init; }

    public object NewValue { get; init; }

    public ChangeVisibility Visibility { get; init; } = ChangeVisibility.ReadWrite;

    public int? Index { get; init; }

    public CollectionStrategy Strategy { get; init; } = CollectionStrategy.WholeArray;
}
```

Create `EntityChangeSet.cs`:

```csharp
namespace GoLive.Saturn.Data.ChangeTracking;

public sealed class EntityChangeSet
{
    public string EntityType { get; init; }

    public string Id { get; init; }

    public long? ExpectedVersion { get; init; }

    public DateTimeOffset CapturedAtUtc { get; init; }

    public IReadOnlyList<FieldChange> Fields { get; init; } = Array.Empty<FieldChange>();

    public bool IsEmpty => Fields.Count == 0;

    public string ToUpdateDocument() => UpdateDocumentBuilder.Build(Fields);
}
```

Create `ChangeTracker.cs` (journal, baseline, path composition, ref normalization):

```csharp
namespace GoLive.Saturn.Data.ChangeTracking;

public sealed class ChangeTracker
{
    private static readonly HashSet<string> NonTrackedMembers = new(StringComparer.Ordinal)
    {
        nameof(IChangeTracked.Id),
        nameof(IChangeTracked.Version),
        "Changes",
        "EnableChangeTracking",
        "_shortId"
    };

    private readonly List<FieldChange> journal = new();
    private readonly Dictionary<string, object> baseline = new();

    public bool IsTracking { get; private set; }

    public bool HasChanges => journal.Count > 0;

    public void Begin(ITrackable owner, bool acceptCurrentState)
    {
        journal.Clear();
        IsTracking = true;

        if (acceptCurrentState)
        {
            owner.CaptureBaseline();
        }
    }

    public void Accept(ITrackable owner)
    {
        journal.Clear();
        owner.CaptureBaseline();
    }

    public void Reject(ITrackable owner)
    {
        owner.RestoreBaseline();
        journal.Clear();
    }

    public void Record(ITrackable owner, string propertyName, object oldValue, object newValue)
    {
        if (!IsTracking || propertyName is null || NonTrackedMembers.Contains(propertyName))
        {
            return;
        }

        journal.Add(new FieldChange
        {
            Path = ComposePath(owner, propertyName),
            Kind = ChangeKind.Set,
            OldValue = Normalize(oldValue),
            NewValue = Normalize(newValue),
            Visibility = ChangeVisibility.ReadWrite,
            Strategy = CollectionStrategy.WholeArray
        });
    }

    public void RecordList(ITrackable owner, string propertyName, object oldValue, object newValue, ChangeKind kind, int? index, CollectionStrategy strategy, ChangeVisibility visibility)
    {
        if (!IsTracking || propertyName is null)
        {
            return;
        }

        journal.Add(new FieldChange
        {
            Path = ComposePath(owner, propertyName),
            Kind = kind,
            OldValue = Normalize(oldValue),
            NewValue = Normalize(newValue),
            Index = index,
            Strategy = strategy,
            Visibility = visibility
        });
    }

    public EntityChangeSet Build(ITrackable owner)
    {
        return new EntityChangeSet
        {
            EntityType = owner.GetType().Name,
            Id = owner.Id,
            ExpectedVersion = owner.Version,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Fields = journal.ToList()
        };
    }

    private static string ComposePath(ITrackable owner, string propertyName)
    {
        if (owner.ChangeTrackingParent is not ITrackable parent || string.IsNullOrEmpty(owner.ChangeTrackingPathSegment))
        {
            return propertyName;
        }

        return ComposePath(parent, owner.ChangeTrackingPathSegment) + "." + propertyName;
    }

    private static object Normalize(object value)
    {
        return value switch
        {
            null => null,
            GoLive.Saturn.Data.Entities.IEntityReference reference => reference.RefId,
            _ => value
        };
    }
}
```

---

## Task C.3 — Type-free base hooks in `Entity`

`GoLive.Saturn.Data.Entities` must not reference the tracking project. Add only object/string hooks to `Entity.cs`:

```csharp
    public object ChangeTrackingParent { get; set; }

    public string ChangeTrackingPathSegment { get; set; }

    protected virtual void OnFieldChanged(string propertyName, object oldValue, object newValue)
    {
    }

    protected virtual void CaptureBaseline()
    {
    }

    protected virtual void RestoreBaseline()
    {
    }
```

Update `SetField` to raise the hook (after the existing `Changes` write):

```csharp
    protected virtual bool SetField<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return false;
        }

        var oldValue = field;
        field = newValue;
        OnPropertyChanged(propertyName);
        OnFieldChanged(propertyName, oldValue, newValue);

        if (propertyName != null && Changes != null && EnableChangeTracking)
        {
            Changes[propertyName] = newValue;
        }

        return true;
    }
```

Add `IEntityReference` to `Ref<T>`/`WeakRef`:

```csharp
public partial class Ref<T> : IEquatable<Ref<T>>, INotifyPropertyChanged, IEntityReference
{
    public string RefId => Id;
    ...
}
```

```csharp
public string RefId => Id;
```

Do **not** add an `ITrackable` implementation or any tracking-project reference to `Entity`.

---

## Task C.4 — Generator emission (opt-in, entities and DTOs)

The generator emits tracking only when opted in (`[ChangeTracking]` or the assembly-level `SaturnChangeTracking` MSBuild property, and not `[NoChangeTracking]`). Read MSBuild properties via `context.AnalyzerConfigOptionsProvider.GlobalOptions` in Phase C (wire it in the pipeline: `build_property.SaturnChangeTracking`, `build_property.SaturnGenerateDtos`).

For an opted-in class (entity or DTO), emit:

```csharp
public bool IsTracking => tracker.IsTracking;

public bool HasChanges => tracker.HasChanges;

public void BeginTracking(bool acceptCurrentState = true) => tracker.Begin(this, acceptCurrentState);

public void AcceptChanges() => tracker.Accept(this);

public void RejectChanges() => tracker.Reject(this);

public global::GoLive.Saturn.Data.ChangeTracking.EntityChangeSet GetChangeSet() => tracker.Build(this);

public string ToUpdateDocument() => tracker.Build(this).ToUpdateDocument();

private readonly global::GoLive.Saturn.Data.ChangeTracking.ChangeTracker tracker = new();

protected override void OnFieldChanged(string propertyName, object oldValue, object newValue)
{
    tracker.Record(this, propertyName, oldValue, newValue);
}

protected override void CaptureBaseline()
{
    ...capture per member...
}

protected override void RestoreBaseline()
{
    ...restore per member...
}
```

- For **entities**, the generated partial adds `: global::GoLive.Saturn.Data.ChangeTracking.ITrackable` and uses `protected override` for `OnFieldChanged`/`CaptureBaseline`/`RestoreBaseline` (they exist on `Entity`).
- For **DTOs**, the DTO does not derive `Entity`, so implement the interface members directly (`public void OnFieldChanged(...)` cannot be an override) and add the interface to the DTO declaration. Generate `Id`/`Version` backing so `IChangeTracked` is satisfied.
- Embed a generated metadata map:

```csharp
private static global::GoLive.Saturn.Data.ChangeTracking.ChangeVisibility VisibilityFor(string memberName)
{
    return memberName switch
    {
        "Password" => global::GoLive.Saturn.Data.ChangeTracking.ChangeVisibility.WriteOnly,
        _ => global::GoLive.Saturn.Data.ChangeTracking.ChangeVisibility.ReadWrite
    };
}

private static global::GoLive.Saturn.Data.ChangeTracking.CollectionStrategy StrategyFor(string memberName)
{
    return memberName switch
    {
        _ => global::GoLive.Saturn.Data.ChangeTracking.CollectionStrategy.WholeArray
    };
}
```

- `Parent` wiring for `EmbeddedEntity` members (Phase D uses it):

```csharp
public Address Address
{
    get => address;
    set
    {
        if (ReferenceEquals(address, value))
        {
            return;
        }

        if (address is not null)
        {
            address.ChangeTrackingParent = null;
            address.ChangeTrackingPathSegment = null;
        }

        SetField(ref this.address, value);

        if (address is not null)
        {
            address.ChangeTrackingParent = this;
            address.ChangeTrackingPathSegment = nameof(Address);
        }
    }
}
```

- Replace any previously generated `HasChanges`/tracking members with these. Remove the constructor-only collection subscription (Phase D moves it to the setter).

- `ChangeTracker` needs visibility/strategy for list ops. Route list recording through the generated `RecordList` call rather than `Record`, or pass the maps via the `ITrackable` implementation. Simplest: generated code exposes `internal static ChangeVisibility VisibilityFor(...)`/`StrategyFor(...)`, and `ChangeTracker.Record` reads them via an interface `ITrackableMetadata` that the generated class implements:

```csharp
public interface ITrackableMetadata
{
    ChangeVisibility VisibilityFor(string memberName);

    CollectionStrategy StrategyFor(string memberName);
}
```

`ChangeTracker.Record` calls `(owner as ITrackableMetadata)?.VisibilityFor(propertyName)` and defaults to `ReadWrite`/`WholeArray`.

---

## Task C.5 — Hydration guard

Add to the generated class (or base `Entity`) a scope that suppresses tracking:

```csharp
using (entity.SuppressTracking())
{
}
```

Implementation (generated or base): set a `IsHydrating` flag on the tracker so `Record` is a no-op. Add to `Entity`:

```csharp
    public bool IsHydrating { get; set; }

    public IDisposable SuppressTracking() => new TrackingScope(this);

    private sealed class TrackingScope : IDisposable
    {
        private readonly Entity entity;
        private readonly bool previous;

        public TrackingScope(Entity entity)
        {
            this.entity = entity;
            previous = entity.IsHydrating;
            entity.IsHydrating = true;
        }

        public void Dispose() => entity.IsHydrating = previous;
    }
```

Have `Entity.OnFieldChanged` skip when `IsHydrating` (the generated override should check it too, or `ChangeTracker.Record` takes the flag). Simplest: generated override:

```csharp
protected override void OnFieldChanged(string propertyName, object oldValue, object newValue)
{
    if (IsHydrating)
    {
        return;
    }

    tracker.Record(this, propertyName, oldValue, newValue);
}
```

Wrap population paths (`PopulationExtensions.Populate`, `UpdateFrom`-based hydration) in `SuppressTracking()`, and make `PopulationExtensions` non-`async` (assessment D13).

---

## Task C.6 — Tracking is opt-in and separable

Verify:

- `GoLive.Saturn.Data.Entities.csproj` has no reference to `GoLive.Saturn.Data.ChangeTracking`.
- A consumer that does not reference `GoLive.Saturn.Data.ChangeTracking` can still use `Entity` and the DTO/view generator (no tracking code emitted unless opted in).
- The generator itself does not reference the tracking assembly (attributes matched by metadata name).

---

## Task C.7 — Tests

`Saturn.Data.ChangeTracking.Tests`:

- `Scalar_Change_Records_Path_And_Values`
- `Setting_Same_Value_Does_Not_Journal`
- `Id_And_Version_Are_Not_Journaled`
- `Ref_Change_Records_Id_String`
- `Nested_Embedded_Change_Records_Dotted_Path`
- `Hydration_Does_Not_Journal`
- `AcceptChanges_Clears_Journal`
- `RejectChanges_Restores_Baseline`
- `Legacy_Changes_Dictionary_Still_Populates`
- `Untracked_Class_Emits_No_Tracking_Members` (generator golden test)

`Saturn.Generator.Entities.Tests`:

- `Tracking_Members_Emitted_Only_When_Opted_In`
- `Assembly_Default_Enables_Tracking_And_NoChangeTracking_Opts_Out`

---

## Task C.8 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.ChangeTracking\GoLive.Saturn.Data.ChangeTracking.Tests\GoLive.Saturn.Data.ChangeTracking.Tests.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Tests\Saturn.Generator.Entities.Tests.csproj"
dotnet build "D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Playground\Saturn.Generator.Entities.Playground.csproj"
```

---

## Do NOT

- Do not add a reference from `GoLive.Saturn.Data.Entities` (or `GoLive.Saturn.Data.Abstractions`) to `GoLive.Saturn.Data.ChangeTracking`.
- Do not put the journal, baseline, or `FieldChange` in the entities project.
- Do not journal `Id`, `Version`, `Changes`, `EnableChangeTracking`, or `_shortId`.
- Do not store `Ref<T>` objects in the journal; store ids.
- Do not use NuGet for intra-repo dependencies.
- Do not add comments to `.cs` files.
