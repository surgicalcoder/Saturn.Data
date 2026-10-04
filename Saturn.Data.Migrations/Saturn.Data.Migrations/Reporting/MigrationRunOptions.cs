using System;

namespace GoLive.Saturn.Data.Migrations;

public sealed class MigrationRunOptions
{
    public bool DryRun { get; set; }

    public bool IncludeDeleted { get; set; }

    public bool ContinueOnError { get; set; }

    public BackupRetentionPolicy BackupRetention { get; set; } = BackupRetentionPolicy.KeepAll;

    public Action<MigrationProgress> ProgressCallback { get; set; }
}
