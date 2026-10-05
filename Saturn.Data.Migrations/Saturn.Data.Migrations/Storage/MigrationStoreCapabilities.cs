namespace GoLive.Saturn.Data.Migrations;

public sealed class MigrationStoreCapabilities
{
    public bool SupportsRawDocuments { get; init; }
    public bool SupportsRebuild { get; init; }
    public bool SupportsRenameCollection { get; init; }
    public bool SupportsIndexEnumeration { get; init; }
    public bool SupportsTransactions { get; init; }
    public bool SupportsObjectIdOnDisk { get; init; }
    public bool SupportsIncludeDeleted { get; init; }
    public bool SupportsBatchInsert { get; init; }

    public static MigrationStoreCapabilities Full { get; } = new()
    {
        SupportsRawDocuments = true,
        SupportsRebuild = true,
        SupportsRenameCollection = true,
        SupportsIndexEnumeration = true,
        SupportsTransactions = true,
        SupportsObjectIdOnDisk = true,
        SupportsIncludeDeleted = true
    };
}
