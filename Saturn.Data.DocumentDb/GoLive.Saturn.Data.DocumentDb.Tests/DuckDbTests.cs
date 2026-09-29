using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.Cascade;
using Saturn.Data.Testing.Shared.ChangeFeed;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.DocumentDb.Tests;

[Collection("DuckDb")]
public class DuckDbBasicTests(DuckDbDatabaseFixture fixture)
    : BasicRepositoryContractTests<DuckDbDatabaseFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DuckDbDatabaseFixture>;

[Collection("DuckDb")]
public class DuckDbScopedTests(DuckDbDatabaseFixture fixture)
    : ScopedRepositoryContractTests<DuckDbDatabaseFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DuckDbDatabaseFixture>;

[Collection("DuckDb")]
public class DuckDbComprehensiveScopedTests(DuckDbDatabaseFixture fixture)
    : ComprehensiveScopedRepositoryContractTests<DuckDbDatabaseFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DuckDbDatabaseFixture>;

[Collection("DuckDb")]
public class DuckDbCascadeTests(DuckDbCascadeTestFixture fixture)
    : CascadeContractTests<DuckDbCascadeTestFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DuckDbCascadeTestFixture>;

[Collection("DuckDb")]
public class DuckDbChangeFeedAfterWriteTests(DuckDbChangeFeedTestFixture fixture)
    : ChangeFeedContractTests<DuckDbChangeFeedTestFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DuckDbChangeFeedTestFixture>;

[Collection("DuckDb")]
public class DuckDbChangeFeedPollerTests(DuckDbChangeFeedTestFixture fixture)
    : ChangeFeedPollerTests<DuckDbChangeFeedTestFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DuckDbChangeFeedTestFixture>;

[Collection("DuckDb")]
public class DuckDbProviderTests(DuckDbDatabaseFixture fixture) : IClassFixture<DuckDbDatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
        await fixture.Repository.HardDelete<ChangeFeedEntity>(entity => true);
    }

    [Fact]
    public void Backend_Is_Detected()
    {
        Assert.Equal("DuckDbDatabaseProvider", fixture.Repository.Capabilities.BackendName);
        Assert.True(fixture.Repository.Capabilities.RequiresSingleConnection);
    }

    [Fact]
    public async Task Crud_And_Query_Work()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "duck" };
        await fixture.Repository.Insert(entity);

        var fetched = await fixture.Repository.ById<BasicEntity>(entity.Id);
        Assert.NotNull(fetched);
        Assert.Equal("duck", fetched!.Name);

        var count = await fixture.Repository.Count<BasicEntity>(item => item.Name == "duck");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SoftDelete_Works()
    {
        var entity = new ChangeFeedEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "duck-soft" };
        await fixture.Repository.Insert(entity);
        await fixture.Repository.Delete<ChangeFeedEntity>(entity.Id);

        Assert.Null(await fixture.Repository.ById<ChangeFeedEntity>(entity.Id));
        Assert.NotNull(await fixture.Repository.ById<ChangeFeedEntity>(entity.Id, includeDeleted: true));
    }
}
