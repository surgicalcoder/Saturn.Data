using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;

namespace Saturn.Data.DocumentDb.Tests;

public class Phase4MigrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private readonly UnitTestableDocumentDbRepository repository = fixture.Repository;

    [Fact]
    public async Task Migrations_RebuildAndCleanupLegacyJson()
    {
        var legacyId = "67a92d08063e3290f03b29dc";
        var scopeId = "65da55b75278b72ba0ffb2de";

        var store = repository.CreateMigrationStore();
        var collection = store.GetCollection("LegacyEntity");

        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From(legacyId));
        seed.Set("Scope", MigrationValue.From(scopeId));
        seed.Set("Name", MigrationValue.From("Ada"));
        seed.Set("Properties", new MigrationObject());
        seed.Set("Payload", MigrationValue.Null);
        await collection.InsertAsync(seed);

        var report = await store.Migrations()
            .Migration("20260410-IDMigration", m => m.ForCollection("LegacyEntity", c =>
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

        var migrated = await FirstAsync(collection);

        Assert.NotNull(migrated);
        Assert.Equal(legacyId, migrated["_id"].AsString);
        Assert.False(migrated.ContainsKey("Properties"));
        Assert.False(migrated.ContainsKey("Payload"));
        Assert.Equal(scopeId, migrated["Scope"].AsString);
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
