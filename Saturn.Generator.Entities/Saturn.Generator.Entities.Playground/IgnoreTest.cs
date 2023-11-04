using GoLive.Generator.Saturn.Resources;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;

public partial class IgnoreTest : Entity
{
    [DoNotTrackChanges]
    private string ignoreTestItem;
    [Readonly]
    private string readonlyTest;
    private string everythingElseTest;
}