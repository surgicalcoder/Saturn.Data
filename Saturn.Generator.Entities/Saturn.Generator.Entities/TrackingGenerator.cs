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
    public string Strategy { get; set; } = "WholeArray";
    public int PlainCollectionKind { get; set; }
    public string KeyTypeText { get; set; }
    public string ValueTypeText { get; set; }
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
                ElementTypeText = member.IsCollection
                    ? SourceCodeGenerator.RenderType(member.CollectionType ?? member.Type)
                    : member.PlainCollectionKind != 0 ? member.ElementTypeName : null,
                NoTracking = member.DoNotTrackChanges,
                Visibility = member.WriteOnly ? "WriteOnly" : member.ReadOnly ? "ReadOnly" : "ReadWrite",
                Strategy = StrategyName(member.CollectionStrategy),
                PlainCollectionKind = member.PlainCollectionKind,
                KeyTypeText = member.KeyTypeName,
                ValueTypeText = member.ValueTypeName
            })
            .ToList();

    private static string StrategyName(int strategy) => strategy switch
    {
        1 => "IndexedOps",
        2 => "SetOps",
        _ => "WholeArray"
    };

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
            source.AppendLine($"tracker.CaptureValue(\"{member.Name}\", {CaptureExpression(member)});");
        }

        source.AppendCloseCurlyBracketLine();

        source.AppendLine();
        source.AppendLine("public void RestoreBaseline()");
        source.AppendOpenCurlyBracketLine();

        foreach (var member in tracked)
        {
            source.AppendLine(RestoreStatement(member));
        }

        source.AppendCloseCurlyBracketLine();

        source.AppendLine();
        source.AppendLine($"public IEnumerable<{TrackingNamespace}.FieldChange> ComputeBaselineDiff()");
        source.AppendOpenCurlyBracketLine();

        var plainCollections = tracked.Where(member => member.PlainCollectionKind != 0).ToList();

        if (plainCollections.Count == 0)
        {
            source.AppendLine("yield break;");
        }
        else
        {
            foreach (var member in plainCollections)
            {
                foreach (var line in BaselineDiffLines(member))
                {
                    source.AppendLine(line);
                }
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
        source.AppendLine("return memberName switch");
        source.AppendOpenCurlyBracketLine();

        foreach (var member in members.Where(member => member.Strategy != "WholeArray"))
        {
            source.AppendLine($"\"{member.Name}\" => {TrackingNamespace}.CollectionStrategy.{member.Strategy},");
        }

        source.AppendLine($"_ => {TrackingNamespace}.CollectionStrategy.WholeArray");
        source.AppendCloseCurlyBracketLine();
        source.AppendLine(";");
        source.AppendCloseCurlyBracketLine();
    }

    private static string Camel(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static string CaptureExpression(TrackedMember member)
    {
        if (member.IsCollection)
        {
            return $"{member.Name} is null ? null : new List<{member.ElementTypeText}>({member.Name})";
        }

        return member.PlainCollectionKind switch
        {
            1 => $"{member.Name} is null ? null : new List<{member.ElementTypeText}>({member.Name})",
            2 => $"{member.Name} is null ? null : new List<{member.ElementTypeText}>({member.Name})",
            3 => $"{member.Name} is null ? null : new HashSet<{member.ElementTypeText}>({member.Name})",
            4 => $"{member.Name} is null ? null : new Dictionary<{member.KeyTypeText}, {member.ValueTypeText}>({member.Name})",
            _ => member.Name
        };
    }

    private static string RestoreStatement(TrackedMember member)
    {
        var value = Camel(member.Name);

        if (member.IsCollection)
        {
            return $"if (tracker.BaselineValue(\"{member.Name}\") is List<{member.ElementTypeText}> {value}Value) {member.Name} = new global::ObservableCollections.ObservableList<{member.ElementTypeText}>({value}Value);";
        }

        return member.PlainCollectionKind switch
        {
            1 => $"if (tracker.BaselineValue(\"{member.Name}\") is List<{member.ElementTypeText}> {value}Value) {member.Name} = {value}Value;",
            2 => $"if (tracker.BaselineValue(\"{member.Name}\") is List<{member.ElementTypeText}> {value}Value) {member.Name} = {value}Value.ToArray();",
            3 => $"if (tracker.BaselineValue(\"{member.Name}\") is HashSet<{member.ElementTypeText}> {value}Value) {member.Name} = {value}Value;",
            4 => $"if (tracker.BaselineValue(\"{member.Name}\") is Dictionary<{member.KeyTypeText}, {member.ValueTypeText}> {value}Value) {member.Name} = {value}Value;",
            _ => member.IsValueType
                ? $"if (tracker.BaselineValue(\"{member.Name}\") is {member.TypeText} {value}Value) {member.Name} = {value}Value;"
                : $"{member.Name} = tracker.BaselineValue(\"{member.Name}\") as {member.TypeText.TrimEnd('?')};"
        };
    }

    private static IEnumerable<string> BaselineDiffLines(TrackedMember member)
    {
        var value = Camel(member.Name);

        switch (member.PlainCollectionKind)
        {
            case 1:
            case 2:
                yield return $"if (tracker.BaselineValue(\"{member.Name}\") is List<{member.ElementTypeText}> {value}Baseline)";
                yield return "{";
                yield return $"var {value}Current = {member.Name} is null ? null : new List<{member.ElementTypeText}>({member.Name});";
                yield return $"if (!{TrackingNamespace}.CollectionDiff.ListEqual({value}Baseline, {value}Current, global::System.Collections.Generic.EqualityComparer<{member.ElementTypeText}>.Default))";
                yield return "{";
                yield return $"yield return new {TrackingNamespace}.FieldChange {{ Path = \"{member.Name}\", Kind = {TrackingNamespace}.ChangeKind.Set, OldValue = {value}Baseline, NewValue = {value}Current }};";
                yield return "}";
                yield return "}";
                break;

            case 3:
                yield return $"if (tracker.BaselineValue(\"{member.Name}\") is HashSet<{member.ElementTypeText}> {value}Baseline)";
                yield return "{";
                yield return $"var {value}Current = {member.Name} is null ? null : new HashSet<{member.ElementTypeText}>({member.Name});";
                yield return $"if (!{TrackingNamespace}.CollectionDiff.SetEqual({value}Baseline, {value}Current))";
                yield return "{";
                yield return $"yield return new {TrackingNamespace}.FieldChange {{ Path = \"{member.Name}\", Kind = {TrackingNamespace}.ChangeKind.Set, OldValue = {value}Baseline, NewValue = {value}Current }};";
                yield return "}";
                yield return "}";
                break;

            case 4:
                yield return $"if (tracker.BaselineValue(\"{member.Name}\") is Dictionary<{member.KeyTypeText}, {member.ValueTypeText}> {value}Baseline)";
                yield return "{";
                yield return $"var {value}Current = {member.Name} is null ? null : new Dictionary<{member.KeyTypeText}, {member.ValueTypeText}>({member.Name});";
                yield return $"if (!{TrackingNamespace}.CollectionDiff.DictionaryEqual({value}Baseline, {value}Current))";
                yield return "{";
                yield return $"yield return new {TrackingNamespace}.FieldChange {{ Path = \"{member.Name}\", Kind = {TrackingNamespace}.ChangeKind.Set, OldValue = {value}Baseline, NewValue = {value}Current }};";
                yield return "}";
                yield return "}";
                break;
        }
    }
}
