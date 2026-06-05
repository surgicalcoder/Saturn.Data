using System;

namespace GoLive.Saturn.Data.Entities;

public interface IArchivable
{
    bool IsArchived { get; set; }
    DateTime? ArchivedAt { get; set; }
    string ArchivedBy { get; set; }
}

