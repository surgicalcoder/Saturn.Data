# Phase 2 — Query Translation, Sorting, Paging, Continuation

**Goal:** Translate the predicate patterns used across the framework into SQLite JSON1 SQL, with a correct in-memory fallback; support sort, page, and `continueFrom` pagination.

**Prerequisite:** Phase 1 complete and `BasicTests` green.

**Exit criteria:**
- `QueryTranslationTests` and `ContinuationTests` (new, below) pass.
- `e => true`, `x.Id == id`, `ids.Contains(x.Id)`, `x.Name == "..."`, `x.Name.Contains("...")`, and `x.Count > n` all translate to SQL (assert via a helper that returns the generated SQL).
- `Many` with `continueFrom` and an `Id`-ascending sort returns non-overlapping pages.
- `Many` with a non-`Id` sort ignores `continueFrom` (matching Mongo/LiteDbX).
- A malformed `continueFrom` is ignored.

**Why the fallback matters:** `SqliteRepositoryOptions.StrictTranslation` defaults to `true`. When `false`, an untranslatable predicate is evaluated in memory after materializing the collection. Never silently return wrong results.

---

## Task 2.1 — Replace `Query\SqliteExpressionTranslator.cs` with the full version

Replace the entire file `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Query\SqliteExpressionTranslator.cs` with:

