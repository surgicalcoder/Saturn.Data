# Phase 1 — Serialization, Storage and Core CRUD

**Goal:** Implement the unscoped `IReadonlyRepository` + `IRepository` surface on `Shiny.DocumentDb` so `BasicRepositoryContractTests` passes on the SQLite backend.

**Prerequisite:** Phase 0 complete; `ProviderApiTests` and `SmokeTests` green; the probe results recorded.

**Exit criteria:**
- `BasicTests` (subclass of `BasicRepositoryContractTests`) green on SQLite: `Add_And_Get_By_Id`, `Update`, `Save_And_Upsert_Logic`.
- `ProviderSpecificTests` green: the stored JSON excludes `Changes`/`EnableChangeTracking`/`_shortId`; a 24-hex Id round-trips byte-for-byte.
- Soft-delete filtering works for `ISoftDeletable` entities; `Delete` on a non-soft-deletable entity physically removes it.

**Copy, don't invent.** For every interface method, copy the signature **verbatim** from the SQLite provider's corresponding file and from `IReadonlyRepository.cs` / `IRepository.cs`. Only the bodies change.

---

## Task 1.1 — Serialization: copy and extend the SQLite files

Copy these six files from `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Serialization\` to `D:\Work\Saturn.Data\Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb\Serialization\`:

```powershell
$src = "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Serialization"
$dst = "D:\Work\Saturn.Data\Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb\Serialization"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item "$src\RefJsonConverter.cs" $dst
Copy-Item "$src\WeakRefJsonConverter.cs" $dst
Copy-Item "$src\PropertiesJsonConverter.cs" $dst
Copy-Item "$src\EntityJsonTypeInfoResolver.cs" $dst
Copy-Item "$src\EntityJsonSerializerOptions.cs" $dst
Copy-Item "$src\EntityJsonSerializer.cs" $dst
```

Then in **every copied file** replace the namespace `Saturn.Data.Sqlite.Serialization` with `Saturn.Data.DocumentDb.Serialization`. Change nothing else in `RefJsonConverter`, `WeakRefJsonConverter`, `PropertiesJsonConverter`, `EntityJsonSerializerOptions`.

In `EntityJsonTypeInfoResolver.cs`, keep the transient-member exclusion (`EnableChangeTracking`, `Changes`, `_shortId`) and add a factory that **combines** the modifier with a consumer-supplied resolver. Replace the `Create()` method with:

```csharp
    public static IJsonTypeInfoResolver Create(IJsonTypeInfoResolver additional = null)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(Modify);

        return additional is null
            ? resolver
            : JsonTypeInfoResolver.Combine(resolver, additional);
    }
```

Add `using System.Text.Json.Serialization.Metadata;` if it is not already present.

In `EntityJsonSerializer.cs`, change the options construction so it accepts an additional resolver and a `JsonSerializerContext`:

```csharp
    public EntityJsonSerializer(EntityJsonSerializerOptions serializerOptions = null, IJsonTypeInfoResolver additionalResolver = null)
    {
        serializerOptions ??= new EntityJsonSerializerOptions();

        options = new JsonSerializerOptions
        {
            WriteIndented = serializerOptions.WriteIndented,
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            TypeInfoResolver = EntityJsonTypeInfoResolver.Create(additionalResolver)
        };

        options.Converters.Add(new RefJsonConverterFactory());
        options.Converters.Add(new WeakRefJsonConverterFactory());
        options.Converters.Add(new PropertiesJsonConverter());

        if (serializerOptions.EnumAsString)
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }
    }
