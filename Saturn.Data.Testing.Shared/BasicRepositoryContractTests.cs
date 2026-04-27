using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared.Entities;
using Xunit;

namespace Saturn.Data.Testing.Shared;

public abstract class BasicRepositoryContractTests<TFixture, TRepository>(TFixture fixture) : IAsyncLifetime
    where TFixture : IRepositoryTestFixture<TRepository>
    where TRepository : IRepository
{
    protected TRepository Repo { get; } = fixture.Repository;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Repo.Delete<BasicEntity>(e => true);
        await Repo.Delete<ChildEntity>(e => true);
        await Repo.Delete<ParentScope>(e => true);
    }

    [Fact]
    public async Task Add_And_Get_By_Id()
    {
        await Repo.Insert(WellKnownData.BasicEntity1);
        var fetched = await Repo.ById<BasicEntity>(WellKnownData.BasicEntity1.Id);
        Assert.NotNull(fetched);
        Assert.Equal(WellKnownData.BasicEntity1.Name, fetched.Name);
    }

    [Fact]
    public async Task Update()
    {
        var entity = new BasicEntity
        {
            Id = WellKnownData.BasicEntity1.Id,
            Name = WellKnownData.BasicEntity1.Name
        };

        await Repo.Insert(entity);
        entity.Name = "Updated Name";
        await Repo.Update(entity);

        var fetched = await Repo.ById<BasicEntity>(WellKnownData.BasicEntity1.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Updated Name", fetched.Name);
    }

    [Fact]
    public async Task Save_And_Upsert_Logic()
    {
        var entity = new BasicEntity
        {
            Id = WellKnownData.BasicEntity1.Id,
            Name = WellKnownData.BasicEntity1.Name
        };

        await Repo.Save(entity);
        Assert.Equal(1, await Repo.Count<BasicEntity>(e => true));

        entity.Name = "Updated Name";
        await Repo.Save(entity);

        Assert.Equal(1, await Repo.Count<BasicEntity>(e => true));
        var fetched = await Repo.ById<BasicEntity>(WellKnownData.BasicEntity1.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Updated Name", fetched.Name);
    }
}

