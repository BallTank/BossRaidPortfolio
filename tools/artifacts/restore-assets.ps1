[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$ManifestPath,
    [string]$ArchivePath,
    [string]$ExpectedSha256
)

$ErrorActionPreference = 'Stop'

$scriptRoot = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Join-Path $scriptRoot '..\..'
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $scriptRoot 'artifact-manifest.json'
}

$ProjectRoot = (Resolve-Path $ProjectRoot).Path
$ManifestPath = (Resolve-Path $ManifestPath).Path
$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
$archivePrefix = if ($manifest.archivePrefix) { [string]$manifest.archivePrefix } else { 'BossRaidPortfolio_RequiredArt' }

if ([string]::IsNullOrWhiteSpace($ArchivePath)) {
    $defaultDirectory = Join-Path (Split-Path -Parent $ProjectRoot) 'BossRaidPortfolio_ArtifactBundles'
    $candidate = Get-ChildItem -LiteralPath $defaultDirectory -Filter "$archivePrefix-*.zip" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $candidate) {
        Write-Error "ArchivePath was not supplied, and no $archivePrefix archive was found in $defaultDirectory."
    }

    $ArchivePath = $candidate.FullName
}

$ArchivePath = (Resolve-Path $ArchivePath).Path

if ([string]::IsNullOrWhiteSpace($ExpectedSha256)) {
    $shaPath = "$ArchivePath.sha256"
    if (Test-Path -LiteralPath $shaPath -PathType Leaf) {
        $ExpectedSha256 = ((Get-Content -LiteralPath $shaPath -TotalCount 1) -split '\s+')[0]
    }
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256)) {
    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $ArchivePath).Hash
    if ($actualHash -ne $ExpectedSha256) {
        Write-Error "SHA256 mismatch. Expected $ExpectedSha256 but got $actualHash."
    }
}

$tempBase = [System.IO.Path]::GetTempPath()
$tempRoot = Join-Path $tempBase ("BossRaidPortfolio-restore-{0}" -f [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

Expand-Archive -LiteralPath $ArchivePath -DestinationPath $tempRoot -Force

Get-ChildItem -LiteralPath $tempRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $ProjectRoot -Recurse -Force
}

if (-not $tempRoot.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Error "Refusing to remove unexpected temp path: $tempRoot"
}

Remove-Item -LiteralPath $tempRoot -Recurse -Force

& (Join-Path $scriptRoot 'verify-assets.ps1') -ProjectRoot $ProjectRoot -ManifestPath $ManifestPath

Write-Host "Restored artifact archive: $ArchivePath"
