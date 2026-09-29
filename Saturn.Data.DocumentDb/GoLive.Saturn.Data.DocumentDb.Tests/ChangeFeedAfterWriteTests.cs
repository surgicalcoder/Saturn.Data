using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.DocumentDb.Tests;

public class ChangeFeedAfterWriteTests(ChangeFeedTestFixture fixture)
    : ChangeFeedContractTests<ChangeFeedTestFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<ChangeFeedTestFixture>;
