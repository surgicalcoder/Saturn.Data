using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions.Cascade;

public sealed class CascadeWriteBehavior : IRepositoryWriteBehavior
{
    private readonly CascadeEngine engine;

    public CascadeWriteBehavior(CascadeEngine engine)
    {
        this.engine = engine;
    }

    public async ValueTask BeforeDelete<TItem>(RepositoryWriteContext<TItem> context) where TItem : Entity
    {
        if (context.Suppress) return;
        var ids = ResolveIds(context);
        foreach (var id in ids)
        {
            await engine.DeleteAsync(typeof(TItem), id, context.Transaction, context.CancellationToken);
        }
    }

    public async ValueTask BeforeHardDelete<TItem>(RepositoryWriteContext<TItem> context) where TItem : Entity
    {
        if (context.Suppress) return;
        var ids = ResolveIds(context);
        foreach (var id in ids)
        {
            await engine.DeleteAsync(typeof(TItem), id, context.Transaction, context.CancellationToken);
        }
    }

    private static System.Collections.Generic.IReadOnlyCollection<string>? ResolveIds<TItem>(RepositoryWriteContext<TItem> context) where TItem : Entity
    {
        if (context.Ids is { Count: > 0 }) return context.Ids;
        if (!string.IsNullOrEmpty(context.Id)) return new[] { context.Id };
        return null;
    }
}
