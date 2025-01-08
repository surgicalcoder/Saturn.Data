using System.ComponentModel.DataAnnotations;
using GoLive.Generator.Saturn.Resources;
using GoLive.Saturn.Data.Entities;
using ObservableCollections;

namespace Saturn.Generator.Entities.Playground;

[AddParentItemToLimitedView("*", "_shortId", ChildField = "Id", InheritFromIUniquelyIdentifiable = true)] // TODO need to fix error when not inheriting from InheritFromIUniquelyIdentifiable
public partial class MainItem : Entity
{
    [AddToLimitedView("View1")]
    [AddToLimitedView("View2", true)]
    private string name;
    
    [AddToLimitedView("View2", TwoWay = true)]
    [AddToLimitedView("View3", true)]
    private string description;

    [Required]
    public partial ObservableList<string> Strings { get; set; } 

    private ObservableList<string> anotherString;

    private string wibble3;
}