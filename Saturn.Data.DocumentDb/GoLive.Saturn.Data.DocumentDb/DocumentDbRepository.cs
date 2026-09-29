using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.DocumentDb.Serialization;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : IDisposable
{
    private readonly RepositoryOptions options;
    private readonly DocumentDbRepositoryOptions documentDbOptions;
    private readonly IDocumentStore store;
    private readonly IDatabaseProvider databaseProvider;
    private readonly EntityJsonSerializer serializer;
    private readonly bool ownsStore;
    private readonly SemaphoreSlim initializationLock = new(1, 1);

    private DocumentDbCapabilities capabilities;
    private Query.DocumentDbQueryRunner queryRunner;
    private bool initialized;
    private bool disposed;

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
        else if (documentDbOptions.DatabaseProvider is not null)
        {
            var storeOptions = new DocumentStoreOptions
            {
                DatabaseProvider = documentDbOptions.DatabaseProvider,
                TableName = documentDbOptions.DefaultTableName,
                JsonSerializerOptions = serializer.JsonOptions
            };

            documentDbOptions.ConfigureStore?.Invoke(storeOptions);

            if (!documentDbOptions.UseReflectionFallback)
            {
                storeOptions.UseReflectionFallback = false;
            }

            databaseProvider = storeOptions.DatabaseProvider;
            store = new DocumentStore(storeOptions);
            ownsStore = true;
        }
        else
        {
            throw new InvalidOperationException("DocumentDbRepositoryOptions requires either Store or DatabaseProvider.");
        }
    }

    internal RepositoryOptions Options => options;

    internal DocumentDbRepositoryOptions DocumentDbOptions => documentDbOptions;

    internal IDocumentStore Store => store;

    internal DocumentDbCapabilities Capabilities => capabilities;

    protected EntityJsonSerializer Serializer => serializer;

    protected Query.DocumentDbQueryRunner QueryRunner => queryRunner;

    protected bool HasWriteBehaviors => options.WriteBehaviors is { Count: > 0 };

    protected string GetCollectionNameForType<TItem>() where TItem : Entity => options.GetCollectionName(typeof(TItem));

    internal static bool SupportsSoftDelete<TItem>() where TItem : Entity => typeof(ISoftDeletable).IsAssignableFrom(typeof(TItem));

    internal static bool SupportsArchivable<TItem>() where TItem : Entity => typeof(IArchivable).IsAssignableFrom(typeof(TItem));

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

    protected IDocumentQuery<TItem> QueryFor<TItem>() where TItem : Entity => store.Query<TItem>();

    protected Expression<Func<TItem, bool>> NotDeletedPredicate<TItem>() where TItem : Entity
    {
        var parameter = Expression.Parameter(typeof(TItem), "item");
        var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var body = Expression.Equal(property, Expression.Constant(false));

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    protected Expression<Func<TItem, bool>> WithSoftDeleteFilter<TItem>(Expression<Func<TItem, bool>> predicate, bool includeDeleted) where TItem : Entity
    {
        if (includeDeleted || !SupportsSoftDelete<TItem>())
        {
            return predicate;
        }

        var filter = NotDeletedPredicate<TItem>();

        return predicate is null ? filter : Query.PredicateComposer.AndAlso(predicate, filter);
    }

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

        if (normalized is null)
        {
            return false;
        }

        var existing = await store.Get<TItem>(normalized).ConfigureAwait(false);

        return existing is not null;
    }

    protected async Task<List<string>> MatchingIdsAsync<TItem>(Expression<Func<TItem, bool>> predicate, CancellationToken cancellationToken) where TItem : Entity
    {
        var matches = await store.Query<TItem>().Where(predicate).ToList().ConfigureAwait(false);

        return matches.Select(entity => entity.Id).ToList();
    }

    protected async Task InsertWithTransactionAsync<TItem>(IDatabaseTransaction transaction, TItem entity, CancellationToken cancellationToken) where TItem : class
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            documentDbTransaction.Session.Add(entity);
            return;
        }

        await store.Insert(entity).ConfigureAwait(false);
    }

    protected async Task InsertManyWithTransactionAsync<TItem>(IDatabaseTransaction transaction, IReadOnlyList<TItem> entities, CancellationToken cancellationToken) where TItem : class
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            documentDbTransaction.Session.AddRange(entities);
            return;
        }

        await store.BatchInsert(entities).ConfigureAwait(false);
    }

    protected async Task UpsertWithTransactionAsync<TItem>(IDatabaseTransaction transaction, TItem entity, CancellationToken cancellationToken) where TItem : class
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            documentDbTransaction.Session.Upsert(entity);
            return;
        }

        await store.Upsert(entity).ConfigureAwait(false);
    }

    protected async Task UpsertManyWithTransactionAsync<TItem>(IDatabaseTransaction transaction, IReadOnlyList<TItem> entities, CancellationToken cancellationToken) where TItem : class
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            foreach (var entity in entities)
            {
                documentDbTransaction.Session.Upsert(entity);
            }

            return;
        }

        await store.BatchUpsert(entities).ConfigureAwait(false);
    }

    protected async Task UpdateWithTransactionAsync<TItem>(IDatabaseTransaction transaction, TItem entity, CancellationToken cancellationToken) where TItem : class
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            documentDbTransaction.Session.Update(entity);
            return;
        }

        await store.Update(entity).ConfigureAwait(false);
    }

    protected async Task RemoveWithTransactionAsync<TItem>(IDatabaseTransaction transaction, IEnumerable<string> ids, CancellationToken cancellationToken) where TItem : class
    {
        var idList = ids.ToList();

        if (idList.Count == 0)
        {
            return;
        }

        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            foreach (var id in idList)
            {
                documentDbTransaction.Session.Remove<TItem>(id);
            }

            return;
        }

        await store.BatchRemove<TItem>(idList).ConfigureAwait(false);
    }

    internal Task InsertCounterAsync(IDatabaseTransaction transaction, ChangeFeed.ChangeFeedCounterRow row, CancellationToken cancellationToken)
        => InsertWithTransactionAsync(transaction, row, cancellationToken);

    internal Task UpdateCounterAsync(IDatabaseTransaction transaction, ChangeFeed.ChangeFeedCounterRow row, CancellationToken cancellationToken)
        => UpdateWithTransactionAsync(transaction, row, cancellationToken);

    internal Task InsertOutboxRowAsync(IDatabaseTransaction transaction, ChangeFeed.ChangeFeedOutboxRow row, CancellationToken cancellationToken)
        => InsertWithTransactionAsync(transaction, row, cancellationToken);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (initialized)
            {
                return;
            }

            var backendName = documentDbOptions.BackendName
                              ?? databaseProvider?.GetType().Name
                              ?? store.GetType().Name;

            capabilities = DocumentDbCapabilities.Probe(store, databaseProvider, backendName);
            queryRunner = new Query.DocumentDbQueryRunner(store, documentDbOptions, capabilities);
            initialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    protected RepositoryWriteContext<TItem> BuildWriteContext<TItem>(
        RepositoryWriteOperation operation,
        TItem item = default!,
        IEnumerable<TItem> items = default!,
        string id = null!,
        IEnumerable<string> ids = null!,
        Expression<Func<TItem, bool>> filter = default!,
        long? expectedVersion = null,
        string jsonDocument = null!,
        IDataUpdateDefinition<TItem> updateDefinition = default!,
        LambdaExpression incrementField = null!,
        object incrementDelta = null!,
        IDatabaseTransaction transaction = null!,
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
            IncrementField = incrementField,
            IncrementDelta = incrementDelta,
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
        IEnumerable<string>? entityIds = null,
        bool wasCreated = false,
        IReadOnlyCollection<string>? matchedIds = null,
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
            EntityIds = entityIds is null ? (IReadOnlyCollection<string>)Array.Empty<string>() : entityIds.ToList(),
            MatchedIds = matchedIds ?? Array.Empty<string>(),
            WasCreated = wasCreated,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        initializationLock.Dispose();

        if (ownsStore && store is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
