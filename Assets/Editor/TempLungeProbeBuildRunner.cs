using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class TempLungeProbeBuildRunner
{
    public static void Build()
    {
        string outputDir = Path.GetFullPath("Temp/LungeProbeBuild");
        Directory.CreateDirectory(outputDir);
        string exePath = Path.Combine(outputDir, "LungeProbe.exe");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/GamePlayScene_TestResult.unity" },
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception($"Build failed: {summary.result}");
        }
    }
}
