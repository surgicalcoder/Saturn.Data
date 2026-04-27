using Saturn.Data.Testing.Shared;

namespace Saturn.Data.LiteDbX.Tests;

public class ComprehensiveScopedTests(DatabaseFixture fixture)
    : ComprehensiveScopedRepositoryContractTests<DatabaseFixture, UnitTestableLiteDb>(fixture), IClassFixture<DatabaseFixture>;
