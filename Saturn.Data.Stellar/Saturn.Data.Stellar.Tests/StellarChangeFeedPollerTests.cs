using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Stellar.Tests;

public class StellarChangeFeedPollerTests(ChangeFeedTestFixture fixture)
    : ChangeFeedPollerTests<ChangeFeedTestFixture, UnitTestableDb>(fixture), IClassFixture<ChangeFeedTestFixture>;
