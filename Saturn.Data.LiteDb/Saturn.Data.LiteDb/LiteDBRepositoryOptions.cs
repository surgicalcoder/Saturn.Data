using GoLive.Saturn.Data.Abstractions;
using LiteDB;

namespace Saturn.Data.LiteDb;

public class LiteDBRepositoryOptions : RepositoryOptions
{
    public BsonMapper Mapper { get; set; } = new EntityMapper();
}