# Phase E — Patch Bridge and Providers

**Goal:** Render an `EntityChangeSet` into a MongoDB-style update document and make every provider's `Patch` consume it, with a `PatchChanges` extension method (kept out of Abstractions) and a change-feed payload mode.

**Prerequisite:** Phase D and Phase B2 (C10) complete and their tests green.

**Exit criteria:**
- `entity.ToUpdateDocument()` and `entity.GetChangeSet().ToUpdateDocument()` produce `{ "$set": {...}, "$unset": {...}, "$inc": {...} }`, honoring collection strategies.
- `repository.PatchChanges(id, expectedVersion, changeSet)` works on Mongo, StellarDB, LiteDbX, and SQLite; version increments; a stale `expectedVersion` throws `FailedToUpdateException`.
- `Id`/`Version`/`WriteOnly` (unless explicitly enabled) never appear in the document.
- End-to-end tests per provider.

---

## Task E.1 — `UpdateDocumentBuilder` in the tracking project

Create `D:\Work\Saturn.Data\Saturn.Data.ChangeTracking\GoLive.Saturn.Data.ChangeTracking\UpdateDocumentBuilder.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.ChangeTracking;

public static class UpdateDocumentBuilder
{
    private static readonly JsonSerializerOptions Options = new();

    public static string Build(IReadOnlyList<FieldChange> changes)
    {
        return Build(changes, new VisibilityChangeSetFilter());
    }

    public static string Build(IReadOnlyList<FieldChange> changes, IChangeSetFilter filter)
    {
        var set = new JsonObject();
        var unset = new JsonObject();
        var inc = new JsonObject();
        var addToSet = new JsonObject();
        var pull = new JsonObject();

        foreach (var change in OrderForApply(changes))
        {
            if (filter?.Filter(change) is null)
            {
                continue;
            }

            switch (change.Kind)
            {
                case ChangeKind.Set:
                    set[change.Path] = ToNode(change.NewValue);
                    break;
                case ChangeKind.Unset:
                    unset[change.Path] = true;
                    break;
                case ChangeKind.Increment:
                    inc[change.Path] = ToNode(change.NewValue);
                    break;
                case ChangeKind.ListClear:
                    set[change.Path] = new JsonArray();
                    break;
                case ChangeKind.ListAdd:
                    ApplyListAdd(change, set, addToSet);
                    break;
                case ChangeKind.ListRemove:
                    ApplyListRemove(change, set, unset, pull);
                    break;
                case ChangeKind.ListReplace:
                case ChangeKind.ListMove:
                    ApplyListReplace(change, set);
                    break;
            }
        }

        var document = new JsonObject();

        if (set.Count > 0)
        {
            document["$set"] = set;
        }

        if (unset.Count > 0)
        {
            document["$unset"] = unset;
        }

        if (inc.Count > 0)
        {
            document["$inc"] = inc;
        }

        if (addToSet.Count > 0)
        {
            document["$addToSet"] = addToSet;
        }

        if (pull.Count > 0)
        {
            document["$pull"] = pull;
        }

        return document.ToJsonString(Options);
    }

    private static void ApplyListAdd(FieldChange change, JsonObject set, JsonObject addToSet)
    {
        if (change.Strategy == CollectionStrategy.SetOps)
        {
            addToSet[change.Path] = ToNode(change.NewValue);
        }
        else if (change.Strategy == CollectionStrategy.IndexedOps && change.Index.HasValue)
        {
            set[change.Path + "." + change.Index.Value] = ToNode(change.NewValue);
        }
        else
        {
            set[change.Path] = ToNode(change.NewValue);
        }
    }

    private static void ApplyListRemove(FieldChange change, JsonObject set, JsonObject unset, JsonObject pull)
    {
        if (change.Strategy == CollectionStrategy.SetOps)
        {
            pull[change.Path] = ToNode(change.OldValue);
        }
        else if (change.Strategy == CollectionStrategy.IndexedOps && change.Index.HasValue)
        {
            unset[change.Path + "." + change.Index.Value] = true;
        }
        else
        {
            set[change.Path] = ToNode(change.NewValue);
        }
    }

    private static void ApplyListReplace(FieldChange change, JsonObject set)
    {
        if (change.Strategy == CollectionStrategy.IndexedOps && change.Index.HasValue)
        {
            set[change.Path + "." + change.Index.Value] = ToNode(change.NewValue);
        }
        else
        {
            set[change.Path] = ToNode(change.NewValue);
        }
    }

    private static IEnumerable<FieldChange> OrderForApply(IReadOnlyList<FieldChange> changes)
    {
        var removals = new List<FieldChange>();
        var others = new List<FieldChange>();

        foreach (var change in changes)
        {
            if (change.Kind == ChangeKind.ListRemove && change.Strategy == CollectionStrategy.IndexedOps)
            {
                removals.Add(change);
            }
            else
            {
                others.Add(change);
            }
        }

        removals.Sort((left, right) => (right.Index ?? 0).CompareTo(left.Index ?? 0));

        foreach (var change in others)
        {
            yield return change;
        }

        foreach (var change in removals)
        {
            yield return change;
        }
    }

    private static JsonNode ToNode(object value)
    {
        return value switch
        {
            null => null,
            JsonNode node => node,
            string text => JsonValue.Create(text),
            bool flag => JsonValue.Create(flag),
            int number => JsonValue.Create(number),
            long number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            DateTime dateTime => JsonValue.Create(dateTime),
            DateTimeOffset dateTimeOffset => JsonValue.Create(dateTimeOffset),
            Enum enumeration => JsonValue.Create(enumeration.ToString()),
            Entity entity => JsonValue.Create(entity.Id),
            IEntityReference reference => JsonValue.Create(reference.RefId),
            _ => JsonSerializer.SerializeToNode(value, Options)
        };
    }
}
```

