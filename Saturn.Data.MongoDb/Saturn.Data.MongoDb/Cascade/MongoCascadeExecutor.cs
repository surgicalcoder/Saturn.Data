using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTx = Saturn.Data.MongoDb.MongoDbTransactionWrapper;
using MongoRepo = Saturn.Data.MongoDb.MongoDbRepository;

namespace GoLive.Saturn.Data.MongoDb.Cascade;

public sealed class MongoCascadeExecutor : ICascadeExecutor
{
    private readonly MongoRepo repository;

    public MongoCascadeExecutor(MongoRepo repository)
    {
        this.repository = repository;
    }

    public bool Supports(Type childType) => typeof(Entity).IsAssignableFrom(childType);

    public async Task<CascadeStepResult> ExecuteAsync(
        CascadeStep step,
        IDatabaseTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var getCollection = typeof(MongoRepo)
            .GetMethod("GetCollection", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!
            .MakeGenericMethod(step.ChildType);
        var collection = (IMongoCollection<Entity>)getCollection.Invoke(repository, null)!;

        var session = transaction is MongoTx wrapper
            ? (IClientSessionHandle?)wrapper.Session
            : null;

        var sharedDeletions = new List<(string, IReadOnlyList<string>)>();
        var skippedShared = new List<(string, IReadOnlyList<string>)>();

        var candidateIds = await MaterializeIdsAsync(collection, step, session, cancellationToken);
        if (candidateIds.Count == 0)
            return new CascadeStepResult(Array.Empty<string>(), sharedDeletions, skippedShared);

        if (step.Mode == CascadeMode.Archive)
            return await ArchiveAsync(collection, step, candidateIds, session, cancellationToken);

        if (step.Mode == CascadeMode.HardDelete)
            return await HardDeleteAsync(collection, step, candidateIds, session, cancellationToken);

        return await SoftDeleteAsync(collection, step, candidateIds, session, cancellationToken);
    }

    private static async Task<List<string>> MaterializeIdsAsync(
        IMongoCollection<Entity> collection,
        CascadeStep step,
        IClientSessionHandle? session,
        CancellationToken ct)
    {
        var filter = BuildParentScopeFilter(step.ChildType, step.ParentId);
        if (filter is null) return new List<string>();
        var find = session is null
            ? collection.Find(filter)
            : collection.Find(session, filter);
        var projection = Builders<Entity>.Projection.Include("_id");
        var docs = await find.Project<BsonDocument>(projection).ToListAsync(ct);
        return docs
            .Select(d => d.GetValue("_id", BsonNull.Value))
            .Where(v => v != null && v != BsonNull.Value)
            .Select(v => v.IsObjectId ? v.AsObjectId.ToString() : v.AsString)
            .ToList();
    }

    private static FilterDefinition<Entity>? BuildParentScopeFilter(Type childType, string parentId)
    {
        if (typeof(MultiscopedEntity<>).MakeGenericType(childType.BaseType?.GetGenericArguments().FirstOrDefault() ?? childType).IsAssignableFrom(childType)
            || HasProperty(childType, "ScopeId"))
        {
            return Builders<Entity>.Filter.Eq("ScopeId", parentId);
        }
        if (HasProperty(childType, "Scopes"))
        {
            return Builders<Entity>.Filter.AnyEq("Scopes", parentId);
        }
        return Builders<Entity>.Filter.Eq("_id", parentId);
    }

    private static bool HasProperty(Type type, string name) => type.GetProperty(name) is not null;

    private async Task<CascadeStepResult> SoftDeleteAsync(
        IMongoCollection<Entity> collection,
        CascadeStep step,
        List<string> ids,
        IClientSessionHandle? session,
        CancellationToken ct)
    {
        var filter = Builders<Entity>.Filter.In("_id", ids.Select(BsonString.Create).ToList());
        var update = Builders<Entity>.Update
            .Set("IsDeleted", true)
            .Set("DeletedAt", DateTime.UtcNow)
            .Set("DeletedBy", step.ParentId)
            .Inc("_v", 1L);
        if (session is null) await collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        else await collection.UpdateManyAsync(session, filter, update, cancellationToken: ct);
        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }

    private async Task<CascadeStepResult> ArchiveAsync(
        IMongoCollection<Entity> collection,
        CascadeStep step,
        List<string> ids,
        IClientSessionHandle? session,
        CancellationToken ct)
    {
        var filter = Builders<Entity>.Filter.In("_id", ids.Select(BsonString.Create).ToList());
        var update = Builders<Entity>.Update
            .Set("IsArchived", true)
            .Set("ArchivedAt", DateTime.UtcNow)
            .Set("ArchivedBy", step.ParentId)
            .Inc("_v", 1L);
        if (session is null) await collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        else await collection.UpdateManyAsync(session, filter, update, cancellationToken: ct);
        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }

    private async Task<CascadeStepResult> HardDeleteAsync(
        IMongoCollection<Entity> collection,
        CascadeStep step,
        List<string> ids,
        IClientSessionHandle? session,
        CancellationToken ct)
    {
        var filter = Builders<Entity>.Filter.In("_id", ids.Select(BsonString.Create).ToList());
        if (session is null) await collection.DeleteManyAsync(filter, cancellationToken: ct);
        else await collection.DeleteManyAsync(session, filter, cancellationToken: ct);
        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }
}
