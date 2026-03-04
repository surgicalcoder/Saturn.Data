using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class AddParentItemsLimitedViews : Attribute
{
    public AddParentItemsLimitedViews() { }
    public AddParentItemsLimitedViews(bool flatten)
    {
        Flatten = flatten;
    }
    public bool Flatten { get; set; }
}