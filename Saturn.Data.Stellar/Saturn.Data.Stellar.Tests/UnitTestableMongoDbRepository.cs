using GoLive.Saturn.Data.Abstractions;
using Stellar.Collections;

namespace Saturn.Data.Stellar.Tests;

public class UnitTestableDb(RepositoryOptions repositoryOptions, StellarRepositoryOptions repOptions)
    : StellarRepository(repositoryOptions, repOptions)
{
    public FastDB Database => database;

    public RepositoryOptions Options => options;

    public void DropRecreateDatabase()
    {
        database.Close();
        Directory.Delete("e:\\_scratch\\_unit_tests\\stellardb\\", true);
        database.Load();
    }
};