using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Migrations;

public sealed class BackupCleanupOptions
{
    public string CollectionFilter { get; set; }

    public int KeepLatestCount { get; set; } = 1;
}

public sealed class BackupCleanupReport
{
    public BackupCleanupReport(IReadOnlyList<string> dropped, IReadOnlyList<string> retained)
    {
        Dropped = dropped ?? Array.Empty<string>();
        Retained = retained ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> Dropped { get; }

    public IReadOnlyList<string> Retained { get; }
}
