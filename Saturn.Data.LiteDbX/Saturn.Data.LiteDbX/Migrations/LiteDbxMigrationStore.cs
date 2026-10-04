using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

internal sealed class LiteDbxMigrationStore : IMigrationStore
{
    private readonly LiteDatabase database;

    public LiteDbxMigrationStore(LiteDatabase database)
    {
        this.database = database;
    }

    public MigrationStoreCapabilities Capabilities { get; } = new()
    {
        SupportsRawDocuments = true,
        SupportsRebuild = true,
        SupportsRenameCollection = true,
        SupportsIndexEnumeration = false,
        SupportsTransactions = false,
        SupportsObjectIdOnDisk = true,
        SupportsIncludeDeleted = true
    };

    public IMigrationFieldMap FieldMap { get; } = new MigrationFieldMap(new[]
    {
        new KeyValuePair<string, string>("Properties", "_p")
    });

    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var name in database.GetCollectionNames(cancellationToken))
        {
            yield return name;
        }
    }

    public bool CollectionExists(string collection) => database.CollectionExists(collection).AsTask().GetAwaiter().GetResult();

    public IMigrationCollection GetCollection(string collection) => new LiteDbxMigrationCollection(database, collection);

    public async Task<bool> RenameCollectionAsync(string source, string target, CancellationToken cancellationToken = default)
        => await database.RenameCollection(source, target, cancellationToken);

    public async Task<bool> DropCollectionAsync(string collection, CancellationToken cancellationToken = default)
        => await database.DropCollection(collection, cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
    }
}

internal sealed class LiteDbxMigrationCollection : IMigrationCollection
{
    private readonly ILiteCollection<BsonDocument> collection;

    public LiteDbxMigrationCollection(LiteDatabase database, string name)
    {
        Name = name;
        collection = database.GetCollection(name, BsonAutoId.ObjectId);
    }

    public string Name { get; }

    public string IdFieldName => MigrationIds.IdField;

    public async IAsyncEnumerable<MigrationObject> ScanAsync(bool includeDeleted, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var document in collection.FindAll(cancellationToken))
        {
            yield return LiteDbxBsonConverter.ToMigrationObject(document);
        }
    }

    public async Task InsertAsync(MigrationObject document, CancellationToken cancellationToken = default)
    {
        await collection.Insert(LiteDbxBsonConverter.ToBsonDocument(document), cancellationToken);
    }

    public async Task<bool> UpdateAsync(MigrationObject document, CancellationToken cancellationToken = default)
        => await collection.Update(LiteDbxBsonConverter.ToBsonDocument(document), cancellationToken);

    public async Task<bool> DeleteAsync(MigrationValue id, CancellationToken cancellationToken = default)
        => await collection.Delete(LiteDbxBsonConverter.ToBsonValue(id), cancellationToken);

    public IReadOnlyList<MigrationIndexDefinition> GetIndexes() => Array.Empty<MigrationIndexDefinition>();

    public Task EnsureIndexAsync(MigrationIndexDefinition index, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("LiteDbX indexes are not discoverable; re-declare them explicitly after a rebuild.");
}
