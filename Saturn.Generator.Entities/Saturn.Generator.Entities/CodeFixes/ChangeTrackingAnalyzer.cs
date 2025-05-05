using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GoLive.Saturn.Generator.Entities.CodeFixes;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ChangeTrackingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ChangeTrackingAnalyzer";
    private const string Title = "Property not partial";
    private const string MessageFormat = "Property '{0}' is not partial and won't be included in change tracking";
    private const string Description = "Properties in classes inheriting from Entity should be partial for change tracking.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;

        // Check for exception comment on the property
        var propertyTrivia = propertyDeclaration.GetLeadingTrivia().ToString();

        if (propertyTrivia.Contains("// EXCEPTION: Don't track changes"))
        {
            return;
        }

        var classDeclaration = propertyDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();

        if (classDeclaration == null)
        {
            return;
        }

        // Check for exception comment on the class
        var classTrivia = classDeclaration.GetLeadingTrivia().ToString();

        if (classTrivia.Contains("// EXCEPTION: Don't track changes"))
        {
            return;
        }

        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

        if (classSymbol == null)
        {
            return;
        }

        // Check if the property has the DoNotTrackChangesAttribute
        var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration);

        if (propertySymbol?.GetAttributes().Any(attr => attr.AttributeClass.ToString() == "GoLive.Saturn.Generator.Entities.Resources.DoNotTrackChangesAttribute") == true)
        {
            return;
        }

        var baseType = classSymbol.BaseType;

        while (baseType != null)
        {
            if (baseType.ToString() == "GoLive.Saturn.Data.Entities.Entity" || 
                baseType.AllInterfaces.Any(i => i.ToString() == "GoLive.Saturn.Data.Entities.Entity"))
            {
                if (!propertyDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    var diagnostic = Diagnostic.Create(Rule, propertyDeclaration.GetLocation(), propertyDeclaration.Identifier.Text);
                    context.ReportDiagnostic(diagnostic);
                }

                break;
            }

            baseType = baseType.BaseType;
        }
    }
}