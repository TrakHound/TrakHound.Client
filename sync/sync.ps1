<#
.SYNOPSIS
Copies the client files from a TrakHound Framework checkout into src/TrakHound.Client/Synced.

.DESCRIPTION
The files listed in sync-files.txt are copied from the Framework's libraries/TrakHound.Common (replacing what is in Synced, and removing
synced files that are no longer listed). The Framework commit and a hash of each file are written to SYNCED_FROM, which the tests use to
check that no synced file was edited here.

With -Check, nothing is copied: it only checks that the files in Synced still match SYNCED_FROM.

.EXAMPLE
./sync/sync.ps1 -Framework C:\Git\TrakHound\TrakHound.Framework

.EXAMPLE
./sync/sync.ps1 -Check
#>
param(
    # The root of the TrakHound Framework checkout (defaults to a TrakHound.Framework folder next to this repository)
    [string] $Framework = (Join-Path $PSScriptRoot '..\..\TrakHound.Framework'),

    # Only check that the synced files are unchanged
    [switch] $Check
)

$ErrorActionPreference = 'Stop'

$repository = Resolve-Path (Join-Path $PSScriptRoot '..')
$syncedRoot = Join-Path $repository 'src\TrakHound.Client\Synced'
$listPath = Join-Path $PSScriptRoot 'sync-files.txt'
$manifestPath = Join-Path $PSScriptRoot 'SYNCED_FROM'

# Hashed with LF line endings so the result doesn't depend on a checkout's core.autocrlf
function Get-NormalizedHash([string] $path) {
    $text = [System.IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Read-Manifest {
    $hashes = @{}
    foreach ($line in Get-Content $manifestPath) {
        if ($line -match '^([0-9a-f]{64})  (.+)$') { $hashes[$Matches[2]] = $Matches[1] }
    }
    return $hashes
}

if ($Check) {
    if (-not (Test-Path $manifestPath)) { throw "SYNCED_FROM not found. Run sync.ps1 first" }

    $hashes = Read-Manifest
    $problems = @()
    foreach ($file in $hashes.Keys) {
        $path = Join-Path $syncedRoot $file
        if (-not (Test-Path $path)) { $problems += "missing: $file" }
        elseif ((Get-NormalizedHash $path) -ne $hashes[$file]) { $problems += "edited: $file" }
    }
    $syncedFullRoot = (Resolve-Path $syncedRoot).Path.TrimEnd('\') + '\'
    Get-ChildItem $syncedRoot -Recurse -File -Filter *.cs | ForEach-Object {
        $file = $_.FullName.Substring($syncedFullRoot.Length).Replace('\', '/')
        if (-not $hashes.ContainsKey($file)) { $problems += "not synced: $file" }
    }

    if ($problems.Count -gt 0) {
        $problems | ForEach-Object { Write-Host $_ }
        throw "Synced files don't match SYNCED_FROM. Make the change in the Framework and sync it (see CONTRIBUTING.md)"
    }

    Write-Host "$($hashes.Count) synced files match SYNCED_FROM"
    return
}

$commonRoot = Join-Path $Framework 'libraries\TrakHound.Common'
if (-not (Test-Path $commonRoot)) { throw "TrakHound.Common not found in '$Framework'. Pass the Framework checkout with -Framework" }

$files = Get-Content $listPath | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { $_.Trim() }

# Copy into an empty folder, so files removed from the list don't stay behind
if (Test-Path $syncedRoot) { Remove-Item $syncedRoot -Recurse -Force }

$manifest = @()
foreach ($file in $files) {
    $source = Join-Path $commonRoot $file
    if (-not (Test-Path $source)) { throw "'$file' is listed in sync-files.txt but not in the Framework's TrakHound.Common" }

    $destination = Join-Path $syncedRoot $file
    New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
    Copy-Item $source $destination

    $manifest += "$(Get-NormalizedHash $destination)  $file"
}

# Record the Framework commit (and whether any synced file had uncommitted changes, which then can't be traced to a commit)
$commit = (git -C $Framework rev-parse HEAD).Trim()
$branch = (git -C $Framework rev-parse --abbrev-ref HEAD).Trim()
$dirty = git -C $Framework status --porcelain -- ($files | ForEach-Object { "libraries/TrakHound.Common/$_" })

$header = @(
    "# Written by sync.ps1. The files in src/TrakHound.Client/Synced and their hashes (SHA-256 with LF line endings)"
    "framework-commit: $commit"
    "framework-branch: $branch"
)
if ($dirty) {
    $header += "framework-uncommitted: true"
    Write-Warning "Some synced files have uncommitted changes in the Framework, so this sync isn't from a commit:"
    $dirty | ForEach-Object { Write-Warning "  $_" }
}

[System.IO.File]::WriteAllLines($manifestPath, [string[]]($header + $manifest))

Write-Host "Synced $($files.Count) files from $branch ($commit)"
