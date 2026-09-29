using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb.Tests;

public sealed class UnitTestableDocumentDbRepository : DocumentDbRepository
{
    public UnitTestableDocumentDbRepository(RepositoryOptions repositoryOptions, DocumentDbRepositoryOptions documentDbOptions)
        : base(repositoryOptions, documentDbOptions)
    {
    }

    public async Task<long> CountRawAsync<TItem>(CancellationToken cancellationToken = default) where TItem : Entity
        => await Store.Query<TItem>().Count();
}
