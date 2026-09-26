using Saturn.Data.Testing.Shared.Cascade;

namespace Saturn.Data.Sqlite.Tests;

public class CascadeTests(CascadeTestFixture fixture)
    : CascadeContractTests<CascadeTestFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<CascadeTestFixture>;
