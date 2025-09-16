using System.Linq;
using System.Collections.Concurrent;
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
            MessagePackOptions = MessagePackSerializerOptions.Standard.WithResolver(
                CompositeResolver.Create(
                    CryptoStringResolver.Instance,
                    EntityIgnoreResolver.Instance,
                    StandardResolver.Instance
                ))
        };
        database = new FastDB(fastDbOptions);
    }
    
    protected virtual string GetCollectionNameForType<T>()
    {
        return typeNameCache.GetOrAdd(typeof(T).FullName, s => options.GetCollectionName.Invoke(typeof(T)));
    }
    
    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();
    
    private IQueryable<TItem> ApplySort<TItem>(IQueryable<TItem> query, IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        if (sortOrders != null && sortOrders.Any())
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending
                ? current.OrderBy(sortOrder.Field)
                : current.OrderByDescending(sortOrder.Field));
        }
        return query;
    }
    
    private IQueryable<TItem> ApplyContinueFrom<TItem>(IQueryable<TItem> query, string continueFrom) where TItem : Entity
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
    
    private IQueryable<TItem> ApplyPaging<TItem>(IQueryable<TItem> query, int? pageSize, int? pageNumber) where TItem : Entity
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
    
    protected static RepositoryOptions options { get; set; }
    
    public void Dispose()
    {
        database.DisposeAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await database.DisposeAsync();
    }
}

public class StellarRepositoryOptions
{
    public string BaseDirectory { get; set; }
    public string DatabaseName { get; set; }
}
