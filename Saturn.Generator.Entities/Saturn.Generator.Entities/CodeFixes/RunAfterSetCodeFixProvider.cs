using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace GoLive.Saturn.Generator.Entities.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RunAfterSetCodeFixProvider)), Shared]
public class RunAfterSetCodeFixProvider : CodeFixProvider
{
    private const string Title = "Create Method that runs after set";

    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RunAfterSetAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var node = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: c => CreateRunAfterSetMethodAsync(context.Document, node, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private async Task<Document> CreateRunAfterSetMethodAsync(Document document, MemberDeclarationSyntax memberDeclaration, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken);

        string methodName;
        string parameterType;
        string parameterName;

        if (memberDeclaration is PropertyDeclarationSyntax propertyDeclaration)
        {
            methodName = char.ToLower(propertyDeclaration.Identifier.Text[0]) + propertyDeclaration.Identifier.Text.Substring(1) + "_runAfterSet";
            parameterType = propertyDeclaration.Type.ToString();
            parameterName = "input";
        }
        else if (memberDeclaration is FieldDeclarationSyntax fieldDeclaration)
        {
            var variable = fieldDeclaration.Declaration.Variables.First();
            methodName = char.ToLower(variable.Identifier.Text[0]) + variable.Identifier.Text.Substring(1) + "_runAfterSet";
            parameterType = fieldDeclaration.Declaration.Type.ToString();
            parameterName = "input";
        }
        else
        {
            return document;
        }

        var methodDeclaration = SyntaxFactory.MethodDeclaration(
            attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
            modifiers: SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)),
            returnType: SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            explicitInterfaceSpecifier: null,
            identifier: SyntaxFactory.Identifier(methodName),
            typeParameterList: null,
            parameterList: SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
                    .WithType(SyntaxFactory.ParseTypeName(parameterType)))),
            constraintClauses: SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(),
            body: SyntaxFactory.Block(),
            expressionBody: null,
            semicolonToken: SyntaxFactory.Token(SyntaxKind.None));

        var newRoot = root.InsertNodesAfter(memberDeclaration, new[] { methodDeclaration });
        return document.WithSyntaxRoot(newRoot);
    }
}