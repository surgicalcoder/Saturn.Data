# Phase 2 — Query Delegation, Sorting, Paging and Continuation

**Goal:** Route predicates to `Shiny.DocumentDb` with a capability gate and an optional in-memory fallback, and implement sorting, pagination, `continueFrom` and `Random`.

**Prerequisite:** Phase 1 complete; `BasicTests` green.

**Exit criteria:**
- `ContinuationTests` green: `Id`-ascending + `continueFrom` returns the next page; a non-`Id` sort ignores the token; a malformed token is ignored.
- `QueryBehaviourTests` green: `string.Contains`, `>`, `Enumerable.Contains`, and boolean members all translate through the library.
- With `UnsupportedPredicateBehaviour.FallbackToClient`, a predicate the backend rejects still returns correct results.
- With `UnsupportedPredicateBehaviour.Throw` (default), the same predicate throws with a message naming the backend and the feature.

---

## Task 2.1 — Create the query runner (the capability gate)

`...\GoLive.Saturn.Data.DocumentDb\Query\DocumentDbQueryRunner.cs`:

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb.Query;

internal sealed class DocumentDbQueryRunner
{
    private readonly IDocumentStore store;
    private readonly DocumentDbRepositoryOptions options;
    private readonly DocumentDbCapabilities capabilities;

    public DocumentDbQueryRunner(IDocumentStore store, DocumentDbRepositoryOptions options, DocumentDbCapabilities capabilities)
    {
        this.store = store;
        this.options = options;
        this.capabilities = capabilities;
    }

    public IQueryable<TItem> Begin<TItem>(Expression<Func<TItem, bool>> predicate) where TItem : Entity
    {
        var query = store.Query<TItem>();

        return predicate is null ? query : query.Where(predicate);
    }

    public async Task<List<TItem>> ToListAsync<TItem>(Expression<Func<TItem, bool>> predicate, string orderBy, bool descending, int? skip, int? take, CancellationToken cancellationToken) where TItem : Entity
    {
        try
        {
            return await ExecuteListAsync<TItem>(predicate, orderBy, descending, skip, take, cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException exception)
        {
            return await FallbackAsync<TItem>(predicate, orderBy, descending, skip, take, cancellationToken, exception).ConfigureAwait(false);
        }
    }

    public async Task<TItem> FirstOrDefaultAsync<TItem>(Expression<Func<TItem, bool>> predicate, string orderBy, bool descending, CancellationToken cancellationToken) where TItem : Entity
    {
        var list = await ToListAsync<TItem>(predicate, orderBy, descending, null, 1, cancellationToken).ConfigureAwait(false);

        return list.Count == 0 ? null : list[0];
    }

    public async Task<long> CountAsync<TItem>(Expression<Func<TItem, bool>> predicate, CancellationToken cancellationToken) where TItem : Entity
    {
        try
        {
            var query = store.Query<TItem>();

            return await (predicate is null ? query : query.Where(predicate)).Count().ConfigureAwait(false);
        }
        catch (NotSupportedException exception)
        {
            var list = await FallbackAsync<TItem>(predicate, null, false, null, null, cancellationToken, exception).ConfigureAwait(false);

            return list.Count;
        }
    }

    private async Task<List<TItem>> ExecuteListAsync<TItem>(Expression<Func<TItem, bool>> predicate, string orderBy, bool descending, int? skip, int? take, CancellationToken cancellationToken) where TItem : Entity
    {
        var query = store.Query<TItem>();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
        }

        if (take.HasValue)
        {
            query = query.Paginate(skip ?? 0, take.Value);
        }
        else if (skip.HasValue && skip.Value > 0)
        {
            query = query.Paginate(skip.Value, int.MaxValue);
        }

        return (await query.ToList().ConfigureAwait(false)).ToList();
    }

    private async Task<List<TItem>> FallbackAsync<TItem>(Expression<Func<TItem, bool>> predicate, string orderBy, bool descending, int? skip, int? take, CancellationToken cancellationToken, NotSupportedException exception) where TItem : Entity
    {
        if (options.UnsupportedPredicateBehaviour == UnsupportedPredicateBehaviour.Throw)
        {
            throw new NotSupportedException($"Shiny.DocumentDb backend '{capabilities.BackendName}' could not execute this query. Set DocumentDbRepositoryOptions.UnsupportedPredicateBehaviour to FallbackToClient to evaluate it in memory. Original: {exception.Message}", exception);
        }

        options.OnClientSideFallback?.Invoke($"Falling back to in-memory evaluation on '{capabilities.BackendName}': {exception.Message}");

        var all = (await store.Query<TItem>().ToList().ConfigureAwait(false)).ToList();

        IEnumerable<TItem> filtered = predicate is null ? all : all.Where(predicate.Compile());

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            filtered = OrderByPath(filtered, orderBy, descending);
        }

