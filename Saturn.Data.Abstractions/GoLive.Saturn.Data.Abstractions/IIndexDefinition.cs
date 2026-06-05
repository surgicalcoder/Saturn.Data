using System.Collections.Generic;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IIndexDefinition<TItem> where TItem : Entity
{
    string Name { get; }

    IReadOnlyCollection<IIndexKey<TItem>> Keys { get; }

    IndexOptions Options { get; }
}

