using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IRepositoryIndexManager
{
    Task EnsureIndexes<TItem>(IEnumerable<IIndexDefinition<TItem>> definitions, CancellationToken cancellationToken = default)
        where TItem : Entity;
}

