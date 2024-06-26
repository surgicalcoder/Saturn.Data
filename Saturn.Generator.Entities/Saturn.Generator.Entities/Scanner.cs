using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GoLive.Saturn.Generator.Entities;

public static class Scanner
{
    private static readonly SymbolDisplayFormat symbolDisplayFormat = new(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    public static bool CanBeEntity(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 } cds && cds.Modifiers.Any(e=>e.IsKind(SyntaxKind.PartialKeyword));
    }

    public static bool IsEntity(INamedTypeSymbol classDeclaration)
    {
        return InheritsFrom(classDeclaration, "GoLive.Saturn.Data.Entities.Entity");
    }

    private static bool InheritsFrom(INamedTypeSymbol classDeclaration, string qualifiedBaseTypeName)
    {
        var currentDeclared = classDeclaration;

        while (currentDeclared.BaseType != null)
        {
            var currentBaseType = currentDeclared.BaseType;

            if (string.Equals(currentBaseType.ToDisplayString(symbolDisplayFormat), qualifiedBaseTypeName, StringComparison.Ordinal))
            {
                return true;
            }

            currentDeclared = currentBaseType;
        }

        return false;
    }

    public static ClassToGenerate ConvertToMapping((INamedTypeSymbol symbol, ClassDeclarationSyntax syntax) input)
    {
        ClassToGenerate retr = new();

        retr.Filename = input.symbol.Locations.FirstOrDefault(e => !e.SourceTree.FilePath.EndsWith(".generated.cs")).SourceTree.FilePath;
        retr.Name = input.symbol.Name;
        retr.Namespace = input.symbol.ContainingNamespace.ToDisplayString();
        retr.Members = ConvertToMembers(input.symbol).ToList();
        retr.ParentItemToGenerate = GetParentItemsToGenerate(input.symbol, input.syntax).ToList();

        return retr;
    }

    private static IEnumerable<LimitedViewParentItemToGenerate> GetParentItemsToGenerate(INamedTypeSymbol classSymbol, ClassDeclarationSyntax inputSyntax)
    {
        var attrs = classSymbol.GetAttributes();

        if (attrs.Any(e => e.AttributeClass.ToString() == "GoLive.Generator.Saturn.Resources.AddParentItemToLimitedViewAttribute"))
        {
            foreach (var attributeData in attrs.Where(f => f.AttributeClass.ToString() == "GoLive.Generator.Saturn.Resources.AddParentItemToLimitedViewAttribute"))
            {
                LimitedViewParentItemToGenerate retr = new();

                retr.ViewName = attributeData.ConstructorArguments.FirstOrDefault(e => e is { Type: { SpecialType: SpecialType.System_String }, Value: not null }).Value as string;
                retr.PropertyName = attributeData.ConstructorArguments.Where(e => e is { Type: { SpecialType: SpecialType.System_String }, Value: not null }).Skip(1).FirstOrDefault().Value as string;
                retr.Property = GetMember(classSymbol, retr.PropertyName) as IPropertySymbol;
                
                if (attributeData.NamedArguments.Any())
                {
                    if (attributeData.NamedArguments.Any(f => f.Key == "UseLimitedView"))
                    {
                        retr.OverrideReturnTypeToUseLimitedView = attributeData.NamedArguments.FirstOrDefault(r => r.Key == "UseLimitedView").Value.Value.ToString();
                    }                    
                    
                    if (attributeData.NamedArguments.Any(f => f.Key == "ChildField"))
                    {
                        retr.ChildPropertyName = attributeData.NamedArguments.FirstOrDefault(r => r.Key == "ChildField").Value.Value.ToString();
                    }

                    if (attributeData.NamedArguments.Any(r => r.Key == "TwoWay"))
                    {
                        retr.TwoWay = (bool)attributeData.NamedArguments.FirstOrDefault(r => r.Key == "TwoWay").Value.Value;
                    }
                    
                    if (attributeData.NamedArguments.Any(r => r.Key == "InheritFromIUniquelyIdentifiable"))
                    {
                        retr.InheritFromIUniquelyIdentifiable = (bool)attributeData.NamedArguments.FirstOrDefault(r => r.Key == "InheritFromIUniquelyIdentifiable").Value.Value;
                    }
                }

                if (string.IsNullOrWhiteSpace(retr.ChildPropertyName))
                {
                    retr.ChildPropertyName = retr.PropertyName;
                }
                
                yield return retr;
            }
        }
    }
    
    
    private static ISymbol GetMember(INamedTypeSymbol classDeclaration, string Name)
    {
        var currentDeclared = classDeclaration;

        while (currentDeclared.BaseType != null)
        {
            var currentBaseType = currentDeclared.BaseType;

            if (currentBaseType.GetMembers(Name) is { Length: > 0 } mem)
            {
                return mem.FirstOrDefault();
            }

            currentDeclared = currentBaseType;
        }

        return null;
    }

