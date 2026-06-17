using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Data.Testing.Shared.Cascade.Entities;

public partial class CascadePost : ScopedEntity<CascadeAccount>, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string DeletedBy { get; set; } = string.Empty;

    [CascadeDelete(CascadeMode.SoftDelete)]
    public override Ref<CascadeAccount> Scope { get => base.Scope; set => base.Scope = value; }
}
