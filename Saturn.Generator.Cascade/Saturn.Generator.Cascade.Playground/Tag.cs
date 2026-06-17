using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Generator.Cascade.Playground;

[CascadeDeleteOnScope(CascadeMode.HardDelete)]
public partial class Tag : ScopedEntity<User>
{
    public string Name { get; set; } = string.Empty;
}