    private static IEnumerable<MemberToGenerate> ConvertToMembers(INamedTypeSymbol classSymbol)
    {
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IFieldSymbol
                {
                    DeclaredAccessibility: Accessibility.Private, IsAbstract: false, AssociatedSymbol: null
                } fieldSymbol /*and not {MethodKind: MethodKind.Constructor}*/)
            {

                if (member is IPropertySymbol { IsAbstract: false } propertySymbol)
                {
                    var propertyMember = new MemberToGenerate
                    {
                        Name = propertySymbol.Name,
                        Type = propertySymbol.Type,
                        UseOnlyForLimited = true
                    };
                    
                    getAddToLimitedViewsFromAttributes(member.GetAttributes(), propertyMember);
                    
                    yield return propertyMember;
                }
                
                continue;
            }

            var memberToGenerate = new MemberToGenerate
            {
                Name = fieldSymbol.Name,
                Type = fieldSymbol.Type
            };

            if (fieldSymbol.GetDocumentationCommentXml() is { } xmlComment && !string.IsNullOrWhiteSpace(xmlComment))
            {
                memberToGenerate.XmlDocumentation = transformDocumentationComment(xmlComment);
            }

            var attr = fieldSymbol.GetAttributes();

            if (attr.Any(e => e.AttributeClass.ToString() == "GoLive.Generator.Saturn.Resources.DoNotTrackChangesAttribute"))
            {
                continue;
            }

            if (attr.Any(e => e.AttributeClass.ToString() == "GoLive.Generator.Saturn.Resources.AddRefToScopeAttribute"))
            {
                memberToGenerate.IsScoped = true;
            }

            if (attr.Any(e => e.AttributeClass.ToString() == "GoLive.Generator.Saturn.Resources.ReadonlyAttribute"))
            {
                memberToGenerate.ReadOnly = true;
            }
            else if (attr.Any(e => e.AttributeClass.ToString() == "GoLive.Generator.Saturn.Resources.WriteOnlyAttribute"))
            {
                memberToGenerate.WriteOnly = true;
            }

            var immutableArray = classSymbol.GetMembers($"{fieldSymbol.Name}_runAfterSet");

            if (immutableArray.Length > 0)
            {
                var runAfterSetMember = immutableArray[0];

                if (runAfterSetMember is IMethodSymbol { Parameters.Length: 1 } runAfterSetMethod)
                {
                    if (SymbolEqualityComparer.IncludeNullability.Equals(runAfterSetMethod.Parameters[0].Type.OriginalDefinition, fieldSymbol.Type.OriginalDefinition))
                    {
                        memberToGenerate.HasRunAfterSetMethodSimple = true;
                    }

                    if (fieldSymbol.Type.OriginalDefinition.ToString() == "GoLive.Saturn.Data.Entities.Ref<T>" )
                    {
                        if (SymbolEqualityComparer.IncludeNullability.Equals(runAfterSetMethod.Parameters[0].Type.OriginalDefinition , ((INamedTypeSymbol)fieldSymbol.Type).TypeArguments[0].OriginalDefinition))
                        {
                            memberToGenerate.HasRunAfterSetMethodIsRefItem = true;
                        }

                        memberToGenerate.RefType = ((INamedTypeSymbol)fieldSymbol.Type).TypeArguments[0].OriginalDefinition.ToString();
                    }
                    
                    if (runAfterSetMethod.Parameters[0].Type.OriginalDefinition.SpecialType == SpecialType.System_String)
                    {
                        memberToGenerate.HasRunAfterSetMethodIsRefItem = true;
                    }
                }
            }

            getAddToLimitedViewsFromAttributes(attr, memberToGenerate);

            

