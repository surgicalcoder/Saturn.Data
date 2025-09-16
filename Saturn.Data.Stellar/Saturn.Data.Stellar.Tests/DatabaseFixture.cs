using GoLive.Saturn.Data.Abstractions;

namespace Saturn.Data.Stellar.Tests;

public class DatabaseFixture : IDisposable
{
    public UnitTestableDb Repository { get; }

    public DatabaseFixture()
    {
        var baseDirectory = "e:\\_scratch\\_unit_tests\\stellardb\\";
        Directory.CreateDirectory(baseDirectory);
        Repository = new UnitTestableDb(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name
        }, new ()
        {
            BaseDirectory = baseDirectory,
            DatabaseName = $"Stellar_{DateTime.UtcNow.ToString("O").Replace(":","_").Replace(".","_").Replace("-","_") }"
        });
        
        // Initialize database once
        Repository.DropRecreateDatabase();
    }

    public void Dispose()
    {
        Repository.DropRecreateDatabase();
    }
}
