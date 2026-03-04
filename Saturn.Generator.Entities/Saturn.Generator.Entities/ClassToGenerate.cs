using System.Collections.Generic;

namespace GoLive.Saturn.Generator.Entities;

public class ClassToGenerate
{
    public override string ToString()
    {
        return $"{nameof(Name)}: {Name}, {nameof(Members)}: {Members?.Count}, {nameof(Filename)}: {Filename}, {nameof(Namespace)}: {Namespace}";
    }

    public string Name { get; set; }
    public List<MemberToGenerate> Members { get; set; } = new();
    public string Filename { get; set; }
    public string Namespace { get; set; }
    public bool HasInitMethod { get; set; }

    public List<LimitedViewParentItemToGenerate> ParentItemToGenerate { get; set; }
    
    public bool InheritsParentLimitedViews { get; set; }
    public bool FlattenParentLimitedViews { get; set; }
    public string ParentClassName { get; set; }

    /// <summary>
    /// View names that exist on the parent class but have no members added by this child class.
    /// Used to generate To_ViewName() delegation methods on the child.
    /// </summary>
    public List<string> ParentOnlyViewNames { get; set; } = new();
}