using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Data.Testing.Shared.Cascade.Entities;

[CascadeDeleteOnScope(CascadeMode.HardDelete)]
public partial class CascadeTag : ScopedEntity<CascadeUser>
{
    public string Name { get; set; } = string.Empty;
}