        if (skip.HasValue)
        {
            filtered = filtered.Skip(skip.Value);
        }

        if (take.HasValue)
        {
            filtered = filtered.Take(take.Value);
        }

        return filtered.ToList();
    }

    private static IEnumerable<TItem> OrderByPath<TItem>(IEnumerable<TItem> source, string path, bool descending)
    {
        var property = typeof(TItem).GetProperty(path);

        if (property is null)
        {
            return source;
        }

        return descending
            ? source.OrderByDescending(item => property.GetValue(item))
            : source.OrderBy(item => property.GetValue(item));
    }
}
```

> `query.OrderBy(string)` / `OrderByDescending(string)` is the library's AOT-safe dynamic-sort overload (it accepts dotted paths). If the probe shows it is named differently, fix it here and note it in the README. If the string overload is unavailable, replace `orderBy` with a typed `Expression<Func<TItem, object>>` and translate the path with reflection into a `MemberExpression` — that is the fallback, not the first choice.

---

## Task 2.2 — Create the member-path resolver

`...\GoLive.Saturn.Data.DocumentDb\Query\MemberPathResolver.cs`:

```csharp
using System.Linq.Expressions;

namespace Saturn.Data.DocumentDb.Query;

internal static class MemberPathResolver
{
    public static bool TryResolve(LambdaExpression lambda, out string path, out bool descendingHint)
    {
        descendingHint = false;
        path = null;

        if (lambda is null)
        {
            return false;
        }

        var body = lambda.Body;

        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        var segments = new List<string>();

        while (body is MemberExpression member)
        {
            segments.Insert(0, member.Member.Name);
            body = member.Expression;
        }

        if (body is not ParameterExpression)
        {
            return false;
        }

        if (segments.Count == 0)
        {
            return false;
        }

        path = string.Join(".", segments);
        return true;
    }
}
```

---

## Task 2.3 — Rewire the read methods

Add the runner to `DocumentDbRepository.cs`:

```csharp
    private Query.DocumentDbQueryRunner queryRunner;

    protected Query.DocumentDbQueryRunner QueryRunner => queryRunner;
```

Initialise it in `InitializeAsync` after capabilities are probed:

```csharp
            queryRunner = new Query.DocumentDbQueryRunner(store, documentDbOptions, capabilities);
```

Then update `DocumentDbRepository.ReadonlyRepository.cs`:

| Method | Body |
| --- | --- |
| `All<TItem>` | `var predicate = WithSoftDeleteFilter<TItem>(null, includeDeleted); var list = await QueryRunner.ToListAsync(predicate, null, false, null, null, ct); return AsyncEnumerableFactory.From(list, ct);` |
| `ById<TItem>(id, ...)` | keep the Phase 1 `GetFilteredByIdAsync` implementation |
| `ById<TItem>(IDs, ...)` | `normalized = NormalizeEntityIds(IDs)`; if empty return empty; `predicate = WithSoftDeleteFilter<TItem>(item => normalized.Contains(item.Id), includeDeleted)`; list; return async |
| `Count<TItem>` | `var predicate = WithSoftDeleteFilter(predicate, includeDeleted); return await QueryRunner.CountAsync(predicate, ct);` |
| `Many<TItem>(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, ...)` | `var effective = ApplyContinuation<TItem>(WithSoftDeleteFilter(predicate, includeDeleted), sortOrders, continueFrom);` `var orderBy = BuildOrderByPath(sortOrders, out var descending);` `int? limit = pageSize;` `int? offset = pageNumber.HasValue && pageNumber.Value > 1 && pageSize.HasValue ? (pageNumber.Value - 1) * pageSize.Value : null;` `var list = await QueryRunner.ToListAsync(effective, orderBy, descending, offset, limit, ct);` return async |
| `Many<TItem>(whereClause, ...)` | build the predicate with `BuildWhereClausePredicate<TItem>` and delegate |
| `One<TItem>(predicate, continueFrom, sortOrders, includeDeleted, ...)` | `effective = ApplyContinuation<TItem>(WithSoftDeleteFilter(predicate, includeDeleted), sortOrders, continueFrom);` `var orderBy = BuildOrderByPath(sortOrders, out var descending);` `return await QueryRunner.FirstOrDefaultAsync(effective, orderBy, descending, ct);` |
| `Random<TItem>(predicate, continueFrom, count, includeDeleted, ...)` | `effective = WithSoftDeleteFilter(predicate, includeDeleted);` `var total = await QueryRunner.CountAsync(effective, ct);` if `total == 0` return empty; `var skip = Random.Shared.Next(0, (int)Math.Min(total, int.MaxValue));` `var list = await QueryRunner.ToListAsync(effective, null, false, skip, count, ct);` return async |

Add to `DocumentDbRepository.ReadonlyRepository.cs`:

```csharp
    private static string BuildOrderByPath<TItem>(IEnumerable<SortOrder<TItem>> sortOrders, out bool descending) where TItem : Entity
    {
        descending = false;

        var first = sortOrders?.FirstOrDefault();

        if (first?.Field is null)
        {
            return null;
        }

        if (!Query.MemberPathResolver.TryResolve(first.Field, out var path, out _))
        {
            throw new NotSupportedException($"Cannot resolve sort path '{first.Field}'.");
        }

        descending = first.Direction == SortDirection.Descending;
        return path;
    }

    private static Expression<Func<TItem, bool>> ApplyContinuation<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders, string continueFrom) where TItem : Entity
    {
        var token = NormalizeId(continueFrom);

        if (token is null || !CanApplyContinuation(sortOrders))
        {
            return predicate;
        }

        var parameter = Expression.Parameter(typeof(TItem), "item");
        var idProperty = Expression.Property(parameter, nameof(Entity.Id));
        var tokenConstant = Expression.Constant(token);

        var compareCall = Expression.Call(idProperty, typeof(string).GetMethod(nameof(string.CompareTo), new[] { typeof(string) })!, tokenConstant);
        var body = Expression.GreaterThan(compareCall, Expression.Constant(0));

        var continuation = Expression.Lambda<Func<TItem, bool>>(body, parameter);

        return predicate is null ? continuation : predicate.And(continuation);
    }
