# Phase 1 — Serialization, Storage, Core CRUD

**Goal:** Persist entities as JSON documents, create tables lazily, and implement the unscoped `IReadonlyRepository` + `IRepository` surface so `BasicRepositoryContractTests` passes.

**Prerequisite:** Phase 0 is complete and its tests pass.

**Exit criteria:**
- `dotnet test` runs `BasicTests` (subclass of `BasicRepositoryContractTests`) green: `Add_And_Get_By_Id`, `Update`, `Save_And_Upsert_Logic`.
- Document round-trips delete `Changes`/`EnableChangeTracking`/`_shortId` and serialize `Ref<T>` as a string.

**What is intentionally NOT in this phase:** full predicate translation, sorting/paging/continuation, `IQueryable` pushdown, scoped variants, transactions, patch/increment, cascade, change feed. Those are later phases. In this phase, `IQueryable` may fall back to in-memory.

---

## Task 1.1 — Create the serialization files

### 1.1.1 `Serialization\RefJsonConverter.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Serialization\RefJsonConverter.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite.Serialization;

public sealed class RefJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Ref<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(
            typeof(RefJsonConverter<>).MakeGenericType(typeToConvert.GenericTypeArguments[0]))!;
}

public sealed class RefJsonConverter<T> : JsonConverter<Ref<T>> where T : Entity, new()
{
    public override Ref<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : new Ref<T>(value);
    }

    public override void Write(Utf8JsonWriter writer, Ref<T> value, JsonSerializerOptions options)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.Id))
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Id);
    }
}
```

### 1.1.2 `Serialization\WeakRefJsonConverter.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Serialization\WeakRefJsonConverter.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite.Serialization;

public sealed class WeakRefJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert == typeof(WeakRef)
           || (typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(WeakRef<>));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => typeToConvert == typeof(WeakRef)
            ? new WeakRefJsonConverter()
            : (JsonConverter)Activator.CreateInstance(
                typeof(WeakRefOfJsonConverter<>).MakeGenericType(typeToConvert.GenericTypeArguments[0]))!;
}

public sealed class WeakRefJsonConverter : JsonConverter<WeakRef>
{
    public override WeakRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : new WeakRef(value);
    }

    public override void Write(Utf8JsonWriter writer, WeakRef value, JsonSerializerOptions options)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.Id))
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Id);
    }
}

public sealed class WeakRefOfJsonConverter<T> : JsonConverter<WeakRef<T>> where T : Entity
{
    public override WeakRef<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : new WeakRef<T>(value);
    }

    public override void Write(Utf8JsonWriter writer, WeakRef<T> value, JsonSerializerOptions options)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.Id))
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Id);
    }
}
```

### 1.1.3 `Serialization\PropertiesJsonConverter.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Serialization\PropertiesJsonConverter.cs`

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Saturn.Data.Sqlite.Serialization;

public sealed class PropertiesJsonConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected a JSON object.");
        }

        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return result;
            }

            var key = reader.GetString()!;
            reader.Read();
            result[key] = ReadValue(ref reader);
        }

        throw new JsonException("Unterminated JSON object.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var pair in value)
        {
            writer.WritePropertyName(pair.Key);
            WriteValue(writer, pair.Value, options);
        }

        writer.WriteEndObject();
    }

    private static object ReadValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.String when reader.TryGetDateTime(out var dateTime):
                return dateTime;
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number when reader.TryGetInt64(out var integer):
                return integer;
            case JsonTokenType.Number:
                return reader.GetDouble();
            default:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    return document.RootElement.Clone();
                }
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime);
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
```

### 1.1.4 `Serialization\EntityJsonTypeInfoResolver.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Serialization\EntityJsonTypeInfoResolver.cs`

```csharp
using System.Text.Json.Serialization.Metadata;

namespace Saturn.Data.Sqlite.Serialization;

public static class EntityJsonTypeInfoResolver
{
    private static readonly HashSet<string> IgnoredMembers = new(StringComparer.Ordinal)
    {
        "EnableChangeTracking",
        "Changes",
        "_shortId"
    };

    public static IJsonTypeInfoResolver Create()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(Modify);
        return resolver;
    }

    private static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            if (IgnoredMembers.Contains(typeInfo.Properties[index].Name))
            {
                typeInfo.Properties.RemoveAt(index);
            }
        }
    }
}
```

### 1.1.5 `Serialization\EntityJsonSerializerOptions.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Serialization\EntityJsonSerializerOptions.cs`

```csharp
namespace Saturn.Data.Sqlite.Serialization;

public sealed class EntityJsonSerializerOptions
{
    public bool WriteIndented { get; set; }

    public bool EnumAsString { get; set; } = true;
}
```

### 1.1.6 `Serialization\EntityJsonSerializer.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Serialization\EntityJsonSerializer.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Saturn.Data.Sqlite.Serialization;

public sealed class EntityJsonSerializer
{
    private readonly JsonSerializerOptions options;

