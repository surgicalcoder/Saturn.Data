using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.LiteDb.Tests.Entities;

namespace Saturn.Data.LiteDb.Tests;

public class ScopedTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableLiteDb repo = fixture.Repository;

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
    public async Task Scoped()
    {
        await repo.Insert<ParentScope>(new List<ParentScope> {WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2});

        var scopedRepo = (IScopedRepository)repo;
        
        await scopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, new List<ChildEntity>
        {
            WELL_KNOWN.Parent_Scope_1_Child_Entity_1,
            WELL_KNOWN.Parent_Scope_1_Child_Entity_2
        });
        
        await scopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, new List<ChildEntity>
        {
            WELL_KNOWN.Parent_Scope_2_Child_Entity_1,
            WELL_KNOWN.Parent_Scope_2_Child_Entity_2
        });
        
        Assert.Equal(4, await repo.Count<ChildEntity>(e=>true));
        Assert.Equal(2, await ((IScopedRepository)repo).Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e=>true));
        Assert.Equal(2, await ((IScopedRepository)repo).Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, e=>true));
        
        var allScoped1 = await (await scopedRepo.All<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id)).ToListAsync();
        var allScoped2 = await (await scopedRepo.All<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id)).ToListAsync();
        
        Assert.Equal(2, allScoped1.Count);
        Assert.Equal(2, allScoped2.Count);
        
        Assert.Contains(allScoped1, x => x.Id == WELL_KNOWN.Parent_Scope_1_Child_Entity_1.Id);
        Assert.Contains(allScoped1, x => x.Id == WELL_KNOWN.Parent_Scope_1_Child_Entity_2.Id);
        
        Assert.Contains(allScoped2, x => x.Id == WELL_KNOWN.Parent_Scope_2_Child_Entity_1.Id);
        Assert.Contains(allScoped2, x => x.Id == WELL_KNOWN.Parent_Scope_2_Child_Entity_2.Id);
    }
}