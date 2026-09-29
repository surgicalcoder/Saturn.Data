using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository
{
    public Task Increment<TItem>(string id, Expression<Func<TItem, int>> field, int delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, long>> field, long delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, double>> field, double delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, delta, expectedVersion, transaction, cancellationToken);

    public Task Increment<TItem>(string id, Expression<Func<TItem, decimal>> field, decimal delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => IncrementCore<TItem>(id, field, (double)delta, expectedVersion, transaction, cancellationToken);

    private async Task IncrementCore<TItem>(string id, LambdaExpression field, object delta, long? expectedVersion,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity
    {
        if (!Query.MemberPathResolver.TryResolve(field, out var path) || path.Contains('.'))
        {
            throw new NotSupportedException($"Cannot increment field '{field.Body}'. Only top-level document fields are supported.");
        }

        var normalized = NormalizeId(id) ?? throw new FailedToUpdateException();

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Increment, id: normalized, expectedVersion: expectedVersion,
            incrementField: field, incrementDelta: delta, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Increment, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var entity = await store.Get<TItem>(normalized).ConfigureAwait(false);

            if (entity is null)
            {
                throw new FailedToUpdateException();
            }

            if (expectedVersion.HasValue && entity.Version != expectedVersion.Value)
            {
                throw new FailedToUpdateException();
            }

            var property = typeof(TItem).GetProperty(path, BindingFlags.Public | BindingFlags.Instance)
                           ?? throw new NotSupportedException($"No property '{path}' on '{typeof(TItem).Name}'.");

            var current = property.GetValue(entity);
            var currentValue = current is null ? 0m : Convert.ToDecimal(current, CultureInfo.InvariantCulture);
            var deltaValue = Convert.ToDecimal(delta, CultureInfo.InvariantCulture);
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            property.SetValue(entity, Convert.ChangeType(currentValue + deltaValue, targetType, CultureInfo.InvariantCulture));

            entity.Version = (entity.Version ?? 0) + 1;
            await UpdateWithTransactionAsync(transaction, entity, cancellationToken).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Increment, context,
                BuildWriteResult(context, WriteOutcome.Incremented, 1, new[] { normalized })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }
}
