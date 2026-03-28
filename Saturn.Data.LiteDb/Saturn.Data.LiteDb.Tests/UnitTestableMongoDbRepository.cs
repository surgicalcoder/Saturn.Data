using GoLive.Saturn.Data.Abstractions;
using LiteDbX;

namespace Saturn.Data.LiteDb.Tests;

public class UnitTestableLiteDb(RepositoryOptions repositoryOptions, LiteDBRepositoryOptions liteDbRepositoryOptions)
    : LiteDbRepository(repositoryOptions, liteDbRepositoryOptions)
{
    public BsonDocument SerializeToDocument<TItem>(TItem entity)
    {
        return mapper.Serialize(typeof(TItem), entity).AsDocument;
    }

    public void DropRecreateDatabase()
    {
        database.Dispose();
        File.Delete("e:\\_scratch\\litedb-unit-tests.db");
        database = new LiteDatabase(liteDbOptions.ConnectionString, mapper);
    }
};