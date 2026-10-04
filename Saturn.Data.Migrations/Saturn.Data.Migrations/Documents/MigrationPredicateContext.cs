namespace GoLive.Saturn.Data.Migrations;

public sealed class MigrationPredicateContext
{
    public MigrationPredicateContext(MigrationObject root, string path, bool exists, MigrationValue value, string collection, string migrationName)
    {
        Root = root;
        Path = path;
        Exists = exists;
        Value = value ?? MigrationValue.Null;
        Collection = collection;
        MigrationName = migrationName;
    }

    public MigrationObject Root { get; }

    public string Path { get; }

    public bool Exists { get; }

    public MigrationValue Value { get; }

    public string Collection { get; }

    public string MigrationName { get; }

    public static MigrationPredicateContext Missing(MigrationObject root, string path, string collection, string migrationName)
        => new(root, path, false, MigrationValue.Null, collection, migrationName);
}
