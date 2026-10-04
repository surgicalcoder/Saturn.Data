using System.Collections.Generic;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;
using Saturn.Data.LiteDbX.Tests.Entities;
using Saturn.Data.Testing.Shared.Cascade.Entities;

namespace Saturn.Data.LiteDbX.Tests;

public class Phase0PersistenceTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private readonly UnitTestableLiteDb repository = fixture.Repository;

    [Fact]
    public void Properties_NonEmpty_SerializesAsUnderscoreP()
    {
        var entity = new BasicEntity { Id = "67a92d08063e3290f03b29dc", Name = "x" };
        entity.Properties["Theme"] = "dark";
        entity.Properties["Count"] = 3;

        var document = repository.SerializeToDocument(entity);

        Assert.True(document.ContainsKey("_p"));
        Assert.False(document.ContainsKey("Properties"));
        Assert.Equal("dark", document["_p"].AsDocument["Theme"].AsString);
    }

    [Fact]
    public void Properties_Empty_IsNotSerialized()
    {
        var entity = new BasicEntity { Id = "67a92d2a063e3290f03b29dd", Name = "y" };

        var document = repository.SerializeToDocument(entity);

        Assert.False(document.ContainsKey("_p"), document.ToString());
    }

    [Fact]
    public async Task Properties_RoundTripsThroughStorage()
    {
        var entity = new BasicEntity { Id = "67a92d48063e3290f03b29de", Name = "z" };
        entity.Properties["Locale"] = "en-GB";

        await repository.Insert(entity);

        var collection = repository.Database.GetCollection("BasicEntity", BsonAutoId.ObjectId);
        var raw = await collection.FindById(new ObjectId(entity.Id));
        Assert.True(raw.ContainsKey("_p"));

        var loaded = await repository.ById<BasicEntity>(entity.Id, false, null);
        Assert.Equal("en-GB", loaded.Properties["Locale"]);
    }

    [Fact]
    public async Task SoftDelete_MissingIsDeleted_IsVisible()
    {
        var collection = repository.Database.GetCollection("CascadeAccount", BsonAutoId.ObjectId);
        collection.Insert(new BsonDocument
        {
            ["_id"] = new ObjectId("67a92d08063e3290f03b29dc"),
            ["Name"] = "legacy-missing-flag"
        });

        var ids = await CollectIds(await repository.All<CascadeAccount>(false, null));

        Assert.Contains("67a92d08063e3290f03b29dc", ids);
    }

    [Fact]
    public async Task SoftDelete_ExplicitTrue_IsHidden()
    {
        var collection = repository.Database.GetCollection("CascadeAccount", BsonAutoId.ObjectId);
        collection.Insert(new BsonDocument
        {
            ["_id"] = new ObjectId("67a92d2a063e3290f03b29dd"),
            ["Name"] = "tombstoned",
            ["IsDeleted"] = true
        });

        var ids = await CollectIds(await repository.All<CascadeAccount>(false, null));

        Assert.DoesNotContain("67a92d2a063e3290f03b29dd", ids);
    }

    private static async Task<List<string>> CollectIds(IAsyncEnumerable<CascadeAccount> source)
    {
        var ids = new List<string>();
        await foreach (var item in source)
        {
            ids.Add(item.Id);
        }
        return ids;
    }
}
