using System;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Testing.Shared.ChangeFeed;

public sealed class ChangeFeedEntity : Entity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string DeletedBy { get; set; } = string.Empty;
}
