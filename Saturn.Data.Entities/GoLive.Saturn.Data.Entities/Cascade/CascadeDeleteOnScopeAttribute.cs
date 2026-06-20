using System;

namespace GoLive.Saturn.Data.Entities.Cascade;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CascadeDeleteOnScopeAttribute : Attribute
{
    public CascadeDeleteOnScopeAttribute(
        CascadeMode mode = CascadeMode.Default,
        CascadeDepth depth = CascadeDepth.Single,
        SharedScopePolicy sharedScope = SharedScopePolicy.Allow)
    {
        Mode = mode;
        Depth = depth;
        SharedScope = sharedScope;
    }

    public CascadeMode Mode { get; }
    public CascadeDepth Depth { get; }
    public SharedScopePolicy SharedScope { get; }
}
