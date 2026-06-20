using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Abstractions.Cascade;

public sealed class CascadeReport
{
    public IReadOnlyDictionary<Type, int> DeletedPerType { get; init; } = new Dictionary<Type, int>();
    public IReadOnlyDictionary<Type, int> ArchivedPerType { get; init; } = new Dictionary<Type, int>();
    public IReadOnlyList<(Type ChildType, string ChildId, IReadOnlyList<string> OtherParentIds)> SharedScopeDeletions { get; init; } = Array.Empty<(Type, string, IReadOnlyList<string>)>();
    public IReadOnlyList<(Type ChildType, string ChildId, IReadOnlyList<string> OtherParentIds)> SkippedSharedChildren { get; init; } = Array.Empty<(Type, string, IReadOnlyList<string>)>();
    public IReadOnlyList<(Type ParentType, string ParentId)> SkippedCycles { get; init; } = Array.Empty<(Type, string)>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public bool Aborted { get; init; }
}
