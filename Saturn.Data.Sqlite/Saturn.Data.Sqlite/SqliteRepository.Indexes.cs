using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IRepositoryIndexManager
{
    public async Task EnsureIndexes<TItem>(IEnumerable<IIndexDefinition<TItem>> definitions, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var manager = new Indexes.SqliteIndexManager(this, sqliteOptions);
        await manager.EnsureIndexesAsync(definitions, cancellationToken).ConfigureAwait(false);
    }
}
