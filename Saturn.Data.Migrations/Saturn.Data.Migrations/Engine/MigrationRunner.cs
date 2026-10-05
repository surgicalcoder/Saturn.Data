using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Migrations;

public sealed class MigrationRunner
{
    private readonly IMigrationStore store;
    private readonly List<MigrationDefinition> definitions = new();
    private readonly MigrationRunOptions options = new();

    internal MigrationRunner(IMigrationStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public MigrationRunner Migration(string name, Action<MigrationBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MigrationBuilder();
        configure(builder);
        definitions.Add(new MigrationDefinition(name, builder.Plans));

        return this;
    }

    public MigrationRunner WithBackupRetention(BackupRetentionPolicy policy)
    {
        options.BackupRetention = policy;
        return this;
    }

    public MigrationRunner WithDryRun(bool dryRun = true)
    {
        options.DryRun = dryRun;
        return this;
    }

    public MigrationRunner WithIncludeDeleted(bool includeDeleted = true)
    {
        options.IncludeDeleted = includeDeleted;
        return this;
    }

    public MigrationRunner WithStrictPathResolution(bool strict = true, bool throwOnFailure = true)
    {
        options.StrictPathResolution = strict;
        options.ThrowOnStrictPathFailure = throwOnFailure;
        return this;
    }

    public MigrationRunner OnProgress(Action<MigrationProgress> callback)
    {
        options.ProgressCallback = callback;
        return this;
    }

    public ValueTask<MigrationReport> RunAsync(CancellationToken cancellationToken = default)
        => RunAsync(options, cancellationToken);

    public async Task<BackupCleanupReport> CleanupBackupsAsync(BackupCleanupOptions cleanupOptions = null, CancellationToken cancellationToken = default)
    {
        cleanupOptions ??= new BackupCleanupOptions();

        var backups = new List<string>();

        await foreach (var name in store.GetCollectionsAsync(cancellationToken).ConfigureAwait(false))
        {
            var marker = name.IndexOf("__backup__", StringComparison.Ordinal);

            if (marker < 0)
            {
                continue;
            }

            var prefix = name[..marker];

            if (cleanupOptions.CollectionFilter != null && !MatchesGlob(cleanupOptions.CollectionFilter, prefix) && !MatchesGlob(cleanupOptions.CollectionFilter, name))
            {
                continue;
            }

            backups.Add(name);
        }

        var dropped = new List<string>();
        var retained = new List<string>();

        foreach (var group in backups.GroupBy(BackupPrefix, StringComparer.Ordinal))
        {
            var ordered = group.OrderByDescending(name => name, StringComparer.Ordinal).ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                if (i < cleanupOptions.KeepLatestCount)
                {
                    retained.Add(ordered[i]);
                    continue;
                }

                if (await store.DropCollectionAsync(ordered[i], cancellationToken).ConfigureAwait(false))
                {
                    dropped.Add(ordered[i]);
                }
            }
        }

        return new BackupCleanupReport(dropped, retained);
    }

    private static string BackupPrefix(string name)
    {
        var marker = name.IndexOf("__backup__", StringComparison.Ordinal);
        return marker < 0 ? name : name[..marker];
    }

    public async ValueTask<MigrationReport> RunAsync(MigrationRunOptions runOptions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runOptions);

        var runId = Guid.NewGuid().ToString("N")[..8];
        var journal = new JournalStore(store);
        var remaps = runOptions.DryRun ? RemapLookup.Empty : await LoadRemapsAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<MigrationExecutionResult>();

        foreach (var definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!runOptions.DryRun && await journal.IsAppliedAsync(definition.Name, cancellationToken).ConfigureAwait(false))
            {
                results.Add(new MigrationExecutionResult(definition.Name, runId, false, false, Array.Empty<CollectionSelectorResult>(), 0, 0, 0, 0, 0, 0, 0));
                continue;
            }

            Report(runOptions, MigrationProgressStage.MigrationStarted, definition.Name, null, null, runOptions.DryRun, 0, 0, 0, 0, 0, 0);