    public EntityJsonSerializer(EntityJsonSerializerOptions serializerOptions = null)
    {
        serializerOptions ??= new EntityJsonSerializerOptions();

        options = new JsonSerializerOptions
        {
            WriteIndented = serializerOptions.WriteIndented,
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            TypeInfoResolver = EntityJsonTypeInfoResolver.Create()
        };

        options.Converters.Add(new RefJsonConverterFactory());
        options.Converters.Add(new WeakRefJsonConverterFactory());
        options.Converters.Add(new PropertiesJsonConverter());

        if (serializerOptions.EnumAsString)
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }
    }

    public JsonSerializerOptions JsonOptions => options;

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, options);

    public string Serialize(object value, Type type) => JsonSerializer.Serialize(value, type, options);

    public T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, options);

    public object Deserialize(string json, Type type) => JsonSerializer.Deserialize(json, type, options);
}
```

---

## Task 1.2 — Create the query core files

### 1.2.1 `Query\SqliteTranslationException.cs`

```csharp
namespace Saturn.Data.Sqlite.Query;

public sealed class SqliteTranslationException : Exception
{
    public SqliteTranslationException(string message) : base(message)
    {
    }

    public SqliteTranslationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
```

### 1.2.2 `Query\SqlFragment.cs`

```csharp
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite.Query;

public sealed class SqlFragment
{
    public string Sql { get; init; } = "1=1";

    public IReadOnlyList<SqliteParameter> Parameters { get; init; } = Array.Empty<SqliteParameter>();

    public static SqlFragment AlwaysTrue { get; } = new() { Sql = "1=1" };

    public static SqlFragment AlwaysFalse { get; } = new() { Sql = "1=0" };

    public static SqlFragment Combine(SqlFragment left, SqlFragment right, string op)
        => new()
        {
            Sql = $"({left.Sql}) {op} ({right.Sql})",
            Parameters = left.Parameters.Concat(right.Parameters).ToList()
        };
}
```

### 1.2.3 `Query\SqliteJsonPathResolver.cs`

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite.Query;

public static class SqliteJsonPathResolver
{
    public static bool TryResolve(Expression expression, out string path, out bool isColumn)
    {
        path = null;
        isColumn = false;

        var current = Unwrap(expression);

        if (current is not MemberExpression member)
        {
            return false;
        }

        var segments = new List<string>();
        Expression cursor = member;

        while (cursor is MemberExpression segment)
        {
            segments.Insert(0, segment.Member.Name);
            cursor = Unwrap(segment.Expression);
        }

        if (cursor is not ParameterExpression)
        {
            return false;
        }

        if (segments.Count >= 2 && segments[^1] == nameof(Entity.Id) && IsReferenceType(member.Expression?.Type))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        if (segments.Count == 0)
        {
            return false;
        }

        if (segments.Count == 1 && segments[0] == nameof(Entity.Id))
        {
            path = "_id";
            isColumn = true;
            return true;
        }

        if (segments.Count == 1 && segments[0] == nameof(Entity.Version))
        {
            path = "_v";
            isColumn = true;
            return true;
        }

        if (segments.Count == 1 && segments[0] == "ScopeId")
        {
            path = "$.Scope";
            return true;
        }

        if (segments.Count == 1 && segments[0] == "SecondScopeId")
        {
            path = "$.SecondScope";
            return true;
        }

        path = "$." + string.Join(".", segments);
        return true;
    }

    public static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }

    public static bool IsReferenceType(Type type)
    {
        if (type is null)
        {
            return false;
        }

        if (type == typeof(WeakRef))
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(Ref<>) || definition == typeof(WeakRef<>);
    }
}
```

### 1.2.4 `Query\SqliteExpressionTranslator.cs` (subset — expanded in Phase 2)

```csharp
using System.Collections;
using System.Linq.Expressions;
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
                return new SqlFragment { Sql = $"NOT ({Visit(not.Operand).Sql})", Parameters = Visit(not.Operand).Parameters };

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
                return VisitComparison(node, "=");

            case ExpressionType.NotEqual:
                return VisitComparison(node, "<>");

            case ExpressionType.GreaterThan:
                return VisitComparison(node, ">");

            case ExpressionType.GreaterThanOrEqual:
                return VisitComparison(node, ">=");

            case ExpressionType.LessThan:
                return VisitComparison(node, "<");

            case ExpressionType.LessThanOrEqual:
                return VisitComparison(node, "<=");

            default:
                throw new SqliteTranslationException($"Unsupported binary operator '{node.NodeType}'.");
        }
    }

    private SqlFragment VisitComparison(BinaryExpression node, string op)
    {
        var left = SqliteJsonPathResolver.Unwrap(node.Left);
        var right = SqliteJsonPathResolver.Unwrap(node.Right);

        if (SqliteJsonPathResolver.TryResolve(left, out var leftPath, out var leftIsColumn)
            && TryEvaluate(right, out var rightValue))
        {
            return BuildComparison(leftPath, leftIsColumn, op, rightValue);
        }

        if (SqliteJsonPathResolver.TryResolve(right, out var rightPath, out var rightIsColumn)
            && TryEvaluate(left, out var leftValue))
        {
            return BuildComparison(rightPath, rightIsColumn, op, leftValue);
        }

        throw new SqliteTranslationException($"Cannot translate comparison between '{left}' and '{right}'.");
    }

    private SqlFragment BuildComparison(string path, bool isColumn, string op, object value)
    {
        var parameter = CreateParameter(value);
        var operand = isColumn ? path : JsonExtract(path);
        return new SqlFragment { Sql = $"{operand} {op} {parameter.ParameterName}", Parameters = new[] { parameter } };
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
        if (TryTranslateContains(node, out var contains))
        {
            return contains;
        }

        throw new SqliteTranslationException($"Unsupported method call '{node.Method.Name}'.");
    }

    private bool TryTranslateContains(MethodCallExpression node, out SqlFragment fragment)
    {
        fragment = null;

        if (node.Method.Name != nameof(Enumerable.Contains))
        {
            return false;
        }

        Expression collectionExpression;
        Expression itemExpression;

        if (node.Object is not null && node.Arguments.Count == 1)
        {
            collectionExpression = node.Object;
            itemExpression = node.Arguments[0];
        }
        else if (node.Arguments.Count == 2)
        {
            collectionExpression = node.Arguments[0];
            itemExpression = node.Arguments[1];
        }
        else
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

    private SqliteParameter CreateParameter(object value)
    {
        var parameter = new SqliteParameter($"@p{parameterIndex++}", value ?? DBNull.Value);
        return parameter;
    }

    private static string JsonExtract(string path) => $"json_extract(_doc, '{path}')";

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

---

## Task 1.3 — Update `SqliteRepository.cs` (add serializer, helpers, DDL, lease)

Add to the top of `SqliteRepository.cs` usings:

```csharp
using System.Linq.Expressions;
using System.Text;
using Saturn.Data.Sqlite.Serialization;
```

Add these fields next to the existing fields:

```csharp
    private readonly EntityJsonSerializer serializer;
```

Add this line inside the constructor after `connectionFactory = ...`:

```csharp
        serializer = new EntityJsonSerializer();
```

Add these members to the class (append before the closing brace):

```csharp
    protected EntityJsonSerializer Serializer => serializer;

