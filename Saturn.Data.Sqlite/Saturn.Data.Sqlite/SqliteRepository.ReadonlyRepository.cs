using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Microsoft.Data.Sqlite;
using Saturn.Data.Sqlite.Query;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IReadonlyRepository
{
    private SqlFragment BuildReadPredicate<TItem>(Expression<Func<TItem, bool>>? predicate, bool includeDeleted)
        where TItem : Entity
    {
        var fragment = predicate is null
            ? SqlFragment.AlwaysTrue
            : new SqliteExpressionTranslator().Translate(predicate);

        if (!includeDeleted && SupportsSoftDelete<TItem>())
        {
            fragment = SqlFragment.Combine(fragment, new SqlFragment { Sql = "_deleted = 0" }, "AND");
        }

        return fragment;
    }

    private async Task<List<TItem>> LoadListAsync<TItem>(SqlFragment predicate, string? orderBy, int? limit, int? offset,
        IDatabaseTransaction? transaction, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

        var table = Quote(GetCollectionNameForType<TItem>());

        var builder = new StringBuilder($"SELECT _doc FROM {table}");

        if (!string.IsNullOrWhiteSpace(predicate.Sql))
        {
            builder.Append($" WHERE {predicate.Sql}");
        }

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            builder.Append($" ORDER BY {orderBy}");
        }

        if (limit.HasValue)
        {
            builder.Append(" LIMIT @limit");
        }

        if (offset.HasValue)
        {
            builder.Append(" OFFSET @offset");
        }

        builder.Append(';');

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = builder.ToString();

        foreach (var parameter in predicate.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        if (limit.HasValue)
        {
            command.Parameters.AddWithValue("@limit", limit.Value);
        }

        if (offset.HasValue)
        {
            command.Parameters.AddWithValue("@offset", offset.Value);
        }

        var results = new List<TItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
            {
                results.Add(serializer.Deserialize<TItem>(reader.GetString(0)));
            }
        }

        return results;
    }

    private static SqliteParameter CloneParameter(SqliteParameter source)
        => new(source.ParameterName, source.Value ?? DBNull.Value);

    private static string? BuildOrderBy<TItem>(IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        if (sortOrders is null)
        {
            return null;
        }

        var clauses = new List<string>();

        foreach (var sortOrder in sortOrders)
        {
            if (sortOrder?.Field is null)
            {
                continue;
            }

            if (!SqliteJsonPathResolver.TryResolve(UnwrapLambdaBody(sortOrder.Field), out var path, out var isColumn))
            {
                throw new SqliteTranslationException($"Cannot translate sort order '{sortOrder.Field}'.");
            }

            var operand = isColumn ? path : $"json_extract(_doc, '{path}')";
            var direction = sortOrder.Direction == SortDirection.Ascending ? "ASC" : "DESC";
            clauses.Add($"{operand} {direction}");
        }

        return clauses.Count == 0 ? null : string.Join(", ", clauses);
    }

    private static Expression UnwrapLambdaBody(LambdaExpression lambda)
        => SqliteJsonPathResolver.Unwrap(lambda.Body);

    private static Expression<Func<TItem, bool>> BuildWhereClausePredicate<TItem>(Dictionary<string, object> whereClause)
        where TItem : Entity
    {
        if (whereClause is null || whereClause.Count == 0)
        {
            return item => true;
        }

        var parameter = Expression.Parameter(typeof(TItem), "item");
        Expression? body = null;

        foreach (var pair in whereClause)
        {
            if (pair.Value is null)
            {
                continue;
            }

            var property = parameter.Type.GetProperty(pair.Key);

            if (property is null)
            {
                throw new SqliteTranslationException($"Unknown property '{pair.Key}' on '{parameter.Type.Name}'.");
            }

            var member = Expression.Property(parameter, property);
            Expression comparison;

            if (member.Type == typeof(string))
            {
                comparison = Expression.Equal(member, Expression.Constant(Convert.ToString(pair.Value, CultureInfo.InvariantCulture)));
            }
            else
            {
                try
                {
                    comparison = Expression.Equal(member, Expression.Convert(Expression.Constant(pair.Value), member.Type));
                }
                catch (InvalidOperationException exception)
                {
                    throw new SqliteTranslationException($"Cannot build where-clause comparison for '{pair.Key}'.", exception);
                }
            }

            body = body is null ? comparison : Expression.AndAlso(body, comparison);
        }

        return body is null ? item => true : Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => All<TItem>(includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var predicate = BuildReadPredicate<TItem>(null, includeDeleted);
        var list = await LoadListAsync<TItem>(predicate, null, null, null, transaction, cancellationToken).ConfigureAwait(false);
        return AsyncEnumerableFactory.From(list, cancellationToken);
    }

    public Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => ById<TItem>(id, includeDeleted: false, transaction, cancellationToken);

    public async Task<TItem> ById<TItem>(string id, bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            return null!;
        }

        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

        var table = Quote(GetCollectionNameForType<TItem>());
        var sql = includeDeleted || !SupportsSoftDelete<TItem>()
            ? $"SELECT _doc FROM {table} WHERE _id = @id;"
            : $"SELECT _doc FROM {table} WHERE _id = @id AND _deleted = 0;";

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", normalized);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is string document ? serializer.Deserialize<TItem>(document) : null!;
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => ById<TItem>(IDs, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeEntityIds(IDs);

        if (normalized.Count == 0)
        {
            return AsyncEnumerableFactory.From(Array.Empty<TItem>(), cancellationToken);
        }

        Expression<Func<TItem, bool>> predicate = item => normalized.Contains(item.Id);
        var fragment = BuildReadPredicate(predicate, includeDeleted);
        var list = await LoadListAsync<TItem>(fragment, null, null, null, transaction, cancellationToken).ConfigureAwait(false);
        return AsyncEnumerableFactory.From(list, cancellationToken);
    }

    public Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null!, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Count(predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var fragment = BuildReadPredicate(predicate, includeDeleted);

        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

        var table = Quote(GetCollectionNameForType<TItem>());
        await using var command = lease.Connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM {table} WHERE {fragment.Sql};";

        foreach (var parameter in fragment.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result);
    }

    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
        => IQueryable<TItem>(includeDeleted: false);

    public IQueryable<TItem> IQueryable<TItem>(bool includeDeleted) where TItem : Entity
    {
        var list = LoadListAsync<TItem>(BuildReadPredicate<TItem>(null, includeDeleted), null, null, null, null, CancellationToken.None).GetAwaiter().GetResult();
        return list.AsQueryable();
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null!, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Many(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var fragment = BuildReadPredicate(predicate, includeDeleted);
        var orderBy = BuildOrderBy(sortOrders);

        int? limit = null;
        int? offset = null;

        if (pageSize.HasValue)
        {
            limit = pageSize.Value;

            if (pageNumber.HasValue && pageNumber.Value > 1)
            {
                offset = (pageNumber.Value - 1) * pageSize.Value;
            }
        }

        var list = await LoadListAsync<TItem>(fragment, orderBy, limit, offset, transaction, cancellationToken).ConfigureAwait(false);
        return AsyncEnumerableFactory.From(list, cancellationToken);
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null!, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Many(whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Many(BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null!, IEnumerable<SortOrder<TItem>> sortOrders = null!,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => One(predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var fragment = BuildReadPredicate(predicate, includeDeleted);
        var orderBy = BuildOrderBy(sortOrders);
        var list = await LoadListAsync<TItem>(fragment, orderBy, 1, null, transaction, cancellationToken).ConfigureAwait(false);
        return list.Count == 0 ? null! : list[0];
    }

    public Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null!, string continueFrom = null!, int count = 1,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Random(predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var fragment = BuildReadPredicate(predicate, includeDeleted);
        var list = await LoadListAsync<TItem>(fragment, "RANDOM()", count, null, transaction, cancellationToken).ConfigureAwait(false);
        return AsyncEnumerableFactory.From(list, cancellationToken);
    }
}
