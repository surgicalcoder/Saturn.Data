using Saturn.Data.Testing.Shared;

namespace Saturn.Data.Sqlite.Tests;

public class ScopedTests(DatabaseFixture fixture)
    : ScopedRepositoryContractTests<DatabaseFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<DatabaseFixture>;
