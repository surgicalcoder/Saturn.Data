using GoLive.Saturn.Data.ChangeTracking;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;

[ChangeTracking]
public partial class TrackingTest : Entity
{
    public partial string? Name { get; set; }

    public partial int Count { get; set; }

    private ObservableCollections.ObservableList<string> tags = new();
}