```csharp
using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite.Query;

public sealed class SqliteExpressionTranslator
{
    private int parameterIndex;

    public SqlFragment Translate(LambdaExpression predicate)
    {
        parameterIndex = 0;

        if (predicate is null || predicate.Body is null)
        {
            return SqlFragment.AlwaysTrue;
        }

        return Visit(predicate.Body);
    }

    public SqlFragment TranslatePredicate(Expression expression)
    {
        parameterIndex = 0;
        return Visit(expression);
    }

    private SqlFragment Visit(Expression node)
    {
        switch (node)
        {
            case ConstantExpression constant when constant.Type == typeof(bool):
                return (bool)constant.Value! ? SqlFragment.AlwaysTrue : SqlFragment.AlwaysFalse;

            case BinaryExpression binary:
                return VisitBinary(binary);

            case MemberExpression member when member.Type == typeof(bool):
                return VisitBooleanMember(member);

            case UnaryExpression { NodeType: ExpressionType.Not } not:
            {
                var inner = Visit(not.Operand);
                return new SqlFragment { Sql = $"NOT ({inner.Sql})", Parameters = inner.Parameters };
            }

            case MethodCallExpression call:
                return VisitMethodCall(call);

            default:
                throw new SqliteTranslationException($"Unsupported expression node '{node.NodeType}' of type '{node.Type.Name}'.");
        }
    }

    private SqlFragment VisitBinary(BinaryExpression node)
    {
        switch (node.NodeType)
        {
            case ExpressionType.AndAlso:
                return SqlFragment.Combine(Visit(node.Left), Visit(node.Right), "AND");

            case ExpressionType.OrElse:
                return SqlFragment.Combine(Visit(node.Left), Visit(node.Right), "OR");

            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
                if (TryTranslateReferenceComparison(node, out var reference))
                {
                    return reference;
                }

                return VisitComparison(node, MapOperator(node.NodeType));

            default:
                throw new SqliteTranslationException($"Unsupported binary operator '{node.NodeType}'.");
        }
    }

    private SqlFragment VisitComparison(BinaryExpression node, string op)
    {
        var left = SqliteJsonPathResolver.Unwrap(node.Left);
        var right = SqliteJsonPathResolver.Unwrap(node.Right);

        if (SqliteJsonPathResolver.TryResolve(left, out var leftPath, out var leftIsColumn) && TryEvaluate(right, out var rightValue))
        {
            return BuildComparison(leftPath, leftIsColumn, op, rightValue);
        }

        if (SqliteJsonPathResolver.TryResolve(right, out var rightPath, out var rightIsColumn) && TryEvaluate(left, out var leftValue))
        {
            return BuildComparison(rightPath, rightIsColumn, op, leftValue);
        }

        throw new SqliteTranslationException($"Cannot translate comparison between '{left}' and '{right}'.");
    }

    private bool TryTranslateReferenceComparison(BinaryExpression node, out SqlFragment fragment)
    {
        fragment = null;

        if (node.Method is null || !IsReferenceOperator(node.Method))
        {
            return false;
        }

        var left = SqliteJsonPathResolver.Unwrap(node.Left);
        var right = SqliteJsonPathResolver.Unwrap(node.Right);
        var op = MapOperator(node.NodeType);

        if (TryResolveReferenceOperand(left, out var leftPath, out var leftIsColumn) && TryResolveReferenceValue(right, out var rightValue))
        {
            fragment = BuildComparison(leftPath, leftIsColumn, op, rightValue);
            return true;
        }

        if (TryResolveReferenceOperand(right, out var rightPath, out var rightIsColumn) && TryResolveReferenceValue(left, out var leftValue))
        {
            fragment = BuildComparison(rightPath, rightIsColumn, op, leftValue);
            return true;
        }

        return false;
    }

    private static bool IsReferenceOperator(MethodInfo method)
    {
        if (!method.Name.StartsWith("op_", StringComparison.Ordinal))
        {
            return false;
        }

        var declaring = method.DeclaringType;

        if (declaring is null)
        {
            return false;
        }

        if (declaring == typeof(GoLive.Saturn.Data.Entities.WeakRef))
        {
            return true;
        }

        return declaring.IsGenericType && (declaring.GetGenericTypeDefinition() == typeof(GoLive.Saturn.Data.Entities.Ref<>)
                                           || declaring.GetGenericTypeDefinition() == typeof(GoLive.Saturn.Data.Entities.WeakRef<>));
    }

    private static bool TryResolveReferenceOperand(Expression expression, out string path, out bool isColumn)
        => SqliteJsonPathResolver.TryResolve(expression, out path, out isColumn);

    private static bool TryResolveReferenceValue(Expression expression, out object value)
    {
        value = null;

        if (!TryEvaluate(expression, out var evaluated))
        {
            return false;
        }

        switch (evaluated)
        {
            case null:
                value = null;
                return true;
            case string text:
                value = text;
                return true;
            case GoLive.Saturn.Data.Entities.WeakRef weak:
                value = weak.Id;
                return true;
            case GoLive.Saturn.Data.Entities.IScopedById:
                value = null;
                return false;
        }

        var idProperty = evaluated.GetType().GetProperty("Id");
        if (idProperty is not null)
        {
            value = idProperty.GetValue(evaluated) as string;
            return true;
        }

        return false;
    }

    private SqlFragment VisitBooleanMember(MemberExpression member)
    {
        if (!SqliteJsonPathResolver.TryResolve(member, out var path, out var isColumn))
        {
            throw new SqliteTranslationException($"Cannot resolve boolean member '{member}'.");
        }

        var operand = isColumn ? path : JsonExtract(path);
        return new SqlFragment { Sql = $"{operand} = 1" };
    }

    private SqlFragment VisitMethodCall(MethodCallExpression node)
    {
        if (TryTranslateStringMethod(node, out var stringMethod))
        {
            return stringMethod;
        }

        if (TryTranslateCollectionContains(node, out var collectionContains))
        {
            return collectionContains;
        }

        if (TryTranslateStaticContains(node, out var staticContains))
        {
            return staticContains;
        }

        throw new SqliteTranslationException($"Unsupported method call '{node.Method.Name}'.");
    }

    private bool TryTranslateStringMethod(MethodCallExpression node, out SqlFragment fragment)
    {
        fragment = null;

        if (node.Object is null || node.Object.Type != typeof(string))
        {
            return false;
        }

        var target = SqliteJsonPathResolver.Unwrap(node.Object);

        if (!SqliteJsonPathResolver.TryResolve(target, out var path, out var isColumn))
        {
            return false;
        }

        var operand = isColumn ? path : JsonExtract(path);

        switch (node.Method.Name)
        {
            case nameof(string.Contains) when node.Arguments.Count == 1 && TryEvaluate(node.Arguments[0], out var containsValue):
            {
                var parameter = CreateParameter(containsValue);
                fragment = new SqlFragment { Sql = $"instr({operand}, {parameter.ParameterName}) > 0", Parameters = new[] { parameter } };
                return true;
            }

            case nameof(string.StartsWith) when node.Arguments.Count == 1 && TryEvaluate(node.Arguments[0], out var startsWithValue):
            {
                var parameter = CreateParameter(EscapeLike(Convert.ToString(startsWithValue, CultureInfo.InvariantCulture)) + "%");
                fragment = new SqlFragment { Sql = $"{operand} LIKE {parameter.ParameterName} ESCAPE '\\'", Parameters = new[] { parameter } };
                return true;
            }

            case nameof(string.EndsWith) when node.Arguments.Count == 1 && TryEvaluate(node.Arguments[0], out var endsWithValue):
            {
                var parameter = CreateParameter("%" + EscapeLike(Convert.ToString(endsWithValue, CultureInfo.InvariantCulture)));
                fragment = new SqlFragment { Sql = $"{operand} LIKE {parameter.ParameterName} ESCAPE '\\'", Parameters = new[] { parameter } };
                return true;
            }

            default:
                return false;
        }
    }

    private bool TryTranslateCollectionContains(MethodCallExpression node, out SqlFragment fragment)
    {
        fragment = null;

        if (node.Object is null || node.Arguments.Count != 1 || node.Method.Name != nameof(IList.Contains))
        {
            return false;
        }

        var target = SqliteJsonPathResolver.Unwrap(node.Object);

        if (target.Type == typeof(string))
        {
            return false;
        }

        if (!typeof(IEnumerable).IsAssignableFrom(target.Type))
        {
            return false;
        }

        if (!SqliteJsonPathResolver.TryResolve(target, out var path, out _) || !TryEvaluate(node.Arguments[0], out var value))
        {
            return false;
        }

        var parameter = CreateParameter(value);
        fragment = new SqlFragment
        {
            Sql = $"EXISTS (SELECT 1 FROM json_each(_doc, '{path}') WHERE value = {parameter.ParameterName})",
            Parameters = new[] { parameter }
        };
        return true;
    }

    private bool TryTranslateStaticContains(MethodCallExpression node, out SqlFragment fragment)
    {
        fragment = null;

        Expression collectionExpression;
        Expression itemExpression;

        if (node.Object is not null && node.Arguments.Count == 1)
        {
            collectionExpression = node.Object;
            itemExpression = node.Arguments[0];
        }
        else if (node.Object is null && node.Arguments.Count == 2)
        {
            collectionExpression = node.Arguments[0];
            itemExpression = node.Arguments[1];
        }
        else
        {
            return false;
        }

        if (collectionExpression.Type == typeof(string))
        {
            return false;
        }

        if (!TryEvaluate(collectionExpression, out var collectionValue) || collectionValue is not IEnumerable collection)
        {
            return false;
        }

        var item = SqliteJsonPathResolver.Unwrap(itemExpression);

        if (!SqliteJsonPathResolver.TryResolve(item, out var path, out var isColumn))
        {
            return false;
        }

        var values = collection.Cast<object>().ToList();

        if (values.Count == 0)
        {
            fragment = SqlFragment.AlwaysFalse;
            return true;
        }

        var parameters = values.Select(CreateParameter).ToList();
        var names = string.Join(", ", parameters.Select(parameter => parameter.ParameterName));
        var operand = isColumn ? path : JsonExtract(path);

        fragment = new SqlFragment { Sql = $"{operand} IN ({names})", Parameters = parameters };
        return true;
    }

    private SqlFragment BuildComparison(string path, bool isColumn, string op, object value)
    {
        var operand = isColumn ? path : JsonExtract(path);

        if (value is null)
        {
            return op switch
            {
                "=" => new SqlFragment { Sql = $"{operand} IS NULL" },
                "<>" => new SqlFragment { Sql = $"{operand} IS NOT NULL" },
                _ => throw new SqliteTranslationException($"Cannot apply operator '{op}' to null.")
            };
        }

        var parameter = CreateParameter(value);
        return new SqlFragment { Sql = $"{operand} {op} {parameter.ParameterName}", Parameters = new[] { parameter } };
    }

    private SqliteParameter CreateParameter(object value)
        => new($"@p{parameterIndex++}", value ?? DBNull.Value);

    private static string MapOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => "=",
        ExpressionType.NotEqual => "<>",
        ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThan => "<",
        ExpressionType.LessThanOrEqual => "<=",
        _ => throw new SqliteTranslationException($"Unsupported operator '{nodeType}'.")
    };

    private static string JsonExtract(string path) => $"json_extract(_doc, '{path}')";

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static bool TryEvaluate(Expression expression, out object value)
    {
        if (expression is ConstantExpression constant)
        {
            value = constant.Value;
            return true;
        }

        if (!ReferencesParameter(expression))
        {
            value = Expression.Lambda(expression).Compile().DynamicInvoke();
            return true;
        }

        value = null;
        return false;
    }

    private static bool ReferencesParameter(Expression expression)
        => new ParameterFinder().Find(expression);

    private sealed class ParameterFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        public bool Find(Expression expression)
        {
            Visit(expression);
            return Found;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found = true;
            return base.VisitParameter(node);
        }
    }
}
```