```

> **Why this matters:** the library requires the serializer and the expression visitor to share one `JsonSerializerOptions`. These options are handed to `DocumentStoreOptions.JsonSerializerOptions` in Task 1.2, so both see the same `Ref<T>`/`WeakRef`/`Properties` converters and the same transient-member exclusions.

---

## Task 1.2 — Wire the serializer into the store and add the write pipeline

Edit `DocumentDbRepository.cs`.

**a. Create the serializer before the store and pass its options to `DocumentStoreOptions`.** Replace the constructor body with:

```csharp
    public DocumentDbRepository(RepositoryOptions options, DocumentDbRepositoryOptions documentDbOptions)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.documentDbOptions = documentDbOptions ?? throw new ArgumentNullException(nameof(documentDbOptions));

        var additionalResolver = documentDbOptions.JsonSerializerContext is not null
            ? documentDbOptions.JsonSerializerContext
            : documentDbOptions.AdditionalTypeInfoResolver;

        serializer = new EntityJsonSerializer(documentDbOptions.Serializer, additionalResolver);

        if (documentDbOptions.Store is not null)
        {
            store = documentDbOptions.Store;
            ownsStore = false;
        }
        else if (documentDbOptions.ConfigureStore is not null)
        {
            var storeOptions = new DocumentStoreOptions
            {
                TableName = documentDbOptions.DefaultTableName,
                JsonSerializerOptions = serializer.JsonOptions
            };

            documentDbOptions.ConfigureStore(storeOptions);

            if (!documentDbOptions.UseReflectionFallback)
            {
                storeOptions.UseReflectionFallback = false;
            }

            store = new DocumentStore(storeOptions);
            ownsStore = true;
        }
        else
        {
            throw new InvalidOperationException("DocumentDbRepositoryOptions requires either Store or ConfigureStore.");
        }
    }
```

Add the field:

```csharp
    private readonly EntityJsonSerializer serializer;
```

and the property:

```csharp
    protected EntityJsonSerializer Serializer => serializer;
```

Add `using Saturn.Data.DocumentDb.Serialization;` and `using System.Text.Json.Serialization;`.

**b. Add the write-behaviour pipeline by copying it from the SQLite provider.** Copy these members verbatim from `SqliteRepository.cs` into `DocumentDbRepository.cs` (they are provider-agnostic):

- `HasWriteBehaviors`
- `BuildWriteContext<TItem>(...)`
- `DispatchWriteBehaviorsAsync<TItem>(...)`
- `ApplyAfterBehaviors<TItem>(...)`
- `ApplyOnWriteFailed<TItem>(...)`
- `BuildWriteResult<TItem>(...)`

Change only the serializer references if any, and keep the `using GoLive.Saturn.Data.Abstractions;` / `System.Linq.Expressions` usings.

**c. Add the storage helpers** to `DocumentDbRepository.cs`:

```csharp
    protected IQueryable<TItem> QueryFor<TItem>() where TItem : Entity => Store.Query<TItem>();

    protected Expression<Func<TItem, bool>> NotDeletedPredicate<TItem>() where TItem : Entity
    {
        var parameter = Expression.Parameter(typeof(TItem), "item");
        var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var body = Expression.Equal(property, Expression.Constant(false));

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    protected Expression<Func<TItem, bool>> WithSoftDeleteFilter<TItem>(Expression<Func<TItem, bool>> predicate, bool includeDeleted) where TItem : Entity
        => !includeDeleted && SupportsSoftDelete<TItem>()
            ? predicate is null ? NotDeletedPredicate<TItem>() : predicate.And(NotDeletedPredicate<TItem>())
            : predicate;

    protected static void EnsureId<TItem>(TItem entity) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityIdGenerator.GenerateNewId();
        }
    }

    protected async Task<bool> ExistsAsync<TItem>(string id, CancellationToken cancellationToken) where TItem : Entity
    {
        var normalized = NormalizeId(id);

        return normalized is not null && await Store.Get<TItem>(normalized).ConfigureAwait(false) is not null;
    }

    protected async Task<List<string>> MatchingIdsAsync<TItem>(Expression<Func<TItem, bool>> predicate, CancellationToken cancellationToken) where TItem : Entity
    {
        var matches = await Store.Query<TItem>().Where(predicate).ToList().ConfigureAwait(false);

        return matches.Select(entity => entity.Id).ToList();
    }
