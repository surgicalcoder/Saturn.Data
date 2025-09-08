using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar.Tests.Entities;

[System.Diagnostics.DebuggerDisplay("Name = {Name}")]
public class BasicEntity : Entity
{
    public string Name { get; set; }
}


[System.Diagnostics.DebuggerDisplay("Name = {Name}")]
public class ParentScope : Entity
{
    public string Name { get; set; }
}

[System.Diagnostics.DebuggerDisplay("Name = {Name}")]
public class ChildEntity : ScopedEntity<ParentScope>
{
    public string Name { get; set; }

    public override string ToString()
    {
        return $"Name: {Name}";
    }
}