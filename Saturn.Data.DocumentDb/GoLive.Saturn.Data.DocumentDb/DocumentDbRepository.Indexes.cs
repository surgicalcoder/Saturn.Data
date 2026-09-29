using System.Linq.Expressions;
using System.Text.Json.Serialization.Metadata;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : IRepositoryIndexManager
{
    public async Task EnsureIndexes<TItem>(IEnumerable<IIndexDefinition<TItem>> definitions, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        foreach (var definition in definitions)
        {
            if (definition.Options.Unique)
            {
                documentDbOptions.OnUnsupportedIndexOption?.Invoke($"Unique index '{definition.Name}' requires per-type configuration (MapUniqueIndex) and was not created.");
                continue;
            }

            if (definition.Options.Sparse)
            {
                documentDbOptions.OnUnsupportedIndexOption?.Invoke($"Sparse index '{definition.Name}' is not supported and was created without a filter.");
            }

            if (definition.Options.HasExpireAfter)
            {
                documentDbOptions.OnUnsupportedIndexOption?.Invoke($"ExpireAfter is not supported for index '{definition.Name}'. The index was created without expiry.");
            }

            var keys = definition.Keys.Select(key => key.Field).ToList();

            if (keys.Count == 0)
            {
                continue;
            }

            await CreateIndexAsync<TItem>(keys, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CreateIndexAsync<TItem>(List<Expression<Func<TItem, object>>> keys, CancellationToken cancellationToken)
        where TItem : Entity
    {
        var method = store.GetType()
            .GetMethods()
            .FirstOrDefault(candidate => candidate.Name == "CreateIndexAsync" && candidate.IsGenericMethodDefinition && candidate.GetParameters().Length == 2);

        if (method is null)
        {
            documentDbOptions.OnUnsupportedIndexOption?.Invoke($"CreateIndexAsync is not available on backend '{capabilities.BackendName}'; indexes were skipped.");
            return;
        }

        if (Serializer.JsonOptions.GetTypeInfo(typeof(TItem)) is not JsonTypeInfo<TItem> typeInfo)
        {
            documentDbOptions.OnUnsupportedIndexOption?.Invoke($"No JsonTypeInfo for '{typeof(TItem).Name}'; indexes were skipped.");
            return;
        }

        var generic = method.MakeGenericMethod(typeof(TItem));
        var result = generic.Invoke(store, new object[] { typeInfo, keys });

        if (result is Task task)
        {
            await task.ConfigureAwait(false);
        }
    }
}
