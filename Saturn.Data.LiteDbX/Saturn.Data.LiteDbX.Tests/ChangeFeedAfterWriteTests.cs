using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.LiteDbX.Tests;

public class ChangeFeedAfterWriteTests(ChangeFeedTestFixture fixture)
    : ChangeFeedContractTests<ChangeFeedTestFixture, UnitTestableLiteDb>(fixture), IClassFixture<ChangeFeedTestFixture>;
