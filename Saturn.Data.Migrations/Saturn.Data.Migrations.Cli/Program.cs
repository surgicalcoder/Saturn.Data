using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Migrations;
using Saturn.Data.LiteDbX;
using Saturn.Data.MongoDb;
using Saturn.Data.Sqlite;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: Saturn.Data.Migrations.Cli <litedbx|sqlite|mongodb> <connection-string> [--assembly <path>]... [--dry-run] [--include-deleted]");
    return 1;
}

var provider = args[0].ToLowerInvariant();
var connectionString = args[1];
var assemblies = new List<string>();
var dryRun = false;
var includeDeleted = false;

for (var i = 2; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--assembly" when i + 1 < args.Length:
            assemblies.Add(args[++i]);
            break;
        case "--dry-run":
            dryRun = true;
            break;
        case "--include-deleted":
            includeDeleted = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 1;
    }
}

var repositoryOptions = new RepositoryOptions { GetCollectionName = type => type.Name };

using var repository = CreateRepository(provider, repositoryOptions, connectionString);

if (repository is not IMigrationStoreSource source)
{
    Console.Error.WriteLine($"Provider '{provider}' does not expose a migration store.");
    return 2;
}

var modules = LoadModules(assemblies);

if (modules.Count == 0)
{
    Console.Error.WriteLine("No migration modules found. Pass --assembly <path> pointing at an assembly containing IMigrationModule types.");
    return 2;
}

var runner = source.CreateMigrationStore().Migrations();

foreach (var module in modules)
{
    module.Register(runner);
}

runner.WithDryRun(dryRun).WithIncludeDeleted(includeDeleted);

var report = await runner.RunAsync();

foreach (var execution in report.Migrations)
{
    Console.WriteLine($"{execution.Name}: applied={execution.WasApplied} skipped={execution.WasSkipped} dryRun={execution.IsDryRun} scanned={execution.DocumentsScanned} modified={execution.DocumentsModified} inserted={execution.DocumentsInserted} generated={execution.GeneratedIdMappings} repaired={execution.RepairedReferences}");
}

return 0;

IDisposable CreateRepository(string name, RepositoryOptions options, string connectionStringValue) => name switch
{
    "litedbx" => new LiteDbRepository(options, new LiteDBRepositoryOptions { ConnectionString = connectionStringValue, Mapper = new CustomEntityMapper() }),
    "sqlite" => new SqliteRepository(options, new SqliteRepositoryOptions { DataSource = connectionStringValue }),
    "mongodb" => new MongoDbRepository(options, new MongoDbRepositoryOptions { ConnectionString = connectionStringValue }),
    _ => throw new NotSupportedException($"Unsupported provider '{name}'. Supported: litedbx, sqlite, mongodb.")
};

static List<IMigrationModule> LoadModules(IEnumerable<string> paths)
{
    var modules = new List<IMigrationModule>();

    foreach (var path in paths)
    {
        Assembly assembly;

        try
        {
            assembly = Assembly.LoadFrom(path);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to load '{path}': {exception.Message}");
            return modules;
        }

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(IMigrationModule).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
            {
                continue;
            }

            if (Activator.CreateInstance(type) is IMigrationModule module)
            {
                modules.Add(module);
            }
        }
    }

    return modules;
}