    protected bool HasWriteBehaviors => options.WriteBehaviors is { Count: > 0 };

    internal static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    protected static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return Entity.TryParseId(id, out var normalized) && normalized is not null ? normalized : null;
    }

    protected static List<string> NormalizeEntityIds(IEnumerable<string> ids)
    {
        var result = new List<string>();

        if (ids is null)
        {
            return result;
        }

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (Entity.TryParseId(id, out var normalized) && normalized is not null && !result.Contains(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    internal static bool SupportsSoftDelete<TItem>() where TItem : Entity => typeof(ISoftDeletable).IsAssignableFrom(typeof(TItem));

    internal static bool SupportsArchivable<TItem>() where TItem : Entity => typeof(IArchivable).IsAssignableFrom(typeof(TItem));

    protected async Task<SqliteConnectionLease> RentConnectionAsync(IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new SqliteConnectionLease(connection, ownsConnection: true);
    }

    protected Task EnsureTableAsync<TItem>(SqliteConnection connection, CancellationToken cancellationToken) where TItem : Entity
        => EnsureTableAsync(GetCollectionNameForType<TItem>(), connection, cancellationToken);

    internal async Task EnsureTableAsync(string collection, SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (knownTables.ContainsKey(collection))
        {
            return;
        }

        var table = Quote(collection);

        var ddl = $"""
            CREATE TABLE IF NOT EXISTS {table} (
                _id TEXT NOT NULL PRIMARY KEY,
                _v INTEGER NULL,
                _deleted INTEGER NOT NULL DEFAULT 0,
                _scope TEXT NULL,
                _scope2 TEXT NULL,
                _archived INTEGER NOT NULL DEFAULT 0,
                _doc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS {Quote($"ix_{collection}__scope")} ON {table}(_scope);
            CREATE INDEX IF NOT EXISTS {Quote($"ix_{collection}__deleted")} ON {table}(_deleted);
            CREATE INDEX IF NOT EXISTS {Quote($"ix_{collection}__scope2")} ON {table}(_scope2);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        knownTables.TryAdd(collection, 0);
    }
```

Add these behavior-pipeline members (also in `SqliteRepository.cs`):

```csharp
    protected RepositoryWriteContext<TItem> BuildWriteContext<TItem>(
        RepositoryWriteOperation operation,
        TItem item = null,
        IEnumerable<TItem> items = null,
        string id = null,
        IEnumerable<string> ids = null,
        Expression<Func<TItem, bool>> filter = null,
        long? expectedVersion = null,
        string jsonDocument = null,
        IDataUpdateDefinition<TItem> updateDefinition = null,
        IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var itemList = items?.ToList() ?? (item is null ? null : new List<TItem> { item });

        return new RepositoryWriteContext<TItem>
        {
            Operation = operation,
            Id = id,
            Ids = ids?.ToList(),
            Items = itemList,
            Filter = filter,
            ExpectedVersion = expectedVersion,
            JsonDocument = jsonDocument,
            UpdateDefinition = updateDefinition,
            Transaction = transaction,
            CancellationToken = cancellationToken
        };
    }

    protected ValueTask DispatchWriteBehaviorsAsync<TItem>(RepositoryWriteOperation operation, RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => BehaviorDispatcher.DispatchBeforeAsync(options.WriteBehaviors, operation, context);

    protected ValueTask ApplyAfterBehaviors<TItem>(RepositoryWriteOperation operation, RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => BehaviorDispatcher.DispatchAfterAsync(options.WriteBehaviors, operation, context, result);

    protected ValueTask ApplyOnWriteFailed<TItem>(RepositoryWriteContext<TItem> context, Exception exception)
        where TItem : Entity
        => BehaviorDispatcher.DispatchOnWriteFailedAsync(options.WriteBehaviors, context, exception);

    protected static RepositoryWriteResult BuildWriteResult<TItem>(
        RepositoryWriteContext<TItem> context,
        WriteOutcome outcome,
        int affected,
        IEnumerable<string> entityIds = null,
        bool wasCreated = false,
        IReadOnlyCollection<string> matchedIds = null,
        bool partialFailure = false,
        int failedCount = 0)
        where TItem : Entity
        => new()
        {
            Operation = context.Operation,
            Succeeded = true,
            PartialFailure = partialFailure,
            Outcome = outcome,
            AffectedCount = affected,
            FailedCount = failedCount,
            EntityIds = entityIds?.ToList() ?? Array.Empty<string>(),
            MatchedIds = matchedIds ?? Array.Empty<string>(),
            WasCreated = wasCreated,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
```

---

## Task 1.4 — Create `SqliteConnectionLease.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteConnectionLease.cs`

```csharp
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

internal sealed class SqliteConnectionLease : IAsyncDisposable
{
    private readonly bool ownsConnection;

    public SqliteConnectionLease(SqliteConnection connection, bool ownsConnection)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.ownsConnection = ownsConnection;
    }

    public SqliteConnection Connection { get; }

    public async ValueTask DisposeAsync()
    {
        if (ownsConnection)
        {
            await Connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
```

---

## Task 1.5 — Create `SqliteRepository.ReadonlyRepository.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.ReadonlyRepository.cs`

Implement `IReadonlyRepository`. Copy each method signature **verbatim** from `D:\Work\Saturn.Data\Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions\IReadonlyRepository.cs`. Do **not** implement methods that already have a default body in the interface unless noted. The methods you MUST implement:

- `All<TItem>(IDatabaseTransaction, CancellationToken)`
- `ById<TItem>(string, IDatabaseTransaction, CancellationToken)`
- `ById<TItem>(string, bool, IDatabaseTransaction, CancellationToken)`
- `ById<TItem>(IEnumerable<string>, IDatabaseTransaction, CancellationToken)`
- `ById<TItem>(IEnumerable<string>, bool, IDatabaseTransaction, CancellationToken)`
- `Count<TItem>(Expression<Func<TItem,bool>>, string, IDatabaseTransaction, CancellationToken)`
- `Count<TItem>(Expression<Func<TItem,bool>>, string, bool, IDatabaseTransaction, CancellationToken)`
- `IQueryable<TItem>()`
- `IQueryable<TItem>(bool)`
- `Many<TItem>(Expression<Func<TItem,bool>>, string, int?, int?, IEnumerable<SortOrder<TItem>>, IDatabaseTransaction, CancellationToken)`
- `Many<TItem>(Expression<Func<TItem,bool>>, string, int?, int?, IEnumerable<SortOrder<TItem>>, bool, IDatabaseTransaction, CancellationToken)`
- `Many<TItem>(Dictionary<string,object>, string, int?, int?, IEnumerable<SortOrder<TItem>>, IDatabaseTransaction, CancellationToken)`
- `Many<TItem>(Dictionary<string,object>, string, int?, int?, IEnumerable<SortOrder<TItem>>, bool, IDatabaseTransaction, CancellationToken)`
- `One<TItem>(Expression<Func<TItem,bool>>, string, IEnumerable<SortOrder<TItem>>, IDatabaseTransaction, CancellationToken)`
- `One<TItem>(Expression<Func<TItem,bool>>, string, IEnumerable<SortOrder<TItem>>, bool, IDatabaseTransaction, CancellationToken)`
- `Random<TItem>(Expression<Func<TItem,bool>>, string, int, IDatabaseTransaction, CancellationToken)`
- `Random<TItem>(Expression<Func<TItem,bool>>, string, int, bool, IDatabaseTransaction, CancellationToken)`

Use this file header:

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Microsoft.Data.Sqlite;
using Saturn.Data.Sqlite.Query;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IReadonlyRepository
{
    ...methods...
}
```

### 1.5.1 Shared read helpers

Add these private helpers inside this file:

```csharp
    private SqlFragment BuildReadPredicate<TItem>(Expression<Func<TItem, bool>> predicate, bool includeDeleted)
        where TItem : Entity
    {
        var fragment = new SqliteExpressionTranslator().Translate(predicate) ?? SqlFragment.AlwaysTrue;

        if (!includeDeleted && SupportsSoftDelete<TItem>())
        {
            fragment = SqlFragment.Combine(fragment, new SqlFragment { Sql = "_deleted = 0" }, "AND");
        }

        return fragment;
    }

    private async Task<List<TItem>> LoadListAsync<TItem>(SqlFragment predicate, string orderBy, int? limit, int? offset, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

        var table = Quote(GetCollectionNameForType<TItem>());

        var builder = new StringBuilder($"SELECT _doc FROM {table}");
        if (!string.IsNullOrWhiteSpace(predicate.Sql))
        {
            builder.Append($" WHERE {predicate.Sql}");
        }
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            builder.Append($" ORDER BY {orderBy}");
        }
        if (limit.HasValue)
        {
            builder.Append(" LIMIT @limit");
        }
        if (offset.HasValue)
        {
            builder.Append(" OFFSET @offset");
        }
        builder.Append(';');

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = builder.ToString();

        foreach (var parameter in predicate.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }
        if (limit.HasValue)
        {
            command.Parameters.AddWithValue("@limit", limit.Value);
        }
        if (offset.HasValue)
        {
            command.Parameters.AddWithValue("@offset", offset.Value);
        }

        var results = new List<TItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
            {
                results.Add(serializer.Deserialize<TItem>(reader.GetString(0)));
            }
        }

        return results;
    }

    private static SqliteParameter CloneParameter(SqliteParameter source)
        => new(source.ParameterName, source.Value ?? DBNull.Value);

    private const int DefaultPageSize = 20;

    private static string BuildOrderBy<TItem>(IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        if (sortOrders is null)
        {
            return null;
        }

        var clauses = new List<string>();

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
        }

        return clauses.Count == 0 ? null : string.Join(", ", clauses);
    }

    private static Expression UnwrapLambdaBody(LambdaExpression lambda)
        => SqliteJsonPathResolver.Unwrap(lambda.Body);
```

> Note: `SqliteExpressionTranslator` is created per call; that is correct because it holds a parameter counter. Do not cache or share an instance.

### 1.5.2 `All<TItem>`

```csharp
    public async Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => await All<TItem>(includeDeleted: false, transaction, cancellationToken).ConfigureAwait(false);

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var predicate = BuildReadPredicate<TItem>(null, includeDeleted);
        var list = await LoadListAsync<TItem>(predicate, null, null, null, transaction, cancellationToken).ConfigureAwait(false);
        return AsyncEnumerableFactory.From(list, cancellationToken);
    }
```

### 1.5.3 `ById<TItem>` (single and multiple)

```csharp
    public Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => ById<TItem>(id, includeDeleted: false, transaction, cancellationToken);

    public async Task<TItem> ById<TItem>(string id, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            return null;
        }

        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

        var table = Quote(GetCollectionNameForType<TItem>());
        var sql = includeDeleted || !SupportsSoftDelete<TItem>()
            ? $"SELECT _doc FROM {table} WHERE _id = @id;"
            : $"SELECT _doc FROM {table} WHERE _id = @id AND _deleted = 0;";

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", normalized);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is string document ? serializer.Deserialize<TItem>(document) : null;
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => ById<TItem>(IDs, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeEntityIds(IDs);

        if (normalized.Count == 0)
        {
            return AsyncEnumerableFactory.From(Array.Empty<TItem>(), cancellationToken);
        }

        Expression<Func<TItem, bool>> predicate = item => normalized.Contains(item.Id);
        var fragment = BuildReadPredicate(predicate, includeDeleted);
        var list = await LoadListAsync<TItem>(fragment, null, null, null, transaction, cancellationToken).ConfigureAwait(false);
        return AsyncEnumerableFactory.From(list, cancellationToken);
    }
```

### 1.5.4 `Count<TItem>`

```csharp
    public Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Count<TItem>(predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var fragment = BuildReadPredicate(predicate, includeDeleted);

        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

        var table = Quote(GetCollectionNameForType<TItem>());
        await using var command = lease.Connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM {table} WHERE {fragment.Sql};";

        foreach (var parameter in fragment.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result);
    }
```

### 1.5.5 `IQueryable<TItem>` (temporary in-memory)

```csharp
    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
        => IQueryable<TItem>(includeDeleted: false);

    public IQueryable<TItem> IQueryable<TItem>(bool includeDeleted) where TItem : Entity
    {
        var list = LoadListAsync<TItem>(BuildReadPredicate<TItem>(null, includeDeleted), null, null, null, null, CancellationToken.None).GetAwaiter().GetResult();
        return list.AsQueryable();
    }
```

### 1.5.6 `Many<TItem>` / `One<TItem>` / `Random<TItem>`

Implement the predicate-based overloads using `BuildReadPredicate`, `BuildOrderBy`, paging, and `LoadListAsync`. `continueFrom` is ignored in this phase. The `whereClause` overloads convert the dictionary to a predicate using the same path resolution as Phase 2; for this phase implement them by building an `Expression` that ANDs `item.<Key> == value` for each entry, then delegate to the predicate overload.

```csharp
    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Many(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var fragment = BuildReadPredicate(predicate, includeDeleted);
        var orderBy = BuildOrderBy(sortOrders);

        int? limit = null;
        int? offset = null;

        if (pageSize.HasValue)
        {
            limit = pageSize.Value;
            if (pageNumber.HasValue && pageNumber.Value > 1)
            {
                offset = (pageNumber.Value - 1) * pageSize.Value;
            }
        }

        var list = await LoadListAsync<TItem>(fragment, orderBy, limit, offset, transaction, cancellationToken).ConfigureAwait(false);
        return AsyncEnumerableFactory.From(list, cancellationToken);
    }

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => await One(predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken).ConfigureAwait(false);

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var fragment = BuildReadPredicate(predicate, includeDeleted);
        var orderBy = BuildOrderBy(sortOrders);
        var list = await LoadListAsync<TItem>(fragment, orderBy, 1, null, transaction, cancellationToken).ConfigureAwait(false);
        return list.Count == 0 ? null : list[0];
    }

    public Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Random(predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var fragment = BuildReadPredicate(predicate, includeDeleted);
        var list = await LoadListAsync<TItem>(fragment, "RANDOM()", count, null, transaction, cancellationToken).ConfigureAwait(false);
        return AsyncEnumerableFactory.From(list, cancellationToken);
    }
```

For the `whereClause` overloads, build the expression:

```csharp
    private static Expression<Func<TItem, bool>> BuildWhereClausePredicate<TItem>(Dictionary<string, object> whereClause)
        where TItem : Entity
    {
        if (whereClause is null || whereClause.Count == 0)
        {
            return item => true;
        }

        var parameter = Expression.Parameter(typeof(TItem), "item");
        Expression? body = null;

        foreach (var pair in whereClause)
        {
            var property = Expression.Property(parameter, pair.Key);
            var constant = Expression.Constant(pair.Value, property.Type);
            Expression comparison = Expression.Equal(property, constant);
            body = body is null ? comparison : Expression.AndAlso(body, comparison);
        }

        return Expression.Lambda<Func<TItem, bool>>(body!, parameter);
    }
```

Then the `whereClause` overloads call the predicate overload with `BuildWhereClausePredicate<TItem>(whereClause)`.

### 1.5.7 `AsyncEnumerableFactory`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\AsyncEnumerableFactory.cs`:

```csharp
using System.Runtime.CompilerServices;

namespace Saturn.Data.Sqlite;

internal static class AsyncEnumerableFactory
{
    public static async IAsyncEnumerable<T> From<T>(IEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}
```

---

## Task 1.6 — Create `SqliteRepository.Repository.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.Repository.cs`

Implement `IRepository`. Copy method signatures **verbatim** from `D:\Work\Saturn.Data\Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions\IRepository.cs`. Implement:

- `CreateTransaction()` — in this phase return `throw new NotImplementedException("Transactions are implemented in Phase 5.");`
- `Delete<TItem>(filter)`, `Delete<TItem>(id)`, `Delete<TItem>(IDs)`
- `Insert<TItem>(entity)`, `Insert<TItem>(entities)`
- `Save<TItem>(entity)`, `Save<TItem>(entities)`
- `Update<TItem>(entity)`, `Update<TItem>(predicate, entity)`, `Update<TItem>(entities)`
- `Upsert<TItem>(entity)`, `Upsert<TItem>(entities)`
- `JsonUpdate<TItem>(...)` — Phase 4; here throw `new NotSupportedException("JsonUpdate is implemented in Phase 4.");`
- `HardDelete<TItem>(filter)`, `HardDelete<TItem>(id)`, `HardDelete<TItem>(IDs)`
- `Restore<TItem>(id)`, `Restore<TItem>(IDs)`, `Restore<TItem>(filter)`
- `Patch<TItem>(...)` — Phase 4; throw `new NotSupportedException("Patch is implemented in Phase 4.");`
- `Increment<TItem>(...)` — four overloads; Phase 4; throw `new NotSupportedException("Increment is implemented in Phase 4.");`
- `DeleteCascade<TItem>(...)` and `HardDeleteCascade<TItem>(...)` — Phase 6; throw `new NotSupportedException("Cascade is implemented in Phase 6.");`

File header:

```csharp
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;
using Microsoft.Data.Sqlite;
using Saturn.Data.Sqlite.Query;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IRepository
{
    ...methods...
}
```

### 1.6.1 Private write helpers

Add these inside the file:

```csharp
    private const string InsertSqlTemplate = """
        INSERT INTO {0} (_id,_v,_deleted,_scope,_scope2,_archived,_doc)
        VALUES (@id, json_extract(@doc,'$.Version'), COALESCE(json_extract(@doc,'$.IsDeleted'),0),
                json_extract(@doc,'$.Scope'), json_extract(@doc,'$.SecondScope'),
                COALESCE(json_extract(@doc,'$.IsArchived'),0), @doc);
        """;

    private const string UpsertSqlTemplate = """
        INSERT INTO {0} (_id,_v,_deleted,_scope,_scope2,_archived,_doc)
        VALUES (@id, json_extract(@doc,'$.Version'), COALESCE(json_extract(@doc,'$.IsDeleted'),0),
                json_extract(@doc,'$.Scope'), json_extract(@doc,'$.SecondScope'),
                COALESCE(json_extract(@doc,'$.IsArchived'),0), @doc)
        ON CONFLICT(_id) DO UPDATE SET
            _doc = excluded._doc,
            _v = excluded._v,
            _deleted = excluded._deleted,
            _scope = excluded._scope,
            _scope2 = excluded._scope2,
            _archived = excluded._archived;
        """;

    private async Task<int> ExecuteInsertAsync<TItem>(SqliteConnection connection, TItem entity, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(InsertSqlTemplate, Quote(GetCollectionNameForType<TItem>()));
        command.Parameters.AddWithValue("@id", entity.Id);
        command.Parameters.AddWithValue("@doc", serializer.Serialize(entity));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteUpsertAsync<TItem>(SqliteConnection connection, TItem entity, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(UpsertSqlTemplate, Quote(GetCollectionNameForType<TItem>()));
        command.Parameters.AddWithValue("@id", entity.Id);
        command.Parameters.AddWithValue("@doc", serializer.Serialize(entity));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteUpdateByIdAsync<TItem>(SqliteConnection connection, TItem entity, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

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
        command.Parameters.AddWithValue("@id", entity.Id);
        command.Parameters.AddWithValue("@doc", serializer.Serialize(entity));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ExistsByIdAsync<TItem>(SqliteConnection connection, string id, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT EXISTS(SELECT 1 FROM {Quote(GetCollectionNameForType<TItem>())} WHERE _id = @id);";
        command.Parameters.AddWithValue("@id", id);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result) == 1;
    }

    private static void EnsureId<TItem>(TItem entity) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityIdGenerator.GenerateNewId();
        }
    }
```

### 1.6.2 `Insert`

```csharp
    public async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);
        var context = BuildWriteContext(RepositoryWriteOperation.Insert, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await ExecuteInsertAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, 1, new[] { entity.Id })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Insert<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entities.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext(RepositoryWriteOperation.Insert, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            foreach (var entity in list)
            {
                await ExecuteInsertAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, list.Count, list.Select(entity => entity.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }
```

### 1.6.3 `Save` and `Upsert`

```csharp
    public async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);
        var context = BuildWriteContext(RepositoryWriteOperation.Save, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            var existed = await ExistsByIdAsync<TItem>(lease.Connection, entity.Id, cancellationToken).ConfigureAwait(false);
            await ExecuteUpsertAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: !existed)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entities.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext(RepositoryWriteOperation.Save, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            foreach (var entity in list)
            {
                await ExecuteUpsertAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, list.Count, list.Select(entity => entity.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Save(entity, transaction, cancellationToken);

    public Task Upsert<TItem>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Save(entity, transaction, cancellationToken);
```

> The `Upsert` change-feed test expects `WasCreated` to be true on first call and false on second. `Save` already computes `wasCreated` from existence. Keep `Upsert` delegating to `Save`. The behavior dispatch operation enum will be `Save` rather than `Upsert`; Phase 6 adjusts `ChangeFeedBehavior` dispatch by implementing `Upsert` explicitly if the contract test requires the `Upsert` operation name. To be safe, implement `Upsert` explicitly with `RepositoryWriteOperation.Upsert` and the same body as `Save` but with the `Upsert` operation value. Do that now.

Replace the delegating `Upsert` with explicit implementations mirroring `Save` but using `RepositoryWriteOperation.Upsert` and `WriteOutcome.Merged`.

### 1.6.4 `Update`

```csharp
    public async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);
        var context = BuildWriteContext(RepositoryWriteOperation.Update, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            var affected = await ExecuteUpdateByIdAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, affected, new[] { entity.Id })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }
```

For `Update<TItem>(Expression<Func<TItem,bool>> conditionPredicate, TItem entity, ...)`:
- Build `combined = conditionPredicate.And(item => item.Id == entity.Id)` using `PredicateHelper.And` (extension in `GoLive.Saturn.Data.Abstractions`).
- Translate `combined` and run `UPDATE ... SET ... WHERE {fragment.Sql}`.
- `affected == 0` → `FailedToUpdateException`.

For `Update<TItem>(IEnumerable<TItem>)`: loop calling the single `Update` behavior once for the batch. For this phase, a simple loop over `ExecuteUpdateByIdAsync` inside one connection with one context and one after-dispatch is acceptable; if any affected == 0 throw `FailedToUpdateException`.

### 1.6.5 `Delete` (soft or hard) and `HardDelete`

Rules:
- If `!SupportsSoftDelete<TItem>()` → physical delete.
- If soft-deletable → `UPDATE` setting `IsDeleted=true`, `DeletedAt=@now`, `DeletedBy=''`, `_v=COALESCE(_v,0)+1`, `_deleted=1`, where the predicate is translated with `includeDeleted: true` (matches Mongo: delete does not filter already-deleted).

```csharp
    private async Task ExecuteSoftDeleteAsync<TItem>(SqliteConnection connection, SqlFragment predicate, DateTimeOffset now, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {Quote(GetCollectionNameForType<TItem>())}
            SET _doc = json_set(_doc,
                        '$.IsDeleted', json('true'),
                        '$.DeletedAt', @now,
                        '$.DeletedBy', ''),
                _v = COALESCE(_v,0) + 1,
                _deleted = 1
            WHERE {predicate.Sql};
            """;

        foreach (var parameter in predicate.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        command.Parameters.AddWithValue("@now", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
```

`Delete<TItem>(id)` builds predicate via `SqliteExpressionTranslator().TranslatePredicate` on `item => item.Id == id`. Use the translator on a built lambda: `Expression<Func<TItem, bool>> filter = item => item.Id == id;` then `new SqliteExpressionTranslator().Translate(filter)`. Simple and safe.

`Delete<TItem>(IEnumerable<string> IDs)` builds `normalized.Contains(item.Id)`.

`HardDelete` runs `DELETE FROM ... WHERE {fragment.Sql}`.

`Restore` requires `SupportsSoftDelete<TItem>()` else `NotSupportedException`. SQL:

```sql
UPDATE <table>
SET _doc = json_set(_doc, '$.IsDeleted', json('false'), '$.DeletedAt', json('null'), '$.DeletedBy', ''),
    _v = COALESCE(_v,0) + 1,
    _deleted = 0
WHERE <predicate>;
```

### 1.6.6 `CreateTransaction`

```csharp
    public Task<IDatabaseTransaction> CreateTransaction()
        => throw new NotImplementedException("Transactions are implemented in Phase 5.");
```

### 1.6.7 Phase-4/6 stubs

Implement the remaining interface members as stubs:

- `JsonUpdate<TItem>(...)` → `throw new NotSupportedException("JsonUpdate is implemented in Phase 4.");`
- `Patch<TItem>(...)` → `throw new NotSupportedException("Patch is implemented in Phase 4.");`
- `Increment<TItem>(...)` (4 overloads) → `throw new NotSupportedException("Increment is implemented in Phase 4.");`
- `DeleteCascade<TItem>(...)`, `HardDeleteCascade<TItem>(...)` → `throw new NotSupportedException("Cascade is implemented in Phase 6.");`

Note: `Patch` and `DeleteCascade`/`HardDeleteCascade` have default interface bodies. You must still override them (add `public` methods) so the throws happen instead of the default. `JsonUpdate` and `Increment` are interface members without defaults, so they are mandatory.

---

## Task 1.7 — Create test entities and `BasicTests`

### 1.7.1 Entities

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Entities\BasicEntity.cs`:

```csharp
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite.Tests.Entities;

public class BasicEntity : Entity
{
    public string Name { get; set; } = string.Empty;
}
```

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Entities\ParentScope.cs`:

```csharp
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite.Tests.Entities;

public class ParentScope : Entity
{
    public string Name { get; set; } = string.Empty;
}
```

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Entities\ChildEntity.cs`:

```csharp
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite.Tests.Entities;

public class ChildEntity : ScopedEntity<ParentScope>
{
    public string Name { get; set; } = string.Empty;
}
```

### 1.7.2 `BasicTests.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\BasicTests.cs`:

```csharp
using Saturn.Data.Testing.Shared;

namespace Saturn.Data.Sqlite.Tests;

public class BasicTests(DatabaseFixture fixture)
    : BasicRepositoryContractTests<DatabaseFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<DatabaseFixture>;
```

The shared contract test uses its own entities in `Saturn.Data.Testing.Shared.Entities` (`BasicEntity`, `ChildEntity`, `ParentScope`). That is fine; do not redefine them in this project with the same names in the global namespace.

> If you get a name clash between `Saturn.Data.Sqlite.Tests.Entities.BasicEntity` and `Saturn.Data.Testing.Shared.Entities.BasicEntity`, delete the local `Entities` folder created above — the tests project does not need its own entities for the shared suite. Keep `ParentScope`/`ChildEntity` only if later phases need them.

### 1.7.3 Extend `DatabaseFixture`

No change needed: `DatabaseFixture` already implements `IRepositoryTestFixture<UnitTestableSqliteRepository>`.

Add to `UnitTestableSqliteRepository` a raw document reader used by `ProviderSpecificTests`:

```csharp
    public async Task<string> ReadDocumentAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT _doc FROM {SqliteRepository.Quote(collection)} WHERE _id = @id;";
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }
```

---

## Task 1.8 — Create `ProviderSpecificTests.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\ProviderSpecificTests.cs`:

```csharp
using System.Text.Json;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class ProviderSpecificTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Document_Excludes_Transient_Members()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "doc-shape" };
        await fixture.Repository.Insert(entity);

        var document = await fixture.Repository.ReadDocumentAsync("BasicEntity", entity.Id);
        using var parsed = JsonDocument.Parse(document);
        var root = parsed.RootElement;

        Assert.False(root.TryGetProperty("Changes", out _));
        Assert.False(root.TryGetProperty("EnableChangeTracking", out _));
        Assert.False(root.TryGetProperty("_shortId", out _));
        Assert.True(root.TryGetProperty("Name", out _));
    }
}
```

---

## Task 1.9 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj"
```

Expected: `BasicTests` (3 tests) and `ProviderSpecificTests` (1 test) pass. `SmokeTests` still pass.

If `Save_And_Upsert_Logic` fails because count is not 1, verify the `ON CONFLICT(_id) DO UPDATE` SQL uses `_id` as the conflict target and that `Id` is normalized to a 24-char string.

---

## Do NOT

- Do not implement `Patch`, `Increment`, `JsonUpdate`, transactions, cascade, or change feed in this phase.
- Do not add comments to `.cs` files.
- Do not cache `SqliteExpressionTranslator` instances.
- Do not build predicate SQL by interpolating values; only `SqliteParameter`.
- Do not change the shared test project or the shared test entities.
