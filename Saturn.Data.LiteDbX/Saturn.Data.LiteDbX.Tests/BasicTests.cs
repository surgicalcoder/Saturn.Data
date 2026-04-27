using Saturn.Data.Testing.Shared;

namespace Saturn.Data.LiteDbX.Tests;

public class BasicTests(DatabaseFixture fixture)
    : BasicRepositoryContractTests<DatabaseFixture, UnitTestableLiteDb>(fixture), IClassFixture<DatabaseFixture>;
