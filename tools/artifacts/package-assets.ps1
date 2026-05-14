[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$ManifestPath,
    [string]$OutputDirectory,
    [switch]$AllowMissing
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

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Split-Path -Parent $ProjectRoot) 'BossRaidPortfolio_ArtifactBundles'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
$archivePrefix = if ($manifest.archivePrefix) { [string]$manifest.archivePrefix } else { 'BossRaidPortfolio_RequiredArt' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$tempBase = [System.IO.Path]::GetTempPath()
$tempRoot = Join-Path $tempBase ("{0}-{1}-{2}" -f $archivePrefix, $stamp, [guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

function Copy-RelativeItem {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $target = Join-Path $tempRoot $RelativePath
    $targetParent = Split-Path -Parent $target
    if ($targetParent) {
        New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
    }

    if (Test-Path -LiteralPath $Source -PathType Container) {
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $target -Recurse -Force
        }
    }
    else {
        Copy-Item -LiteralPath $Source -Destination $target -Force
    }
}

$missing = New-Object System.Collections.Generic.List[string]

foreach ($entry in $manifest.paths) {
    $relativePath = ([string]$entry).Replace('\', '/').Trim('/')
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        continue
    }

    $source = Join-Path $ProjectRoot $relativePath
    if (Test-Path -LiteralPath $source) {
        Copy-RelativeItem -Source $source -RelativePath $relativePath

        $metaSource = "$source.meta"
        if (Test-Path -LiteralPath $metaSource -PathType Leaf) {
            Copy-RelativeItem -Source $metaSource -RelativePath "$relativePath.meta"
        }
    }
    else {
        $missing.Add($relativePath) | Out-Null
    }
}

if ($missing.Count -gt 0 -and -not $AllowMissing) {
    Write-Error ("Missing artifact paths:`n{0}" -f ($missing -join "`n"))
}

$archivePath = Join-Path $OutputDirectory ("{0}-{1}.zip" -f $archivePrefix, $stamp)
$topItems = Get-ChildItem -LiteralPath $tempRoot -Force
if (-not $topItems) {
    Write-Error 'No files were copied into the artifact bundle.'
}

Compress-Archive -Path $topItems.FullName -DestinationPath $archivePath -CompressionLevel Optimal -Force

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath
$shaPath = "$archivePath.sha256"
("{0}  {1}" -f $hash.Hash, (Split-Path -Leaf $archivePath)) | Set-Content -LiteralPath $shaPath -Encoding ASCII

$fileCount = (Get-ChildItem -LiteralPath $tempRoot -File -Recurse | Measure-Object).Count

if (-not $tempRoot.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Error "Refusing to remove unexpected temp path: $tempRoot"
}

Remove-Item -LiteralPath $tempRoot -Recurse -Force

Write-Host "Artifact archive: $archivePath"
Write-Host "SHA256: $($hash.Hash)"
Write-Host "SHA file: $shaPath"
Write-Host "Files packed: $fileCount"
if ($missing.Count -gt 0) {
    Write-Host "Missing paths ignored: $($missing.Count)"
}
