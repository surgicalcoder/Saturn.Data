namespace GoLive.Saturn.Data.Migrations;

public static class MigrationStoreExtensions
{
    public static MigrationRunner Migrations(this IMigrationStore store) => new(store);
}
