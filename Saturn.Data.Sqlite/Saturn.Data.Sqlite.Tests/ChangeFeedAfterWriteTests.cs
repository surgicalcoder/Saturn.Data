using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class ChangeFeedAfterWriteTests(ChangeFeedTestFixture fixture)
    : ChangeFeedContractTests<ChangeFeedTestFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<ChangeFeedTestFixture>;
