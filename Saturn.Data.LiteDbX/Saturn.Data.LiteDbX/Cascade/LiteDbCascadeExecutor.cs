using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;
using LiteDbX;
using LiteRepo = Saturn.Data.LiteDbX.LiteDbRepository;

namespace Saturn.Data.LiteDbX.Cascade;

public sealed class LiteDbCascadeExecutor : ICascadeExecutor
{
    private readonly LiteRepo repository;

    public LiteDbCascadeExecutor(LiteRepo repository)
    {
        this.repository = repository;
    }

    public bool Supports(Type childType) => typeof(Entity).IsAssignableFrom(childType);

    public async Task<CascadeStepResult> ExecuteAsync(
        CascadeStep step,
        IDatabaseTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var collection = (ILiteCollection<Entity>)typeof(LiteRepo)
            .GetMethod("CollectionFor", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(step.ChildType)
            .Invoke(repository, null)!;

        var ids = await MaterializeIdsAsync(collection, step, cancellationToken);
        if (ids.Count == 0)
            return new CascadeStepResult(Array.Empty<string>(), Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());

        return step.Mode switch
        {
            CascadeMode.Archive => await ArchiveAsync(collection, step, ids, cancellationToken),
            CascadeMode.HardDelete => await HardDeleteAsync(collection, ids, cancellationToken),
            _ => await SoftDeleteAsync(collection, step, ids, cancellationToken),
        };
    }

    private static async Task<List<string>> MaterializeIdsAsync(
        ILiteCollection<Entity> collection,
        CascadeStep step,
        CancellationToken ct)
    {
        var query = Query.EQ("ScopeId", step.ParentId);
        var result = collection.Find(query);
        return await result.Select(e => e.Id).Where(id => !string.IsNullOrEmpty(id)).ToListAsync(ct);
    }

    private async Task<CascadeStepResult> SoftDeleteAsync(
        ILiteCollection<Entity> collection,
        CascadeStep step,
        List<string> ids,
        CancellationToken ct)
    {
        var items = await collection.Query().Where(Query.In("_id", ids.Select(i => new BsonValue(i)).ToArray())).ToEnumerable(ct).ToListAsync(ct);
        foreach (var item in items)
        {
            if (item is not ISoftDeletable sd) continue;
            sd.IsDeleted = true;
            sd.DeletedAt = DateTime.UtcNow;
            sd.DeletedBy = step.ParentId;
            await Task.Run(() => collection.Update(item), ct);
        }
        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }

    private async Task<CascadeStepResult> ArchiveAsync(
        ILiteCollection<Entity> collection,
        CascadeStep step,
        List<string> ids,
        CancellationToken ct)
    {
        var items = await collection.Query().Where(Query.In("_id", ids.Select(i => new BsonValue(i)).ToArray())).ToEnumerable(ct).ToListAsync(ct);
        foreach (var item in items)
        {
            if (item is not IArchivable ar) continue;
            ar.IsArchived = true;
            ar.ArchivedAt = DateTime.UtcNow;
            ar.ArchivedBy = step.ParentId;
            await Task.Run(() => collection.Update(item), ct);
        }
        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }

    private static async Task<CascadeStepResult> HardDeleteAsync(
        ILiteCollection<Entity> collection,
        List<string> ids,
        CancellationToken ct)
    {
        await Task.Run(() => collection.DeleteMany(e => ids.Contains(e.Id)), ct);
        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }
}
