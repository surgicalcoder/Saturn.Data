namespace Saturn.Data.Sqlite.Tests;

public class SmokeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Json1_Is_Available()
    {
        var result = await fixture.Repository.ProbeJson1Async();
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task File_Database_Uses_Wal()
    {
        var mode = await fixture.Repository.ReadJournalModeAsync();
        Assert.Equal("wal", mode);
    }
}
