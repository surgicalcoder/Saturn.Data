using System.Text.Json.Nodes;
using GoLive.Saturn.Data.Abstractions;

namespace GoLive.Saturn.Data.ChangeTracking.Tests;

public class JsonPatchDocumentTests
{
    [Fact]
    public void Set_Nested_Path_Updates_Array_Element()
    {
        var document = JsonNode.Parse("""{ "Lines": [ { "Title": "a" } ] }""")!.AsObject();
        var patch = JsonNode.Parse("""{ "$set": { "Lines.0.Title": "b" } }""")!.AsObject();

        JsonPatchDocument.Apply(document, patch);

        Assert.Equal("b", document["Lines"]![0]!["Title"]!.GetValue<string>());
    }

    [Fact]
    public void Set_Nested_Path_Creates_Missing_Object()
    {
        var document = JsonNode.Parse("{}")!.AsObject();
        var patch = JsonNode.Parse("""{ "$set": { "Customer.Name": "x" } }""")!.AsObject();

        JsonPatchDocument.Apply(document, patch);

        Assert.Equal("x", document["Customer"]!["Name"]!.GetValue<string>());
    }

    [Fact]
    public void Unset_Nested_Path_Removes_Member_Only()
    {
        var document = JsonNode.Parse("""{ "Customer": { "Name": "x", "Age": 1 } }""")!.AsObject();
        var patch = JsonNode.Parse("""{ "$unset": { "Customer.Age": true } }""")!.AsObject();

        JsonPatchDocument.Apply(document, patch);

        Assert.Null(document["Customer"]!["Age"]);
        Assert.Equal("x", document["Customer"]!["Name"]!.GetValue<string>());
    }

    [Fact]
    public void Increment_Nested_Path_Adds_Delta()
    {
        var document = JsonNode.Parse("""{ "Stats": { "Count": 2 } }""")!.AsObject();
        var patch = JsonNode.Parse("""{ "$inc": { "Stats.Count": 3 } }""")!.AsObject();

        JsonPatchDocument.Apply(document, patch);

        Assert.Equal(5m, document["Stats"]!["Count"]!.GetValue<decimal>());
    }

    [Fact]
    public void AddToSet_Appends_Only_Once()
    {
        var document = JsonNode.Parse("""{ "Tags": [ "a" ] }""")!.AsObject();
        var patch = JsonNode.Parse("""{ "$addToSet": { "Tags": "b" } }""")!.AsObject();

        JsonPatchDocument.Apply(document, patch);
        JsonPatchDocument.Apply(document, patch);

        var tags = document["Tags"]!.AsArray();
        Assert.Equal(2, tags.Count);
        Assert.Equal("b", tags[1]!.GetValue<string>());
    }

    [Fact]
    public void Pull_Removes_All_Matching_Elements()
    {
        var document = JsonNode.Parse("""{ "Tags": [ "a", "b", "a" ] }""")!.AsObject();
        var patch = JsonNode.Parse("""{ "$pull": { "Tags": "a" } }""")!.AsObject();

        JsonPatchDocument.Apply(document, patch);

        var tags = document["Tags"]!.AsArray();
        Assert.Single(tags);
        Assert.Equal("b", tags[0]!.GetValue<string>());
    }

    [Fact]
    public void HasOperators_Distinguishes_Operators_From_Plain_Document()
    {
        Assert.True(JsonPatchDocument.HasOperators(JsonNode.Parse("""{ "$addToSet": { "Tags": "a" } }""")!.AsObject()));
        Assert.True(JsonPatchDocument.HasOperators(JsonNode.Parse("""{ "$pull": { "Tags": "a" } }""")!.AsObject()));
        Assert.False(JsonPatchDocument.HasOperators(JsonNode.Parse("""{ "Name": "x" }""")!.AsObject()));
    }
}
