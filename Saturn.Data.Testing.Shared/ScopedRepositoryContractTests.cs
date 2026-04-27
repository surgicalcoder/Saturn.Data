using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared.Entities;
using Xunit;

namespace Saturn.Data.Testing.Shared;

public abstract class ScopedRepositoryContractTests<TFixture, TRepository>(TFixture fixture) : IAsyncLifetime
    where TFixture : IRepositoryTestFixture<TRepository>
    where TRepository : IRepository, IScopedRepository
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
    public async Task Scoped()
    {
        await Repo.Insert<ParentScope>(new List<ParentScope> { WellKnownData.ParentScope1, WellKnownData.ParentScope2 });

        var scopedRepo = (IScopedRepository)Repo;

        await scopedRepo.Insert<ChildEntity, ParentScope>(WellKnownData.ParentScope1.Id, new List<ChildEntity>
        {
            WellKnownData.ParentScope1ChildEntity1,
            WellKnownData.ParentScope1ChildEntity2
        });

        await scopedRepo.Insert<ChildEntity, ParentScope>(WellKnownData.ParentScope2.Id, new List<ChildEntity>
        {
            WellKnownData.ParentScope2ChildEntity1,
            WellKnownData.ParentScope2ChildEntity2
        });

        Assert.Equal(4, await Repo.Count<ChildEntity>(e => true));
        Assert.Equal(2, await scopedRepo.Count<ChildEntity, ParentScope>(WellKnownData.ParentScope1.Id, e => true));
        Assert.Equal(2, await scopedRepo.Count<ChildEntity, ParentScope>(WellKnownData.ParentScope2.Id, e => true));

        var allScoped1 = await (await scopedRepo.All<ChildEntity, ParentScope>(WellKnownData.ParentScope1.Id)).ToListAsync();
        var allScoped2 = await (await scopedRepo.All<ChildEntity, ParentScope>(WellKnownData.ParentScope2.Id)).ToListAsync();

        Assert.Equal(2, allScoped1.Count);
        Assert.Equal(2, allScoped2.Count);

        Assert.Contains(allScoped1, x => x.Id == WellKnownData.ParentScope1ChildEntity1.Id);
        Assert.Contains(allScoped1, x => x.Id == WellKnownData.ParentScope1ChildEntity2.Id);

        Assert.Contains(allScoped2, x => x.Id == WellKnownData.ParentScope2ChildEntity1.Id);
        Assert.Contains(allScoped2, x => x.Id == WellKnownData.ParentScope2ChildEntity2.Id);
    }
}

