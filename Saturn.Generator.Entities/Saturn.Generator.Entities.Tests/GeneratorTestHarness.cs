using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Generator.Entities;
using GoLive.Saturn.Generator.Entities.Resources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

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
        references.Add(MetadataReference.CreateFromFile(typeof(GoLive.Saturn.Data.ChangeTracking.ChangeTrackingAttribute).Assembly.Location));

        return CSharpCompilation.Create(
            "GeneratorTests",
            new[] { CSharpSyntaxTree.ParseText(source, path: "TestInput.cs") },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static IReadOnlyList<(string HintName, string Text)> Run(string source, bool generateDtosByDefault = false, bool trackChangesByDefault = false)
    {
        var compilation = CreateCompilation(source);

        var options = new Dictionary<string, string>();

        if (generateDtosByDefault)
        {
            options["build_property.SaturnGenerateDtos"] = "true";
        }

        if (trackChangesByDefault)
        {
            options["build_property.SaturnChangeTracking"] = "true";
        }

        var driver = CSharpGeneratorDriver.Create(
            new[] { new SaturnGenerator().AsSourceGenerator() },
            additionalTexts: null,
            parseOptions: null,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(options));

        var updated = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);

        var runResult = updated.GetRunResult();

        return runResult.Results
            .SelectMany(result => result.GeneratedSources)
            .Select(generated => (generated.HintName, generated.SourceText.ToString()))
            .ToList();
    }

    public static string GeneratedFor(string source, string hintNameFragment, bool generateDtosByDefault = false, bool trackChangesByDefault = false)
        => Run(source, generateDtosByDefault, trackChangesByDefault).Single(generated => generated.HintName.Contains(hintNameFragment)).Text;

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> values;

        public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values) => this.values = values;

        public override bool TryGetValue(string key, out string? value)
        {
            var found = values.TryGetValue(key, out var stored);
            value = found ? stored : null;
            return found;
        }

        public override IEnumerable<string> Keys => values.Keys;
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions options;

        public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> values)
            => options = new TestAnalyzerConfigOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
    }
}
