using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class SqliteChangeFeedPollerTests(ChangeFeedTestFixture fixture)
    : ChangeFeedPollerTests<ChangeFeedTestFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<ChangeFeedTestFixture>;
