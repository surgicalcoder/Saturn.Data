using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.MongoDb.Tests.Entities;

public class RefEntity : Entity
{
    public string Name { get; set; } = string.Empty;
    public Ref<BasicEntity> BasicEntityItem { get; set; }
}

