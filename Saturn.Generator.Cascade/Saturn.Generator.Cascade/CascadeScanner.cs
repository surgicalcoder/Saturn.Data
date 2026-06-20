using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GoLive.Saturn.Generator.Cascade;

public enum CascadeRelationKind
{
    ChildScope = 0,
    ChildList = 1,
    ParentRef = 2,
    ParentWeakRef = 3,
}

public sealed record CascadeChildMapping(
    string PropertyName,
    string PropertyTypeFullName,
    CascadeRelationKind RelationKind,
    string ParentTypeFullName,
    string ChildTypeFullName,
    string Mode,
    string Depth,
    string SharedScope,
    bool EmitOverride);

public sealed record CascadeClassMapping(
    string ClassName,
    string Namespace,
    string TypeFullName,
    bool IsPartial,
    IReadOnlyList<CascadeChildMapping> Children,
    bool InheritsScopedEntity,
    string? ScopedParentFullName);

public static class CascadeScanner
{
    public const string AttributeFullName = "GoLive.Saturn.Data.Entities.Cascade.CascadeDeleteAttribute";
    public const string ClassAttributeFullName = "GoLive.Saturn.Data.Entities.Cascade.CascadeDeleteOnScopeAttribute";
    public const string EntityFullName = "GoLive.Saturn.Data.Entities.Entity";
    public const string ScopedEntityOpen = "GoLive.Saturn.Data.Entities.ScopedEntity<T>";
    public const string SecondScopedEntityOpen = "GoLive.Saturn.Data.Entities.SecondScopedEntity<TSecond, TPrimary>";
    public const string WeakScopedEntityOpen = "GoLive.Saturn.Data.Entities.WeakScopedEntity<TScope>";
    public const string WeakSecondScopedEntityOpen = "GoLive.Saturn.Data.Entities.WeakSecondScopedEntity<TSecond, TPrimary>";
    public const string WeakMultiscopedEntityOpen = "GoLive.Saturn.Data.Entities.WeakMultiscopedEntity<TScope>";

    public static bool CanBeCascadeCandidate(SyntaxNode node, CancellationToken _)
        => node is ClassDeclarationSyntax cds && cds.Members.Count > 0;

    public static CascadeClassMapping ConvertToMapping(
        GeneratorSyntaxContext ctx,
        CancellationToken _)
    {
        var cds = (ClassDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(cds) as INamedTypeSymbol;
        if (symbol is null) return Empty("__null__", string.Empty);
        if (!IsOrInheritsEntity(symbol)) return Empty("__notentity__" + symbol.Name, symbol.ToDisplayString());
        if (!cds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            return Empty("__notpartial__" + symbol.Name, symbol.ToDisplayString());

        var (inheritsScoped, scopedParent) = InspectScopedInheritance(symbol);
        var classCascadeAttr = FindClassCascadeAttribute(symbol);
        var children = ImmutableArray.CreateBuilder<CascadeChildMapping>();

        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.IsStatic) continue;
            var (cascadeAttr, _) = FindCascadeAttributeWithSource(member);
            if (cascadeAttr is null) continue;

            var kind = ClassifyProperty(member);
            if (kind is null) continue;

            var (mode, depth, sharedScope) = ReadCascadeArgs(cascadeAttr);
            var childTypeName = ResolveChildTypeName(cascadeAttr, member);

            children.Add(new CascadeChildMapping(
                PropertyName: member.Name,
                PropertyTypeFullName: member.Type.ToDisplayString(),
                RelationKind: kind.Value,
                ParentTypeFullName: scopedParent ?? symbol.ToDisplayString(),
                ChildTypeFullName: childTypeName,
                Mode: mode,
                Depth: depth,
                SharedScope: sharedScope,
                EmitOverride: ShouldEmitOverride(member, kind.Value, inheritsScoped)));
        }

        if (classCascadeAttr is not null && inheritsScoped)
        {
            var (mode, depth, sharedScope) = ReadCascadeArgs(classCascadeAttr);
            children.Add(new CascadeChildMapping(
                PropertyName: "Scope",
                PropertyTypeFullName: "GoLive.Saturn.Data.Entities.Ref<" + scopedParent + ">",
                RelationKind: CascadeRelationKind.ChildScope,
                ParentTypeFullName: scopedParent ?? symbol.ToDisplayString(),
                ChildTypeFullName: symbol.ToDisplayString(),
                Mode: mode,
                Depth: depth,
                SharedScope: sharedScope,
                EmitOverride: true));
        }

        return new CascadeClassMapping(
            ClassName: symbol.Name,
            Namespace: symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : symbol.ContainingNamespace.ToDisplayString(),
            TypeFullName: symbol.ToDisplayString(),
            IsPartial: true,
            Children: children.ToImmutable(),
            InheritsScopedEntity: inheritsScoped,
            ScopedParentFullName: scopedParent);
    }

    private static CascadeClassMapping Empty(string className, string fullName)
        => new(className, string.Empty, fullName, false, ImmutableArray<CascadeChildMapping>.Empty, false, null);

    private static bool IsOrInheritsEntity(INamedTypeSymbol symbol)
    {
        for (var t = symbol; t is not null; t = t.BaseType)
        {
            var fqn = t.OriginalDefinition.ToDisplayString();
            if (fqn == EntityFullName) return true;
        }
        return false;
    }

