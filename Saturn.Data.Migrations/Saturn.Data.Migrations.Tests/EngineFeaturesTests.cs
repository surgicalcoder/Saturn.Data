using System.Collections.Generic;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;

namespace Saturn.Data.Migrations.Tests;

public class EngineFeaturesTests
{
    [Fact]
    public void FieldTranslation_RoundTripsAliases()
    {
        var map = new MigrationFieldMap(new[]
        {
            new KeyValuePair<string, string>("Properties", "_p"),
            new KeyValuePair<string, string>("Version", "_v")
        });

        var physical = new MigrationObject();
        physical.Set("_p", new MigrationObject());
        physical.Set("_v", MigrationValue.From(3));
        physical.Set("Name", MigrationValue.From("Ada"));

        MigrationFieldTranslation.ApplyToLogical(physical, map);

        Assert.True(physical.ContainsKey("Properties"));
        Assert.True(physical.ContainsKey("Version"));
        Assert.False(physical.ContainsKey("_p"));
        Assert.Equal("Ada", physical["Name"].AsString);

        MigrationFieldTranslation.ApplyToPhysical(physical, map);

        Assert.True(physical.ContainsKey("_p"));
        Assert.True(physical.ContainsKey("_v"));
    }

    [Fact]
    public async Task Runner_AppliesFieldMapOnScan()
    {
        var map = new MigrationFieldMap(new[] { new KeyValuePair<string, string>("Properties", "_p") });
        var store = new InMemoryMigrationStore(map);

        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        seed.Set("_p", new MigrationObject());
        store.Seed("User", seed);

        await store.Migrations()
            .Migration("cleanup", m => m.ForCollection("User", c => c.RemoveFieldWhen("Properties", MigrationPredicates.EmptyDocument)))
            .RunAsync();

        var stored = await FirstAsync(store.GetCollection("User"));
        Assert.False(stored.ContainsKey("_p"));
        Assert.False(stored.ContainsKey("Properties"));
    }

    [Fact]
    public async Task RepairReference_RewritesOldIdsFromPriorMigration()
    {
        var store = new InMemoryMigrationStore();

        var customer = new MigrationObject();
        customer.Set("_id", MigrationValue.From("bad-id"));
        store.Seed("Customer", customer);

        var order = new MigrationObject();
        order.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        order.Set("Customer", MigrationValue.From("bad-id"));
        store.Seed("Order", order);

        await store.Migrations()
            .Migration("ids", m => m.ForCollection("Customer", c =>
                c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId)))
            .RunAsync();

        var migratedCustomer = await FirstAsync(store.GetCollection("Customer"));
        var newId = migratedCustomer["_id"].AsObjectId.ToString();

        var report = await store.Migrations()
            .Migration("repair", m => m.ForCollection("Order", c =>
                c.RepairReference("Customer").FromCollection("Customer").FromMigration("ids").Apply()))
            .RunAsync();

        var execution = Assert.Single(report.Migrations);
        Assert.Equal(1, execution.RepairedReferences);

        var migratedOrder = await FirstAsync(store.GetCollection("Order"));
        Assert.Equal(newId, migratedOrder["Customer"].AsString);
    }

    [Fact]
    public async Task BackupCleanup_KeepsConfiguredCount()
    {
        var store = new InMemoryMigrationStore();
        store.Seed("User__backup__aaaa");
        store.Seed("User__backup__bbbb");
        store.Seed("User__backup__cccc");

        var report = await store.Migrations().CleanupBackupsAsync(new BackupCleanupOptions { KeepLatestCount = 2 });

        Assert.Single(report.Dropped);
        Assert.Equal(2, report.Retained.Count);
    }

    [Fact]
    public async Task Rebuild_DetectsDuplicateTargetIds()
    {
        var store = new InMemoryMigrationStore();
        var first = new MigrationObject();
        first.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        var second = new MigrationObject();
        second.Set("_id", MigrationValue.From("67A92D08063E3290F03B29DC"));
        store.Seed("User", first, second);

        var report = await store.Migrations()
            .Migration("ids", m => m.ForCollection("User", c =>
                c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId)))
            .RunAsync();

        var execution = Assert.Single(report.Migrations);
        var collection = Assert.Single(execution.Selectors).Collections[0];
        Assert.Single(collection.DuplicateTargetIdSamples);
        Assert.Equal(1, collection.DocumentsInserted);
    }

    [Fact]
    public async Task InsertDocumentWhen_InsertsTemplateOnce()
    {
        var store = new InMemoryMigrationStore();
        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        store.Seed("User", seed);

        var template = new MigrationObject();
        template.Set("_id", MigrationValue.From("66a5032dfa0b29da8989b525"));
        template.Set("Name", MigrationValue.From("seeded"));

        await store.Migrations()
            .Migration("seed", m => m.ForCollection("User", c => c.InsertDocumentWhen(template, MigrationPredicates.Always)))
            .RunAsync();

        var count = 0;
        await foreach (var _ in store.GetCollection("User").ScanAsync(includeDeleted: true))
        {
            count++;
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task StrictPathResolution_FailsOnMissingPath()
    {
        var store = new InMemoryMigrationStore();
        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        store.Seed("User", seed);

        await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
            await store.Migrations()
                .Migration("strict", m => m.ForCollection("User", c => c.RemoveFieldWhen("Missing.Path", MigrationPredicates.Always)))
                .WithStrictPathResolution()
                .RunAsync());
    }

    [Fact]
    public async Task StrictPathResolution_DisabledDoesNotFail()
    {
        var store = new InMemoryMigrationStore();
        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From("67a92d08063e3290f03b29dc"));
        store.Seed("User", seed);

        var report = await store.Migrations()
            .Migration("lenient", m => m.ForCollection("User", c => c.RemoveFieldWhen("Missing.Path", MigrationPredicates.Always)))
            .RunAsync();

        var execution = Assert.Single(report.Migrations);
        Assert.Equal(0, execution.StrictPathFailureCount);
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
