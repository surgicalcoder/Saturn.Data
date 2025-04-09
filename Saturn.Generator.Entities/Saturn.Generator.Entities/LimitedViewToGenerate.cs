namespace GoLive.Saturn.Generator.Entities;

public class LimitedViewToGenerate
{
    public string Name { get; set; }
    public string OverrideReturnTypeToUseLimitedView { get; set; }
    public bool TwoWay { get; set; }
    public string Initializer { get; set; }
    public string ComputedProperty { get; set; }
    public bool DisableComputedPropertyDefault { get; set; }
}