```

> `Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted))` resolves because `SupportsSoftDelete<TItem>()` has already been checked by the caller. `predicate.And(other)` is the `PredicateHelper` extension from `GoLive.Saturn.Data.Abstractions`.

---

## Task 1.3 — Create `DocumentDbRepository.ReadonlyRepository.cs`

Copy `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.ReadonlyRepository.cs` to `...\GoLive.Saturn.Data.DocumentDb\DocumentDbRepository.ReadonlyRepository.cs`. Change the namespace to `Saturn.Data.DocumentDb`, the class name to `DocumentDbRepository`, and keep the class declaration `public partial class DocumentDbRepository : IReadonlyRepository`.

**Delete** the SQL-specific helpers (`LoadListAsync`, `CloneParameter`, `BuildOrderBy` SQL fragment version, `BuildWhereClausePredicate`'s expression-builder is reusable — keep it if it compiles, otherwise replace it with the version below).

Then implement each interface method with the mapping below. Signatures must match the interface exactly.

| Method | Body |
| --- | --- |
| `All<TItem>(includeDeleted, tx, ct)` | `var list = await QueryFor<TItem>().Where(WithSoftDeleteFilter<TItem>(null, includeDeleted)).ToList(); return AsyncEnumerableFactory.From(list, ct);` |
| `ById<TItem>(id, includeDeleted, tx, ct)` | `normalized = NormalizeId(id)`; if null return `null!`; `var entity = await Store.Get<TItem>(normalized);` if `entity is null` return `null!`; if `!includeDeleted && SupportsSoftDelete<TItem>() && entity is ISoftDeletable { IsDeleted: true }` return `null!`; return entity |
| `ById<TItem>(IDs, includeDeleted, tx, ct)` | `normalized = NormalizeEntityIds(IDs)`; if empty return empty async; `var list = await QueryFor<TItem>().WhereIn(item => item.Id, normalized).ToList();` then filter soft-deleted the same way as `ById`, return `AsyncEnumerableFactory.From(list, ct)` |
| `Count<TItem>(predicate, continueFrom, includeDeleted, tx, ct)` | build `var effective = WithSoftDeleteFilter(predicate, includeDeleted) ?? (item => true);` then apply continuation (Phase 2 — for Phase 1 ignore `continueFrom`); `return await QueryFor<TItem>().Where(effective).Count();` |
| `Many<TItem>(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, tx, ct)` | `effective = WithSoftDeleteFilter(predicate, includeDeleted)`; `query = QueryFor<TItem>().Where(effective)`; apply sorting (Phase 2 — Phase 1 may ignore `sortOrders`); `skip = pageNumber > 1 && pageSize.HasValue ? (pageNumber.Value - 1) * pageSize.Value : 0`; if `pageSize.HasValue` → `query = query.Paginate(skip, pageSize.Value)`; `var list = await query.ToList(); return AsyncEnumerableFactory.From(list, ct);` |
| `Many<TItem>(whereClause, ..., includeDeleted, ...)` | convert the dictionary to a predicate with `BuildWhereClausePredicate<TItem>` then delegate to the predicate overload |
| `One<TItem>(predicate, continueFrom, sortOrders, includeDeleted, tx, ct)` | `effective = WithSoftDeleteFilter(predicate, includeDeleted)`; `return await QueryFor<TItem>().Where(effective).FirstOrDefault();` (return `null!` when absent) |
| `Random<TItem>(...)` | **Phase 2** — leave a `throw new NotSupportedException("Implemented in Phase 2.")` for now |

Add these helpers to the file:

```csharp
    private async Task<TItem> GetFilteredByIdAsync<TItem>(string id, bool includeDeleted, CancellationToken cancellationToken) where TItem : Entity
    {
        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            return null;
        }

        var entity = await Store.Get<TItem>(normalized).ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        if (!includeDeleted && SupportsSoftDelete<TItem>() && entity is ISoftDeletable { IsDeleted: true })
        {
            return null;
        }

        return entity;
    }

    private static Expression<Func<TItem, bool>> BuildWhereClausePredicate<TItem>(Dictionary<string, object> whereClause) where TItem : Entity
    {
        if (whereClause is null || whereClause.Count == 0)
        {
            return item => true;
        }

        var parameter = Expression.Parameter(typeof(TItem), "item");
        Expression body = null;

        foreach (var pair in whereClause)
        {
            if (pair.Value is null)
            {
                continue;
            }

            var property = parameter.Type.GetProperty(pair.Key);

            if (property is null)
            {
                throw new NotSupportedException($"Unknown property '{pair.Key}' on '{parameter.Type.Name}'.");
            }

            var member = Expression.Property(parameter, property);

            var constant = property.PropertyType == typeof(string)
                ? Expression.Constant(Convert.ToString(pair.Value))
                : Expression.Convert(Expression.Constant(pair.Value), property.PropertyType);

            var comparison = Expression.Equal(member, constant);
            body = body is null ? comparison : Expression.AndAlso(body, comparison);
        }

        return body is null ? item => true : Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }
