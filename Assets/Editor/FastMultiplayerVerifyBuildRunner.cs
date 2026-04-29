using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class FastMultiplayerVerifyBuildRunner
{
    private const string TitleScenePath = "Assets/Scenes/mutiplayer/TitleScene.unity";
    private const string VerifyScenePath = "Assets/Scenes/mutiplayer/GamePlayScene_Verify.unity";
    private const string FullGamePlayScenePath = "Assets/Scenes/mutiplayer/GamePlayScene.unity";

    private const string BeautifyResourcesPath = "Assets/Map/Beautify/URP/Runtime/Resources";
    private const string BeautifyResourcesDisabledPath = "Assets/Map/Beautify/URP/Runtime/Resources~";

    private const string VerifyOutputDirectory = "Builds/FastMultiplayerVerify";
    private const string VerifyExecutableName = "BossRaidPortfolio_MPVerify.exe";
    private const string GameplayOutputDirectory = "Builds/FastMultiplayerGameplay";
    private const string GameplayExecutableName = "BossRaidPortfolio_MPGameplay.exe";

    [MenuItem("Tools/Build/Fast Multiplayer Verify Build")]
    public static void BuildVerifyWindows64()
    {
        BuildWindows64Internal(
            targetScenePath: VerifyScenePath,
            outputDirectoryRelativePath: VerifyOutputDirectory,
            executableName: VerifyExecutableName,
            buildLabel: "verify",
            excludeBeautifyResources: true,
            revealOutputDirectory: true);
    }

    public static void BuildVerifyWindows64BatchMode()
    {
        BuildWindows64Internal(
            targetScenePath: VerifyScenePath,
            outputDirectoryRelativePath: VerifyOutputDirectory,
            executableName: VerifyExecutableName,
            buildLabel: "verify",
            excludeBeautifyResources: true,
            revealOutputDirectory: false);
    }

    [MenuItem("Tools/Build/Fast Multiplayer Gameplay Build")]
    public static void BuildGameplayWindows64()
    {
        BuildWindows64Internal(
            targetScenePath: FullGamePlayScenePath,
            outputDirectoryRelativePath: GameplayOutputDirectory,
            executableName: GameplayExecutableName,
            buildLabel: "gameplay",
            excludeBeautifyResources: false,
            revealOutputDirectory: true);
    }

    public static void BuildGameplayWindows64BatchMode()
    {
        BuildWindows64Internal(
            targetScenePath: FullGamePlayScenePath,
            outputDirectoryRelativePath: GameplayOutputDirectory,
            executableName: GameplayExecutableName,
            buildLabel: "gameplay",
            excludeBeautifyResources: false,
            revealOutputDirectory: false);
    }

    [MenuItem("Tools/Build/Restore Fast Build Assets")]
    public static void RestoreFastBuildAssets()
    {
        RestoreBeautifyResourcesIfNeeded(logIfNoop: true);
    }

    [InitializeOnLoadMethod]
    private static void RestoreBeautifyResourcesOnEditorLoad()
    {
        RestoreBeautifyResourcesIfNeeded(logIfNoop: false);
    }

    private static void BuildWindows64Internal(
        string targetScenePath,
        string outputDirectoryRelativePath,
        string executableName,
        string buildLabel,
        bool excludeBeautifyResources,
        bool revealOutputDirectory)
    {
        ValidateBuildInputs(targetScenePath);

        bool restoreAfterBuild = false;

        try
        {
            if (excludeBeautifyResources)
            {
                EnsureBeautifyResourcesExcludedForBuild(ref restoreAfterBuild);
            }

            string outputDirectory = Path.GetFullPath(outputDirectoryRelativePath);
            Directory.CreateDirectory(outputDirectory);

            string executablePath = Path.Combine(outputDirectory, executableName);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { TitleScenePath, targetScenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Fast multiplayer {buildLabel} build failed: {summary.result}");
            }

            Debug.Log($"Fast multiplayer {buildLabel} build completed: {executablePath}");

            if (revealOutputDirectory)
            {
                EditorUtility.RevealInFinder(outputDirectory);
            }
        }
        finally
        {
            if (restoreAfterBuild)
            {
                RestoreBeautifyResourcesIfNeeded(logIfNoop: false);
            }
        }
    }

    private static void ValidateBuildInputs(string targetScenePath)
    {
        if (!File.Exists(TitleScenePath))
        {
            throw new FileNotFoundException($"Missing build scene: {TitleScenePath}");
        }

        if (!File.Exists(targetScenePath))
        {
            throw new FileNotFoundException($"Missing build scene: {targetScenePath}");
        }
    }

    private static void EnsureBeautifyResourcesExcludedForBuild(ref bool restoreAfterBuild)
    {
        bool hasActiveResources = Directory.Exists(BeautifyResourcesPath);
        bool hasDisabledResources = Directory.Exists(BeautifyResourcesDisabledPath);

        if (hasActiveResources && hasDisabledResources)
        {
            throw new InvalidOperationException(
                $"Both Beautify resource folders exist. Resolve manually: {BeautifyResourcesPath}, {BeautifyResourcesDisabledPath}");
        }

        if (hasDisabledResources && !hasActiveResources)
        {
            restoreAfterBuild = true;
            Debug.Log("Beautify runtime resources are already excluded for fast build.");
            return;
        }

        if (!hasActiveResources)
        {
            Debug.Log("Beautify runtime resources were not found. Fast build will continue without exclusion step.");
            return;
        }

        MoveAssetSidecar(BeautifyResourcesPath, BeautifyResourcesDisabledPath);
        restoreAfterBuild = true;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Debug.Log("Beautify runtime resources are temporarily excluded for fast multiplayer verify build.");
    }

    private static void RestoreBeautifyResourcesIfNeeded(bool logIfNoop)
    {
        bool hasActiveResources = Directory.Exists(BeautifyResourcesPath);
        bool hasDisabledResources = Directory.Exists(BeautifyResourcesDisabledPath);

        if (hasActiveResources && hasDisabledResources)
        {
            throw new InvalidOperationException(
                $"Both Beautify resource folders exist. Resolve manually: {BeautifyResourcesPath}, {BeautifyResourcesDisabledPath}");
        }

        if (!hasDisabledResources)
        {
            if (logIfNoop)
            {
                Debug.Log("No excluded Beautify runtime resources were found.");
            }

            return;
        }

        MoveAssetSidecar(BeautifyResourcesDisabledPath, BeautifyResourcesPath);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Debug.Log("Beautify runtime resources have been restored.");
    }

    private static void MoveAssetSidecar(string sourceAssetPath, string destinationAssetPath)
    {
        string sourceMetaPath = sourceAssetPath + ".meta";
        string destinationMetaPath = destinationAssetPath + ".meta";

        if (File.Exists(destinationMetaPath) || Directory.Exists(destinationAssetPath) || File.Exists(destinationAssetPath))
        {
            throw new IOException($"Destination already exists: {destinationAssetPath}");
        }

        FileUtil.MoveFileOrDirectory(sourceAssetPath, destinationAssetPath);

        if (File.Exists(sourceMetaPath))
        {
            FileUtil.MoveFileOrDirectory(sourceMetaPath, destinationMetaPath);
        }
    }
}
