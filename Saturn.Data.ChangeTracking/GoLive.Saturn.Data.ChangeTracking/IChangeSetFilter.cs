namespace GoLive.Saturn.Data.ChangeTracking;

public interface IChangeSetFilter
{
    FieldChange? Filter(FieldChange change);
}

public sealed class VisibilityChangeSetFilter : IChangeSetFilter
{
    public bool IncludeWriteOnly { get; set; }

    public bool IncludeServerManaged { get; set; }

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

        return change;
    }
}
