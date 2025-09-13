using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Stellar.Tests.Entities;

namespace Saturn.Data.Stellar.Tests;

public class Scoped_Tests : IDisposable
{
    StellarRepository repo;
    string fullPath;
    public Scoped_Tests()
    {
        var unitTestPath = "e:\\_scratch\\_unit_tests\\";
        var databaseName = $"Stellar_{DateTime.UtcNow.ToString("O").Replace(":","_").Replace(".","_").Replace("-","_") }";

        fullPath = Path.Combine(unitTestPath, databaseName);
        repo = new StellarRepository(new RepositoryOptions()
        {
            ConnectionString = unitTestPath,
            GetCollectionName = type => type.Name
        }, new StellarRepositoryOptions()
        {
            DatabaseName = databaseName
        });
    }

    public void Dispose()
    {
        repo.Dispose();
        Directory.Delete(fullPath, true);
    }

    [Fact]
    public async Task Scoped()
    {
        await repo.InsertMany<ParentScope>(new List<ParentScope> {WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2});

        var scopedRepo = (IScopedRepository)repo;
        
        await scopedRepo.InsertMany<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, new List<ChildEntity>
        {
            WELL_KNOWN.Parent_Scope_1_Child_Entity_1,
            WELL_KNOWN.Parent_Scope_1_Child_Entity_2
        });
        
        await scopedRepo.InsertMany<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, new List<ChildEntity>
        {
            WELL_KNOWN.Parent_Scope_2_Child_Entity_1,
            WELL_KNOWN.Parent_Scope_2_Child_Entity_2
        });
        
        Assert.Equal(4, await repo.CountMany<ChildEntity>(e=>true));
        Assert.Equal(2, await ((IScopedRepository)repo).CountMany<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e=>true));
        Assert.Equal(2, await ((IScopedRepository)repo).CountMany<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, e=>true));
        
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

