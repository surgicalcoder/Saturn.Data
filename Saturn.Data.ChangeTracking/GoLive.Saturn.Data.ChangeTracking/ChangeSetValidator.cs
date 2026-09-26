namespace GoLive.Saturn.Data.ChangeTracking;

public static class ChangeSetValidator
{
    public static void ValidatePatch(IEnumerable<FieldChange> changes, IReadOnlySet<string> allowed)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(allowed);

        foreach (var change in changes)
        {
            var root = change.Path;
            var separator = root.IndexOf('.');

            if (separator >= 0)
            {
                root = root[..separator];
            }

            if (!allowed.Contains(root))
            {
                throw new InvalidOperationException($"Field '{change.Path}' is not patchable for this view.");
            }
        }
    }
}
