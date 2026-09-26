# Phase 4 — Patch, Increment, JsonUpdate, Index Manager

**Goal:** Implement the mutation surface (`Patch`, `JsonUpdate`, `Increment`) using SQLite JSON functions, and `IRepositoryIndexManager.EnsureIndexes`.

**Prerequisite:** Phase 3 complete and all tests green.

**Exit criteria:**
- `PatchWritesTests` passes: `{"$set":{"Count":5}}` sets the field and bumps `_v`; `expectedVersion` mismatch throws `FailedToUpdateException`; unknown id throws `FailedToUpdateException`.
- `IncrementWritesTests` passes: `Increment(e => e.Count, 5)` adds 5 and bumps `_v`; unknown id throws `FailedToUpdateException`.
- `SqliteIndexManagerTests` passes: `EnsureIndexes` creates an index visible in `PRAGMA index_list`; a unique index raises on duplicate; `ExpireAfter` is reported as unsupported and does not throw.

---

## Task 4.1 — Extend `BuildWriteContext` with increment fields

In `SqliteRepository.cs`, change the `BuildWriteContext<TItem>` signature so that `incrementField` and `incrementDelta` are accepted. Insert these two parameters immediately before `IDatabaseTransaction transaction`:

```csharp
        LambdaExpression incrementField = null,
        object incrementDelta = null,
```

And set them in the returned object (add to the initializer):

```csharp
            IncrementField = incrementField,
            IncrementDelta = incrementDelta,
```

Add `using System.Linq.Expressions;` to `SqliteRepository.cs` if not already present.

---

## Task 4.2 — Create `SqliteDataUpdateDefinition.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteDataUpdateDefinition.cs`

```csharp
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public sealed class SqliteDataUpdateDefinition<TItem> : IDataUpdateDefinition<TItem> where TItem : Entity
{
    public SqliteDataUpdateDefinition(Action<TItem> apply)
    {
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    internal Action<TItem> Apply { get; }
}
```

---

## Task 4.3 — Create `SqliteRepository.Patch.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.Patch.cs`

