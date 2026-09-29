using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public sealed class DocumentDbDataUpdateDefinition<TItem> : IDataUpdateDefinition<TItem> where TItem : Entity
{
    public DocumentDbDataUpdateDefinition(Action<TItem> apply)
    {
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    internal Action<TItem> Apply { get; }
}
