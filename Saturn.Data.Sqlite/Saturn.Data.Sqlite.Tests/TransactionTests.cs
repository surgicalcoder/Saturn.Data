using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class TransactionTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
    }

    [Fact]
    public async Task Commit_Persists_Write()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "tx-commit" };

        await using var transaction = await fixture.Repository.CreateTransaction();
        await transaction.Start();
        await fixture.Repository.Insert(entity, transaction);
        await transaction.CommitAsync();

        Assert.NotNull(await fixture.Repository.ById<BasicEntity>(entity.Id));
    }

    [Fact]
    public async Task Rollback_Discards_Write()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "tx-rollback" };

        await using var transaction = await fixture.Repository.CreateTransaction();
        await transaction.Start();
        await fixture.Repository.Insert(entity, transaction);
        await transaction.RollbackAsync();

        Assert.Null(await fixture.Repository.ById<BasicEntity>(entity.Id));
    }

    [Fact]
    public async Task Read_Inside_Transaction_Sees_Uncommitted_Write()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "tx-read" };

        await using var transaction = await fixture.Repository.CreateTransaction();
        await transaction.Start();
        await fixture.Repository.Insert(entity, transaction);

        var visible = await fixture.Repository.ById<BasicEntity>(entity.Id, transaction);
        Assert.NotNull(visible);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Update_Inside_Transaction_Is_Visible_And_Rolled_Back()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "before" };
        await fixture.Repository.Insert(entity);

        await using (var transaction = await fixture.Repository.CreateTransaction())
        {
            await transaction.Start();
            entity.Name = "after";
            await fixture.Repository.Update(entity, transaction);
            await transaction.RollbackAsync();
        }

        var reloaded = await fixture.Repository.ById<BasicEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("before", reloaded.Name);
    }
}
