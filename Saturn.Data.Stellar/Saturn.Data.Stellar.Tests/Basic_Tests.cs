using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Stellar.Tests.Entities;

namespace Saturn.Data.Stellar.Tests;

public class Basic_Tests : IDisposable
{
    StellarRepository repo;
    string fullPath;
    public Basic_Tests()
    {
        var unitTestPath = "e:\\_scratch\\_unit_tests\\";
        var databaseName = $"Stellar_{DateTime.UtcNow.ToString("O").Replace(":","_").Replace(".","_").Replace("-","_") }";

        fullPath = Path.Combine(unitTestPath, databaseName);
        repo = new StellarRepository(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name
        }, new StellarRepositoryOptions()
        {
            BaseDirectory = unitTestPath,
            DatabaseName = databaseName
        });
    }

    public void Dispose()
    {
        repo.Dispose();
        Directory.Delete(fullPath, true);
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
    public async Task Update()
    {
        await repo.Delete<BasicEntity>(e => true);
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
    public async Task Save_And_Upsert_Logic()
    {
        await repo.Delete<BasicEntity>(e => true);
        
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

