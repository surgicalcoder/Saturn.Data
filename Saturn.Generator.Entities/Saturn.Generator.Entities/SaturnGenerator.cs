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

    private static readonly DiagnosticDescriptor AddRefToScopeRequiresMultiscoped = new(
        id: "SATURN005",
        title: "AddRefToScope requires Ref<T> on MultiscopedEntity",
        messageFormat: "Member '{0}' in class '{1}' has [AddRefToScope] but this requires a Ref<T> member on a class deriving from MultiscopedEntity<T>",
        category: "Saturn.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider.CreateSyntaxProvider(static (s, _) => Scanner.CanBeEntity(s),
                static (ctx, _) => GetEntityDeclarations(ctx))
            .Where(static c => c != default)
            .Select(static (c, _) => Scanner.ConvertToMapping(c));

        var options = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) => (
            GenerateDtosByDefault: provider.GlobalOptions.TryGetValue("build_property.SaturnGenerateDtos", out var dtos) && bool.TryParse(dtos, out var dtosEnabled) && dtosEnabled,
            TrackChangesByDefault: provider.GlobalOptions.TryGetValue("build_property.SaturnChangeTracking", out var tracking) && bool.TryParse(tracking, out var trackingEnabled) && trackingEnabled));

        context.RegisterSourceOutput(classDeclarations.Collect().Combine(options),
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static void Execute(SourceProductionContext spc, ImmutableArray<ClassToGenerate> classesToGenerate, (bool GenerateDtosByDefault, bool TrackChangesByDefault) options)
    {
        foreach (var toGenerate in classesToGenerate)
        {
            toGenerate.GenerateDto = (toGenerate.GenerateDto || (options.GenerateDtosByDefault && !toGenerate.NoGenerateDto)) && !toGenerate.DtoAlreadyExists;
            toGenerate.TrackChanges = toGenerate.TrackChanges || (options.TrackChangesByDefault && !toGenerate.NoChangeTracking);

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

            // SATURN005: AddRefToScope requires a Ref<T> member on a class deriving from MultiscopedEntity<T>
            if (member.IsScoped)
            {
                var isRefType = member.Type != null && member.Type.OriginalDefinition.ToString() == "GoLive.Saturn.Data.Entities.Ref<T>";

                if (!toGenerate.IsMultiscopedEntity || !isRefType)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(AddRefToScopeRequiresMultiscoped, Location.None, member.Name, toGenerate.Name));
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