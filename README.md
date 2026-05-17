# Saturn.Data

Saturn.Data is an experimental .NET data access ecosystem built around shared repository abstractions and multiple backend implementations.

This repository is organized as a multi-project workspace with:
- core contracts (`GoLive.Saturn.Data.Abstractions`)
- entity primitives (`GoLive.Saturn.Data.Entities`)
- backend providers (`LiteDbX`, `MongoDb`, `Stellar`)
- serializer support for MongoDB entities
- shared repository contract tests
- source-generator tooling projects

## Repository layout

Top-level folders and their primary purpose:

- `Saturn.Data.Abstractions/` - repository interfaces, scope abstractions, sort helpers, options, exceptions.
- `Saturn.Data.Entities/` - base entity types, scoped entity models, IDs/refs, and JSON converter package.
- `Saturn.Data.LiteDbX/` - LiteDbX-backed repository implementation, tests, and playground.
- `Saturn.Data.MongoDb/` - MongoDB-backed repository implementation and tests.
- `Saturn.Data.MongoDb.EntitySerializers/` - BSON/entity serializer packages for MongoDB integration.
- `Saturn.Data.Stellar/` - Stellar FastDB-backed repository implementation and tests.
- `Saturn.Data.Testing.Shared/` - provider-agnostic repository contract test base classes and fixtures.
- `Saturn.Data.Template/` - template package for Saturn.Data usage patterns.
- `Saturn.Generator.Entities/` - source generator and resources projects.
- `scripts/` - change detection and package version bump scripts used by publishing workflow.

Main aggregate solution file:
- `Saturn.Data.slnx`

## Architecture at a glance

The repository centers on `IReadonlyRepository` and `IRepository` from `GoLive.Saturn.Data.Abstractions`.

Implemented patterns include:
- unscoped repository access
- scoped repository variants (`IScoped*`, `ISecondScoped*`, transparent/weak scoped variants)
- async CRUD/query operations (`All`, `Many`, `One`, `ById`, `Count`, `Insert`, `Update`, `Upsert`, `Save`)

Entity types in `GoLive.Saturn.Data.Entities` provide shared identity and scope models consumed by all provider implementations.

Provider libraries then map those contracts to specific backends:
- `Saturn.Data.LiteDbX`
- `Saturn.Data.MongoDb`
- `Saturn.Data.Stellar`

## Project and package map

### Core

| Project | PackageId | Purpose |
| --- | --- | --- |
| `Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions/GoLive.Saturn.Data.Abstractions.csproj` | `GoLive.Saturn.Data.Abstractions` | Shared repository contracts and support models. |
| `Saturn.Data.Entities/GoLive.Saturn.Data.Entities/GoLive.Saturn.Data.Entities.csproj` | `GoLive.Saturn.Data.Entities` | Shared entity primitives and identity/scope abstractions. |
| `Saturn.Data.Entities/Saturn.Data.Entities.JsonConverters/Saturn.Data.Entities.JsonConverters.csproj` | `GoLive.Saturn.Data.Entities.JsonConverters` | JSON converter extensions for entity types. |

### Provider packages

| Project | PackageId | Purpose |
| --- | --- | --- |
| `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX/Saturn.Data.LiteDbX.csproj` | `GoLive.Saturn.Data.LiteDb` | LiteDbX-backed repository implementation. |
| `Saturn.Data.MongoDb/Saturn.Data.MongoDb/Saturn.Data.MongoDb.csproj` | `GoLive.Saturn.Data.MongoDb` | MongoDB-backed repository implementation. |
| `Saturn.Data.Stellar/Saturn.Data.Stellar/Saturn.Data.Stellar.csproj` | `GoLive.Saturn.Data.Stellar` | Stellar FastDB-backed repository implementation. |

### MongoDB serializer packages

| Project | PackageId | Purpose |
| --- | --- | --- |
| `Saturn.Data.MongoDb.EntitySerializers/GoLive.Saturn.Data.MongoDb.EntitySerializers/GoLive.Saturn.Data.MongoDb.EntitySerializers.csproj` | `GoLive.Saturn.Data.MongoDb.EntitySerializers` | Core MongoDB serializers for Saturn entities. |
| `Saturn.Data.MongoDb.EntitySerializers/GoLive.Saturn.Data.MongoDb.EntitySerializers.Json/GoLive.Saturn.Data.MongoDb.EntitySerializers.Json.csproj` | `GoLive.Saturn.Data.MongoDb.EntitySerializers.Json` | `System.Text.Json`-oriented serializer package. |
| `Saturn.Data.MongoDb.EntitySerializers/GoLive.Saturn.Data.MongoDb.EntitySerializers.Newtonsoft/GoLive.Saturn.Data.MongoDb.EntitySerializers.Newtonsoft.csproj` | `GoLive.Saturn.Data.MongoDb.EntitySerializers.Newtonsoft` | `Newtonsoft.Json`-oriented serializer package. |

### Tooling and templates

| Project | PackageId | Purpose |
| --- | --- | --- |
| `Saturn.Data.Template/Saturn.Data.Template/Saturn.Data.Template.csproj` | `GoLive.Saturn.Data.Template` | Template package for Saturn.Data usage patterns. |
| `Saturn.Generator.Entities/Saturn.Generator.Entities/Saturn.Generator.Entities.csproj` | `GoLive.Saturn.Generator.Entities` | Roslyn generator for entity-related code enrichment. |
| `Saturn.Generator.Entities/Saturn.Generator.Entities.Resources/Saturn.Generator.Entities.Resources.csproj` | `GoLive.Saturn.Generator.Entities.Resources` | Generator resource package. |

