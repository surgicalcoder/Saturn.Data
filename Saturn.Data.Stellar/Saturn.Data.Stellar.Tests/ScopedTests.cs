using Saturn.Data.Testing.Shared;

namespace Saturn.Data.Stellar.Tests;

public class ScopedTests(DatabaseFixture fixture)
    : ScopedRepositoryContractTests<DatabaseFixture, UnitTestableDb>(fixture), IClassFixture<DatabaseFixture>;
