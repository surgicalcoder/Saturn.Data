using System.Collections.Generic;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Migrations;

namespace Saturn.Data.Migrations.Tests;

public class DocumentPathNavigatorTests
{
    [Fact]
    public void TryGet_ResolvesNestedProperty()
    {
        var document = new MigrationObject();
        var profile = new MigrationObject();
        profile.Set("CustomerId", MigrationValue.From("abc"));
        document.Set("Profile", profile);

        Assert.True(DocumentPathNavigator.TryGet(document, "Profile.CustomerId", out _, out var field, out var value));
        Assert.Equal("CustomerId", field);
        Assert.Equal("abc", value.AsString);
    }

    [Fact]
    public void TryAdd_CreatesIntermediateObjects()
    {
        var document = new MigrationObject();

        Assert.True(DocumentPathNavigator.TryAdd(document, "Profile.Customer.Name", MigrationValue.From("Ada"), FieldWriteMode.MissingOnly, createParents: true));
        Assert.True(DocumentPathNavigator.TryGet(document, "Profile.Customer.Name", out _, out _, out var value));
        Assert.Equal("Ada", value.AsString);
    }

    [Fact]
    public void TryRemove_PrunesEmptyParents()
    {
        var document = new MigrationObject();
        DocumentPathNavigator.TryAdd(document, "Profile.Customer.Name", MigrationValue.From("Ada"), FieldWriteMode.MissingOnly, createParents: true);

        Assert.True(DocumentPathNavigator.TryRemove(document, "Profile.Customer.Name", pruneEmptyParents: true));
        Assert.False(document.ContainsKey("Profile"));
    }

    [Fact]
    public void CreateContexts_ExpandsArrayWildcard()
    {
        var document = new MigrationObject();
        var scopes = new MigrationArray();
        scopes.Add(MigrationValue.From("a"));
        scopes.Add(MigrationValue.From("b"));
        document.Set("Scopes", scopes);

        var contexts = DocumentPathNavigator.CreateContexts(document, "Scopes[*]", includeLeafWhenMissing: false, "User", "test");

        Assert.Equal(2, contexts.Count);
        Assert.Equal("Scopes[0]", contexts[0].Path);
        Assert.Equal("Scopes[1]", contexts[1].Path);
    }

    [Fact]
    public void CreateContexts_ExpandsRecursiveDescent()
    {
        var document = new MigrationObject();
        var nested = new MigrationObject();
        nested.Set("LegacyId", MigrationValue.From("x"));
        document.Set("Child", nested);
        document.Set("LegacyId", MigrationValue.From("y"));

        var contexts = DocumentPathNavigator.CreateContexts(document, "**.LegacyId", includeLeafWhenMissing: false, "User", "test");

        var paths = new List<string>();
        foreach (var context in contexts)
        {
            paths.Add(context.Path);
        }

        Assert.Contains("LegacyId", paths);
        Assert.Contains("Child.LegacyId", paths);
    }

    [Fact]
    public void PathsConflict_DetectsAncestors()
    {
        Assert.True(DocumentPathNavigator.PathsConflict("Profile", "Profile.Customer"));
        Assert.False(DocumentPathNavigator.PathsConflict("Profile", "Account"));
    }
}
