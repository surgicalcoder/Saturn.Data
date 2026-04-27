using Saturn.Data.Testing.Shared;

namespace Saturn.Data.MongoDb.Tests;

public class BasicTests(DatabaseFixture fixture)
    : BasicRepositoryContractTests<DatabaseFixture, UnitTestableMongoDbRepository>(fixture), IClassFixture<DatabaseFixture>;
