using Saturn.Data.Testing.Shared;

namespace Saturn.Data.DocumentDb.Tests;

public class ScopedTests(DatabaseFixture fixture)
    : ScopedRepositoryContractTests<DatabaseFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DatabaseFixture>;
