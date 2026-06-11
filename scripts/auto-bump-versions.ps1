[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]] $ProjectPaths,

    [string] $RepoRoot = (Get-Location).Path,

    [ValidateSet('none', 'patch', 'minor', 'major')]
    [string] $Bump = 'patch',

    [ValidateSet('none', 'alpha', 'beta', 'rc')]
    [string] $VersionSuffix = 'none',

    [switch] $DryRun,
    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'

function Get-SemVerCore([string] $VersionText) {
    if ([string]::IsNullOrWhiteSpace($VersionText)) { return $null }

    $match = [System.Text.RegularExpressions.Regex]::Match($VersionText.Trim(), '^(\d+)\.(\d+)\.(\d+)')
    if (-not $match.Success) {
        throw "Version '$VersionText' does not start with major.minor.patch."
    }

    return [PSCustomObject]@{
        Major = [int] $match.Groups[1].Value
        Minor = [int] $match.Groups[2].Value
        Patch = [int] $match.Groups[3].Value
    }
}

function Compare-SemVerCore([object] $A, [object] $B) {
    if ($A.Major -ne $B.Major) { return [Math]::Sign($A.Major - $B.Major) }
    if ($A.Minor -ne $B.Minor) { return [Math]::Sign($A.Minor - $B.Minor) }
    if ($A.Patch -ne $B.Patch) { return [Math]::Sign($A.Patch - $B.Patch) }
    return 0
}

function Format-SemVerCore([object] $V) {
    return "$($V.Major).$($V.Minor).$($V.Patch)"
}

function Bump-SemVerCore([object] $Base, [string] $BumpLevel) {
    switch ($BumpLevel) {
        'none' {
            return [PSCustomObject]@{ Major = $Base.Major; Minor = $Base.Minor; Patch = $Base.Patch }
        }
        'major' {
            return [PSCustomObject]@{ Major = $Base.Major + 1; Minor = 0; Patch = 0 }
        }
        'minor' {
            return [PSCustomObject]@{ Major = $Base.Major; Minor = $Base.Minor + 1; Patch = 0 }
        }
        'patch' {
            return [PSCustomObject]@{ Major = $Base.Major; Minor = $Base.Minor; Patch = $Base.Patch + 1 }
        }
        default {
            throw "Unsupported bump level '$BumpLevel'."
        }
    }
}

function Get-AllPublishedVersions([string] $PackageId) {
    $lowerId = $PackageId.ToLowerInvariant()
    $url = "https://api.nuget.org/v3-flatcontainer/$lowerId/index.json"

    try {
        $response = Invoke-RestMethod -Uri $url -Method Get -RetryIntervalSec 2 -MaximumRetryCount 3
        if ($null -ne $response -and $null -ne $response.versions -and $response.versions.Count -gt 0) {
            return [string[]]$response.versions
        }
    } catch {
        if ($_.Exception.Message -match '404') { return $null }
        throw
    }

    return $null
}

function Get-NextSuffixCounter([string[]] $PublishedVersions, [string] $SuffixLabel) {
    if ($null -eq $PublishedVersions -or $PublishedVersions.Count -eq 0) { return 1 }
    $pattern = "-$SuffixLabel\.(\d+)$"
    $max = 0
    foreach ($v in $PublishedVersions) {
        $match = [System.Text.RegularExpressions.Regex]::Match($v, $pattern)
        if ($match.Success) {
            $c = [int]$match.Groups[1].Value
            if ($c -gt $max) { $max = $c }
        }
    }
    return $max + 1
}

$repoRootResolved = [System.IO.Path]::GetFullPath($RepoRoot)
$results = New-Object System.Collections.Generic.List[object]

