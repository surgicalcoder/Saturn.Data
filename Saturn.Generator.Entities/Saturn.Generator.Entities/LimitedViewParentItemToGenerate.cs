using Microsoft.CodeAnalysis;

namespace GoLive.Saturn.Generator.Entities;

public class LimitedViewParentItemToGenerate
{
    public string ViewName { get; set; }
    public string PropertyName { get; set; }
    public IPropertySymbol Property { get; set; }
    public string OverrideReturnTypeToUseLimitedView { get; set; }
    public bool TwoWay { get; set; }
}