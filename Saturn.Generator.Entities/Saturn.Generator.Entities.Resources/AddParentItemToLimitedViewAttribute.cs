using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class AddParentItemToLimitedViewAttribute : Attribute
{
    public AddParentItemToLimitedViewAttribute(string ViewName, string ParentField)
    {
        this.ViewName = ViewName;
        this.ParentField = ParentField;
    }
    internal string ViewName { get; set; }
    internal string ParentField { get; set; }
    public string ChildField { get; set; }
    public Type LimitedViewType { get; set; }
    public bool TwoWay { get; set; }
    public bool InheritFromIUniquelyIdentifiable { get; set; }
}