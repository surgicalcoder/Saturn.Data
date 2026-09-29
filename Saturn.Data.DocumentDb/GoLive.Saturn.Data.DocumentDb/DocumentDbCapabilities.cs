using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

public sealed class DocumentDbCapabilities
{
    public required string BackendName { get; init; }

    public required bool SupportsTransactions { get; init; }

    public required bool SupportsPessimisticLocking { get; init; }

    public required bool SupportsUniqueIndexes { get; init; }

    public required bool SupportsJsonMergePatch { get; init; }

    public required bool SupportsBatchUpsert { get; init; }

    public required bool SupportsChangeFeed { get; init; }

    public required bool RequiresSingleConnection { get; init; }

    public required long MaxBlobSize { get; init; }

    public static DocumentDbCapabilities Probe(IDocumentStore store, IDatabaseProvider provider, string backendName)
    {
        return new DocumentDbCapabilities
        {
            BackendName = backendName,
            SupportsTransactions = store.SupportsTransactions,
            SupportsPessimisticLocking = store.SupportsPessimisticLocking,
            SupportsUniqueIndexes = provider?.SupportsUniqueIndexes ?? false,
            SupportsJsonMergePatch = provider?.SupportsJsonMergePatch ?? false,
            SupportsBatchUpsert = provider?.SupportsBatchUpsert ?? false,
            SupportsChangeFeed = provider?.SupportsChangeFeed ?? false,
            RequiresSingleConnection = provider?.RequiresSingleConnection ?? false,
            MaxBlobSize = store.MaxBlobSize
        };
    }
}
