using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public sealed class MongoDataUpdateDefinition<TItem>(UpdateDefinition<TItem> definition)
    : IDataUpdateDefinition<TItem>
    where TItem : Entity
{
    public UpdateDefinition<TItem> Definition { get; } = definition ?? throw new ArgumentNullException(nameof(definition));
}

