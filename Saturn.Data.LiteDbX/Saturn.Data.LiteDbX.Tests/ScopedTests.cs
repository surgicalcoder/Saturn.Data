using Saturn.Data.Testing.Shared;

namespace Saturn.Data.LiteDbX.Tests;

public class ScopedTests(DatabaseFixture fixture)
    : ScopedRepositoryContractTests<DatabaseFixture, UnitTestableLiteDb>(fixture), IClassFixture<DatabaseFixture>;
