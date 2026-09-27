namespace Saturn.Generator.Entities.Tests;

public class ViewPatchableMembersTests
{
    private const string Source = """
        using GoLive.Saturn.Data.Entities;
        using GoLive.Saturn.Generator.Entities.Resources;

        namespace Sample;

        public partial class Order : Entity
        {
            [AddToLimitedView("V")]
            public partial string? Name { get; set; }

            [AddToLimitedView("V")]
            [ReadonlyInView("V")]
            public partial string? Computed { get; set; }
        }
        """;

    [Fact]
    public void View_Emits_PatchableMembers()
    {
        var generated = GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs");

        Assert.Contains("public static readonly global::System.Collections.Generic.HashSet<string> PatchableMembers", generated);
        Assert.Contains("\"Name\"", generated);
    }

    [Fact]
    public void View_PatchableMembers_Excludes_Readonly_Members()
    {
        var generated = GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs");
        var index = generated.IndexOf("PatchableMembers", StringComparison.Ordinal);
        var block = generated[index..generated.IndexOf("return set;", index, StringComparison.Ordinal)];

        Assert.DoesNotContain("\"Computed\"", block);
    }
}
