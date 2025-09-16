using GoLive.Saturn.Data.Abstractions;

namespace Saturn.Data.Stellar.Tests;

public class UnitTestableDb(RepositoryOptions repositoryOptions, StellarRepositoryOptions repOptions)
    : StellarRepository(repositoryOptions, repOptions)
{
    public void DropRecreateDatabase()
    {
        database.Close();
        Directory.Delete("e:\\_scratch\\_unit_tests\\stellardb\\", true);
        database.Load();
    }
};