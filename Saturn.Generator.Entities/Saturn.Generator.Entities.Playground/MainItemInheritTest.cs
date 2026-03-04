using GoLive.Saturn.Generator.Entities.Resources;

namespace Saturn.Generator.Entities.Playground;

[AddParentItemsLimitedViews(true)]
public partial class MainItemInheritTest : MainItem
{
    [AddToLimitedView("View1")]
    public partial string MainTest3 { get; set; }
}