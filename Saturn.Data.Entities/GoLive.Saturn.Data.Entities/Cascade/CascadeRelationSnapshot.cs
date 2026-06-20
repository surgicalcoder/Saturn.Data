using System;

namespace GoLive.Saturn.Data.Entities.Cascade;

public sealed record CascadeRelationSnapshot(
    Type ParentType,
    Type ChildType,
    CascadeMode Mode,
    CascadeDepth Depth,
    SharedScopePolicy SharedScope);
