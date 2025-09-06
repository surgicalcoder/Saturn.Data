using System.Linq;
using System.Collections.Concurrent;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Stellar.Collections;

namespace Saturn.Data.Stellar;

public partial class StellarRepository
{
    protected FastDB database;
    public StellarRepository(RepositoryOptions options, StellarRepositoryOptions databaseOptions)
    {
        var fastDbOptions = new FastDBOptions
        {
            BaseDirectory = options.ConnectionString,
            BufferMode = BufferModeType.WriteParallelEnabled,
            DatabaseName = databaseOptions.DatabaseName
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
    
    protected static RepositoryOptions options { get; set; }
    
    public void Dispose()
    {
    }
}

public class StellarRepositoryOptions
{
    public string DatabaseName { get; set; }
}
