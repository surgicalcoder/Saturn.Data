using System;
using System.Collections.Generic;
using System.Linq;
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
        SupportsIndexEnumeration = true,
        SupportsTransactions = false,
        SupportsObjectIdOnDisk = true,
        SupportsIncludeDeleted = true
    };

    public IMigrationFieldMap FieldMap => MigrationFieldAliases.LiteDbx;

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
    private readonly LiteDatabase database;
    private readonly ILiteCollection<BsonDocument> collection;

    public LiteDbxMigrationCollection(LiteDatabase database, string name)
    {
        this.database = database;
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

    public IReadOnlyList<MigrationIndexDefinition> GetIndexes()
    {
        var definitions = new List<MigrationIndexDefinition>();
        var system = database.GetCollection("$indexes", BsonAutoId.ObjectId);

        foreach (var index in system.FindAll().ToBlockingEnumerable())
        {
            var owner = index.TryGetValue("collection", out var collectionValue) ? collectionValue.AsString : null;

            if (!string.Equals(owner, Name, StringComparison.Ordinal))
            {
                continue;
            }

            var name = index.TryGetValue("name", out var nameValue) ? nameValue.AsString : null;

            if (string.IsNullOrEmpty(name) || string.Equals(name, "_id", StringComparison.Ordinal) || string.Equals(name, "_id_", StringComparison.Ordinal))
            {
                continue;
            }

            var expression = index.TryGetValue("expression", out var expressionValue)
                ? expressionValue.AsString
                : index.TryGetValue("expr", out var exprValue) ? exprValue.AsString : null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                continue;
            }

            var unique = index.TryGetValue("unique", out var uniqueValue) && uniqueValue.AsBoolean;
            definitions.Add(new MigrationIndexDefinition(name, expression, unique));
        }

        return definitions;
    }

    public async Task EnsureIndexAsync(MigrationIndexDefinition index, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(index.Expression))
        {
            throw new ArgumentException($"Index '{index.Name}' has no expression to replay.", nameof(index));
        }

        await collection.EnsureIndex(index.Name, BsonExpression.Create(index.Expression), index.Unique, cancellationToken);
    }
}
