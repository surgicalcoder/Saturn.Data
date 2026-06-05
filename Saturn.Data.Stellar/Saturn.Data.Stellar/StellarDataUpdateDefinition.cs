using GoLive.Saturn.Data.Abstractions;

namespace Saturn.Data.Stellar;

public sealed class StellarDataUpdateDefinition<TItem>(Action<TItem> apply) : IDataUpdateDefinition<TItem>
{
    public void Apply(TItem entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        apply(entity);
    }
}

