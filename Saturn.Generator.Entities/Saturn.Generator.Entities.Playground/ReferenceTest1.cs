using GoLive.Saturn.Generator.Entities.Resources;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;

public partial class ReferenceTest1 : MultiscopedEntity<ReferenceTestScope>
{
    [AddRefToScope]
    private Ref<ReferenceTest2> test2;
    [AddRefToScope]
    private Ref<ReferenceTest3> test3;
}