using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Generator.Cascade.Playground;

public partial class User : Entity
{
    public string Name { get; set; } = string.Empty;
}
