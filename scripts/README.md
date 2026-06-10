# Scripts

This folder contains helper scripts used by ` .github/workflows/publish-changed-nugets.yml `.

## What each script does

- `detect-changed-projects.ps1`
  - Finds changed files between a base and head commit.
  - Maps changed files to `.csproj` folders.
  - Expands to dependent projects for build correctness.
  - Outputs:
    - projects to build
    - projects to publish (changed + packable)

- `auto-bump-versions.ps1`
  - For changed publishable projects, reads package/version metadata.
  - Checks latest version on NuGet.org for each package.
  - Applies an automatic bump (`patch`, `minor`, or `major`).
  - Writes `<Version>` back into the `.csproj`.

## Workflow summary

The manual workflow (`Publish Changed NuGets`) runs in this order:

1. Detect changed projects.
2. Auto-bump versions for changed packages.
3. Build impacted projects.
4. Pack changed packages only.
5. Publish to NuGet.org (if `publish=true`).

## Required secret

Configure this repository secret:

- `NUGET_KEY` - NuGet.org API key.

## Workflow inputs

- `version_bump` (required): `patch`, `minor`, `major`
- `publish` (default `true`): publish generated packages
- `base_ref` (optional): override base commit/ref for change detection
- `head_ref` (optional): override head commit/ref for change detection

## Recommended defaults

For routine releases:

- `version_bump=patch`
- `publish=true`

Use `minor` or `major` only when intentionally making those release-level changes.

## Notes

- Version tracking is done by reading/writing `<Version>` in `.csproj` during build. NuGet registry is persistent record — csproj changes not committed.
- `dotnet nuget push` uses `--skip-duplicate` to avoid hard failure if a package/version already exists.
- If no changed packable projects are found, the workflow exits without publishing.

