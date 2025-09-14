using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository
{
    public async Task Watch<TItem>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var pipelineDefinition = new EmptyPipelineDefinition<ChangeStreamDocument<TItem>>();

        var expression = Converter<ChangeStreamDocument<TItem>>.Convert(predicate);

        var opType = (ChangeStreamOperationType)operationFilter;

        var definition = pipelineDefinition.Match(expression).Match(e => e.OperationType == opType);

        if (transaction != null)
        {
            await GetCollection<TItem>().WatchAsync(((MongoDbTransactionWrapper)transaction).Session, definition, cancellationToken: cancellationToken);
        }
        else
        {
            await GetCollection<TItem>().WatchAsync(definition, cancellationToken: cancellationToken);
        }

        var collection = GetCollection<TItem>();

        using (var asyncCursor = await collection.WatchAsync(pipelineDefinition, cancellationToken: cancellationToken))
        {
            foreach (var changeStreamDocument in asyncCursor.ToEnumerable())
            {
                callback.Invoke(changeStreamDocument.FullDocument, changeStreamDocument?.DocumentKey[0]?.AsObjectId.ToString(), (ChangeOperation)changeStreamDocument.OperationType);
            }
        }
    }
}