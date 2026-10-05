using System.Linq;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;
using LiteDbX;
using Saturn.Data.LiteDbX.Tests.Entities;

namespace Saturn.Data.LiteDbX.Tests;

public class Phase1MigrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private readonly UnitTestableLiteDb repository = fixture.Repository;

    [Fact]
    public async Task Migrations_ConvertLegacyStringIdsOnRealStorage()
    {
        var legacyId = "67a92d08063e3290f03b29dc";
        var scopeId = "65da55b75278b72ba0ffb2de";

        var legacy = repository.Database.GetCollection("LegacyEntity", BsonAutoId.ObjectId);
        await legacy.Insert(new BsonDocument
        {
            ["_id"] = legacyId,
            ["Scope"] = scopeId,
            ["Name"] = "Ada",
            ["Properties"] = new BsonDocument(),
            ["Payload"] = BsonValue.Null
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

        var migrated = await FirstRawAsync("LegacyEntity");

        Assert.True(migrated["_id"].IsObjectId);
        Assert.Equal(legacyId, migrated["_id"].AsObjectId.ToString());
        Assert.True(migrated["Scope"].IsObjectId);
        Assert.Equal(scopeId, migrated["Scope"].AsObjectId.ToString());
        Assert.False(migrated.ContainsKey("Properties"));
        Assert.False(migrated.ContainsKey("Payload"));
        Assert.True(repository.Database.CollectionExists("__saturn_migrations").AsTask().GetAwaiter().GetResult());
    }

    [Fact]
    public void FieldAliases_MatchLiteDbxStorageShape()
    {
        var entity = new BasicEntity { Id = "67a92d08063e3290f03b29dc", Name = "x" };
        entity.Properties["Theme"] = "dark";

        var document = repository.SerializeToDocument(entity);

        Assert.True(document.ContainsKey("_p"));
        Assert.Equal("_p", MigrationFieldAliases.LiteDbx.ToPhysical("Properties"));
        Assert.Equal("Properties", MigrationFieldAliases.LiteDbx.ToLogical("_p"));
    }

    [Fact]
    public async Task IndexEnumeration_ReadsSystemIndexes()
    {
        var store = repository.CreateMigrationStore();
        var collection = store.GetCollection("IndexedEntity");
        await collection.EnsureIndexAsync(new MigrationIndexDefinition("ix_custom_name", "Name", false));

        var indexes = collection.GetIndexes();

        Assert.Contains(indexes, index => index.Name == "ix_custom_name");
    }

    private async Task<BsonDocument> FirstRawAsync(string collectionName)
    {
        var collection = repository.Database.GetCollection(collectionName, BsonAutoId.ObjectId);

        await foreach (var document in collection.FindAll())
        {
            return document;
        }

        return null;
    }
}
