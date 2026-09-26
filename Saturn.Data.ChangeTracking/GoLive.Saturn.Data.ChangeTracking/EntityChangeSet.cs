namespace GoLive.Saturn.Data.ChangeTracking;

public sealed class EntityChangeSet
{
    public string EntityType { get; set; }

    public string Id { get; set; }

    public long? ExpectedVersion { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }

    public IReadOnlyList<FieldChange> Fields { get; set; } = Array.Empty<FieldChange>();

    public bool IsEmpty => Fields.Count == 0;

    public string ToUpdateDocument() => UpdateDocumentBuilder.Build(Fields);

    public string ToUpdateDocument(IChangeSetFilter filter) => UpdateDocumentBuilder.Build(Fields, filter);
}
