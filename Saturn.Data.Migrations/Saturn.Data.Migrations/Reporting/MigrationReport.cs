using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Migrations;

public enum BackupRetentionPolicy
{
    KeepAll,
    DeleteOnSuccess
}

public enum BackupDisposition
{
    None,
    Retained,
    DeletedOnSuccess,
    PlannedDeleteOnSuccess,
    PlannedRetain
}

public sealed class InvalidValueSample
{
    public InvalidValueSample(string collection, string path, string value, string reason)
    {
        Collection = collection;
        Path = path;
        Value = value;
        Reason = reason;
    }

    public string Collection { get; }
    public string Path { get; }
    public string Value { get; }
    public string Reason { get; }
}

public sealed class RebuildValidationSummary
{
    public RebuildValidationSummary(int sourceDocumentCount, int expectedTargetDocumentCount, int preparedTargetDocumentCount, int duplicateTargetIdCount)
    {
        SourceDocumentCount = sourceDocumentCount;
        ExpectedTargetDocumentCount = expectedTargetDocumentCount;
        PreparedTargetDocumentCount = preparedTargetDocumentCount;
        DuplicateTargetIdCount = duplicateTargetIdCount;
    }

    public int SourceDocumentCount { get; }
    public int ExpectedTargetDocumentCount { get; }
    public int PreparedTargetDocumentCount { get; }
    public int DuplicateTargetIdCount { get; }
}

public sealed class DuplicateTargetIdSample
{
    public DuplicateTargetIdSample(string collection, string targetId, int documentOrdinal)
    {
        Collection = collection;
        TargetId = targetId;
        DocumentOrdinal = documentOrdinal;
    }

    public string Collection { get; }
    public string TargetId { get; }
    public int DocumentOrdinal { get; }
}

public sealed class CollectionMigrationResult
{
    public CollectionMigrationResult(
        string collectionName,
        int documentsScanned,
        int documentsModified,
        int documentsRemoved,
        int documentsInserted,
        int generatedIdMappings,
        int repairedReferences,
        int invalidValueCount,
        IReadOnlyList<InvalidValueSample> invalidValueSamples,
        RebuildValidationSummary rebuildValidation,
        string backupCollectionName,
        BackupDisposition backupDisposition,
        IReadOnlyList<DuplicateTargetIdSample> duplicateTargetIdSamples = null,
        IReadOnlyList<MigrationIndexDefinition> replayedIndexes = null)
    {
        CollectionName = collectionName;
        DocumentsScanned = documentsScanned;
        DocumentsModified = documentsModified;
        DocumentsRemoved = documentsRemoved;
        DocumentsInserted = documentsInserted;
        GeneratedIdMappings = generatedIdMappings;
        RepairedReferences = repairedReferences;
        InvalidValueCount = invalidValueCount;
        InvalidValueSamples = invalidValueSamples ?? Array.Empty<InvalidValueSample>();
        RebuildValidation = rebuildValidation;
        BackupCollectionName = backupCollectionName;
        BackupDisposition = backupDisposition;
        DuplicateTargetIdSamples = duplicateTargetIdSamples ?? Array.Empty<DuplicateTargetIdSample>();
        ReplayedIndexes = replayedIndexes ?? Array.Empty<MigrationIndexDefinition>();
    }

    public string CollectionName { get; }
    public int DocumentsScanned { get; }
    public int DocumentsModified { get; }
    public int DocumentsRemoved { get; }
    public int DocumentsInserted { get; }
    public int GeneratedIdMappings { get; }
    public int RepairedReferences { get; }
    public int InvalidValueCount { get; }
    public IReadOnlyList<InvalidValueSample> InvalidValueSamples { get; }
    public RebuildValidationSummary RebuildValidation { get; }
    public string BackupCollectionName { get; }
    public BackupDisposition BackupDisposition { get; }
    public IReadOnlyList<DuplicateTargetIdSample> DuplicateTargetIdSamples { get; }
    public IReadOnlyList<MigrationIndexDefinition> ReplayedIndexes { get; }
}

public sealed class CollectionSelectorResult
{
    public CollectionSelectorResult(string selector, IReadOnlyList<string> matchedCollections, IReadOnlyList<CollectionMigrationResult> collections)
    {
        Selector = selector;
        MatchedCollections = matchedCollections ?? Array.Empty<string>();
        Collections = collections ?? Array.Empty<CollectionMigrationResult>();
    }

    public string Selector { get; }
    public IReadOnlyList<string> MatchedCollections { get; }
    public IReadOnlyList<CollectionMigrationResult> Collections { get; }
    public bool IsUnmatched => MatchedCollections.Count == 0;
}

public sealed class MigrationExecutionResult
{
    public MigrationExecutionResult(
        string name,
        string runId,
        bool wasApplied,
        bool isDryRun,
        IReadOnlyList<CollectionSelectorResult> selectors,
        int documentsScanned,
        int documentsModified,
        int documentsRemoved,
        int documentsInserted,
        int generatedIdMappings,
        int repairedReferences,
        int invalidValueCount)
    {
        Name = name;
        RunId = runId;
        WasApplied = wasApplied;
        IsDryRun = isDryRun;
        Selectors = selectors ?? Array.Empty<CollectionSelectorResult>();
        DocumentsScanned = documentsScanned;
        DocumentsModified = documentsModified;
        DocumentsRemoved = documentsRemoved;
        DocumentsInserted = documentsInserted;
        GeneratedIdMappings = generatedIdMappings;
        RepairedReferences = repairedReferences;
        InvalidValueCount = invalidValueCount;
    }

    public string Name { get; }
    public string RunId { get; }
    public bool WasApplied { get; }
    public bool IsDryRun { get; }
    public bool WasSkipped => !WasApplied && !IsDryRun;
    public IReadOnlyList<CollectionSelectorResult> Selectors { get; }
    public int DocumentsScanned { get; }
    public int DocumentsModified { get; }
    public int DocumentsRemoved { get; }
    public int DocumentsInserted { get; }
    public int GeneratedIdMappings { get; }
    public int RepairedReferences { get; }
    public int InvalidValueCount { get; }
}

public sealed class MigrationReport
{
    public MigrationReport(IReadOnlyList<MigrationExecutionResult> migrations)
    {
        Migrations = migrations ?? Array.Empty<MigrationExecutionResult>();
    }

    public IReadOnlyList<MigrationExecutionResult> Migrations { get; }
}
