using GoLive.Saturn.Generator.Entities.Resources;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;

public partial class IgnoreTest : Entity
{
    [DoNotTrackChanges]
    private string ignoreTestItem;
    [Readonly]
    private string readonlyTest;
    private string everythingElseTest;
    
    [DoNotTrackChanges]
    public string Blar { get; set; }
}