# Phase 0 — Scaffolding

**Goal:** Create the `Saturn.Data.Sqlite` library and `Saturn.Data.Sqlite.Tests` projects, wire them into the solution, and prove SQLite JSON1 + WAL are available. No repository methods are implemented yet.

**Prerequisite:** Read `README.md` in this folder. Do not modify any existing project.

**Exit criteria:**
- `dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug` succeeds.
- `dotnet test` on the tests project passes `SmokeTests` (JSON1 probe returns 1, WAL journal mode is active on a file database).

---

## Task 0.1 — Create the project folders

Create these directories (create intermediate folders as needed):

- `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\`
- `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\`

---

## Task 0.2 — Create the library csproj

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Saturn.Data.Sqlite.csproj` with exactly:

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
        <PackageId>GoLive.Saturn.Data.Sqlite</PackageId>
        <Authors>SurgicalCoder</Authors>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        <Description>SQLite JSON document provider for Saturn.Data</Description>
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

Then add the SQLite package by command (do not guess a version; let NuGet resolve the latest stable):

```powershell
dotnet add "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Saturn.Data.Sqlite.csproj" package Microsoft.Data.Sqlite
```

Confirm `Microsoft.Data.Sqlite` now appears in the `<ItemGroup>` with a resolved version.

---

## Task 0.3 — Create the tests csproj

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj` with exactly:

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
        <ProjectReference Include="..\Saturn.Data.Sqlite\Saturn.Data.Sqlite.csproj"/>
        <ProjectReference Include="..\..\Saturn.Data.Testing.Shared\Saturn.Data.Testing.Shared.csproj"/>
    </ItemGroup>

</Project>
```

---

## Task 0.4 — Add both projects to the solution

Open `D:\Work\Saturn.Data\Saturn.Data.slnx`. Add this folder block immediately after the `<Folder Name="/StellarDb/">...</Folder>` block and before the standalone `<Project Path="Saturn.Data.Abstractions...` entries:

```xml
  <Folder Name="/Sqlite/">
    <Project Path="Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj" Type="Classic C#" />
    <Project Path="Saturn.Data.Sqlite\Saturn.Data.Sqlite\Saturn.Data.Sqlite.csproj" Type="Classic C#" />
  </Folder>
```

---

## Task 0.5 — Create `SqliteRepositoryOptions.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepositoryOptions.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

public enum SqliteSynchronousMode
{
    Off,
    Normal,
    Full
}

public class SqliteRepositoryOptions
{
    public string DataSource { get; set; } = "saturn.db";

    public SqliteOpenMode Mode { get; set; } = SqliteOpenMode.ReadWriteCreate;

    public SqliteCacheMode Cache { get; set; } = SqliteCacheMode.Default;

    public bool Pooling { get; set; } = true;

    public string EncryptionPassword { get; set; }

    public bool UseJsonB { get; set; }

    public bool StrictTranslation { get; set; } = true;

    public bool UseJsonMergePatch { get; set; }

    public bool EnableWal { get; set; } = true;

    public bool RequireWal { get; set; } = true;

    public int BusyTimeoutSeconds { get; set; } = 30;

    public int BusyTimeoutMs { get; set; } = 5000;

    public SqliteSynchronousMode Synchronous { get; set; } = SqliteSynchronousMode.Normal;

    public int WalAutoCheckpointPages { get; set; } = 1000;

    public long JournalSizeLimitBytes { get; set; } = 67108864;

    public bool AllowNestedTransactions { get; set; }

    public bool ServerSideProjection { get; set; }

    public bool EnableFullTextSearch { get; set; }

    public Action<SqliteConnection> ConfigureConnection { get; set; }

    public Action<string> OnUnsupportedIndexOption { get; set; }
}
```

---

## Task 0.6 — Create `SqliteConnectionFactory.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteConnectionFactory.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

internal sealed class SqliteConnectionFactory
{
    private readonly SqliteRepositoryOptions options;
    private readonly string connectionString;

    public SqliteConnectionFactory(SqliteRepositoryOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = options.DataSource,
            Mode = options.Mode,
            Cache = options.Cache,
            Pooling = options.Pooling,
            DefaultTimeout = options.BusyTimeoutSeconds
        };

        if (!string.IsNullOrWhiteSpace(options.EncryptionPassword))
        {
            builder.Password = options.EncryptionPassword;
        }

        connectionString = builder.ToString();
    }

    public bool IsMemory => string.Equals(options.DataSource, ":memory:", StringComparison.Ordinal);

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        ApplyPragmas(connection);
        return connection;
    }

    public void ApplyPragmas(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, $"PRAGMA busy_timeout={options.BusyTimeoutMs};");
        ExecuteNonQuery(connection, $"PRAGMA synchronous={MapSynchronous(options.Synchronous)};");
        ExecuteNonQuery(connection, $"PRAGMA wal_autocheckpoint={options.WalAutoCheckpointPages};");
        ExecuteNonQuery(connection, $"PRAGMA journal_size_limit={options.JournalSizeLimitBytes};");
        options.ConfigureConnection?.Invoke(connection);
    }

    private static string MapSynchronous(SqliteSynchronousMode mode) => mode switch
    {
        SqliteSynchronousMode.Off => "OFF",
        SqliteSynchronousMode.Normal => "NORMAL",
        SqliteSynchronousMode.Full => "FULL",
        _ => "NORMAL"
    };

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
```

---

## Task 0.7 — Create `SqliteRepository.cs` (skeleton)

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.cs`:

```csharp
using System.Collections.Concurrent;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IDisposable
{
    private readonly RepositoryOptions options;
    private readonly SqliteRepositoryOptions sqliteOptions;
    private readonly SqliteConnectionFactory connectionFactory;
    private readonly ConcurrentDictionary<string, byte> knownTables = new(StringComparer.Ordinal);

    protected bool initialized;
    private bool disposed;

    public SqliteRepository(RepositoryOptions options, SqliteRepositoryOptions sqliteOptions)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.sqliteOptions = sqliteOptions ?? throw new ArgumentNullException(nameof(sqliteOptions));
        connectionFactory = new SqliteConnectionFactory(sqliteOptions);
    }

    internal RepositoryOptions Options => options;

    internal SqliteRepositoryOptions SqliteOptions => sqliteOptions;

    internal SqliteConnectionFactory ConnectionFactory => connectionFactory;

    protected string GetCollectionNameForType<TItem>() where TItem : Entity => GetCollectionNameForType(typeof(TItem));

    protected string GetCollectionNameForType(Type type) => options.GetCollectionName(type);

    internal string CollectionNameForType(Type type) => GetCollectionNameForType(type);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "SELECT json_extract('{\"a\":1}', '$.a');";
            var result = await probe.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (result is null || Convert.ToInt64(result) != 1)
            {
                throw new InvalidOperationException("SQLite JSON1 extension is not available in this build.");
            }
        }

        if (sqliteOptions.EnableWal && !connectionFactory.IsMemory)
        {
            await using var wal = connection.CreateCommand();
            wal.CommandText = "PRAGMA journal_mode=WAL;";
            var mode = await wal.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;

            if (sqliteOptions.RequireWal && !string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SQLite journal_mode is '{mode}', expected 'wal'.");
            }
        }

        initialized = true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        SqliteConnection.ClearAllPools();
        GC.SuppressFinalize(this);
    }
}
```

---

## Task 0.8 — Create `ServicesExtensions.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\ServicesExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using ServiceScan.SourceGenerator;

namespace Saturn.Data.Sqlite;

public static partial class ServicesExtensions
{
    [GenerateServiceRegistrations(
        TypeNameFilter = "*Repository",
        AsImplementedInterfaces = true,
        AsSelf = true,
        Lifetime = ServiceLifetime.Singleton)]
    public static partial IServiceCollection AddSaturnSqliteRepositoryServices(this IServiceCollection services);
}
```

---

## Task 0.9 — Create `UnitTestableSqliteRepository.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\UnitTestableSqliteRepository.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite.Tests;

public sealed class UnitTestableSqliteRepository : SqliteRepository
{
    private readonly SqliteRepositoryOptions sqliteOptions;

    public UnitTestableSqliteRepository(RepositoryOptions repositoryOptions, SqliteRepositoryOptions sqliteOptions)
        : base(repositoryOptions, sqliteOptions)
    {
        this.sqliteOptions = sqliteOptions;
    }

    public string DatabasePath => sqliteOptions.DataSource;

    public void DropRecreateDatabase()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(DatabasePath))
        {
            File.Delete(DatabasePath);
        }

        if (File.Exists(DatabasePath + "-wal"))
        {
            File.Delete(DatabasePath + "-wal");
        }

        if (File.Exists(DatabasePath + "-shm"))
        {
            File.Delete(DatabasePath + "-shm");
        }

        initialized = false;
        InitializeAsync().GetAwaiter().GetResult();
    }

    public async Task<long> ProbeJson1Async(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json_extract('{\"a\":1}', '$.a');";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<string> ReadJournalModeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }
}
```

---

## Task 0.10 — Create `DatabaseFixture.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\DatabaseFixture.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;

namespace Saturn.Data.Sqlite.Tests;

public class DatabaseFixture : IDisposable, IRepositoryTestFixture<UnitTestableSqliteRepository>
{
    public UnitTestableSqliteRepository Repository { get; }

    public DatabaseFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-sqlite-{Guid.NewGuid():N}.db");

        Repository = new UnitTestableSqliteRepository(
            new RepositoryOptions
            {
                GetCollectionName = type => type.Name
            },
            new SqliteRepositoryOptions
            {
                DataSource = path
            });

        Repository.DropRecreateDatabase();
    }

    public void Dispose()
    {
        Repository.Dispose();
    }
}
```

---

## Task 0.11 — Create `SmokeTests.cs`

Create `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\SmokeTests.cs`:

```csharp
namespace Saturn.Data.Sqlite.Tests;

public class SmokeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Json1_Is_Available()
    {
        var result = await fixture.Repository.ProbeJson1Async();
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task File_Database_Uses_Wal()
    {
        var mode = await fixture.Repository.ReadJournalModeAsync();
        Assert.Equal("wal", mode);
    }
}
```

---

## Task 0.12 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj"
```

Expected: build succeeds; both smoke tests pass.

---

## Do NOT

- Do not implement any repository interface yet.
- Do not add a `SqliteEntityStore` yet.
- Do not modify `Saturn.Data.slnx` entries other than adding the `/Sqlite/` folder.
- Do not pin a hardcoded `Microsoft.Data.Sqlite` version; use the `dotnet add package` command.
- Do not add comments to any `.cs` file.
