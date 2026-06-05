using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.LiteDbX;

public sealed class LiteDbDataUpdateDefinition<TItem> : IDataUpdateDefinition<TItem> where TItem : Entity
{
    private readonly Action<TItem> apply;

    public LiteDbDataUpdateDefinition(Action<TItem> apply)
    {
        this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    public void Apply(TItem entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        apply(entity);
    }
}

