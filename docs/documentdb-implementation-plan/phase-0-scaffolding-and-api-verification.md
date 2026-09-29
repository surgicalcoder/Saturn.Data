# Phase 0 — Scaffolding and API Verification

**Goal:** Create the `GoLive.Saturn.Data.DocumentDb` projects, wire them into the solution, and — critically — **prove the `Shiny.DocumentDb` API shapes this plan depends on**. Every later phase assumes the probe results.

**Prerequisite:** Read `README.md` in this folder. Read its "verified facts" table. Do not modify any existing project.

**Exit criteria:**
- `dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug` succeeds.
- `ProviderApiTests` passes on the SQLite backend, and its output records exactly which optional features (`ExecuteUpdate`, `ExecuteDelete`, `SetProperty`, JSON collections, `MapVersionProperty`, `AddSoftDelete`, `CreateIndexAsync`) work on SQLite.
- `SmokeTests` passes: an entity inserted through the repository reads back by id.
- If any probe contradicts this plan, you have **updated** the README facts table and the affected phase files.

---

## Task 0.0 — Read the template first

Before writing code, read the SQLite provider's `SqliteRepository.cs`, `SqliteRepository.ReadonlyRepository.cs`, `SqliteRepository.Repository.cs`, and `Saturn.Data.Sqlite.Tests\DatabaseFixture.cs` + `UnitTestableSqliteRepository.cs`. You are mirroring their structure.

---

## Task 0.1 — Create the project folders

- `D:\Work\Saturn.Data\Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb\`
- `D:\Work\Saturn.Data\Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb.Tests\`

---

## Task 0.2 — Create the library csproj

`D:\Work\Saturn.Data\Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>latest</LangVersion>
        <Version>7.0.0</Version>
    </PropertyGroup>

    <PropertyGroup>
        <PackageId>GoLive.Saturn.Data.DocumentDb</PackageId>
        <Authors>SurgicalCoder</Authors>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        <Description>Shiny.DocumentDb-backed document provider for Saturn.Data, supporting every database Shiny.DocumentDb supports.</Description>
        <Copyright>Copyright 2020-2026 - SurgicalCoder</Copyright>
        <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
        <PackageLicenseExpression>MIT</PackageLicenseExpression>
        <PublishRepositoryUrl>true</PublishRepositoryUrl>
        <GenerateRepositoryUrlAttribute>true</GenerateRepositoryUrlAttribute>
        <PackOnBuild>true</PackOnBuild>
        <PackageProjectUrl>https://github.com/surgicalcoder/Saturn.Data</PackageProjectUrl>
        <RepositoryUrl>https://github.com/surgicalcoder/Saturn.Data</RepositoryUrl>
        <RepositoryType>git</RepositoryType>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions.csproj"/>
        <ProjectReference Include="..\..\Saturn.Data.Entities\GoLive.Saturn.Data.Entities\GoLive.Saturn.Data.Entities.csproj"/>
        <ProjectReference Include="..\..\Saturn.Data.Entities\Saturn.Data.Entities.JsonConverters\Saturn.Data.Entities.JsonConverters.csproj"/>
        <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.12" />
        <PackageReference Include="ServiceScan.SourceGenerator" Version="3.2.2">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
    </ItemGroup>

</Project>
```

Then add the DocumentDb packages by command (do not guess versions):

```powershell
dotnet add "D:\Work\Saturn.Data\Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb.csproj" package Shiny.DocumentDb
```

> **Do not** add a backend package (`Shiny.DocumentDb.Sqlite`, `.DuckDb`, …) to the **library**. The library is backend-agnostic — it consumes an `IDocumentStore` the consumer built, exactly as the Stellar provider consumes a connection string and the Mongo provider consumes a client. Backend packages go in the **tests only**.

---

## Task 0.3 — Create the tests csproj

`...\GoLive.Saturn.Data.DocumentDb.Tests\GoLive.Saturn.Data.DocumentDb.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>latest</LangVersion>
        <IsPackable>false</IsPackable>
        <IsPublishable>false</IsPublishable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="coverlet.collector" Version="10.0.1">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
        <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.12" />
        <PackageReference Include="xunit" Version="2.9.3"/>
        <PackageReference Include="xunit.runner.visualstudio" Version="4.0.0">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
    </ItemGroup>

    <ItemGroup>
        <Using Include="Xunit"/>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\GoLive.Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb.csproj"/>
        <ProjectReference Include="..\..\Saturn.Data.Testing.Shared\Saturn.Data.Testing.Shared.csproj"/>
    </ItemGroup>

