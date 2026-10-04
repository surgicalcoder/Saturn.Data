using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;

namespace Saturn.Data.Migrations.Tests;

public class MigrationRunnerTests
{
    [Fact]
    public async Task LunarFlareMigration_ConvertsIdsAndRemovesLegacyFields()
    {
        var store = new InMemoryMigrationStore();
        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        seed.Set("Scope", MigrationValue.From("65da55b75278b72ba0ffb2de"));
        seed.Set("Name", MigrationValue.From("Ada"));
        seed.Set("Properties", new MigrationObject());
        seed.Set("Payload", MigrationValue.Null);
        store.Seed("User", seed);

        var report = await store.Migrations()
            .Migration("20260410-IDMigration", m => m.ForCollection("*", c =>
            {
                c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId);
                c.ConvertField("Scope").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
                c.RemoveFieldWhen("Properties", MigrationPredicates.EmptyDocument);
                c.RemoveFieldWhen("Payload", MigrationPredicates.Null);
            }))
            .WithBackupRetention(BackupRetentionPolicy.DeleteOnSuccess)
            .RunAsync();

        var execution = Assert.Single(report.Migrations);
        Assert.True(execution.WasApplied);
        Assert.Equal(1, execution.DocumentsInserted);
        Assert.Equal(1, execution.DocumentsScanned);

        var migrated = await FirstAsync(store.GetCollection("User"));
        Assert.Equal(MigrationValueKind.ObjectId, migrated["_id"].Kind);
        Assert.Equal("67a92d08063e3290f03b29dc", migrated["_id"].AsObjectId.ToString());
        Assert.Equal(MigrationValueKind.ObjectId, migrated["Scope"].Kind);
        Assert.False(migrated.ContainsKey("Properties"));
        Assert.False(migrated.ContainsKey("Payload"));

        Assert.False(store.CollectionExists("User__backup__" + execution.RunId));
    }

    [Fact]
    public async Task Migration_IsIdempotentAcrossRuns()
    {
        var store = new InMemoryMigrationStore();
        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        seed.Set("Payload", MigrationValue.Null);
        store.Seed("User", seed);

        await store.Migrations()
            .Migration("cleanup", m => m.ForCollection("*", c => c.RemoveFieldWhen("Payload", MigrationPredicates.Null)))
            .RunAsync();

        var report = await store.Migrations()
            .Migration("cleanup", m => m.ForCollection("*", c => c.RemoveFieldWhen("Payload", MigrationPredicates.Null)))
            .RunAsync();

        var execution = Assert.Single(report.Migrations);
        Assert.True(execution.WasSkipped);
        Assert.False(execution.WasApplied);
    }

    [Fact]
    public async Task ConvertId_GeneratesNewIdAndRecordsRemap()
    {
        var store = new InMemoryMigrationStore();
        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From("bad-id"));
        store.Seed("User", seed);

        var report = await store.Migrations()
            .Migration("ids", m => m.ForCollection("*", c => c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId)))
            .RunAsync();

        var execution = Assert.Single(report.Migrations);
        var migrated = await FirstAsync(store.GetCollection("User"));
        Assert.Equal(1, execution.GeneratedIdMappings);

        Assert.Equal(MigrationValueKind.ObjectId, migrated["_id"].Kind);

        var remaps = new List<MigrationObject>();
        await foreach (var remap in store.GetCollection("__saturn_migration_id_mappings").ScanAsync(true))
        {
            remaps.Add(remap);
        }

        Assert.Single(remaps);
        Assert.Equal("bad-id", remaps[0]["OldId"].AsString);
    }

    [Fact]
    public async Task DryRun_DoesNotPersistChanges()
    {
        var store = new InMemoryMigrationStore();
        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        seed.Set("Payload", MigrationValue.Null);
        store.Seed("User", seed);

        var report = await store.Migrations()
            .Migration("cleanup", m => m.ForCollection("*", c => c.RemoveFieldWhen("Payload", MigrationPredicates.Null)))
            .WithDryRun()
            .RunAsync();

        var execution = Assert.Single(report.Migrations);
        Assert.True(execution.IsDryRun);
        Assert.Equal(1, execution.DocumentsModified);

        var unchanged = await FirstAsync(store.GetCollection("User"));
        Assert.True(unchanged.ContainsKey("Payload"));
        Assert.False(store.CollectionExists("__saturn_migrations"));
    }

    [Fact]
    public async Task RemoveWhere_AggressiveCleanup_RemovesUselessValues()
    {
        var store = new InMemoryMigrationStore();
        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        seed.Set("Empty", new MigrationObject());
        seed.Set("List", new MigrationArray());
        seed.Set("Zero", MigrationValue.From(0));
        seed.Set("Keep", MigrationValue.From("value"));
        store.Seed("User", seed);

        await store.Migrations()
            .Migration("aggressive", m => m.ForCollection("*", c =>
                c.RemoveWhere(MigrationPredicates.UselessValueAggressive, recursive: true)))
            .RunAsync();

        var migrated = await FirstAsync(store.GetCollection("User"));
        Assert.False(migrated.ContainsKey("Empty"));
        Assert.False(migrated.ContainsKey("List"));
        Assert.False(migrated.ContainsKey("Zero"));
        Assert.Equal("value", migrated["Keep"].AsString);
    }

    private static async Task<MigrationObject> FirstAsync(IMigrationCollection collection)
    {
        await foreach (var document in collection.ScanAsync(includeDeleted: true))
        {
            return document;
        }

        return null;
    }
}
