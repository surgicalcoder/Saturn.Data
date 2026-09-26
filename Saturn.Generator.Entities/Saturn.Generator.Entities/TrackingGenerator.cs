using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GoLive.Saturn.Generator.Entities;

public sealed class TrackedMember
{
    public string Name { get; set; }
    public string TypeText { get; set; }
    public bool IsValueType { get; set; }
    public bool IsCollection { get; set; }
    public string ElementTypeText { get; set; }
    public bool NoTracking { get; set; }
    public string Visibility { get; set; } = "ReadWrite";
}

public static class TrackingGenerator
{
    private const string TrackingNamespace = "global::GoLive.Saturn.Data.ChangeTracking";

    public static IReadOnlyList<TrackedMember> FromClass(ClassToGenerate classToGen)
        => classToGen.Members
            .Where(member => member.Type is not null)
            .Select(member => new TrackedMember
            {
                Name = member.Name.FirstCharToUpper(),
                TypeText = SourceCodeGenerator.RenderType(member.Type),
                IsValueType = member.Type.IsValueType,
                IsCollection = member.IsCollection,
                ElementTypeText = member.IsCollection ? SourceCodeGenerator.RenderType(member.CollectionType ?? member.Type) : null,
                NoTracking = member.DoNotTrackChanges,
                Visibility = member.WriteOnly ? "WriteOnly" : member.ReadOnly ? "ReadOnly" : "ReadWrite"
            })
            .ToList();

    public static void EmitEntityTracking(SourceStringBuilder source, ClassToGenerate classToGen)
        => Emit(source, FromClass(classToGen), isEntity: true);

    public static void EmitDtoTracking(SourceStringBuilder source, IReadOnlyList<TrackedMember> members)
        => Emit(source, members, isEntity: false);

    private static void Emit(SourceStringBuilder source, IReadOnlyList<TrackedMember> members, bool isEntity)
    {
        var tracked = members.Where(member => !member.NoTracking).ToList();

        source.AppendLine(2);
        source.AppendLine($"private readonly {TrackingNamespace}.ChangeTracker tracker = new();");
        source.AppendLine("private object changeTrackingParent;");
        source.AppendLine("private string changeTrackingPathSegment;");
        source.AppendLine();
        source.AppendLine($"object? {TrackingNamespace}.ITrackable.ChangeTrackingParent {{ get => changeTrackingParent; set => changeTrackingParent = value; }}");
        source.AppendLine($"string? {TrackingNamespace}.ITrackable.ChangeTrackingPathSegment {{ get => changeTrackingPathSegment; set => changeTrackingPathSegment = value; }}");
        source.AppendLine($"public bool IsTracking => tracker.IsTracking;");
        source.AppendLine($"public bool HasChanges => tracker.HasChanges;");
        source.AppendLine($"public void BeginTracking(bool acceptCurrentState = true) => tracker.Begin(this, acceptCurrentState);");
        source.AppendLine($"public void AcceptChanges() => tracker.Accept(this);");
        source.AppendLine($"public void RejectChanges() => tracker.Reject(this);");
        source.AppendLine($"public {TrackingNamespace}.EntityChangeSet GetChangeSet() => tracker.Build(this);");
        source.AppendLine($"public string ToUpdateDocument() => tracker.Build(this).ToUpdateDocument();");
        source.AppendLine($"public {TrackingNamespace}.ChangeTracker GetTracker() => tracker;");
        source.AppendLine($"public IDisposable SuppressTracking() => tracker.Suppress();");
        source.AppendLine();

        if (isEntity)
        {
            source.AppendLine("protected override void OnFieldChanged(string propertyName, object oldValue, object newValue)");
            source.AppendOpenCurlyBracketLine();
            source.AppendLine("if (tracker.IsHydrating)");
            source.AppendOpenCurlyBracketLine();
            source.AppendLine("return;");
            source.AppendCloseCurlyBracketLine();
            source.AppendLine("tracker.Record(this, propertyName, oldValue, newValue);");
            source.AppendCloseCurlyBracketLine();
        }
        else
        {
            source.AppendLine("public void OnFieldChanged(string propertyName, object oldValue, object newValue)");
            source.AppendOpenCurlyBracketLine();
            source.AppendLine("if (tracker.IsHydrating)");
            source.AppendOpenCurlyBracketLine();
            source.AppendLine("return;");
            source.AppendCloseCurlyBracketLine();
            source.AppendLine("tracker.Record(this, propertyName, oldValue, newValue);");
            source.AppendCloseCurlyBracketLine();
        }

        source.AppendLine();
        source.AppendLine("public void CaptureBaseline()");
        source.AppendOpenCurlyBracketLine();

        foreach (var member in tracked)
        {
            if (member.IsCollection)
            {
                source.AppendLine($"tracker.CaptureValue(\"{member.Name}\", {member.Name} is null ? null : new List<{member.ElementTypeText}>({member.Name}));");
            }
            else
            {
                source.AppendLine($"tracker.CaptureValue(\"{member.Name}\", {member.Name});");
            }
        }

        source.AppendCloseCurlyBracketLine();

        source.AppendLine();
        source.AppendLine("public void RestoreBaseline()");
        source.AppendOpenCurlyBracketLine();

        foreach (var member in tracked)
        {
            if (member.IsCollection)
            {
                source.AppendLine($"if (tracker.BaselineValue(\"{member.Name}\") is List<{member.ElementTypeText}> {Camel(member.Name)}Value) {member.Name} = new global::ObservableCollections.ObservableList<{member.ElementTypeText}>({Camel(member.Name)}Value);");
            }
            else if (member.IsValueType)
            {
                source.AppendLine($"if (tracker.BaselineValue(\"{member.Name}\") is {member.TypeText} {Camel(member.Name)}Value) {member.Name} = {Camel(member.Name)}Value;");
            }
            else
            {
                source.AppendLine($"{member.Name} = tracker.BaselineValue(\"{member.Name}\") as {member.TypeText.TrimEnd('?')};");
            }
        }

        source.AppendCloseCurlyBracketLine();

        source.AppendLine();
        source.AppendLine($"public {TrackingNamespace}.ChangeVisibility VisibilityFor(string memberName)");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine("return memberName switch");
        source.AppendOpenCurlyBracketLine();

        foreach (var member in members.Where(member => member.Visibility != "ReadWrite"))
        {
            source.AppendLine($"\"{member.Name}\" => {TrackingNamespace}.ChangeVisibility.{member.Visibility},");
        }

        source.AppendLine($"_ => {TrackingNamespace}.ChangeVisibility.ReadWrite");
        source.AppendCloseCurlyBracketLine();
        source.AppendLine(";");
        source.AppendCloseCurlyBracketLine();

        source.AppendLine();
        source.AppendLine($"public {TrackingNamespace}.CollectionStrategy StrategyFor(string memberName)");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine($"return {TrackingNamespace}.CollectionStrategy.WholeArray;");
        source.AppendCloseCurlyBracketLine();
    }

    private static string Camel(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
}
