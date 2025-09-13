using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public static class MultiscopedHelper
{

    public static void AddEntityScope<T, T2>(this T target, T2 input)
        where T : MultiscopedEntity<T2>, new()
        where T2 : Entity, new()
    {
        if (!target.Scopes.Contains(input.Id))
        {
            target.Scopes.Add(input.Id);
        }
    }
    public static void SetScope<T, T2, T3>(this T target, T2 input)
        where T : MultiscopedEntity<T3>, new()
        where T2 : ScopedEntity<T3>, new()
        where T3 : Entity, new()
    {
        target.Scope = input.Scope;
        target.Scopes.Add(input.Id);
    }

    public static void SetScope<T, T2, T3>(this T target, T input)
        where T : MultiscopedEntity<T3>, new()
        where T2 : ScopedEntity<T3>, new()
        where T3 : Entity, new()
    {
        target.Scope = input.Scope;
        target.Scopes.AddRange(input.Scopes);
        target.Scopes.Add(input.Id);
    }
}