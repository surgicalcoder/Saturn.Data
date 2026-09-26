# Phase 3 — Scoped Variants

**Goal:** Implement all scoped repository interfaces by composing a scope predicate onto the unscoped core. No new SQL engine work is required; scope filtering resolves to the `_scope` / `_scope2` shadow columns.

**Prerequisite:** Phase 2 complete and all tests green.

**Exit criteria:**
- `ScopedTests`, `ComprehensiveScopedTests`, and `ScopedReadonlyContinuationTests` pass.
- The class implements: `IScopedRepository`, `IScopedReadonlyRepository`, `ISecondScopedRepository`, `ISecondScopedReadonlyRepository`, `IWeakScopedRepository`, `IWeakScopedReadonlyRepository`, `IWeakSecondScopedRepository`, `IWeakSecondScopedReadonlyRepository`, `ITransparentScopedRepository`, `ITransparentScopedReadonlyRepository`.

**Key decisions:**
- Use `GoLive.Saturn.Data.Abstractions.ScopeModelHelper` for **all** scope predicate building and scope setting (`SetScope`, `SetSecondScope`, `BuildScopePredicate`, `BuildSecondScopePredicate`). This works uniformly for `Ref<T>`, `WeakRef`, `WeakRef<T>`, and `IScopedById` string implementations, so strong and weak variants share the same code shape.
- Compose with `PredicateHelper.And` (the `.And(...)` extension).
- Scope reads/writes delegate to the unscoped methods with a combined predicate.

---

## Task 3.1 — Map scope members to shadow columns in the resolver

Edit `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Query\SqliteJsonPathResolver.cs`. In `TryResolve`, immediately before the generic fallback line `path = "$." + string.Join(".", segments);`, insert:

```csharp
        if (segments.Count == 1 && segments[0] == "Scope")
        {
            path = "_scope";
            isColumn = true;
            return true;
        }

        if (segments.Count == 1 && segments[0] == "SecondScope")
        {
            path = "_scope2";
            isColumn = true;
            return true;
        }

        if (segments.Count == 1 && segments[0] == "ScopeId")
        {
            path = "_scope";
            isColumn = true;
            return true;
        }

        if (segments.Count == 1 && segments[0] == "SecondScopeId")
        {
            path = "_scope2";
            isColumn = true;
            return true;
        }
```

This makes scope predicates use `_scope` / `_scope2` (indexed) instead of `json_extract(_doc,'$.Scope')`.

---

