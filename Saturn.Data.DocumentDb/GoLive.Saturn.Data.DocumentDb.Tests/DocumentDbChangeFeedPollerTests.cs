using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.DocumentDb.Tests;

public class DocumentDbChangeFeedPollerTests(ChangeFeedTestFixture fixture)
    : ChangeFeedPollerTests<ChangeFeedTestFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<ChangeFeedTestFixture>;
