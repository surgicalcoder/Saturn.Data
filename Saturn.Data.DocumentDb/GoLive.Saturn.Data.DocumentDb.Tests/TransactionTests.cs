using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.DocumentDb.Tests;

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
        if (!fixture.Repository.Capabilities.SupportsTransactions)
        {
            return;
        }

        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "tx-commit" };

        await using var transaction = await fixture.Repository.CreateTransaction();
        await fixture.Repository.Insert(entity, transaction);
        await transaction.CommitAsync();

        Assert.NotNull(await fixture.Repository.ById<BasicEntity>(entity.Id));
    }

    [Fact]
    public async Task Rollback_Discards_Write()
    {
        if (!fixture.Repository.Capabilities.SupportsTransactions)
        {
            return;
        }

        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "tx-rollback" };

        await using var transaction = await fixture.Repository.CreateTransaction();
        await fixture.Repository.Insert(entity, transaction);
        await transaction.RollbackAsync();

        Assert.Null(await fixture.Repository.ById<BasicEntity>(entity.Id));
    }

    [Fact]
    public async Task Buffered_Write_Is_Not_Visible_Until_Commit()
    {
        if (!fixture.Repository.Capabilities.SupportsTransactions)
        {
            return;
        }

        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "tx-buffered" };

        await using var transaction = await fixture.Repository.CreateTransaction();
        await fixture.Repository.Insert(entity, transaction);

        Assert.Null(await fixture.Repository.ById<BasicEntity>(entity.Id));

        await transaction.CommitAsync();

        Assert.NotNull(await fixture.Repository.ById<BasicEntity>(entity.Id));
    }

    [Fact]
    public async Task Update_Inside_Transaction_Is_Rolled_Back()
    {
        if (!fixture.Repository.Capabilities.SupportsTransactions)
        {
            return;
        }

        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "before" };
        await fixture.Repository.Insert(entity);

        await using (var transaction = await fixture.Repository.CreateTransaction())
        {
            entity.Name = "after";
            await fixture.Repository.Update(entity, transaction);
            await transaction.RollbackAsync();
        }

        var reloaded = await fixture.Repository.ById<BasicEntity>(entity.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("before", reloaded!.Name);
    }
}
