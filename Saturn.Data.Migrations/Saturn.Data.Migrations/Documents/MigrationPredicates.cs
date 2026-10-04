using System;
using System.Linq;

namespace GoLive.Saturn.Data.Migrations;

public delegate bool MigrationPredicate(MigrationPredicateContext context);

public delegate MigrationValue MigrationValueFactory(MigrationPredicateContext context);

public delegate MigrationValue MigrationValueMutator(MigrationPredicateContext context);

public static class MigrationPredicates
{
    public static readonly MigrationPredicate Always = _ => true;
    public static readonly MigrationPredicate Missing = context => context.Exists == false;
    public static readonly MigrationPredicate Null = context => context.Exists && context.Value.IsNull;
    public static readonly MigrationPredicate NullOrMissing = context => context.Exists == false || context.Value.IsNull;
    public static readonly MigrationPredicate EmptyArray = context => context.Exists && context.Value.IsArray && context.Value.AsArray().Count == 0;
    public static readonly MigrationPredicate EmptyDocument = context => context.Exists && context.Value.IsObject && context.Value.AsObject().Count == 0;
    public static readonly MigrationPredicate EmptyString = context => context.Exists && context.Value.IsString && context.Value.AsString.Length == 0;
    public static readonly MigrationPredicate WhiteSpaceString = context => context.Exists && context.Value.IsString && string.IsNullOrWhiteSpace(context.Value.AsString);
    public static readonly MigrationPredicate TrimmedEmptyString = context => context.Exists && context.Value.IsString && context.Value.AsString.Trim().Length == 0;
    public static readonly MigrationPredicate NullOrWhiteSpaceString = Or(NullOrMissing, WhiteSpaceString);
    public static readonly MigrationPredicate IsString = context => context.Exists && context.Value.IsString;
    public static readonly MigrationPredicate IsArray = context => context.Exists && context.Value.IsArray;
    public static readonly MigrationPredicate IsObject = context => context.Exists && context.Value.IsObject;
    public static readonly MigrationPredicate IsObjectId = context => context.Exists && context.Value.IsObjectId;
    public static readonly MigrationPredicate IsGuid = context => context.Exists && context.Value.IsGuid;
    public static readonly MigrationPredicate IsBoolean = context => context.Exists && context.Value.IsBoolean;
    public static readonly MigrationPredicate IsNumber = context => context.Exists && context.Value.IsNumber;
    public static readonly MigrationPredicate ZeroNumber = context => context.Exists && context.Value.IsNumber && context.Value.AsDecimal == 0m;
    public static readonly MigrationPredicate FalseBoolean = context => context.Exists && context.Value.IsBoolean && context.Value.AsBoolean == false;
    public static readonly MigrationPredicate MinValue = context => context.Exists && context.Value.Kind == MigrationValueKind.MinValue;
    public static readonly MigrationPredicate MaxValue = context => context.Exists && context.Value.Kind == MigrationValueKind.MaxValue;
    public static readonly MigrationPredicate EmptyBinary = context => context.Exists && context.Value.IsBinary && context.Value.AsBinary.Length == 0;
    public static readonly MigrationPredicate EmptyGuid = context => context.Exists && context.Value.IsGuid && context.Value.AsGuid == Guid.Empty;
    public static readonly MigrationPredicate EmptyObjectId = context => context.Exists && context.Value.IsObjectId && context.Value.AsObjectId == MigrationObjectId.Empty;
    public static readonly MigrationPredicate NullLike = Or(NullOrMissing, EmptyString, WhiteSpaceString);
    public static readonly MigrationPredicate StructurallyEmpty = Or(EmptyArray, EmptyDocument, EmptyBinary);
    public static readonly MigrationPredicate UselessValue = Or(NullLike, StructurallyEmpty, EmptyGuid, EmptyObjectId, MinValue, MaxValue);
    public static readonly MigrationPredicate UselessValueAggressive = Or(UselessValue, ZeroNumber, FalseBoolean);

    public static MigrationPredicate Default(MigrationValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return context => context.Exists && context.Value == value;
    }

    public static MigrationPredicate AnyOfDefaults(params MigrationValue[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length == 0)
        {
            return _ => false;
        }

        return context => context.Exists && values.Any(x => x == context.Value);
    }

    public static MigrationPredicate AllOf(params MigrationPredicate[] predicates) => And(predicates);

    public static MigrationPredicate AnyOf(params MigrationPredicate[] predicates) => Or(predicates);

    public static MigrationPredicate And(params MigrationPredicate[] predicates)
    {
        ArgumentNullException.ThrowIfNull(predicates);

        return context => predicates.All(predicate => predicate(context));
    }

    public static MigrationPredicate Or(params MigrationPredicate[] predicates)
    {
        ArgumentNullException.ThrowIfNull(predicates);

        return context => predicates.Any(predicate => predicate(context));
    }

    public static MigrationPredicate Not(MigrationPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return context => !predicate(context);
    }
}
