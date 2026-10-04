using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;

namespace Saturn.Data.Migrations.Tests;

public sealed class InMemoryMigrationStore : IMigrationStore
{
    private readonly Dictionary<string, InMemoryMigrationCollection> collections = new(StringComparer.Ordinal);

    public MigrationStoreCapabilities Capabilities { get; } = MigrationStoreCapabilities.Full;

    public IMigrationCollection Seed(string name, params MigrationObject[] documents)
    {
        var collection = GetCollection(name);

        foreach (var document in documents)
        {
            collection.InsertAsync(document).GetAwaiter().GetResult();
        }

        return collection;
    }

    public async IAsyncEnumerable<string> GetCollectionsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var name in collections.Keys.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return name;
        }

        await Task.CompletedTask;
    }

    public bool CollectionExists(string collection) => collections.ContainsKey(collection);

    public IMigrationCollection GetCollection(string collection)
    {
        if (!collections.TryGetValue(collection, out var existing))
        {
            existing = new InMemoryMigrationCollection(collection);
            collections[collection] = existing;
        }

        return existing;
    }

    public Task<bool> RenameCollectionAsync(string source, string target, CancellationToken cancellationToken = default)
    {
        if (!collections.TryGetValue(source, out var collection))
        {
            return Task.FromResult(false);
        }

        collections.Remove(source);
        collection.Rename(target);
        collections[target] = collection;
        return Task.FromResult(true);
    }

    public Task<bool> DropCollectionAsync(string collection, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(collections.Remove(collection));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
    }
}

public sealed class InMemoryMigrationCollection : IMigrationCollection
{
    private readonly List<MigrationObject> documents = new();

    public InMemoryMigrationCollection(string name)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public string IdFieldName => MigrationIds.IdField;

    public IReadOnlyList<MigrationIndexDefinition> GetIndexes() => Array.Empty<MigrationIndexDefinition>();

    public Task EnsureIndexAsync(MigrationIndexDefinition index, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Rename(string name) => Name = name;

    public async IAsyncEnumerable<MigrationObject> ScanAsync(bool includeDeleted, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var document in documents.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!includeDeleted && IsDeleted(document))
            {
                continue;
            }

            yield return document;
        }

        await Task.CompletedTask;
    }

    public Task InsertAsync(MigrationObject document, CancellationToken cancellationToken = default)
    {
        if (!document.TryGetValue(MigrationIds.IdField, out var id) || id == null || id.IsNull)
        {
            document.Set(MigrationIds.IdField, MigrationValue.From(MigrationObjectId.NewObjectId()));
        }

        documents.Add(document);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(MigrationObject document, CancellationToken cancellationToken = default)
    {
        if (!document.TryGetValue(MigrationIds.IdField, out var id))
        {
            return Task.FromResult(false);
        }

        var key = IdString(id);
        var index = documents.FindIndex(existing => existing.TryGetValue(MigrationIds.IdField, out var existingId) && IdString(existingId) == key);

        if (index < 0)
        {
            return Task.FromResult(false);
        }

        documents[index] = document;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(MigrationValue id, CancellationToken cancellationToken = default)
    {
        var key = IdString(id);
        var removed = documents.RemoveAll(existing => existing.TryGetValue(MigrationIds.IdField, out var existingId) && IdString(existingId) == key);
        return Task.FromResult(removed > 0);
    }

    private static bool IsDeleted(MigrationObject document)
    {
        return document.TryGetValue("IsDeleted", out var value) && value.IsBoolean && value.AsBoolean;
    }

    private static string IdString(MigrationValue value)
    {
        if (value == null)
        {
            return null;
        }

        return value.IsObjectId ? value.AsObjectId.ToString() : value.AsString;
    }
}
