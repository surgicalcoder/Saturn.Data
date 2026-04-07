using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.LiteDbX.Tests.Entities;

public class RefContainerEntity : Entity
{
    public Ref<BasicEntity>? Related { get; set; }
}


