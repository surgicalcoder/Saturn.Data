using Saturn.Data.Testing.Shared;

namespace Saturn.Data.DocumentDb.Tests;

public class ComprehensiveScopedTests(DatabaseFixture fixture)
    : ComprehensiveScopedRepositoryContractTests<DatabaseFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DatabaseFixture>;
