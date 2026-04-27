using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Testing.Shared;

public static class WellKnownData
{
    public static readonly BasicEntity BasicEntity1 = new() { Id = "68bdd5525324ff2610c4360d", Name = "Basic Entity 1" };
    public static readonly BasicEntity BasicEntity2 = new() { Id = "68bdd5521301349168c4360e", Name = "Basic Entity 2" };
    public static readonly BasicEntity BasicEntity3 = new() { Id = "68bdd552c91393242ec4360f", Name = "Basic Entity 3" };
    public static readonly BasicEntity BasicEntity4 = new() { Id = "68bdd552e162c898c7c43610", Name = "Basic Entity 4" };
    public static readonly BasicEntity BasicEntity5 = new() { Id = "68bdd5526b4f986dcac43611", Name = "Basic Entity 5" };
    public static readonly BasicEntity BasicEntity6 = new() { Id = "68bdd5521911958521c43612", Name = "Basic Entity 6" };
    public static readonly BasicEntity BasicEntity7 = new() { Id = "68bdd552993bb474fcc43613", Name = "Basic Entity 7" };
    public static readonly BasicEntity BasicEntity8 = new() { Id = "68bdd552305a3e3802c43614", Name = "Basic Entity 8" };
    public static readonly BasicEntity BasicEntity9 = new() { Id = "68bdd552561e748332c43615", Name = "Basic Entity 9" };
    public static readonly BasicEntity BasicEntity10 = new() { Id = "68bdd552c96b1764c0c43616", Name = "Basic Entity 10" };

    public static readonly BasicEntity[] Entities =
    [
        BasicEntity1,
        BasicEntity2,
        BasicEntity3,
        BasicEntity4,
        BasicEntity5,
        BasicEntity6,
        BasicEntity7,
        BasicEntity8,
        BasicEntity9,
        BasicEntity10
    ];

    public static readonly ParentScope ParentScope1 = new() { Id = "68bdd5525324ff2610c4361d", Name = "Parent Scope 1" };
    public static readonly ParentScope ParentScope2 = new() { Id = "68bdd5525324ff2610c4361e", Name = "Parent Scope 1" };

    public static readonly ChildEntity ParentScope1ChildEntity1 = new() { Id = "68bdd5525324ff2610c4362d", Name = "Child Entity 1 of Parent Scope 1", Scope = ParentScope1.Id };
    public static readonly ChildEntity ParentScope1ChildEntity2 = new() { Id = "68bdd5525324ff2610c4362e", Name = "Child Entity 2 of Parent Scope 1", Scope = ParentScope1.Id };

    public static readonly ChildEntity ParentScope2ChildEntity1 = new() { Id = "68bdd5525324ff2610c4362f", Name = "Child Entity 1 of Parent Scope 2", Scope = ParentScope2.Id };
    public static readonly ChildEntity ParentScope2ChildEntity2 = new() { Id = "68bdd5525324ff2610c43630", Name = "Child Entity 2 of Parent Scope 2", Scope = ParentScope2.Id };
}

