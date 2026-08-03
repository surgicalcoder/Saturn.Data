using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.MongoDb.Tests;

public class ChangeFeedAfterWriteTests(ChangeFeedTestFixture fixture)
    : ChangeFeedContractTests<ChangeFeedTestFixture, UnitTestableMongoDbRepository>(fixture), IClassFixture<ChangeFeedTestFixture>;
