using System.Collections.Generic;

namespace GoLive.Saturn.Data.Migrations;

public static class MigrationFieldAliases
{
    public static IMigrationFieldMap Identity => IdentityMigrationFieldMap.Instance;

    public static IMigrationFieldMap LiteDbx { get; } = new MigrationFieldMap(new[]
    {
        new KeyValuePair<string, string>("Properties", "_p")
    });

    public static IMigrationFieldMap MongoDb { get; } = new MigrationFieldMap(new[]
    {
        new KeyValuePair<string, string>("Properties", "_p"),
        new KeyValuePair<string, string>("Version", "_v")
    });
}
