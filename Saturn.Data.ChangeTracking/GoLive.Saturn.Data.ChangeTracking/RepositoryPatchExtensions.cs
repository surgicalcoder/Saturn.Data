using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.ChangeTracking;

public static class RepositoryPatchExtensions
{
    public static Task PatchChanges<TEntity>(this IRepository repository, string id, long? expectedVersion, EntityChangeSet changes,
        IDatabaseTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TEntity : Entity
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(changes);

        return repository.Patch<TEntity>(id, expectedVersion, changes.ToUpdateDocument(), null, transaction, cancellationToken);
    }
}
