using System;
using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Migrations;

internal sealed class JournalStore
{
    public const string CollectionName = "__saturn_migrations";
    public const string IdMappingCollection = "__saturn_migration_id_mappings";

    private readonly IMigrationStore store;

    public JournalStore(IMigrationStore store)
    {
        this.store = store;
    }

    public async Task<bool> IsAppliedAsync(string name, CancellationToken cancellationToken)
    {
        if (!store.CollectionExists(CollectionName))
        {
            return false;
        }

        var collection = store.GetCollection(CollectionName);

        await foreach (var document in collection.ScanAsync(includeDeleted: true, cancellationToken).ConfigureAwait(false))
        {
            if (document.TryGetValue("Name", out var value) && value.IsString && string.Equals(value.AsString, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public async Task MarkAppliedAsync(string name, string runId, CancellationToken cancellationToken)
    {
        var collection = store.GetCollection(CollectionName);
        var document = new MigrationObject();
        document.Set("Name", MigrationValue.From(name));
        document.Set("RunId", MigrationValue.From(runId));
        document.Set("AppliedUtc", MigrationValue.From(DateTime.UtcNow));
        await collection.InsertAsync(document, cancellationToken).ConfigureAwait(false);
    }
}
