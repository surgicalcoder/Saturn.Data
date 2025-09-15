using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.LiteDb.Tests.Entities;

[System.Diagnostics.DebuggerDisplay("Name = {Name}")]
public class ParentScope : Entity
{
    public string Name { get; set; }
}