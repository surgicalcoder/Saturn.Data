using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.ChangeTracking;

public static class RepositoryPatchExtensions
{
    public static async Task PatchChanges<TEntity>(this IRepository repository, string id, long? expectedVersion, EntityChangeSet changes,
        IChangeTrackingObserver? observer = null, IDatabaseTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TEntity : Entity
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(changes);

        try
        {
            await repository.Patch<TEntity>(id, expectedVersion, changes.ToUpdateDocument(), null, transaction, cancellationToken).ConfigureAwait(false);
        }
        catch (FailedToUpdateException)
        {
            observer?.OnPatchConflict(typeof(TEntity).Name, id, expectedVersion);
            throw;
        }
    }
}
