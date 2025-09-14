using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.MongoDb.Tests.Entities;

[System.Diagnostics.DebuggerDisplay("Name = {Name}")]
public class ChildEntity : ScopedEntity<ParentScope>
{
    public string Name { get; set; }

    public override string ToString()
    {
        return $"Name: {Name}";
    }
}