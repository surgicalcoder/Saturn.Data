using GoLive.Generator.Saturn.Resources;
using GoLive.Saturn.Data.Entities;
using ObservableCollections;

namespace Saturn.Generator.Entities.Playground;

public partial class SecondItem : Entity
{
    public partial string Item1 { get; set; }
    
    [AddToLimitedView("View1", ComputedProperty = "Count", LimitedViewType = typeof(int), DisableComputedPropertyDefault = true)]
    public partial ObservableList<MainItem> ThingsContained { get; set; }
}