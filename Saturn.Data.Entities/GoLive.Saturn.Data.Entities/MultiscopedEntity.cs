using System.Collections.Generic;

namespace GoLive.Saturn.Data.Entities;

public abstract class MultiscopedEntity<T> : ScopedEntity<T> where T : Entity, new() {
    private List<string> scopes;

    protected MultiscopedEntity()
    {
        Scopes = new();
    }

    public override Ref<T> Scope
    {
        get => base.Scope;
        set
        {
            if (value != null)
            {
                if (string.IsNullOrWhiteSpace(value.Id))
                {
                    Scopes.RemoveAll(f => f == base.Scope.Id);
                    base.Scope = null;
                }
                else
                {
                    if (!Scopes.Contains(value.Id))
                    {
                        Scopes.Add(value.Id);
                    }

                    base.Scope = value;
                }
            }
            else
            {
                if (base.Scope != null && !string.IsNullOrWhiteSpace(base.Scope.Id))
                {
                    Scopes.RemoveAll(f => f == base.Scope.Id);
                }
                base.Scope = null;
            }
        }
    }

    public virtual List<string> Scopes
    {
        get => scopes;
        set => scopes = value;
    }
}