```

> `AsyncEnumerableFactory` — copy it from `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\AsyncEnumerableFactory.cs` into the new project root, namespace `Saturn.Data.DocumentDb`.

---

## Task 1.4 — Create `DocumentDbRepository.Repository.cs`

Copy `SqliteRepository.Repository.cs` to `...\DocumentDbRepository.Repository.cs`, change namespace/class name, keep `public partial class DocumentDbRepository : IRepository`, and delete the SQL templates (`InsertSqlTemplate`, `ExecuteInsertAsync`, `ExecuteUpsertAsync`, `ExecuteUpdateByIdAsync`, `ExecuteUpdateWhereAsync`, `ExistsByIdAsync`, `SelectIdsAsync`, `ExecuteSoftDeleteAsync`, `ExecuteRestoreAsync`, `ExecuteHardDeleteAsync`) — the helpers added in Task 1.2 replace them.

Implement each interface member as follows. Signatures verbatim from `IRepository.cs`.

| Method | Body |
| --- | --- |
| `Insert<TItem>(entity, tx, ct)` | `EnsureId(entity)`; build context (`RepositoryWriteOperation.Insert`); dispatch before; `await Store.Insert(entity)`; after |
| `Insert<TItem>(entities, tx, ct)` | materialise list; `list.ForEach(EnsureId)`; empty → return; one context with `items:`; `await Store.BatchInsert(list)`; after with all ids |
| `Save<TItem>(entity, tx, ct)` | `EnsureId(entity)`; `existed = await ExistsAsync<TItem>(entity.Id, ct)`; `await Store.Upsert(entity)`; `wasCreated: !existed` |
| `Save<TItem>(entities, tx, ct)` | list; `EnsureId` each; `await Store.BatchUpsert(list)`; after |
| `Upsert<TItem>(entity...)` | identical to `Save` but with `RepositoryWriteOperation.Upsert` |
| `Upsert<TItem>(entities...)` | identical to batch `Save` but with `RepositoryWriteOperation.Upsert` |
| `Update<TItem>(entity, tx, ct)` | `EnsureId`; `existed = await ExistsAsync<TItem>(entity.Id, ct)`; if `!existed` throw `new FailedToUpdateException()`; `await Store.Update(entity)`; after |
| `Update<TItem>(conditionPredicate, entity, tx, ct)` | `combined = conditionPredicate.And(item => item.Id == entity.Id)`; `match = await QueryFor<TItem>().Where(combined).FirstOrDefault()`; if `match is null` throw `new FailedToUpdateException()`; `await Store.Update(entity)`; after |
| `Update<TItem>(entities, tx, ct)` | list; for each: existence probe then `Store.Update`; any miss → `FailedToUpdateException` |
| `Delete<TItem>(filter, tx, ct)` | `matches = await MatchingIdsAsync<TItem>(filter, ct)`; if `SupportsSoftDelete<TItem>()` → for each matched id: `entity = await Store.Get<TItem>(id)`; set `IsDeleted = true`, `DeletedAt = DateTimeOffset.UtcNow`, `DeletedBy = string.Empty` via `ISoftDeletable`; `await Store.Update(entity)`; else → `await Store.BatchRemove<TItem>(matches)`; after with `matches` |
| `Delete<TItem>(id, ...)` / `(IDs, ...)` | build `item => item.Id == normalized` / `item => normalized.Contains(item.Id)` and delegate to the filter overload |
| `HardDelete<TItem>(filter, ...)` | `matches = await MatchingIdsAsync<TItem>(filter, ct)`; `await Store.BatchRemove<TItem>(matches)`; after |
| `HardDelete<TItem>(id)` / `(IDs)` | as `Delete`'s id overloads, delegating to `HardDelete(filter, ...)` |
| `Restore<TItem>(filter/id/IDs, ...)` | require `SupportsSoftDelete<TItem>()` else `NotSupportedException`; load each match, clear `IsDeleted`/`DeletedAt`, `Store.Update` |
| `CreateTransaction()` | Phase 5 — `throw new NotSupportedException("Implemented in Phase 5.")` |
| `Patch`/`JsonUpdate`/`Increment` | Phase 4 — `throw new NotSupportedException("Implemented in Phase 4.")` |
| `DeleteCascade`/`HardDeleteCascade` | Phase 6 — `throw new NotSupportedException("Implemented in Phase 6.")` |

Casting to the interface for soft-delete flag writes:

```csharp
    if (entity is ISoftDeletable deletable)
    {
        deletable.IsDeleted = true;
        deletable.DeletedAt = DateTimeOffset.UtcNow;
        deletable.DeletedBy = string.Empty;
    }
