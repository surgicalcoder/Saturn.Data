using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Migrations;

public sealed class MigrationBuilder
{
    internal List<CollectionMigrationPlan> Plans { get; } = new();

    public MigrationBuilder ForCollection(string selector, Action<CollectionMigrationBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new CollectionMigrationBuilder();
        configure(builder);
        Plans.Add(new CollectionMigrationPlan(selector, builder.Operations));

        return this;
    }
}

internal sealed class CollectionMigrationPlan
{
    public CollectionMigrationPlan(string selector, IReadOnlyList<IMigrationOperation> operations)
    {
        Selector = selector;
        Operations = operations;
    }

    public string Selector { get; }

    public IReadOnlyList<IMigrationOperation> Operations { get; }
}

public sealed class CollectionMigrationBuilder
{
    internal List<IMigrationOperation> Operations { get; } = new();

    public CollectionMigrationBuilder RemoveFieldWhen(string path, MigrationPredicate predicate, bool recursive = false)
    {
        Operations.Add(new RemoveFieldWhenOperation(path, predicate, recursive));
        return this;
    }

    public CollectionMigrationBuilder RemoveWhere(MigrationPredicate predicate, bool recursive = true)
    {
        Operations.Add(new RemoveWhereOperation(predicate, recursive));
        return this;
    }

    public CollectionMigrationBuilder AddFieldWhen(string path, MigrationValue value, MigrationPredicate predicate)
    {
        Operations.Add(new AddFieldWhenOperation(path, value, predicate));
        return this;
    }

    public CollectionMigrationBuilder SetFieldWhen(string path, MigrationValueFactory factory, MigrationPredicate predicate)
    {
        Operations.Add(new SetFieldWhenOperation(path, factory, predicate));
        return this;
    }

    public CollectionMigrationBuilder ModifyFieldWhen(string path, MigrationValueMutator mutator, MigrationPredicate predicate)
    {
        Operations.Add(new ModifyFieldWhenOperation(path, mutator, predicate));
        return this;
    }

    public CollectionMigrationBuilder PruneEmptyContainers(bool recursive = true)
    {
        Operations.Add(new PruneEmptyContainersOperation(recursive));
        return this;
    }

    public FieldConversionBuilder ConvertField(string path) => new(this, path);

    public IdConversionBuilder ConvertId() => new(this);
}

public sealed class FieldConversionBuilder
{
    private readonly CollectionMigrationBuilder owner;
    private readonly ConvertFieldOperation operation;

    internal FieldConversionBuilder(CollectionMigrationBuilder owner, string path)
    {
        this.owner = owner;
        operation = new ConvertFieldOperation(path, InvalidObjectIdPolicy.Fail);
    }

    public FieldConversionBuilder FromStringToObjectId()
    {
        owner.Operations.Add(operation);
        return this;
    }

    public CollectionMigrationBuilder OnInvalidString(InvalidObjectIdPolicy policy)
    {
        operation.Policy = policy;
        return owner;
    }
}

public sealed class IdConversionBuilder
{
    private readonly CollectionMigrationBuilder owner;
    private readonly ConvertIdOperation operation;

    internal IdConversionBuilder(CollectionMigrationBuilder owner)
    {
        this.owner = owner;
        operation = new ConvertIdOperation(InvalidObjectIdPolicy.Fail);
    }

    public IdConversionBuilder FromStringToObjectId()
    {
        owner.Operations.Add(operation);
        return this;
    }

    public CollectionMigrationBuilder OnInvalidString(InvalidObjectIdPolicy policy)
    {
        operation.Policy = policy;
        return owner;
    }
}
