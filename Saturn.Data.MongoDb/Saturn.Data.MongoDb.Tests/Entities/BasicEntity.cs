using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.MongoDb.Tests.Entities;

[System.Diagnostics.DebuggerDisplay("Name = {Name}")]
public class BasicEntity : Entity
{
    public string Name { get; set; }
}