Notes:

- `CollectionStrategy` lives in `GoLive.Saturn.Data.ChangeTracking` (Phase C), so `FieldChange.Strategy` and this builder need no generator reference.
- `VisibilityChangeSetFilter` (Phase F) defaults to excluding `WriteOnly`. For server-side/internal documents, pass a filter with `IncludeWriteOnly = true`.
- `EntityChangeSet.ToUpdateDocument()` calls `UpdateDocumentBuilder.Build(Fields)` (already defined in Phase C).

---

## Task E.2 — Whole-array snapshots (already captured in B2/C10)

For `CollectionStrategy.WholeArray`, the journal entry must carry the collection snapshot so the renderer can emit `$set` of the whole array. Phase B2 (C10) and Phase D define this:

- Observable collections: on any list mutation, record `NewValue = new List<T>(collection)` when the strategy is `WholeArray`.
- Plain collections: baseline diff records a single `Set` with `NewValue = new List<T>(current)` when the collection differs.

If those phases are implemented as written, no work is required here. Verify with the E.6 tests.

---

## Task E.3 — `PatchChanges` extension method (in the tracking project)

Do **not** modify `IRepository` or add the tracking dependency to `GoLive.Saturn.Data.Abstractions`. Put the bridge in the tracking project:

Create `D:\Work\Saturn.Data\Saturn.Data.ChangeTracking\GoLive.Saturn.Data.ChangeTracking\RepositoryPatchExtensions.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.ChangeTracking;

public static class RepositoryPatchExtensions
{
    public static Task PatchChanges<TEntity>(this IRepository repository, string id, long? expectedVersion, EntityChangeSet changes,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TEntity : Entity
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(changes);

        return repository.Patch<TEntity>(id, expectedVersion, changes.ToUpdateDocument(), null, transaction, cancellationToken);
    }
}
```

This keeps `GoLive.Saturn.Data.Abstractions` free of the tracking dependency (decision 1) and requires no provider changes for v1.

Optional typed definition (for providers that want structured application) — place in the tracking project, not Abstractions:

```csharp
public sealed class ChangeSetUpdateDefinition<TEntity> : IDataUpdateDefinition<TEntity> where TEntity : Entity
{
    public ChangeSetUpdateDefinition(EntityChangeSet changes)
    {
        Changes = changes ?? throw new ArgumentNullException(nameof(changes));
    }

    public EntityChangeSet Changes { get; }
}
```

---

## Task E.4 — Verify and repair provider patch support

Confirm each provider applies the v1 operators (`$set`, `$unset`, `$inc`, and `$set` of whole arrays):

