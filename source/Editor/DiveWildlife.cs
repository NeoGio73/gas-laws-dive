using System.Collections.Generic;
using System.IO;
using DiveGame.Scene;
using UnityEditor;
using UnityEngine;

// Places the reef's animals and Sam (called by DiveEnvironment.BuildDiveScene). Stylized models
// only (his choice, 2026-11-01): Quaternius's CC0 low-poly fish from Poly Pizza (lionfish, puffer,
// butterflyfish, flatfish, school fish), chroma 3D's low-poly sea turtle and stingray (BlenderKit,
// royalty free), and Sam, made for this game in Blender (Tools/models/dive_animals.blend).
// Every number is fixed here, so the animals are the same for every student and both versions.
namespace DiveGame.EditorTools
{
    public static class DiveWildlife
    {
        const string Fish = "Assets/DiveGame/Models/Fish/";
        const string Animals = "Assets/DiveGame/Models/Animals/";
        const string Gen = "Assets/DiveGame/Generated/";
        const float FishFace = 90f;   // Quaternius's fish face −x as imported: a quarter turn faces them along their path

        // The swim loop inside each fish's baked clip (frames at 24 per second).
        static readonly Dictionary<string, int> SwimFrames = new Dictionary<string, int>
        {
            { "Lionfish", 55 }, { "Puffer", 55 }, { "Butterflyfish", 55 }, { "Flatfish", 55 }, { "SchoolFish", 31 },
        };

        public static void Build(Transform cam, System.Random rng)
        {
            foreach (var kv in SwimFrames) ConfigureFish(Fish + kv.Key + ".fbx", kv.Value);

            var root = new GameObject("Wildlife").AddComponent<Wildlife>().transform;

            // Reef dwellers, at fixed places along the reef near the path: seen near the bottom
            // (Dive 1 at 30 m, the start of Dive 5).
            foreach (float z in new[] { 9f, 19f, 33f, 52f, 70f })
                Hover(root, "Lionfish", new Vector3(-5.5f - (z % 3f) * 0.5f, 0, z), 0.5f, 18f, z * 0.01f);
            foreach (float z in new[] { 6f, 15f, 27f, 44f, 62f })
            {   // butterflyfish swim in pairs
                Hover(root, "Butterflyfish", new Vector3(-4.8f, 0.4f, z), 0.9f, 14f, 0f);
                Hover(root, "Butterflyfish", new Vector3(-4.8f, 0.4f, z), 0.9f, 14f, 0.08f);
            }
            Hover(root, "Puffer", new Vector3(-6f, 0.6f, 22f), 1.2f, 30f, 0.3f);
            Hover(root, "Puffer", new Vector3(-5.5f, 0.5f, 58f), 1.0f, 26f, 0.7f);
            foreach (var at in new[] { new Vector3(-2.8f, 0, 10f), new Vector3(-3.4f, 0, 29f) })
                Flounder(root, at);

            // Swimmers that travel with the diver, ahead and to the left, at the diver's depth.
            // Facing: the turtle and stingray face +z as imported (their eyes are at Blender's −Y end);
            // Quaternius's fish face −x as imported, so they get a quarter turn (FishFace).
            var turtle = Swimmer(root, Animals + "SeaTurtle.fbx", "Sea Turtle", new Vector3(-4.2f, -0.8f, 9f), new Vector2(2.2f, 3.2f), 55f, 0.15f, 0f, false);
            Wave(turtle, 0.55f, 1.4f, 0f, 0.17f);     // flippers rise and fall together
            var ray = Swimmer(root, Animals + "Stingray.fbx", "Stingray", new Vector3(-3.5f, 0f, 8f), new Vector2(2.5f, 3.5f), 48f, 0.6f, 0f, true);
            Wave(ray, 0.28f, 2.6f, 3.5f, 0.12f);      // wings ripple from head to tail

            var school = new GameObject("School of fish");
            school.transform.SetParent(root, false);
            var sw = school.AddComponent<Swimmer>();
            sw.followDiver = true; sw.centre = new Vector3(-4.5f, 1.0f, 10f); sw.radius = new Vector2(2.8f, 3.6f); sw.period = 36f; sw.phase = 0.45f; sw.bob = 0.4f;
            school.AddComponent<School>().faceOffset = FishFace;
            for (int i = 0; i < 26; i++) Spawn(Fish + "SchoolFish.fbx", school.transform, "Fish " + (i + 1));

            BuildSam(cam);
        }

        // ---------- kinds of animal ----------

