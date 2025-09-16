using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Stellar.Tests.Entities;

namespace Saturn.Data.Stellar.Tests;

public class ComprehensiveScopedTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableDb repo = fixture.Repository;
    private IScopedRepository ScopedRepo => repo;
    private IScopedReadonlyRepository ScopedReadonlyRepo => repo;

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
    public async Task Insert_Single_Entity_Should_Not_Leak_Between_Scopes()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var entity1 = new ChildEntity { Id =  EntityIdGenerator.GenerateNewId(), Name = "Entity in Scope 1" };
        var entity2 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Entity in Scope 2" };

        // Act
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2);

        // Assert
        var scope1Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e => true);
        var scope2Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, e => true);
        
        Assert.Equal(1, scope1Count);
        Assert.Equal(1, scope2Count);
        
        var scope1Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1.Id);
        var scope2Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2.Id);
        
        Assert.NotNull(scope1Entity);
        Assert.NotNull(scope2Entity);
        Assert.Equal("Entity in Scope 1", scope1Entity.Name);
        Assert.Equal("Entity in Scope 2", scope2Entity.Name);
        
        // Verify cross-scope isolation
        var scope1EntityInScope2 = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity1.Id);
        var scope2EntityInScope1 = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity2.Id);
        
        Assert.Null(scope1EntityInScope2);
        Assert.Null(scope2EntityInScope1);
    }

    [Fact]
    public async Task Insert_Multiple_Entities_Should_Not_Leak_Between_Scopes()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Entity 1 in Scope 1" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Entity 2 in Scope 1" }
        };
        
        var scope2Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Entity 1 in Scope 2" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Entity 2 in Scope 2" }
        };

        // Act
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        // Assert
        var scope1Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e => true);
        var scope2Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, e => true);
        
        Assert.Equal(2, scope1Count);
        Assert.Equal(2, scope2Count);
        
        var scope1All = await (await ScopedReadonlyRepo.All<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id)).ToListAsync();
        var scope2All = await (await ScopedReadonlyRepo.All<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id)).ToListAsync();
        
        Assert.Equal(2, scope1All.Count);
        Assert.Equal(2, scope2All.Count);
        
        Assert.All(scope1All, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        Assert.All(scope2All, e => Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, e.Scope));
    }

    [Fact]
    public async Task Update_Should_Only_Affect_Entities_In_Same_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var entity1 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Original Name 1" };
        var entity2 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Original Name 2" };
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2);

        // Act
        entity1.Name = "Updated Name 1";
        await ScopedRepo.Update<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1);

        // Assert
        var updatedEntity1 = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1.Id);
        var unchangedEntity2 = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2.Id);
        
        Assert.NotNull(updatedEntity1);
        Assert.NotNull(unchangedEntity2);
        Assert.Equal("Updated Name 1", updatedEntity1.Name);
        Assert.Equal("Original Name 2", unchangedEntity2.Name);
    }

    [Fact]
    public async Task Save_Should_Respect_Scope_Boundaries()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var entity = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Save Test Entity" };

        // Act - Save to scope 1 first
        await ScopedRepo.Save<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity);
        
        // Verify entity exists in scope 1
        var scope1Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity.Id);
        Assert.NotNull(scope1Entity);
        Assert.Equal("Save Test Entity", scope1Entity.Name);
        Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, scope1Entity.Scope);
        
        // Update entity and save to scope 2 (should move the entity to scope 2 since ID is the primary key)
        entity.Name = "Updated Save Test Entity";
        await ScopedRepo.Save<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity);

        // Assert - The entity should now be in scope 2 only (moved, not duplicated)
        var scope1EntityAfterMove = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity.Id);
        var scope2Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity.Id);
        
        Assert.Null(scope1EntityAfterMove); // Should no longer exist in scope 1
        Assert.NotNull(scope2Entity); // Should exist in scope 2
        Assert.Equal("Updated Save Test Entity", scope2Entity.Name);
        Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, scope2Entity.Scope);
        
        // Verify only one entity exists in total (not duplicated)
        var totalCount = await repo.Count<ChildEntity>(e => true);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task Delete_By_Id_Should_Only_Delete_From_Specified_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var entity1 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Delete Test 1" };
        var entity2 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Delete Test 2" }; // Different ID, different scope
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2);

        // Act - Delete from scope 1 only
        await ScopedRepo.Delete<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1.Id);

        // Assert
        var scope1Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1.Id);
        var scope2Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2.Id);
        
        Assert.Null(scope1Entity); // Should be deleted from scope 1
        Assert.NotNull(scope2Entity); // Should still exist in scope 2
        Assert.Equal("Delete Test 2", scope2Entity.Name);
        
        // Verify the scoped delete respects scope boundaries by trying to delete a non-existent ID from scope 2
        // This should not affect anything since the ID doesn't exist in scope 2
        await ScopedRepo.Delete<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity1.Id);
        
        // Entity in scope 2 should still exist since the delete was scoped
        var scope2EntityAfterWrongScopeDelete = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2.Id);
        Assert.NotNull(scope2EntityAfterWrongScopeDelete);
        
        // Verify total count - should be 1 (only entity2 remains)
        var totalCount = await repo.Count<ChildEntity>(e => true);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task Delete_By_Filter_Should_Only_Delete_From_Specified_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Filter Delete Test" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Keep This One" }
        };
        
        var scope2Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Filter Delete Test" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Keep This One" }
        };
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        // Act - Delete entities with specific name from scope 1 only
        await ScopedRepo.Delete<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e => e.Name == "Filter Delete Test");

        // Assert
        var scope1Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e => true);
        var scope2Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, e => true);
        
        Assert.Equal(1, scope1Count); // Only "Keep This One" should remain
        Assert.Equal(2, scope2Count); // Both entities should remain in scope 2
        
        var scope1Remaining = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities[1].Id);
        var scope2FilterEntity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities[0].Id);
        
        Assert.NotNull(scope1Remaining);
        Assert.NotNull(scope2FilterEntity);
        Assert.Equal("Keep This One", scope1Remaining.Name);
        Assert.Equal("Filter Delete Test", scope2FilterEntity.Name);
    }

    [Fact]
    public async Task Delete_Multiple_IDs_Should_Only_Delete_From_Specified_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Delete 1" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Delete 2" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Keep 1" }
        };
        
        var scope2Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Delete 1 Scope 2" }, // Different ID but similar name
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Delete 2 Scope 2" }, // Different ID but similar name
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Multi Keep 2" }
        };
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        // Act - Delete specific IDs from scope 1 only
        await ScopedRepo.Delete<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, new[] { scope1Entities[0].Id, scope1Entities[1].Id });

        // Assert
        var scope1Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e => true);
        var scope2Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, e => true);
        
        Assert.Equal(1, scope1Count); // Only "Multi Keep 1" should remain
        Assert.Equal(3, scope2Count); // All entities should remain in scope 2
        
        var scope2Entity1 = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities[0].Id);
        var scope2Entity2 = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities[1].Id);
        
        Assert.NotNull(scope2Entity1);
        Assert.NotNull(scope2Entity2);
        Assert.Equal("Multi Delete 1 Scope 2", scope2Entity1.Name);
        Assert.Equal("Multi Delete 2 Scope 2", scope2Entity2.Name);
        
        // Check the correct remaining entity in scope 1
        var scope1CorrectRemaining = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities[2].Id);
        Assert.NotNull(scope1CorrectRemaining);
        Assert.Equal("Multi Keep 1", scope1CorrectRemaining.Name);
    }

    [Fact]
    public async Task Upsert_Should_Respect_Scope_Boundaries()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var entity = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Original Upsert Entity" };
        
        // Insert into scope 1
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity);

        // Verify entity exists in scope 1
        var scope1EntityAfterInsert = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity.Id);
        Assert.NotNull(scope1EntityAfterInsert);
        Assert.Equal("Original Upsert Entity", scope1EntityAfterInsert.Name);
        Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, scope1EntityAfterInsert.Scope);

        // Act - Upsert same ID in scope 2 (should move the entity to scope 2 since ID is the primary key)
        entity.Name = "Upserted Entity Moved to Scope 2";
        await ScopedRepo.Upsert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity);

        // Assert - The entity should now be in scope 2 only (moved, not duplicated)
        var scope1EntityAfterUpsert = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity.Id);
        var scope2Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity.Id);
        
        Assert.Null(scope1EntityAfterUpsert); // Should no longer exist in scope 1
        Assert.NotNull(scope2Entity); // Should exist in scope 2
        Assert.Equal("Upserted Entity Moved to Scope 2", scope2Entity.Name);
        Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, scope2Entity.Scope);
        
        // Verify only one entity exists in total (not duplicated)
        var totalCount = await repo.Count<ChildEntity>(e => true);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task Count_Should_Only_Count_Entities_In_Specified_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Count Test" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Count Test" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Different Name" }
        };
        
        var scope2Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Count Test" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Count Test" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "Count Test" }
        };
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        // Act & Assert
        var scope1TotalCount = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e => true);
        var scope2TotalCount = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, e => true);
        var scope1FilteredCount = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e => e.Name == "Count Test");
        var scope2FilteredCount = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, e => e.Name == "Count Test");
        
        Assert.Equal(3, scope1TotalCount);
        Assert.Equal(3, scope2TotalCount);
        Assert.Equal(2, scope1FilteredCount);
        Assert.Equal(3, scope2FilteredCount);
    }

    [Fact]
    public async Task Many_Should_Only_Return_Entities_From_Specified_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>();
        var scope2Entities = new List<ChildEntity>();
        
        for (int i = 1; i <= 10; i++)
        {
            scope1Entities.Add(new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"Many Test Scope 1 - {i}" });
            scope2Entities.Add(new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"Many Test Scope 2 - {i}" });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        // Act
        var scope1Results = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id, 
            e => e.Name.Contains("Many Test"), 
            pageSize: 5)).ToListAsync();
            
        var scope2Results = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_2.Id, 
            e => e.Name.Contains("Many Test"), 
            pageSize: 7)).ToListAsync();

        // Assert
        Assert.Equal(5, scope1Results.Count);
        Assert.Equal(7, scope2Results.Count);
        
        Assert.All(scope1Results, e => Assert.Contains("Scope 1", e.Name));
        Assert.All(scope2Results, e => Assert.Contains("Scope 2", e.Name));
        Assert.All(scope1Results, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        Assert.All(scope2Results, e => Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, e.Scope));
    }

    [Fact]
    public async Task One_Should_Only_Return_Entity_From_Specified_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var entity1 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "One Test Entity" };
        var entity2 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "One Test Entity" }; // Same name, different scope
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2);

        // Act
        var scope1Result = await ScopedReadonlyRepo.One<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id, 
            e => e.Name == "One Test Entity");
            
        var scope2Result = await ScopedReadonlyRepo.One<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_2.Id, 
            e => e.Name == "One Test Entity");

        // Assert
        Assert.NotNull(scope1Result);
        Assert.NotNull(scope2Result);
        Assert.Equal(entity1.Id, scope1Result.Id);
        Assert.Equal(entity2.Id, scope2Result.Id);
        Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, scope1Result.Scope);
        Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, scope2Result.Scope);
    }

    [Fact]
    public async Task ById_Multiple_IDs_Should_Only_Return_Entities_From_Specified_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "ById Test 1 Scope 1" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "ById Test 2 Scope 1" },
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "ById Test 3 Scope 1" }
        };
        
        var scope2Entities = new List<ChildEntity>
        {
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "ById Test 1 Scope 2" }, // Different ID, different scope
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "ById Test 2 Scope 2" }, // Different ID, different scope
            new() { Id = EntityIdGenerator.GenerateNewId(), Name = "ById Test 4 Scope 2" }
        };
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        // Act - Try to find entities by ID in each scope
        var scope1Results = await (await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id, 
            new[] { scope1Entities[0].Id, scope1Entities[1].Id, scope2Entities[2].Id })).ToListAsync();
            
        var scope2Results = await (await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_2.Id, 
            new[] { scope2Entities[0].Id, scope2Entities[1].Id, scope1Entities[2].Id })).ToListAsync();

        // Assert
        Assert.Equal(2, scope1Results.Count); // Should only find the 2 entities that exist in scope 1
        Assert.Equal(2, scope2Results.Count); // Should only find the 2 entities that exist in scope 2
        
        Assert.All(scope1Results, e => Assert.Contains("Scope 1", e.Name));
        Assert.All(scope2Results, e => Assert.Contains("Scope 2", e.Name));
        Assert.All(scope1Results, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        Assert.All(scope2Results, e => Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, e.Scope));
        
        // Verify specific entities found
        Assert.Contains(scope1Results, e => e.Id == scope1Entities[0].Id);
        Assert.Contains(scope1Results, e => e.Id == scope1Entities[1].Id);
        Assert.Contains(scope2Results, e => e.Id == scope2Entities[0].Id);
        Assert.Contains(scope2Results, e => e.Id == scope2Entities[1].Id);
    }

    [Fact]
    public async Task Random_Should_Only_Return_Entities_From_Specified_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>();
        var scope2Entities = new List<ChildEntity>();
        
        for (int i = 1; i <= 20; i++)
        {
            scope1Entities.Add(new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"Random Test Scope 1 - {i}" });
            scope2Entities.Add(new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = $"Random Test Scope 2 - {i}" });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        // Act
        var scope1Random = await (await ScopedReadonlyRepo.Random<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id, 
            predicate: null,
            count: 5)).ToListAsync();
            
        var scope2Random = await (await ScopedReadonlyRepo.Random<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_2.Id, 
            predicate: null,
            count: 3)).ToListAsync();

        // Assert
        Assert.Equal(5, scope1Random.Count);
        Assert.Equal(3, scope2Random.Count);
        
        Assert.All(scope1Random, e => Assert.Contains("Random Test Scope 1", e.Name));
        Assert.All(scope2Random, e => Assert.Contains("Random Test Scope 2", e.Name));
        Assert.All(scope1Random, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        Assert.All(scope2Random, e => Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, e.Scope));
    }

    [Fact]
    public async Task Complex_Multi_Scope_Operations_Should_Maintain_Data_Integrity()
    {
        // Arrange - Create 3 scopes
        var scope3 = new ParentScope { Id = EntityIdGenerator.GenerateNewId(), Name = "Parent Scope 3" };
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2, scope3 });
        
        // Act - Perform complex operations across multiple scopes using unique IDs
        
        // Insert entities into all 3 scopes with unique IDs
        var entity1 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Complex Test 1" };
        var entity2 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Complex Test 2" };
        var entity3 = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "Complex Test 3" };
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(scope3.Id, entity3);
        
        // Update entity in scope 2
        entity2.Name = "Updated Complex Test 2";
        await ScopedRepo.Update<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2);
        
        // Delete entity from scope 3
        await ScopedRepo.Delete<ChildEntity, ParentScope>(scope3.Id, entity3.Id);
        
        // Insert new entity into scope 3
        var entity3New = new ChildEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "New Complex Test 3" };
        await ScopedRepo.Insert<ChildEntity, ParentScope>(scope3.Id, entity3New);

        // Assert - Verify each scope has correct data
        var scope1Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entity1.Id);
        var scope2Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, entity2.Id);
        var scope3Entity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(scope3.Id, entity3New.Id);
        
        Assert.NotNull(scope1Entity);
        Assert.NotNull(scope2Entity);
        Assert.NotNull(scope3Entity);
        
        Assert.Equal("Complex Test 1", scope1Entity.Name);
        Assert.Equal("Updated Complex Test 2", scope2Entity.Name);
        Assert.Equal("New Complex Test 3", scope3Entity.Name);
        
        Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, scope1Entity.Scope);
        Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, scope2Entity.Scope);
        Assert.Equal(scope3.Id, scope3Entity.Scope);
        
        // Verify that the deleted entity is gone
        var deletedEntity = await ScopedReadonlyRepo.ById<ChildEntity, ParentScope>(scope3.Id, entity3.Id);
        Assert.Null(deletedEntity);
        
        // Verify total count
        var totalCount = await repo.Count<ChildEntity>(e => true);
        Assert.Equal(3, totalCount);
        
        // Verify each scope has exactly one entity
        var scope1Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, e => true);
        var scope2Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, e => true);
        var scope3Count = await ScopedReadonlyRepo.Count<ChildEntity, ParentScope>(scope3.Id, e => true);
        
        Assert.Equal(1, scope1Count);
        Assert.Equal(1, scope2Count);
        Assert.Equal(1, scope3Count);
    }
}
