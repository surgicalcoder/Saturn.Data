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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FieldToPartialPropertyCodeFixProvider)), Shared]
public class FieldToPartialPropertyCodeFixProvider : CodeFixProvider
{
    private const string Title = "Convert to Partial Property";

    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(FieldToPartialPropertyAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var fieldDeclaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: c => ConvertToPartialPropertyAsync(context.Document, fieldDeclaration, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private async Task<Document> ConvertToPartialPropertyAsync(Document document, FieldDeclarationSyntax fieldDeclaration, CancellationToken cancellationToken)
    {
        var variableDeclaration = fieldDeclaration.Declaration;
        var variable = variableDeclaration.Variables.First();
        var fieldName = variable.Identifier.Text;
        var propertyName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);

        var propertyDeclaration = SyntaxFactory.PropertyDeclaration(variableDeclaration.Type, propertyName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            .AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root.ReplaceNode(fieldDeclaration, propertyDeclaration);
        return document.WithSyntaxRoot(newRoot);
    }
}