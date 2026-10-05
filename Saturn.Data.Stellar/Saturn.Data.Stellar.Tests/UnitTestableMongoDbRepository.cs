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

        var path = repOptions.BaseDirectory;

        if (!string.IsNullOrWhiteSpace(path))
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }

        database.Load();
    }
};