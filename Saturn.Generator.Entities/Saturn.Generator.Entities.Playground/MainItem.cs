using GoLive.Generator.Saturn.Resources;
using GoLive.Saturn.Data.Entities;
using ObservableCollections;

namespace Saturn.Generator.Entities.Playground;

public partial class MainItem : Entity
{
    [AddToLimitedView("View1")]
    [AddToLimitedView("View2", true)]
    private string name;
    [AddToLimitedView("View2", TwoWay = true)]
    private string description;
    private ObservableList<string> strings;
    private ObservableList<string> anotherString;

    private string wibble3;
}