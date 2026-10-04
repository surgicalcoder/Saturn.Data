using System;

namespace Saturn.Data.Stellar.Tests;

public class Phase5MigrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private readonly UnitTestableDb repository = fixture.Repository;

    [Fact]
    public void CreateMigrationStore_IsUnsupportedWithActionableMessage()
    {
        var exception = Assert.Throws<NotSupportedException>(() => repository.CreateMigrationStore());

        Assert.Contains("Stellar", exception.Message);
        Assert.Contains("raw-document migrations", exception.Message);
    }
}
