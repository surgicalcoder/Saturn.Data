using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public class ExcludeFromLimitedViewAttribute : Attribute
{
    /// <summary>
    /// Excludes this member from the specified view. Pass "*" to exclude from all views.
    /// </summary>
    public ExcludeFromLimitedViewAttribute(string viewName)
    {
        ViewName = viewName;
    }

    public string ViewName { get; set; }
}

