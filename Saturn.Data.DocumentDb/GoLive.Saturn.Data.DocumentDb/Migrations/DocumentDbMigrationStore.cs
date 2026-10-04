using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

internal sealed class DocumentDbMigrationStore : IMigrationStore
{
    private readonly IDocumentStore store;

    public DocumentDbMigrationStore(IDocumentStore store)
    {
        this.store = store;
    }

    public MigrationStoreCapabilities Capabilities { get; } = new()
    {
        SupportsRawDocuments = true,
        SupportsRebuild = false,
        SupportsRenameCollection = false,
        SupportsIndexEnumeration = false,
        SupportsTransactions = true,
        SupportsObjectIdOnDisk = false,
        SupportsIncludeDeleted = true
    };

    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public bool CollectionExists(string collection) => true;

    public IMigrationCollection GetCollection(string collection) => new DocumentDbMigrationCollection(store, collection);

    public Task<bool> RenameCollectionAsync(string source, string target, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DocumentDb uses a single shared table keyed by TypeName; collection rename is not supported.");

    public Task<bool> DropCollectionAsync(string collection, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DocumentDb uses a single shared table keyed by TypeName; collection drop is not supported.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
    }
}

internal sealed class DocumentDbMigrationCollection : IMigrationCollection
{
    private readonly IDocumentStore store;

    public DocumentDbMigrationCollection(IDocumentStore store, string name)
    {
        this.store = store;
        Name = name;
    }

    public string Name { get; }

    public string IdFieldName => "Id";

    public async IAsyncEnumerable<MigrationObject> ScanAsync(bool includeDeleted, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var collection = store.Collection(Name, "Id");

        await foreach (var document in collection.QueryStream("1=1", null, cancellationToken).ConfigureAwait(false))
        {
            yield return DocumentDbJsonConverter.ToMigrationObject(document);
        }
    }

    public async Task InsertAsync(MigrationObject document, CancellationToken cancellationToken = default)
    {
        var collection = store.Collection(Name, "Id");
        await collection.Insert(DocumentDbJsonConverter.ToJsonObject(document), cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAsync(MigrationObject document, CancellationToken cancellationToken = default)
    {
        var collection = store.Collection(Name, "Id");
        await collection.Upsert(DocumentDbJsonConverter.ToJsonObject(document), patchIfUpdate: false, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteAsync(MigrationValue id, CancellationToken cancellationToken = default)
    {
        var collection = store.Collection(Name, "Id");
        var idText = id == null ? null : id.IsObjectId ? id.AsObjectId.ToString() : id.AsString;
        return await collection.Remove(idText, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<MigrationIndexDefinition> GetIndexes() => Array.Empty<MigrationIndexDefinition>();

    public async Task EnsureIndexAsync(MigrationIndexDefinition index, CancellationToken cancellationToken = default)
    {
        var collection = store.Collection(Name, "Id");
        await collection.CreateIndex(cancellationToken, index.Expression).ConfigureAwait(false);
    }
}
