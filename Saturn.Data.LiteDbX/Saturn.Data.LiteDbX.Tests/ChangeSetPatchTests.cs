using Saturn.Data.Testing.Shared.ChangeTracking;

namespace Saturn.Data.LiteDbX.Tests;

public class ChangeSetPatchTests(DatabaseFixture fixture)
    : ChangeSetPatchContractTests<DatabaseFixture, UnitTestableLiteDb>(fixture), IClassFixture<DatabaseFixture>;
