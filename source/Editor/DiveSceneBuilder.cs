using System.Collections.Generic;
using System.IO;
using DiveGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds the game's scenes from code, so they can always be made again the same way.
// Dive Game > Rebuild Title Scene.
namespace DiveGame.EditorTools
{
    public static class DiveSceneBuilder
    {
        public const string SceneDir = "Assets/DiveGame/Scenes";
        public const string TitleScene = SceneDir + "/Title.unity";

        [MenuItem("Dive Game/Rebuild Title Scene")]
        public static void RebuildTitle()
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // For now the title sits over open blue water; the dive scenery comes later.
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0E, 0x4A, 0x6E, 0xFF);
            cam.transform.position = new Vector3(0, 1, -10);

            var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(0x4A, 0x7A, 0x9A, 0xFF);

            new GameObject("Title").AddComponent<TitleScreen>();

            EditorSceneManager.SaveScene(scene, TitleScene);
            PutFirstInBuild(TitleScene);
            Debug.Log("Dive Game: Title scene rebuilt (" + TitleScene + ").");
        }

        public const string DiveScene = SceneDir + "/Dive.unity";
        public const string EndScene = SceneDir + "/End.unity";

        // All the game's scenes, and the build list in play order: Title, Dive, End.
        [MenuItem("Dive Game/Rebuild Game Scenes")]
        public static void RebuildGame()
        {
            RebuildTitle();
            DiveEnvironment.BuildDiveScene(DiveScene);
            EditorSceneManager.OpenScene(DiveScene);
            new GameObject("Dive").AddComponent<DiveGame.Dives.DiveDirector>();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Plain(EndScene, "End", typeof(EndScreen), new Color32(0x0E, 0x4A, 0x6E, 0xFF));
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScene, true),
                new EditorBuildSettingsScene(DiveScene, true),
                new EditorBuildSettingsScene(EndScene, true),
            };
            EditorSceneManager.OpenScene(TitleScene);
            Debug.Log("Dive Game: game scenes rebuilt; build list = Title, Dive, End.");
        }

        // A scene with a camera over open water, a light and one object running the scene.
        static void Plain(string path, string name, System.Type runner, Color background)
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            new GameObject(name).AddComponent(runner);
            EditorSceneManager.SaveScene(scene, path);
        }

        // A test bench for the side panel. Not in the build list.
        [MenuItem("Dive Game/Rebuild Panel Test Scene")]
        public static void RebuildPanelTest()
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0E, 0x4A, 0x6E, 0xFF);
            new GameObject("Panel Test").AddComponent<PanelTest>();
            EditorSceneManager.SaveScene(scene, SceneDir + "/PanelTest.unity");
            Debug.Log("Dive Game: Panel test scene rebuilt.");
        }

        // A test bench for the questions. Not in the build list.
        [MenuItem("Dive Game/Rebuild Question Test Scene")]
        public static void RebuildQuestionTest()
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0E, 0x4A, 0x6E, 0xFF);
            new GameObject("Question Test").AddComponent<QuestionTest>();
            EditorSceneManager.SaveScene(scene, SceneDir + "/QuestionTest.unity");
            Debug.Log("Dive Game: Question test scene rebuilt.");
        }

        static void PutFirstInBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
