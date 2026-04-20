[CmdletBinding()]
param(
    [string]$ProjectRoot = ".",
    [string]$ConfigPath = "tools/asset_export/export_roots.json",
    [string]$OutputRoot = "Builds/AssetExports",
    [string]$UnityEditorPath,
    [string]$BranchName,
    [string[]]$UnityIncludePath = @(),
    [string[]]$RawIncludePath = @(),
    [string[]]$SpecialResourcesIncludePath = @(),
    [switch]$SkipUnityPackage,
    [switch]$SkipRawArchive,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-RelativePath {
    param([string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return ""
    }

    $normalized = $PathValue.Trim().Replace("\", "/")
    while ($normalized.StartsWith("./", [System.StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }

    return $normalized.TrimStart("/")
}

function Get-FullPath {
    param(
        [string]$PathValue,
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $PathValue))
}

function Convert-ToRepoRelativePath {
    param(
        [string]$FullPath,
        [string]$RepoRoot
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd("\", "/")
    $resolvedFull = [System.IO.Path]::GetFullPath($FullPath)

    if (-not $resolvedFull.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the repository root: $resolvedFull"
    }

    $relativePath = $resolvedFull.Substring($resolvedRoot.Length).TrimStart("\", "/")
    return $relativePath.Replace("\", "/")
}

function Convert-GlobToRegex {
    param([string]$Glob)

    $normalizedGlob = Normalize-RelativePath $Glob
    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append("^")

    for ($index = 0; $index -lt $normalizedGlob.Length; $index++) {
        $character = $normalizedGlob[$index]
        $nextIsDoubleStar = $character -eq "*" -and ($index + 1) -lt $normalizedGlob.Length -and $normalizedGlob[$index + 1] -eq "*"

        if ($nextIsDoubleStar) {
            [void]$builder.Append(".*")
            $index++
            continue
        }

        switch ($character) {
            "*" { [void]$builder.Append("[^/]*") }
            "?" { [void]$builder.Append(".") }
            "." { [void]$builder.Append("\.") }
            "(" { [void]$builder.Append("\(") }
            ")" { [void]$builder.Append("\)") }
            "[" { [void]$builder.Append("\[") }
            "]" { [void]$builder.Append("\]") }
            "{" { [void]$builder.Append("\{") }
            "}" { [void]$builder.Append("\}") }
            "+" { [void]$builder.Append("\+") }
            "^" { [void]$builder.Append("\^") }
            "$" { [void]$builder.Append("\$") }
            "|" { [void]$builder.Append("\|") }
            "\" { [void]$builder.Append("/") }
            default { [void]$builder.Append($character) }
        }
    }

    [void]$builder.Append("$")
    return $builder.ToString()
}

function Test-PathMatchesAnyPattern {
    param(
        [string]$RelativePath,
        [string[]]$RegexPatterns
    )

    $normalizedRelativePath = Normalize-RelativePath $RelativePath
    foreach ($regexPattern in $RegexPatterns) {
        if ($normalizedRelativePath -match $regexPattern) {
            return $true
        }
    }

    return $false
}

function ConvertTo-ConfigEntries {
    param(
        [object[]]$Entries,
        [string]$RootType
    )

    $convertedEntries = New-Object System.Collections.Generic.List[object]

    foreach ($entry in $Entries) {
        if ($entry -is [string]) {
            $convertedEntries.Add([pscustomobject]@{
                    RootType        = $RootType
                    Path            = Normalize-RelativePath $entry
                    Category        = "unspecified"
                    ExportByDefault = $true
                    Notes           = ""
                })
            continue
        }

        $convertedEntries.Add([pscustomobject]@{
                RootType        = $RootType
                Path            = Normalize-RelativePath ([string]$entry.path)
                Category        = [string]$entry.category
                ExportByDefault = [bool]$entry.exportByDefault
                Notes           = [string]$entry.notes
            })
    }

    return $convertedEntries
}

function Find-MatchingConfigEntry {
    param(
        [string]$RelativePath,
        [System.Collections.Generic.List[object]]$ConfigEntries
    )

    $normalizedRelativePath = Normalize-RelativePath $RelativePath
    $bestMatch = $null

    foreach ($entry in $ConfigEntries) {
        $configuredPath = Normalize-RelativePath ([string]$entry.Path)
        $isExactMatch = $normalizedRelativePath.Equals($configuredPath, [System.StringComparison]::OrdinalIgnoreCase)
        $isChildMatch = $normalizedRelativePath.StartsWith($configuredPath + "/", [System.StringComparison]::OrdinalIgnoreCase)

        if (-not $isExactMatch -and -not $isChildMatch) {
            continue
        }

        if ($null -eq $bestMatch -or $configuredPath.Length -gt $bestMatch.Path.Length) {
            $bestMatch = $entry
        }
    }

    return $bestMatch
}

function Resolve-SelectedRoots {
    param(
        [System.Collections.Generic.List[object]]$ConfigEntries,
        [string[]]$ExplicitPaths
    )

    $selectedRoots = New-Object System.Collections.Generic.List[object]
    $selectedLookup = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($entry in $ConfigEntries) {
        if (-not $entry.ExportByDefault) {
            continue
        }

        if ($selectedLookup.Add($entry.Path)) {
            $selectedRoots.Add([pscustomobject]@{
                    RootType      = $entry.RootType
                    SelectedPath  = $entry.Path
                    ConfigPath    = $entry.Path
                    Category      = $entry.Category
                    Notes         = $entry.Notes
                    IsExplicitAdd = $false
                })
        }
    }

    foreach ($explicitPath in $ExplicitPaths) {
        if ([string]::IsNullOrWhiteSpace($explicitPath)) {
            continue
        }

        $normalizedExplicitPath = Normalize-RelativePath $explicitPath
        $matchingEntry = Find-MatchingConfigEntry -RelativePath $normalizedExplicitPath -ConfigEntries $ConfigEntries
        if ($null -eq $matchingEntry) {
            throw "Path '$normalizedExplicitPath' is not allowed by the export config."
        }

        if ($selectedLookup.Add($normalizedExplicitPath)) {
            $selectedRoots.Add([pscustomobject]@{
                    RootType      = $matchingEntry.RootType
                    SelectedPath  = $normalizedExplicitPath
                    ConfigPath    = $matchingEntry.Path
                    Category      = $matchingEntry.Category
                    Notes         = $matchingEntry.Notes
                    IsExplicitAdd = $true
                })
        }
    }

    return $selectedRoots
}

function Collect-SelectedFiles {
    param(
        [object[]]$SelectedRoots,
        [string]$RepoRoot,
        [string[]]$ExcludePatterns,
        [bool]$SkipMetaFiles
    )

    $selectedFiles = New-Object System.Collections.Generic.List[string]
    $selectedLookup = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
    $missingRoots = New-Object System.Collections.Generic.List[string]

    foreach ($selectedRoot in $SelectedRoots) {
        $selectedPath = Normalize-RelativePath ([string]$selectedRoot.SelectedPath)
        $absoluteSelectedPath = Get-FullPath -PathValue $selectedPath -BasePath $RepoRoot

        if (-not (Test-Path -LiteralPath $absoluteSelectedPath)) {
            $missingRoots.Add($selectedPath)
            continue
        }

        $resolvedItem = Get-Item -LiteralPath $absoluteSelectedPath -Force
        $files = @()
        if ($resolvedItem.PSIsContainer) {
            $files = Get-ChildItem -LiteralPath $absoluteSelectedPath -Recurse -File -Force
        }
        else {
            $files = @($resolvedItem)
        }

        foreach ($file in $files) {
            $relativeFilePath = Convert-ToRepoRelativePath -FullPath $file.FullName -RepoRoot $RepoRoot

            if (Test-PathMatchesAnyPattern -RelativePath $relativeFilePath -RegexPatterns $ExcludePatterns) {
                continue
            }

            if ($SkipMetaFiles -and $relativeFilePath.EndsWith(".meta", [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            if ($SkipMetaFiles -and -not $relativeFilePath.StartsWith("Assets/", [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Unity export selection must stay under Assets/: $relativeFilePath"
            }

            if ($selectedLookup.Add($relativeFilePath)) {
                $selectedFiles.Add($relativeFilePath)
            }
        }
    }

    return [pscustomobject]@{
        Files        = @($selectedFiles)
        MissingRoots = @($missingRoots)
    }
}

function Get-MissingMetaFiles {
    param(
        [string[]]$UnityAssetFiles,
        [string]$RepoRoot
    )

    $missingMetaFiles = New-Object System.Collections.Generic.List[string]

    foreach ($assetPath in $UnityAssetFiles) {
        if (-not $assetPath.StartsWith("Assets/", [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $metaPath = Get-FullPath -PathValue ($assetPath + ".meta") -BasePath $RepoRoot
        if (-not (Test-Path -LiteralPath $metaPath)) {
            $missingMetaFiles.Add($assetPath)
        }
    }

    return @($missingMetaFiles)
}

function Resolve-BranchName {
    param(
        [string]$RepoRoot,
        [string]$ProvidedBranchName
    )

    if (-not [string]::IsNullOrWhiteSpace($ProvidedBranchName)) {
        return $ProvidedBranchName.Trim()
    }

    $branchName = (& git -C $RepoRoot rev-parse --abbrev-ref HEAD).Trim()
    if ([string]::IsNullOrWhiteSpace($branchName)) {
        throw "Could not resolve the current branch name."
    }

    return $branchName
}

function Convert-ToBranchSlug {
    param([string]$BranchNameValue)

    $slug = $BranchNameValue -replace "[\\/]+", "__"
    $slug = $slug -replace "\s+", "-"
    $slug = $slug -replace "[^A-Za-z0-9._-]", "-"
    return $slug
}

function Resolve-DriveBranchFolderName {
    param(
        [object]$GoogleDriveConfig,
        [string]$ResolvedBranchName
    )

    $overrides = $GoogleDriveConfig.branchFolderNameOverrides
    if ($null -ne $overrides) {
        foreach ($property in $overrides.PSObject.Properties) {
            if ($property.Name -eq $ResolvedBranchName) {
                return [string]$property.Value
            }
        }
    }

    return $ResolvedBranchName
}

function Get-UnityEditorVersion {
    param([string]$RepoRoot)

    $projectVersionFilePath = Join-Path $RepoRoot "ProjectSettings/ProjectVersion.txt"
    $projectVersionLine = Get-Content -LiteralPath $projectVersionFilePath | Where-Object { $_ -like "m_EditorVersion:*" } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($projectVersionLine)) {
        throw "Could not resolve Unity editor version from ProjectSettings/ProjectVersion.txt."
    }

    return ($projectVersionLine.Split(":")[1]).Trim()
}

function Resolve-UnityEditorExecutable {
    param(
        [string]$RepoRoot,
        [string]$ProvidedUnityEditorPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ProvidedUnityEditorPath)) {
        $resolvedProvidedPath = Get-FullPath -PathValue $ProvidedUnityEditorPath -BasePath $RepoRoot
        if (-not (Test-Path -LiteralPath $resolvedProvidedPath)) {
            throw "Unity editor executable was not found: $resolvedProvidedPath"
        }

        return $resolvedProvidedPath
    }

    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR_PATH) -and (Test-Path -LiteralPath $env:UNITY_EDITOR_PATH)) {
        return $env:UNITY_EDITOR_PATH
    }

    $unityVersion = Get-UnityEditorVersion -RepoRoot $RepoRoot
    $candidatePaths = @(
        "C:\Program Files\Unity\Hub\Editor\$unityVersion\Editor\Unity.exe",
        "C:\Program Files\Unity\Editor\$unityVersion\Editor\Unity.exe"
    )

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path -LiteralPath $candidatePath) {
            return $candidatePath
        }
    }

    throw "Could not locate Unity.exe automatically. Pass -UnityEditorPath or set UNITY_EDITOR_PATH."
}

function Ensure-UnityBuildEnvironment {
    if ([string]::IsNullOrWhiteSpace($env:HOME) -and -not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $env:HOME = $env:USERPROFILE
    }

    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA) -and -not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $candidateLocalAppData = Join-Path $env:USERPROFILE "AppData\Local"
        if (Test-Path -LiteralPath $candidateLocalAppData) {
            $env:LOCALAPPDATA = $candidateLocalAppData
        }
    }

    if ([string]::IsNullOrWhiteSpace($env:APPDATA) -and -not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $candidateRoamingAppData = Join-Path $env:USERPROFILE "AppData\Roaming"
        if (Test-Path -LiteralPath $candidateRoamingAppData) {
            $env:APPDATA = $candidateRoamingAppData
        }
    }

    if ([string]::IsNullOrWhiteSpace($env:PROGRAMDATA)) {
        if (-not [string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE) -and (Test-Path -LiteralPath $env:ALLUSERSPROFILE)) {
            $env:PROGRAMDATA = $env:ALLUSERSPROFILE
        }
        elseif (-not [string]::IsNullOrWhiteSpace($env:SystemDrive)) {
            $candidateProgramData = Join-Path ($env:SystemDrive + "\") "ProgramData"
            if (Test-Path -LiteralPath $candidateProgramData) {
                $env:PROGRAMDATA = $candidateProgramData
            }
        }
        elseif (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
            $userProfileDrive = [System.IO.Path]::GetPathRoot($env:USERPROFILE)
            if (-not [string]::IsNullOrWhiteSpace($userProfileDrive)) {
                $candidateProgramData = Join-Path $userProfileDrive "ProgramData"
                if (Test-Path -LiteralPath $candidateProgramData) {
                    $env:PROGRAMDATA = $candidateProgramData
                }
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE) -and -not [string]::IsNullOrWhiteSpace($env:PROGRAMDATA)) {
        $env:ALLUSERSPROFILE = $env:PROGRAMDATA
    }
}

function Get-UnityProjectLockFiles {
    param([string]$RepoRoot)

    $lockRelativePaths = @(
        "Temp/UnityLockfile",
        "Library/SourceAssetDB-lock",
        "Library/ArtifactDB-lock"
    )

    $detectedLocks = New-Object System.Collections.Generic.List[string]
    foreach ($relativePath in $lockRelativePaths) {
        $absolutePath = Get-FullPath -PathValue $relativePath -BasePath $RepoRoot
        if (Test-Path -LiteralPath $absolutePath) {
            $detectedLocks.Add($relativePath)
        }
    }

    return @($detectedLocks)
}

function Resolve-UnityPackageManagerExecutable {
    param([string]$ResolvedUnityEditorPath)

    $editorDirectory = Split-Path -Parent $ResolvedUnityEditorPath
    $packageManagerExecutable = Join-Path $editorDirectory "Data\Resources\PackageManager\Server\UnityPackageManager.exe"
    if (-not (Test-Path -LiteralPath $packageManagerExecutable)) {
        throw "Unity Package Manager executable was not found: $packageManagerExecutable"
    }

    return $packageManagerExecutable
}

function Start-UnityPackageManagerServer {
    param(
        [string]$ResolvedUnityEditorPath,
        [string]$RunOutputDirectory
    )

    $resolvedPackageManagerExecutable = Resolve-UnityPackageManagerExecutable -ResolvedUnityEditorPath $ResolvedUnityEditorPath
    $ipcSuffix = Get-Random -Minimum 10000 -Maximum 60000
    $unityIpcPath = "Upm-$ipcSuffix"
    $serverIpcPath = "Unity-$unityIpcPath"
    $stdoutLogPath = Join-Path $RunOutputDirectory ("unity_package_manager_stdout_{0}.log" -f $ipcSuffix)
    $stderrLogPath = Join-Path $RunOutputDirectory ("unity_package_manager_stderr_{0}.log" -f $ipcSuffix)

    $process = Start-Process -FilePath $resolvedPackageManagerExecutable -ArgumentList @(
        "-ipc",
        "-ipc-path", $serverIpcPath,
        "-cl", "2",
        "-l", "2"
    ) -PassThru -RedirectStandardOutput $stdoutLogPath -RedirectStandardError $stderrLogPath

    Start-Sleep -Seconds 3
    if (-not (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) {
        $stdoutTail = ""
        if (Test-Path -LiteralPath $stdoutLogPath) {
            $stdoutTail = (Get-Content -LiteralPath $stdoutLogPath -Tail 40) -join [Environment]::NewLine
        }

        $stderrTail = ""
        if (Test-Path -LiteralPath $stderrLogPath) {
            $stderrTail = (Get-Content -LiteralPath $stderrLogPath -Tail 40) -join [Environment]::NewLine
        }

        throw "Unity Package Manager server exited before Unity batch launch.`nSTDOUT:`n$stdoutTail`nSTDERR:`n$stderrTail"
    }

    return [pscustomobject]@{
        ProcessId     = $process.Id
        UnityIpcPath  = $unityIpcPath
        StdoutLogPath = $stdoutLogPath
        StderrLogPath = $stderrLogPath
    }
}

function Invoke-UnityPackageExport {
    param(
        [string]$RepoRoot,
        [string]$ResolvedUnityEditorPath,
        [string]$RequestFilePath,
        [string]$LogFilePath
    )

    Write-Host "Invoking Unity batchmode export..."
    Ensure-UnityBuildEnvironment

    $detectedLockFiles = Get-UnityProjectLockFiles -RepoRoot $RepoRoot
    if (@($detectedLockFiles).Count -gt 0) {
        throw "Unity batch export requires this project to be closed in the Unity Editor. Detected lock file(s): $($detectedLockFiles -join ', '). Close the open Unity instance for this project and run the export again."
    }

    $runOutputDirectory = Split-Path -Parent $LogFilePath
    $upmServer = $null

    try {
        $upmServer = Start-UnityPackageManagerServer -ResolvedUnityEditorPath $ResolvedUnityEditorPath -RunOutputDirectory $runOutputDirectory

        $argumentList = @(
            "-quit",
            "-batchmode",
            "-nographics",
            "-projectPath", $RepoRoot,
            "-upmIpcPath", $upmServer.UnityIpcPath,
            "-executeMethod", "BranchAssetExportRunner.ExportFromCommandLine",
            "-branchAssetExportRequestFile", $RequestFilePath,
            "-logFile", $LogFilePath
        )

        $process = Start-Process -FilePath $ResolvedUnityEditorPath -ArgumentList $argumentList -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -ne 0) {
            $tail = ""
            if (Test-Path -LiteralPath $LogFilePath) {
                $tail = (Get-Content -LiteralPath $LogFilePath -Tail 60) -join [Environment]::NewLine
            }

            $upmStdoutTail = ""
            if (Test-Path -LiteralPath $upmServer.StdoutLogPath) {
                $upmStdoutTail = (Get-Content -LiteralPath $upmServer.StdoutLogPath -Tail 40) -join [Environment]::NewLine
            }

            $upmStderrTail = ""
            if (Test-Path -LiteralPath $upmServer.StderrLogPath) {
                $upmStderrTail = (Get-Content -LiteralPath $upmServer.StderrLogPath -Tail 40) -join [Environment]::NewLine
            }

            throw "Unity package export failed with exit code $($process.ExitCode).`nUNITY LOG:`n$tail`nUPM STDOUT:`n$upmStdoutTail`nUPM STDERR:`n$upmStderrTail"
        }
    }
    finally {
        if ($null -ne $upmServer -and (Get-Process -Id $upmServer.ProcessId -ErrorAction SilentlyContinue)) {
            Stop-Process -Id $upmServer.ProcessId -Force
        }
    }
}

function New-ZipArchiveFromRelativeFiles {
    param(
        [string]$ZipFilePath,
        [string]$RepoRoot,
        [string[]]$RelativeFiles
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (Test-Path -LiteralPath $ZipFilePath) {
        Remove-Item -LiteralPath $ZipFilePath -Force
    }

    $zipDirectory = Split-Path -Parent $ZipFilePath
    if (-not [string]::IsNullOrWhiteSpace($zipDirectory)) {
        New-Item -ItemType Directory -Force -Path $zipDirectory | Out-Null
    }

    $fileStream = [System.IO.File]::Open($ZipFilePath, [System.IO.FileMode]::CreateNew)

    try {
        $zipArchive = New-Object System.IO.Compression.ZipArchive($fileStream, [System.IO.Compression.ZipArchiveMode]::Create, $false)

        try {
            foreach ($relativeFile in $RelativeFiles) {
                $sourceFilePath = Get-FullPath -PathValue $relativeFile -BasePath $RepoRoot
                $entry = $zipArchive.CreateEntry($relativeFile, [System.IO.Compression.CompressionLevel]::Optimal)
                $entryStream = $entry.Open()

                try {
                    $sourceStream = [System.IO.File]::OpenRead($sourceFilePath)
                    try {
                        $sourceStream.CopyTo($entryStream)
                    }
                    finally {
                        $sourceStream.Dispose()
                    }
                }
                finally {
                    $entryStream.Dispose()
                }
            }
        }
        finally {
            $zipArchive.Dispose()
        }
    }
    finally {
        $fileStream.Dispose()
    }
}

function New-ManifestContent {
    param(
        [hashtable]$ManifestData
    )

    $lines = New-Object System.Collections.Generic.List[string]

    $lines.Add("# Branch Asset Export Manifest")
    $lines.Add("")
    $lines.Add("- Branch: ``$($ManifestData.BranchName)``")
    $lines.Add("- Branch slug: ``$($ManifestData.BranchSlug)``")
    $lines.Add("- Export date (local): ``$($ManifestData.ExportDate)``")
    $lines.Add("- Dry run: ``$($ManifestData.IsDryRun)``")
    $lines.Add("- Unity editor version: ``$($ManifestData.UnityEditorVersion)``")
    $lines.Add("- Drive root folder: ``$($ManifestData.DriveRootUrl)``")
    $lines.Add("- Drive branch folder: ``$($ManifestData.DriveBranchFolderName)``")
    $lines.Add("- Source config: ``$($ManifestData.ConfigRelativePath)``")
    $lines.Add("- Local output directory: ``$($ManifestData.OutputDirectoryRelativePath)``")
    $lines.Add("")
    $lines.Add("## Artifacts")
    $lines.Add("")
    $lines.Add("- Unity package: ``$($ManifestData.UnityPackageFileName)``")
    $lines.Add("- Raw archive: ``$($ManifestData.RawArchiveFileName)``")
    $lines.Add("- Manifest: ``$($ManifestData.ManifestFileName)``")
    $lines.Add("")
    $lines.Add("## Selected Unity Roots")
    $lines.Add("")
    $lines.Add("| Selected Path | Config Root | Category | Default | Notes |")
    $lines.Add("| --- | --- | --- | --- | --- |")
    foreach ($root in $ManifestData.SelectedUnityRoots) {
        $lines.Add("| ``$($root.SelectedPath)`` | ``$($root.ConfigPath)`` | ``$($root.Category)`` | ``$($root.IsExplicitAdd -eq $false)`` | $($root.Notes) |")
    }

    $lines.Add("")
    $lines.Add("## Selected Special Resources Roots")
    $lines.Add("")
    $lines.Add("| Selected Path | Config Root | Category | Default | Notes |")
    $lines.Add("| --- | --- | --- | --- | --- |")
    foreach ($root in $ManifestData.SelectedSpecialResourcesRoots) {
        $lines.Add("| ``$($root.SelectedPath)`` | ``$($root.ConfigPath)`` | ``$($root.Category)`` | ``$($root.IsExplicitAdd -eq $false)`` | $($root.Notes) |")
    }

    $lines.Add("")
    $lines.Add("## Selected Raw Roots")
    $lines.Add("")
    $lines.Add("| Selected Path | Config Root | Category | Default | Notes |")
    $lines.Add("| --- | --- | --- | --- | --- |")
    foreach ($root in $ManifestData.SelectedRawRoots) {
        $lines.Add("| ``$($root.SelectedPath)`` | ``$($root.ConfigPath)`` | ``$($root.Category)`` | ``$($root.IsExplicitAdd -eq $false)`` | $($root.Notes) |")
    }

    $lines.Add("")
    $lines.Add("## Excluded Globs")
    $lines.Add("")
    foreach ($glob in $ManifestData.ExcludeGlobs) {
        $lines.Add("- ``$glob``")
    }

    $lines.Add("")
    $lines.Add("## Validation")
    $lines.Add("")
    $lines.Add("- Unity asset file count: ``$($ManifestData.UnityAssetCount)``")
    $lines.Add("- Raw archive file count: ``$($ManifestData.RawFileCount)``")
    $lines.Add("- Placeholder recovery scripts excluded: ``$($ManifestData.PlaceholderRecoveryScriptsExcluded)``")
    $lines.Add("- Missing ``.meta`` pair count: ``$($ManifestData.MissingMetaCount)``")

    if ($ManifestData.MissingMetaCount -gt 0) {
        $lines.Add("- Missing ``.meta`` assets:")
        foreach ($missingMetaFile in $ManifestData.MissingMetaFiles) {
            $lines.Add("  - ``$missingMetaFile``")
        }
    }

    if (@($ManifestData.MissingRoots).Count -gt 0) {
        $lines.Add("- Missing configured/selected roots:")
        foreach ($missingRoot in $ManifestData.MissingRoots) {
            $lines.Add("  - ``$missingRoot``")
        }
    }

    $lines.Add("")
    $lines.Add("## Import Order")
    $lines.Add("")
    $lines.Add("1. Restore required vendor baseline packs from Google Drive before applying this branch delta.")
    $lines.Add("2. Import ``$($ManifestData.UnityPackageFileName)`` into a clean checkout of this branch when the branch includes Unity asset deltas.")
    $lines.Add("3. Unzip ``$($ManifestData.RawArchiveFileName)`` only when you need the raw downloaded source tree. Do not treat the zip as a Unity import package.")
    $lines.Add("")
    $lines.Add("## Resources Exceptions")
    $lines.Add("")
    $lines.Add("- ``Assets/Resources/**`` stays runtime-special only. New generic project content should go under ``Assets/Project/Content``, not under ``Assets/Resources``.")
    foreach ($note in $ManifestData.SpecialResourcesNotes) {
        $lines.Add("- $note")
    }

    $lines.Add("")
    $lines.Add("## Notes")
    $lines.Add("")
    $lines.Add("- This workflow uses the repo allowlist config instead of raw ``git diff``, because local excludes and hidden tracked files can make diff incomplete.")
    $lines.Add("- Code and docs stay in Git. The export package is for Unity assets and raw download sources only.")
    $lines.Add("- Current branch export is treated as a delta export, not a full snapshot.")

    return ($lines -join [Environment]::NewLine) + [Environment]::NewLine
}

$resolvedProjectRoot = Get-FullPath -PathValue $ProjectRoot -BasePath (Get-Location).Path
$resolvedConfigPath = Get-FullPath -PathValue $ConfigPath -BasePath $resolvedProjectRoot
$resolvedOutputRoot = Get-FullPath -PathValue $OutputRoot -BasePath $resolvedProjectRoot

if (-not (Test-Path -LiteralPath $resolvedConfigPath)) {
    throw "Asset export config was not found: $resolvedConfigPath"
}

$config = Get-Content -LiteralPath $resolvedConfigPath -Raw | ConvertFrom-Json
$excludePatterns = @($config.exclude_globs | ForEach-Object { Convert-GlobToRegex $_ })

$unityConfigEntries = ConvertTo-ConfigEntries -Entries $config.unity_roots -RootType "Unity"
$rawConfigEntries = ConvertTo-ConfigEntries -Entries $config.raw_roots -RootType "Raw"
$specialResourcesConfigEntries = ConvertTo-ConfigEntries -Entries $config.special_resources_roots -RootType "SpecialResources"

$resolvedBranchName = Resolve-BranchName -RepoRoot $resolvedProjectRoot -ProvidedBranchName $BranchName
$resolvedBranchSlug = Convert-ToBranchSlug -BranchNameValue $resolvedBranchName
$driveBranchFolderName = Resolve-DriveBranchFolderName -GoogleDriveConfig $config.googleDrive -ResolvedBranchName $resolvedBranchName
$unityEditorVersion = if ($null -ne $config.unityEditorVersion -and -not [string]::IsNullOrWhiteSpace([string]$config.unityEditorVersion)) {
    [string]$config.unityEditorVersion
}
else {
    Get-UnityEditorVersion -RepoRoot $resolvedProjectRoot
}

$selectedUnityRoots = Resolve-SelectedRoots -ConfigEntries $unityConfigEntries -ExplicitPaths $UnityIncludePath
$selectedRawRoots = Resolve-SelectedRoots -ConfigEntries $rawConfigEntries -ExplicitPaths $RawIncludePath
$selectedSpecialResourcesRoots = Resolve-SelectedRoots -ConfigEntries $specialResourcesConfigEntries -ExplicitPaths $SpecialResourcesIncludePath

$unityFileSelection = Collect-SelectedFiles -SelectedRoots $selectedUnityRoots -RepoRoot $resolvedProjectRoot -ExcludePatterns $excludePatterns -SkipMetaFiles $true
$specialResourcesSelection = Collect-SelectedFiles -SelectedRoots $selectedSpecialResourcesRoots -RepoRoot $resolvedProjectRoot -ExcludePatterns $excludePatterns -SkipMetaFiles $true
$rawFileSelection = Collect-SelectedFiles -SelectedRoots $selectedRawRoots -RepoRoot $resolvedProjectRoot -ExcludePatterns $excludePatterns -SkipMetaFiles $false

$unityAssetFiles = @($unityFileSelection.Files + $specialResourcesSelection.Files | Sort-Object -Unique)
$rawArchiveFiles = @($rawFileSelection.Files | Sort-Object -Unique)
$missingRoots = @($unityFileSelection.MissingRoots + $specialResourcesSelection.MissingRoots + $rawFileSelection.MissingRoots | Sort-Object -Unique)
$missingMetaFiles = Get-MissingMetaFiles -UnityAssetFiles $unityAssetFiles -RepoRoot $resolvedProjectRoot

$placeholderRecoveryScriptsExcluded = -not ($unityAssetFiles | Where-Object { $_ -like "Assets/Scripts/Test/MissingLegacySceneScript_*" -or $_ -like "Assets/Scripts/Test/MissingArtSceneScript_*" })
if (@($missingMetaFiles).Count -gt 0) {
    throw "Unity asset export selection contains asset files without matching .meta files: $($missingMetaFiles -join ', ')"
}

$dateStamp = Get-Date -Format "yyyyMMdd"
$timestampStamp = Get-Date -Format "yyyyMMdd_HHmmss"
$runOutputDirectory = Join-Path $resolvedOutputRoot (Join-Path $resolvedBranchSlug $timestampStamp)
New-Item -ItemType Directory -Force -Path $runOutputDirectory | Out-Null

$unityPackageFileName = "{0}_delta_unity_assets_{1}.unitypackage" -f $resolvedBranchSlug, $dateStamp
$rawArchiveFileName = "{0}_raw_downloads_{1}.zip" -f $resolvedBranchSlug, $dateStamp
$manifestFileName = "{0}_manifest_{1}.md" -f $resolvedBranchSlug, $dateStamp
$requestFileName = "{0}_unity_export_request_{1}.json" -f $resolvedBranchSlug, $dateStamp
$selectionFileName = "{0}_selection_{1}.json" -f $resolvedBranchSlug, $dateStamp
$unityLogFileName = "{0}_unity_export_{1}.log" -f $resolvedBranchSlug, $dateStamp

$unityPackagePath = Join-Path $runOutputDirectory $unityPackageFileName
$rawArchivePath = Join-Path $runOutputDirectory $rawArchiveFileName
$manifestPath = Join-Path $runOutputDirectory $manifestFileName
$requestFilePath = Join-Path $runOutputDirectory $requestFileName
$selectionFilePath = Join-Path $runOutputDirectory $selectionFileName
$unityLogFilePath = Join-Path $runOutputDirectory $unityLogFileName

$requestPayload = [ordered]@{
    schemaVersion = 1
    branchName    = $resolvedBranchName
    outputPath    = $unityPackagePath
    assetPaths    = $unityAssetFiles
}
$requestPayload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $requestFilePath -Encoding utf8

$selectionPayload = [ordered]@{
    branchName                    = $resolvedBranchName
    branchSlug                    = $resolvedBranchSlug
    driveBranchFolderName         = $driveBranchFolderName
    selectedUnityRoots            = $selectedUnityRoots
    selectedSpecialResourcesRoots = $selectedSpecialResourcesRoots
    selectedRawRoots              = $selectedRawRoots
    unityAssetFiles               = $unityAssetFiles
    rawArchiveFiles               = $rawArchiveFiles
    missingMetaFiles              = $missingMetaFiles
    missingRoots                  = $missingRoots
    excludeGlobs                  = @($config.exclude_globs)
}
$selectionPayload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $selectionFilePath -Encoding utf8

$manifestContent = New-ManifestContent -ManifestData @{
    BranchName                     = $resolvedBranchName
    BranchSlug                     = $resolvedBranchSlug
    ExportDate                     = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss zzz")
    IsDryRun                       = [bool]$DryRun
    UnityEditorVersion             = $unityEditorVersion
    DriveRootUrl                   = [string]$config.googleDrive.rootFolderUrl
    DriveBranchFolderName          = $driveBranchFolderName
    ConfigRelativePath             = (Convert-ToRepoRelativePath -FullPath $resolvedConfigPath -RepoRoot $resolvedProjectRoot)
    OutputDirectoryRelativePath    = (Convert-ToRepoRelativePath -FullPath $runOutputDirectory -RepoRoot $resolvedProjectRoot)
    UnityPackageFileName           = $unityPackageFileName
    RawArchiveFileName             = $rawArchiveFileName
    ManifestFileName               = $manifestFileName
    SelectedUnityRoots             = $selectedUnityRoots
    SelectedSpecialResourcesRoots  = $selectedSpecialResourcesRoots
    SelectedRawRoots               = $selectedRawRoots
    ExcludeGlobs                   = @($config.exclude_globs)
    UnityAssetCount                = @($unityAssetFiles).Count
    RawFileCount                   = @($rawArchiveFiles).Count
    PlaceholderRecoveryScriptsExcluded = $placeholderRecoveryScriptsExcluded
    MissingMetaCount               = @($missingMetaFiles).Count
    MissingMetaFiles               = $missingMetaFiles
    MissingRoots                   = $missingRoots
    SpecialResourcesNotes          = @($selectedSpecialResourcesRoots | ForEach-Object { "``$($_.SelectedPath)`` - $($_.Notes)" })
}
$manifestContent | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host "Branch asset export selection prepared."
Write-Host "Branch: $resolvedBranchName"
Write-Host "Unity asset files: $(@($unityAssetFiles).Count)"
Write-Host "Raw archive files: $(@($rawArchiveFiles).Count)"
Write-Host "Output directory: $runOutputDirectory"

if ($DryRun) {
    Write-Host "Dry run requested. Unity package export and raw zip creation were skipped."
    return
}

if (-not $SkipUnityPackage) {
    if (@($unityAssetFiles).Count -eq 0) {
        Write-Host "No Unity asset files were selected. Unity package export was skipped."
    }
    else {
        $resolvedUnityEditorPath = Resolve-UnityEditorExecutable -RepoRoot $resolvedProjectRoot -ProvidedUnityEditorPath $UnityEditorPath
        Invoke-UnityPackageExport -RepoRoot $resolvedProjectRoot -ResolvedUnityEditorPath $resolvedUnityEditorPath -RequestFilePath $requestFilePath -LogFilePath $unityLogFilePath
    }
}
else {
    Write-Host "Unity package export was skipped by parameter."
}

if (-not $SkipRawArchive) {
    if (@($rawArchiveFiles).Count -eq 0) {
        Write-Host "No raw files were selected. Raw archive creation was skipped."
    }
    else {
        Write-Host "Creating raw archive..."
        New-ZipArchiveFromRelativeFiles -ZipFilePath $rawArchivePath -RepoRoot $resolvedProjectRoot -RelativeFiles $rawArchiveFiles
    }
}
else {
    Write-Host "Raw archive creation was skipped by parameter."
}

Write-Host "Branch asset export workflow completed."




