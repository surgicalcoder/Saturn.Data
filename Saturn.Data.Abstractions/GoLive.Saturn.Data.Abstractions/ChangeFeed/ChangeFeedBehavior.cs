using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public sealed class ChangeFeedBehavior : IRepositoryWriteBehavior
{
    private readonly IChangeFeedSink sink;
    private readonly string source;
    private readonly ChangeFeedBehaviorOptions options;

    public ChangeFeedBehavior(IChangeFeedSink sink, string source, ChangeFeedBehaviorOptions options = null)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.options = options ?? new ChangeFeedBehaviorOptions();
    }

    public ValueTask AfterInsert<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => EmitAsync(RepositoryWriteOperation.Insert, context, result, isPartial: false);

    public ValueTask AfterUpdate<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => EmitAsync(RepositoryWriteOperation.Update, context, result, isPartial: false);

    public ValueTask AfterUpsert<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => EmitAsync(RepositoryWriteOperation.Upsert, context, result, isPartial: false);

    public ValueTask AfterSave<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => EmitAsync(RepositoryWriteOperation.Save, context, result, isPartial: false);

    public ValueTask AfterDelete<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => EmitAsync(RepositoryWriteOperation.Delete, context, result, isPartial: false);

    public ValueTask AfterHardDelete<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => EmitAsync(RepositoryWriteOperation.HardDelete, context, result, isPartial: false);

    public ValueTask AfterRestore<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => EmitAsync(RepositoryWriteOperation.Restore, context, result, isPartial: false);

    public ValueTask AfterPatch<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => EmitAsync(RepositoryWriteOperation.Patch, context, result, isPartial: true);

    public ValueTask AfterIncrement<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result)
        where TItem : Entity
        => EmitAsync(RepositoryWriteOperation.Increment, context, result, isPartial: true);

    private async ValueTask EmitAsync<TItem>(
        RepositoryWriteOperation operation,
        RepositoryWriteContext<TItem> context,
        RepositoryWriteResult result,
        bool isPartial)
        where TItem : Entity
    {
        if (!options.Enabled)
        {
            return;
        }

        if (!options.FeedPartialOps && isPartial)
        {
            return;
        }

        var hasFullItems = options.PayloadMode == FeedPayloadMode.Item && context.Items != null && context.Items.Count > 0;

        var change = new DataChangeEvent<TItem>
        {
            ChangeId = Guid.NewGuid().ToString("N"),
            Sequence = 0,
            Source = source,
            OccuredAtUtc = result.CompletedAtUtc,
            EntityType = typeof(TItem),
            Operation = operation,
            Outcome = result.Outcome,
            EntityIds = result.EntityIds,
            IsPartial = isPartial,
            HasFullItems = hasFullItems,
            Items = hasFullItems ? context.Items.Cast<object>().ToList() : Array.Empty<object>(),
            TypedItems = hasFullItems ? context.Items.ToList() : Array.Empty<TItem>()
        };

        await sink.AppendAsync(change, context.Transaction, context.CancellationToken);
    }
}