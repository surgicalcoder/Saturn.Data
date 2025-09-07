using Saturn.Data.Stellar.Tests.Entities;

namespace Saturn.Data.Stellar.Tests;

public static class WELL_KNOWN
{
    public static readonly BasicEntity Basic_Entity_1 = new() { Id = "68bdd5525324ff2610c4360d", Name = "Basic Entity 1" };
    public static readonly BasicEntity Basic_Entity_2 = new() { Id = "68bdd5521301349168c4360e", Name = "Basic Entity 2" };
    public static readonly BasicEntity Basic_Entity_3 = new() { Id = "68bdd552c91393242ec4360f", Name = "Basic Entity 3" };
    public static readonly BasicEntity Basic_Entity_4 = new() { Id = "68bdd552e162c898c7c43610", Name = "Basic Entity 4" };
    public static readonly BasicEntity Basic_Entity_5 = new() { Id = "68bdd5526b4f986dcac43611", Name = "Basic Entity 5" };
    public static readonly BasicEntity Basic_Entity_6 = new() { Id = "68bdd5521911958521c43612", Name = "Basic Entity 6" };
    public static readonly BasicEntity Basic_Entity_7 = new() { Id = "68bdd552993bb474fcc43613", Name = "Basic Entity 7" };
    public static readonly BasicEntity Basic_Entity_8 = new() { Id = "68bdd552305a3e3802c43614", Name = "Basic Entity 8" };
    public static readonly BasicEntity Basic_Entity_9 = new() { Id = "68bdd552561e748332c43615", Name = "Basic Entity 9" };
    public static readonly BasicEntity Basic_Entity_10 = new() { Id = "68bdd552c96b1764c0c43616", Name = "Basic Entity 10" };

    public static readonly BasicEntity[] Entities =
    [
        Basic_Entity_1,
        Basic_Entity_2,
        Basic_Entity_3,
        Basic_Entity_4,
        Basic_Entity_5,
        Basic_Entity_6,
        Basic_Entity_7,
        Basic_Entity_8,
        Basic_Entity_9,
        Basic_Entity_10
    ];
    
    public static readonly ParentScope Parent_Scope_1 = new() { Id = "68bdd5525324ff2610c4361d", Name = "Parent Scope 1" };
    public static readonly ParentScope Parent_Scope_2 = new() { Id = "68bdd5525324ff2610c4361e", Name = "Parent Scope 1" };
    
    public static readonly ChildEntity Parent_Scope_1_Child_Entity_1 = new() { Id = "68bdd5525324ff2610c4362d", Name = "Child Entity 1 of Parent Scope 1", Scope = Parent_Scope_1.Id };
    public static readonly ChildEntity Parent_Scope_1_Child_Entity_2 = new() { Id = "68bdd5525324ff2610c4362e", Name = "Child Entity 2 of Parent Scope 1", Scope = Parent_Scope_1.Id };
    public static readonly ChildEntity Parent_Scope_2_Child_Entity_1 = new() { Id = "68bdd5525324ff2610c4362f", Name = "Child Entity 1 of Parent Scope 2", Scope = Parent_Scope_2.Id };
    public static readonly ChildEntity Parent_Scope_2_Child_Entity_2 = new() { Id = "68bdd5525324ff2610c43630", Name = "Child Entity 1 of Parent Scope 2", Scope = Parent_Scope_2.Id };
    
}