using Saturn.Data.Testing.Shared.ChangeTracking;

namespace Saturn.Data.Stellar.Tests;

public class ChangeSetPatchTests(DatabaseFixture fixture)
    : ChangeSetPatchContractTests<DatabaseFixture, UnitTestableDb>(fixture), IClassFixture<DatabaseFixture>;
