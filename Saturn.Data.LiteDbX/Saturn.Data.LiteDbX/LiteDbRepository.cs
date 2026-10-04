using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;
using LiteDbX.Engine;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository //: IRepository
{
    protected LiteDatabase database;
    protected LiteDBRepositoryOptions liteDbOptions;
    protected RepositoryOptions options;
    protected BsonMapper mapper;

    public LiteDbRepository(RepositoryOptions repositoryOptions, LiteDBRepositoryOptions liteDbRepositoryOptions)
    {
        liteDbOptions = liteDbRepositoryOptions;
        mapper = liteDbRepositoryOptions.Mapper;
        BsonMapper.Global = mapper;
        database = LiteDatabase.Open(liteDbOptions.ConnectionString, mapper);
        options = repositoryOptions;
    }

    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();

    public async Task Rebuild()
    {
        await database.Rebuild();
    }
    
    public void Dispose()
    {
        database?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async Task<IDatabaseTransaction> CreateTransaction()
    {
        return new LiteDbXTransactionWrapper(await database.BeginTransaction());
    }

    internal ILiteTransaction ResolveLiteTransaction(IDatabaseTransaction transaction)
    {
        return transaction is LiteDbXTransactionWrapper wrapper ? wrapper.Inner : null;
    }

    protected virtual string GetCollectionNameForType<T>()
    {
        return typeNameCache.GetOrAdd(typeof(T).FullName, s => options.GetCollectionName.Invoke(typeof(T)));
    }

    protected virtual ILiteCollection<T> GetCollection<T>() where T : Entity
    {
        return database.GetCollection<T>(GetCollectionNameForType<T>());
    }
    
    
    /// <summary>
    /// Applies sort orders to a LiteDB async query builder
    /// </summary>
    internal virtual ILiteQueryable<TItem> ApplySortOrders<TItem>(ILiteQueryable<TItem> query, IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        if (sortOrders == null)
        {
            return query;
        }

        var sortList = sortOrders.ToList();

        if (sortList.Count == 0)
        {
            return query;
        }

        var first = sortList[0];
        var orderedQuery = first.Direction == SortDirection.Ascending
            ? query.OrderBy(first.Field)
            : query.OrderByDescending(first.Field);

        for (var i = 1; i < sortList.Count; i++)
        {
            var sort = sortList[i];
            orderedQuery = sort.Direction == SortDirection.Ascending
                ? orderedQuery.ThenBy(sort.Field)
                : orderedQuery.ThenByDescending(sort.Field);
        }

        return orderedQuery;
    }

    /// <summary>
    /// Applies sort orders to a LINQ queryable
    /// </summary>
    internal virtual IQueryable<TItem> ApplySortOrders<TItem>(IQueryable<TItem> query, IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        if (sortOrders == null)
        {
            return query;
        }

        var sortList = sortOrders.ToList();

        if (sortList.Count == 0)
        {
            return query;
        }

        var first = sortList[0];
        IOrderedQueryable<TItem> orderedQuery = first.Direction == SortDirection.Ascending
            ? query.OrderBy(first.Field)
            : query.OrderByDescending(first.Field);

        for (var i = 1; i < sortList.Count; i++)
        {
            var sort = sortList[i];
            orderedQuery = sort.Direction == SortDirection.Ascending
                ? orderedQuery.ThenBy(sort.Field)
                : orderedQuery.ThenByDescending(sort.Field);
        }

        return orderedQuery;
    }

    /// <summary>
    /// Creates a LiteDB async query with predicate and continueFrom logic.
    /// Continuation tokens are always interpreted as canonical ObjectId-based entity IDs.
    /// Paging remains stable only when the caller sorts by <c>Id</c> ascending.
    /// </summary>
    internal virtual ILiteQueryable<TItem> BuildQuery<TItem>(ILiteCollection<TItem> collection, Expression<Func<TItem, bool>> predicate, string? continueFrom = null) where TItem : Entity
    {
        var query = collection.Query().Where(BsonMapper.Global.GetExpression(predicate));

        if (!TryNormalizeContinuationToken(continueFrom, out var continuationToken, out _))
        {
            return query;
        }

        return query.Where(Query.GT("_id", new BsonValue(continuationToken)));
    }

    internal virtual bool CanApplyContinuation<TItem>(IEnumerable<SortOrder<TItem>>? sortOrders) where TItem : Entity
    {
        if (sortOrders == null)
        {
            return true;
        }

        using var enumerator = sortOrders.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            return true;
        }

        var firstSort = enumerator.Current;

        return firstSort.Direction == SortDirection.Ascending &&
               TryGetMemberName(firstSort.Field.Body) == nameof(Entity.Id);
    }

    /// <summary>
    /// Builds a predicate expression with optional continuation token support.
    /// Invalid continuation tokens are ignored so callers can handle them gracefully.
    /// </summary>
    internal virtual BsonExpression BuildExpressionWithContinuation<TItem>(Expression<Func<TItem, bool>> predicate, string? continueFrom = null) where TItem : Entity
    {
        return BsonMapper.Global.GetExpression(predicate);
    }

    /// <summary>
    /// Applies continueFrom logic to a LINQ queryable.
    /// Continuation tokens are compared using their canonical ObjectId hex string representation.
    /// </summary>
    internal virtual IQueryable<TItem> ApplyContinueFrom<TItem>(IQueryable<TItem> query, string? continueFrom) where TItem : Entity
    {
        if (!TryNormalizeContinuationToken(continueFrom, out _, out var normalizedContinuationToken))
        {
            return query;
        }

        return query.Where(x => string.Compare(x.Id, normalizedContinuationToken) > 0);
    }

    /// <summary>
    /// Applies pagination to a LiteDB query.
    /// <paramref name="pageNumber"/> is 1-based when supplied together with <paramref name="pageSize"/>.
    /// </summary>
    internal virtual ILiteQueryableResult<TItem> ApplyPagination<TItem>(ILiteQueryable<TItem> query, int? pageSize, int? pageNumber = null) where TItem : Entity
    {
        if (pageSize is not > 0)
        {
            return query;
        }

        if (pageNumber is > 1)
        {
            return query.Offset((pageNumber.Value - 1) * pageSize.Value).Limit(pageSize.Value);
        }

        return query.Limit(pageSize.Value);
    }

    internal virtual ILiteQueryableResult<TItem> ApplyPagination<TItem>(ILiteQueryableResult<TItem> query, int? pageSize, int? pageNumber = null)
    {
        if (pageSize is not > 0)
        {
            return query;
        }

        if (pageNumber is > 1)
        {
            return query.Offset((pageNumber.Value - 1) * pageSize.Value).Limit(pageSize.Value);
        }

        return query.Limit(pageSize.Value);
    }

    /// <summary>
    /// Applies pagination to a queryable.
    /// <paramref name="pageNumber"/> is 1-based when supplied together with <paramref name="pageSize"/>.
    /// </summary>
    internal virtual IQueryable<TItem> ApplyPagination<TItem>(IQueryable<TItem> query, int? pageSize, int? pageNumber = null) where TItem : Entity
    {
        if (pageSize is not > 0)
        {
            return query;
        }

        if (pageNumber is > 1)
        {
            query = query.Skip((pageNumber.Value - 1) * pageSize.Value);
        }

        return query.Take(pageSize.Value);
    }

    /// <summary>
    /// Tries to normalize a continuation token into the canonical ObjectId hex string used by entity IDs.
    /// </summary>
    internal virtual bool TryNormalizeContinuationToken(string? continueFrom, out ObjectId continuationToken, out string? normalizedContinuationToken)
    {
        continuationToken = ObjectId.Empty;
        normalizedContinuationToken = null;

        if (string.IsNullOrWhiteSpace(continueFrom))
        {
            return false;
        }

        try
        {
            continuationToken = new ObjectId(continueFrom);
            normalizedContinuationToken = continuationToken.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal virtual List<BsonValue> NormalizeEntityIds(IEnumerable<string>? ids)
    {
        var normalizedIds = new List<BsonValue>();

        if (ids == null)
        {
            return normalizedIds;
        }

        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (TryNormalizeContinuationToken(id, out var objectId, out _))
            {
                normalizedIds.Add(new BsonValue(objectId));
            }
        }

        return normalizedIds;
    }

    internal static async IAsyncEnumerable<TItem> EmptyAsyncEnumerable<TItem>()
    {
        yield break;
    }

    internal static bool SupportsSoftDelete<TItem>() where TItem : Entity
    {
        return typeof(ISoftDeletable).IsAssignableFrom(typeof(TItem));
    }

    internal static Expression<Func<TItem, bool>> ApplySoftDeleteFilter<TItem>(Expression<Func<TItem, bool>> predicate, bool includeDeleted) where TItem : Entity
    {
        if (includeDeleted || !SupportsSoftDelete<TItem>())
        {
            return predicate;
        }

        return predicate.And(BuildNotDeletedPredicate<TItem>());
    }

    internal static Expression<Func<TItem, bool>> BuildNotDeletedPredicate<TItem>() where TItem : Entity
    {
        if (!SupportsSoftDelete<TItem>())
        {
            return _ => true;
        }

        var parameter = Expression.Parameter(typeof(TItem), "item");
        var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var isNotDeleted = Expression.NotEqual(isDeleted, Expression.Constant(true));

        return Expression.Lambda<Func<TItem, bool>>(isNotDeleted, parameter);
    }

    private static string? TryGetMemberName(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unaryExpression)
        {
            return TryGetMemberName(unaryExpression.Operand);
        }

        return expression is MemberExpression memberExpression ? memberExpression.Member.Name : null;
    }

    protected virtual RepositoryWriteContext<TItem> BuildWriteContext<TItem>(
        RepositoryWriteOperation operation,
        string? id = null,
        IEnumerable<string>? ids = null,
        IEnumerable<TItem>? items = null,
        Expression<Func<TItem, bool>>? filter = null,
        long? expectedVersion = null,
        string? jsonDocument = null,
        IDataUpdateDefinition<TItem>? updateDefinition = null,
        LambdaExpression? incrementField = null,
        object? incrementDelta = null,
        IDatabaseTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        return new RepositoryWriteContext<TItem>
        {
            Operation = operation,
            Id = id,
            Ids = ids?.ToList(),
            Items = items?.ToList(),
            Filter = filter,
            ExpectedVersion = expectedVersion,
            JsonDocument = jsonDocument,
            UpdateDefinition = updateDefinition,
            IncrementField = incrementField,
            IncrementDelta = incrementDelta,
            Transaction = transaction,
            CancellationToken = cancellationToken
        };
    }

    protected virtual ValueTask DispatchWriteBehaviorsAsync<TItem>(RepositoryWriteOperation operation, RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => BehaviorDispatcher.DispatchBeforeAsync(options.WriteBehaviors, operation, context);

    protected virtual async ValueTask ApplyAfterBehaviors<TItem>(RepositoryWriteOperation operation, RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
    {
        await BehaviorDispatcher.DispatchAfterAsync(options.WriteBehaviors, operation, context, result);
    }

    protected virtual async ValueTask ApplyOnWriteFailed<TItem>(RepositoryWriteContext<TItem> context, Exception exception)
        where TItem : Entity
    {
        await BehaviorDispatcher.DispatchOnWriteFailedAsync(options.WriteBehaviors, context, exception);
    }

    protected virtual RepositoryWriteResult BuildWriteResult<TItem>(
        RepositoryWriteContext<TItem> context,
        WriteOutcome outcome,
        int affectedCount,
        IReadOnlyCollection<string>? entityIds = null,
        IReadOnlyCollection<string>? matchedIds = null,
        bool wasCreated = false,
        object? rawResult = null,
        bool partialFailure = false,
        int failedCount = 0,
        IReadOnlyCollection<string>? failedIds = null)
        where TItem : Entity
    {
        return new RepositoryWriteResult
        {
            Operation = context.Operation,
            Succeeded = true,
            PartialFailure = partialFailure,
            Outcome = outcome,
            AffectedCount = affectedCount,
            FailedCount = failedCount,
            EntityIds = entityIds ?? Array.Empty<string>(),
            FailedIds = failedIds ?? Array.Empty<string>(),
            MatchedIds = matchedIds ?? Array.Empty<string>(),
            WasCreated = wasCreated,
            RawResult = rawResult,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
    }

    protected bool HasWriteBehaviors => options.WriteBehaviors is { Count: > 0 };

    private async Task<IReadOnlyList<string>> MaterializeIdsAsync<TItem>(Expression<Func<TItem, bool>> filter, CancellationToken cancellationToken)
        where TItem : Entity
    {
        var items = await GetCollection<TItem>().Query().Where(BsonMapper.Global.GetExpression(filter))
            .ToEnumerable(cancellationToken).ToListAsync(cancellationToken);
        return items.Select(i => i.Id).ToList();
    }
}