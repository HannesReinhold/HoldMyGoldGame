using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command-line WebGL build for itch.io.
///
/// Unity.exe -batchmode -nographics -quit -projectPath "<project>" -buildTarget WebGL
///           -executeMethod ItchWebGLBuild.Build -outputPath "<folder>" -logFile "<log>"
///
/// Also available from the menu: Build > WebGL for itch.io
/// </summary>
public static class ItchWebGLBuild
{
    [MenuItem("Build/WebGL for itch.io")]
    public static void BuildFromMenu()
    {
        string output = EditorUtility.SaveFolderPanel("WebGL build folder", "", "HoldMyGoldGame-WebGL");
        if (!string.IsNullOrEmpty(output))
            BuildTo(output);
    }

    public static void Build()
    {
        string output = GetArg("-outputPath");
        if (string.IsNullOrEmpty(output))
            throw new Exception("ItchWebGLBuild: missing -outputPath <folder>");

        BuildTo(output);
    }

    private static void BuildTo(string output)
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new Exception("ItchWebGLBuild: no enabled scenes in Build Settings");

        // itch.io does not reliably send Content-Encoding headers for compressed files.
        // Gzip with the decompression fallback loads everywhere; Brotli without fallback does not.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        Directory.CreateDirectory(output);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        Debug.Log($"ItchWebGLBuild: building {string.Join(", ", scenes)} to {output}");

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"ItchWebGLBuild: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, " +
                  $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime}");

        if (summary.result == BuildResult.Succeeded)
            return;

        foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage msg in step.messages)
                if (msg.type == LogType.Error || msg.type == LogType.Exception)
                    Debug.LogError($"ItchWebGLBuild [{step.name}] {msg.content}");

        if (Application.isBatchMode)
            EditorApplication.Exit(1);
        else
            throw new Exception($"ItchWebGLBuild: build {summary.result}");
    }

    private static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}
