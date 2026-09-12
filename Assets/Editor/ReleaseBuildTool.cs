using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Explicit local Windows build request; never uploads or publishes anything.</summary>
[InitializeOnLoad]
public static class ReleaseBuildTool
{
    private static bool _building;
    static ReleaseBuildTool() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (_building || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/ToxicBarrierUI.build")) return;
        File.Delete("Temp/ToxicBarrierUI.build");
        BuildWindows();
    }

    [MenuItem("Tools/Toxic Barrier/Build Windows Game")]
    public static void BuildWindows()
    {
        _building = true;
        try
        {
            Directory.CreateDirectory("Builds/ToxicBarrier");
            PlayerSettings.productName = "Toxic Barrier";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/GameScene.unity" },
                locationPathName = "Builds/ToxicBarrier/ToxicBarrier.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            string errors = string.Join("\n", report.steps.SelectMany(step => step.messages)
                .Where(message => message.type == LogType.Error || message.type == LogType.Exception)
                .Select(message => message.content));
            string result = $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}\n{errors}";
            File.WriteAllText("Temp/ToxicBarrierUI.build-result", result);
            if (report.summary.result != BuildResult.Succeeded) Debug.LogError(result);
            else Debug.Log("Windows build complete: Builds/ToxicBarrier/ToxicBarrier.exe");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Temp/ToxicBarrierUI.build-result", "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally { _building = false; }
    }
}
