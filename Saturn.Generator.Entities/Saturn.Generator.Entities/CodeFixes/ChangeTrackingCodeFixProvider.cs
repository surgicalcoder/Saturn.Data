using System;
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
using Microsoft.CodeAnalysis.Rename;

namespace GoLive.Saturn.Generator.Entities.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ChangeTrackingCodeFixProvider)), Shared]
public class ChangeTrackingCodeFixProvider : CodeFixProvider
{
    private const string TitleMakePartial = "Make property partial";
    private const string TitleAddException = "Add exception comment";

    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ChangeTrackingAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var propertyDeclaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleMakePartial,
                createChangedDocument: c => MakePartialAsync(context.Document, propertyDeclaration, c),
                equivalenceKey: TitleMakePartial),
            diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: TitleAddException,
                createChangedDocument: c => AddExceptionCommentAsync(context.Document, propertyDeclaration, c),
                equivalenceKey: TitleAddException),
            diagnostic);
    }

    private async Task<Document> MakePartialAsync(Document document, PropertyDeclarationSyntax propertyDeclaration, CancellationToken cancellationToken)
    {
        var partialProperty = propertyDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root.ReplaceNode(propertyDeclaration, partialProperty);
        return document.WithSyntaxRoot(newRoot);
    }

    private async Task<Document> AddExceptionCommentAsync(Document document, PropertyDeclarationSyntax propertyDeclaration, CancellationToken cancellationToken)
    {
        var trivia = SyntaxFactory.Comment("// EXCEPTION: Don't track changes \n");
        var newProperty = propertyDeclaration.WithLeadingTrivia(propertyDeclaration.GetLeadingTrivia().Add(trivia));
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root.ReplaceNode(propertyDeclaration, newProperty);
        return document.WithSyntaxRoot(newRoot);
    }
}