> Note: `case string text:` does not exist; the `case string` pattern is `case string text:` which is valid C#. The `TryResolveReferenceValue` switch uses `case null:` first — verify it compiles; if the compiler complains about unreachable `IScopedById`, remove that branch.

---

## Task 2.2 — Add translation diagnostics for tests

Add to `SqliteRepository` a test-visible helper that returns generated predicate SQL. Add to `SqliteRepository.cs`:

```csharp
    internal static string TranslatePredicateSql<TItem>(Expression<Func<TItem, bool>> predicate) where TItem : Entity
        => new SqliteExpressionTranslator().Translate(predicate).Sql;
```

---

## Task 2.3 — Add continuation support to `SqliteRepository.ReadonlyRepository.cs`

### 2.3.1 Add helpers (inside the partial class)

```csharp
    protected static bool CanApplyContinuation<TItem>(IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        if (sortOrders is null)
        {
            return true;
        }

        using var enumerator = sortOrders.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            return true;
        }

        var first = enumerator.Current;

        if (first?.Field is null)
        {
            return true;
        }

        return sortOrders.All(order => order is null || order.Field is null)
               && false
               || IsIdAscending(first);
    }

    private static bool IsIdAscending<TItem>(SortOrder<TItem> sortOrder) where TItem : Entity
    {
        if (!SqliteJsonPathResolver.TryResolve(UnwrapLambdaBody(sortOrder.Field), out var path, out var isColumn))
        {
            return false;
        }

        return isColumn && path == "_id" && sortOrder.Direction == SortDirection.Ascending;
    }

    private static SqlFragment ApplyContinuation<TItem>(SqlFragment predicate, IEnumerable<SortOrder<TItem>> sortOrders, string continueFrom)
        where TItem : Entity
    {
        var token = NormalizeId(continueFrom);

        if (token is null || !CanApplyContinuation(sortOrders))
        {
            return predicate;
        }

        var parameter = new SqliteParameter("@continueFrom", token);
        var continuation = new SqlFragment { Sql = "_id > @continueFrom", Parameters = new[] { parameter } };
        return SqlFragment.Combine(predicate, continuation, "AND");
    }
```