        static void Hover(Transform root, string kind, Vector3 at, float radius, float period, float phase)
        {
            var holder = new GameObject(kind);
            holder.transform.SetParent(root, false);
            var s = holder.AddComponent<Swimmer>();
            s.followDiver = false;
            s.centre = new Vector3(at.x, DiveSand.HeightAt(at.x, at.z) + 0.8f + at.y, at.z);
            s.radius = new Vector2(radius, radius * 0.7f); s.period = period; s.phase = phase; s.bob = 0.1f; s.minAboveSand = 0.4f;
            s.faceOffset = FishFace;
            Spawn(Fish + kind + ".fbx", holder.transform, kind + " model");
        }

        // A flatfish lies on its side on the sand, still, half-hidden: a flounder.
        static void Flounder(Transform root, Vector3 at)
        {
            var go = Spawn(Fish + "Flatfish.fbx", root, "Flounder");
            go.transform.position = new Vector3(at.x, DiveSand.HeightAt(at.x, at.z) + 0.03f, at.z);
            go.transform.rotation = Quaternion.Euler(0f, 30f + at.z * 7f, 90f) * go.transform.rotation;   // on its side, keeping the import rotation
            foreach (var a in go.GetComponentsInChildren<Animation>()) Object.DestroyImmediate(a);
        }

        static GameObject Swimmer(Transform root, string path, string name, Vector3 centre, Vector2 radius, float period, float phase, float face, bool hugSand)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(root, false);
            var s = holder.AddComponent<Swimmer>();
            s.followDiver = true; s.centre = centre; s.radius = radius; s.period = period; s.phase = phase; s.faceOffset = face;
            s.hugSand = hugSand; s.bob = hugSand ? 0.05f : 0.25f;
            return Spawn(path, holder.transform, name + " model");
        }

        // Gives an unanimated model the SwimWave shader, keeping each part's colour.
        static void Wave(GameObject model, float amp, float freq, float phase, float inner)
        {
            var shader = Shader.Find("DiveGame/SwimWave");
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    string path = Gen + model.name.Replace(" ", "") + "_" + src.name.Replace(" ", "").Replace("\\", "").Replace(".", "") + ".mat";
                    AssetDatabase.DeleteAsset(path);
                    var m = new Material(shader);
                    m.SetColor("_BaseColor", src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.color);
                    m.SetFloat("_Amp", amp); m.SetFloat("_Freq", freq); m.SetFloat("_Phase", phase); m.SetFloat("_Inner", inner);
                    m.enableInstancing = true;
                    AssetDatabase.CreateAsset(m, path);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        static void BuildSam(Transform cam)
        {
            var holder = new GameObject("Sam (dive buddy)");
            holder.transform.position = cam.position + new Vector3(-2.4f, -0.65f, 5.6f);
            var model = Spawn("Assets/DiveGame/Models/Sam/Sam.fbx", holder.transform, "Sam model");
            // The model faces −z: turn it round, keeping the rotation the importer gave it.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
            var buddy = holder.AddComponent<Buddy>();
            foreach (var t in model.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Hip L") buddy.hipLeft = t;
                if (t.name == "Hip R") buddy.hipRight = t;
            }
            // Sam's bubbles come from Sam's regulator (the model's head end, now facing +z).
            DiveEnvironment.BuildBubbles(holder.transform, "Sam's Bubbles", new Vector3(0f, 0f, 0.64f), 0.07f, 20, 12f, 2.2f);
        }

        // ---------- helpers ----------

        static GameObject Spawn(string path, Transform parent, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var a in go.GetComponentsInChildren<Animation>()) { a.playAutomatically = true; a.wrapMode = WrapMode.Loop; a.cullingType = AnimationCullingType.BasedOnRenderers; }
            return go;
        }

        // Legacy animation with one looping clip, "Swim": the fish's swim loop only.
        static void ConfigureFish(string path, int frames)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) return;
            if (imp.animationType == ModelImporterAnimationType.Legacy && imp.clipAnimations.Length == 1 &&
                imp.clipAnimations[0].name == "Swim" && (int)imp.clipAnimations[0].lastFrame == frames) return;
            imp.animationType = ModelImporterAnimationType.Legacy;
            imp.importAnimation = true;
            var take = imp.importedTakeInfos.Length > 0 ? imp.importedTakeInfos[0].name : "Scene";
            imp.clipAnimations = new[]
            {
                new ModelImporterClipAnimation { name = "Swim", takeName = take, firstFrame = 0, lastFrame = frames, loopTime = true, wrapMode = WrapMode.Loop }
            };
            imp.SaveAndReimport();
        }
    }
}
