using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using LiteDB;

namespace Saturn.Data.LiteDb;

public class LiteDBRepositoryOptions : RepositoryOptions
{
    public string ConnectionString { get; set; }
    public BsonMapper Mapper { get; set; } = new CustomEntityMapper();
    public Assembly[] AdditionalAssembliesToScanForRefs { get; set; } = [];
}