</Project>
```

Add the backend packages (tests only):

```powershell
dotnet add "...\GoLive.Saturn.Data.DocumentDb.Tests\GoLive.Saturn.Data.DocumentDb.Tests.csproj" package Shiny.DocumentDb.Sqlite
dotnet add "...\GoLive.Saturn.Data.DocumentDb.Tests\GoLive.Saturn.Data.DocumentDb.Tests.csproj" package Shiny.DocumentDb.DuckDb
```

---

## Task 0.4 — Add both projects to the solution

`D:\Work\Saturn.Data\Saturn.Data.slnx` — add a folder block next to the existing `/Sqlite/` block:

```xml
  <Folder Name="/DocumentDb/">
    <Project Path="Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb.Tests\GoLive.Saturn.Data.DocumentDb.Tests.csproj" Type="Classic C#" />
    <Project Path="Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb.csproj" Type="Classic C#" />
  </Folder>
```

---

## Task 0.5 — Create `DocumentDbRepositoryOptions.cs`

`...\GoLive.Saturn.Data.DocumentDb\DocumentDbRepositoryOptions.cs`:

```csharp
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

public enum UnsupportedPredicateBehaviour
{
    Throw,
    FallbackToClient
}

public enum PatchStrategy
{
    ExecuteUpdate,
    ReadModifyWrite,
    JsonLane
}

public enum ChangeFeedMode
{
    Outbox,
    Native
}

public sealed class DocumentDbEntitySerializerOptions
{
    public bool WriteIndented { get; set; }

    public bool EnumAsString { get; set; } = true;
}

public sealed class DocumentDbRepositoryOptions
{
    public IDocumentStore Store { get; set; }

    public Action<DocumentStoreOptions> ConfigureStore { get; set; }

    public string BackendName { get; set; }

    public string DefaultTableName { get; set; } = "documents";

    public bool HonorCollectionNames { get; set; }

    public UnsupportedPredicateBehaviour UnsupportedPredicateBehaviour { get; set; } = UnsupportedPredicateBehaviour.Throw;

    public PatchStrategy PatchStrategy { get; set; } = PatchStrategy.ExecuteUpdate;

    public ChangeFeedMode ChangeFeedMode { get; set; } = ChangeFeedMode.Outbox;

    public JsonSerializerContext JsonSerializerContext { get; set; }

    public IJsonTypeInfoResolver AdditionalTypeInfoResolver { get; set; }

    public bool UseReflectionFallback { get; set; } = true;

    public Action<string> OnUnsupportedIndexOption { get; set; }

    public Action<string> OnClientSideFallback { get; set; }

    public DocumentDbEntitySerializerOptions Serializer { get; set; } = new();
}
```

> `IDocumentStore` and `DocumentStoreOptions` live in the `Shiny.DocumentDb` namespace. If the compiler disagrees, correct the `using` — do **not** invent different type names.

---

## Task 0.6 — Create `DocumentDbCapabilities.cs`

`...\GoLive.Saturn.Data.DocumentDb\DocumentDbCapabilities.cs`:

```csharp
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

public sealed class DocumentDbCapabilities
{
    public required string BackendName { get; init; }

    public required bool SupportsTransactions { get; init; }

    public required bool SupportsExecuteUpdate { get; init; }

    public required bool SupportsExecuteDelete { get; init; }

    public required bool SupportsSetProperty { get; init; }

    public required bool SupportsJsonLane { get; init; }

    public required bool SupportsCreateIndex { get; init; }

    public required bool SupportsCollectionPredicates { get; init; }

    public required bool RequiresSingleConnection { get; init; }

