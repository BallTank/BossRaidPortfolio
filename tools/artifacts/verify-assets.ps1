[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$ManifestPath
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

$missingArtifactPaths = New-Object System.Collections.Generic.List[string]
$missingGitPaths = New-Object System.Collections.Generic.List[string]
$missingMetaPaths = New-Object System.Collections.Generic.List[string]

foreach ($entry in $manifest.paths) {
    $relativePath = ([string]$entry).Replace('\', '/').Trim('/')
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        continue
    }

    $absolutePath = Join-Path $ProjectRoot $relativePath
    if (-not (Test-Path -LiteralPath $absolutePath)) {
        $missingArtifactPaths.Add($relativePath) | Out-Null
        continue
    }

    $metaPath = "$absolutePath.meta"
    if (-not (Test-Path -LiteralPath $metaPath -PathType Leaf)) {
        $missingMetaPaths.Add("$relativePath.meta") | Out-Null
    }
}

foreach ($entry in $manifest.gitTrackedMustExist) {
    $relativePath = ([string]$entry).Replace('\', '/').Trim('/')
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        continue
    }

    if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $relativePath))) {
        $missingGitPaths.Add($relativePath) | Out-Null
    }
}

if ($missingArtifactPaths.Count -gt 0 -or $missingGitPaths.Count -gt 0) {
    if ($missingArtifactPaths.Count -gt 0) {
        Write-Error ("Missing artifact paths:`n{0}" -f ($missingArtifactPaths -join "`n"))
    }

    if ($missingGitPaths.Count -gt 0) {
        Write-Error ("Missing git-tracked required paths:`n{0}" -f ($missingGitPaths -join "`n"))
    }
}

Write-Host "Artifact paths present: $($manifest.paths.Count)"
Write-Host "Git required paths present: $($manifest.gitTrackedMustExist.Count)"

if ($missingMetaPaths.Count -gt 0) {
    Write-Warning ("Missing optional meta files:`n{0}" -f ($missingMetaPaths -join "`n"))
}