            if (attr.Any(r => !r.AttributeClass.ToString().StartsWith("GoLive.Generator.Saturn.Resources.")))
            {
                foreach (var at in attr.Where(r => !r.AttributeClass.ToString().StartsWith("GoLive.Generator.Saturn.Resources.")))
                {
                    MemberAttribute memAt = new();

                    memAt.Name = at.AttributeClass.ToString();

                    if (at.ConstructorArguments != null && at.ConstructorArguments.Length > 0)
                    {
                        foreach (var atConstructorArgument in at.ConstructorArguments)
                        {
                            memAt.ConstructorParameters.AddRange(atConstructorArgument.Values.Select(f=>f.Value?.ToString()));
                        }
                    }

                    if (at.NamedArguments != null && at.NamedArguments.Length > 0)
                    {
                        memAt.NamedParameters = at.NamedArguments.Select
                            (r => new KeyValuePair<string, string>(r.Key, r.Value.Value?.ToString())).ToList();
                    }
                    
                    memberToGenerate.AdditionalAttributes.Add(memAt);
                }
            }

            switch (fieldSymbol.Type)
            {
                case INamedTypeSymbol s2 when s2.OriginalDefinition.ToString() == "FastMember.TypeAccessor":
                    continue;
                case INamedTypeSymbol s1 when s1.OriginalDefinition.ToString() == "ObservableCollections.ObservableList<T>":
                    memberToGenerate.IsCollection = true;
                    memberToGenerate.CollectionType = s1.TypeArguments.FirstOrDefault();

                    break;
            }

            yield return memberToGenerate;
        }
    }

    private static IParameterSymbol getFirstGenericParameter(IFieldSymbol fieldSymbol)
    {
        return fieldSymbol.Type.GetMembers().Where(m => m.Kind == SymbolKind.Property).OfType<IPropertySymbol>().Where(propertySymbol => propertySymbol.GetMethod.IsGenericMethod).Select(propertySymbol => propertySymbol.Parameters).Where(parameters => parameters.Length == 1).Select(parameters => parameters[0].OriginalDefinition).FirstOrDefault();
    }

    private static void getAddToLimitedViewsFromAttributes(ImmutableArray<AttributeData> attr, MemberToGenerate memberToGenerate)
    {
        if (attr.Any(e => e.AttributeClass.ToString() == "GoLive.Generator.Saturn.Resources.AddToLimitedViewAttribute"))
        {
            memberToGenerate.LimitedViews = attr.Where(f => f.AttributeClass.ToString() == "GoLive.Generator.Saturn.Resources.AddToLimitedViewAttribute")
                .Select(e =>
                {
                    var retr = new LimitedViewToGenerate();
                        
                    retr.Name = e.ConstructorArguments.FirstOrDefault(r =>  r is { Type: { SpecialType: SpecialType.System_String }, Value: not null }).Value as string;
                    retr.TwoWay = (bool)e.ConstructorArguments.FirstOrDefault(r =>  r is { Type: { SpecialType: SpecialType.System_Boolean }, Value: not null }).Value;

                    if (e.NamedArguments.Any())
                    {
                        if (e.NamedArguments.Any(f => f.Key == "UseLimitedView"))
                        {
                            retr.OverrideReturnTypeToUseLimitedView = e.NamedArguments.FirstOrDefault(r => r.Key == "UseLimitedView").Value.Value.ToString();
                        }

                        if (e.NamedArguments.Any(r => r.Key == "TwoWay"))
                        {
                            retr.TwoWay = (bool)e.NamedArguments.FirstOrDefault(r => r.Key == "TwoWay").Value.Value;
                        }

                        if (e.NamedArguments.Any(r => r.Key == "Initializer"))
                        {
                            retr.Initializer = (string)e.NamedArguments.FirstOrDefault(r => r.Key == "Initializer").Value.Value;
                        }
                    }

                    return retr;
                }).ToList();
        }
    }
    public static string transformDocumentationComment(string xmlComment)
    {
        if (string.IsNullOrWhiteSpace(xmlComment))
            return string.Empty;

        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml("<root>" + xmlComment + "</root>");

        // Extract summary, remarks, example, etc. from the XML
        var summaryNode = xmlDoc.SelectSingleNode("root/summary");
        var remarksNode = xmlDoc.SelectSingleNode("root/remarks");
        var exampleNode = xmlDoc.SelectSingleNode("root/example");

        // Construct formatted comment
        string formattedComment = "";

        if (summaryNode != null)
        {
            formattedComment += $"/// <summary>\n/// {summaryNode.InnerText.Trim()}\n/// </summary>\n";
        }

        if (remarksNode != null)
        {
            formattedComment += $"/// <remarks>\n/// {remarksNode.InnerText.Trim()}\n/// </remarks>\n";
        }

        if (exampleNode != null)
        {
            formattedComment += $"/// <example>\n/// {exampleNode.InnerText.Trim()}\n/// </example>\n";
        }

        // Add other XML nodes as needed

        return formattedComment;
    }
}