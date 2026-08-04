using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.MongoDb.Tests;

public class MongoChangeFeedPollerTests(ChangeFeedTestFixture fixture)
    : ChangeFeedPollerTests<ChangeFeedTestFixture, UnitTestableMongoDbRepository>(fixture), IClassFixture<ChangeFeedTestFixture>;
