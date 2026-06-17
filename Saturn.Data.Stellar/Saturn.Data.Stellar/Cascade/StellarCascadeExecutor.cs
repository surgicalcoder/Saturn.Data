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
using StellarRepo = Saturn.Data.Stellar.StellarRepository;

namespace Saturn.Data.Stellar.Cascade;

public sealed class StellarCascadeExecutor : ICascadeExecutor
{
    private readonly StellarRepo repository;
    private readonly List<(Type ChildType, string ChildId, CascadeMode Mode)> compensationLog = new();

    public StellarCascadeExecutor(StellarRepo repository)
    {
        this.repository = repository;
    }

    public bool Supports(Type childType) => typeof(Entity).IsAssignableFrom(childType);

    public async Task<CascadeStepResult> ExecuteAsync(
        CascadeStep step,
        IDatabaseTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var queryMethod = typeof(StellarRepo).GetMethod("QueryAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(step.ChildType);
        var query = (IQueryable<Entity>)await (Task<object>)queryMethod.Invoke(repository, null)!;

        var ids = await MaterializeIdsAsync(query, step, cancellationToken);
        if (ids.Count == 0)
            return new CascadeStepResult(Array.Empty<string>(), Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());

        try
        {
            return step.Mode switch
            {
                CascadeMode.Archive => await ArchiveAsync(step.ChildType, step, ids, cancellationToken),
                CascadeMode.HardDelete => await HardDeleteAsync(step.ChildType, ids, cancellationToken),
                _ => await SoftDeleteAsync(step.ChildType, step, ids, cancellationToken),
            };
        }
        catch (Exception ex)
        {
            await CompensateAsync(cancellationToken);
            throw new CascadeException(
                $"Stellar cascade failed for child type {step.ChildType.Name}: {ex.Message}",
                new CascadeReport(),
                compensationLog.ToList(),
                ex);
        }
    }

    private static async Task<List<string>> MaterializeIdsAsync(
        IQueryable<Entity> query,
        CascadeStep step,
        CancellationToken ct)
    {
        var items = await Task.Run(() => query
            .Where(e => e.GetType().GetProperty("ScopeId")!.GetValue(e) as string == step.ParentId)
            .ToList(), ct);
        return items.Select(e => e.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
    }

    private async Task<CascadeStepResult> SoftDeleteAsync(
        Type childType,
        CascadeStep step,
        List<string> ids,
        CancellationToken ct)
    {
        var collection = await GetCollectionAsync(childType);
        IQueryable<Entity> q = AsEntityQueryable(collection);
        var items = await Task.Run(() => q.Where((Entity e) => ids.Contains(e.Id)).ToList(), ct);
        foreach (var item in items)
        {
            if (item is not ISoftDeletable sd) continue;
            sd.IsDeleted = true;
            sd.DeletedAt = DateTime.UtcNow;
            sd.DeletedBy = step.ParentId;
            await UpdateAsync(collection, item);
            compensationLog.Add((step.ChildType, item.Id, CascadeMode.SoftDelete));
        }
        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }

    private async Task<CascadeStepResult> ArchiveAsync(
        Type childType,
        CascadeStep step,
        List<string> ids,
        CancellationToken ct)
    {
        var collection = await GetCollectionAsync(childType);
        IQueryable<Entity> q = AsEntityQueryable(collection);
        var items = await Task.Run(() => q.Where((Entity e) => ids.Contains(e.Id)).ToList(), ct);
        foreach (var item in items)
        {
            if (item is not IArchivable ar) continue;
            ar.IsArchived = true;
            ar.ArchivedAt = DateTime.UtcNow;
            ar.ArchivedBy = step.ParentId;
            await UpdateAsync(collection, item);
            compensationLog.Add((step.ChildType, item.Id, CascadeMode.Archive));
        }
        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }

    private async Task<CascadeStepResult> HardDeleteAsync(
        Type childType,
        List<string> ids,
        CancellationToken ct)
    {
        var collection = await GetCollectionAsync(childType);
        var entityIds = ids.Select(id => new EntityId(id)).ToList();
        await RemoveBulkAsync(collection, entityIds);
        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }

    private async Task<dynamic> GetCollectionAsync(Type childType)
    {
        var method = typeof(StellarRepo).GetMethod("CollectionForAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(childType);
        return await (Task<dynamic>)method.Invoke(repository, null)!;
    }

    private static IQueryable<Entity> AsEntityQueryable(object collection)
    {
        dynamic d = collection;
        return ((IQueryable)d.AsQueryable()).Cast<Entity>();
    }

    private static Task UpdateAsync(dynamic collection, Entity item) => collection.UpdateAsync(item.Id, item);
    private static Task RemoveBulkAsync(dynamic collection, IList<EntityId> ids) => collection.RemoveBulkAsync(ids);

    private async Task CompensateAsync(CancellationToken ct)
    {
        for (int i = compensationLog.Count - 1; i >= 0; i--)
        {
            var (childType, childId, mode) = compensationLog[i];
            var collection = await GetCollectionAsync(childType);
            IQueryable<Entity> q = AsEntityQueryable(collection);
            var items = await Task.Run(() => q.Where((Entity e) => e.Id == childId).ToList(), ct);
            var item = items.FirstOrDefault();
            if (item is null) continue;
            if (mode == CascadeMode.SoftDelete && item is ISoftDeletable sd)
            {
                sd.IsDeleted = false;
                sd.DeletedAt = null;
                sd.DeletedBy = string.Empty;
                await UpdateAsync(collection, item);
            }
            else if (mode == CascadeMode.Archive && item is IArchivable ar)
            {
                ar.IsArchived = false;
                ar.ArchivedAt = null;
                ar.ArchivedBy = string.Empty;
                await UpdateAsync(collection, item);
            }
        }
    }
}
