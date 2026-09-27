namespace GoLive.Saturn.Data.ChangeTracking;

public sealed class ChangeTracker
{
    private static readonly HashSet<string> NonTrackedMembers = new(StringComparer.Ordinal)
    {
        "Id",
        "Version",
        "Changes",
        "EnableChangeTracking",
        "_shortId",
        "ChangeTrackingParent",
        "ChangeTrackingPathSegment"
    };

    private readonly List<FieldChange> journal = new();
    private readonly Dictionary<string, object?> baselineValues = new();

    public bool IsTracking { get; private set; }

    public bool IsHydrating { get; private set; }

    public bool HasChanges => journal.Count > 0;

    public IReadOnlyList<FieldChange> Journal => journal;

    public IChangeTrackingObserver? Observer { get; set; }

    public void Begin(ITrackable owner, bool acceptCurrentState)
    {
        journal.Clear();
        IsTracking = true;
        IsHydrating = false;

        if (acceptCurrentState)
        {
            owner.CaptureBaseline();
        }
    }

    public void Accept(ITrackable owner)
    {
        journal.Clear();
        owner.CaptureBaseline();
    }

    public void Reject(ITrackable owner)
    {
        owner.RestoreBaseline();
        journal.Clear();
    }

    public void Record(ITrackable owner, string propertyName, object? oldValue, object? newValue)
    {
        Append(owner, propertyName, oldValue, newValue, ChangeKind.Set, null, null, null);
    }

    public void RecordList(ITrackable owner, string propertyName, object? oldValue, object? newValue, ChangeKind kind, int? index,
        CollectionStrategy strategy, ChangeVisibility visibility)
    {
        Append(owner, propertyName, oldValue, newValue, kind, index, strategy, visibility);
    }

    public EntityChangeSet Build(ITrackable owner)
    {
        var fields = journal.ToList();
        fields.AddRange(owner.ComputeBaselineDiff());

        Observer?.OnChangeSetCaptured(owner.GetType().Name, fields.Count, fields.Count(change => change.Visibility == ChangeVisibility.WriteOnly));

        return new EntityChangeSet
        {
            EntityType = owner.GetType().Name,
            Id = owner.Id,
            ExpectedVersion = owner.Version,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Fields = fields
        };
    }

    public void CaptureValue(string memberName, object? value) => baselineValues[memberName] = value;

    public object? BaselineValue(string memberName) => baselineValues.TryGetValue(memberName, out var value) ? value : null;

    public IDisposable Suppress() => new HydrationScope(this);

    private void Append(ITrackable owner, string propertyName, object? oldValue, object? newValue, ChangeKind kind, int? index,
        CollectionStrategy? strategy, ChangeVisibility? visibility)
    {
        if (!IsTracking || IsHydrating || string.IsNullOrEmpty(propertyName) || NonTrackedMembers.Contains(propertyName))
        {
            return;
        }

        var root = ResolveRoot(owner);
        var metadata = root as ITrackableMetadata;
        var path = ComposePath(owner, propertyName);

        root.GetTracker().journal.Add(new FieldChange
        {
            Path = path,
            Kind = kind,
            OldValue = Normalize(oldValue),
            NewValue = Normalize(newValue),
            Index = index,
            Strategy = strategy ?? metadata?.StrategyFor(propertyName) ?? CollectionStrategy.WholeArray,
            Visibility = visibility ?? metadata?.VisibilityFor(propertyName) ?? ChangeVisibility.ReadWrite
        });
    }

    private static ITrackable ResolveRoot(ITrackable owner)
    {
        var current = owner;

        while (current.ChangeTrackingParent is ITrackable parent)
        {
            current = parent;
        }

        return current;
    }

    private static string ComposePath(ITrackable owner, string propertyName)
    {
        if (owner.ChangeTrackingParent is not ITrackable parent || string.IsNullOrEmpty(owner.ChangeTrackingPathSegment))
        {
            return propertyName;
        }

        return ComposePath(parent, owner.ChangeTrackingPathSegment) + "." + propertyName;
    }

    private static object? Normalize(object? value) => value switch
    {
        null => null,
        GoLive.Saturn.Data.Entities.IEntityReference reference => reference.RefId,
        _ => value
    };

    private sealed class HydrationScope : IDisposable
    {
        private readonly ChangeTracker tracker;
        private readonly bool previous;

        public HydrationScope(ChangeTracker tracker)
        {
            this.tracker = tracker;
            previous = tracker.IsHydrating;
            tracker.IsHydrating = true;
        }

        public void Dispose() => tracker.IsHydrating = previous;
    }
}
