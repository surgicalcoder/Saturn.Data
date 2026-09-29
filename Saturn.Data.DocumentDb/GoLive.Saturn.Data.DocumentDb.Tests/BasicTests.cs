using Saturn.Data.Testing.Shared;

namespace Saturn.Data.DocumentDb.Tests;

public class BasicTests(DatabaseFixture fixture)
    : BasicRepositoryContractTests<DatabaseFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DatabaseFixture>;