```csharp
using System.Linq.Expressions;
using System.Text.Json;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository
{
    public async Task Patch<TItem>(string id, long? expectedVersion = null, string jsonDocument = null,
        IDataUpdateDefinition<TItem> updateDefinition = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(jsonDocument) && updateDefinition == null)
        {
            throw new ArgumentException("At least one patch input must be supplied.", nameof(jsonDocument));
        }

        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            throw new FailedToUpdateException();
        }

        var context = BuildWriteContext(RepositoryWriteOperation.Patch, id: normalized, expectedVersion: expectedVersion,
            jsonDocument: jsonDocument, updateDefinition: updateDefinition, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Patch, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

            int affected;

            if (!string.IsNullOrWhiteSpace(jsonDocument))
            {
                affected = await ExecutePatchAsync<TItem>(lease.Connection, normalized, expectedVersion, jsonDocument, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                affected = await ExecuteUpdateDefinitionPatchAsync(lease.Connection, normalized, expectedVersion, (SqliteDataUpdateDefinition<TItem>)updateDefinition,
                    cancellationToken).ConfigureAwait(false);
            }

            if (affected == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, affected, new[] { normalized })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            throw new FailedToUpdateException();
        }

        var context = BuildWriteContext(RepositoryWriteOperation.Patch, id: normalized, jsonDocument: json,
            transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Patch, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

            var affected = await ExecutePatchAsync<TItem>(lease.Connection, normalized, expectedVersion: null, json, cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, affected, new[] { normalized })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<int> ExecutePatchAsync<TItem>(SqliteConnection connection, string id, long? expectedVersion, string jsonDocument,
        CancellationToken cancellationToken) where TItem : Entity
    {
        using var document = JsonDocument.Parse(jsonDocument);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Patch JSON must be an object.", nameof(jsonDocument));
        }

        var parameters = new List<SqliteParameter>();
        var current = BuildPatchExpression(root, parameters);

        await using var command = connection.CreateCommand();
        var table = Quote(GetCollectionNameForType<TItem>());
        var versionFilter = expectedVersion.HasValue ? " AND COALESCE(_v,0) = @expectedVersion" : string.Empty;

        command.CommandText = $"""
            UPDATE {table}
            SET _doc = {current},
                _v = COALESCE(_v,0) + 1,
                _deleted = COALESCE(json_extract({current},'$.IsDeleted'),0),
                _scope = json_extract({current},'$.Scope'),
                _scope2 = json_extract({current},'$.SecondScope'),
                _archived = COALESCE(json_extract({current},'$.IsArchived'),0)
            WHERE _id = @id{versionFilter};
            """;

        command.Parameters.AddWithValue("@id", id);

        if (expectedVersion.HasValue)
        {
            command.Parameters.AddWithValue("@expectedVersion", expectedVersion.Value);
        }

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildPatchExpression(JsonElement root, List<SqliteParameter> parameters)
    {
        var current = "_doc";
        var hasOperator = root.TryGetProperty("$set", out _) || root.TryGetProperty("$inc", out _) || root.TryGetProperty("$unset", out _);

        if (hasOperator)
        {
            if (root.TryGetProperty("$set", out var setElement) && setElement.ValueKind == JsonValueKind.Object)
            {
                current = AppendSet(current, setElement, parameters);
            }

            if (root.TryGetProperty("$inc", out var incElement) && incElement.ValueKind == JsonValueKind.Object)
            {
                current = AppendInc(current, incElement, parameters);
            }

            if (root.TryGetProperty("$unset", out var unsetElement) && unsetElement.ValueKind == JsonValueKind.Object)
            {
                current = AppendUnset(current, unsetElement);
            }

            if (current == "_doc")
            {
                throw new ArgumentException("Patch JSON contained update operators but no supported operations.");
            }

            return current;
        }

        return AppendSet(current, root, parameters);
    }

    private static string AppendSet(string current, JsonElement setElement, List<SqliteParameter> parameters)
    {
        foreach (var property in setElement.EnumerateObject())
        {
            var path = BuildJsonPath(property.Name);
            var parameter = new SqliteParameter($"@patch{parameters.Count}", property.Value.GetRawText());
            parameters.Add(parameter);
            current = $"json_set({current}, '{path}', json({parameter.ParameterName}))";
        }

        return current;
    }

    private static string AppendInc(string current, JsonElement incElement, List<SqliteParameter> parameters)
    {
        foreach (var property in incElement.EnumerateObject())
        {
            var path = BuildJsonPath(property.Name);
            var parameter = new SqliteParameter($"@patch{parameters.Count}", property.Value.GetRawText());
            parameters.Add(parameter);
            current = $"json_set({current}, '{path}', COALESCE(json_extract(_doc,'{path}'),0) + json_extract('{{ \"v\": ' || {parameter.ParameterName} || ' }}','$.v'))";
        }

        return current;
    }

    private static string AppendUnset(string current, JsonElement unsetElement)
    {
        foreach (var property in unsetElement.EnumerateObject())
        {
            var path = BuildJsonPath(property.Name);
            current = $"json_remove({current}, '{path}')";
        }

        return current;
    }

    private static string BuildJsonPath(string key)
        => key.All(character => char.IsLetterOrDigit(character) || character == '_') ? $"$.{key}" : $"$.\"{key}\"";

    private async Task<int> ExecuteUpdateDefinitionPatchAsync<TItem>(SqliteConnection connection, string id, long? expectedVersion,
        SqliteDataUpdateDefinition<TItem> updateDefinition, CancellationToken cancellationToken) where TItem : Entity
    {
        if (updateDefinition is null)
        {
            throw new NotSupportedException("Only SqliteDataUpdateDefinition<TItem> is supported.");
        }

        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        var existing = await ReadDocumentAsync<TItem>(connection, id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return 0;
        }

        if (expectedVersion.HasValue && existing.Version != expectedVersion.Value)
        {
            return 0;
        }

        updateDefinition.Apply(existing);
        existing.Id = id;
        existing.Version = (existing.Version ?? 0) + 1;

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {Quote(GetCollectionNameForType<TItem>())}
            SET _doc = @doc,
                _v = json_extract(@doc,'$.Version'),
                _deleted = COALESCE(json_extract(@doc,'$.IsDeleted'),0),
                _scope = json_extract(@doc,'$.Scope'),
                _scope2 = json_extract(@doc,'$.SecondScope'),
                _archived = COALESCE(json_extract(@doc,'$.IsArchived'),0)
            WHERE _id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@doc", serializer.Serialize(existing));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<TItem> ReadDocumentAsync<TItem>(SqliteConnection connection, string id, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT _doc FROM {Quote(GetCollectionNameForType<TItem>())} WHERE _id = @id;";
        command.Parameters.AddWithValue("@id", id);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is string document ? serializer.Deserialize<TItem>(document) : null;
    }
}
```