### Tests and playgrounds (non-packable)

- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX.Tests/`
- `Saturn.Data.MongoDb/Saturn.Data.MongoDb.Tests/`
- `Saturn.Data.Stellar/Saturn.Data.Stellar.Tests/`
- `Saturn.Data.Testing.Shared/`
- `Saturn.Data.LiteDbX/Saturn.Data.LiteDbX.Playground/`
- `Saturn.Generator.Entities/Saturn.Generator.Entities.Playground/`

## Getting started

### Prerequisites

- .NET SDK capable of building all targeted frameworks in this repo (`net10.0` and `netstandard2.0`).
- PowerShell (scripts are authored as `.ps1`).
- MongoDB running locally for MongoDB tests (default fixture connection string points to `mongodb://localhost:27017/UnitTests`).

### Restore and build

```powershell
Set-Location "D:\Work\Saturn.Data"
dotnet restore .\Saturn.Data.slnx
dotnet build .\Saturn.Data.slnx -c Release
```

### Run tests

```powershell
Set-Location "D:\Work\Saturn.Data"
dotnet test .\Saturn.Data.LiteDbX\Saturn.Data.LiteDbX.Tests\Saturn.Data.LiteDbX.Tests.csproj -c Release
dotnet test .\Saturn.Data.MongoDb\Saturn.Data.MongoDb.Tests\Saturn.Data.MongoDb.Tests.csproj -c Release
dotnet test .\Saturn.Data.Stellar\Saturn.Data.Stellar.Tests\Saturn.Data.Stellar.Tests.csproj -c Release
```

Notes:
- MongoDB tests depend on a reachable local MongoDB instance.
- LiteDbX and Stellar tests use local filesystem paths in their test fixtures.

## Build and publish workflow (canonical)

Going forward, the only GitHub Actions workflow to use for package publishing is:
- `.github/workflows/publish-changed-nugets.yml`

Other workflow files in `.github/workflows/` are not part of the forward path for publishing.

### What `publish-changed-nugets.yml` does

The workflow is manually triggered (`workflow_dispatch`) and runs in two jobs:

1. **Detect changed projects**
   - Runs `scripts/detect-changed-projects.ps1`.
   - Computes diff from base/head SHAs (auto-resolved or overridden by input).
   - Produces two project lists:
	 - `BuildProjects`: changed projects plus dependent projects.
	 - `PublishProjects`: changed projects that are packable.

2. **Build, pack, and publish**
   - Optionally bumps versions for changed publishable projects using `scripts/auto-bump-versions.ps1`.
   - Builds impacted projects.
   - Packs changed publishable projects to `_packages/`.
   - Uploads `.nupkg` artifacts.
   - Publishes to NuGet.org (if enabled).
   - Optionally commits version changes back to the branch.

### Workflow inputs

| Input | Required | Default | Description |
| --- | --- | --- | --- |
| `publish` | no | `true` | If `true`, pushes generated packages to NuGet.org. |
| `version_bump` | yes | `patch` | Automatic semver bump for changed packages: `patch`, `minor`, `major`. |
| `commit_version_changes` | no | `true` | Commit updated `<Version>` entries back to the branch after publish. |
| `base_ref` | no | _empty_ | Optional explicit base ref/SHA for diff calculation. |
| `head_ref` | no | _empty_ | Optional explicit head ref/SHA for diff calculation. |

Required repository secret:
- `NUGET_KEY` - API key used by `dotnet nuget push`.

### How change detection works

`scripts/detect-changed-projects.ps1`:
- resolves base/head git refs from event context or manual overrides
- diffs changed files (`git diff --name-only`)
- maps each changed file to containing `.csproj` directory
- expands build impact to dependent projects via `ProjectReference` graph
- marks publish candidates from changed projects where `IsPackable` is not false

### How version bump works

`scripts/auto-bump-versions.ps1`:
- reads each changed publishable project
- resolves `PackageId` and current project version (`Version` or `VersionPrefix`)
- fetches latest published package version from NuGet flat container API
- picks the higher of current/published as baseline
- increments by requested bump level (`patch`, `minor`, `major`)
- writes resulting value back into `<Version>` in the project file

## Working with scripts locally

You can run the same workflow logic locally from PowerShell.

### Detect changed projects locally

```powershell
Set-Location "D:\Work\Saturn.Data"
.\scripts\detect-changed-projects.ps1 -RepoRoot (Get-Location).Path -BaseSha HEAD~1 -HeadSha HEAD -AsJson
```

### Dry-run auto bump for selected projects

```powershell
Set-Location "D:\Work\Saturn.Data"
.\scripts\auto-bump-versions.ps1 -RepoRoot (Get-Location).Path -ProjectPaths @(
  "Saturn.Data.MongoDb\Saturn.Data.MongoDb\Saturn.Data.MongoDb.csproj"
) -Bump patch -DryRun -AsJson
```

## Development notes

- Provider test projects consume shared contract tests from `Saturn.Data.Testing.Shared/` to enforce behavior consistency.
- Service registration helpers exist in provider projects (for example, `AddSaturnDataRepositoryServices` and `AddSaturnLiteDBRepositoryServices`).
- Repository interfaces and entity contracts are intended to remain backend-agnostic.

## License

This project is licensed under the MIT License. See `LICENSE`.
