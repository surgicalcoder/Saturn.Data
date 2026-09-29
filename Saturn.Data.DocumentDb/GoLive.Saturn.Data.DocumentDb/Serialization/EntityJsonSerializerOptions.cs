namespace Saturn.Data.DocumentDb.Serialization;

public sealed class EntityJsonSerializerOptions
{
    public bool WriteIndented { get; set; }

    public bool EnumAsString { get; set; } = true;
}
