using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace GoLive.Saturn.Data.Abstractions.Cascade;

public sealed record CascadeStep(
    Type ChildType,
    string ParentId,
    IReadOnlyList<string> ChildIds,
    CascadeMode Mode,
    CascadeDepth Depth,
    SharedScopePolicy SharedScope);

public sealed record CascadeStepResult(
    IReadOnlyList<string> AffectedIds,
    IReadOnlyList<(string ChildId, IReadOnlyList<string> OtherParentIds)> SharedScopeDeletions,
    IReadOnlyList<(string ChildId, IReadOnlyList<string> OtherParentIds)> SkippedSharedChildren);

public interface ICascadeExecutor
{
    bool Supports(Type childType);

    Task<CascadeStepResult> ExecuteAsync(
        CascadeStep step,
        IDatabaseTransaction? transaction,
        CancellationToken cancellationToken);
}
