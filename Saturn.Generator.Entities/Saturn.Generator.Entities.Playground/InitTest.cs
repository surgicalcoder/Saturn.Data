using GoLive.Generator.Saturn.Resources;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;

[AddParentItemToLimitedView("*", "Id", ChildField = "Id", InheritFromIUniquelyIdentifiable = true)]
public partial class InitTest : Entity
{
    /// <summary>
    /// This is a comment summary test.
    /// </summary>
    [AddToLimitedView("View2", Initializer = "\"Wobble\"")]
    private string blarg = "Wibble";
}