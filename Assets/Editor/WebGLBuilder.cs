using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One-click WebGL build for putting the game on a website.
// Menu: Tools > Haunted House > Build WebGL   ->   <project>/Builds/WebGL
//
// Uses Gzip with the decompression fallback so the build runs on any static host
// (itch.io, GitHub Pages, Netlify, your own site) without special server headers.
[InitializeOnLoad]
public static class WebGLBuilder
{
    const string ScenePath = "Assets/Scenes/HauntedHouse.unity";
    const string OutputDir = "Builds/WebGL";
    const string PendingKey = "HauntedHouse.BuildWebGLAfterSwitch";

    // Responsive page in Assets/WebGLTemplates/Responsive: game fills the window at 16:9.
    const string Template = "PROJECT:Responsive";

    // Switching platform reloads scripts, so the build resumes here afterwards.
    static WebGLBuilder()
    {
        // Keep the saved project setting pointing at our template (the cloud build reads it).
        if (PlayerSettings.WebGL.template != Template)
        {
            PlayerSettings.WebGL.template = Template;
            AssetDatabase.SaveAssets();
        }

        if (!SessionState.GetBool(PendingKey, false)) return;
        SessionState.SetBool(PendingKey, false);
        EditorApplication.delayCall += () =>
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL) RunBuild();
            else Debug.LogError("HauntedHouse: platform switch to WebGL didn't complete; try Build WebGL again.");
        };
    }

    [MenuItem("Tools/Haunted House/Build WebGL")]
    public static void Build()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("HauntedHouse: build the map first (Tools > Haunted House > Build Map).");
            return;
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            Debug.Log("HauntedHouse: switching platform to WebGL (reimports assets, takes a while)...");
            SessionState.SetBool(PendingKey, true);
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                SessionState.SetBool(PendingKey, false);
                Debug.LogError("HauntedHouse: couldn't switch to WebGL. Is the WebGL module installed in Unity Hub?");
                return;
            }
            // If the switch finished without a script reload, build right away.
            if (SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                RunBuild();
            }
            return;
        }

        RunBuild();
    }

    static void RunBuild()
    {
        PlayerSettings.productName = "Haunted House Escape";
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.template = Template;
        // Smaller generated code: less disk needed while building and a lighter download.
        var web = UnityEditor.Build.NamedBuildTarget.WebGL;
        PlayerSettings.SetIl2CppCodeGeneration(web, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.Medium);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };

        Debug.Log("HauntedHouse: WebGL build started.");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"HauntedHouse: WebGL build succeeded -> {Path.GetFullPath(OutputDir)} ({summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalMinutes:F1} min)");
        }
        else
        {
            Debug.LogError($"HauntedHouse: WebGL build {summary.result} with {summary.totalErrors} errors. See the Console for details.");
            foreach (var step in report.steps)
                foreach (var msg in step.messages)
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"HauntedHouse build error [{step.name}]: {msg.content}");
        }
    }
}
