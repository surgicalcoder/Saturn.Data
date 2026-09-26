namespace Saturn.Data.Sqlite.Serialization;

public sealed class EntityJsonSerializerOptions
{
    public bool WriteIndented { get; set; }

    public bool EnumAsString { get; set; } = true;
}
