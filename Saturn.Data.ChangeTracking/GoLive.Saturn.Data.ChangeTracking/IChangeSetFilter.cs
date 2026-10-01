namespace GoLive.Saturn.Data.ChangeTracking;

public interface IChangeSetFilter
{
    FieldChange? Filter(FieldChange change);
}

public sealed class VisibilityChangeSetFilter : IChangeSetFilter
{
    public bool IncludeWriteOnly { get; set; }

    public bool IncludeServerManaged { get; set; }

    public bool IncludeReadOnly { get; set; }

    public FieldChange? Filter(FieldChange change)
    {
        if (change.Visibility == ChangeVisibility.WriteOnly && !IncludeWriteOnly)
        {
            return null;
        }

        if (change.Visibility == ChangeVisibility.ServerManaged && !IncludeServerManaged)
        {
            return null;
        }

        if (change.Visibility == ChangeVisibility.ReadOnly && !IncludeReadOnly)
        {
            return null;
        }

        return change;
    }
}
