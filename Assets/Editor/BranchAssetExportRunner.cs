using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BranchAssetExportRunner
{
    private const string RequestArgumentName = "-branchAssetExportRequestFile";

    [Serializable]
    private sealed class BranchAssetExportRequest
    {
        public int schemaVersion = 1;
        public string branchName;
        public string outputPath;
        public string[] assetPaths = Array.Empty<string>();
    }

    public static void ExportFromCommandLine()
    {
        string requestFilePath = GetRequiredCommandLineArgument(RequestArgumentName);
        ExportFromRequestFile(requestFilePath);
    }

    public static void ExportFromRequestFile(string requestFilePath)
    {
        if (string.IsNullOrWhiteSpace(requestFilePath))
        {
            throw new ArgumentException("Asset export request path is required.", nameof(requestFilePath));
        }

        string absoluteRequestFilePath = Path.GetFullPath(requestFilePath);
        if (!File.Exists(absoluteRequestFilePath))
        {
            throw new FileNotFoundException($"Asset export request file was not found: {absoluteRequestFilePath}");
        }

        BranchAssetExportRequest request = LoadRequest(absoluteRequestFilePath);
        string[] assetPaths = BuildValidatedAssetPathList(request);
        string outputPath = PrepareOutputPath(request.outputPath);

        AssetDatabase.ExportPackage(assetPaths, outputPath, ExportPackageOptions.Recurse);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Debug.Log(
            $"[BranchAssetExportRunner] Exported branch asset package for '{request.branchName}' " +
            $"with {assetPaths.Length} asset entries: {outputPath}");
    }

    public static void ValidateBatchModeBridge()
    {
        Debug.Log("[BranchAssetExportRunner] Batchmode bridge compiled successfully.");
    }

    private static BranchAssetExportRequest LoadRequest(string absoluteRequestFilePath)
    {
        string json = File.ReadAllText(absoluteRequestFilePath);
        BranchAssetExportRequest request = JsonUtility.FromJson<BranchAssetExportRequest>(json);

        if (request == null)
        {
            throw new InvalidOperationException($"Could not parse asset export request JSON: {absoluteRequestFilePath}");
        }

        return request;
    }

    private static string[] BuildValidatedAssetPathList(BranchAssetExportRequest request)
    {
        if (request.assetPaths == null || request.assetPaths.Length == 0)
        {
            throw new InvalidOperationException("Asset export request does not contain any asset paths.");
        }

        string projectRootPath = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve Unity project root.");

        string[] normalizedAssetPaths = new string[request.assetPaths.Length];
        int normalizedCount = 0;

        for (int i = 0; i < request.assetPaths.Length; i++)
        {
            string assetPath = NormalizeAssetPath(request.assetPaths[i]);
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Asset export path must stay under Assets/: {assetPath}");
            }

            string absoluteAssetPath = Path.GetFullPath(Path.Combine(projectRootPath, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            bool exists = File.Exists(absoluteAssetPath) || Directory.Exists(absoluteAssetPath);
            if (!exists)
            {
                throw new FileNotFoundException($"Asset export path was not found: {assetPath}");
            }

            if (assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool alreadyAdded = false;
            for (int existingIndex = 0; existingIndex < normalizedCount; existingIndex++)
            {
                if (string.Equals(normalizedAssetPaths[existingIndex], assetPath, StringComparison.Ordinal))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (alreadyAdded)
            {
                continue;
            }

            normalizedAssetPaths[normalizedCount] = assetPath;
            normalizedCount++;
        }

        if (normalizedCount == 0)
        {
            throw new InvalidOperationException("No valid asset paths remained after request validation.");
        }

        Array.Resize(ref normalizedAssetPaths, normalizedCount);
        Array.Sort(normalizedAssetPaths, StringComparer.Ordinal);
        return normalizedAssetPaths;
    }

    private static string PrepareOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Asset export request does not define an output path.");
        }

        string absoluteOutputPath = Path.GetFullPath(outputPath);
        string outputDirectory = Path.GetDirectoryName(absoluteOutputPath);

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException($"Could not resolve output directory for asset export: {outputPath}");
        }

        Directory.CreateDirectory(outputDirectory);
        return absoluteOutputPath;
    }

    private static string GetRequiredCommandLineArgument(string argumentName)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], argumentName, StringComparison.OrdinalIgnoreCase))
            {
                string value = args[index + 1];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        throw new InvalidOperationException($"Missing required command line argument: {argumentName}");
    }

    private static string NormalizeAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return string.Empty;
        }

        string normalized = assetPath.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(2);
        }

        return normalized.TrimStart('/');
    }
}
