using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Migrations;

internal interface IMigrationOperation
{
    bool RequiresRebuild { get; }

    bool Apply(MigrationObject document, DocumentMigrationExecutionContext context);
}

internal sealed class RemoveFieldWhenOperation : IMigrationOperation
{
    private readonly string path;
    private readonly MigrationPredicate predicate;
    private readonly bool recursive;

    public RemoveFieldWhenOperation(string path, MigrationPredicate predicate, bool recursive)
    {
        this.path = path;
        this.predicate = predicate;
        this.recursive = recursive;
    }

    public bool RequiresRebuild => false;

    public bool Apply(MigrationObject document, DocumentMigrationExecutionContext context)
    {
        var changed = false;

        foreach (var candidate in MigrationOperationSupport.Contexts(document, path, recursive, context))
        {
            if (!predicate(candidate) || string.Equals(candidate.Path, MigrationIds.IdField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (DocumentPathNavigator.TryRemove(document, candidate.Path, pruneEmptyParents: false))
            {
                changed = true;
            }
        }

        return changed;
    }
}

internal sealed class RemoveWhereOperation : IMigrationOperation
{
    private readonly MigrationPredicate predicate;
    private readonly bool recursive;

    public RemoveWhereOperation(MigrationPredicate predicate, bool recursive)
    {
        this.predicate = predicate;
        this.recursive = recursive;
    }

    public bool RequiresRebuild => false;

    public bool Apply(MigrationObject document, DocumentMigrationExecutionContext context)
    {
        var changed = false;

        foreach (var candidate in MigrationOperationSupport.CleanupContexts(document, recursive, context))
        {
            if (!predicate(candidate) || string.Equals(candidate.Path, MigrationIds.IdField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (DocumentPathNavigator.TryRemove(document, candidate.Path, pruneEmptyParents: false))
            {
                changed = true;
            }
        }

        return changed;
    }
}

internal sealed class AddFieldWhenOperation : IMigrationOperation
{
    private readonly string path;
    private readonly MigrationValue value;
    private readonly MigrationPredicate predicate;

    public AddFieldWhenOperation(string path, MigrationValue value, MigrationPredicate predicate)
    {
        this.path = path;
        this.value = value;
        this.predicate = predicate;
    }

    public bool RequiresRebuild => false;

    public bool Apply(MigrationObject document, DocumentMigrationExecutionContext context)
    {
        var changed = false;

        foreach (var candidate in MigrationOperationSupport.Contexts(document, path, recursive: false, context))
        {
            if (!predicate(candidate))
            {
                continue;
            }

            if (DocumentPathNavigator.TryAdd(document, candidate.Path, value, FieldWriteMode.MissingOnly, createParents: true))
            {
                changed = true;
            }
        }

        return changed;
    }
}

internal sealed class SetFieldWhenOperation : IMigrationOperation
{
    private readonly string path;
    private readonly MigrationValueFactory factory;
    private readonly MigrationPredicate predicate;

    public SetFieldWhenOperation(string path, MigrationValueFactory factory, MigrationPredicate predicate)
    {
        this.path = path;
        this.factory = factory;
        this.predicate = predicate;
    }

    public bool RequiresRebuild => false;

    public bool Apply(MigrationObject document, DocumentMigrationExecutionContext context)
    {
        var changed = false;

        foreach (var candidate in MigrationOperationSupport.Contexts(document, path, recursive: false, context))
        {
            if (!predicate(candidate))
            {
                continue;
            }

            if (DocumentPathNavigator.TryAdd(document, candidate.Path, factory(candidate), FieldWriteMode.Overwrite, createParents: true))
            {
                changed = true;
            }
        }

        return changed;
    }
}

internal sealed class ModifyFieldWhenOperation : IMigrationOperation
{
    private readonly string path;
    private readonly MigrationValueMutator mutator;
    private readonly MigrationPredicate predicate;

    public ModifyFieldWhenOperation(string path, MigrationValueMutator mutator, MigrationPredicate predicate)
    {
        this.path = path;
        this.mutator = mutator;
        this.predicate = predicate;
    }

    public bool RequiresRebuild => false;

    public bool Apply(MigrationObject document, DocumentMigrationExecutionContext context)
    {
        var changed = false;

        foreach (var candidate in MigrationOperationSupport.Contexts(document, path, recursive: false, context))
        {
            if (!predicate(candidate))
            {
                continue;
            }

            if (DocumentPathNavigator.TryReplace(document, candidate.Path, mutator(candidate)))
            {
                changed = true;
            }
        }

        return changed;
    }
}

internal sealed class PruneEmptyContainersOperation : IMigrationOperation
{
    private readonly bool recursive;

    public PruneEmptyContainersOperation(bool recursive)
    {
        this.recursive = recursive;
    }

    public bool RequiresRebuild => false;

    public bool Apply(MigrationObject document, DocumentMigrationExecutionContext context)
    {
        var changed = false;

        foreach (var candidate in MigrationOperationSupport.CleanupContexts(document, recursive, context))
        {
            if (string.Equals(candidate.Path, MigrationIds.IdField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = candidate.Value;

            if (value == null || value.IsNull)
            {
                continue;
            }

            var empty = (value.IsObject && value.AsObject().Count == 0) || (value.IsArray && value.AsArray().Count == 0);

            if (empty && DocumentPathNavigator.TryRemove(document, candidate.Path, pruneEmptyParents: false))
            {
                changed = true;
            }
        }

        return changed;
    }
}

internal sealed class ConvertFieldOperation : IMigrationOperation
{
    private readonly string path;

    public ConvertFieldOperation(string path, InvalidObjectIdPolicy policy)
    {
        this.path = path;
        Policy = policy;
    }

    public InvalidObjectIdPolicy Policy { get; internal set; }

    public bool RequiresRebuild => false;

    public bool Apply(MigrationObject document, DocumentMigrationExecutionContext context)
    {
        var changed = false;

        foreach (var candidate in MigrationOperationSupport.Contexts(document, path, recursive: false, context))
        {
            if (!candidate.Exists)
            {
                continue;
            }

            var outcome = MigrationOperationSupport.ConvertObjectId(candidate.Value, Policy, context, candidate.Path);

            if (outcome.Remove)
            {
                if (DocumentPathNavigator.TryRemove(document, candidate.Path, pruneEmptyParents: false))
                {
                    changed = true;
                }

                continue;
            }

            if (outcome.Changed && DocumentPathNavigator.TryReplace(document, candidate.Path, outcome.Value))
            {
                changed = true;
            }

            if (context.SkipDocument)
            {
                return changed;
            }
        }

        return changed;
    }
}

internal sealed class ConvertIdOperation : IMigrationOperation
{
    public ConvertIdOperation(InvalidObjectIdPolicy policy)
    {
        if (policy is InvalidObjectIdPolicy.LeaveUnchanged or InvalidObjectIdPolicy.RemoveField)
        {
            throw new ArgumentException($"Policy '{policy}' is not valid for an id conversion.", nameof(policy));
        }

        Policy = policy;
    }

    public InvalidObjectIdPolicy Policy { get; internal set; }

    public bool RequiresRebuild => true;

    public bool Apply(MigrationObject document, DocumentMigrationExecutionContext context)
    {
        if (!document.TryGetValue(MigrationIds.IdField, out var current) || current == null || current.IsNull)
        {
            return false;
        }

        var outcome = MigrationOperationSupport.ConvertObjectId(current, Policy, context, MigrationIds.IdField);

        if (!outcome.Changed)
        {
            return false;
        }

        document.Set(MigrationIds.IdField, outcome.Value);
        return true;
    }
}

internal static class MigrationOperationSupport
{
    public static IEnumerable<MigrationPredicateContext> Contexts(MigrationObject document, string path, bool recursive, DocumentMigrationExecutionContext context)
    {
        return recursive
            ? DocumentPathNavigator.CreateCleanupContexts(document, CleanupScope.Recursive, context.Collection, context.MigrationName)
            : DocumentPathNavigator.CreateContexts(document, path, includeLeafWhenMissing: true, context.Collection, context.MigrationName);
    }

    public static IEnumerable<MigrationPredicateContext> CleanupContexts(MigrationObject document, bool recursive, DocumentMigrationExecutionContext context)
    {
        return DocumentPathNavigator.CreateCleanupContexts(document, recursive ? CleanupScope.Recursive : CleanupScope.TopLevel, context.Collection, context.MigrationName);
    }

    public static ConversionOutcome ConvertObjectId(MigrationValue value, InvalidObjectIdPolicy policy, DocumentMigrationExecutionContext context, string path)
    {
        if (value == null || value.IsNull || value.Kind == MigrationValueKind.ObjectId || !value.IsString)
        {
            return ConversionOutcome.Unchanged;
        }

        var raw = value.AsString;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return ConversionOutcome.Unchanged;
        }

        if (MigrationObjectId.TryParse(raw, out var objectId))
        {
            return new ConversionOutcome(true, false, MigrationValue.From(objectId));
        }

        context.RecordInvalidValue(path, raw, policy.ToString());

        switch (policy)
        {
            case InvalidObjectIdPolicy.Fail:
                throw new InvalidOperationException($"Invalid object id '{raw}' at '{path}' in collection '{context.Collection}'.");
            case InvalidObjectIdPolicy.SkipDocument:
                context.SkipDocument = true;
                return ConversionOutcome.Unchanged;
            case InvalidObjectIdPolicy.RemoveField:
                return new ConversionOutcome(false, true, MigrationValue.Null);
            case InvalidObjectIdPolicy.GenerateNewId:
                var generated = MigrationObjectId.NewObjectId();
                context.RecordGeneratedId(raw, generated.ToString(), policy);
                return new ConversionOutcome(true, false, MigrationValue.From(generated));
            default:
                return ConversionOutcome.Unchanged;
        }
    }
}

internal readonly struct ConversionOutcome
{
    public ConversionOutcome(bool changed, bool remove, MigrationValue value)
    {
        Changed = changed;
        Remove = remove;
        Value = value;
    }

    public bool Changed { get; }
    public bool Remove { get; }
    public MigrationValue Value { get; }

    public static ConversionOutcome Unchanged => new(false, false, MigrationValue.Null);
}
