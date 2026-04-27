[CmdletBinding()]
param(
    [string] $RepoRoot = (Get-Location).Path,
    [string] $EventName,
    [string] $HeadSha,
    [string] $BeforeSha,
    [string] $PrBaseRef,
    [string] $PrBaseSha,
    [string] $BaseSha,
    [switch] $IncludeDependents = $true,
    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'

function Normalize-RepoPath([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return '' }
    return ($Path -replace '\\', '/').TrimStart('./').ToLowerInvariant()
}

function Resolve-GitRef([string] $Ref) {
    if ([string]::IsNullOrWhiteSpace($Ref)) { return $null }
    return (git rev-parse $Ref).Trim()
}

$repoRootResolved = [System.IO.Path]::GetFullPath($RepoRoot)
$eventNameResolved = if (-not [string]::IsNullOrWhiteSpace($EventName)) { $EventName } else { $env:GITHUB_EVENT_NAME }
$headShaResolved = if (-not [string]::IsNullOrWhiteSpace($HeadSha)) { Resolve-GitRef $HeadSha } else { Resolve-GitRef $env:GITHUB_SHA }
$beforeShaResolved = if (-not [string]::IsNullOrWhiteSpace($BeforeSha)) { $BeforeSha } else { $env:GITHUB_EVENT_BEFORE }
$prBaseRefResolved = if (-not [string]::IsNullOrWhiteSpace($PrBaseRef)) { $PrBaseRef } else { $env:GITHUB_BASE_REF }
$prBaseShaResolved = if (-not [string]::IsNullOrWhiteSpace($PrBaseSha)) { $PrBaseSha } else { $null }

if ([string]::IsNullOrWhiteSpace($headShaResolved)) {
    $headShaResolved = (git rev-parse HEAD).Trim()
}

$baseShaResolved = $null
if (-not [string]::IsNullOrWhiteSpace($BaseSha)) {
    $baseShaResolved = Resolve-GitRef $BaseSha
} elseif ($eventNameResolved -eq 'pull_request') {
    if (-not [string]::IsNullOrWhiteSpace($prBaseRefResolved)) {
        git fetch --no-tags --prune origin "$prBaseRefResolved" | Out-Null
        $mergeBase = (git merge-base "origin/$prBaseRefResolved" "$headShaResolved").Trim()
        if (-not [string]::IsNullOrWhiteSpace($mergeBase)) {
            $baseShaResolved = $mergeBase
        }
    }

    if ([string]::IsNullOrWhiteSpace($baseShaResolved) -and -not [string]::IsNullOrWhiteSpace($prBaseShaResolved)) {
        $baseShaResolved = Resolve-GitRef $prBaseShaResolved
    }
} elseif ($eventNameResolved -eq 'push') {
    if ([string]::IsNullOrWhiteSpace($beforeShaResolved) -or $beforeShaResolved -match '^0+$') {
        $baseShaResolved = (git rev-parse "$headShaResolved^").Trim()
    } else {
        $baseShaResolved = Resolve-GitRef $beforeShaResolved
    }
} else {
    $baseShaResolved = (git rev-parse "$headShaResolved^").Trim()
}

$rawChangedFiles = git diff --name-only "$baseShaResolved" "$headShaResolved"
$changedFiles = @(
    $rawChangedFiles |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { Normalize-RepoPath $_ } |
        Sort-Object -Unique
)

$projectFiles = Get-ChildItem -Path $repoRootResolved -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName
$projects = @{}
$dependents = @{}

foreach ($projectPath in $projectFiles) {
    $fullPath = [System.IO.Path]::GetFullPath($projectPath)
    $relativePath = Normalize-RepoPath($fullPath.Substring($repoRootResolved.Length).TrimStart('\', '/'))
    $projectDir = [System.IO.Path]::GetDirectoryName($fullPath)
    $dirPrefix = Normalize-RepoPath($projectDir.Substring($repoRootResolved.Length).TrimStart('\', '/')) + '/'

    [xml] $xml = Get-Content -LiteralPath $fullPath

    $isPackable = $true
    $packableNode = $xml.SelectSingleNode('//IsPackable')
    if ($null -ne $packableNode -and -not [string]::IsNullOrWhiteSpace($packableNode.InnerText)) {
        if ($packableNode.InnerText.Trim() -match '^(?i:false|0)$') {
            $isPackable = $false
        }
    }

    $refNodes = $xml.SelectNodes('//ProjectReference')
    foreach ($node in $refNodes) {
        $include = $node.GetAttribute('Include')
        if ([string]::IsNullOrWhiteSpace($include)) { continue }

        $refPath = [System.IO.Path]::GetFullPath((Join-Path -Path $projectDir -ChildPath $include))
        if (-not $dependents.ContainsKey($refPath)) {
            $dependents[$refPath] = New-Object System.Collections.Generic.List[string]
        }

        $dependents[$refPath].Add($fullPath)
    }

    $projects[$fullPath] = [PSCustomObject]@{
        FullPath = $fullPath
        RelativePath = $relativePath
        DirPrefix = $dirPrefix
        IsPackable = $isPackable
    }
}

$changedProjectSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($file in $changedFiles) {
    foreach ($project in $projects.Values) {
        if ($file.StartsWith($project.DirPrefix)) {
            [void] $changedProjectSet.Add($project.FullPath)
        }
    }
}

$buildSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$queue = [System.Collections.Generic.Queue[string]]::new()

foreach ($project in $changedProjectSet) {
    [void] $buildSet.Add($project)
    if ($IncludeDependents) {
        $queue.Enqueue($project)
    }
}

if ($IncludeDependents) {
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        if (-not $dependents.ContainsKey($current)) { continue }

        foreach ($dependent in $dependents[$current]) {
            if ($buildSet.Add($dependent)) {
                $queue.Enqueue($dependent)
            }
        }
    }
}

$publishSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($project in $changedProjectSet) {
    if ($projects[$project].IsPackable) {
        [void] $publishSet.Add($project)
    }
}

$buildRelative = @($buildSet | ForEach-Object { $projects[$_].RelativePath } | Sort-Object)
$publishRelative = @($publishSet | ForEach-Object { $projects[$_].RelativePath } | Sort-Object)
$changedRelative = @($changedProjectSet | ForEach-Object { $projects[$_].RelativePath } | Sort-Object)

$result = [PSCustomObject]@{
    BaseSha = $baseShaResolved
    HeadSha = $headShaResolved
    ChangedFiles = $changedFiles
    ChangedFileCount = $changedFiles.Count
    ChangedProjects = $changedRelative
    ChangedProjectCount = $changedRelative.Count
    BuildProjects = $buildRelative
    BuildProjectCount = $buildRelative.Count
    PublishProjects = $publishRelative
    PublishProjectCount = $publishRelative.Count
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 8 -Compress
} else {
    $result
}

