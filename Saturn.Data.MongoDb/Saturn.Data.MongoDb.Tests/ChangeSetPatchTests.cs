using Saturn.Data.Testing.Shared.ChangeTracking;

namespace Saturn.Data.MongoDb.Tests;

public class ChangeSetPatchTests(DatabaseFixture fixture)
    : ChangeSetPatchContractTests<DatabaseFixture, UnitTestableMongoDbRepository>(fixture), IClassFixture<DatabaseFixture>;