foreach ($projectRelativePath in $ProjectPaths) {
    $projectPath = if ([System.IO.Path]::IsPathRooted($projectRelativePath)) {
        $projectRelativePath
    } else {
        Join-Path -Path $repoRootResolved -ChildPath $projectRelativePath
    }

    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw "Project not found: $projectRelativePath"
    }

    $xmlDoc = New-Object System.Xml.XmlDocument
    $xmlDoc.PreserveWhitespace = $true
    $xmlDoc.Load($projectPath)

    $packageIdNode = $xmlDoc.SelectSingleNode('//PackageId')
    $packageId = if ($null -ne $packageIdNode -and -not [string]::IsNullOrWhiteSpace($packageIdNode.InnerText)) {
        $packageIdNode.InnerText.Trim()
    } else {
        [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    }

    $versionElement = $xmlDoc.SelectSingleNode('//Version')
    $versionPrefixElement = $xmlDoc.SelectSingleNode('//VersionPrefix')

    $currentVersionText = $null
    if ($null -ne $versionElement -and -not [string]::IsNullOrWhiteSpace($versionElement.InnerText)) {
        $currentVersionText = $versionElement.InnerText.Trim()
    } elseif ($null -ne $versionPrefixElement -and -not [string]::IsNullOrWhiteSpace($versionPrefixElement.InnerText)) {
        $currentVersionText = $versionPrefixElement.InnerText.Trim()
    }

    $allPublishedVersions = Get-AllPublishedVersions -PackageId $packageId
    $publishedVersionText = if ($allPublishedVersions -and $allPublishedVersions.Count -gt 0) { $allPublishedVersions[-1] } else { $null }
    Write-Host "DEBUG: project=$projectRelativePath current='$currentVersionText' published='$publishedVersionText'"
    $publishedCore = Get-SemVerCore $publishedVersionText
    $currentCore = Get-SemVerCore $currentVersionText

    $baseCore = $null
    $isFirstVersion = $false
    if ($null -ne $currentCore -and $null -ne $publishedCore) {
        $baseCore = if ((Compare-SemVerCore -A $currentCore -B $publishedCore) -ge 0) { $currentCore } else { $publishedCore }
    } elseif ($null -ne $currentCore) {
        $baseCore = $currentCore
    } elseif ($null -ne $publishedCore) {
        $baseCore = $publishedCore
    } else {
        $baseCore = [PSCustomObject]@{ Major = 7; Minor = 0; Patch = 0 }
        $isFirstVersion = $true
    }

    $nextCore = if ($isFirstVersion) {
        $baseCore
    } else {
        Bump-SemVerCore -Base $baseCore -BumpLevel $Bump
    }
    $nextVersion = Format-SemVerCore $nextCore

    if (-not [string]::IsNullOrWhiteSpace($VersionSuffix) -and $VersionSuffix -ne 'none') {
        $counter = Get-NextSuffixCounter -PublishedVersions $allPublishedVersions -SuffixLabel $VersionSuffix
        $nextVersion = "$nextVersion-$VersionSuffix.$counter"
    }

    if ($null -eq $versionElement) {
        $propertyGroup = $xmlDoc.SelectSingleNode('/Project/PropertyGroup')
        if ($null -eq $propertyGroup) {
            $propertyGroup = $xmlDoc.CreateElement('PropertyGroup')
            [void]$xmlDoc.DocumentElement.AppendChild($propertyGroup)
        }

        $versionElement = $xmlDoc.CreateElement('Version')
        [void]$propertyGroup.AppendChild($versionElement)
    }

    $versionElement.InnerText = $nextVersion

    if (-not $DryRun) {
        $xmlDoc.Save($projectPath)
    }

    Write-Host "BUMP: $packageId current=$currentVersionText published=$publishedVersionText next=$nextVersion"

    $results.Add([PSCustomObject]@{
        ProjectPath = $projectRelativePath
        PackageId = $packageId
        CurrentVersion = $currentVersionText
        PublishedVersion = $publishedVersionText
        NextVersion = $nextVersion
    }) | Out-Null
}

if ($AsJson) {
    $results | ConvertTo-Json -Depth 8 -Compress
} else {
    $results
}

