namespace GoLive.Saturn.Data.Entities;

public abstract class SecondScopedEntity<TSecondScope, TPrimaryScope> : MultiscopedEntity<TPrimaryScope> 
    where TSecondScope : Entity, new() 
    where TPrimaryScope : Entity, new()
{
    private Ref<TSecondScope> secondScope;
    
    public Ref<TSecondScope> SecondScope
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

                SetField(ref secondScope, value.Id);
            }
            else
            {
                if (secondScope != null && !string.IsNullOrWhiteSpace(secondScope.Id) && Scopes.Contains(secondScope.Id))
                {
                    Scopes.Remove(secondScope.Id);
                    SetField(ref secondScope, string.Empty);
                }
            }
        }
    }
}