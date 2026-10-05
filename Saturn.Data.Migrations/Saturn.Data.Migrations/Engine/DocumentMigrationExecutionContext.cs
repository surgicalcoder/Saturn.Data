using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Migrations;

public sealed class IdRemapEntry
{
    public IdRemapEntry(string migrationName, string runId, string sourceCollection, string oldId, string newObjectId, InvalidObjectIdPolicy policy, int documentOrdinal)
    {
        MigrationName = migrationName;
        RunId = runId;
        SourceCollection = sourceCollection;
        OldId = oldId;
        NewObjectId = newObjectId;
        Policy = policy;
        DocumentOrdinal = documentOrdinal;
    }

    public string MigrationName { get; }
    public string RunId { get; }
    public string SourceCollection { get; }
    public string OldId { get; }
    public string NewObjectId { get; }
    public InvalidObjectIdPolicy Policy { get; }
    public int DocumentOrdinal { get; }
}

public sealed class RemapLookup
{
    private readonly Dictionary<string, Dictionary<string, string>> mappings = new(StringComparer.Ordinal);

    public static RemapLookup Empty { get; } = new();

    public bool IsEmpty => mappings.Count == 0;

    public void Add(string collection, string migrationName, string oldId, string newId)
    {
        if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId))
        {
            return;
        }

        var key = collection + "|" + migrationName;

        if (!mappings.TryGetValue(key, out var entries))
        {
            entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            mappings[key] = entries;
        }

        entries[oldId] = newId;
    }

    public bool TryResolve(string collection, string migrationName, string oldId, out string newId)
    {
        newId = null;

        if (string.IsNullOrEmpty(oldId))
        {
            return false;
        }

        return mappings.TryGetValue(collection + "|" + migrationName, out var entries) && entries.TryGetValue(oldId, out newId);
    }
}

public sealed class DocumentMigrationExecutionContext
{
    public DocumentMigrationExecutionContext(string collection, string migrationName, string runId, bool strictPathResolution)
    {
        Collection = collection;
        MigrationName = migrationName;
        RunId = runId;
        StrictPathResolution = strictPathResolution;
    }

    public string Collection { get; }
    public string MigrationName { get; }
    public string RunId { get; }
    public bool StrictPathResolution { get; }
    public bool SkipDocument { get; set; }
    public int DocumentOrdinal { get; set; }
    public RemapLookup Remaps { get; set; } = RemapLookup.Empty;
    public int GeneratedIdMappings { get; private set; }
    public int RepairedReferences { get; private set; }
    public List<InvalidValueSample> InvalidValueSamples { get; } = new();
    public List<IdRemapEntry> IdRemaps { get; } = new();
    public List<MigrationObject> PendingInserts { get; } = new();
    public List<string> StrictPathFailures { get; } = new();

    public int InvalidValueCount => InvalidValueSamples.Count;

    public void RecordInvalidValue(string path, string value, string reason)
    {
        InvalidValueSamples.Add(new InvalidValueSample(Collection, path, value, reason));
    }

    public void RecordPathFailure(string path, string reason)
    {
        StrictPathFailures.Add(path + ": " + reason);
    }

    public void RecordGeneratedId(string oldId, string newId, InvalidObjectIdPolicy policy)
    {
        GeneratedIdMappings++;
        IdRemaps.Add(new IdRemapEntry(MigrationName, RunId, Collection, oldId, newId, policy, DocumentOrdinal));
    }

    public void RecordRepairedReference()
    {
        RepairedReferences++;
    }
}
