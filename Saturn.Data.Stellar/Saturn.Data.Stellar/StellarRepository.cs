using System.Linq;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MessagePack;
using MessagePack.Resolvers;
using Saturn.Data.Stellar.Resolvers;
using Stellar.Collections;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IAsyncDisposable
{
    protected FastDB database;
    public StellarRepository(RepositoryOptions repositoryOptions, StellarRepositoryOptions databaseOptions)
    {
        options = repositoryOptions ?? new RepositoryOptions();
        var fastDbOptions = new FastDBOptions
        {
            BaseDirectory = databaseOptions.BaseDirectory,
            BufferMode = BufferModeType.WriteParallelEnabled,
            DatabaseName = databaseOptions.DatabaseName,
            AddDuplicateKeyBehavior = DuplicateKeyBehaviorType.Upsert,
            BulkAddDuplicateKeyBehavior = DuplicateKeyBehaviorType.Upsert,
            IsCompressionEnabled = databaseOptions.IsCompressed,
            IsEncryptionEnabled = databaseOptions.IsEncrypted,
            EncryptionPassword = databaseOptions.EncryptionKey,
            MaxDegreeOfParallelism = databaseOptions.MaxDegreeOfParallelism,
            MessagePackOptions = MessagePackSerializerOptions.Standard.WithResolver(
                CompositeResolver.Create(
                    CryptoStringResolver.Instance,
                    EntityIgnoreResolver.Instance,
                    RefResolver.Instance,
                    WeakRefResolver.Instance,
                    ContractlessStandardResolver.Instance
                ))
        };
        database = new FastDB(fastDbOptions);
    }
    
    protected virtual string GetCollectionNameForType<T>()
    {
        return typeNameCache.GetOrAdd(typeof(T).FullName, s => options.GetCollectionName.Invoke(typeof(T)));
    }
    
    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();
    
    protected virtual IQueryable<TItem> ApplySort<TItem>(IQueryable<TItem> query, IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        var sortOrderList = sortOrders?.ToList();

        if (sortOrderList == null || sortOrderList.Count == 0)
        {
            return query;
        }

        var firstSort = sortOrderList[0];
        IOrderedQueryable<TItem> orderedQuery = firstSort.Direction == SortDirection.Ascending
            ? query.OrderBy(firstSort.Field)
            : query.OrderByDescending(firstSort.Field);

        foreach (var sortOrder in sortOrderList.Skip(1))
        {
            orderedQuery = sortOrder.Direction == SortDirection.Ascending
                ? orderedQuery.ThenBy(sortOrder.Field)
                : orderedQuery.ThenByDescending(sortOrder.Field);
        }

        return orderedQuery;
    }

    protected virtual bool SupportsSoftDelete<TItem>() where TItem : Entity
    {
        return typeof(ISoftDeletable).IsAssignableFrom(typeof(TItem));
    }

    protected virtual Expression<Func<TItem, bool>> BuildNotDeletedPredicate<TItem>() where TItem : Entity
    {
        var parameter = Expression.Parameter(typeof(TItem), "item");
        var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var body = Expression.Equal(isDeleted, Expression.Constant(false));

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    protected virtual Expression<Func<TItem, bool>> ApplySoftDeleteFilter<TItem>(Expression<Func<TItem, bool>> predicate, bool includeDeleted) where TItem : Entity
    {
        if (includeDeleted || !SupportsSoftDelete<TItem>())
        {
            return predicate;
        }

        return BuildNotDeletedPredicate<TItem>().And(predicate);
    }

    protected virtual IQueryable<TItem> ApplySoftDeleteFilter<TItem>(IQueryable<TItem> query, bool includeDeleted) where TItem : Entity
    {
        if (includeDeleted || !SupportsSoftDelete<TItem>())
        {
            return query;
        }

        return query.Where(BuildNotDeletedPredicate<TItem>());
    }
    
    protected virtual IQueryable<TItem> ApplyContinueFrom<TItem>(IQueryable<TItem> query, string continueFrom) where TItem : Entity
    {
        if (!string.IsNullOrEmpty(continueFrom))
        {
            // Instead of using ID-based filtering which doesn't work with custom sort orders,
            // we'll use a different approach: skip all items until we find the continueFrom entity,
            // then skip that entity too to avoid duplication
            var continueFromId = new EntityId(continueFrom);
            
            // Convert to list to work with the sorted order, find the continuation point,
            // then skip past it
            var sortedItems = query.ToList();
            var continueFromIndex = sortedItems.FindIndex(e => e.Id == continueFromId);
            
            if (continueFromIndex >= 0)
            {
                // Skip all items up to and including the continuation token
                var remainingItems = sortedItems.Skip(continueFromIndex + 1);
                return remainingItems.AsQueryable();
            }
        }
        return query;
    }
    
    protected virtual IQueryable<TItem> ApplyPaging<TItem>(IQueryable<TItem> query, int? pageSize, int? pageNumber) where TItem : Entity
    {
        if (pageNumber.HasValue && pageSize.HasValue)
        {
            var skip = (pageNumber.Value - 1) * pageSize.Value;
            query = query.Skip(skip).Take(pageSize.Value);
        }
        else if (pageSize.HasValue)
        {
            query = query.Take(pageSize.Value);
        }
        return query;
    }
    
    protected RepositoryOptions options { get; set; }
    
    public void Dispose()
    {
        database.DisposeAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await database.DisposeAsync();
    }
}