using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;

namespace Saturn.Data.Sqlite.Tests;

public class Phase3MigrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private readonly UnitTestableSqliteRepository repository = fixture.Repository;

    [Fact]
    public async Task Migrations_NormalizeLegacyJsonDocuments()
    {
        var legacyId = "67a92d08063e3290f03b29dc";
        var scopeId = "65da55b75278b72ba0ffb2de";

        var store = repository.CreateMigrationStore();
        var seed = new MigrationObject();
        seed.Set("_id", MigrationValue.From(legacyId));
        seed.Set("Scope", MigrationValue.From(scopeId));
        seed.Set("Name", MigrationValue.From("Ada"));
        seed.Set("Properties", new MigrationObject());
        seed.Set("Payload", MigrationValue.Null);
        await store.GetCollection("LegacyEntity").InsertAsync(seed);

        await store.Migrations()
            .Migration("20260410-IDMigration", m => m.ForCollection("*", c =>
            {
                c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId);
                c.ConvertField("Scope").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
                c.RemoveFieldWhen("Properties", MigrationPredicates.EmptyDocument);
                c.RemoveFieldWhen("Payload", MigrationPredicates.Null);
            }))
            .WithBackupRetention(BackupRetentionPolicy.DeleteOnSuccess)
            .RunAsync();

        var json = await repository.ReadDocumentAsync("LegacyEntity", legacyId);

        Assert.NotNull(json);
        Assert.DoesNotContain("\"Properties\"", json);
        Assert.DoesNotContain("\"Payload\"", json);
        Assert.Contains($"\"Id\":\"{legacyId}\"", json);
        Assert.Contains($"\"Scope\":\"{scopeId}\"", json);
    }
}
