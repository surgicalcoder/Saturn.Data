using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb.Tests;

public class UnitTestableMongoDbRepository(RepositoryOptions repositoryOptions, MongoDbRepositoryOptions mongoDbRepositoryOptions)
    : MongoDbRepository(repositoryOptions, mongoDbRepositoryOptions)
{
    public void DropRecreateDatabase()
    {
        var databaseName = mongoDatabase.DatabaseNamespace.DatabaseName;
        client.DropDatabase(databaseName);
        _ = client.GetDatabase(databaseName);
    }

    /// <summary>
    /// Gets the raw BSON collection for testing purposes (to inspect serialization)
    /// </summary>
    public IMongoCollection<BsonDocument> GetRawCollection<T>() where T : Entity
    {
        return mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<T>());
    }

    /// <summary>
    /// Runs a LINQ predicate directly against MongoDB WITHOUT the NormalizeForRef() rewriter.
    /// This is used to prove that Ref&lt;T&gt;.Id comparisons fail without the fix.
    /// </summary>
    public async Task<List<T>> ManyWithoutNormalization<T>(Expression<Func<T, bool>> predicate) where T : Entity
    {
        var filter = Builders<T>.Filter.Where(predicate);
        var cursor = await GetCollection<T>().FindAsync(filter);
        return await cursor.ToListAsync();
    }
}