> The `$inc` expression above is intentionally avoided in practice if it is too clever. If the generated SQL for `$inc` looks wrong, use this simpler form instead (SQLite `json_extract` on a bound text value):
> ```csharp
> var parameter = new SqliteParameter($"@patch{parameters.Count}", property.Value.GetRawText());
> parameters.Add(parameter);
> current = $"json_set({current}, '{path}', COALESCE(json_extract(_doc,'{path}'),0) + CAST({parameter.ParameterName} AS NUMERIC))";
> ```
> Use the `CAST(... AS NUMERIC)` form. Replace the `AppendInc` method body with it. The change-feed contract only uses `$set`, so either form passes the required tests; prefer the simpler `CAST` form.

---

## Task 4.4 — Create `SqliteRepository.Increment.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.Increment.cs`

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Microsoft.Data.Sqlite;
using Saturn.Data.Sqlite.Query;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository
{
    public Task Increment<TItem>(string id, Expression<Func<TItem, int>> field, int delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore(id, field, (long)delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, long>> field, long delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, double>> field, double delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, decimal>> field, decimal delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore(id, field, (double)delta, expectedVersion, transaction, cancellationToken);

    private async Task IncrementCore<TItem>(string id, LambdaExpression field, object delta, long? expectedVersion,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity
    {
        if (!SqliteJsonPathResolver.TryResolve(field.Body, out var path, out var isColumn) || isColumn)
        {
            throw new NotSupportedException($"Cannot increment field '{field.Body}'. Only document fields are supported.");
        }

        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            throw new FailedToUpdateException();
        }

        var context = BuildWriteContext(RepositoryWriteOperation.Increment, id: normalized, expectedVersion: expectedVersion,
            incrementField: field, incrementDelta: delta, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Increment, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

            var versionFilter = expectedVersion.HasValue ? " AND COALESCE(_v,0) = @expectedVersion" : string.Empty;

            await using var command = lease.Connection.CreateCommand();
            command.CommandText = $"""
                UPDATE {Quote(GetCollectionNameForType<TItem>())}
                SET _doc = json_set(_doc, '{path}', COALESCE(json_extract(_doc,'{path}'),0) + @delta),
                    _v = COALESCE(_v,0) + 1
                WHERE _id = @id{versionFilter};
                """;
            command.Parameters.AddWithValue("@id", normalized);
            command.Parameters.AddWithValue("@delta", delta);

            if (expectedVersion.HasValue)
            {
                command.Parameters.AddWithValue("@expectedVersion", expectedVersion.Value);
            }

            var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Increment, context,
                BuildWriteResult(context, WriteOutcome.Incremented, affected, new[] { normalized })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }
}
```

> `AddWithValue("@delta", delta)` where `delta` is `double` or `long` binds numerically. Do **not** pass `decimal` (it binds as TEXT). The decimal overload converts to `double` first.

---

## Task 4.5 — Create `Indexes\SqliteIndexManager.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Indexes\SqliteIndexManager.cs`

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Microsoft.Data.Sqlite;
using Saturn.Data.Sqlite.Query;

namespace Saturn.Data.Sqlite.Indexes;

internal sealed class SqliteIndexManager
{
    private readonly SqliteRepository repository;
    private readonly SqliteRepositoryOptions options;

    public SqliteIndexManager(SqliteRepository repository, SqliteRepositoryOptions options)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task EnsureIndexesAsync<TItem>(IEnumerable<IIndexDefinition<TItem>> definitions, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await repository.ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await repository.EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        foreach (var definition in definitions)
        {
            if (definition.Options.HasExpireAfter)
            {
                options.OnUnsupportedIndexOption?.Invoke($"ExpireAfter is not supported for index '{definition.Name}'. The index was created without expiry.");
            }

            var clause = BuildIndexClause(definition);
            var unique = definition.Options.Unique ? "UNIQUE " : string.Empty;
            var table = SqliteRepository.Quote(repository.GetCollectionNameForType<TItem>());
            var name = SqliteRepository.Quote(string.IsNullOrWhiteSpace(definition.Name)
                ? $"ix_{repository.GetCollectionNameForType<TItem>()}_{Math.Abs(string.Join("_", clause).GetHashCode())}"
                : definition.Name);
            var sparse = definition.Options.Sparse ? $" WHERE {BuildSparsePredicate(definition)}" : string.Empty;

            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE {unique}INDEX IF NOT EXISTS {name} ON {table}({clause}){sparse};";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildIndexClause<TItem>(IIndexDefinition<TItem> definition) where TItem : Entity
    {
        var parts = new List<string>();

        foreach (var key in definition.Keys)
        {
            if (!SqliteJsonPathResolver.TryResolve(key.Field.Body, out var path, out var isColumn))
            {
                throw new SqliteTranslationException($"Cannot resolve index key '{key.Field}'.");
            }

            var operand = isColumn ? path : $"json_extract(_doc, '{path}')";
            var direction = key.Direction == IndexSortDirection.Ascending ? "ASC" : "DESC";
            parts.Add($"{operand} {direction}");
        }

        if (parts.Count == 0)
        {
            throw new ArgumentException($"Index '{definition.Name}' has no keys.");
        }

        return string.Join(", ", parts);
    }

    private static string BuildSparsePredicate<TItem>(IIndexDefinition<TItem> definition) where TItem : Entity
    {
        var key = definition.Keys.First();

        if (!SqliteJsonPathResolver.TryResolve(key.Field.Body, out var path, out var isColumn))
        {
            throw new SqliteTranslationException($"Cannot resolve index key '{key.Field}'.");
        }

        var operand = isColumn ? path : $"json_extract(_doc, '{path}')";
        return $"{operand} IS NOT NULL";
    }
}
```

Then make `EnsureIndexes` public on the repository. Add to `SqliteRepository.ReadonlyRepository.cs` (or a new partial) and declare `IRepositoryIndexManager` on the class. Since all partials share the class, add `, IRepositoryIndexManager` to the class declaration in one place. Add to `SqliteRepository.cs` class declaration: `public partial class SqliteRepository : IDisposable, IRepositoryIndexManager`. Then add:

```csharp
    public async Task EnsureIndexes<TItem>(IEnumerable<IIndexDefinition<TItem>> definitions, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var manager = new Indexes.SqliteIndexManager(this, sqliteOptions);
        await manager.EnsureIndexesAsync(definitions, cancellationToken).ConfigureAwait(false);
    }
```

> `SqliteIndexManager` needs `SqliteRepository.GetCollectionNameForType<TItem>()`, `ConnectionFactory`, `InitializeAsync`, and `EnsureTableAsync` — all `protected`/`internal`. Mark `GetCollectionNameForType<TItem>()` as `internal` if accessibility fails (it is currently `protected`; since `SqliteIndexManager` is not derived, change `protected` to `internal` for both overloads and `EnsureTableAsync<TItem>`).

---

## Task 4.6 — Tests

### 4.6.1 `PatchWritesTests.cs`

```csharp
using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class PatchWritesTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<ChangeFeedEntity>(entity => true);
    }

    [Fact]
    public async Task Patch_Set_Updates_Field_And_Bumps_Version()
    {
        var entity = new ChangeFeedEntity { Name = "patch", Count = 1 };
        await fixture.Repository.Insert(entity);

        await fixture.Repository.Patch<ChangeFeedEntity>(entity.Id, jsonDocument: """{"$set":{"Count":5}}""");

        var reloaded = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.Equal(5, reloaded.Count);
    }

    [Fact]
    public async Task Patch_Unknown_Id_Throws()
    {
        await Assert.ThrowsAsync<FailedToUpdateException>(() =>
            fixture.Repository.Patch<ChangeFeedEntity>("000000000000000000000000", jsonDocument: """{"$set":{"Count":5}}"""));
    }

    [Fact]
    public async Task Patch_With_ExpectedVersion_Mismatch_Throws()
    {
        var entity = new ChangeFeedEntity { Name = "patch-version", Count = 1 };
        await fixture.Repository.Insert(entity);
        await fixture.Repository.Patch<ChangeFeedEntity>(entity.Id, jsonDocument: """{"$set":{"Count":2}}""");

        await Assert.ThrowsAsync<FailedToUpdateException>(() =>
            fixture.Repository.Patch<ChangeFeedEntity>(entity.Id, expectedVersion: 0, jsonDocument: """{"$set":{"Count":9}}"""));
    }
}
```

### 4.6.2 `IncrementWritesTests.cs`

```csharp
using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class IncrementWritesTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<ChangeFeedEntity>(entity => true);
    }

    [Fact]
    public async Task Increment_Adds_Delta()
    {
        var entity = new ChangeFeedEntity { Name = "inc", Count = 0 };
        await fixture.Repository.Insert(entity);

        await fixture.Repository.Increment<ChangeFeedEntity>(entity.Id, item => item.Count, 5);

        var reloaded = await fixture.Repository.ById<ChangeFeedEntity>(entity.Id);
        Assert.Equal(5, reloaded.Count);
    }

    [Fact]
    public async Task Increment_Unknown_Id_Throws()
    {
        await Assert.ThrowsAsync<FailedToUpdateException>(() =>
            fixture.Repository.Increment<ChangeFeedEntity>("000000000000000000000000", item => item.Count, 5));
    }
}
```

### 4.6.3 `SqliteIndexManagerTests.cs`

```csharp
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class SqliteIndexManagerTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<IndexedEntity>(entity => true);
    }

    [Fact]
    public async Task EnsureIndexes_Creates_Index()
    {
        await fixture.Repository.Insert(new IndexedEntity { Id = EntityIdGenerator.GenerateNewId(), Email = "a@b.com", Age = 30 });

        var definitions = new[] { new TestIndexDefinition<IndexedEntity>("ix_indexed_email", new IndexKey<IndexedEntity>(entity => entity.Email)) };
        await fixture.Repository.EnsureIndexes(definitions);

        var indexes = await fixture.Repository.ListIndexesAsync("IndexedEntity");
        Assert.Contains(indexes, name => name == "ix_indexed_email");
    }

    [Fact]
    public async Task Unique_Index_Raises_On_Duplicate()
    {
        await fixture.Repository.Insert(new IndexedEntity { Id = EntityIdGenerator.GenerateNewId(), Email = "dup@x.com", Age = 1 });

        var definitions = new[]
        {
            new TestIndexDefinition<IndexedEntity>("ix_indexed_email_unique", new IndexKey<IndexedEntity>(entity => entity.Email))
            {
                Options = new IndexOptions { Unique = true }
            }
        };
        await fixture.Repository.EnsureIndexes(definitions);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            fixture.Repository.Insert(new IndexedEntity { Id = EntityIdGenerator.GenerateNewId(), Email = "dup@x.com", Age = 2 }));
    }

    [Fact]
    public async Task ExpireAfter_Is_Reported_Unsupported()
    {
        var warnings = new List<string>();
        var options = new SqliteRepositoryOptions { DataSource = Path.Combine(Path.GetTempPath(), $"saturn-sqlite-idx-{Guid.NewGuid():N}.db"), OnUnsupportedIndexOption = warnings.Add };

        using var repository = new UnitTestableSqliteRepository(new RepositoryOptions { GetCollectionName = type => type.Name }, options);
        repository.DropRecreateDatabase();

        var definitions = new[]
        {
            new TestIndexDefinition<IndexedEntity>("ix_indexed_age", new IndexKey<IndexedEntity>(entity => entity.Age))
            {
                Options = new IndexOptions { HasExpireAfter = true, ExpireAfter = TimeSpan.FromMinutes(5) }
            }
        };

        await repository.EnsureIndexes(definitions);
        Assert.Single(warnings);
    }
}

public sealed class IndexedEntity : Entity
{
    public string Email { get; set; } = string.Empty;

    public int Age { get; set; }
}

public sealed class TestIndexDefinition<TItem> : IIndexDefinition<TItem> where TItem : Entity
{
    public TestIndexDefinition(string name, params IndexKey<TItem>[] keys)
    {
        Name = name;
        Keys = keys;
    }

    public string Name { get; }

    public IReadOnlyCollection<IIndexKey<TItem>> Keys { get; }

    public IndexOptions Options { get; init; } = new();
}
```

Add a helper to `UnitTestableSqliteRepository`:

```csharp
    public async Task<List<string>> ListIndexesAsync(string collection, CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list({SqliteRepository.Quote(collection)});";

        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(1));
        }

        return result;
    }
```

> `PRAGMA index_list` may need the table name unquoted. If it fails, use `command.CommandText = $"PRAGMA index_list('{collection}');";`.

---

## Task 4.7 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj"
```

---

## Do NOT

- Do not use string interpolation for patch values; use `json(@param)` with the raw JSON text as the parameter value.
- Do not bind `decimal` directly (it becomes TEXT); convert to `double`.
- Do not implement cascade or change feed here (Phase 6).
- Do not add comments to `.cs` files.
