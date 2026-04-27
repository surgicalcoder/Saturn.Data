using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Testing.Shared.Entities;

[System.Diagnostics.DebuggerDisplay("Name = {Name}")]
public class BasicEntity : Entity
{
    public string Name { get; set; } = string.Empty;
}

