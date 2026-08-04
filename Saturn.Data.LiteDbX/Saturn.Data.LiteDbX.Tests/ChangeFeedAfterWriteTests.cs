using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.LiteDbX.Tests;

[Collection("ChangeFeed")]
public class ChangeFeedAfterWriteTests(ChangeFeedTestFixture fixture)
    : ChangeFeedContractTests<ChangeFeedTestFixture, UnitTestableLiteDb>(fixture);
