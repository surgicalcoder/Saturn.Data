using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GoLive.Saturn.Generator.Cascade;

[Generator]
public sealed class CascadeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (s, _) => CascadeScanner.CanBeCascadeCandidate(s, _),
                static (ctx, ct) => CascadeScanner.ConvertToMapping(ctx, ct))
            .Where(static m => m.Children.Count > 0);

        context.RegisterSourceOutput(classDeclarations.Collect(), static (spc, all) => Execute(spc, all));
    }

    private static void Execute(SourceProductionContext spc, ImmutableArray<CascadeClassMapping> mappings)
    {
        foreach (var mapping in mappings)
        {
            var src = CascadeEmitter.Emit(mapping);
            spc.AddSource($"{mapping.ClassName}.__Cascade.g.cs", src);
        }
    }
}