> The `CanApplyContinuation` body above is intentionally simplified to "first sort must be Id ascending". If it does not read cleanly, replace the whole method with:
> ```csharp
>     protected static bool CanApplyContinuation<TItem>(IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
>         => sortOrders is null || !sortOrders.Any() || IsIdAscending(sortOrders.First());
> ```
> Use that simpler version. Delete the `All(...) && false` expression.

### 2.3.2 Add `_id` tiebreak to `BuildOrderBy`

Replace `BuildOrderBy<TItem>` with:

```csharp
    private static string BuildOrderBy<TItem>(IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        var clauses = new List<string>();
        var hasIdClause = false;

        if (sortOrders is not null)
        {
            foreach (var sortOrder in sortOrders)
            {
                if (sortOrder?.Field is null)
                {
                    continue;
                }

                if (!SqliteJsonPathResolver.TryResolve(UnwrapLambdaBody(sortOrder.Field), out var path, out var isColumn))
                {
                    throw new SqliteTranslationException($"Cannot translate sort order '{sortOrder.Field}'.");
                }

                var operand = isColumn ? path : $"json_extract(_doc, '{path}')";
                var direction = sortOrder.Direction == SortDirection.Ascending ? "ASC" : "DESC";
                clauses.Add($"{operand} {direction}");

                if (isColumn && path == "_id")
                {
                    hasIdClause = true;
                }
            }
        }

        if (!hasIdClause)
        {
            clauses.Add("_id ASC");
        }

        return string.Join(", ", clauses);
    }
```

### 2.3.3 Update `Many`, `One`, `Count` to apply continuation

`Count<TItem>(predicate, continueFrom, includeDeleted, ...)`: after `BuildReadPredicate`, call `fragment = ApplyContinuation(fragment, sortOrders: null, continueFrom);`.