## Task 3.2 — Create `SqliteRepository.ScopedReadonly.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.ScopedReadonly.cs`

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IScopedReadonlyRepository
{
    public Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => All<TItem, TScope>(scope, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var predicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return await Many<TItem>(predicate, continueFrom: null, pageSize: null, pageNumber: null, sortOrders: null, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
    }

    public Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ById<TItem, TScope>(scope, id, includeDeleted: false, transaction, cancellationToken);

    public async Task<TItem> ById<TItem, TScope>(string scope, string id, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            return null;
        }

        var predicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combined = predicate.And(item => item.Id == normalized);
        return await One<TItem>(combined, continueFrom: null, sortOrders: null, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ById<TItem, TScope>(scope, IDs, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var normalized = NormalizeEntityIds(IDs);

        if (normalized.Count == 0)
        {
            return AsyncEnumerableFactory.From(Array.Empty<TItem>(), cancellationToken);
        }

        var predicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combined = predicate.And(item => normalized.Contains(item.Id));
        return await Many<TItem>(combined, continueFrom: null, pageSize: normalized.Count, pageNumber: null, sortOrders: null, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
    }

    public Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => Count<TItem, TScope>(scope, predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);

    public async Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(scope).And(predicate);
        return await Count<TItem>(combined, continueFrom, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
    }

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => IQueryable<TItem, TScope>(scope, includeDeleted: false);

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope, bool includeDeleted)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var predicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope).Compile();
        return IQueryable<TItem>(includeDeleted).Where(predicate);
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => Many<TItem, TScope>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(scope).And(predicate);
        return await Many<TItem>(combined, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => Many<TItem, TScope>(scope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(scope).And(BuildWhereClausePredicate<TItem>(whereClause));
        return await Many<TItem>(combined, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
    }

    public Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => One<TItem, TScope>(scope, predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(scope).And(predicate);
        return await One<TItem>(combined, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
    }

    public Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => Random<TItem, TScope>(scope, predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, int count, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combined = predicate is null ? scopePredicate : scopePredicate.And(predicate);
        return await Random<TItem>(combined, continueFrom, count, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
    }
}
```

---

## Task 3.3 — Create `SqliteRepository.Scoped.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.Scoped.cs`

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IScopedRepository
{
    public async Task Delete<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            return;
        }

        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(scope).And(item => item.Id == normalized);
        await Delete<TItem>(combined, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => await Delete<TItem>(ScopeModelHelper.BuildScopePredicate<TItem>(scope).And(filter), transaction, cancellationToken).ConfigureAwait(false);

    public async Task Delete<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var normalized = NormalizeEntityIds(IDs);

        if (normalized.Count == 0)
        {
            return;
        }

        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(scope).And(item => normalized.Contains(item.Id));
        await Delete<TItem>(combined, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task Insert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Insert<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task Insert<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var list = entities.ToList();
        list.ForEach(entity => ScopeModelHelper.SetScope(entity, scope));
        await Insert<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    public Task JsonUpdate<TItem, TScope>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => JsonUpdate<TItem>(id, version, json, transaction, cancellationToken);

    public async Task Save<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Save<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task Save<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var list = entities.ToList();
        list.ForEach(entity => ScopeModelHelper.SetScope(entity, scope));
        await Save<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task Update<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Update<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task Update<TItem, TScope>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(scope).And(conditionPredicate);
        await Update<TItem>(combined, entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task Update<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var list = entities.ToList();
        list.ForEach(entity => ScopeModelHelper.SetScope(entity, scope));
        await Update<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task Upsert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Upsert<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task Upsert<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        var list = entities.ToList();
        list.ForEach(entity => ScopeModelHelper.SetScope(entity, scope));
        await Upsert<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }
}
```

---

## Task 3.4 — Weak scoped variants

### 3.4.1 `SqliteRepository.WeakScopedReadonly.cs`

Copy `SqliteRepository.ScopedReadonly.cs` to `SqliteRepository.WeakScopedReadonly.cs`, then make these exact substitutions:

1. Class declaration: `public partial class SqliteRepository : IWeakScopedReadonlyRepository`.
2. Every method's generic constraints change from
   `where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()`
   to
   `where TItem : Entity, IScopedById, new()`.
3. Remove the `<TScope>` generic parameter from every method name (`All<TItem, TScope>` → `All<TItem>`, `ById<TItem, TScope>` → `ById<TItem>`, etc.).
4. `All<TItem>` and every other method keep the same bodies. `ScopeModelHelper.BuildScopePredicate<TItem>(scope)` already matches the `IScopedById` constraint.
5. `IQueryable<TItem>(string scope)` / `IQueryable<TItem>(string scope, bool includeDeleted)`.

### 3.4.2 `SqliteRepository.WeakScoped.cs`

Copy `SqliteRepository.Scoped.cs` to `SqliteRepository.WeakScoped.cs`, then make the same substitutions (class declares `IWeakScopedRepository`, constraints become `where TItem : Entity, IScopedById, new()`, drop `<TScope>`).

---

## Task 3.5 — Second scoped variants

### 3.5.1 `SqliteRepository.SecondScopedReadonly.cs`

Copy `SqliteRepository.ScopedReadonly.cs` to `SqliteRepository.SecondScopedReadonly.cs`, then:

1. Class declaration: `public partial class SqliteRepository : ISecondScopedReadonlyRepository`.
2. Method generic parameters become `<TItem, TSecondScope, TPrimaryScope>` and constraints:
   `where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()`.
3. Replace the scope argument list `(string scope, ...)` with `(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, ...)`.
4. Replace every `ScopeModelHelper.BuildScopePredicate<TItem>(scope)` with:
   ```csharp
   ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope.Id).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope.Id))
   ```
5. For methods that also combine a user predicate, keep `.And(predicate)` / `.And(BuildWhereClausePredicate<TItem>(whereClause))` appended after the two scope predicates.
6. `ById` uses `NormalizeId(id)` and `item.Id == normalized` as before.
7. `IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope)`.

### 3.5.2 `SqliteRepository.SecondScoped.cs`

Copy `SqliteRepository.Scoped.cs` to `SqliteRepository.SecondScoped.cs`, then:

1. Class declares `ISecondScopedRepository`.
2. Generic parameters `<TItem, TSecondScope, TPrimaryScope>` and the same constraints as 3.5.1.
3. Replace `(string scope, ...)` with `(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, ...)`.
4. Replace `ScopeModelHelper.SetScope(entity, scope)` with:
   ```csharp
   ScopeModelHelper.SetScope(entity, primaryScope.Id);
   ScopeModelHelper.SetSecondScope(entity, secondScope.Id);
   ```
5. Replace `ScopeModelHelper.BuildScopePredicate<TItem>(scope)` with:
   ```csharp
   ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope.Id).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope.Id))
   ```
6. `list.ForEach(entity => { ... })` for the two setter calls — write it as a block lambda, one statement per line.

---

## Task 3.6 — Weak second scoped variants

### 3.6.1 `SqliteRepository.WeakSecondScopedReadonly.cs`

Copy `SqliteRepository.SecondScopedReadonly.cs` to `SqliteRepository.WeakSecondScopedReadonly.cs`, then:

1. Class declares `IWeakSecondScopedReadonlyRepository`.
2. Constraints become `where TItem : Entity, ISecondScopedById, new()`.
3. Method signatures take `(string primaryScope, string secondScope, ...)` instead of `Ref<...>`.
4. Replace `primaryScope.Id` with `primaryScope` and `secondScope.Id` with `secondScope`.
5. `IQueryable<TItem>(string primaryScope, string secondScope)` / `(..., bool includeDeleted)`.

### 3.6.2 `SqliteRepository.WeakSecondScoped.cs`

Copy `SqliteRepository.SecondScoped.cs` to `SqliteRepository.WeakSecondScoped.cs`, then apply the same substitutions (2–4 from 3.6.1).

---

## Task 3.7 — Transparent scoped variants

### 3.7.1 `SqliteRepository.TransparentScopedReadonly.cs`

Copy `SqliteRepository.ScopedReadonly.cs` to `SqliteRepository.TransparentScopedReadonly.cs`, then:

1. Class declares `ITransparentScopedReadonlyRepository`.
2. Generic parameters become `<TItem, TParent>` with constraints
   `where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()`.
3. Remove the `string scope` parameter from every method. At the start of each method body, resolve the scope:
   ```csharp
   var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
   ```
   Then keep the rest of the body identical (it still uses `ScopeModelHelper.BuildScopePredicate<TItem>(scope)`).
4. `IQueryable<TItem, TParent>()` and `IQueryable<TItem, TParent>(bool includeDeleted)`.

If `options.TransparentScopeProvider` is `null`, throw `new InvalidOperationException("TransparentScopeProvider is not configured.")`.

### 3.7.2 `SqliteRepository.TransparentScoped.cs`

Copy `SqliteRepository.Scoped.cs` to `SqliteRepository.TransparentScoped.cs`, then:

1. Class declares `ITransparentScopedRepository`.
2. Same generic/constraint change and scope resolution as 3.7.1.
3. Remove the `string scope` parameter from every method; resolve `scope` at the top of each body.

---

## Task 3.8 — Tests

### 3.8.1 `ScopedTests.cs`

```csharp
using Saturn.Data.Testing.Shared;

namespace Saturn.Data.Sqlite.Tests;

public class ScopedTests(DatabaseFixture fixture)
    : ScopedRepositoryContractTests<DatabaseFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<DatabaseFixture>;
```

### 3.8.2 `ComprehensiveScopedTests.cs`

```csharp
using Saturn.Data.Testing.Shared;

namespace Saturn.Data.Sqlite.Tests;

public class ComprehensiveScopedTests(DatabaseFixture fixture)
    : ComprehensiveScopedRepositoryContractTests<DatabaseFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<DatabaseFixture>;
```

### 3.8.3 `ScopedReadonlyContinuationTests.cs`

Port the LiteDbX test file `D:\Work\Saturn.Data\Saturn.Data.LiteDbX\Saturn.Data.LiteDbX.Tests\ScopedReadonlyContinuationTests.cs` into `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\ScopedReadonlyContinuationTests.cs` with these changes:

1. Namespace `Saturn.Data.Sqlite.Tests`.
2. Use `DatabaseFixture` and `UnitTestableSqliteRepository`.
3. Replace `using Saturn.Data.LiteDbX.Tests.Entities;` with `using Saturn.Data.Testing.Shared.Entities;`.
4. Replace every `WELL_KNOWN.Parent_Scope_1` → `WellKnownData.ParentScope1`, `WELL_KNOWN.Parent_Scope_2` → `WellKnownData.ParentScope2`.
5. `DisposeAsync` should use `HardDelete`:
   ```csharp
   public async Task DisposeAsync()
   {
       await repo.HardDelete<BasicEntity>(entity => true);
       await repo.HardDelete<ChildEntity>(entity => true);
       await repo.HardDelete<ParentScope>(entity => true);
   }
   ```
6. Add `using Saturn.Data.Testing.Shared;` so `WellKnownData` resolves.

All other test bodies stay identical.

---

## Task 3.9 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj"
```

Expected: all previous tests plus `ScopedTests`, `ComprehensiveScopedTests`, `ScopedReadonlyContinuationTests` pass.

---

## Troubleshooting

- If you get `CS0121` ambiguous call errors, add explicit type arguments, e.g. `Many<TItem>(...)` rather than `Many(...)`.
- If `ScopeModelHelper.BuildScopePredicate` throws `InvalidOperationException` for `ChildEntity`, verify `ChildEntity` derives from `ScopedEntity<ParentScope>` and the resolver maps `Scope` to `_scope`.
- If scoped `Save`/`Upsert` does not move an entity between scopes, verify `ScopeModelHelper.SetScope` is called before delegating and that `Upsert` writes `_scope` from the document (the Phase 1 upsert SQL does).
- If `ComprehensiveScopedTests.Save_Should_Respect_Scope_Boundaries` fails with total count 2, the upsert is not replacing the row; confirm `ON CONFLICT(_id) DO UPDATE` and that `_scope` is updated in the conflict branch.

---

## Do NOT

- Do not add per-family duplicated SQL; all scoped methods must compose predicates and delegate to the unscoped methods.
- Do not modify `ScopeModelHelper` or any shared abstraction.
- Do not implement `Patch`/`Increment`/`JsonUpdate` bodies here (Phase 4).
- Do not add comments to `.cs` files.
- Do not change the unscoped method signatures.
