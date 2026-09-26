using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public sealed class SqliteDataUpdateDefinition<TItem> : IDataUpdateDefinition<TItem> where TItem : Entity
{
    public SqliteDataUpdateDefinition(Action<TItem> apply)
    {
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    internal Action<TItem> Apply { get; }
}
