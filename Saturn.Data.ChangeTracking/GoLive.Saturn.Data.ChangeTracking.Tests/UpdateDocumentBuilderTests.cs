using System.Text.Json;
using GoLive.Saturn.Data.ChangeTracking;

namespace GoLive.Saturn.Data.ChangeTracking.Tests;

public class UpdateDocumentBuilderTests
{
    [Fact]
    public void Scalar_Set()
    {
        var document = UpdateDocumentBuilder.Build(new[]
        {
            new FieldChange { Path = "Name", Kind = ChangeKind.Set, NewValue = "Ada" }
        });

        Assert.Equal("Ada", ReadSet(document, "Name"));
    }

    [Fact]
    public void Unset()
    {
        var document = UpdateDocumentBuilder.Build(new[]
        {
            new FieldChange { Path = "Notes", Kind = ChangeKind.Unset }
        });

        using var parsed = JsonDocument.Parse(document);
        Assert.True(parsed.RootElement.GetProperty("$unset").GetProperty("Notes").GetBoolean());
    }

    [Fact]
    public void Increment()
    {
        var document = UpdateDocumentBuilder.Build(new[]
        {
            new FieldChange { Path = "Count", Kind = ChangeKind.Increment, NewValue = 5 }
        });

        using var parsed = JsonDocument.Parse(document);
        Assert.Equal(5, parsed.RootElement.GetProperty("$inc").GetProperty("Count").GetInt32());
    }

    [Fact]
    public void WriteOnly_Excluded_By_Default()
    {
        var document = UpdateDocumentBuilder.Build(new[]
        {
            new FieldChange { Path = "Password", Kind = ChangeKind.Set, NewValue = "secret", Visibility = ChangeVisibility.WriteOnly }
        });

        Assert.DoesNotContain("Password", document);
    }

    [Fact]
    public void WriteOnly_Included_When_Filter_Allows()
    {
        var document = UpdateDocumentBuilder.Build(
            new[] { new FieldChange { Path = "Password", Kind = ChangeKind.Set, NewValue = "secret", Visibility = ChangeVisibility.WriteOnly } },
            new VisibilityChangeSetFilter { IncludeWriteOnly = true });

        Assert.Equal("secret", ReadSet(document, "Password"));
    }

    [Fact]
    public void WholeArray_Replaces_Array()
    {
        var document = UpdateDocumentBuilder.Build(new[]
        {
            new FieldChange { Path = "Tags", Kind = ChangeKind.Set, NewValue = new[] { "a", "b" } }
        });

        using var parsed = JsonDocument.Parse(document);
        Assert.Equal(2, parsed.RootElement.GetProperty("$set").GetProperty("Tags").GetArrayLength());
    }

    [Fact]
    public void NoOp_Is_Empty_Object()
    {
        Assert.Equal("{}", UpdateDocumentBuilder.Build(Array.Empty<FieldChange>()));
    }

    [Fact]
    public void ReadOnly_Excluded_By_Default()
    {
        var document = UpdateDocumentBuilder.Build(new[]
        {
            new FieldChange { Path = "CreatedBy", Kind = ChangeKind.Set, NewValue = "system", Visibility = ChangeVisibility.ReadOnly }
        });

        Assert.DoesNotContain("CreatedBy", document);
    }

    [Fact]
    public void ReadOnly_Included_When_Filter_Allows()
    {
        var document = UpdateDocumentBuilder.Build(
            new[] { new FieldChange { Path = "CreatedBy", Kind = ChangeKind.Set, NewValue = "system", Visibility = ChangeVisibility.ReadOnly } },
            new VisibilityChangeSetFilter { IncludeReadOnly = true });

        Assert.Equal("system", ReadSet(document, "CreatedBy"));
    }

    private static string? ReadSet(string document, string path)
    {
        using var parsed = JsonDocument.Parse(document);

        return parsed.RootElement.TryGetProperty("$set", out var set) && set.TryGetProperty(path, out var value)
            ? value.GetString()
            : null;
    }
}
