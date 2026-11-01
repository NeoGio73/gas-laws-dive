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

        // Sam (2026-11-01, his request): Quaternius's CC0 "Adventurer" in a wetsuit, coral vest, tank,
        // mask, regulator and long teal fins, made in Blender by Tools/models/build_sam.py
        // (Tools/models/sam_v2.blend). A Unity humanoid, so a swim clip can be retargeted onto him.
        const string SamPath = "Assets/DiveGame/Models/Sam/SamDiver.fbx";

        // The rig's names are Quaternius's; Unity's guess takes "Root" for the hips. The hips are
        // "Body" (the legs and the spine hang from it); the feet are "Foot.L" and "Foot.R".
        static Avatar ConfigureSam()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(SamPath);
            bool changed = imp.animationType != ModelImporterAnimationType.Human || imp.importAnimation || imp.optimizeGameObjects;
            var hd = imp.humanDescription;
            var bones = new List<HumanBone>(hd.human ?? new HumanBone[0]);
            foreach (var pair in new[] { ("Hips", "Body"), ("LeftFoot", "Foot.L"), ("RightFoot", "Foot.R") })
            {
                int i = bones.FindIndex(h => h.humanName == pair.Item1);
                if (i >= 0 && bones[i].boneName == pair.Item2) continue;
                if (i >= 0) bones.RemoveAt(i);
                var hb = new HumanBone { humanName = pair.Item1, boneName = pair.Item2 };
                hb.limit.useDefaultValues = true;
                bones.Add(hb); changed = true;
            }
            if (changed)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = false; imp.optimizeGameObjects = false;
                hd.human = bones.ToArray(); imp.humanDescription = hd;
                imp.SaveAndReimport();
            }
            Avatar found = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(SamPath)) if (a is Avatar av) found = av;
            if (found && found.isHuman) return found;
            // A new export with a changed skeleton leaves the old skeleton in the import settings and
            // the avatar fails: read the skeleton afresh (Generic, then Human), then map again.
            imp.animationType = ModelImporterAnimationType.Generic; imp.humanDescription = new HumanDescription();
            imp.SaveAndReimport();
            imp.animationType = ModelImporterAnimationType.Human; imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
            return ConfigureSamOnce();
        }

        // His swim clip (2026-11-01): Mixamo's "Swimming" (Adobe; he downloaded it, FBX for Unity,
        // without skin, 30 fps). Only its legs are used (made into a flutter kick, see KickClip), through
        // a legs-only mask, so the clip's arms and body roll never reach Sam; SamSwim keeps his arms
        // relaxed and his head up.
        const string SwimClipPath = "Assets/DiveGame/Models/Sam/Y Bot@Swimming.fbx";
        public const float SwimSpeed = 1f;   // the clip kicks about 0.44 times a second: already slow

        static AnimationClip ConfigureSwimClip()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(SwimClipPath);
            if (imp == null) return null;
            bool changed = false;
            if (imp.animationType != ModelImporterAnimationType.Human)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            var clips = imp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                if (c.loopTime && c.lockRootRotation && c.lockRootHeightY && c.lockRootPositionXZ) continue;
                c.loopTime = true; c.loopPose = true;
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
                changed = true;
            }
            if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(SwimClipPath))
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        // Mixamo's clip holds the thighs about 30° bent and the knees about 60°, spreads the legs and
        // untwists the shins (which turns Sam's fins on edge). Sam's kick keeps only the clip's hip swing
        // (scaled to about ±13°) and its knee bend (scaled to 0–18° beyond rest); every other leg muscle stays at
        // Sam's rest pose. Written to Generated/SamKick.anim on every rebuild.
        const float KickSwing = 0.7f;        // of the clip's front-back swing (0.1 of the muscle ≈ 5°)
        const float KneeMost = 0.22f;        // most knee bend beyond rest, in muscle units (0.22 ≈ 18°)

        static AnimationClip KickClip(AnimationClip source, GameObject model, Avatar avatar)
        {
            var handler = new HumanPoseHandler(avatar, model.transform);
            var pose = new HumanPose(); handler.GetHumanPose(ref pose);
            handler.Dispose();
            var names = HumanTrait.MuscleName;
            var kick = new AnimationClip { name = "Sam flutter kick", frameRate = 30f };
            int frames = Mathf.RoundToInt(source.length * 30f);
            foreach (var side in new[] { "Left", "Right" })
            {
                AnimationCurve Src(string muscle)
                {
                    foreach (var b in AnimationUtility.GetCurveBindings(source))
                        if (b.propertyName == side + " " + muscle) return AnimationUtility.GetEditorCurve(source, b);
                    return null;
                }
                var fb = Src("Upper Leg Front-Back"); var kn = Src("Lower Leg Stretch");
                // The clip's two legs kick together (two kicks per loop, like a breaststroke kick):
                // the right leg runs half a kick behind the left, which makes it a flutter kick.
                float lag = side == "Right" ? source.length / 4f : 0f;
                if (fb == null || kn == null) return null;
                float mean = 0, kMin = 9, kMax = -9;
                for (int i = 0; i < frames; i++)
                {
                    float t = i / 30f; mean += fb.Evaluate(t) / frames;
                    kMin = Mathf.Min(kMin, kn.Evaluate(t)); kMax = Mathf.Max(kMax, kn.Evaluate(t));
                }
                for (int m = 0; m < names.Length; m++)
                {
                    if (!names[m].StartsWith(side + " ") || !(names[m].Contains("Leg") || names[m].Contains("Foot") || names[m].Contains("Toes"))) continue;
                    var curve = new AnimationCurve();
                    for (int i = 0; i <= frames; i++)
                    {
                        float t = i / 30f, tw = Mathf.Repeat(t + lag, source.length), v = pose.muscles[m];
                        if (names[m] == side + " Upper Leg Front-Back") v += KickSwing * (fb.Evaluate(tw) - mean);
                        else if (names[m] == side + " Lower Leg Stretch") v = 1f - KneeMost * (kMax - kn.Evaluate(tw)) / Mathf.Max(1e-3f, kMax - kMin);
                        curve.AddKey(t, v);
                    }
                    kick.SetCurve("", typeof(Animator), names[m], curve);
                }
            }
            var settings = AnimationUtility.GetAnimationClipSettings(kick);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(kick, settings);
            Directory.CreateDirectory(Gen);
            AssetDatabase.DeleteAsset(Gen + "SamKick.anim");
            AssetDatabase.CreateAsset(kick, Gen + "SamKick.anim");
            return kick;
        }

        // A controller that plays the clip's legs only.
        static RuntimeAnimatorController SwimController(AnimationClip clip)
        {
            Directory.CreateDirectory(Gen);
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.LeftLeg || part == AvatarMaskBodyPart.RightLeg);
            }
            AssetDatabase.DeleteAsset(Gen + "SamLegs.mask");
            AssetDatabase.CreateAsset(mask, Gen + "SamLegs.mask");
            AssetDatabase.DeleteAsset(Gen + "SamSwim.controller");
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(Gen + "SamSwim.controller");
            var layers = ctrl.layers;
            layers[0].avatarMask = mask;
            ctrl.layers = layers;
            var state = ctrl.layers[0].stateMachine.AddState("Flutter kick");
            state.motion = clip; state.speed = SwimSpeed; state.writeDefaultValues = false;
            ctrl.layers[0].stateMachine.defaultState = state;
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        static bool configuring;
        static Avatar ConfigureSamOnce()
        {
            if (configuring) return null;
            configuring = true;
            try { return ConfigureSam(); } finally { configuring = false; }
        }

        static void BuildSam(Transform cam)
        {
            var avatar = ConfigureSam();
            var holder = new GameObject("Sam (dive buddy)");
            holder.transform.position = cam.position + new Vector3(-2.4f, -0.65f, 5.6f);
            var model = Spawn(SamPath, holder.transform, "Sam model");
            // He is modelled standing, feet at the origin, facing +z: lay him face down, head first,
            // trimmed a little head-up as divers swim, centred on the holder.
            model.transform.localRotation = Quaternion.Euler(84f, 0f, 0f) * model.transform.localRotation;
            model.transform.localPosition = new Vector3(0f, 0.09f, -0.95f);
            var anim = model.GetComponent<Animator>();
            if (!anim) anim = model.AddComponent<Animator>();
            anim.avatar = avatar; anim.applyRootMotion = false;
            var swim = model.AddComponent<SamSwim>();
            var source = ConfigureSwimClip();
            var clip = source ? KickClip(source, model, avatar) : null;
            if (clip)
            {
                anim.runtimeAnimatorController = SwimController(clip);
                swim.clipMovesLegs = true;
            }
            else Debug.LogWarning("Sam: no swim clip found at " + SwimClipPath + "; the kick set in code is used.");
            var buddy = holder.AddComponent<Buddy>();
            // The regulator's own pivot is at his feet (Blender's origin): mark the middle of its mesh
            // with a point that rides on it, and let the bubbles follow that.
            foreach (var t in model.GetComponentsInChildren<Transform>())
                if (t.name == "Sam Regulator" && t.GetComponent<Renderer>())
                {
                    var point = new GameObject("Mouth point").transform;
                    point.SetParent(t, false);
                    point.position = t.GetComponent<Renderer>().bounds.center;
                    buddy.mouth = point;
                }
            // Sam's bubbles leave from his regulator (Buddy keeps the emitter there).
            DiveEnvironment.BuildBubbles(holder.transform, "Sam's Bubbles", new Vector3(0f, 0f, 0.9f), 0.07f, 20, 12f, 2.2f);
            buddy.bubbles = holder.transform.Find("Sam's Bubbles");
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