    public static DocumentDbCapabilities Probe(IDocumentStore store, string backendName, DocumentDbRepositoryOptions options)
    {
        return new DocumentDbCapabilities
        {
            BackendName = backendName,
            SupportsTransactions = SafeProbe(() => store.SupportsTransactions, false),
            SupportsExecuteUpdate = BackendFeatureTable.SupportsExecuteUpdate(backendName),
            SupportsExecuteDelete = BackendFeatureTable.SupportsExecuteDelete(backendName),
            SupportsSetProperty = BackendFeatureTable.SupportsSetProperty(backendName),
            SupportsJsonLane = BackendFeatureTable.SupportsJsonLane(backendName),
            SupportsCreateIndex = true,
            SupportsCollectionPredicates = BackendFeatureTable.SupportsCollectionPredicates(backendName),
            RequiresSingleConnection = BackendFeatureTable.RequiresSingleConnection(backendName)
        };
    }

    private static bool SafeProbe(Func<bool> probe, bool fallback)
    {
        try
        {
            return probe();
        }
        catch
        {
            return fallback;
        }
    }
}
```

And `...\BackendFeatureTable.cs`:

```csharp
namespace Saturn.Data.DocumentDb;

internal static class BackendFeatureTable
{
    public static bool RequiresSingleConnection(string backend) => backend is "SqliteDatabaseProvider" or "SqlCipherDatabaseProvider" or "DuckDbDatabaseProvider";

    public static bool SupportsJsonLane(string backend) => backend is "SqliteDatabaseProvider" or "SqlCipherDatabaseProvider" or "DuckDbDatabaseProvider" or "PostgreSqlDatabaseProvider" or "CockroachDbDatabaseProvider" or "SqlServerDatabaseProvider" or "MySqlDatabaseProvider" or "MariaDbDatabaseProvider" or "OracleDatabaseProvider";

    public static bool SupportsExecuteUpdate(string backend) => SupportsJsonLane(backend) || backend is "MongoDbDocumentStore" or "CosmosDbDocumentStore";

    public static bool SupportsExecuteDelete(string backend) => SupportsExecuteUpdate(backend);

    public static bool SupportsSetProperty(string backend) => SupportsJsonLane(backend);