```

And the `CanApplyContinuation` guard (mirror the SQLite provider — continuation only when the first sort is `Id` ascending, or when there is no sort):

```csharp
    protected static bool CanApplyContinuation<TItem>(IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        var first = sortOrders?.FirstOrDefault();

        if (first?.Field is null)
        {
            return true;
        }

        if (!Query.MemberPathResolver.TryResolve(first.Field, out var path, out _))
        {
            return false;
        }

        return string.Equals(path, nameof(Entity.Id), StringComparison.Ordinal)
               && first.Direction == SortDirection.Ascending;
    }
```

> **`CompareTo` translation is the risk here.** `item.Id.CompareTo(token)` compiles to a `string.CompareTo(string)` call, which the library's translator may or may not support. Add a test (Task 2.4) that asserts the continuation path works; if the library rejects `CompareTo`, replace the comparison with the library's filtered-query string grammar **after** validating the token: the token always comes from `NormalizeId`, so it is 24 lowercase hex characters; add a guard `IsSafeContinuationToken(string)` that checks length `24` and every character is in `[0-9a-f]`, then use `query.Where($"id > '{token}'")` in `ExecuteListAsync`. Never interpolate an unvalidated value.

---

## Task 2.4 — Tests

`...\Saturn.Data.DocumentDb.Tests\ContinuationTests.cs` — port `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\ContinuationTests.cs` verbatim, changing the namespace to `Saturn.Data.DocumentDb.Tests` and the fixture/repository types to `DatabaseFixture` / `UnitTestableDocumentDbRepository`.

`...\QueryBehaviourTests.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.DocumentDb.Tests;

public class QueryBehaviourTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
    }

    [Fact]
    public async Task String_Contains_Translates()
    {
        await fixture.Repository.Insert(
        [
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "alpha-one" },
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "beta-two" }
        ]);

        var matches = await (await fixture.Repository.Many<BasicEntity>(entity => entity.Name.Contains("alpha"), pageSize: 10)).ToListAsync();

        Assert.Single(matches);
    }

    [Fact]
    public async Task Id_List_Contains_Translates()
    {
        var first = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "one" };
        var second = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "two" };
        await fixture.Repository.Insert([first, second]);

        var ids = new List<string> { first.Id, second.Id };
        var matches = await (await fixture.Repository.ById<BasicEntity>(ids)).ToListAsync();

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public async Task Count_And_Sorting_Work()
    {
        await fixture.Repository.Insert(
        [
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "c" },
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "a" },
            new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "b" }
        ]);

        var count = await fixture.Repository.Count<BasicEntity>(entity => true);
        Assert.Equal(3, count);

        var sorted = await (await fixture.Repository.Many<BasicEntity>(
            entity => true,
            pageSize: 3,
            sortOrders: new[] { new SortOrder<BasicEntity>(entity => entity.Name, SortDirection.Ascending) })).ToListAsync();

        Assert.Equal(new[] { "a", "b", "c" }, sorted.Select(entity => entity.Name));
    }
}
```

---

## Task 2.5 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "...\GoLive.Saturn.Data.DocumentDb.Tests.csproj"
```

---

## Do NOT

- Do not interpolate unvalidated values into any query text. The only allowed exception is the validated continuation token described in Task 2.3.
- Do not implement a LINQ expression translator — the library owns translation; this phase only delegates and gates.
- Do not implement `Patch`, `Increment`, transactions, cascade or change feed here.
- Do not add comments to `.cs` files.
