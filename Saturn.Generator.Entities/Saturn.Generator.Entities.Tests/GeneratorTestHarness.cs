using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Generator.Entities;
using GoLive.Saturn.Generator.Entities.Resources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Saturn.Generator.Entities.Tests;

public static class GeneratorTestHarness
{
    public static Compilation CreateCompilation(string source)
    {
        var references = new List<MetadataReference>();
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");

        if (trusted is not null)
        {
            foreach (var path in trusted.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        references.Add(MetadataReference.CreateFromFile(typeof(Entity).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(AddToLimitedViewAttribute).Assembly.Location));

        return CSharpCompilation.Create(
            "GeneratorTests",
            new[] { CSharpSyntaxTree.ParseText(source, path: "TestInput.cs") },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static IReadOnlyList<(string HintName, string Text)> Run(string source)
    {
        var compilation = CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new SaturnGenerator().AsSourceGenerator());
        var updated = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);

        var runResult = updated.GetRunResult();

        return runResult.Results
            .SelectMany(result => result.GeneratedSources)
            .Select(generated => (generated.HintName, generated.SourceText.ToString()))
            .ToList();
    }

    public static string GeneratedFor(string source, string hintNameFragment)
        => Run(source).Single(generated => generated.HintName.Contains(hintNameFragment)).Text;
}
