using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace GoLive.Saturn.Data.Abstractions.Cascade;

public sealed class CascadeEngine
{
    private readonly IReadOnlyList<ICascadeExecutor> executors;

    public CascadeEngine(IEnumerable<ICascadeExecutor> executors)
    {
        this.executors = executors.ToList();
    }

    public async Task<CascadeReport> DeleteAsync(
        Type parentType,
        string parentId,
        IDatabaseTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (parentType is null) throw new ArgumentNullException(nameof(parentType));
        if (string.IsNullOrEmpty(parentId)) throw new ArgumentException("parentId required", nameof(parentId));

        var visited = new HashSet<(Type, string)>();
        var deleted = new Dictionary<Type, int>();
        var archived = new Dictionary<Type, int>();
        var sharedDeletions = new List<(Type, string, IReadOnlyList<string>)>();
        var skippedShared = new List<(Type, string, IReadOnlyList<string>)>();
        var skippedCycles = new List<(Type, string)>();

        var work = new Queue<(Type ParentType, string ParentId, CascadeDepth Depth)>();
        work.Enqueue((parentType, parentId, CascadeDepth.Transitive));

        while (work.Count > 0)
        {
            var (currentType, currentId, currentDepth) = work.Dequeue();
            if (!visited.Add((currentType, currentId)))
            {
                skippedCycles.Add((currentType, currentId));
                continue;
            }

            var snapshot = GetSnapshotFor(currentType);
            foreach (var rel in snapshot)
            {
                if (rel.Mode == CascadeMode.None) continue;

                var executor = executors.FirstOrDefault(e => e.Supports(rel.ChildType));
                if (executor is null) continue;

                var step = new CascadeStep(
                    ChildType: rel.ChildType,
                    ParentId: currentId,
                    ChildIds: Array.Empty<string>(),
                    Mode: rel.Mode,
                    Depth: rel.Depth,
                    SharedScope: rel.SharedScope);

                var result = await executor.ExecuteAsync(step, transaction, cancellationToken);

                if (rel.Mode == CascadeMode.Archive)
                    archived[rel.ChildType] = archived.GetValueOrDefault(rel.ChildType) + result.AffectedIds.Count;
                else
                    deleted[rel.ChildType] = deleted.GetValueOrDefault(rel.ChildType) + result.AffectedIds.Count;

                sharedDeletions.AddRange(result.SharedScopeDeletions.Select(x => (rel.ChildType, x.ChildId, x.OtherParentIds)));
                skippedShared.AddRange(result.SkippedSharedChildren.Select(x => (rel.ChildType, x.ChildId, x.OtherParentIds)));

                if (currentDepth == CascadeDepth.Transitive && rel.Depth == CascadeDepth.Transitive)
                {
                    foreach (var childId in result.AffectedIds)
                    {
                        work.Enqueue((rel.ChildType, childId, CascadeDepth.Transitive));
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
            Aborted = false,
        };
    }

    private static IReadOnlyList<CascadeRelationSnapshot> GetSnapshotFor(Type parentType)
    {
        var property = parentType.GetProperty("__Cascade", BindingFlags.Public | BindingFlags.Static);
        if (property is null) return Array.Empty<CascadeRelationSnapshot>();
        var snapshotProp = property.PropertyType.GetProperty("For", BindingFlags.Public | BindingFlags.Static);
        if (snapshotProp is null) return Array.Empty<CascadeRelationSnapshot>();
        var value = snapshotProp.GetValue(null);
        return value as IReadOnlyList<CascadeRelationSnapshot> ?? Array.Empty<CascadeRelationSnapshot>();
    }
}
