using GoLive.Saturn.Data.ChangeTracking;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;

[ChangeTracking]
public partial class TrackingTest : Entity
{
    public partial string? Name { get; set; }

    public partial int Count { get; set; }

    private ObservableCollections.ObservableList<string> tags = new();

    private List<string> scores = new();

    [CollectionTracking(Instrument = true)]
    private List<string> instrumentedList = new();

    [CollectionTracking(Instrument = true)]
    private HashSet<string> instrumentedSet = new();

    [CollectionTracking(Instrument = true)]
    private Dictionary<string, int> instrumentedDictionary = new();
}
