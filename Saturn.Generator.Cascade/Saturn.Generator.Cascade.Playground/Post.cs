using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Generator.Cascade.Playground;

public partial class Post : ScopedEntity<Account>, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string DeletedBy { get; set; } = string.Empty;

    [CascadeDelete(CascadeMode.SoftDelete)]
    public override Ref<Account> Scope { get => base.Scope; set => base.Scope = value; }
}
