using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GoLive.Saturn.Generator.Entities;

public class MemberToGenerate
{
    public override string ToString()
    {
        return $"{nameof(Name)}: {Name}, {nameof(IsCollection)}: {IsCollection}, {nameof(ReadOnly)}: {ReadOnly}, {nameof(WriteOnly)}: {WriteOnly}, {nameof(IsScoped)}: {IsScoped}, {nameof(LimitedViews)}: {LimitedViews?.Count}";
    }

    public string Name { get; set; }
    public ITypeSymbol Type { get; set; }

    public bool IsCollection { get; set; }
    public ITypeSymbol? CollectionType { get; set; }

    public bool ReadOnly { get; set; }
    public bool WriteOnly { get; set; }
    public bool IsScoped { get; set; }
    public bool UseOnlyForLimited { get; set; }
    
    
    public List<LimitedViewToGenerate> LimitedViews { get; set; } = new();

    public List<MemberAttribute> AdditionalAttributes { get; set; } = new();
}