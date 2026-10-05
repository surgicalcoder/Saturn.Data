using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

internal sealed class MongoMigrationStore : IMigrationStore
{
    private readonly IMongoDatabase database;

    public MongoMigrationStore(IMongoDatabase database)
    {
        this.database = database;
    }

    public MigrationStoreCapabilities Capabilities { get; } = new()
    {
        SupportsRawDocuments = true,
        SupportsRebuild = true,
        SupportsRenameCollection = true,
        SupportsIndexEnumeration = true,
        SupportsTransactions = true,
        SupportsObjectIdOnDisk = true,
        SupportsIncludeDeleted = true
    };

    public IMigrationFieldMap FieldMap => MigrationFieldAliases.MongoDb;

    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var cursor = await database.ListCollectionNamesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var name in cursor.Current)
            {
                yield return name;
            }
        }
    }

    public bool CollectionExists(string collection)
    {
        var options = new ListCollectionNamesOptions { Filter = Builders<BsonDocument>.Filter.Eq("name", collection) };
        using var cursor = database.ListCollectionNames(options);
        return cursor.Any();
    }

    public IMigrationCollection GetCollection(string collection) => new MongoMigrationCollection(database, collection);

    public async Task<bool> RenameCollectionAsync(string source, string target, CancellationToken cancellationToken = default)
    {
        await database.RenameCollectionAsync(source, target, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DropCollectionAsync(string collection, CancellationToken cancellationToken = default)
    {
        await database.DropCollectionAsync(collection, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
    }
}

internal sealed class MongoMigrationCollection : IMigrationCollection
{
    private readonly IMongoCollection<BsonDocument> collection;
    private readonly IMongoDatabase database;

    public MongoMigrationCollection(IMongoDatabase database, string name)
    {
        this.database = database;
        Name = name;
        collection = database.GetCollection<BsonDocument>(name);
    }

    public string Name { get; }

    public string IdFieldName => MigrationIds.IdField;

    public async IAsyncEnumerable<MigrationObject> ScanAsync(bool includeDeleted, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var filter = includeDeleted
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Ne("IsDeleted", true);

        using var cursor = await collection.Find(filter).ToCursorAsync(cancellationToken).ConfigureAwait(false);

        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var document in cursor.Current)
            {
                yield return MongoBsonConverter.ToMigrationObject(document);
            }
        }
    }

    public async Task InsertAsync(MigrationObject document, CancellationToken cancellationToken = default)
    {
        await collection.InsertOneAsync(MongoBsonConverter.ToBsonDocument(document), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAsync(MigrationObject document, CancellationToken cancellationToken = default)
    {
        var bson = MongoBsonConverter.ToBsonDocument(document);
        var filter = Builders<BsonDocument>.Filter.Eq(MigrationIds.IdField, bson[MigrationIds.IdField]);
        var result = await collection.ReplaceOneAsync(filter, bson, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(MigrationValue id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(MigrationIds.IdField, MongoBsonConverter.ToBsonValue(id));
        var result = await collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    public IReadOnlyList<MigrationIndexDefinition> GetIndexes()
    {
        var definitions = new List<MigrationIndexDefinition>();
        using var cursor = collection.Indexes.List();

        foreach (var index in cursor.ToEnumerable())
        {
            if (!index.Contains("key"))
            {
                continue;
            }

            var name = index.TryGetValue("name", out var nameValue) ? nameValue.AsString : null;
            var unique = index.TryGetValue("unique", out var uniqueValue) && uniqueValue.AsBoolean;
            var expression = string.Empty;
            var keyDocument = index["key"].AsBsonDocument;

            foreach (var key in keyDocument)
            {
                expression = key.Name;
                break;
            }

            if (string.Equals(name, "_id_", StringComparison.Ordinal))
            {
                continue;
            }

            definitions.Add(new MigrationIndexDefinition(name, expression, unique));
        }

        return definitions;
    }

    public async Task EnsureIndexAsync(MigrationIndexDefinition index, CancellationToken cancellationToken = default)
    {
        var keys = Builders<BsonDocument>.IndexKeys.Ascending(index.Expression);
        var model = new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = index.Name, Unique = index.Unique });
        await collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
