using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Sqlite.Query;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository
{
    public Task Increment<TItem>(string id, Expression<Func<TItem, int>> field, int delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, (long)delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, long>> field, long delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, double>> field, double delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, decimal>> field, decimal delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, (double)delta, expectedVersion, transaction, cancellationToken);

    private async Task IncrementCore<TItem>(string id, LambdaExpression field, object delta, long? expectedVersion,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity
    {
        if (!SqliteJsonPathResolver.TryResolve(field.Body, out var path, out var isColumn) || isColumn)
        {
            throw new NotSupportedException($"Cannot increment field '{field.Body}'. Only document fields are supported.");
        }

        var normalized = NormalizeId(id) ?? throw new FailedToUpdateException();

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Increment, id: normalized, expectedVersion: expectedVersion,
            incrementField: field, incrementDelta: delta, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Increment, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

            var versionFilter = expectedVersion.HasValue ? " AND COALESCE(_v,0) = @expectedVersion" : string.Empty;

            await using var command = lease.Connection.CreateCommand();
            command.CommandText = $"""
                UPDATE {Quote(GetCollectionNameForType<TItem>())}
                SET _doc = json_set(json_set(_doc, '{path}', COALESCE(json_extract(_doc,'{path}'),0) + @delta), '$.Version', COALESCE(_v,0) + 1),
                    _v = COALESCE(_v,0) + 1
                WHERE _id = @id{versionFilter};
                """;
            command.Parameters.AddWithValue("@id", normalized);
            command.Parameters.AddWithValue("@delta", delta);

            if (expectedVersion.HasValue)
            {
                command.Parameters.AddWithValue("@expectedVersion", expectedVersion.Value);
            }

            var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Increment, context,
                BuildWriteResult(context, WriteOutcome.Incremented, affected, new[] { normalized })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }
}
