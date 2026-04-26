namespace GoLive.Saturn.Data.Entities;

public abstract class ScopedEntity<T> : Entity, IScopedById where T : Entity, new()
{
    public virtual Ref<T> Scope { get; set; }

    public virtual string ScopeId
    {
        get => Scope?.Id;
        set => Scope = string.IsNullOrWhiteSpace(value) ? null : value;
    }
}