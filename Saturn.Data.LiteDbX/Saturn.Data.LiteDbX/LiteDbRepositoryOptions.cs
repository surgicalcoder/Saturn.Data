using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

public class LiteDBRepositoryOptions : RepositoryOptions
{
    public string ConnectionString { get; set; }
    public BsonMapper Mapper { get; set; } = new CustomEntityMapper();
}