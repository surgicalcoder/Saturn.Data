using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Generator.Cascade.Playground;

public partial class Account : ScopedEntity<User>, IArchivable, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string ArchivedBy { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string DeletedBy { get; set; } = string.Empty;

    [CascadeDelete(CascadeMode.Archive, CascadeDepth.Transitive, SharedScopePolicy.Refuse)]
    public override Ref<User> Scope { get => base.Scope; set => base.Scope = value; }
}
