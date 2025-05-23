using GoLive.Saturn.Generator.Entities.Resources;
using GoLive.Saturn.Data.Entities;
using ObservableCollections;

namespace Saturn.Generator.Entities.Playground;

public partial class SecondItem : Entity
{
    public partial string Item1 { get; set; }
    
    [AddToLimitedView("View1", ComputedProperty = "Count", LimitedViewType = typeof(int))]
    public partial ObservableList<MainItem> ThingsContained { get; set; }
}