# Phase 3 — Scoped Variants

**Goal:** Implement all ten scoped repository interfaces by composing a scope predicate onto the unscoped core, using **`ScopeId`/`SecondScopeId` string predicates** (never `Ref<T>` equality).

**Prerequisite:** Phase 2 complete; unscoped tests green.

**Exit criteria:**
- `ScopedTests`, `ComprehensiveScopedTests` and `ScopedReadonlyContinuationTests` green on SQLite.
- `ScopedEntity<T>` writes set the scope via `ScopeModelHelper` and reads filter on `ScopeId`.
- A scope predicate appears in `ToQueryString()` as a filter over the serialized `ScopeId` field (spot-check one query).

**Why this differs from the SQLite provider.** The SQLite provider's `CombineScope` calls `ScopeModelHelper.BuildScopePredicate<TItem>(scope)`, which for a `ScopedEntity<T>` produces `entity.Scope == scope` — an equality over `Ref<T>`. That works because its translator resolves `Ref<T>` to the `_scope` column. Shiny.DocumentDb's translator sees `Ref<T>` as an object, so this plan builds the predicate directly over the **string** `ScopeId` property instead. `ScopeModelHelper` is still used for **writing** (`SetScope`/`SetSecondScope`), because that handles all the ref variants.

---

## Task 3.1 — Create the scope predicate helpers

`...\GoLive.Saturn.Data.DocumentDb\DocumentDbRepository.ScopedOperations.cs` — start by copying `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.ScopedOperations.cs`, change the namespace and class name.

Then **replace** the two `CombineScope` / `CombineSecondScope` methods with:

```csharp
    private static Expression<Func<TItem, bool>> CombineScope<TItem>(string scope, Expression<Func<TItem, bool>> predicate)
        where TItem : Entity, IScopedById
    {
        var scopePredicate = BuildScopePredicate<TItem>(scope);

        return predicate is null ? scopePredicate : scopePredicate.And(predicate);
    }

    private static Expression<Func<TItem, bool>> CombineSecondScope<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate)
        where TItem : Entity, ISecondScopedById
    {
        var scoped = BuildScopePredicate<TItem>(primaryScope).And(BuildSecondScopePredicate<TItem>(secondScope));

        return predicate is null ? scoped : scoped.And(predicate);
    }

    protected static Expression<Func<TItem, bool>> BuildScopePredicate<TItem>(string scope) where TItem : Entity, IScopedById
    {
        var parameter = Expression.Parameter(typeof(TItem), "item");
        var property = Expression.Property(parameter, nameof(IScopedById.ScopeId));
        var body = Expression.Equal(property, Expression.Constant(scope));

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    protected static Expression<Func<TItem, bool>> BuildSecondScopePredicate<TItem>(string secondScope) where TItem : Entity, ISecondScopedById
    {
        var parameter = Expression.Parameter(typeof(TItem), "item");
        var property = Expression.Property(parameter, nameof(ISecondScopedById.SecondScopeId));
        var body = Expression.Equal(property, Expression.Constant(secondScope));

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }
```

Keep every other method in the file (`ScopedAllAsync`, `ScopedManyAsync`, `ScopedByIdAsync`, `ScopedByIdsAsync`, `ScopedCountAsync`, `ScopedQueryable`, `ScopedOneAsync`, `ScopedRandomAsync`, `ScopedDeleteAsync`, `ScopedDeleteByIdAsync`, `ScopedDeleteByIdsAsync`, `ScopedInsertAsync`, `ScopedInsertManyAsync`, `ScopedSaveAsync`, `ScopedSaveManyAsync`, `ScopedUpdateAsync`, `ScopedUpdateWhereAsync`, `ScopedUpdateManyAsync`, `ScopedUpsertAsync`, `ScopedUpsertManyAsync`, `ScopedJsonUpdateAsync`, and the `SecondScoped*` cores) unchanged except where they reference the two replaced methods.

Add `using GoLive.Saturn.Data.Entities;` (for `IScopedById`, `ISecondScopedById`) if not already present.

