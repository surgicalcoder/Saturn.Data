using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;
using Saturn.Data.MongoDb.Tests.Entities;

namespace Saturn.Data.MongoDb.Tests;

public class Phase2MigrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private readonly UnitTestableMongoDbRepository repository = fixture.Repository;

    [Fact]
    public async Task Migrations_ConvertLegacyStringIdsOnMongo()
    {
        var legacyId = "67a92d08063e3290f03b29dc";
        var scopeId = "65da55b75278b72ba0ffb2de";
        var raw = repository.GetRawCollection<BasicEntity>();

        await raw.InsertOneAsync(new BsonDocument
        {
            ["_id"] = legacyId,
            ["Scope"] = scopeId,
            ["Name"] = "Ada",
            ["Properties"] = new BsonDocument(),
            ["Payload"] = BsonNull.Value
        });

        await repository.CreateMigrationStore().Migrations()
            .Migration("20260410-IDMigration", m => m.ForCollection("*", c =>
            {
                c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId);
                c.ConvertField("Scope").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
                c.RemoveFieldWhen("Properties", MigrationPredicates.EmptyDocument);
                c.RemoveFieldWhen("Payload", MigrationPredicates.Null);
            }))
            .WithBackupRetention(BackupRetentionPolicy.DeleteOnSuccess)
            .RunAsync();

        var migrated = await raw.Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(legacyId))).FirstOrDefaultAsync();

        Assert.NotNull(migrated);
        Assert.True(migrated["_id"].IsObjectId);
        Assert.True(migrated["Scope"].IsObjectId);
        Assert.False(migrated.Contains("Properties"));
        Assert.False(migrated.Contains("Payload"));
    }
}
