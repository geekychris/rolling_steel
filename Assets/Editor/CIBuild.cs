using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Headless macOS player build, driven from the command line with:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod CIBuild.BuildMacOS -buildOut <path>
public static class CIBuild
{
    public static void BuildMacOS()
    {
        string outPath = ArgValue("-buildOut") ?? "Builds/Mac/Player.app";
        if (!Path.IsPathRooted(outPath))
            outPath = Path.Combine(Directory.GetCurrentDirectory(), outPath);

        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Fail("No enabled scenes in EditorBuildSettings; nothing to build.");
            return;
        }

        Debug.Log($"[CIBuild] Building {scenes.Length} scene(s) -> {outPath}");
        foreach (var s in scenes) Debug.Log($"[CIBuild]   scene: {s}");

        // IL2CPP is not installed on this machine, so pin the Mono backend.
        var group = BuildTargetGroup.Standalone;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.StandaloneOSX,
            targetGroup = group,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary summary = report.summary;

        Debug.Log($"[CIBuild] result={summary.result} errors={summary.totalErrors} " +
                  $"warnings={summary.totalWarnings} size={summary.totalSize / (1024 * 1024)}MB " +
                  $"time={summary.totalTime}");

        if (summary.result != BuildResult.Succeeded)
        {
            Fail($"Build did not succeed: {summary.result}");
            return;
        }

        Debug.Log("[CIBuild] BUILD_OK " + outPath);
        EditorApplication.Exit(0);
    }

    static void Fail(string msg)
    {
        Debug.LogError("[CIBuild] BUILD_FAILED " + msg);
        EditorApplication.Exit(1);
    }

    static string ArgValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
