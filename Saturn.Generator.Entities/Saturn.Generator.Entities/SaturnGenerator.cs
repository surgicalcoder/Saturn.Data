using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GoLive.Saturn.Generator.Entities;

[Generator]
public class SaturnGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(AddAdditionalFiles);

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
            var sourceStringBuilder = new SourceStringBuilder();
            SourceCodeGenerator.Generate(sourceStringBuilder, toGenerate);

            if (sourceStringBuilder.ToString() is { Length: > 0 } s)
            {
                spc.AddSource($"{toGenerate.Name}.g.cs", sourceStringBuilder.ToString());
            }
        }
    }

    private void AddAdditionalFiles(IncrementalGeneratorPostInitializationContext context)
    {
        using var reader = new StreamReader(typeof(SaturnGenerator).Assembly.GetManifestResourceStream(EmbeddedResources.Resources_AdditionalFiles_cs), Encoding.UTF8);
        {
            var additionalFileContents = reader.ReadToEnd();
            context.AddSource("_additionalfiles.g.cs", additionalFileContents);
        }
    }

    private static (INamedTypeSymbol symbol, ClassDeclarationSyntax syntax) GetEntityDeclarations(GeneratorSyntaxContext context)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax);

        return symbol is not null && Scanner.IsEntity(symbol) ? (symbol, classDeclarationSyntax) : default;
    }
    
    
}