| Provider | File | Expected | Action |
| --- | --- | --- | --- |
| Mongo | `MongoDbRepository.Repository.cs` (`Patch`) | native `JsonUpdateDefinition` + `Inc("_v",1)` | none; add a test |
| LiteDbX | `LiteDbRepository.Repository.cs` (`Patch`) | read-modify-write merge; verify `$unset`/`$inc` | extend the parser if missing |
| StellarDB | `StellarRepository.Repository.cs` (`Patch`) | read-modify-write; verify `$unset`/`$inc` | extend if missing |
| SQLite | `Saturn.Data.Sqlite/.../SqliteRepository.Patch.cs` | `$set`/`$inc`/`$unset` already specified | none; add a test |

Requirements when extending LiteDbX/StellarDB:

- Parse the document as a JSON object.
- `$set`: resolve the dotted path (nested objects; array index for `a.3`) and assign.
- `$unset`: remove the property.
- `$inc`: add the numeric delta to the existing numeric value.
- Bump the version exactly once per patch.
- Enforce `expectedVersion` (0 rows / mismatch → `FailedToUpdateException`).
- `$addToSet`/`$pull`: `NotSupportedException` outside Mongo in v1.

Do not change Mongo's existing behavior.

---

## Task E.5 — Change-feed payload mode

- Extend `FeedPayloadMode` with `Patch`.
- In `ChangeFeedBehavior`, when the mode is `Patch` and the operation is update/patch/increment, include the rendered update document in the feed item (`ItemsJson`).
- Keep `Outbox`/`Queue` sinks unchanged.

Verify with a change-feed test asserting the payload contains `$set`.

---

## Task E.6 — Tests

`Saturn.Data.ChangeTracking.Tests`:

- `UpdateDocument_Scalar_Set`
- `UpdateDocument_Unset`
- `UpdateDocument_Increment`
- `UpdateDocument_WriteOnly_Excluded_By_Default`
- `UpdateDocument_WriteOnly_Included_When_Filter_Allows`
- `UpdateDocument_WholeArray_Replaces_Array`
- `UpdateDocument_IndexedOps_Removes_In_Descending_Index_Order`
- `UpdateDocument_NoOp_Is_Empty_Object`

Add provider end-to-end tests (in each provider's test project, or as new `Saturn.Data.Testing.Shared` contract tests):

```csharp
[Fact]
public async Task Patch_From_ChangeSet_Updates_Field_And_Bumps_Version()
{
    var entity = new PatchEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "before", Count = 1 };
    await Repo.Insert(entity);

    entity = await Repo.ById<PatchEntity>(entity.Id);
    entity.BeginTracking();
    entity.Name = "after";
    entity.Count = 5;

    await Repo.PatchChanges<PatchEntity>(entity.Id, entity.Version, entity.GetChangeSet());

    var reloaded = await Repo.ById<PatchEntity>(entity.Id);
    Assert.Equal("after", reloaded.Name);
    Assert.Equal(5, reloaded.Count);
    Assert.True(reloaded.Version > entity.Version);
}
```

Add a stale-version test asserting `FailedToUpdateException`.

---

## Task E.7 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.ChangeTracking\GoLive.Saturn.Data.ChangeTracking.Tests\GoLive.Saturn.Data.ChangeTracking.Tests.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Data.MongoDb\Saturn.Data.MongoDb.Tests\Saturn.Data.MongoDb.Tests.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Data.LiteDbX\Saturn.Data.LiteDbX.Tests\Saturn.Data.LiteDbX.Tests.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Stellar\Saturn.Data.Stellar.Tests\Saturn.Data.Stellar.Tests.csproj"
```

---

## Do NOT

- Do not add a reference from `GoLive.Saturn.Data.Abstractions` to `GoLive.Saturn.Data.ChangeTracking`.
- Do not invent a new wire format; target the existing `Patch(jsonDocument)` contract.
- Do not change Mongo's existing `Patch` semantics; only add tests.
- Do not emit `$addToSet`/`$pull` outside Mongo in v1.
- Do not include `Id`, `Version`, or `WriteOnly` fields in a client-facing document.
- Do not add comments to `.cs` files.
