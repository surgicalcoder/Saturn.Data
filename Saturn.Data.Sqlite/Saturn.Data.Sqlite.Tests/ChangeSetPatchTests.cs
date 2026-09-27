using Saturn.Data.Testing.Shared.ChangeTracking;

namespace Saturn.Data.Sqlite.Tests;

public class ChangeSetPatchTests(DatabaseFixture fixture)
    : ChangeSetPatchContractTests<DatabaseFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<DatabaseFixture>;
