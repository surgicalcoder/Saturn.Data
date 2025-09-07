using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar.Tests.Entities;

public class BasicEntity : Entity
{
    public string Name { get; set; }
}


public class ParentScope : Entity
{
    public string Name { get; set; }
}

public class ChildEntity : ScopedEntity<ParentScope>
{
    public string Name { get; set; }
}