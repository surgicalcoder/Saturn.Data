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
    public bool IsMultiscopedEntity { get; set; }

    public List<LimitedViewParentItemToGenerate> ParentItemToGenerate { get; set; }
    
    public bool InheritsParentLimitedViews { get; set; }
    public bool FlattenParentLimitedViews { get; set; }
    public string ParentClassName { get; set; }

    public bool GenerateDto { get; set; }
    public bool NoGenerateDto { get; set; }
    public bool DtoTrackChanges { get; set; }
    public bool DtoExpandRefs { get; set; }
    public bool DtoUseFullId { get; set; }
    public bool DtoIncludeProperties { get; set; }
    public string DtoName { get; set; }
    public bool DtoAlreadyExists { get; set; }

    public bool TrackChanges { get; set; }
    public bool NoChangeTracking { get; set; }
    public int TrackingMode { get; set; }
    public bool TrackRefItem { get; set; }

    /// <summary>
    /// View names that exist on the parent class but have no members added by this child class.
    /// Used to generate To_ViewName() delegation methods on the child.
    /// </summary>
    public List<string> ParentOnlyViewNames { get; set; } = new();
}