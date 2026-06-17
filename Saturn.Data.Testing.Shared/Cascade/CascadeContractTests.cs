using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;
using Saturn.Data.Testing.Shared.Cascade.Entities;
using Xunit;

namespace Saturn.Data.Testing.Shared.Cascade;

public abstract class CascadeContractTests<TFixture, TRepository>(TFixture fixture) : IAsyncLifetime
    where TFixture : ICascadeTestFixture<TRepository>
    where TRepository : IRepository
{
    protected TRepository Repo { get; } = fixture.Repository;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Repo.Delete<CascadeTag>(e => true);
        await Repo.Delete<CascadePost>(e => true);
        await Repo.Delete<CascadeAccount>(e => true);
        await Repo.HardDelete<CascadeAccount>(e => true);
        await Repo.HardDelete<CascadePost>(e => true);
        await Repo.HardDelete<CascadeTag>(e => true);
        await Repo.HardDelete<CascadeUser>(e => true);
    }

    [Fact]
    public async Task DeleteCascade_UserWithAccounts_ArchivesAccounts()
    {
        var user = new CascadeUser { Id = EntityIdGenerator.GenerateNewId(), Name = "u" };
        var account = new CascadeAccount { Id = EntityIdGenerator.GenerateNewId(), Name = "a" };
        account.Scope = new Ref<CascadeUser>(user.Id);
        await Repo.Insert(user);
        await Repo.Insert(account);

        var report = await Repo.DeleteCascade<CascadeUser>(user.Id);

        Assert.Contains(typeof(CascadeAccount), report.ArchivedPerType.Keys);
        var reloaded = await Repo.ById<CascadeAccount>(account.Id, includeDeleted: true);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsArchived);
    }

    [Fact]
    public async Task HardDeleteCascade_RemovesAllChildren()
    {
        var user = new CascadeUser { Id = EntityIdGenerator.GenerateNewId(), Name = "u" };
        var account = new CascadeAccount { Id = EntityIdGenerator.GenerateNewId(), Name = "a" };
        var post = new CascadePost { Id = EntityIdGenerator.GenerateNewId(), Title = "p" };
        account.Scope = new Ref<CascadeUser>(user.Id);
        post.Scope = new Ref<CascadeAccount>(account.Id);
        await Repo.Insert(user);
        await Repo.Insert(account);
        await Repo.Insert(post);

        await Repo.HardDeleteCascade<CascadeUser>(user.Id);

        Assert.Null(await Repo.ById<CascadeUser>(user.Id));
        Assert.Null(await Repo.ById<CascadeAccount>(account.Id));
        Assert.Null(await Repo.ById<CascadePost>(post.Id));
    }

    [Fact]
    public async Task DeleteCascade_Transitive_AffectsGrandchildren()
    {
        var user = new CascadeUser { Id = EntityIdGenerator.GenerateNewId(), Name = "u" };
        var account = new CascadeAccount { Id = EntityIdGenerator.GenerateNewId(), Name = "a" };
        var post = new CascadePost { Id = EntityIdGenerator.GenerateNewId(), Title = "p" };
        account.Scope = new Ref<CascadeUser>(user.Id);
        post.Scope = new Ref<CascadeAccount>(account.Id);
        await Repo.Insert(user);
        await Repo.Insert(account);
        await Repo.Insert(post);

        var report = await Repo.HardDeleteCascade<CascadeUser>(user.Id);

        Assert.True(report.DeletedPerType.ContainsKey(typeof(CascadeAccount)));
        Assert.True(report.DeletedPerType.ContainsKey(typeof(CascadePost)));
    }

    [Fact]
    public async Task DeleteCascade_NonCascadingProperty_LeavesSiblingUntouched()
    {
        var user = new CascadeUser { Id = EntityIdGenerator.GenerateNewId(), Name = "u" };
        var tag = new CascadeTag { Id = EntityIdGenerator.GenerateNewId(), Name = "t" };
        tag.Scope = new Ref<CascadeUser>(user.Id);
        var survivor = new CascadeTag { Id = EntityIdGenerator.GenerateNewId(), Name = "survivor" };
        await Repo.Insert(user);
        await Repo.Insert(tag);
        await Repo.Insert(survivor);

        await Repo.HardDeleteCascade<CascadeUser>(user.Id);

        Assert.Null(await Repo.ById<CascadeTag>(tag.Id));
        Assert.NotNull(await Repo.ById<CascadeTag>(survivor.Id));
    }
}