            var work = new List<(CollectionMigrationPlan Plan, List<string> Matched)>();
            var totalCollections = 0;

            foreach (var plan in definition.Collections)
            {
                var matched = ResolveCollections(plan.Selector);
                work.Add((plan, matched));
                totalCollections += matched.Count;
            }

            var selectorResults = new List<CollectionSelectorResult>();
            var completedCollections = 0;
            var scanned = 0;
            var modified = 0;
            var removed = 0;
            var inserted = 0;
            var generated = 0;
            var repaired = 0;
            var invalid = 0;
            var strictFailureCount = 0;

            foreach (var (plan, matched) in work)
            {
                var collectionResults = new List<CollectionMigrationResult>();

                foreach (var collectionName in matched)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Report(runOptions, MigrationProgressStage.CollectionStarted, definition.Name, plan.Selector, collectionName, runOptions.DryRun, completedCollections, totalCollections, scanned, modified, removed, inserted);

                    var result = await ProcessCollectionAsync(definition, plan, collectionName, runId, runOptions, remaps, cancellationToken).ConfigureAwait(false);
                    collectionResults.Add(result);

                    scanned += result.DocumentsScanned;
                    modified += result.DocumentsModified;
                    removed += result.DocumentsRemoved;
                    inserted += result.DocumentsInserted;
                    generated += result.GeneratedIdMappings;
                    repaired += result.RepairedReferences;
                    invalid += result.InvalidValueCount;
                    strictFailureCount += result.StrictPathFailureCount;
                    completedCollections++;

                    Report(runOptions, MigrationProgressStage.CollectionCompleted, definition.Name, plan.Selector, collectionName, runOptions.DryRun, completedCollections, totalCollections, scanned, modified, removed, inserted);
                }

