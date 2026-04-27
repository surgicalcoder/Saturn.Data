using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Testing.Shared.Entities;

[System.Diagnostics.DebuggerDisplay("Name = {Name}")]
public class ChildEntity : ScopedEntity<ParentScope>
{
    public string Name { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Name: {Name}";
    }
}

