using Saturn.Data.Stellar.Tests.Entities;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar.Tests;

public class RefFormatterTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableDb repo = fixture.Repository;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.Delete<RefEntity>(e => true);
        await repo.Delete<BasicEntity>(e => true);
    }

    [Fact]
    public async Task RefFormatter_Should_Serialize_And_Deserialize_Ref_Correctly()
    {
        // Step 1: Create and save a BasicEntity so it has an Id
        var basicEntity = new BasicEntity
        {
            Id = "68bdd5525324ff2610c43701", // Using a unique ID
            Name = "Test Basic Entity for Ref"
        };
        await repo.Save(basicEntity);

        // Step 2: Create a RefEntity and set the BasicEntityItem to reference the saved BasicEntity
        var refEntity = new RefEntity
        {
            Id = "68bdd5525324ff2610c43702", // Using a unique ID
            Name = "Test Ref Entity",
            BasicEntityItem = new Ref<BasicEntity>(basicEntity.Id)
        };
        await repo.Save(refEntity);

        // Step 3: Pull the same RefEntity out of the database using its ID into a different variable
        var retrievedRefEntity = await repo.ById<RefEntity>(refEntity.Id);

        // Step 4: Verify the results
        Assert.NotNull(retrievedRefEntity);
        Assert.NotNull(retrievedRefEntity.BasicEntityItem);
        
        // Check that BasicEntityItem.Id is populated
        Assert.Equal(basicEntity.Id, retrievedRefEntity.BasicEntityItem.Id);
        
        // Check that BasicEntityItem.Item is not populated (lazy loading)
        Assert.Null(retrievedRefEntity.BasicEntityItem.Item);
    }

    [Fact]
    public async Task RefFormatter_Should_Handle_Null_Ref()
    {
        // Create a RefEntity with null BasicEntityItem
        var refEntity = new RefEntity
        {
            Id = "68bdd5525324ff2610c43703",
            Name = "Test Ref Entity with Null Ref",
            BasicEntityItem = null
        };
        await repo.Save(refEntity);

        // Retrieve and verify
        var retrievedRefEntity = await repo.ById<RefEntity>(refEntity.Id);
        
        Assert.NotNull(retrievedRefEntity);
        Assert.Null(retrievedRefEntity.BasicEntityItem);
    }

    [Fact]
    public async Task RefFormatter_Should_Handle_Empty_Ref()
    {
        // Create a RefEntity with empty Ref (empty Id)
        var refEntity = new RefEntity
        {
            Id = "68bdd5525324ff2610c43704",
            Name = "Test Ref Entity with Empty Ref",
            BasicEntityItem = new Ref<BasicEntity>("")
        };
        await repo.Save(refEntity);

        // Retrieve and verify
        var retrievedRefEntity = await repo.ById<RefEntity>(refEntity.Id);
        
        Assert.NotNull(retrievedRefEntity);
        // Should be null or default when serialized/deserialized with empty ID
        Assert.True(retrievedRefEntity.BasicEntityItem == null || 
                   string.IsNullOrWhiteSpace(retrievedRefEntity.BasicEntityItem.Id));
    }

    [Fact]
    public async Task RefFormatter_Should_Preserve_Reference_Id_After_Roundtrip()
    {
        // Create multiple BasicEntities
        var basicEntity1 = new BasicEntity { Id = "68bdd5525324ff2610c43705", Name = "Basic Entity 1" };
        var basicEntity2 = new BasicEntity { Id = "68bdd5525324ff2610c43706", Name = "Basic Entity 2" };
        
        await repo.Save(basicEntity1);
        await repo.Save(basicEntity2);

        // Create RefEntities pointing to different BasicEntities
        var refEntity1 = new RefEntity
        {
            Id = "68bdd5525324ff2610c43707",
            Name = "Ref Entity 1",
            BasicEntityItem = new Ref<BasicEntity>(basicEntity1.Id)
        };

        var refEntity2 = new RefEntity
        {
            Id = "68bdd5525324ff2610c43708",
            Name = "Ref Entity 2", 
            BasicEntityItem = new Ref<BasicEntity>(basicEntity2.Id)
        };

        await repo.Save(refEntity1);
        await repo.Save(refEntity2);

        // Retrieve and verify each maintains correct reference
        var retrieved1 = await repo.ById<RefEntity>(refEntity1.Id);
        var retrieved2 = await repo.ById<RefEntity>(refEntity2.Id);

        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.Equal(basicEntity1.Id, retrieved1.BasicEntityItem.Id);
        Assert.Equal(basicEntity2.Id, retrieved2.BasicEntityItem.Id);
        Assert.Null(retrieved1.BasicEntityItem.Item);
        Assert.Null(retrieved2.BasicEntityItem.Item);
    }
}