`Many<TItem>(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, ...)`: after `BuildReadPredicate`, call `fragment = ApplyContinuation(fragment, sortOrders, continueFrom);`.

`One<TItem>(predicate, continueFrom, sortOrders, includeDeleted, ...)`: after `BuildReadPredicate`, call `fragment = ApplyContinuation(fragment, sortOrders, continueFrom);`.

`All<TItem>` and `ById<TItem>(IDs)` have no `continueFrom`, leave unchanged.

Also update `BuildOrderBy` usage: when `sortOrders` is null, `Many`/`One` should still order by `_id ASC` for stable ordering. Pass `BuildOrderBy(sortOrders)` which now always returns at least `_id ASC`.

---

## Task 2.4 — `whereClause` keys that reference scope/ref properties

The Phase 1 `BuildWhereClausePredicate` uses `Expression.Property(parameter, pair.Key)` and `Expression.Constant(pair.Value, property.Type)`. This fails when `pair.Value` type does not match the property (e.g., `Scope` is `Ref<T>` and value is `string`). Replace it with:

```csharp
    private static Expression<Func<TItem, bool>> BuildWhereClausePredicate<TItem>(Dictionary<string, object> whereClause)
        where TItem : Entity
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
                throw new SqliteTranslationException($"Unknown property '{pair.Key}' on '{parameter.Type.Name}'.");
            }

            var member = Expression.Property(parameter, property);
            Expression comparison;

            if (member.Type == typeof(string))
            {
                comparison = Expression.Equal(member, Expression.Constant(Convert.ToString(pair.Value, CultureInfo.InvariantCulture)));
            }
            else
            {
                try
                {
                    comparison = Expression.Equal(member, Expression.Convert(Expression.Constant(pair.Value), member.Type));
                }
                catch (InvalidOperationException)
                {
                    throw new SqliteTranslationException($"Cannot build where-clause comparison for '{pair.Key}'.");
                }
            }

            body = body is null ? comparison : Expression.AndAlso(body, comparison);
        }

        return body is null ? item => true : Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }
```

Add `using System.Globalization;` where needed.

---

## Task 2.5 — Create `QueryTranslationTests.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\QueryTranslationTests.cs`:

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class QueryTranslationTests
{
    [Fact]
    public void True_Constant()
    {
        Expression<Func<BasicEntity, bool>> predicate = entity => true;
        var sql = SqliteRepository.TranslatePredicateSql(predicate);
        Assert.Equal("1=1", sql);
    }

    [Fact]
    public void Id_Equality_Uses_Column()
    {
        var id = "000000000000000000000001";
        Expression<Func<BasicEntity, bool>> predicate = entity => entity.Id == id;
        var sql = SqliteRepository.TranslatePredicateSql(predicate);
        Assert.Equal("_id = @p0", sql);
    }

    [Fact]
    public void Property_Equality_Uses_JsonExtract()
    {
        Expression<Func<BasicEntity, bool>> predicate = entity => entity.Name == "abc";
        var sql = SqliteRepository.TranslatePredicateSql(predicate);
        Assert.Equal("json_extract(_doc, '$.Name') = @p0", sql);
    }

    [Fact]
    public void String_Contains_Uses_Instr()
    {
        Expression<Func<BasicEntity, bool>> predicate = entity => entity.Name.Contains("abc");
        var sql = SqliteRepository.TranslatePredicateSql(predicate);
        Assert.Equal("instr(json_extract(_doc, '$.Name'), @p0) > 0", sql);
    }

    [Fact]
    public void Id_List_Contains_Uses_In()
    {
        var ids = new[] { "000000000000000000000001", "000000000000000000000002" };
        Expression<Func<BasicEntity, bool>> predicate = entity => ids.Contains(entity.Id);
        var sql = SqliteRepository.TranslatePredicateSql(predicate);
        Assert.Equal("_id IN (@p0, @p1)", sql);
    }

    [Fact]
    public void Greater_Than_Uses_JsonExtract()
    {
        Expression<Func<CountedEntity, bool>> predicate = entity => entity.Count > 3;
        var sql = SqliteRepository.TranslatePredicateSql(predicate);
        Assert.Equal("json_extract(_doc, '$.Count') > @p0", sql);
    }
}