> `ScopeModelHelper.SetScope(entity, scope)` and `ScopeModelHelper.SetSecondScope(entity, secondScope)` are used unchanged in the write cores — do not replace those.

---

## Task 3.2 — Copy the ten scoped interface files

From `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\` to `...\GoLive.Saturn.Data.DocumentDb\`, copy and rename:

| Source (SQLite) | Target (DocumentDb) |
| --- | --- |
| `SqliteRepository.ScopedReadonly.cs` | `DocumentDbRepository.ScopedReadonly.cs` |
| `SqliteRepository.Scoped.cs` | `DocumentDbRepository.Scoped.cs` |
| `SqliteRepository.WeakScopedReadonly.cs` | `DocumentDbRepository.WeakScopedReadonly.cs` |
| `SqliteRepository.WeakScoped.cs` | `DocumentDbRepository.WeakScoped.cs` |
| `SqliteRepository.SecondScopedReadonly.cs` | `DocumentDbRepository.SecondScopedReadonly.cs` |
| `SqliteRepository.SecondScoped.cs` | `DocumentDbRepository.SecondScoped.cs` |
| `SqliteRepository.WeakSecondScopedReadonly.cs` | `DocumentDbRepository.WeakSecondScopedReadonly.cs` |
| `SqliteRepository.WeakSecondScoped.cs` | `DocumentDbRepository.WeakSecondScoped.cs` |
| `SqliteRepository.TransparentScopedReadonly.cs` | `DocumentDbRepository.TransparentScopedReadonly.cs` |
| `SqliteRepository.TransparentScoped.cs` | `DocumentDbRepository.TransparentScoped.cs` |

For each file:

1. Change the namespace to `Saturn.Data.DocumentDb`.
2. Change `SqliteRepository` to `DocumentDbRepository` in the class declaration.
3. Change `SqliteRepositoryOptions` references (if any) to `DocumentDbRepositoryOptions`.
4. Leave every method signature **identical** — these implement the interface verbatim.
5. Leave the bodies calling the `Scoped*Async` cores unchanged.

The `TransparentScoped` files read `options.TransparentScopeProvider`. That property lives on `RepositoryOptions`, which this provider receives unchanged, so the code compiles as-is.

---

## Task 3.3 — Tests

Port these three files from `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\`, changing only the namespace and type names:

| Source | Target | Change |
| --- | --- | --- |
| `ScopedTests.cs` | `...\ScopedTests.cs` | `ScopedRepositoryContractTests<DatabaseFixture, UnitTestableDocumentDbRepository>` |
| `ComprehensiveScopedTests.cs` | `...\ComprehensiveScopedTests.cs` | `ComprehensiveScopedRepositoryContractTests<DatabaseFixture, UnitTestableDocumentDbRepository>` |
| `ScopedReadonlyContinuationTests.cs` | `...\ScopedReadonlyContinuationTests.cs` | `DatabaseFixture`, `UnitTestableDocumentDbRepository` |

In `ScopedReadonlyContinuationTests`, keep the `DisposeAsync` cleanup using `HardDelete`.

---

## Task 3.4 — Scope-membership note (do not implement yet)

`MultiscopedEntity<T>.Scopes` membership queries (`Scopes.Contains(scope)`) are **not** part of the scoped read interfaces — they are used only by cascade and by explicit user predicates. Cascade is Phase 6, where membership is implemented with a capability check because `Any`/array-unnest is unsupported on MariaDB. Do not add `Scopes` filtering here.

---

## Task 3.5 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "...\GoLive.Saturn.Data.DocumentDb.Tests.csproj"
```

If a scoped test returns nothing, check the stored body contains a `ScopeId` field — `ScopedEntity<T>.ScopeId` is a public property, so it must serialize. If it does not, the serializer resolver is excluding it (it should not).

---

## Do NOT

- Do not build scope predicates from `Ref<T>` equality or from `entity.Scope`.
- Do not use Shiny.DocumentDb's tenancy features.
- Do not add `Scopes` membership filtering in this phase.
- Do not change any scoped method signature.
- Do not add comments to `.cs` files.
