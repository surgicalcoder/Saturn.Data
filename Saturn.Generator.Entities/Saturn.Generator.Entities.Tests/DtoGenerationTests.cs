namespace Saturn.Generator.Entities.Tests;

public class DtoGenerationTests
{
    private const string Source = """
        using GoLive.Saturn.Data.Entities;
        using GoLive.Saturn.Generator.Entities.Resources;

        namespace Sample;

        public partial class Parent : Entity
        {
            [AddToLimitedView("V")]
            public partial string? Name { get; set; }
        }

        [GenerateDto]
        public partial class Order : Entity
        {
            public partial string? Customer { get; set; }

            public int Total { get; set; }

            [AddToLimitedView("V")]
            public partial Ref<Parent> Parent { get; set; }

            [ExcludeFromDto]
            public partial string? Secret { get; set; }

            private ObservableCollections.ObservableList<string> tags = new();

            [AddToLimitedView("V")]
            public ObservableCollections.ObservableList<string> Tags { get; set; }
        }
        """;

    private const string ExpandSource = """
        using GoLive.Saturn.Data.Entities;
        using GoLive.Saturn.Generator.Entities.Resources;

        namespace Sample;

        public partial class Parent : Entity
        {
        }

        [GenerateDto(ExpandRefs = true)]
        public partial class Order : Entity
        {
            [AddToLimitedView("V")]
            public partial Ref<Parent> Parent { get; set; }
        }
        """;

    private const string FullIdSource = """
        using GoLive.Saturn.Data.Entities;
        using GoLive.Saturn.Generator.Entities.Resources;

        namespace Sample;

        [GenerateDto(UseFullId = true)]
        public partial class Order : Entity
        {
            public partial string? Customer { get; set; }
        }
        """;

    private const string DefaultSource = """
        using GoLive.Saturn.Data.Entities;
        using GoLive.Saturn.Generator.Entities.Resources;

        namespace Sample;

        public partial class Order : Entity
        {
            public partial string? Customer { get; set; }
        }
        """;

    private const string NoDtoSource = """
        using GoLive.Saturn.Data.Entities;
        using GoLive.Saturn.Generator.Entities.Resources;

        namespace Sample;

        [NoGenerateDto]
        public partial class Order : Entity
        {
            public partial string? Customer { get; set; }
        }
        """;

    [Fact]
    public void Dto_Is_Generated_For_GenerateDto_Entity()
    {
        Assert.Contains("class OrderDto", GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs"));
    }

    [Fact]
    public void Dto_Is_Generated_By_Assembly_Default()
    {
        Assert.Contains("class OrderDto", GeneratorTestHarness.GeneratedFor(DefaultSource, "Order.g.cs", generateDtosByDefault: true));
    }

    [Fact]
    public void NoGenerateDto_Opts_Out_Of_Assembly_Default()
    {
        Assert.DoesNotContain("class OrderDto", GeneratorTestHarness.GeneratedFor(NoDtoSource, "Order.g.cs", generateDtosByDefault: true));
    }

    [Fact]
    public void Dto_Does_Not_Generate_Without_Opt_In()
    {
        Assert.DoesNotContain("class OrderDto", GeneratorTestHarness.GeneratedFor(DefaultSource, "Order.g.cs"));
    }

    [Fact]
    public void Dto_Maps_Ref_As_Id_By_Default()
    {
        var generated = GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs");

        Assert.Contains("public string? Parent { get; set; }", generated);
        Assert.Contains("source.Parent?.Id", generated);
        Assert.Contains("new Ref<Sample.Parent>(Parent)", generated);
    }

    [Fact]
    public void Dto_Expands_Ref_When_Configured()
    {
        var generated = GeneratorTestHarness.GeneratedFor(ExpandSource, "Order.g.cs");

        Assert.Contains("public Sample.Parent Parent { get; set; }", generated);
        Assert.Contains("source.Parent?.Item", generated);
    }

    [Fact]
    public void Dto_Uses_ShortId_By_Default()
    {
        Assert.Contains("source._shortId", GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs"));
    }

    [Fact]
    public void Dto_Uses_FullId_When_Configured()
    {
        var generated = GeneratorTestHarness.GeneratedFor(FullIdSource, "Order.g.cs");

        Assert.Contains("Id = source.Id;", generated);
        Assert.DoesNotContain("_shortId", generated);
    }

    [Fact]
    public void Dto_Maps_Collection_To_List()
    {
        Assert.Contains("public List<string> Tags { get; set; }", GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs"));
    }

    [Fact]
    public void Dto_Excludes_ExcludeFromDto_Member()
    {
        Assert.DoesNotContain("Secret", DtoBlock(GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs")));
    }

    [Fact]
    public void Dto_FromRef_Is_Null_Safe()
    {
        Assert.Contains("source?.Item is null ? null : FromEntity(source.Item)", GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs"));
    }

    [Fact]
    public void Dto_Selector_Contains_No_Null_Propagation_Or_Conversions()
    {
        var dto = DtoBlock(GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs"));
        var selector = dto[dto.IndexOf("Selector =>", StringComparison.Ordinal)..];

        Assert.DoesNotContain("?.", selector);
        Assert.DoesNotContain("FromEntity", selector);
        Assert.DoesNotContain("ToList", selector);
    }

    [Fact]
    public void Dto_Preserves_Nullability()
    {
        var dto = DtoBlock(GeneratorTestHarness.GeneratedFor(Source, "Order.g.cs"));

        Assert.Contains("public string? Customer { get; set; }", dto);
        Assert.Contains("public string? Id { get; set; }", dto);
    }

    private static string DtoBlock(string generated)
    {
        var index = generated.IndexOf("class OrderDto", StringComparison.Ordinal);

        return index < 0 ? string.Empty : generated[index..];
    }
}
