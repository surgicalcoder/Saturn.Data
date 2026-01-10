using Saturn.Data.Stellar.Tests.Entities;

namespace Saturn.Data.Stellar.Tests;

public class BasicTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableDb repo = fixture.Repository;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.Delete<BasicEntity>(e => true);
        await repo.Delete<ChildEntity>(e => true);
        await repo.Delete<ParentScope>(e => true);
    }

    [Fact]
    public async Task Add_And_Get_By_Id()
    {
        await repo.Insert(WELL_KNOWN.Basic_Entity_1);
        var fetched = await repo.ById<BasicEntity>(WELL_KNOWN.Basic_Entity_1.Id);
        Assert.NotNull(fetched);
        Assert.Equal(WELL_KNOWN.Basic_Entity_1.Name, fetched.Name);
    }
    
    [Fact]
    public async Task AddGeneratesId()
    {
        var item = new BasicEntity();
        item.Name = "Test item with empty id";
        await repo.Insert(item);
        Assert.False(string.IsNullOrWhiteSpace(item.Id));
    }
    
    [Fact]
    public async Task Update()
    {
        // No longer need to clear at the start of each test
        var entity = new BasicEntity
        {
            Id = WELL_KNOWN.Basic_Entity_1.Id,
            Name = WELL_KNOWN.Basic_Entity_1.Name
        };
        await repo.Insert(entity);
        entity.Name = "Updated Name";
        await repo.Update(entity);
        var fetched = await repo.ById<BasicEntity>(WELL_KNOWN.Basic_Entity_1.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Updated Name", fetched.Name);
    }
    
    
    [Fact]
    public async Task BulkInsertAndRetrieve()
    {
        var entities = new List<BasicEntity>
        {
            WELL_KNOWN.Basic_Entity_1,
            WELL_KNOWN.Basic_Entity_2,
            WELL_KNOWN.Basic_Entity_3
        };
        await repo.Insert(entities);
        
        var allEntities = await repo.ById<BasicEntity>(new List<string>{WELL_KNOWN.Basic_Entity_1.Id, WELL_KNOWN.Basic_Entity_2.Id, WELL_KNOWN.Basic_Entity_3.Id}) ;
        Assert.Equal(3, await allEntities.CountAsync());
    }
    
    [Fact]
    public async Task Save_And_Upsert_Logic()
    {
        // No longer need to clear at the start of each test
        var entity = new BasicEntity
        {
            Id = WELL_KNOWN.Basic_Entity_1.Id,
            Name = WELL_KNOWN.Basic_Entity_1.Name
        };
        await repo.Save(entity);
        
        Assert.Equal(1, await repo.Count<BasicEntity>(e=>true));
        
        entity.Name = "Updated Name";
        await repo.Save(entity);
        Assert.Equal(1, await repo.Count<BasicEntity>(e=>true));
        var fetched = await repo.ById<BasicEntity>(WELL_KNOWN.Basic_Entity_1.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Updated Name", fetched.Name);
    }
}