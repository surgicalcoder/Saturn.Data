namespace GoLive.Saturn.Data.Entities;

public abstract class WeakSecondScopedEntity<TSecondScope, TPrimaryScope> : WeakMultiscopedEntity<TPrimaryScope>, ISecondScopedById
    where TSecondScope : Entity
    where TPrimaryScope : Entity
{
    private WeakRef<TSecondScope> secondScope;

    public virtual WeakRef<TSecondScope> SecondScope
    {
        get => secondScope;
        set
        {
            if (value != null && !string.IsNullOrWhiteSpace(value.Id))
            {
                if (secondScope != null && !string.IsNullOrWhiteSpace(secondScope.Id) && Scopes.Contains(secondScope.Id) && secondScope.Id != value.Id)
                {
                    Scopes.Remove(secondScope.Id);
                }

                if (!Scopes.Contains(value.Id))
                {
                    Scopes.Add(value.Id);
                }

                secondScope = value;
            }
            else
            {
                if (secondScope != null && !string.IsNullOrWhiteSpace(secondScope.Id) && Scopes.Contains(secondScope.Id))
                {
                    Scopes.Remove(secondScope.Id);
                }

                secondScope = null;
            }
        }
    }

    public virtual string SecondScopeId
    {
        get => SecondScope?.Id;
        set => SecondScope = string.IsNullOrWhiteSpace(value) ? null : new WeakRef<TSecondScope>(value);
    }
}

