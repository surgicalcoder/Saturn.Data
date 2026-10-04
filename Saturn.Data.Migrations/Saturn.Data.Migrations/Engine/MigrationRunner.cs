using System;
using System.Collections.Generic;
using System.Linq;
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

    public MigrationRunner OnProgress(Action<MigrationProgress> callback)
    {
        options.ProgressCallback = callback;
        return this;
    }

    public ValueTask<MigrationReport> RunAsync(CancellationToken cancellationToken = default)
        => RunAsync(options, cancellationToken);

    public async ValueTask<MigrationReport> RunAsync(MigrationRunOptions runOptions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runOptions);

        var runId = Guid.NewGuid().ToString("N")[..8];
        var journal = new JournalStore(store);

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

            foreach (var (plan, matched) in work)
            {
                var collectionResults = new List<CollectionMigrationResult>();

                foreach (var collectionName in matched)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Report(runOptions, MigrationProgressStage.CollectionStarted, definition.Name, plan.Selector, collectionName, runOptions.DryRun, completedCollections, totalCollections, scanned, modified, removed, inserted);

                    var result = await ProcessCollectionAsync(definition, plan, collectionName, runId, runOptions, cancellationToken).ConfigureAwait(false);
                    collectionResults.Add(result);

                    scanned += result.DocumentsScanned;
                    modified += result.DocumentsModified;
                    removed += result.DocumentsRemoved;
                    inserted += result.DocumentsInserted;
                    generated += result.GeneratedIdMappings;
                    repaired += result.RepairedReferences;
                    invalid += result.InvalidValueCount;
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

            results.Add(new MigrationExecutionResult(definition.Name, runId, !runOptions.DryRun, runOptions.DryRun, selectorResults, scanned, modified, removed, inserted, generated, repaired, invalid));
        }

        return new MigrationReport(results);
    }

    private async Task<CollectionMigrationResult> ProcessCollectionAsync(MigrationDefinition definition, CollectionMigrationPlan plan, string collectionName, string runId, MigrationRunOptions runOptions, CancellationToken cancellationToken)
    {
        var requiresRebuild = plan.Operations.Any(operation => operation.RequiresRebuild);

        if (requiresRebuild)
        {
            if (!store.Capabilities.SupportsRebuild)
            {
                throw new NotSupportedException($"Migration '{definition.Name}' requires a collection rebuild, but '{store.GetType().Name}' does not support rebuilds.");
            }

            return await RebuildCollectionAsync(definition, plan, collectionName, runId, runOptions, cancellationToken).ConfigureAwait(false);
        }

        return await MigrateInPlaceAsync(definition, plan, collectionName, runId, runOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CollectionMigrationResult> MigrateInPlaceAsync(MigrationDefinition definition, CollectionMigrationPlan plan, string collectionName, string runId, MigrationRunOptions runOptions, CancellationToken cancellationToken)
    {
        var collection = store.GetCollection(collectionName);
        var samples = new List<InvalidValueSample>();
        var scanned = 0;
        var modified = 0;
        var generated = 0;
        var repaired = 0;
        var ordinal = 0;

        await foreach (var raw in collection.ScanAsync(runOptions.IncludeDeleted, cancellationToken).ConfigureAwait(false))
        {
            scanned++;
            ordinal++;

            var document = raw.Clone();
            var context = new DocumentMigrationExecutionContext(collectionName, definition.Name, runId, strictPathResolution: true) { DocumentOrdinal = ordinal };
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

            if (!changed)
            {
                continue;
            }

            modified++;

            if (!runOptions.DryRun)
            {
                await collection.UpdateAsync(document, cancellationToken).ConfigureAwait(false);
            }

            if (context.IdRemaps.Count > 0 && !runOptions.DryRun)
            {
                await PersistRemapsAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }

        return new CollectionMigrationResult(collectionName, scanned, modified, 0, 0, generated, repaired, samples.Count, samples, null, null, BackupDisposition.None);
    }

    private async Task<CollectionMigrationResult> RebuildCollectionAsync(MigrationDefinition definition, CollectionMigrationPlan plan, string collectionName, string runId, MigrationRunOptions runOptions, CancellationToken cancellationToken)
    {
        var source = store.GetCollection(collectionName);
        var shadowName = $"{collectionName}__migrating__{runId}";
        var backupName = $"{collectionName}__backup__{runId}";
        var shadow = store.GetCollection(shadowName);

        var samples = new List<InvalidValueSample>();
        var scanned = 0;
        var inserted = 0;
        var generated = 0;
        var repaired = 0;
        var ordinal = 0;

        await foreach (var raw in source.ScanAsync(runOptions.IncludeDeleted, cancellationToken).ConfigureAwait(false))
        {
            scanned++;
            ordinal++;

            var document = raw.Clone();
            var context = new DocumentMigrationExecutionContext(collectionName, definition.Name, runId, strictPathResolution: true) { DocumentOrdinal = ordinal };

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

            if (context.SkipDocument)
            {
                continue;
            }

            inserted++;

            if (!runOptions.DryRun)
            {
                await shadow.InsertAsync(document, cancellationToken).ConfigureAwait(false);
            }

            if (context.IdRemaps.Count > 0 && !runOptions.DryRun)
            {
                await PersistRemapsAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }

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

        var validation = new RebuildValidationSummary(scanned, inserted, inserted, 0);
        return new CollectionMigrationResult(collectionName, scanned, 0, 0, inserted, generated, repaired, samples.Count, samples, validation, backupName, disposition);
    }

    private async Task PersistRemapsAsync(DocumentMigrationExecutionContext context, CancellationToken cancellationToken)
    {
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

    private List<string> ResolveCollections(string selector)
    {
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
