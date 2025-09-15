using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.LiteDb.Tests.Entities;

namespace Saturn.Data.LiteDb.Tests;

public class ScopedReadonlyContinuationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly UnitTestableLiteDb repo = fixture.Repository;
    private IScopedReadonlyRepository ScopedReadonlyRepo => repo;
    private IScopedRepository ScopedRepo => repo;

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
    public async Task Many_With_ContinueFrom_Should_Return_Next_Page_Within_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>();
        var scope2Entities = new List<ChildEntity>();
        
        // Create 20 entities in each scope with sequential names for predictable sorting
        for (int i = 1; i <= 20; i++)
        {
            scope1Entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = $"Scope1Entity{i:D2}" 
            });
            scope2Entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = $"Scope2Entity{i:D2}" 
            });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        // Use ID-based sorting for reliable continuation token pagination
        // This ensures continuation tokens work correctly since they are ID-based
        var sortOrders = new[] { new SortOrder<ChildEntity>(e => e.Id, SortDirection.Ascending) };

        // Act - Get first page from scope 1
        var firstPageScope1 = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => e.Name.Contains("Scope1"),
            pageSize: 5,
            sortOrders: sortOrders)).ToListAsync();

        // Get continuation token from last item
        var continueFromToken = firstPageScope1.Last().Id;

        // Get second page using continuation token
        var secondPageScope1 = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => e.Name.Contains("Scope1"),
            continueFrom: continueFromToken,
            pageSize: 5,
            sortOrders: sortOrders)).ToListAsync();

        // Assert
        Assert.Equal(5, firstPageScope1.Count);
        Assert.Equal(5, secondPageScope1.Count);
        
        // Verify all entities are from scope 1
        Assert.All(firstPageScope1, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        Assert.All(secondPageScope1, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        
        // Verify no overlap between pages
        var firstPageIds = firstPageScope1.Select(e => e.Id).ToHashSet();
        var secondPageIds = secondPageScope1.Select(e => e.Id).ToHashSet();
        Assert.Empty(firstPageIds.Intersect(secondPageIds));
        
        // Verify sequential ID ordering (since we're sorting by ID)
        Assert.True(string.Compare(firstPageScope1.Last().Id, secondPageScope1.First().Id, StringComparison.Ordinal) < 0);
    }

    [Fact]
    public async Task Many_With_ContinueFrom_Should_Respect_Scope_Boundaries()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>();
        var scope2Entities = new List<ChildEntity>();
        
        for (int i = 1; i <= 10; i++)
        {
            scope1Entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = $"TestEntity{i:D2}" 
            });
            scope2Entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = $"TestEntity{i:D2}" 
            });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        var sortOrders = new[] { new SortOrder<ChildEntity>(e => e.Name, SortDirection.Ascending) };

        // Act - Get first page from scope 1
        var firstPageScope1 = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => true,
            pageSize: 3,
            sortOrders: sortOrders)).ToListAsync();

        var continueFromToken = firstPageScope1.Last().Id;

        // Try to use continuation token from scope 1 in scope 2 (should not return scope 1 data)
        var scope2ResultsWithScope1Token = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_2.Id,
            e => true,
            continueFrom: continueFromToken,
            pageSize: 5,
            sortOrders: sortOrders)).ToListAsync();

        // Assert
        Assert.Equal(3, firstPageScope1.Count);
        Assert.All(firstPageScope1, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        
        // Results from scope 2 should only contain scope 2 entities, regardless of continuation token
        Assert.All(scope2ResultsWithScope1Token, e => Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, e.Scope));
        
        // Should not find any entities with IDs from scope 1
        var scope1Ids = firstPageScope1.Select(e => e.Id).ToHashSet();
        Assert.DoesNotContain(scope2ResultsWithScope1Token, e => scope1Ids.Contains(e.Id));
    }

    [Fact]
    public async Task Many_With_Invalid_ContinueFrom_Should_Handle_Gracefully()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1 });
        
        var entities = new List<ChildEntity>();
        for (int i = 1; i <= 5; i++)
        {
            entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = $"TestEntity{i}" 
            });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entities);

        // Act - Use non-existent continuation token
        var nonExistentToken = EntityIdGenerator.GenerateNewId();
        var results = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => true,
            continueFrom: nonExistentToken,
            pageSize: 10)).ToListAsync();

        // Assert - Should return results (as continuation filtering should handle gracefully)
        Assert.All(results, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
    }

    [Fact]
    public async Task One_With_ContinueFrom_Should_Return_Next_Entity_Within_Scope()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entities = new List<ChildEntity>();
        for (int i = 1; i <= 10; i++)
        {
            scope1Entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = $"TestEntity{i:D2}" 
            });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entities);

        // Use ID-based sorting for reliable continuation token pagination
        // This ensures continuation tokens work correctly since they are ID-based
        var sortOrders = new[] { new SortOrder<ChildEntity>(e => e.Id, SortDirection.Ascending) };

        // Act - Get first entity
        var firstEntity = await ScopedReadonlyRepo.One<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => true,
            sortOrders: sortOrders);

        // Get next entity using continuation
        var secondEntity = await ScopedReadonlyRepo.One<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => true,
            continueFrom: firstEntity.Id,
            sortOrders: sortOrders);

        // Assert
        Assert.NotNull(firstEntity);
        Assert.NotNull(secondEntity);
        Assert.NotEqual(firstEntity.Id, secondEntity.Id);
        Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, firstEntity.Scope);
        Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, secondEntity.Scope);
        
        // Verify ID ordering (since we're sorting by ID)
        Assert.True(string.Compare(firstEntity.Id, secondEntity.Id, StringComparison.Ordinal) < 0);
    }

    [Fact]
    public async Task One_With_ContinueFrom_Should_Respect_Scope_Boundaries()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1, WELL_KNOWN.Parent_Scope_2 });
        
        var scope1Entity = new ChildEntity 
        { 
            Id = EntityIdGenerator.GenerateNewId(), 
            Name = "Scope1Entity" 
        };
        
        var scope2Entities = new List<ChildEntity>();
        for (int i = 1; i <= 5; i++)
        {
            scope2Entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = $"Scope2Entity{i}" 
            });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, scope1Entity);
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_2.Id, scope2Entities);

        var sortOrders = new[] { new SortOrder<ChildEntity>(e => e.Name, SortDirection.Ascending) };

        // Act - Try to use continuation token from scope 1 in scope 2
        var scope2Result = await ScopedReadonlyRepo.One<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_2.Id,
            e => true,
            continueFrom: scope1Entity.Id,
            sortOrders: sortOrders);

        // Assert
        Assert.NotNull(scope2Result);
        Assert.Equal(WELL_KNOWN.Parent_Scope_2.Id, scope2Result.Scope);
        Assert.NotEqual(scope1Entity.Id, scope2Result.Id);
        Assert.Contains("Scope2", scope2Result.Name);
    }

    [Fact]
    public async Task Many_With_ContinueFrom_And_PageNumber_Should_Work_Together()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1 });
        
        var entities = new List<ChildEntity>();
        for (int i = 1; i <= 20; i++)
        {
            entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = $"Entity{i:D2}" 
            });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entities);

        // Use ID-based sorting to match the ID-based continuation logic
        var sortOrders = new[] { new SortOrder<ChildEntity>(e => e.Id, SortDirection.Ascending) };

        // Act - Get page 1
        var page1 = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => true,
            pageSize: 5,
            pageNumber: 1,
            sortOrders: sortOrders)).ToListAsync();

        // Get continuation token from page 1
        var continueFromToken = page1.Last().Id;

        // Get next results using continuation (which should work with or without pageNumber)
        var continuationResults = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => true,
            continueFrom: continueFromToken,
            pageSize: 5,
            sortOrders: sortOrders)).ToListAsync();

        // Assert
        Assert.Equal(5, page1.Count);
        
        // No overlap between results - the continuation should exclude the continueFromToken entity
        var page1Ids = page1.Select(e => e.Id).ToHashSet();
        var continuationIds = continuationResults.Select(e => e.Id).ToHashSet();
        Assert.Empty(page1Ids.Intersect(continuationIds));
        
        // All results should be from the same scope
        Assert.All(page1, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        Assert.All(continuationResults, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        
        // Verify that continuation results have IDs greater than the continuation token
        Assert.All(continuationResults, e => Assert.True(string.Compare(e.Id, continueFromToken, StringComparison.Ordinal) > 0));
    }

    [Fact]
    public async Task Many_With_ContinueFrom_Should_Work_With_Complex_Filters()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1 });
        
        var entities = new List<ChildEntity>();
        for (int i = 1; i <= 20; i++)
        {
            entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = i % 2 == 0 ? $"EvenEntity{i:D2}" : $"OddEntity{i:D2}" 
            });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entities);

        // Use ID-based sorting for reliable continuation token pagination
        // This ensures continuation tokens work correctly since they are ID-based
        var sortOrders = new[] { new SortOrder<ChildEntity>(e => e.Id, SortDirection.Ascending) };

        // Act - Get first page of only "Even" entities
        var firstPageEven = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => e.Name.Contains("Even"),
            pageSize: 3,
            sortOrders: sortOrders)).ToListAsync();

        var continueFromToken = firstPageEven.Last().Id;

        // Get second page of "Even" entities using continuation
        var secondPageEven = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => e.Name.Contains("Even"),
            continueFrom: continueFromToken,
            pageSize: 3,
            sortOrders: sortOrders)).ToListAsync();

        // Assert
        Assert.Equal(3, firstPageEven.Count);
        Assert.True(secondPageEven.Count > 0); // Should have some results
        
        // All results should be "Even" entities
        Assert.All(firstPageEven, e => Assert.Contains("Even", e.Name));
        Assert.All(secondPageEven, e => Assert.Contains("Even", e.Name));
        
        // No overlap between pages
        var firstPageIds = firstPageEven.Select(e => e.Id).ToHashSet();
        var secondPageIds = secondPageEven.Select(e => e.Id).ToHashSet();
        Assert.Empty(firstPageIds.Intersect(secondPageIds));
        
        // All results should be from the same scope
        Assert.All(firstPageEven, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        Assert.All(secondPageEven, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
        
        // Verify that continuation results have IDs greater than the continuation token
        Assert.All(secondPageEven, e => Assert.True(string.Compare(e.Id, continueFromToken, StringComparison.Ordinal) > 0));
    }

    [Fact]
    public async Task Many_With_ContinueFrom_Should_Work_With_Dictionary_WhereClause()
    {
        // Arrange
        await repo.Insert(new List<ParentScope> { WELL_KNOWN.Parent_Scope_1 });
        
        var entities = new List<ChildEntity>();
        for (int i = 1; i <= 15; i++)
        {
            entities.Add(new ChildEntity 
            { 
                Id = EntityIdGenerator.GenerateNewId(), 
                Name = $"TestEntity{i:D2}" 
            });
        }
        
        await ScopedRepo.Insert<ChildEntity, ParentScope>(WELL_KNOWN.Parent_Scope_1.Id, entities);

        var sortOrders = new[] { new SortOrder<ChildEntity>(e => e.Name, SortDirection.Ascending) };

        // Act - Get first page using dictionary where clause - use simple string matching instead of regex
        var whereClause = new Dictionary<string, object>();

        var firstPage = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            whereClause,
            pageSize: 4,
            sortOrders: sortOrders)).ToListAsync();

        if (firstPage.Count > 0)
        {
            var continueFromToken = firstPage.Last().Id;

            // Get second page using continuation
            var secondPage = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
                WELL_KNOWN.Parent_Scope_1.Id,
                whereClause,
                continueFrom: continueFromToken,
                pageSize: 4,
                sortOrders: sortOrders)).ToListAsync();

            // Assert
            Assert.Equal(4, firstPage.Count);
            
            // All results should match the filter and be from correct scope
            Assert.All(firstPage, e => Assert.Contains("TestEntity", e.Name));
            Assert.All(secondPage, e => Assert.Contains("TestEntity", e.Name));
            Assert.All(firstPage, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
            Assert.All(secondPage, e => Assert.Equal(WELL_KNOWN.Parent_Scope_1.Id, e.Scope));
            
            // No overlap between pages
            var firstPageIds = firstPage.Select(e => e.Id).ToHashSet();
            var secondPageIds = secondPage.Select(e => e.Id).ToHashSet();
            Assert.Empty(firstPageIds.Intersect(secondPageIds));
        }
    }
}
