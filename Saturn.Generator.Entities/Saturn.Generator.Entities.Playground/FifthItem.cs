using GoLive.Generator.Saturn.Resources;
using GoLive.Saturn.Data.Entities;

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