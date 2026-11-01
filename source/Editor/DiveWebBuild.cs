using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Dive Game > Build for the Browser: checks the questions, rebuilds the game scenes and builds the
// WebGL site into Builds/WebGL. The result is written to Builds/build_result.txt (RUNNING while it
// works). Gzip with decompression fallback, so it runs on GitHub Pages, which does not send the
// headers for compressed files.
namespace DiveGame.EditorTools
{
    public static class DiveWebBuild
    {
        const string OutputFolder = "Builds/WebGL";
        const string ResultFile = "Builds/build_result.txt";

        [MenuItem("Dive Game/Build for the Browser")]
        public static void Build()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(root, OutputFolder);
            string result = Path.Combine(root, ResultFile);
            Directory.CreateDirectory(Path.GetDirectoryName(result));
            File.WriteAllText(result, "RUNNING");
            try
            {
                // Scripts for the browser compile against the browser's Unity parts only once the
                // project's active target is WebGL (building from another target fails on WebGLInput).
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
                    !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                    throw new System.InvalidOperationException("Could not switch the project to WebGL.");
                DiveSceneBuilder.RebuildGame();
                ConfigurePlayer();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { DiveSceneBuilder.TitleScene, DiveSceneBuilder.DiveScene, DiveSceneBuilder.EndScene },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });
                var s = report.summary;
                string text = s.result + "\nerrors=" + s.totalErrors + "\nwarnings=" + s.totalWarnings +
                              "\nbytes=" + s.totalSize + "\nseconds=" + s.totalTime.TotalSeconds.ToString("F0") +
                              "\nbuild=" + GameConfig.BuildVersion + "\noutput=" + s.outputPath;
                foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception) text += "\nERROR: " + m.content;
                File.WriteAllText(result, text);
                Debug.Log("Dive Game: browser build " + s.result + " -> " + output);
            }
            catch (System.Exception e)
            {
                File.WriteAllText(result, "EXCEPTION\n" + e);
                throw;
            }
        }

        public static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Gas Laws Dive";
            PlayerSettings.bundleVersion = GameConfig.BuildVersion;
            PlayerSettings.runInBackground = true;   // keeps the clocks and the sender going in a background tab
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.WebGL.template = "PROJECT:DiveGame";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            AssetDatabase.SaveAssets();
        }
    }
}
