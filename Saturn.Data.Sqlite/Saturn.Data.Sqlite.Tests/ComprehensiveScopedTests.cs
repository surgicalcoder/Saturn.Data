using Saturn.Data.Testing.Shared;

namespace Saturn.Data.Sqlite.Tests;

public class ComprehensiveScopedTests(DatabaseFixture fixture)
    : ComprehensiveScopedRepositoryContractTests<DatabaseFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<DatabaseFixture>;