                selectorResults.Add(new CollectionSelectorResult(plan.Selector, matched, collectionResults));
            }

            if (!runOptions.DryRun)
            {
                await journal.MarkAppliedAsync(definition.Name, runId, cancellationToken).ConfigureAwait(false);
            }

            Report(runOptions, MigrationProgressStage.MigrationCompleted, definition.Name, null, null, runOptions.DryRun, completedCollections, totalCollections, scanned, modified, removed, inserted);

            results.Add(new MigrationExecutionResult(definition.Name, runId, !runOptions.DryRun, runOptions.DryRun, selectorResults, scanned, modified, removed, inserted, generated, repaired, invalid, strictFailureCount));
        }

        return new MigrationReport(results);
    }

    private async Task<CollectionMigrationResult> ProcessCollectionAsync(MigrationDefinition definition, CollectionMigrationPlan plan, string collectionName, string runId, MigrationRunOptions runOptions, RemapLookup remaps, CancellationToken cancellationToken)
    {
        var requiresRebuild = plan.Operations.Any(operation => operation.RequiresRebuild);

        if (requiresRebuild)
        {
            if (!store.Capabilities.SupportsRebuild)
            {
                throw new NotSupportedException($"Migration '{definition.Name}' requires a collection rebuild, but '{store.GetType().Name}' does not support rebuilds.");
            }

            return await RebuildCollectionAsync(definition, plan, collectionName, runId, runOptions, remaps, cancellationToken).ConfigureAwait(false);
        }

        return await MigrateInPlaceAsync(definition, plan, collectionName, runId, runOptions, remaps, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CollectionMigrationResult> MigrateInPlaceAsync(MigrationDefinition definition, CollectionMigrationPlan plan, string collectionName, string runId, MigrationRunOptions runOptions, RemapLookup remaps, CancellationToken cancellationToken)
    {
        var collection = store.GetCollection(collectionName);
        var samples = new List<InvalidValueSample>();
        var scanned = 0;
        var modified = 0;
        var generated = 0;
        var repaired = 0;
        var ordinal = 0;
        var pendingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingList = new List<MigrationObject>();
        var strictFailures = 0;

        await foreach (var raw in collection.ScanAsync(runOptions.IncludeDeleted, cancellationToken).ConfigureAwait(false))
        {
            scanned++;
            ordinal++;

            var document = raw.Clone();
            MigrationFieldTranslation.ApplyToLogical(document, store.FieldMap);
            var context = CreateContext(collectionName, definition.Name, runId, ordinal, remaps, runOptions.StrictPathResolution);
            var changed = false;

            foreach (var operation in plan.Operations)
            {
                if (operation.Apply(document, context))
                {
                    changed = true;
                }

                if (context.SkipDocument)
                {
                    changed = false;
                    break;
                }
            }

            samples.AddRange(context.InvalidValueSamples);
            generated += context.GeneratedIdMappings;
            repaired += context.RepairedReferences;
            CollectRemaps(context, remaps);
            CollectPending(context, pendingIds, pendingList);
            strictFailures += context.StrictPathFailures.Count;

            if (!changed)
            {
                continue;
            }

            modified++;

            if (!runOptions.DryRun)
            {
                var physical = document.Clone();
                MigrationFieldTranslation.ApplyToPhysical(physical, store.FieldMap);
                await collection.UpdateAsync(physical, cancellationToken).ConfigureAwait(false);
                await PersistRemapsAsync(context, remaps, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!runOptions.DryRun)
        {
            await InsertPendingAsync(collection, pendingList, cancellationToken).ConfigureAwait(false);
        }

        if (runOptions.StrictPathResolution && runOptions.ThrowOnStrictPathFailure && strictFailures > 0)
        {
            throw new InvalidOperationException($"Migration '{definition.Name}' found {strictFailures} strict path resolution failure(s) in collection '{collectionName}'.");
        }

        return new CollectionMigrationResult(collectionName, scanned, modified, 0, 0, generated, repaired, samples.Count, samples, null, null, BackupDisposition.None, strictPathFailureCount: strictFailures);
    }

    private async Task<CollectionMigrationResult> RebuildCollectionAsync(MigrationDefinition definition, CollectionMigrationPlan plan, string collectionName, string runId, MigrationRunOptions runOptions, RemapLookup remaps, CancellationToken cancellationToken)
    {
        var source = store.GetCollection(collectionName);
        var shadowName = $"{collectionName}__migrating__{runId}";
        var backupName = $"{collectionName}__backup__{runId}";
        var shadow = store.GetCollection(shadowName);

        var samples = new List<InvalidValueSample>();
        var duplicates = new List<DuplicateTargetIdSample>();
        var scanned = 0;
        var inserted = 0;
        var generated = 0;
        var repaired = 0;
        var ordinal = 0;
        var targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batchDocs = new List<MigrationObject>();
        var pendingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingList = new List<MigrationObject>();
        var strictFailures = 0;

        await foreach (var raw in source.ScanAsync(runOptions.IncludeDeleted, cancellationToken).ConfigureAwait(false))
        {
            scanned++;
            ordinal++;

            var document = raw.Clone();
            MigrationFieldTranslation.ApplyToLogical(document, store.FieldMap);
            var context = CreateContext(collectionName, definition.Name, runId, ordinal, remaps, runOptions.StrictPathResolution);

            foreach (var operation in plan.Operations)
            {
                operation.Apply(document, context);

                if (context.SkipDocument)
                {
                    break;
                }
            }

            samples.AddRange(context.InvalidValueSamples);
            generated += context.GeneratedIdMappings;
            repaired += context.RepairedReferences;
            CollectRemaps(context, remaps);
            CollectPending(context, pendingIds, pendingList);
            strictFailures += context.StrictPathFailures.Count;

            if (context.SkipDocument)
            {
                continue;
            }

            var targetId = MigrationOperationSupport.IdText(document.TryGetValue(MigrationIds.IdField, out var idValue) ? idValue : null);

            if (targetId != null && !targetIds.Add(targetId))
            {
                duplicates.Add(new DuplicateTargetIdSample(collectionName, targetId, ordinal));
                continue;
            }

            inserted++;

            if (!runOptions.DryRun)
            {
                var physical = document.Clone();
                MigrationFieldTranslation.ApplyToPhysical(physical, store.FieldMap);

                if (store.Capabilities.SupportsBatchInsert)
                {
                    batchDocs.Add(physical);
                }
                else
                {
                    await shadow.InsertAsync(physical, cancellationToken).ConfigureAwait(false);
                }

                await PersistRemapsAsync(context, remaps, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!runOptions.DryRun && batchDocs.Count > 0)
        {
            await shadow.InsertManyAsync(AsAsyncEnumerable(batchDocs, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        if (!runOptions.DryRun)
        {
            await InsertPendingAsync(shadow, pendingList, cancellationToken).ConfigureAwait(false);
        }

        var indexDefinitions = store.Capabilities.SupportsIndexEnumeration ? source.GetIndexes() : Array.Empty<MigrationIndexDefinition>();
        var replayedIndexes = new List<MigrationIndexDefinition>();

        var disposition = runOptions.DryRun
            ? runOptions.BackupRetention == BackupRetentionPolicy.DeleteOnSuccess ? BackupDisposition.PlannedDeleteOnSuccess : BackupDisposition.PlannedRetain
            : BackupDisposition.None;

        if (!runOptions.DryRun)
        {
            if (!store.Capabilities.SupportsRenameCollection)
            {
                throw new NotSupportedException($"Migration '{definition.Name}' requires a collection swap, but '{store.GetType().Name}' cannot rename collections.");
            }

            await store.RenameCollectionAsync(collectionName, backupName, cancellationToken).ConfigureAwait(false);
            await store.RenameCollectionAsync(shadowName, collectionName, cancellationToken).ConfigureAwait(false);

            if (indexDefinitions.Count > 0)
            {
                var target = store.GetCollection(collectionName);

                foreach (var index in indexDefinitions)
                {
                    await target.EnsureIndexAsync(index, cancellationToken).ConfigureAwait(false);
                    replayedIndexes.Add(index);
                }
            }

            if (runOptions.BackupRetention == BackupRetentionPolicy.DeleteOnSuccess)
            {
                await store.DropCollectionAsync(backupName, cancellationToken).ConfigureAwait(false);
                disposition = BackupDisposition.DeletedOnSuccess;
            }
            else
            {
                disposition = BackupDisposition.Retained;
            }
        }

        if (runOptions.StrictPathResolution && runOptions.ThrowOnStrictPathFailure && strictFailures > 0)
        {
            throw new InvalidOperationException($"Migration '{definition.Name}' found {strictFailures} strict path resolution failure(s) in collection '{collectionName}'.");
        }

        var validation = new RebuildValidationSummary(scanned, inserted + duplicates.Count, inserted, duplicates.Count);
        return new CollectionMigrationResult(collectionName, scanned, 0, 0, inserted, generated, repaired, samples.Count, samples, validation, backupName, disposition, duplicates, replayedIndexes, strictFailures);
    }

    private static DocumentMigrationExecutionContext CreateContext(string collectionName, string migrationName, string runId, int ordinal, RemapLookup remaps, bool strictPathResolution)
        => new(collectionName, migrationName, runId, strictPathResolution) { DocumentOrdinal = ordinal, Remaps = remaps };

    private static void CollectRemaps(DocumentMigrationExecutionContext context, RemapLookup remaps)
    {
        foreach (var remap in context.IdRemaps)
        {
            remaps.Add(remap.SourceCollection, remap.MigrationName, remap.OldId, remap.NewObjectId);
        }
    }

    private static void CollectPending(DocumentMigrationExecutionContext context, HashSet<string> ids, List<MigrationObject> pending)
    {
        foreach (var document in context.PendingInserts)
        {
            var id = MigrationOperationSupport.IdText(document.TryGetValue(MigrationIds.IdField, out var value) ? value : null);

            if (id == null || ids.Add(id))
            {
                pending.Add(document);
            }
        }
    }

    private static async IAsyncEnumerable<MigrationObject> AsAsyncEnumerable(IEnumerable<MigrationObject> documents, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return document;
        }

        await Task.CompletedTask;
    }

    private async Task InsertPendingAsync(IMigrationCollection collection, IReadOnlyList<MigrationObject> pending, CancellationToken cancellationToken)
    {
        foreach (var document in pending)
        {
            await collection.InsertAsync(document.Clone(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PersistRemapsAsync(DocumentMigrationExecutionContext context, RemapLookup remaps, CancellationToken cancellationToken)
    {
        if (context.IdRemaps.Count == 0)
        {
            return;
        }

        var collection = store.GetCollection(JournalStore.IdMappingCollection);

        foreach (var remap in context.IdRemaps)
        {
            var document = new MigrationObject();
            document.Set("MigrationName", MigrationValue.From(remap.MigrationName));
            document.Set("RunId", MigrationValue.From(remap.RunId));
            document.Set("SourceCollection", MigrationValue.From(remap.SourceCollection));
            document.Set("OldId", MigrationValue.From(remap.OldId));
            document.Set("NewObjectId", MigrationValue.From(remap.NewObjectId));
            document.Set("Policy", MigrationValue.From(remap.Policy.ToString()));
            document.Set("DocumentOrdinal", MigrationValue.From(remap.DocumentOrdinal));
            await collection.InsertAsync(document, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<RemapLookup> LoadRemapsAsync(CancellationToken cancellationToken)
    {
        var lookup = new RemapLookup();

        if (!store.CollectionExists(JournalStore.IdMappingCollection))
        {
            return lookup;
        }

        var collection = store.GetCollection(JournalStore.IdMappingCollection);

        await foreach (var document in collection.ScanAsync(includeDeleted: true, cancellationToken).ConfigureAwait(false))
        {
            var sourceCollection = document.TryGetValue("SourceCollection", out var c) ? c.AsString : null;
            var migrationName = document.TryGetValue("MigrationName", out var m) ? m.AsString : null;
            var oldId = document.TryGetValue("OldId", out var o) ? o.AsString : null;
            var newId = document.TryGetValue("NewObjectId", out var n) ? n.AsString : null;

            if (sourceCollection != null && migrationName != null && oldId != null && newId != null)
            {
                lookup.Add(sourceCollection, migrationName, oldId, newId);
            }
        }

        return lookup;
    }

    private List<string> ResolveCollections(string selector)
    {
        if (selector != "*" && !selector.Contains('*') && !selector.Contains('?'))
        {
            return store.CollectionExists(selector) ? new List<string> { selector } : new List<string>();
        }

        var all = new List<string>();

        foreach (var name in store.GetCollectionsAsync().ToBlockingEnumerable())
        {
            if (IsExcluded(name))
            {
                continue;
            }

            if (selector == "*" || MatchesGlob(selector, name))
            {
                all.Add(name);
            }
        }

        return all;
    }

    private static bool IsExcluded(string name)
    {
        return name.StartsWith("__", StringComparison.Ordinal)
               || name.StartsWith("$", StringComparison.Ordinal)
               || name.Contains("__backup__", StringComparison.Ordinal)
               || name.Contains("__migrating__", StringComparison.Ordinal);
    }

    private static bool MatchesGlob(string pattern, string value)
    {
        var p = 0;
        var v = 0;
        var star = -1;
        var match = 0;

        while (v < value.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == value[v]))
            {
                p++;
                v++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p;
                match = v;
                p++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                match++;
                v = match;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    private static void Report(MigrationRunOptions options, MigrationProgressStage stage, string migrationName, string selector, string collectionName, bool dryRun, int completedCollections, int totalCollections, int scanned, int modified, int removed, int inserted)
    {
        options.ProgressCallback?.Invoke(new MigrationProgress(stage, migrationName, selector, collectionName, dryRun, completedCollections, totalCollections, scanned, modified, removed, inserted));
    }
}

internal sealed class MigrationDefinition
{
    public MigrationDefinition(string name, IReadOnlyList<CollectionMigrationPlan> collections)
    {
        Name = name;
        Collections = collections;
    }

    public string Name { get; }

    public IReadOnlyList<CollectionMigrationPlan> Collections { get; }
}
