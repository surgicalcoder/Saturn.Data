using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace GoLive.Saturn.Generator.Entities.CodeFixes;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RunAfterSetAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "RunAfterSetAnalyzer";
    private const string Title = "Create Method that runs after set";
    private const string MessageFormat = "Field or partial property '{0}' could have a method that runs after set";
    private const string Description = "Fields or partial properties in classes inheriting from Entity could have a method that runs after set.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.FieldDeclaration, SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;

        if (node is PropertyDeclarationSyntax propertyDeclaration)
        {
            if (!propertyDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return;
            }
        }
        else if (!(node is FieldDeclarationSyntax))
        {
            return;
        }

        var classDeclaration = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration == null)
        {
            return;
        }

        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
        {
            return;
        }

        var baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.ToString() == "GoLive.Saturn.Data.Entities.Entity" || 
                baseType.AllInterfaces.Any(i => i.ToString() == "GoLive.Saturn.Data.Entities.Entity"))
            {
                var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node is PropertyDeclarationSyntax property ? property.Identifier.Text : ((FieldDeclarationSyntax)node).Declaration.Variables.First().Identifier.Text);
                context.ReportDiagnostic(diagnostic);
                break;
            }
            baseType = baseType.BaseType;
        }
    }
}