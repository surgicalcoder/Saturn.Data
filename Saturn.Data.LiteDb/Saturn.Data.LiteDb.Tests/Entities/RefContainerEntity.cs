using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.LiteDb.Tests.Entities;

public class RefContainerEntity : Entity
{
    public Ref<BasicEntity>? Related { get; set; }
}


