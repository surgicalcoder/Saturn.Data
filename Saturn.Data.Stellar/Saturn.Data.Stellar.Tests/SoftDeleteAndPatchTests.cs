using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Stellar;

namespace Saturn.Data.Stellar.Tests;

public class SoftDeleteAndPatchTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableDb repo = fixture.Repository;
    private IReadonlyRepository ReadonlyRepo => repo;
    private IScopedReadonlyRepository ScopedReadonlyRepo => repo;
    private IScopedRepository ScopedRepo => repo;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await repo.HardDelete<SoftDeleteEntity>(e => true);
        await repo.HardDelete<SoftDeleteScopedEntity>(e => true);
        await repo.HardDelete<TestScope>(e => true);
    }

    [Fact]
    public async Task Delete_Should_Soft_Delete_And_Hide_From_Default_Reads()
    {
        var entity = new SoftDeleteEntity
        {
            Id = EntityIdGenerator.GenerateNewId(),
            Name = "Soft Delete",
            Count = 1
        };

        await repo.Insert(entity);
        await repo.Delete<SoftDeleteEntity>(entity.Id);

        var hidden = await ReadonlyRepo.ById<SoftDeleteEntity>(entity.Id);
        var included = await ReadonlyRepo.ById<SoftDeleteEntity>(entity.Id, includeDeleted: true);

        Assert.Null(hidden);
        Assert.NotNull(included);
        Assert.True(included.IsDeleted);
        Assert.Equal(1, await ReadonlyRepo.Count<SoftDeleteEntity>(e => true, continueFrom: null, includeDeleted: true));
        Assert.Equal(0, await ReadonlyRepo.Count<SoftDeleteEntity>(e => true, continueFrom: null, includeDeleted: false));
    }

    [Fact]
    public async Task Restore_Should_Reveal_Soft_Deleted_Scoped_Item_When_Requested()
    {
        var scope = new TestScope
        {
            Id = EntityIdGenerator.GenerateNewId(),
            Name = "Scope"
        };
        var entity = new SoftDeleteScopedEntity
        {
            Id = EntityIdGenerator.GenerateNewId(),
            Name = "Scoped Soft Delete"
        };

        await repo.Insert(scope);
        await ScopedRepo.Insert<SoftDeleteScopedEntity, TestScope>(scope.Id, entity);
        await repo.Delete<SoftDeleteScopedEntity>(entity.Id);

        Assert.Null(await ScopedReadonlyRepo.ById<SoftDeleteScopedEntity, TestScope>(scope.Id, entity.Id));
        Assert.NotNull(await ScopedReadonlyRepo.ById<SoftDeleteScopedEntity, TestScope>(scope.Id, entity.Id, includeDeleted: true));

        await repo.Restore<SoftDeleteScopedEntity>(entity.Id);

        var restored = await ScopedReadonlyRepo.ById<SoftDeleteScopedEntity, TestScope>(scope.Id, entity.Id);

        Assert.NotNull(restored);
        Assert.False(restored.IsDeleted);
    }

    [Fact]
    public async Task Patch_And_Increment_Should_Update_Entity_And_Bump_Version()
    {
        var entity = new SoftDeleteEntity
        {
            Id = EntityIdGenerator.GenerateNewId(),
            Name = "Original",
            Count = 2
        };

        await repo.Insert(entity);

        await repo.Patch<SoftDeleteEntity>(entity.Id, jsonDocument: "{\"Name\":\"Patched\"}");
        var patched = await ReadonlyRepo.ById<SoftDeleteEntity>(entity.Id, includeDeleted: true);

        Assert.NotNull(patched);
        Assert.Equal("Patched", patched.Name);
        Assert.Equal(2, patched.Count);
        Assert.Equal(1, patched.Version);

        await repo.Increment<SoftDeleteEntity>(entity.Id, item => item.Count, 3, expectedVersion: 1);
        await repo.Patch<SoftDeleteEntity>(
            entity.Id,
            expectedVersion: 2,
            updateDefinition: new StellarDataUpdateDefinition<SoftDeleteEntity>(item => item.Name = "Patched Again"));

        var updated = await ReadonlyRepo.ById<SoftDeleteEntity>(entity.Id, includeDeleted: true);

        Assert.NotNull(updated);
        Assert.Equal("Patched Again", updated.Name);
        Assert.Equal(5, updated.Count);
        Assert.Equal(3, updated.Version);
    }

    private sealed class TestScope : Entity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SoftDeleteEntity : Entity, ISoftDeletable
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; } = string.Empty;
    }

    private sealed class SoftDeleteScopedEntity : ScopedEntity<TestScope>, ISoftDeletable
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; } = string.Empty;
    }
}


