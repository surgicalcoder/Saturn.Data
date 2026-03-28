using GoLive.Saturn.Data.Entities;
using LiteDbX;
using Saturn.Data.LiteDb.Tests.Entities;

namespace Saturn.Data.LiteDb.Tests;

public class RefSerializationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string ContainerId = "68fdd5525324ff2610c43640";
    private readonly UnitTestableLiteDb repo = fixture.Repository;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.Delete<RefContainerEntity>(e => true);
        await repo.Delete<BasicEntity>(e => e.Id == WELL_KNOWN.Basic_Entity_1.Id);
    }

    [Fact]
    public void Serialize_Ref_Property_As_ObjectId()
    {
        var entity = new RefContainerEntity
        {
            Id = ContainerId,
            Related = new Ref<BasicEntity> { Id = WELL_KNOWN.Basic_Entity_1.Id }
        };

        var document = repo.SerializeToDocument(entity);

        Assert.True(document.TryGetValue(nameof(RefContainerEntity.Related), out var relatedValue));
        Assert.True(relatedValue.IsObjectId);
        Assert.Equal(WELL_KNOWN.Basic_Entity_1.Id, relatedValue.AsObjectId.ToString());
    }

    [Fact]
    public async Task Insert_And_Get_RoundTrips_Ref_Property()
    {
        await repo.Insert(new BasicEntity
        {
            Id = WELL_KNOWN.Basic_Entity_1.Id,
            Name = WELL_KNOWN.Basic_Entity_1.Name
        });
        

        Assert.Equal(1, await repo.Count<BasicEntity>(e=>true));
        
        await repo.Insert(new RefContainerEntity
        {
            Id = ContainerId,
            Related = new Ref<BasicEntity> { Id = WELL_KNOWN.Basic_Entity_1.Id }
        });
        
        Assert.Equal(1, await repo.Count<RefContainerEntity>(e=>true));

        var allItems = await repo.IQueryable<RefContainerEntity>().ToListAsync(); // repo.All<RefContainerEntity>(). await (await repo.All<RefContainerEntity>() ).ToListAsync();

        var fetched = await repo.ById<RefContainerEntity>(ContainerId);

        Assert.NotNull(fetched);
        Assert.NotNull(fetched.Related);
        Assert.Equal(WELL_KNOWN.Basic_Entity_1.Id, fetched.Related.Id);
    }
}


