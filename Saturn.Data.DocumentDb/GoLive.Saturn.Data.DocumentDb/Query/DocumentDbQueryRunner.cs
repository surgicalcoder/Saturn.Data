using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb.Query;

public sealed class DocumentDbQueryRunner
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

    public async Task<List<TItem>> ExecuteAsync<TItem>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, object>> orderBy, bool descending,
        int? skip, int? take, CancellationToken cancellationToken) where TItem : Entity
    {
        try
        {
            var query = Build(predicate, orderBy, descending, skip, take);

            return (await query.ToList(cancellationToken).ConfigureAwait(false)).ToList();
        }
        catch (NotSupportedException exception)
        {
            return await FallbackAsync(predicate, orderBy, descending, skip, take, cancellationToken, exception).ConfigureAwait(false);
        }
    }

    public async Task<TItem> FirstOrDefaultAsync<TItem>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, object>> orderBy, bool descending,
        CancellationToken cancellationToken) where TItem : Entity
    {
        try
        {
            var query = Build(predicate, orderBy, descending, null, null);

            return await query.FirstOrDefault(cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException exception)
        {
            var list = await FallbackAsync(predicate, orderBy, descending, null, 1, cancellationToken, exception).ConfigureAwait(false);

            return list.Count == 0 ? null : list[0];
        }
    }

    public async Task<long> CountAsync<TItem>(Expression<Func<TItem, bool>> predicate, CancellationToken cancellationToken) where TItem : Entity
    {
        try
        {
            var query = store.Query<TItem>();

            return await (predicate is null ? query : query.Where(predicate)).Count(cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException exception)
        {
            var list = await FallbackAsync(predicate, null, false, null, null, cancellationToken, exception).ConfigureAwait(false);

            return list.Count;
        }
    }

    private IDocumentQuery<TItem> Build<TItem>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, object>> orderBy, bool descending,
        int? skip, int? take) where TItem : Entity
    {
        var query = store.Query<TItem>();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        if (orderBy is not null)
        {
            query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
        }

        if (take.HasValue)
        {
            query = query.Paginate(skip ?? 0, take.Value);
        }

        return query;
    }

    private async Task<List<TItem>> FallbackAsync<TItem>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, object>> orderBy, bool descending,
        int? skip, int? take, CancellationToken cancellationToken, NotSupportedException exception) where TItem : Entity
    {
        if (options.UnsupportedPredicateBehaviour == UnsupportedPredicateBehaviour.Throw)
        {
            throw new NotSupportedException(
                $"Shiny.DocumentDb backend '{capabilities.BackendName}' could not execute this query. Set DocumentDbRepositoryOptions.UnsupportedPredicateBehaviour to FallbackToClient to evaluate it in memory. Original: {exception.Message}",
                exception);
        }

        options.OnClientSideFallback?.Invoke($"Falling back to in-memory evaluation on '{capabilities.BackendName}': {exception.Message}");

        var all = (await store.Query<TItem>().ToList(cancellationToken).ConfigureAwait(false)).ToList();
        IEnumerable<TItem> filtered = predicate is null ? all : all.Where(predicate.Compile());

        if (orderBy is not null)
        {
            var selector = orderBy.Compile();
            filtered = descending ? filtered.OrderByDescending(selector) : filtered.OrderBy(selector);
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
}
