using GoLive.Saturn.Data.Entities;
using ObservableCollections;

namespace Saturn.Generator.Entities.Playground;

public partial class SecondItem : Entity
{
    private string item1;
    private ObservableList<MainItem> thingsContained;
}