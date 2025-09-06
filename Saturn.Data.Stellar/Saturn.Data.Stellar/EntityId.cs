namespace Saturn.Data.Stellar;

public record struct EntityId : IComparable<EntityId>
{
    public EntityId() { }
    public EntityId(string id) {Id = id; }

    public string Id { get; set; } = GoLive.Saturn.Data.Entities.EntityIdGenerator.GenerateNewId();

    public override string ToString()
    {
        return $"Id: {Id}";
    }

    public int CompareTo(EntityId other)
    {
        return string.Compare(Id, other.Id, StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool operator <(EntityId left, EntityId right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(EntityId left, EntityId right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(EntityId left, EntityId right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(EntityId left, EntityId right)
    {
        return left.CompareTo(right) >= 0;
    }

    public static implicit operator EntityId(string id)
    {
        return new EntityId { Id = id };
    }

    public static implicit operator string(EntityId entityId)
    {
        return entityId.Id;
    }
}