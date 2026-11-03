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
                var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
                if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL) || apis.Length != 1 ||
                    apis[0] != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
                    throw new System.InvalidOperationException("The browser build must use WebGL 2 only.");
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
            // WebGL 2 only (his rule, 2026-11-03): never WebGPU, never chosen automatically.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            ShrinkTextures();
            AssetDatabase.SaveAssets();
        }

        // The water package's textures at full size add several MB to the download. In the browser
        // build they are capped (normals, caustics, foam, light shafts, coral and kelp textures).
        static readonly string[] SmallInBrowser =
        {
            "Assets/Stylized Water 3/Materials/Textures",
            "Assets/Stylized Water 3/Materials/Effects/Underwater",
            "Assets/Stylized Water 3/_Demo/Underwater/DemoAssets/Materials/Textures",
        };
        const int BrowserMaxSize = 512;

        static void ShrinkTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", SmallInBrowser))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                var s = imp.GetPlatformTextureSettings("WebGL");
                if (s.overridden && s.maxTextureSize <= BrowserMaxSize) continue;
                s.overridden = true;
                s.maxTextureSize = BrowserMaxSize;
                s.format = TextureImporterFormat.Automatic;
                s.textureCompression = TextureImporterCompression.Compressed;
                s.crunchedCompression = true; s.compressionQuality = 60;
                imp.SetPlatformTextureSettings(s);
                imp.SaveAndReimport();
            }
        }
    }
}
