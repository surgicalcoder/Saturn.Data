<#
.SYNOPSIS
    Recursively replaces all occurrences of a string with another string in file contents, filenames, and directory names.
.DESCRIPTION
    This script takes two parameters: the string to find and the string to replace it with.
.PARAMETER OldString
    The text to search for.
.PARAMETER NewString
    The text to replace it with.
.EXAMPLE
    .\ReplaceStrings.ps1 -OldString "foo" -NewString "bar"
    Replaces all instances of "foo" with "bar" in the current directory tree.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$OldString,

    [Parameter(Mandatory=$true)]
    [string]$NewString
)

# 1. Replace inside file contents
Get-ChildItem -Path . -Recurse -File | ForEach-Object {
    (Get-Content -LiteralPath $_.FullName) |
        ForEach-Object { $_ -replace [regex]::Escape($OldString), $NewString } |
        Set-Content -LiteralPath $_.FullName
}

# 2. Rename files containing the OldString
Get-ChildItem -Path . -Recurse -File |
    Where-Object { $_.Name -like "*${OldString}*" } |
    ForEach-Object {
        $newName = $_.Name -replace [regex]::Escape($OldString), $NewString
        Rename-Item -LiteralPath $_.FullName -NewName $newName
}

# 3. Rename directories (deepest first to avoid path conflicts)
Get-ChildItem -Path . -Recurse -Directory |
    Sort-Object { $_.FullName.Length } -Descending |
    Where-Object { $_.Name -like "*${OldString}*" } |
    ForEach-Object {
        $parentPath = Split-Path -Path $_.FullName -Parent
        $newName    = $_.Name -replace [regex]::Escape($OldString), $NewString
        Rename-Item -LiteralPath $_.FullName -NewName (Join-Path -Path $parentPath -ChildPath $newName)
}
