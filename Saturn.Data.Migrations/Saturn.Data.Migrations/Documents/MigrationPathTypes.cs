namespace GoLive.Saturn.Data.Migrations;

public static class MigrationIds
{
    public const string IdField = "_id";
}

public enum FieldWriteMode
{
    MissingOnly,
    ExistingOnly,
    NullOrMissing,
    Overwrite
}

internal enum CleanupScope
{
    TopLevel,
    Recursive
}

internal enum MigrationPathResolutionFailure
{
    None,
    InvalidPath,
    MissingIntermediate,
    MissingLeaf,
    ExpectedDocument,
    ExpectedArray,
    UnsupportedPattern
}

internal enum MigrationPathSegmentKind
{
    Property,
    ArrayIndex,
    ArrayWildcard,
    RecursiveDescent
}

internal readonly struct MigrationPathSegment
{
    private MigrationPathSegment(MigrationPathSegmentKind kind, string propertyName, int arrayIndex)
    {
        Kind = kind;
        PropertyName = propertyName;
        ArrayIndex = arrayIndex;
    }

    public MigrationPathSegmentKind Kind { get; }

    public string PropertyName { get; }

    public int ArrayIndex { get; }

    public static MigrationPathSegment ForProperty(string propertyName) => new(MigrationPathSegmentKind.Property, propertyName, -1);

    public static MigrationPathSegment ForArrayIndex(int arrayIndex) => new(MigrationPathSegmentKind.ArrayIndex, null, arrayIndex);

    public static MigrationPathSegment ForArrayWildcard() => new(MigrationPathSegmentKind.ArrayWildcard, null, -1);

    public static MigrationPathSegment ForRecursiveDescent() => new(MigrationPathSegmentKind.RecursiveDescent, null, -1);
}

internal sealed class MigrationPathTarget
{
    private MigrationPathTarget(MigrationObject documentParent, string fieldName, MigrationArray arrayParent, int arrayIndex, bool exists, bool canWrite, MigrationValue value)
    {
        DocumentParent = documentParent;
        FieldName = fieldName ?? string.Empty;
        ArrayParent = arrayParent;
        ArrayIndex = arrayIndex;
        Exists = exists;
        CanWrite = canWrite;
        Value = value ?? MigrationValue.Null;
    }

    public MigrationObject DocumentParent { get; }

    public string FieldName { get; }

    public MigrationArray ArrayParent { get; }

    public int ArrayIndex { get; }

    public bool Exists { get; }

    public bool CanWrite { get; }

    public MigrationValue Value { get; }

    public bool IsArrayIndex => ArrayParent != null;

    public static MigrationPathTarget ForProperty(MigrationObject documentParent, string fieldName, bool exists, MigrationValue value)
        => new(documentParent, fieldName, null, -1, exists, documentParent != null, value);

    public static MigrationPathTarget ForArrayIndex(MigrationArray arrayParent, int arrayIndex, bool exists, MigrationValue value)
        => new(null, string.Empty, arrayParent, arrayIndex, exists, true, value);
}
