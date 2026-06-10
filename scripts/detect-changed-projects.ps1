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
    [switch] $ForceBuildAll,
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
$beforeShaResolved = if (-not [string]::IsNullOrWhiteSpace($BeforeSha)) { Resolve-GitRef $BeforeSha } else { $env:GITHUB_EVENT_BEFORE }
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

function Select-XmlNode([System.Xml.XmlDocument] $Doc, [string] $XPath, [System.Xml.XmlNamespaceManager] $NsMgr) {
    if ($null -ne $NsMgr) { return $Doc.SelectSingleNode($XPath, $NsMgr) }
    return $Doc.SelectSingleNode($XPath)
}

function Select-XmlNodes([System.Xml.XmlDocument] $Doc, [string] $XPath, [System.Xml.XmlNamespaceManager] $NsMgr) {
    if ($null -ne $NsMgr) { return $Doc.SelectNodes($XPath, $NsMgr) }
    return $Doc.SelectNodes($XPath)
}

foreach ($projectPath in $projectFiles) {
    $fullPath = [System.IO.Path]::GetFullPath($projectPath)
    $relativePath = ($fullPath.Substring($repoRootResolved.Length).TrimStart('\', '/') -replace '\\', '/')
    $projectDir = [System.IO.Path]::GetDirectoryName($fullPath)
    $dirPrefix = Normalize-RepoPath($projectDir.Substring($repoRootResolved.Length).TrimStart('\', '/')) + '/'

    [System.Xml.XmlDocument] $xml = New-Object System.Xml.XmlDocument
    try {
        $xml.Load($projectPath)
    } catch {
        Write-Warning "Skipping malformed csproj: $relativePath ($($_.Exception.Message))"
        continue
    }

    # Handle optional default namespace (SDK-style projects omit it; defend against edge cases)
    $nsMgr = $null
    $defaultNs = $xml.DocumentElement.GetAttribute('xmlns')
    if (-not [string]::IsNullOrWhiteSpace($defaultNs)) {
        $nsMgr = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
        $nsMgr.AddNamespace('msb', $defaultNs)
    }

    $xpfx = if ($null -ne $nsMgr) { 'msb:' } else { '' }

    $isPackable = $false
    $genNode = Select-XmlNode -Doc $xml -XPath "//${xpfx}GeneratePackageOnBuild" -NsMgr $nsMgr
    if ($null -ne $genNode -and $genNode.InnerText.Trim() -match '^(?i:true|1)$') {
        $isPackable = $true
    }
    $packableNode = Select-XmlNode -Doc $xml -XPath "//${xpfx}IsPackable" -NsMgr $nsMgr
    if ($null -ne $packableNode -and $packableNode.InnerText.Trim() -match '^(?i:true|1)$') {
        $isPackable = $true
    }

    $refNodes = Select-XmlNodes -Doc $xml -XPath "//${xpfx}ProjectReference" -NsMgr $nsMgr
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
if ($ForceBuildAll) {
    foreach ($project in $projects.Values) {
        [void] $changedProjectSet.Add($project.FullPath)
    }
} else {
    foreach ($file in $changedFiles) {
        foreach ($project in $projects.Values) {
            if ($file.StartsWith($project.DirPrefix)) {
                [void] $changedProjectSet.Add($project.FullPath)
            }
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

