using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

internal sealed class DocumentDbMigrationStore : IMigrationStore
{
    private readonly IDocumentStore store;
    private readonly IDocumentBackup backup;

    public DocumentDbMigrationStore(IDocumentStore store)
    {
        this.store = store;
        backup = store as IDocumentBackup;
        Capabilities = new MigrationStoreCapabilities
        {
            SupportsRawDocuments = backup != null,
            SupportsRebuild = backup != null,
            SupportsRenameCollection = backup != null,
            SupportsIndexEnumeration = false,
            SupportsTransactions = true,
            SupportsObjectIdOnDisk = false,
            SupportsIncludeDeleted = true,
            SupportsBatchInsert = backup != null
        };
    }

    public MigrationStoreCapabilities Capabilities { get; }

    public IMigrationFieldMap FieldMap => IdentityMigrationFieldMap.Instance;

    internal IDocumentStore Store => store;

    internal bool SupportsBackup => backup != null;

    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (backup == null)
        {
            yield break;
        }

        var rows = await DocumentDbBackup.ReadAsync(backup, null, cancellationToken).ConfigureAwait(false);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (row.DocType != null && seen.Add(row.DocType))
            {
                yield return row.DocType;
            }
        }
    }

    public bool CollectionExists(string collection)
    {
        if (backup == null)
        {
            return false;
        }

        var rows = DocumentDbBackup.ReadAsync(backup, new[] { collection }, CancellationToken.None).GetAwaiter().GetResult();
        return rows.Any(row => string.Equals(row.DocType, collection, StringComparison.Ordinal));
    }

    public IMigrationCollection GetCollection(string collection) => new DocumentDbMigrationCollection(store, backup, collection);

    public async Task<bool> RenameCollectionAsync(string source, string target, CancellationToken cancellationToken = default)
    {
        if (backup == null)
        {
            throw new NotSupportedException("DocumentDb collection rename requires the store to implement IDocumentBackup.");
        }

        var rows = await DocumentDbBackup.ReadAsync(backup, new[] { source }, cancellationToken).ConfigureAwait(false);
        var sourceRows = rows.Where(row => string.Equals(row.DocType, source, StringComparison.Ordinal)).ToList();

        if (sourceRows.Count == 0)
        {
            return false;
        }

        var moved = sourceRows.Select(row => new RawDocument(row.Id, target, Encoding.UTF8.GetBytes(row.Data), null, null, null)).ToList();
        await backup.BulkImportAsync(DocumentDbBackup.AsAsyncEnumerable(moved, cancellationToken), new BulkRestoreOptions { SingleTransaction = true, ChunkSize = 500 }, cancellationToken).ConfigureAwait(false);

        var collection = store.Collection(source, "Id");
        await collection.BatchRemove(sourceRows.Select(row => (object)row.Id).ToList(), cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DropCollectionAsync(string collection, CancellationToken cancellationToken = default)
    {
        if (backup == null)
        {
            throw new NotSupportedException("DocumentDb collection drop requires the store to implement IDocumentBackup.");
        }

        var rows = await DocumentDbBackup.ReadAsync(backup, new[] { collection }, cancellationToken).ConfigureAwait(false);
        var ids = rows.Where(row => string.Equals(row.DocType, collection, StringComparison.Ordinal)).Select(row => (object)row.Id).ToList();

        if (ids.Count == 0)
        {
            return false;
        }

        await store.Collection(collection, "Id").BatchRemove(ids, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
    }
}

internal sealed class DocumentDbMigrationCollection : IMigrationCollection
{
    private readonly IDocumentStore store;
    private readonly IDocumentBackup backup;

    public DocumentDbMigrationCollection(IDocumentStore store, IDocumentBackup backup, string name)
    {
        this.store = store;
        this.backup = backup;
        Name = name;
    }

    public string Name { get; }

    public string IdFieldName => "Id";

    public async IAsyncEnumerable<MigrationObject> ScanAsync(bool includeDeleted, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (backup == null)
        {
            throw new NotSupportedException("DocumentDb raw migration requires the store to implement IDocumentBackup.");
        }

        var rows = await DocumentDbBackup.ReadAsync(backup, new[] { Name }, cancellationToken).ConfigureAwait(false);

        foreach (var row in rows)
        {
            if (!string.Equals(row.DocType, Name, StringComparison.Ordinal))
            {
                continue;
            }

            var node = string.IsNullOrWhiteSpace(row.Data) ? new JsonObject() : JsonNode.Parse(row.Data);
            var document = DocumentDbJsonConverter.ToMigrationObject(node);
            document.Set(MigrationIds.IdField, MigrationValue.From(row.Id));

            if (!includeDeleted && document.TryGetValue("IsDeleted", out var deleted) && deleted.IsBoolean && deleted.AsBoolean)
            {
                continue;
            }

            yield return document;
        }
    }

    public async Task InsertAsync(MigrationObject document, CancellationToken cancellationToken = default)
    {
        var collection = store.Collection(Name, "Id");
        await collection.Insert(DocumentDbJsonConverter.ToJsonObject(document), cancellationToken).ConfigureAwait(false);
    }

    public async Task InsertManyAsync(IAsyncEnumerable<MigrationObject> documents, CancellationToken cancellationToken = default)
    {
        if (backup == null)
        {
            await foreach (var document in documents)
            {
                await InsertAsync(document, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        await backup.BulkImportAsync(ToRawDocuments(documents, cancellationToken), new BulkRestoreOptions { SingleTransaction = true, ChunkSize = 500 }, cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<RawDocument> ToRawDocuments(IAsyncEnumerable<MigrationObject> documents, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var document in documents.WithCancellation(cancellationToken))
        {
            var json = DocumentDbJsonConverter.ToJsonObject(document).ToJsonString();
            var id = DocumentDbJsonConverter.GetIdText(document) ?? Guid.NewGuid().ToString("N");
            yield return new RawDocument(id, Name, Encoding.UTF8.GetBytes(json), null, null, null);
        }
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

internal sealed record DocumentDbBackupRow(string Id, string DocType, string Data);

internal static class DocumentDbBackup
{
    public static async Task<List<DocumentDbBackupRow>> ReadAsync(IDocumentBackup backup, IReadOnlyList<string> docTypes, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();

        var options = new BackupExportOptions();

        if (docTypes != null)
        {
            options.DocTypes = docTypes;
        }

        await backup.ExportAsync(stream, options, cancellationToken).ConfigureAwait(false);

        var rows = new List<DocumentDbBackupRow>();
        var bytes = stream.ToArray();

        if (bytes.Length == 0)
        {
            return rows;
        }

        using var document = JsonDocument.Parse(bytes);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return rows;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var id = GetString(element, "id") ?? GetString(element, "Id");
            var docType = GetString(element, "docType") ?? GetString(element, "TypeName") ?? GetString(element, "typeName");
            var data = element.TryGetProperty("data", out var dataElement) ? dataElement.GetRawText() : "{}";

            if (id != null)
            {
                rows.Add(new DocumentDbBackupRow(id, docType, data));
            }
        }

        return rows;
    }

    public static async IAsyncEnumerable<RawDocument> AsAsyncEnumerable(List<RawDocument> rows, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }

        await Task.CompletedTask;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
