using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public class ReadonlyInViewAttribute : Attribute
{
    /// <summary>
    /// Makes this member read-only (no setter) in the specified view. Pass "*" to apply to all views.
    /// </summary>
    public ReadonlyInViewAttribute(string viewName)
    {
        ViewName = viewName;
    }

    public string ViewName { get; set; }
}