    public static bool SupportsCollectionPredicates(string backend) => backend is not "MariaDbDatabaseProvider";
}
```

> **These tables are provisional.** They were derived from the library's documentation, not from execution. Phase 0.8's probe records what SQLite actually does; Phase 7 validates the rest against real backends. When a row is wrong, fix it here and in the README facts table — never work around it in the repository code.

---

## Task 0.7 — Create `DocumentDbRepository.cs` (skeleton)

`...\GoLive.Saturn.Data.DocumentDb\DocumentDbRepository.cs`:

```csharp
using System.Collections.Concurrent;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : IDisposable
{
    private readonly RepositoryOptions options;
    private readonly DocumentDbRepositoryOptions documentDbOptions;
    private readonly IDocumentStore store;
    private readonly bool ownsStore;
    private readonly SemaphoreSlim initializationLock = new(1, 1);

    private DocumentDbCapabilities capabilities;
    private bool initialized;
    private bool disposed;

    public DocumentDbRepository(RepositoryOptions options, DocumentDbRepositoryOptions documentDbOptions)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.documentDbOptions = documentDbOptions ?? throw new ArgumentNullException(nameof(documentDbOptions));

        if (documentDbOptions.Store is not null)
        {
            store = documentDbOptions.Store;
            ownsStore = false;
        }
        else if (documentDbOptions.ConfigureStore is not null)
        {
            var storeOptions = new DocumentStoreOptions { TableName = documentDbOptions.DefaultTableName };
            documentDbOptions.ConfigureStore(storeOptions);

            if (!documentDbOptions.UseReflectionFallback)
            {
                storeOptions.UseReflectionFallback = false;
            }

            store = new DocumentStore(storeOptions);
            ownsStore = true;
        }
        else
        {
            throw new InvalidOperationException("DocumentDbRepositoryOptions requires either Store or ConfigureStore.");
        }
    }

    internal RepositoryOptions Options => options;

    internal DocumentDbRepositoryOptions DocumentDbOptions => documentDbOptions;

    internal IDocumentStore Store => store;

    internal DocumentDbCapabilities Capabilities => capabilities;

    protected string GetCollectionNameForType<TItem>() where TItem : Entity => options.GetCollectionName(typeof(TItem));

    internal static bool SupportsSoftDelete<TItem>() where TItem : Entity => typeof(ISoftDeletable).IsAssignableFrom(typeof(TItem));

    internal static bool SupportsArchivable<TItem>() where TItem : Entity => typeof(IArchivable).IsAssignableFrom(typeof(TItem));

    protected static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return Entity.TryParseId(id, out var normalized) && normalized is not null ? normalized : null;
    }

    protected static List<string> NormalizeEntityIds(IEnumerable<string> ids)
    {
        var result = new List<string>();

        if (ids is null)
        {
            return result;
        }

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (Entity.TryParseId(id, out var normalized) && normalized is not null && !result.Contains(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (initialized)
            {
                return;
            }

            var backendName = documentDbOptions.BackendName ?? store.GetType().Name;
            capabilities = DocumentDbCapabilities.Probe(store, backendName, documentDbOptions);
            initialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        initializationLock.Dispose();

        if (ownsStore && store is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
```

> If `UseReflectionFallback` / `SupportsTransactions` / `DocumentStoreOptions` shapes differ, fix the code to the real API and note the correction.

---

## Task 0.8 — Create the API probe test (the phase's real deliverable)

`...\GoLive.Saturn.Data.DocumentDb.Tests\ProviderApiTests.cs`:

```csharp
using Shiny.DocumentDb;
using Shiny.DocumentDb.Sqlite;
using Xunit.Abstractions;

namespace Saturn.Data.DocumentDb.Tests;

public class ProviderApiTests(ITestOutputHelper output)
{
    private sealed class Probe
    {
        public string Id { get; set; } = ""

        // no more needed for shape probing
        ;
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public long Version { get; set; }
        public bool IsDeleted { get; set; }
    }

    private static DocumentStore CreateStore()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-probe-{Guid.NewGuid():N}.db");
        return new DocumentStore(new DocumentStoreOptions
        {
            DatabaseProvider = new SqliteDatabaseProvider($"Data Source={path}")
        });
    }

    private const string HexId = "68bdd5525324ff2610c4361d";

    [Fact]
    public async Task Insert_With_Explicit_24Hex_Id_Writes_It_Back_Unchanged()
    {
        using var store = CreateStore();
        var probe = new Probe { Id = HexId, Name = "a" };

        await store.Insert(probe);

        Assert.Equal(HexId, probe.Id);
        var fetched = await store.Get<Probe>(HexId);
        Assert.NotNull(fetched);
        Assert.Equal(HexId, fetched!.Id);
    }

    [Fact]
    public async Task Update_Replaces_And_Upsert_Merges()
    {
        using var store = CreateStore();
        await store.Insert(new Probe { Id = HexId, Name = "a", Count = 1 });

        await store.Update(new Probe { Id = HexId, Name = "b", Count = 2 });
        var updated = await store.Get<Probe>(HexId);
        Assert.Equal("b", updated!.Name);

        await store.Upsert(new Probe { Id = HexId, Name = "c" });
        var merged = await store.Get<Probe>(HexId);
        Assert.Equal("c", merged!.Name);
    }

    [Fact]
    public async Task Remove_Returns_True_Then_False()
    {
        using var store = CreateStore();
        await store.Insert(new Probe { Id = HexId, Name = "a" });

        Assert.True(await store.Remove<Probe>(HexId));
        Assert.False(await store.Remove<Probe>(HexId));
    }

    [Fact]
    public async Task Batch_Insert_And_Remove()
    {
        using var store = CreateStore();
        var items = Enumerable.Range(0, 20)
            .Select(index => new Probe { Id = $"pad{index:D21}", Name = $"n{index}" })
            .ToList();

        var inserted = await store.BatchInsert(items);
        Assert.Equal(20, inserted);

        var removed = await store.BatchRemove<Probe>(items.Take(5).Select(item => item.Id).ToList());
        Assert.Equal(5, removed);
    }

    [Fact]
    public async Task Query_Where_OrderBy_Paginate_Count_First_WhereIn()
    {
        using var store = CreateStore();
        await store.BatchInsert(Enumerable.Range(0, 30)
            .Select(index => new Probe { Id = $"pad{index:D21}", Name = $"n{index:D2}", Count = index })
            .ToList());

        var page = await store.Query<Probe>().Where(item => item.Count >= 10).OrderBy(item => item.Count).Paginate(0, 5).ToList();
        Assert.Equal(5, page.Count);
        Assert.Equal(10, page[0].Count);

        var count = await store.Query<Probe>().Where(item => item.Count >= 10).Count();
        Assert.Equal(20, count);

        var first = await store.Query<Probe>().OrderBy(item => item.Count).First();
        Assert.Equal(0, first.Count);

        var byIds = await store.Query<Probe>()
            .WhereIn(item => item.Id, new List<string> { "pad000000000000000000000", "pad000000000000000000001" })
            .ToList();
        Assert.Equal(2, byIds.Count);
    }

    [Fact]
    public async Task Id_Range_Query_Is_Ordered_And_Seeks()
    {
        using var store = CreateStore();
        await store.BatchInsert(Enumerable.Range(0, 30)
            .Select(index => new Probe { Id = $"pad{index:D21}", Name = $"n{index:D2}" })
            .ToList());

        var after = await store.Query<Probe>().Where(item => string.Compare(item.Id, "pad000000000000000000015") > 0).OrderBy(item => item.Id).Paginate(0, 5).ToList();

        Assert.Equal(5, after.Count);
        Assert.Equal("pad000000000000000000016", after[0].Id);
    }

    [Fact]
    public async Task ExecuteUpdate_And_ExecuteDelete_Record_Capability()
    {
        using var store = CreateStore();
        await store.Insert(new Probe { Id = HexId, Name = "a", Count = 1 });

        var updateSupported = true;

        try
        {
            await store.Query<Probe>().Where(item => item.Id == HexId).ExecuteUpdate(builder => builder.Set(item => item.Count, 5));
        }
        catch (NotSupportedException)
        {
            updateSupported = false;
        }

        output.WriteLine($"ExecuteUpdate supported on SQLite: {updateSupported}");

        var deleteSupported = true;

        try
        {
            await store.Query<Probe>().Where(item => item.Id == HexId).ExecuteDelete();
        }
        catch (NotSupportedException)
        {
            deleteSupported = false;
        }

        output.WriteLine($"ExecuteDelete supported on SQLite: {deleteSupported}");
    }

    [Fact]
    public async Task SetProperty_And_RemoveProperty_Record_Capability()
    {
        using var store = CreateStore();
        await store.Insert(new Probe { Id = HexId, Name = "a" });

        var setSupported = true;

        try
        {
            await store.SetProperty<Probe>(HexId, item => item.Name, "b");
        }
        catch (NotSupportedException)
        {
            setSupported = false;
        }

        output.WriteLine($"SetProperty supported on SQLite: {setSupported}");
    }

    [Fact]
    public async Task OpenSession_Commits_And_Rolls_Back()
    {
        using var store = CreateStore();

        await using (var session = store.OpenSession())
        {
            session.Add(new Probe { Id = HexId, Name = "committed" });
            await session.SaveChanges();
        }

        Assert.NotNull(await store.Get<Probe>(HexId));

        var rolledBackId = "pad000000000000000000099";

        try
        {
            await using var session = store.OpenSession();
            session.Add(new Probe { Id = rolledBackId, Name = "rolled-back" });
            await session.SaveChanges();
        }
        catch (Exception exception)
        {
            output.WriteLine($"Session probe exception: {exception.GetType().Name}: {exception.Message}");
        }
    }

    [Fact]
    public void Capability_Properties_Read()
    {
        using var store = CreateStore();

        output.WriteLine($"SupportsTransactions={store.SupportsTransactions}");
        output.WriteLine($"SupportsSpatial={store.SupportsSpatial}");
        output.WriteLine($"SupportsVector={store.SupportsVector}");
        output.WriteLine($"SupportsFullText={store.SupportsFullText}");
        output.WriteLine($"SupportsRawJson={store.SupportsRawJson}");
    }

    [Fact]
    public async Task CreateIndexAsync_Works()
    {
        using var store = CreateStore();
        await store.Insert(new Probe { Id = HexId, Name = "a" });

        var supported = true;

        try
        {
            await store.CreateIndexAsync<Probe>(item => item.Name);
        }
        catch (NotSupportedException)
        {
            supported = false;
        }

        output.WriteLine($"CreateIndexAsync supported on SQLite: {supported}");
    }

    [Fact]
    public async Task VersionMapping_And_Delete_Flags_Probe()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-probe-v-{Guid.NewGuid():N}.db");
        var storeOptions = new DocumentStoreOptions
        {
            DatabaseProvider = new SqliteDatabaseProvider($"Data Source={path}")
        };

        var mapped = true;

        try
        {
            storeOptions.ConfigureDocument<Probe>(cfg =>
            {
                cfg.MapVersionProperty(item => item.Version);
                cfg.AddSoftDelete(item => item.IsDeleted);
            });
        }
        catch (Exception exception)
        {
            mapped = false;
            output.WriteLine($"ConfigureDocument probe exception: {exception.GetType().Name}: {exception.Message}");
        }

        output.WriteLine($"MapVersionProperty/AddSoftDelete mapped on SQLite: {mapped}");
        await Task.CompletedTask;
    }
}
```

**Note on the `Probe` class:** the property/field layout in the sample above is deliberately odd-looking; clean it up to a normal class with `Id`, `Name`, `Count`, `Version`, `IsDeleted`. Do not leave the stray brace/semicolon.

Run it:

```powershell
dotnet test "...\GoLive.Saturn.Data.DocumentDb.Tests.csproj" --filter "FullyQualifiedName~ProviderApiTests" --logger "console;verbosity=detailed"
```

**Then act on the output — this is mandatory:**

| Probe outcome | Action |
| --- | --- |
| A call does not compile | Read the real signature from the compiler error/metadata and **fix this plan** and the README table |
| `ExecuteUpdate`/`ExecuteDelete` unsupported on SQLite | Set the corresponding `BackendFeatureTable` row and README row accordingly |
| `SetProperty` unsupported | Same |
| `MapVersionProperty`/`AddSoftDelete` unsupported | Record it; Phase 5/4 use predicates instead |
| `SupportsTransactions` etc. do not exist | Remove from `DocumentDbCapabilities.Probe` and record what does exist |
| `OpenSession().Add(...)` shape differs | Record the real shape; Phase 5 depends on it |

---

## Task 0.9 — Create the test fixture and repository wrapper

`...\GoLive.Saturn.Data.DocumentDb.Tests\UnitTestableDocumentDbRepository.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb.Tests;

public sealed class UnitTestableDocumentDbRepository : DocumentDbRepository
{
    public UnitTestableDocumentDbRepository(RepositoryOptions repositoryOptions, DocumentDbRepositoryOptions documentDbOptions)
        : base(repositoryOptions, documentDbOptions)
    {
    }

    public async Task<long> CountRawAsync<TItem>(CancellationToken cancellationToken = default) where TItem : Entity
        => await Store.Query<TItem>().Count();
}
```

`...\DatabaseFixture.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;
using Shiny.DocumentDb;
using Shiny.DocumentDb.Sqlite;

namespace Saturn.Data.DocumentDb.Tests;

public class DatabaseFixture : IDisposable, IRepositoryTestFixture<UnitTestableDocumentDbRepository>
{
    public UnitTestableDocumentDbRepository Repository { get; }

    public DatabaseFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-{Guid.NewGuid():N}.db");

        Repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new DocumentDbRepositoryOptions
            {
                BackendName = nameof(SqliteDatabaseProvider),
                ConfigureStore = options => options.DatabaseProvider = new SqliteDatabaseProvider($"Data Source={path}")
            });

        Repository.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => Repository.Dispose();
}
```

---

## Task 0.10 — Create `SmokeTests.cs`

```csharp
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.DocumentDb.Tests;

public class SmokeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Store_Roundtrips_An_Entity()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "smoke" };
        await fixture.Repository.Store.Insert(entity);

        var fetched = await fixture.Repository.Store.Get<BasicEntity>(entity.Id);

        Assert.NotNull(fetched);
        Assert.Equal("smoke", fetched!.Name);
    }
}
```

> The insert above bypasses the repository deliberately — Phase 0 only proves the store works and the entity shape is acceptable. Phase 1 routes it through the repository.

---

## Task 0.11 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "...\GoLive.Saturn.Data.DocumentDb.Tests.csproj"
```

Expected: build succeeds; `ProviderApiTests` and `SmokeTests` pass; the probe test output lists which optional features work on SQLite.

---

## Do NOT

- Do not add a backend package to the **library** project.
- Do not implement any repository interface in this phase.
- Do not add per-type `ConfigureDocument` calls in the repository — core semantics are predicate-based (README design rule 1).
- Do not convert `Entity.Id` to a `Guid` (README decision 9).
- Do not modify shared projects or other providers.
- Do not add comments to `.cs` files.