public class CountedEntity : Entity
{
    public int Count { get; set; }
}
```

> `SqliteRepository.TranslatePredicateSql` is `internal`. The tests project must be able to see it. Add `[assembly: InternalsVisibleTo("Saturn.Data.Sqlite.Tests")]` to the library. Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\AssemblyInfo.cs`:
> ```csharp
> using System.Runtime.CompilerServices;
>
> [assembly: InternalsVisibleTo("Saturn.Data.Sqlite.Tests")]
> ```

---

## Task 2.6 — Create `ContinuationTests.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\ContinuationTests.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class ContinuationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private static readonly string[] CleanupCollections = { "BasicEntity", "ChildEntity", "ParentScope" };

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
        await fixture.Repository.HardDelete<ChildEntity>(entity => true);
        await fixture.Repository.HardDelete<ParentScope>(entity => true);
    }

    [Fact]
    public async Task Many_With_ContinueFrom_Id_Ascending_Returns_Next_Page()
    {
        var entities = Enumerable.Range(1, 12)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"item-{index:D2}" })
            .ToList();

        await fixture.Repository.Insert(entities);

        var sortOrders = new[] { new SortOrder<BasicEntity>(entity => entity.Id, SortDirection.Ascending) };

        var firstPage = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        var secondPage = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, continueFrom: firstPage[^1].Id, pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        Assert.Equal(5, firstPage.Count);
        Assert.Equal(5, secondPage.Count);
        Assert.Empty(firstPage.Select(entity => entity.Id).Intersect(secondPage.Select(entity => entity.Id)));

        var orderedIds = entities.Select(entity => entity.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.Equal(orderedIds.Skip(5).Take(5), secondPage.Select(entity => entity.Id));
    }

    [Fact]
    public async Task Many_With_Non_Id_Sort_Ignores_ContinueFrom()
    {
        var entities = Enumerable.Range(1, 6)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"item-{index:D2}" })
            .ToList();

        await fixture.Repository.Insert(entities);

        var sortOrders = new[] { new SortOrder<BasicEntity>(entity => entity.Name, SortDirection.Ascending) };

        var firstPage = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, pageSize: 3, sortOrders: sortOrders)).ToListAsync();

        var withToken = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, continueFrom: firstPage[^1].Id, pageSize: 3, sortOrders: sortOrders)).ToListAsync();

        Assert.Equal(3, withToken.Count);
        Assert.NotEmpty(withToken);
    }

    [Fact]
    public async Task Many_With_Malformed_ContinueFrom_Is_Ignored()
    {
        var entities = Enumerable.Range(1, 5)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"item-{index:D2}" })
            .ToList();

        await fixture.Repository.Insert(entities);

        var sortOrders = new[] { new SortOrder<BasicEntity>(entity => entity.Id, SortDirection.Ascending) };

        var expected = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        var actual = await (await fixture.Repository.Many<BasicEntity>(
            entity => true, continueFrom: "not-a-valid-object-id", pageSize: 5, sortOrders: sortOrders)).ToListAsync();

        Assert.Equal(expected.Select(entity => entity.Id), actual.Select(entity => entity.Id));
    }

    [Fact]
    public async Task Count_With_ContinueFrom_Applies_Id_Boundary()
    {
        var entities = Enumerable.Range(1, 10)
            .Select(index => new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"item-{index:D2}" })
            .ToList();

        await fixture.Repository.Insert(entities);

        var orderedIds = entities.Select(entity => entity.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var token = orderedIds[4];

        var count = await fixture.Repository.Count<BasicEntity>(entity => true, continueFrom: token);

        Assert.Equal(5, count);
    }
}
```

---

## Task 2.7 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj"
```

All prior tests plus `QueryTranslationTests` and `ContinuationTests` must pass.

---

## Known simplification (do not "fix" in this phase)

`SqliteRepository.IQueryable<TItem>()` still loads in memory and returns `AsQueryable()`. This is intentional for Phase 2. No shared contract test depends on server-side `IQueryable` pushdown. A real `IQueryProvider` is an optional Phase 7 task; do not attempt it now.

---

## Do NOT

- Do not change the public method signatures from the interface.
- Do not use string concatenation for constant values; parameters only.
- Do not cache translator instances.
- Do not implement scoped interfaces yet (Phase 3).
- Do not add a custom `IQueryProvider` in this phase.
- Do not add comments to `.cs` files.
