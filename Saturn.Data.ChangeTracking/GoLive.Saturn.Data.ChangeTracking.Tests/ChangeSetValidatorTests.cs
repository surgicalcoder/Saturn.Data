using GoLive.Saturn.Data.ChangeTracking;

namespace GoLive.Saturn.Data.ChangeTracking.Tests;

public class ChangeSetValidatorTests
{
    [Fact]
    public void ValidatePatch_Accepts_Allowed_Field()
    {
        var changes = new[] { new FieldChange { Path = "Name" } };

        ChangeSetValidator.ValidatePatch(changes, new HashSet<string> { "Name" });
    }

    [Fact]
    public void ValidatePatch_Rejects_Unknown_Field()
    {
        var changes = new[] { new FieldChange { Path = "Secret" } };

        Assert.Throws<InvalidOperationException>(() => ChangeSetValidator.ValidatePatch(changes, new HashSet<string> { "Name" }));
    }

    [Fact]
    public void ValidatePatch_Allows_Nested_Path_When_Root_Allowed()
    {
        var changes = new[] { new FieldChange { Path = "Lines.0.Title" } };

        ChangeSetValidator.ValidatePatch(changes, new HashSet<string> { "Lines" });
    }
}
