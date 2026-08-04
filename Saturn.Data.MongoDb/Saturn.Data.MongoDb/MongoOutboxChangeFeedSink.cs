using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public class MongoOutboxChangeFeedSink : OutboxChangeFeedSink
{
    private const string FeedCollection = "__change_feed";
    private const string CounterCollection = "__change_feed_counters";

    private readonly IMongoDatabase database;

    static MongoOutboxChangeFeedSink()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(ChangeFeedRecord)))
        {
            BsonClassMap.RegisterClassMap<ChangeFeedRecord>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(r => r.Id).SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }

    public MongoOutboxChangeFeedSink(IMongoDatabase database, string source)
        : base(source)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
    }

    private IMongoCollection<ChangeFeedRecord> Feed => database.GetCollection<ChangeFeedRecord>(FeedCollection);

    private IMongoCollection<BsonDocument> Counters => database.GetCollection<BsonDocument>(CounterCollection);

    protected override async ValueTask<long> NextSequenceAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var counterId = $"{change.Source}|{change.EntityType.FullName}";
        var filter = Builders<BsonDocument>.Filter.Eq("_id", counterId);
        var update = Builders<BsonDocument>.Update.Inc("seq", 1L).SetOnInsert("_id", counterId);
        var options = new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, IsUpsert = true };

        var result = transaction is MongoDbTransactionWrapper wrapper
            ? await Counters.FindOneAndUpdateAsync(wrapper.Session, filter, update, options, ct)
            : await Counters.FindOneAndUpdateAsync(filter, update, options, ct);

        return result["seq"].AsInt64;
    }

    protected override async ValueTask AppendRowAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var record = ChangeFeedRecordMapper.ToRecord(change);

        if (transaction is MongoDbTransactionWrapper wrapper)
        {
            await Feed.InsertOneAsync(wrapper.Session, record, null, ct);
        }
        else
        {
            await Feed.InsertOneAsync(record, null, ct);
        }
    }

    protected override async ValueTask<IReadOnlyList<DataChangeEvent>> ReadRowsAsync(string source, long afterSequence, int take, CancellationToken ct)
    {
        var filter = Builders<ChangeFeedRecord>.Filter.And(
            Builders<ChangeFeedRecord>.Filter.Eq(x => x.Source, source),
            Builders<ChangeFeedRecord>.Filter.Gt(x => x.Sequence, afterSequence));

        var sort = Builders<ChangeFeedRecord>.Sort.Ascending(x => x.Sequence);

        var cursor = await Feed.Find(filter).Sort(sort).Limit(take).ToCursorAsync(ct);
        var records = await cursor.ToListAsync(ct);

        return records.Select(ChangeFeedRecordMapper.ToEvent).ToList();
    }
}