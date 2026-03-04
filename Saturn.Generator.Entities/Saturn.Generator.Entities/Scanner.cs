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
    private const string ATTRIBUTES_DoNotTrackChanges = $"{ATTRIBUTE_NAMESPACE}.DoNotTrackChangesAttribute";
    private const string ATTRIBUTES_AddRefToScope = $"{ATTRIBUTE_NAMESPACE}.AddRefToScopeAttribute";
    private const string ATTRIBUTES_WriteOnly = $"{ATTRIBUTE_NAMESPACE}.WriteOnlyAttribute";
    private const string ATTRIBUTES_ReadOnly = $"{ATTRIBUTE_NAMESPACE}.ReadonlyAttribute";
    private const string ATTRIBUTE_AddToLimitedView = $"{ATTRIBUTE_NAMESPACE}.AddToLimitedViewAttribute";
    private const string ATTRIBUTE_AddParentItemToLimitedView = $"{ATTRIBUTE_NAMESPACE}.AddParentItemToLimitedViewAttribute";
    private const string ATTRIBUTE_AddParentItemsLimitedViews = $"{ATTRIBUTE_NAMESPACE}.AddParentItemsLimitedViews";
    private const string ATTRIBUTE_ExcludeFromLimitedView = $"{ATTRIBUTE_NAMESPACE}.ExcludeFromLimitedViewAttribute";
    private const string ATTRIBUTE_ReadonlyInView = $"{ATTRIBUTE_NAMESPACE}.ReadonlyInViewAttribute";
    private const string ATTRIBUTE_NAMESPACE = "GoLive.Saturn.Generator.Entities.Resources";

    public static bool CanBeEntity(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 } cds && cds.Modifiers.Any(e => e.IsKind(SyntaxKind.PartialKeyword));
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
        retr.HasInitMethod = input.symbol.GetMembers().OfType<IMethodSymbol>().Any(m => m.Name == "_init" && m.DeclaredAccessibility == Accessibility.Private);
        
        if (input.symbol.GetAttributes().Any(e => e.AttributeClass?.ToString() == ATTRIBUTE_AddParentItemsLimitedViews))
        {
            var addParentAttr = input.symbol.GetAttributes().First(e => e.AttributeClass?.ToString() == ATTRIBUTE_AddParentItemsLimitedViews);
            var flatten = addParentAttr.NamedArguments.Any(a => a.Key == "Flatten") && (bool)addParentAttr.NamedArguments.First(a => a.Key == "Flatten").Value.Value;
            // Also support positional bool constructor argument
            if (!flatten && addParentAttr.ConstructorArguments.Any(a => a is { Type: { SpecialType: SpecialType.System_Boolean } }))
            {
                flatten = (bool)addParentAttr.ConstructorArguments.First(a => a is { Type: { SpecialType: SpecialType.System_Boolean } }).Value;
            }

            retr.InheritsParentLimitedViews = true;
            retr.FlattenParentLimitedViews = flatten;
            retr.ParentClassName = input.symbol.BaseType?.Name;

            if (flatten)
            {
                // Collect parent members that participate in limited views and merge them in
                var parentMembers = CollectParentMembersWithLimitedViews(input.symbol);
                retr.Members.InsertRange(0, parentMembers);
            }
            else
            {
                // Feature 1: find view names from the parent that this child doesn't add any members to
                var parentViewNames = CollectParentViewNames(input.symbol);
                var childViewNames = retr.Members.SelectMany(m => m.LimitedViews.Select(lv => lv.Name));
                retr.ParentOnlyViewNames = parentViewNames.Where(v => !childViewNames.Contains(v)).ToList();
            }
        }

        // Feature 3: deduplicate ParentItemToGenerate entries (same ViewName + ChildPropertyName)
        retr.ParentItemToGenerate = retr.ParentItemToGenerate
            .GroupBy(r => (r.ViewName, r.ChildPropertyName))
            .Select(g => g.First())
            .ToList();

        // Feature 4: expand wildcard [AddToLimitedView("*")] — replace with one entry per known view name
        var allViewNames = retr.Members
            .SelectMany(m => m.LimitedViews.Select(lv => lv.Name))
            .Where(n => n != "*")
            .Distinct()
            .ToList();

        foreach (var member in retr.Members)
        {
            var wildcardViews = member.LimitedViews.Where(lv => lv.Name == "*").ToList();
            if (!wildcardViews.Any()) continue;

            member.LimitedViews.RemoveAll(lv => lv.Name == "*");

            foreach (var wildcard in wildcardViews)
            {
                foreach (var viewName in allViewNames)
                {
                    // Only add if not already explicitly registered for this view
                    if (!member.LimitedViews.Any(lv => lv.Name == viewName))
                    {
                        member.LimitedViews.Add(new LimitedViewToGenerate
                        {
                            Name = viewName,
                            OverrideReturnTypeToUseLimitedView = wildcard.OverrideReturnTypeToUseLimitedView,
                            TwoWay = wildcard.TwoWay,
                            Initializer = wildcard.Initializer,
                            ComputedProperty = wildcard.ComputedProperty,
                            DisableComputedPropertyDefault = wildcard.DisableComputedPropertyDefault,
                            ReadOnly = wildcard.ReadOnly,
                        });
                    }
                }
            }
        }

        return retr;
    }

    private static IEnumerable<LimitedViewParentItemToGenerate> GetParentItemsToGenerate(INamedTypeSymbol classSymbol, ClassDeclarationSyntax inputSyntax)
    {
        var attrs = classSymbol.GetAttributes();

        // If AddParentItemsLimitedViews is present, collect AddParentItemToLimitedView from all base types
        if (attrs.Any(e => e.AttributeClass?.ToString() == ATTRIBUTE_AddParentItemsLimitedViews))
        {
            var baseType = classSymbol.BaseType;
            while (baseType != null)
            {
                var baseAttrs = baseType.GetAttributes()
                    .Where(f => f.AttributeClass?.ToString() == ATTRIBUTE_AddParentItemToLimitedView);

                foreach (var attributeData in baseAttrs)
                {
                    foreach (var item in ParseParentItemToGenerate(baseType, attributeData))
                    {
                        yield return item;
                    }
                }

                baseType = baseType.BaseType;
            }
        }

        if (attrs.Any(e => e.AttributeClass.ToString() == ATTRIBUTE_AddParentItemToLimitedView))
        {
            foreach (var attributeData in attrs.Where(f => f.AttributeClass.ToString() == ATTRIBUTE_AddParentItemToLimitedView))
            {
                foreach (var item in ParseParentItemToGenerate(classSymbol, attributeData))
                {
                    yield return item;
                }
            }
        }
    }

    private static IEnumerable<LimitedViewParentItemToGenerate> ParseParentItemToGenerate(INamedTypeSymbol classSymbol, AttributeData attributeData)
    {
        LimitedViewParentItemToGenerate retr = new();

        retr.ViewName = attributeData.ConstructorArguments.FirstOrDefault(e => e is { Type: { SpecialType: SpecialType.System_String }, Value: not null }).Value as string;
        retr.PropertyName = attributeData.ConstructorArguments.Where(e => e is { Type: { SpecialType: SpecialType.System_String }, Value: not null }).Skip(1).FirstOrDefault().Value as string;
        retr.Property = GetMember(classSymbol, retr.PropertyName) as IPropertySymbol;

        if (attributeData.NamedArguments.Any())
        {
            foreach (var namedArg in attributeData.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "LimitedViewType":
                        string typeName = namedArg.Value.Value.ToString().Split('(').Last().Split(')').First();
                        retr.OverrideReturnTypeToUseLimitedView = typeName;
                        break;
                    case "ChildField":
                        retr.ChildPropertyName = namedArg.Value.Value.ToString();
                        break;
                    case "TwoWay":
                        retr.TwoWay = (bool)namedArg.Value.Value;
                        break;
                    case "InheritFromIUniquelyIdentifiable":
                        retr.InheritFromIUniquelyIdentifiable = (bool)namedArg.Value.Value;
                        break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(retr.ChildPropertyName))
        {
            retr.ChildPropertyName = retr.PropertyName;
        }

        yield return retr;
    }


    private static List<MemberToGenerate> CollectParentMembersWithLimitedViews(INamedTypeSymbol classSymbol)
    {
        var result = new List<MemberToGenerate>();
        var baseType = classSymbol.BaseType;

        while (baseType != null && baseType.SpecialType == SpecialType.None)
        {
            var parentMembers = ConvertToMembers(baseType)
                .Where(m => m.LimitedViews.Any())
                .Select(m =>
                {
                    // Mark as view-only so the child class doesn't try to re-declare
                    // properties that reference private backing fields from the parent
                    m.UseOnlyForLimited = true;
                    return m;
                })
                .ToList();

            // Insert at beginning so higher-level base class members come first
            result.InsertRange(0, parentMembers);

            baseType = baseType.BaseType;
        }

        return result;
    }

    private static List<string> CollectParentViewNames(INamedTypeSymbol classSymbol)
    {
        var result = new HashSet<string>();
        var baseType = classSymbol.BaseType;

        while (baseType != null && baseType.SpecialType == SpecialType.None)
        {
            foreach (var member in ConvertToMembers(baseType))
            {
                foreach (var view in member.LimitedViews)
                {
                    result.Add(view.Name);
                }
            }

            baseType = baseType.BaseType;
        }

        return result.ToList();
    }

    private static ISymbol GetMember(INamedTypeSymbol classDeclaration, string Name)    {
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
        { //if (member.IsStatic || member.IsReadOnly || (member.Kind == SymbolKind.Field && ((IFieldSymbol)member).IsConst))
            if (member.IsStatic || (member.Kind == SymbolKind.Field && ((IFieldSymbol)member).IsReadOnly) || (member.Kind == SymbolKind.Field && ((IFieldSymbol)member).IsConst) || (member.Kind == SymbolKind.Property && ((IPropertySymbol)member).IsReadOnly))
            {
                continue;
            }
        
            var memberToGenerate = new MemberToGenerate
            {
                Name = member.Name,
            };

            IFieldSymbol fieldSymbol = null;
            IPropertySymbol propertySymbol = null;

            if (member is IFieldSymbol { AssociatedSymbol: null } field and not (IFieldSymbol { DeclaredAccessibility: Accessibility.Private, IsAbstract: false, AssociatedSymbol: not null }))
            {
                fieldSymbol = field;
                memberToGenerate.Type = fieldSymbol.Type;
            }
            else if (member is IPropertySymbol property)
            {
                propertySymbol = property;
                memberToGenerate.Type = propertySymbol.Type;

                if (propertySymbol.IsPartialDefinition)
                {
                    memberToGenerate.IsPartialProperty = true;
                    memberToGenerate.Name = $"{char.ToLowerInvariant(memberToGenerate.Name[0])}{memberToGenerate.Name.Substring(1)}";
                }
                else
                {
                    memberToGenerate.UseOnlyForLimited = true;
                }

                getAddToLimitedViewsFromAttributes(member.GetAttributes(), memberToGenerate);

                if (memberToGenerate.UseOnlyForLimited)
                {
                    yield return memberToGenerate;

                    continue;
                }
            }
            else
            {
                continue;
            }

            if (member.GetDocumentationCommentXml() is { } xmlComment && !string.IsNullOrWhiteSpace(xmlComment))
            {
                memberToGenerate.XmlDocumentation = transformDocumentationComment(xmlComment);
            }

            var attr = member.GetAttributes();

            if (AttributeExists(attr, ATTRIBUTES_DoNotTrackChanges))
            {
                continue;
            }

            if (AttributeExists(attr, ATTRIBUTES_AddRefToScope))
            {
                memberToGenerate.IsScoped = true;
            }

            if (AttributeExists(attr, ATTRIBUTES_ReadOnly))
            {
                memberToGenerate.ReadOnly = true;
            }
            else if (AttributeExists(attr, ATTRIBUTES_WriteOnly))
            {
                memberToGenerate.WriteOnly = true;
            }

            var immutableArray = classSymbol.GetMembers($"{memberToGenerate.Name}_runAfterSet");

            if (immutableArray.Length > 0)
            {
                var runAfterSetMember = immutableArray[0];

                if (runAfterSetMember is IMethodSymbol { Parameters.Length: 1 } runAfterSetMethod)
                {
                    if (memberToGenerate.Type != null && SymbolEqualityComparer.IncludeNullability.Equals(runAfterSetMethod.Parameters[0].Type.OriginalDefinition, memberToGenerate.Type.OriginalDefinition))
                    {
                        memberToGenerate.HasRunAfterSetMethodSimple = true;
                    }

                    if (memberToGenerate.Type != null && memberToGenerate.Type.OriginalDefinition.ToString() == "GoLive.Saturn.Data.Entities.Ref<T>")
                    {
                        if (SymbolEqualityComparer.IncludeNullability.Equals(runAfterSetMethod.Parameters[0].Type.OriginalDefinition, ((INamedTypeSymbol)memberToGenerate.Type).TypeArguments[0].OriginalDefinition))
                        {
                            memberToGenerate.HasRunAfterSetMethodIsRefItem = true;
                        }

                        memberToGenerate.RefType = ((INamedTypeSymbol)memberToGenerate.Type).TypeArguments[0].OriginalDefinition.ToString();
                    }

                    if (runAfterSetMethod.Parameters[0].Type.OriginalDefinition.SpecialType == SpecialType.System_String)
                    {
                        memberToGenerate.HasRunAfterSetMethodIsRefItem = true;
                    }
                }
            }

            getAddToLimitedViewsFromAttributes(attr, memberToGenerate);

            

            if ( member is not IPropertySymbol && attr.Any(r => !r.AttributeClass.ToString().StartsWith(ATTRIBUTE_NAMESPACE)))
            {
                foreach (var at in attr.Where(r => !r.AttributeClass.ToString().StartsWith(ATTRIBUTE_NAMESPACE)))
                {
                    MemberAttribute memAt = new();

                    memAt.Name = at.AttributeClass.ToString();

                    if (at.ConstructorArguments != null && at.ConstructorArguments.Length > 0)
                    {
                        foreach (var atConstructorArgument in at.ConstructorArguments)
                        {
                            memAt.ConstructorParameters.AddRange(atConstructorArgument.Kind == TypedConstantKind.Array
                                ? atConstructorArgument.Values.Select(f => f.Value?.ToString())
                                : new[] { atConstructorArgument.Value?.ToString() });
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

            if (memberToGenerate is { Type: not null })
            {
                switch (memberToGenerate.Type)
                {
                    case INamedTypeSymbol s2 when s2.OriginalDefinition.ToString() == "FastMember.TypeAccessor":
                        continue;
                    case INamedTypeSymbol s1 when s1.OriginalDefinition.ToString() == "ObservableCollections.ObservableList<T>":
                        memberToGenerate.IsCollection = true;
                        memberToGenerate.CollectionType = s1.TypeArguments.FirstOrDefault();

                        break;
                }
            }

            yield return memberToGenerate;
        }
    }

    private static bool AttributeExists(ImmutableArray<AttributeData> attr, string AttributeName)
    {
        return attr.Any(e => e.AttributeClass?.ToString() == AttributeName);
    }

    private static IParameterSymbol getFirstGenericParameter(IFieldSymbol fieldSymbol)
    {
        return fieldSymbol.Type.GetMembers().Where(m => m.Kind == SymbolKind.Property).OfType<IPropertySymbol>().Where(propertySymbol => propertySymbol.GetMethod.IsGenericMethod).Select(propertySymbol => propertySymbol.Parameters).Where(parameters => parameters.Length == 1).Select(parameters => parameters[0].OriginalDefinition).FirstOrDefault();
    }

    private static void getAddToLimitedViewsFromAttributes(ImmutableArray<AttributeData> attr, MemberToGenerate memberToGenerate)
    {
        if (attr.Any(e => e.AttributeClass.ToString() == ATTRIBUTE_AddToLimitedView))
        {
            memberToGenerate.LimitedViews = attr.Where(f => f.AttributeClass.ToString() == ATTRIBUTE_AddToLimitedView)
                .Select(e =>
                {
                    var retr = new LimitedViewToGenerate();

                    retr.Name = e.ConstructorArguments.FirstOrDefault(r => r is { Type: { SpecialType: SpecialType.System_String }, Value: not null }).Value as string;
                    retr.TwoWay = (bool)e.ConstructorArguments.FirstOrDefault(r => r is { Type: { SpecialType: SpecialType.System_Boolean }, Value: not null }).Value;

                    if (e.NamedArguments.Any())
                    {
                        if (e.NamedArguments.Any(r => r.Key == "TwoWay"))
                        {
                            retr.TwoWay = (bool)e.NamedArguments.FirstOrDefault(r => r.Key == "TwoWay").Value.Value;
                        }

                        if (e.NamedArguments.Any(f => f.Key == "LimitedViewType"))
                        {
                            var limitedViewType = e.NamedArguments.FirstOrDefault(r => r.Key == "LimitedViewType").Value.Value.ToString();
                            if (!string.IsNullOrEmpty(limitedViewType))
                            {
                                var typeName = limitedViewType.Split('(').Last().Split(')').First();
                                retr.OverrideReturnTypeToUseLimitedView = typeName;
                            }
                        }

                        if (e.NamedArguments.Any(r => r.Key == "Initializer"))
                        {
                            retr.Initializer = (string)e.NamedArguments.FirstOrDefault(r => r.Key == "Initializer").Value.Value;
                        }
                        
                        if (e.NamedArguments.Any(r => r.Key == "ComputedProperty"))
                        {
                            retr.ComputedProperty = (string)e.NamedArguments.FirstOrDefault(r => r.Key == "ComputedProperty").Value.Value;
                        }
                        if (e.NamedArguments.Any(r => r.Key == "DisableComputedPropertyDefault"))
                        {
                            retr.DisableComputedPropertyDefault = (bool)e.NamedArguments.FirstOrDefault(r => r.Key == "DisableComputedPropertyDefault").Value.Value;
                        }
                    }

                    return retr;
                }).ToList();
        }

        // Feature 7: apply ReadonlyInView — mark matching LimitedViews as read-only
        if (attr.Any(e => e.AttributeClass?.ToString() == ATTRIBUTE_ReadonlyInView))
        {
            var readonlyViewNames = attr
                .Where(e => e.AttributeClass?.ToString() == ATTRIBUTE_ReadonlyInView)
                .Select(e => e.ConstructorArguments.FirstOrDefault(r => r is { Type: { SpecialType: SpecialType.System_String }, Value: not null }).Value as string)
                .Where(n => n != null)
                ;

            foreach (var lv in memberToGenerate.LimitedViews)
            {
                if (readonlyViewNames.Contains("*") || readonlyViewNames.Contains(lv.Name))
                {
                    lv.ReadOnly = true;
                }
            }
        }

        // Feature 2: apply ExcludeFromLimitedView — remove matching LimitedViews
        if (attr.Any(e => e.AttributeClass?.ToString() == ATTRIBUTE_ExcludeFromLimitedView))
        {
            var excludedViewNames = attr
                .Where(e => e.AttributeClass?.ToString() == ATTRIBUTE_ExcludeFromLimitedView)
                .Select(e => e.ConstructorArguments.FirstOrDefault(r => r is { Type: { SpecialType: SpecialType.System_String }, Value: not null }).Value as string)
                .Where(n => n != null)
                ;

            if (excludedViewNames.Contains("*"))
            {
                memberToGenerate.LimitedViews.Clear();
            }
            else
            {
                memberToGenerate.LimitedViews.RemoveAll(lv => excludedViewNames.Contains(lv.Name));
            }
        }
    }

    public static string transformDocumentationComment(string xmlComment)
    {
        if (string.IsNullOrWhiteSpace(xmlComment))
            return string.Empty;

        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml($"<root>{xmlComment}</root>");

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