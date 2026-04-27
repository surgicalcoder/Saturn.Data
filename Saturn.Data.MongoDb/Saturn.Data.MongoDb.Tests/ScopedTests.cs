using Saturn.Data.Testing.Shared;

namespace Saturn.Data.MongoDb.Tests;

public class ScopedTests(DatabaseFixture fixture)
    : ScopedRepositoryContractTests<DatabaseFixture, UnitTestableMongoDbRepository>(fixture), IClassFixture<DatabaseFixture>;
