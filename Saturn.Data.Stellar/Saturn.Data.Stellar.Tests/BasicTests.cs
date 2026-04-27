using Saturn.Data.Testing.Shared;

namespace Saturn.Data.Stellar.Tests;

public class BasicTests(DatabaseFixture fixture)
    : BasicRepositoryContractTests<DatabaseFixture, UnitTestableDb>(fixture), IClassFixture<DatabaseFixture>;
