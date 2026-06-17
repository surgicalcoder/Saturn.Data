using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Data.Testing.Shared.Cascade.Entities;

public partial class CascadeAccount : ScopedEntity<CascadeUser>, IArchivable, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string ArchivedBy { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string DeletedBy { get; set; } = string.Empty;

    [CascadeDelete(CascadeMode.Archive, CascadeDepth.Transitive, SharedScopePolicy.Allow)]
    public override Ref<CascadeUser> Scope { get => base.Scope; set => base.Scope = value; }
}
