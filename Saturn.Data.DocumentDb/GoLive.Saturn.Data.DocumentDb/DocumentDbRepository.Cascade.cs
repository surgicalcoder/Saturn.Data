using System.Linq.Expressions;
using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;
using Saturn.Data.DocumentDb.Cascade;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository
{
    public async Task<CascadeReport> DeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var report = await RunCascadeAsync(typeof(TItem), id, forceMode: null, transaction, cancellationToken).ConfigureAwait(false);
        await Delete<TItem>(id, transaction, cancellationToken).ConfigureAwait(false);
        return report;
    }

    public async Task<CascadeReport> HardDeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var report = await RunCascadeAsync(typeof(TItem), id, forceMode: CascadeMode.HardDelete, transaction, cancellationToken).ConfigureAwait(false);
        await HardDelete<TItem>(id, transaction, cancellationToken).ConfigureAwait(false);
        return report;
    }

    internal async Task<List<string>> MaterializeCascadeChildrenAsync(Type childType, string parentId, IDatabaseTransaction transaction,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var method = typeof(DocumentDbRepository)
            .GetMethod(nameof(MaterializeCascadeChildrenCoreAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(childType);

        var task = (Task<List<string>>)method.Invoke(this, new object[] { parentId, cancellationToken })!;

        return await task.ConfigureAwait(false);
    }

    internal async Task ApplyCascadeAsync(Type childType, IReadOnlyList<string> ids, CascadeMode mode, string parentId,
        IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var method = typeof(DocumentDbRepository)
            .GetMethod(nameof(ApplyCascadeCoreAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(childType);

        var task = (Task)method.Invoke(this, new object[] { ids, mode, parentId, transaction, cancellationToken })!;

        await task.ConfigureAwait(false);
    }

    private async Task<List<string>> MaterializeCascadeChildrenCoreAsync<TChild>(string parentId, CancellationToken cancellationToken) where TChild : Entity
    {
        var predicate = BuildCascadePredicate<TChild>(parentId);
        var matches = await QueryRunner.ExecuteAsync(predicate, null, false, null, null, cancellationToken).ConfigureAwait(false);

        return matches.Select(entity => entity.Id).ToList();
    }

    private async Task ApplyCascadeCoreAsync<TChild>(IReadOnlyList<string> ids, CascadeMode mode, string parentId,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TChild : Entity
    {
        if (mode == CascadeMode.HardDelete)
        {
            await RemoveWithTransactionAsync<TChild>(transaction, ids, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var id in ids)
        {
            var entity = await store.Get<TChild>(id).ConfigureAwait(false);

            if (entity is null)
            {
                continue;
            }

            if (mode == CascadeMode.Archive && entity is IArchivable archivable)
            {
                archivable.IsArchived = true;
                archivable.ArchivedAt = DateTime.UtcNow;
                archivable.ArchivedBy = parentId;
            }
            else if (entity is ISoftDeletable deletable)
            {
                deletable.IsDeleted = true;
                deletable.DeletedAt = DateTime.UtcNow;
                deletable.DeletedBy = parentId;
            }

            entity.Version = (entity.Version ?? 0) + 1;
            await UpdateWithTransactionAsync(transaction, entity, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Expression<Func<TChild, bool>> BuildCascadePredicate<TChild>(string parentId) where TChild : Entity
    {
        var parameter = Expression.Parameter(typeof(TChild), "item");

        if (typeof(TChild).GetProperty("ScopeId") is not null)
        {
            var scopeBody = Expression.Equal(Expression.Property(parameter, "ScopeId"), Expression.Constant(parentId));

            return Expression.Lambda<Func<TChild, bool>>(scopeBody, parameter);
        }

        if (typeof(TChild).GetProperty("Scopes") is not null)
        {
            var scopes = Expression.Property(parameter, "Scopes");
            var contains = Expression.Call(scopes, typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!, Expression.Constant(parentId));

            return Expression.Lambda<Func<TChild, bool>>(contains, parameter);
        }

        var idBody = Expression.Equal(Expression.Property(parameter, nameof(Entity.Id)), Expression.Constant(parentId));

        return Expression.Lambda<Func<TChild, bool>>(idBody, parameter);
    }

    private async Task<CascadeReport> RunCascadeAsync(Type parentType, string parentId, CascadeMode? forceMode,
        IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        var executor = new DocumentDbCascadeExecutor(this, forceMode);
        var deleted = new Dictionary<Type, int>();
        var archived = new Dictionary<Type, int>();
        var sharedDeletions = new List<(Type, string, IReadOnlyList<string>)>();
        var skippedShared = new List<(Type, string, IReadOnlyList<string>)>();
        var skippedCycles = new List<(Type, string)>();
        var visited = new HashSet<(Type, string)>();
        var work = new Queue<(Type Type, string Id, CascadeDepth Depth)>();

        work.Enqueue((parentType, parentId, CascadeDepth.Transitive));

        while (work.Count > 0)
        {
            var (currentType, currentId, currentDepth) = work.Dequeue();

            if (!visited.Add((currentType, currentId)))
            {
                skippedCycles.Add((currentType, currentId));
                continue;
            }

            foreach (var relation in CascadeRelationResolver.RelationsForParent(currentType))
            {
                var effectiveMode = forceMode ?? relation.Mode;

                if (effectiveMode == CascadeMode.None)
                {
                    continue;
                }

                var step = new CascadeStep(relation.ChildType, currentId, Array.Empty<string>(), effectiveMode, relation.Depth, relation.SharedScope);
                var result = await executor.ExecuteAsync(step, transaction, cancellationToken).ConfigureAwait(false);

                if (effectiveMode == CascadeMode.Archive)
                {
                    archived[relation.ChildType] = archived.GetValueOrDefault(relation.ChildType) + result.AffectedIds.Count;
                }
                else
                {
                    deleted[relation.ChildType] = deleted.GetValueOrDefault(relation.ChildType) + result.AffectedIds.Count;
                }

                sharedDeletions.AddRange(result.SharedScopeDeletions.Select(item => (relation.ChildType, item.ChildId, item.OtherParentIds)));
                skippedShared.AddRange(result.SkippedSharedChildren.Select(item => (relation.ChildType, item.ChildId, item.OtherParentIds)));

                if (currentDepth == CascadeDepth.Transitive && relation.Depth == CascadeDepth.Transitive)
                {
                    foreach (var childId in result.AffectedIds)
                    {
                        work.Enqueue((relation.ChildType, childId, CascadeDepth.Transitive));
                    }
                }
            }
        }

        return new CascadeReport
        {
            DeletedPerType = deleted,
            ArchivedPerType = archived,
            SharedScopeDeletions = sharedDeletions,
            SkippedSharedChildren = skippedShared,
            SkippedCycles = skippedCycles,
            Warnings = Array.Empty<string>(),
            Aborted = false
        };
    }
}
