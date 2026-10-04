using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Migrations;

public interface IMigrationStore : IAsyncDisposable, IDisposable
{
    MigrationStoreCapabilities Capabilities { get; }

    IAsyncEnumerable<string> GetCollectionsAsync(CancellationToken cancellationToken = default);

    bool CollectionExists(string collection);

    IMigrationCollection GetCollection(string collection);

    Task<bool> RenameCollectionAsync(string source, string target, CancellationToken cancellationToken = default);

    Task<bool> DropCollectionAsync(string collection, CancellationToken cancellationToken = default);
}

public interface IMigrationCollection
{
    string Name { get; }

    string IdFieldName { get; }

    IAsyncEnumerable<MigrationObject> ScanAsync(bool includeDeleted, CancellationToken cancellationToken = default);

    Task InsertAsync(MigrationObject document, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(MigrationObject document, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(MigrationValue id, CancellationToken cancellationToken = default);

    IReadOnlyList<MigrationIndexDefinition> GetIndexes();

    Task EnsureIndexAsync(MigrationIndexDefinition index, CancellationToken cancellationToken = default);
}

public interface IMigrationStoreSource
{
    IMigrationStore CreateMigrationStore();
}
