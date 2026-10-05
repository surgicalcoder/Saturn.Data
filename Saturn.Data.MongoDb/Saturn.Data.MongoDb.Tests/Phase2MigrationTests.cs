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

    [Fact]
    public async Task FieldAliases_MatchMongoStorageShape()
    {
        var entity = new BasicEntity { Id = "67a92d2a063e3290f03b29dd", Name = "x", Version = 4 };
        entity.Properties["Theme"] = "dark";

        await repository.Insert(entity, null, default);

        var raw = repository.GetRawCollection<BasicEntity>()
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id)))
            .FirstOrDefault();

        Assert.NotNull(raw);
        Assert.True(raw.Contains("_p"), raw.ToString());
        Assert.True(raw.Contains("_v"), raw.ToString());
        Assert.Equal("_p", MigrationFieldAliases.MongoDb.ToPhysical("Properties"));
        Assert.Equal("_v", MigrationFieldAliases.MongoDb.ToPhysical("Version"));
    }
}
