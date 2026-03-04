using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GoLive.Saturn.Generator.Entities;

[Generator]
public class SaturnGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor NoParentViewsFound = new(
        id: "SATURN001",
        title: "No parent views found",
        messageFormat: "Class '{0}' has [AddParentItemsLimitedViews] but its parent class '{1}' defines no limited views",
        category: "Saturn.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ExcludeViewNotFound = new(
        id: "SATURN002",
        title: "View name not found for exclusion",
        messageFormat: "Member '{0}' in class '{1}' has [ExcludeFromLimitedView(\"{2}\")] but that view name is not defined on the member",
        category: "Saturn.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ReadonlyInViewNotFound = new(
        id: "SATURN003",
        title: "View name not found for ReadonlyInView",
        messageFormat: "Member '{0}' in class '{1}' has [ReadonlyInView(\"{2}\")] but that view name is not defined on the member",
        category: "Saturn.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ParentItemPropertyNotFound = new(
        id: "SATURN004",
        title: "Parent field not found",
        messageFormat: "Class '{0}' has [AddParentItemToLimitedView] referencing field '{1}' which could not be found",
        category: "Saturn.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider.CreateSyntaxProvider(static (s, _) => Scanner.CanBeEntity(s),
                static (ctx, _) => GetEntityDeclarations(ctx))
            .Where(static c => c != default)
            .Select(static (c, _) => Scanner.ConvertToMapping(c));

        context.RegisterSourceOutput(classDeclarations.Collect(), static (spc, source) => Execute(spc, source));
    }

    private static void Execute(SourceProductionContext spc, ImmutableArray<ClassToGenerate> classesToGenerate)
    {
        foreach (var toGenerate in classesToGenerate)
        {
            EmitDiagnostics(spc, toGenerate);

            var sourceStringBuilder = new SourceStringBuilder();
            SourceCodeGenerator.Generate(sourceStringBuilder, toGenerate);

            if (sourceStringBuilder.ToString() is { Length: > 0 } s)
            {
                spc.AddSource($"{toGenerate.Name}.g.cs", sourceStringBuilder.ToString());
            }
        }
    }

    private static void EmitDiagnostics(SourceProductionContext spc, ClassToGenerate toGenerate)
    {
        // SATURN001: AddParentItemsLimitedViews on a class whose parent has no views
        if (toGenerate.InheritsParentLimitedViews
            && toGenerate.ParentOnlyViewNames.Count == 0
            && !toGenerate.Members.Any(m => m.LimitedViews.Any())
            && (toGenerate.ParentItemToGenerate == null || toGenerate.ParentItemToGenerate.Count == 0))
        {
            spc.ReportDiagnostic(Diagnostic.Create(NoParentViewsFound, Location.None, toGenerate.Name, toGenerate.ParentClassName ?? "unknown"));
        }

        foreach (var member in toGenerate.Members)
        {
            var viewNames = new HashSet<string>(member.LimitedViews.Select(lv => lv.Name));

            // SATURN002: ExcludeFromLimitedView referencing a view that doesn't exist
            foreach (var attr in member.AdditionalAttributes.Where(a => a.Name.EndsWith("ExcludeFromLimitedViewAttribute")))
            {
                var viewName = attr.ConstructorParameters.FirstOrDefault();
                if (viewName != null && viewName != "*" && !viewNames.Contains(viewName))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(ExcludeViewNotFound, Location.None, member.Name, toGenerate.Name, viewName));
                }
            }

            // SATURN003: ReadonlyInView referencing a view that doesn't exist  
            foreach (var attr in member.AdditionalAttributes.Where(a => a.Name.EndsWith("ReadonlyInViewAttribute")))
            {
                var viewName = attr.ConstructorParameters.FirstOrDefault();
                if (viewName != null && viewName != "*" && !viewNames.Contains(viewName))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(ReadonlyInViewNotFound, Location.None, member.Name, toGenerate.Name, viewName));
                }
            }
        }

        // SATURN004: AddParentItemToLimitedView referencing a property that couldn't be resolved
        if (toGenerate.ParentItemToGenerate != null)
        {
            foreach (var parentItem in toGenerate.ParentItemToGenerate.Where(p => p.Property == null && !string.IsNullOrWhiteSpace(p.PropertyName)))
            {
                spc.ReportDiagnostic(Diagnostic.Create(ParentItemPropertyNotFound, Location.None, toGenerate.Name, parentItem.PropertyName));
            }
        }
    }

    private static (INamedTypeSymbol symbol, ClassDeclarationSyntax syntax) GetEntityDeclarations(GeneratorSyntaxContext context)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax);

        return symbol is not null && Scanner.IsEntity(symbol) ? (symbol, classDeclarationSyntax) : default;
    }
}