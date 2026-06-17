using System;

namespace GoLive.Saturn.Data.Entities.Cascade;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class CascadeDeleteAttribute : Attribute
{
    public CascadeDeleteAttribute(
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
    public Type ChildType { get; set; } = null!;
}
