using System.ComponentModel;
using GoLive.Saturn.Generator.Entities.Resources;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Generator.Entities.Resources;

namespace Saturn.Generator.Entities.Playground;

[AddParentItemToLimitedView("*", "_shortId", ChildField = "Id", InheritFromIUniquelyIdentifiable = true)]
public partial class FifthItem : Entity
{
    [AddToLimitedView("PublicView")]
    private string username;
    private string password;
    
    
    [AddToLimitedView("PublicView", true)]  
    public string PropertyTest { get; set; }
}