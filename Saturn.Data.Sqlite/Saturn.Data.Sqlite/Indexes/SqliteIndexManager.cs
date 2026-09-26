using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Sqlite.Query;

namespace Saturn.Data.Sqlite.Indexes;

internal sealed class SqliteIndexManager
{
    private readonly SqliteRepository repository;
    private readonly SqliteRepositoryOptions options;

    public SqliteIndexManager(SqliteRepository repository, SqliteRepositoryOptions options)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task EnsureIndexesAsync<TItem>(IEnumerable<IIndexDefinition<TItem>> definitions, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await repository.ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var collection = repository.CollectionNameForType(typeof(TItem));
        await repository.EnsureTableAsync(collection, connection, cancellationToken).ConfigureAwait(false);

        var table = SqliteRepository.Quote(collection);
        var position = 0;

        foreach (var definition in definitions)
        {
            if (definition.Options.HasExpireAfter)
            {
                options.OnUnsupportedIndexOption?.Invoke($"ExpireAfter is not supported for index '{definition.Name}'. The index was created without expiry.");
            }

            var clause = BuildIndexClause(definition);
            var unique = definition.Options.Unique ? "UNIQUE " : string.Empty;
            var name = definition.Name;

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"ix_{collection}_{position}";
            }

            var sparse = definition.Options.Sparse ? $" WHERE {BuildSparsePredicate(definition)}" : string.Empty;

            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE {unique}INDEX IF NOT EXISTS {SqliteRepository.Quote(name)} ON {table}({clause}){sparse};";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            position++;
        }
    }

    private static string BuildIndexClause<TItem>(IIndexDefinition<TItem> definition) where TItem : Entity
    {
        var parts = new List<string>();

        foreach (var key in definition.Keys)
        {
            var operand = ResolveOperand(key.Field);
            var direction = key.Direction == IndexSortDirection.Ascending ? "ASC" : "DESC";
            parts.Add($"{operand} {direction}");
        }

        if (parts.Count == 0)
        {
            throw new ArgumentException($"Index '{definition.Name}' has no keys.");
        }

        return string.Join(", ", parts);
    }

    private static string BuildSparsePredicate<TItem>(IIndexDefinition<TItem> definition) where TItem : Entity
        => $"{ResolveOperand(definition.Keys.First().Field)} IS NOT NULL";

    private static string ResolveOperand<TItem>(System.Linq.Expressions.Expression<Func<TItem, object>> field) where TItem : Entity
    {
        if (!SqliteJsonPathResolver.TryResolve(SqliteJsonPathResolver.Unwrap(field.Body), out var path, out var isColumn))
        {
            throw new SqliteTranslationException($"Cannot resolve index key '{field}'.");
        }

        return isColumn ? path : $"json_extract(_doc, '{path}')";
    }
}
