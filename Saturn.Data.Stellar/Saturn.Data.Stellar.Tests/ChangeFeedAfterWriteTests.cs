using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Stellar.Tests;

public class ChangeFeedAfterWriteTests(ChangeFeedTestFixture fixture)
    : ChangeFeedContractTests<ChangeFeedTestFixture, UnitTestableDb>(fixture), IClassFixture<ChangeFeedTestFixture>;
