using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Data.Sqlite.Cascade;

public sealed class SqliteCascadeExecutor : ICascadeExecutor
{
    private readonly SqliteRepository repository;
    private readonly CascadeMode? forceMode;

    public SqliteCascadeExecutor(SqliteRepository repository, CascadeMode? forceMode = null)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.forceMode = forceMode;
    }

    public bool Supports(Type childType) => typeof(Entity).IsAssignableFrom(childType);

    public async Task<CascadeStepResult> ExecuteAsync(CascadeStep step, IDatabaseTransaction? transaction, CancellationToken cancellationToken)
    {
        var mode = forceMode ?? step.Mode;

        var ids = step.ChildIds is { Count: > 0 }
            ? step.ChildIds.ToList()
            : await repository.MaterializeCascadeChildrenAsync(step.ChildType, step.ParentId, transaction, cancellationToken).ConfigureAwait(false);

        if (ids.Count == 0)
        {
            return new CascadeStepResult(Array.Empty<string>(), Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
        }

        await repository.ApplyCascadeAsync(step.ChildType, ids, mode, step.ParentId, transaction, cancellationToken).ConfigureAwait(false);

        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }
}
