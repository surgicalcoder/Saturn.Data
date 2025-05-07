using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Entities;

public partial class Ref<T>
{
    public async Task Fetch(IAsyncEnumerable<T> items)
    {
        // Using GetAsyncEnumerator directly to minimize allocations
        var enumerator = items.GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current.Id == _refId)
                {
                    Item = enumerator.Current;
                    break;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    public void Fetch(IQueryable<T> items)
    {
        Item = items.FirstOrDefault(f => f.Id == _refId);
    }
    
    public void Fetch(IList<T> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null && item.Id.Equals(_refId))
            {
                Item = item;
                return;
            }
        }
    
        Item = null;
    }

    public void Fetch(Func<string, T> expr)
    {
        if (expr == null)
            throw new ArgumentNullException(nameof(expr));
        
        try
        {
            // Convert Guid to string for the function call
            string id = _refId.ToString();
        
            // Direct invocation with null check
            Item = expr(id);
        }
        catch (Exception ex)
        {
            // Add proper exception handling based on your application needs
            // Consider wrapping in a custom exception with the original as InnerException
            throw new InvalidOperationException($"Failed to fetch item with ID {_refId}", ex);
        }
    }
}