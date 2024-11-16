using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;

public partial class StaticReadonlyTest : Entity
{
    public static readonly string staticStringTest = "WibbleWobble";
    public readonly string readonlyStringTest = "WibbleWobble";
}