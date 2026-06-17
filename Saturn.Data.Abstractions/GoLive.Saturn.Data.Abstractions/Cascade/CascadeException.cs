using System;
using System.Collections.Generic;
using GoLive.Saturn.Data.Entities.Cascade;

namespace GoLive.Saturn.Data.Abstractions.Cascade;

public sealed class CascadeException : Exception
{
    public CascadeReport PartialReport { get; }
    public IReadOnlyList<(Type ChildType, string ChildId, CascadeMode Mode)> CompensationLog { get; }

    public CascadeException(
        string message,
        CascadeReport partialReport,
        IReadOnlyList<(Type ChildType, string ChildId, CascadeMode Mode)> compensationLog,
        Exception? inner = null)
        : base(message, inner)
    {
        PartialReport = partialReport;
        CompensationLog = compensationLog;
    }
}
