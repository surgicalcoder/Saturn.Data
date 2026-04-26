namespace GoLive.Saturn.Data.Entities;

public abstract class WeakScopedEntity : Entity, IScopedById
{
    public virtual WeakRef Scope { get; set; }

    public virtual string ScopeId
    {
        get => Scope?.Id;
        set => Scope = string.IsNullOrWhiteSpace(value) ? null : new WeakRef(value);
    }
}

public abstract class WeakScopedEntity<TScope> : Entity, IScopedById where TScope : Entity
{
    public virtual WeakRef<TScope> Scope { get; set; }

    public virtual string ScopeId
    {
        get => Scope?.Id;
        set => Scope = string.IsNullOrWhiteSpace(value) ? null : new WeakRef<TScope>(value);
    }
}