    private static (bool inherits, string? parentFullName) InspectScopedInheritance(INamedTypeSymbol symbol)
    {
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            var orig = t.OriginalDefinition.ToDisplayString();
            if (orig == ScopedEntityOpen && t is INamedTypeSymbol { IsGenericType: true } g1)
                return (true, g1.TypeArguments[0].ToDisplayString());
            if (orig == SecondScopedEntityOpen && t is INamedTypeSymbol { IsGenericType: true } g2)
                return (true, g2.TypeArguments[1].ToDisplayString());
            if (orig == WeakScopedEntityOpen && t is INamedTypeSymbol { IsGenericType: true } g3)
                return (true, g3.TypeArguments[0].ToDisplayString());
            if (orig == WeakMultiscopedEntityOpen && t is INamedTypeSymbol { IsGenericType: true } g4)
                return (true, g4.TypeArguments[0].ToDisplayString());
            if (orig == WeakSecondScopedEntityOpen && t is INamedTypeSymbol { IsGenericType: true } g5)
                return (true, g5.TypeArguments[1].ToDisplayString());
        }
        return (false, null);
    }

    private static CascadeRelationKind? ClassifyProperty(IPropertySymbol member)
    {
        if (member.OverriddenProperty is not null) return CascadeRelationKind.ChildScope;
        var type = member.Type;
        if (type is INamedTypeSymbol { IsGenericType: true } ng)
        {
            var def = ng.OriginalDefinition.ToDisplayString();
            if (def == "GoLive.Saturn.Data.Entities.Ref`1") return CascadeRelationKind.ParentRef;
            if (def == "GoLive.Saturn.Data.Entities.WeakRef`1") return CascadeRelationKind.ParentWeakRef;
        }
        if (type.ToDisplayString() == "GoLive.Saturn.Data.Entities.WeakRef")
            return CascadeRelationKind.ParentWeakRef;
        if (type is INamedTypeSymbol { IsGenericType: true } ng2
            && ng2.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List`1"
            && ng2.TypeArguments[0] is INamedTypeSymbol inner
            && inner.IsGenericType
            && inner.OriginalDefinition.ToDisplayString() == "GoLive.Saturn.Data.Entities.Ref`1")
            return CascadeRelationKind.ChildList;
        return null;
    }

    private static (string mode, string depth, string sharedScope) ReadCascadeArgs(AttributeData attr)
    {
        var mode = "Default";
        var depth = "Single";
        var shared = "Allow";
        if (attr.ConstructorArguments.Length > 0)
        {
            var ctor0 = attr.ConstructorArguments[0];
            if (ctor0.Type?.ToDisplayString() == "GoLive.Saturn.Data.Entities.Cascade.CascadeMode" && ctor0.Value is int mv)
                mode = EnumName<CascadeModeAlias>(mv);
            var ctor1 = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1] : default;
            if (ctor1.Type?.ToDisplayString() == "GoLive.Saturn.Data.Entities.Cascade.CascadeDepth" && ctor1.Value is int dv)
                depth = EnumName<CascadeDepthAlias>(dv);
            var ctor2 = attr.ConstructorArguments.Length > 2 ? attr.ConstructorArguments[2] : default;
            if (ctor2.Type?.ToDisplayString() == "GoLive.Saturn.Data.Entities.Cascade.SharedScopePolicy" && ctor2.Value is int sv)
                shared = EnumName<SharedScopeAlias>(sv);
        }
        return (mode, depth, shared);
    }

    private enum CascadeModeAlias { Default = 0, Archive = 1, SoftDelete = 2, HardDelete = 3, None = 4 }
    private enum CascadeDepthAlias { Single = 0, Transitive = 1 }
    private enum SharedScopeAlias { Allow = 0, Refuse = 1, Skip = 2 }

    private static string EnumName<T>(int v) where T : Enum => Enum.GetName(typeof(T), v) ?? "Default";

    private static string ResolveChildTypeName(AttributeData attr, IPropertySymbol property)
    {
        foreach (var na in attr.NamedArguments)
        {
            if (na.Key == "ChildType" && na.Value.Value is INamedTypeSymbol nt)
                return nt.ToDisplayString();
        }
        return property.ContainingType.ToDisplayString();
    }

    private static bool ShouldEmitOverride(IPropertySymbol member, CascadeRelationKind kind, bool inheritsScoped)
        => kind == CascadeRelationKind.ChildScope && inheritsScoped && member.OverriddenProperty is null;

    private static (AttributeData?, IPropertySymbol) FindCascadeAttributeWithSource(IPropertySymbol member)
    {
        for (var m = member; m is not null; m = m.OverriddenProperty)
        {
            foreach (var a in m.GetAttributes())
            {
                if (a.AttributeClass?.ToDisplayString() == AttributeFullName)
                    return (a, m);
            }
        }
        return (null, member);
    }

    private static AttributeData? FindClassCascadeAttribute(INamedTypeSymbol symbol)
    {
        foreach (var a in symbol.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() == ClassAttributeFullName)
                return a;
        }
        return null;
    }
}
