using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public class AddToLimitedViewAttribute : Attribute
{
    public AddToLimitedViewAttribute(string ViewName, bool TwoWay = false)
    {
        this.ViewName = ViewName;
        this.TwoWay = TwoWay;
    }
    public string ViewName { get; set; }
    public Type LimitedViewType { get; set; }
    public bool TwoWay { get; set; }
    public string Initializer { get; set; }
    public string ComputedProperty { get; set; }
    public bool DisableComputedPropertyDefault { get; set; }
}