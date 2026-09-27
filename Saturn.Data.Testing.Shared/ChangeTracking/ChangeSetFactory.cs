using GoLive.Saturn.Data.ChangeTracking;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Testing.Shared.ChangeTracking;

public static class ChangeSetFactory
{
    public static EntityChangeSet Set<TEntity>(TEntity entity, params FieldChange[] fields) where TEntity : Entity
        => new()
        {
            EntityType = typeof(TEntity).Name,
            Id = entity.Id,
            ExpectedVersion = entity.Version,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Fields = fields
        };

    public static FieldChange Set(string path, object? value)
        => new() { Path = path, Kind = ChangeKind.Set, NewValue = value };

    public static FieldChange Unset(string path)
        => new() { Path = path, Kind = ChangeKind.Unset };

    public static FieldChange Increment(string path, object delta)
        => new() { Path = path, Kind = ChangeKind.Increment, NewValue = delta };
}