```

> **Known limitation to record in the README:** Phase 1's soft delete is read-modify-write (one `Update` per matched document) rather than set-based. Phase 4 may replace it with `ExecuteUpdate` when `Capabilities.SupportsExecuteUpdate`. Contract-test volumes are small, so correctness comes first.

---

## Task 1.5 — Tests

`...\Saturn.Data.DocumentDb.Tests\BasicTests.cs`:

```csharp
using Saturn.Data.Testing.Shared;

namespace Saturn.Data.DocumentDb.Tests;

public class BasicTests(DatabaseFixture fixture)
    : BasicRepositoryContractTests<DatabaseFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DatabaseFixture>;
```

`...\ProviderSpecificTests.cs`:

```csharp
using System.Text.Json.Nodes;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.DocumentDb.Tests;

public class ProviderSpecificTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
    }

    [Fact]
    public async Task Stored_Document_Excludes_Transient_Members()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "doc-shape" };
        await fixture.Repository.Insert(entity);

        var raw = await fixture.Repository.Store.Collection<BasicEntity>().Get(entity.Id);

        Assert.NotNull(raw);
        Assert.False(raw!.ContainsKey("Changes"));
        Assert.False(raw.ContainsKey("EnableChangeTracking"));
        Assert.False(raw.ContainsKey("_shortId"));
        Assert.True(raw.ContainsKey("Name"));
    }

    [Fact]
    public async Task Id_Is_Stored_As_The_Original_24Hex_String()
    {
        var id = EntityIdGenerator.GenerateNewId();
        await fixture.Repository.Insert(new BasicEntity { Id = id, Name = "id-shape" });

        var fetched = await fixture.Repository.ById<BasicEntity>(id);

        Assert.NotNull(fetched);
        Assert.Equal(id, fetched!.Id);
    }
}
```

> If `Collection<BasicEntity>()` is unavailable (Phase 0 probe decides), replace the first test's raw read with `Store.GetDiff(id, entity) is null` — that returns `null` for an unchanged document, which does not prove the shape. In that case, keep the round-trip assertion only and move the shape assertion to a JSON-lane test that skips when `Capabilities.SupportsJsonLane` is false.

---

## Task 1.6 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "...\GoLive.Saturn.Data.DocumentDb.Tests.csproj"
```

Expected: `BasicTests` (3 tests), `ProviderSpecificTests` (2 tests), plus the Phase 0 suites, all green.

If `Save_And_Upsert_Logic` fails because the count is 2, the upsert is inserting rather than merging — verify `Store.Upsert(entity)` is the merge path, and that the Id assigned by `EnsureId` is the same on both calls.

---

## Do NOT

- Do not add per-type `ConfigureDocument` calls (`MapVersionProperty`, `AddSoftDelete`, `MapUniqueIndex`) — core semantics are predicate-based.
- Do not implement query translation, sorting, `continueFrom`, `Random`, `Patch`, `Increment`, transactions, cascade or change feed in this phase.
- Do not bypass `Entity.TryParseId` normalisation.
- Do not convert Ids to `Guid`.
- Do not add comments to `.cs` files.
