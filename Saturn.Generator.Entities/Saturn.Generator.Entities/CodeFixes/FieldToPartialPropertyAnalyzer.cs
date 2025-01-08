using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace GoLive.Saturn.Generator.Entities.CodeFixes;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FieldToPartialPropertyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FieldToPartialPropertyAnalyzer";
    private const string Title = "Field not partial property";
    private const string MessageFormat = "Field '{0}' is not a partial property and won't be included in change tracking";
    private const string Description = "Fields in classes inheriting from Entity should be partial properties for change tracking.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

        // Check if the field is private
        if (!fieldDeclaration.Modifiers.Any(SyntaxKind.PrivateKeyword))
        {
            return;
        }

        // Check for exception comment on the field
        var fieldTrivia = fieldDeclaration.GetLeadingTrivia().ToString();
        if (fieldTrivia.Contains("// EXCEPTION: Don't track changes"))
        {
            return;
        }

        var variableDeclaration = fieldDeclaration.Declaration;
        var variable = variableDeclaration.Variables.First();
        var semanticModel = context.SemanticModel;
        var fieldSymbol = semanticModel.GetDeclaredSymbol(variable);

        // Check if the field has the DoNotTrackChangesAttribute
        if (fieldSymbol?.GetAttributes().Any(attr => attr.AttributeClass.ToString() == "GoLive.Generator.Saturn.Resources.DoNotTrackChangesAttribute") == true)
        {
            return;
        }

        var classDeclaration = fieldDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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
                var diagnostic = Diagnostic.Create(Rule, fieldDeclaration.GetLocation(), variable.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
                break;
            }

            baseType = baseType.BaseType;
        }
    }
}