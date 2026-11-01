using System.Collections.Generic;
using System.IO;
using DiveGame.Dives;
using DiveGame.Scene;
using StylizedWater3;
using StylizedWater3.UnderwaterRendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the Dive scene's surroundings from code (Dive Game > Rebuild Game Scenes calls it):
// Stylized Water 3's surface and underwater rendering, a sandy bottom at about 31 m shaped by
// noise, a reef band of rocks, the package's coral and its kelp used small and dark as a stand-in
// for Florida's soft corals (sea plumes and sea rods; giant kelp does not grow off Florida),
// plankton and light shafts, the sun, and the diver camera with its exhaled bubbles.
// The same in both versions. Meshes and materials are regenerated on every rebuild.
namespace DiveGame.EditorTools
{
    public static class DiveEnvironment
    {
        const string Gen = "Assets/DiveGame/Generated";
        const string SW = "Assets/Stylized Water 3/";
        const string CoralPrefab = SW + "_Demo/Underwater/DemoAssets/Prefabs/SW3_Coral.prefab";
        const string KelpPrefab = SW + "_Demo/Underwater/DemoAssets/Prefabs/SW3_Kelp.prefab";
        const string PlanktonPrefab = SW + "Prefabs/Particles/Underwater/Plankton.prefab";
        const string ShaftsPrefab = SW + "Prefabs/Particles/Underwater/Lightshafts.prefab";
        const string BubbleMat = SW + "Materials/Effects/Underwater/BubbleParticle.mat";
        const string SourceWaterMat = SW + "Materials/StylizedWater3_Default.mat";

        public const float BottomY = -31f;
        // The camera swims along +z from z = 0; the reef lies along that path.
        const float PathLength = 260f;

        public static void BuildDiveScene(string path)
        {
            Directory.CreateDirectory(Gen);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rng = new System.Random(20261101);

            // ---- light and the air above ----
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(58f, -25f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.48f, 0.56f);

            // ---- camera ----
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1E, 0x7F, 0xA8, 0xFF);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 220f;
            cam.fieldOfView = 62f;
            cam.transform.position = new Vector3(0, -1, 0);
            cam.gameObject.AddComponent<DiveCamera>();
            // The player's own bubbles (they pass the face quickly), and Sam's: a column a few
            // metres ahead and to the left, where Sam swims, that stays in view as it rises.
            BuildBubbles(cam.transform, "Breath Bubbles", new Vector3(0.1f, -0.42f, 1.1f), 0.06f, 18, 7f, 0f);
            BuildBubbles(cam.transform, "Sam's Bubbles", new Vector3(-2.4f, -0.6f, 6f), 0.07f, 20, 12f, 2.2f);

            // ---- water surface and underwater rendering ----
            Material water = WaterMaterial();
            var waterGO = new GameObject("Water Surface");
            var grid = waterGO.AddComponent<WaterGrid>();
            grid.material = water;
            grid.scale = 300f; grid.vertexDistance = 3f; grid.rowsColumns = 4;
            grid.followTarget = cam.transform;
            grid.Recreate();
            grid.SetMaterial(water);

            var areaGO = new GameObject("Underwater Area");
            var area = areaGO.AddComponent<UnderwaterArea>();
            area.Reset();
            area.waterMaterial = water;
            area.waterLevelSource = UnderwaterArea.WaterLevelSource.FixedValue;
            area.waterLevel = 0f;
            area.boxCollider.size = new Vector3(2000f, 80f, 2000f);
            area.boxCollider.center = new Vector3(0f, -38f, 0f);   // top 2 m above the surface
            if (area.shadingSettings == null) area.shadingSettings = new UnderwaterArea.ShadingSettings();
            if (area.particleEffects == null) area.particleEffects = new List<UnderwaterArea.ParticleEffect>();
            var s = area.shadingSettings;
            // Tuned by eye (2026-11-01): about 20 m of visibility, bluer with distance and depth.
            s.fogDensity = 0.35f; s.fogStartDistance = 2f; s.fogBrightness = 0.9f; s.colorAbsorption = 0.25f;
            s.heightFogDensity = 3f; s.heightFogStart = 3f; s.heightFogEnd = 30f; s.heightFogBrightness = 0.35f;
            s.causticsBrightness = 0.6f; s.translucencyStrength = 0.5f; s.translucencyExponent = 2f;
            AddEffect(area, PlanktonPrefab, 0f, 40f, false);
            AddEffect(area, ShaftsPrefab, 0f, 14f, true);

            // ---- the bottom and the reef ----
            var env = new GameObject("Seafloor").transform;
            BuildSand(env);
            var rocks = RockMeshes(rng);
            Material rockMat = Lit("Rock", new Color(0.46f, 0.42f, 0.36f), 0.15f);
            var coralMats = new[]
            {
                Tinted("Coral Brain", new Color(0.78f, 0.70f, 0.48f)),    // brain coral: tan
                Tinted("Coral Star", new Color(0.62f, 0.72f, 0.50f)),     // star coral: olive green
                Tinted("Coral Orange", new Color(0.92f, 0.55f, 0.32f)),   // orange sponges and corals
                Tinted("Coral Purple", new Color(0.62f, 0.42f, 0.70f)),   // sea fans: purple
            };
            Material plume = Tinted("Soft Coral", new Color(0.45f, 0.30f, 0.38f));

            var reef = new GameObject("Reef").transform; reef.SetParent(env);
            for (int i = 0; i < 90; i++)   // rocks: the reef's frame
            {
                Vector3 at = ReefPoint(rng, 0.9f);
                var r = new GameObject("Rock", typeof(MeshFilter), typeof(MeshRenderer));
                r.transform.SetParent(reef);
                r.GetComponent<MeshFilter>().sharedMesh = rocks[rng.Next(rocks.Count)];
                r.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
                float size = 0.8f + (float)rng.NextDouble() * 2.6f;
                r.transform.position = at + Vector3.down * size * 0.35f;
                r.transform.localScale = new Vector3(size, size * (0.5f + 0.4f * (float)rng.NextDouble()), size);
                r.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                r.isStatic = true;
            }
            Scatter(reef, CoralPrefab, 150, rng, 0.7f, 2.0f, coralMats);
            Scatter(reef, KelpPrefab, 60, rng, 0.18f, 0.32f, new[] { plume });

            EditorSceneManager.SaveScene(scene, path);
        }

        // ---------- parts ----------

        // The diver's path runs along x = 0. A sandy channel there stays below 30 m (the dives go no
        // deeper), so the camera never meets the bottom; nothing is placed in it.
        const float ChannelHalfWidth = 4f;

        static Vector3 ReefPoint(System.Random rng, float spread)
        {
            // Mostly a band left of the path (so the reef shows in the left of the screen, away from
            // the side panel), some scattered wider.
            float x;
            do x = (float)(rng.NextDouble() < spread ? -29 + rng.NextDouble() * 26 : -50 + rng.NextDouble() * 95);
            while (Mathf.Abs(x) < ChannelHalfWidth + 1.5f);
            float z = (float)(-10 + rng.NextDouble() * (PathLength + 20));
            return new Vector3(x, SandHeight(x, z), z);
        }

        static float SandHeight(float x, float z)
        {
            float h = BottomY + 1.2f * Mathf.PerlinNoise(x * 0.04f + 10f, z * 0.04f + 3f) + 0.35f * Mathf.PerlinNoise(x * 0.25f, z * 0.25f);
            float ridge = Mathf.Exp(-Mathf.Pow((x + 15f) / 9f, 2f));   // the reef rises along its band
            h += ridge * (1.5f + 1.2f * Mathf.PerlinNoise(z * 0.05f, 7f));
            // Flatten into the channel along the path: there the sand is at least 31.3 m below the surface.
            float inChannel = Mathf.Clamp01(1f - (Mathf.Abs(x) - ChannelHalfWidth) / 6f);
            float channel = BottomY - 0.3f - 0.3f * Mathf.PerlinNoise(x * 0.3f, z * 0.3f);
            return Mathf.Lerp(h, Mathf.Min(h, channel), inChannel);
        }

        static void BuildSand(Transform parent)
        {
            const int nx = 90, nz = 120;   // about 1.5 m and 3.4 m between points: smooth enough, light to download
            const float x0 = -110f, x1 = 110f, z0 = -60f, z1 = PathLength + 80f;
            var verts = new Vector3[nx * nz];
            var uv = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = Mathf.Lerp(x0, x1, i / (nx - 1f)), z = Mathf.Lerp(z0, z1, j / (nz - 1f));
                    verts[j * nx + i] = new Vector3(x, SandHeight(x, z), z);
                    uv[j * nx + i] = new Vector2(x * 0.15f, z * 0.15f);
                }
            var tris = new List<int>();
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i;
                    tris.AddRange(new[] { a, a + nx, a + 1, a + 1, a + nx, a + nx + 1 });
                }
            var mesh = new Mesh { name = "Sand", vertices = verts, uv = uv, triangles = tris.ToArray() };   // under 65,536 points: 16-bit indices
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Medium);
            Save(mesh, "Sand");
            var go = new GameObject("Sand", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = Lit("Sand", new Color(0.72f, 0.66f, 0.52f), 0.05f);
            go.isStatic = true;
        }

        // A few lumpy rocks: icospheres pushed in and out by noise.
        static List<Mesh> RockMeshes(System.Random rng)
        {
            var list = new List<Mesh>();
            for (int k = 0; k < 4; k++)
            {
                var m = Icosphere(2);
                var v = m.vertices;
                float o = (float)rng.NextDouble() * 50f;
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 d = v[i].normalized;
                    float n = Mathf.PerlinNoise(d.x * 2.1f + o, d.y * 2.1f + d.z * 1.3f + o);
                    v[i] = d * (0.75f + 0.5f * n);
                }
                m.vertices = v; m.RecalculateNormals(); m.RecalculateBounds(); m.name = "Rock " + k;
                Save(m, "Rock" + k);
                list.Add(m);
            }
            return list;
        }

        static Mesh Icosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3> {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            var faces = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                                        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<int>(); var mid = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int i)) return i;
                    verts.Add((verts[a] + verts[b]) * 0.5f); mid[key] = verts.Count - 1; return verts.Count - 1;
                }
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            return new Mesh { vertices = verts.ToArray(), triangles = faces.ToArray() };
        }

        static void Scatter(Transform parent, string prefabPath, int count, System.Random rng, float minScale, float maxScale, Material[] mats)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) { Debug.LogWarning("Dive Game: missing " + prefabPath); return; }
            for (int i = 0; i < count; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Vector3 at = ReefPoint(rng, 0.85f);
                float sc = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
                go.transform.position = at + Vector3.down * 0.1f;
                go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                go.transform.localScale = Vector3.one * sc;
                var mat = mats[rng.Next(mats.Length)];
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                go.isStatic = true;
                foreach (Transform t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            }
        }

        static void AddEffect(UnderwaterArea area, string prefabPath, float minDepth, float maxDepth, bool alignToSun)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) { Debug.LogWarning("Dive Game: missing " + prefabPath); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, area.transform);
            var ps = go.GetComponent<ParticleSystem>() ?? go.GetComponentInChildren<ParticleSystem>();
            area.particleEffects.Add(new UnderwaterArea.ParticleEffect(minDepth, maxDepth, alignToSun) { particleSystem = ps });
        }

        static void BuildBubbles(Transform cam, string name, Vector3 local, float size, int perBreath, float lifetime, float delay)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(cam, false);
            go.transform.localPosition = local;
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true; main.loop = true; main.duration = 5f;
            main.startLifetime = lifetime; main.startSpeed = 0f; main.startSize = size;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256; main.gravityModifier = 0f;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.06f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            // Every velocity curve (x, y, z and the hidden orbital and radial ones) must be in the same
            // mode, or the browser build complains every frame: all plain constants; the noise below
            // gives the wobble.
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.8f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.6f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BubbleMat);
            var b = go.AddComponent<BreathBubbles>();
            b.baseSize = size; b.perBreath = perBreath; b.firstDelay = 1.5f + delay;
        }

        // ---------- materials ----------

        static Material WaterMaterial()
        {
            string path = Gen + "/Water.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(SourceWaterMat, path);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            // Clear Atlantic blue-green off Florida.
            m.SetColor("_BaseColor", new Color(0.02f, 0.22f, 0.32f, 1f));
            m.SetColor("_ShallowColor", new Color(0.10f, 0.58f, 0.62f, 0.6f));
            m.SetFloat("_NormalStrength", 0.25f);
            m.SetFloat("_CausticsChromance", 0.15f);   // light ripples on the sand: almost no rainbow fringes
            m.SetFloat("_CausticsTiling", 0.35f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Lit(string name, Color color, float smoothness)
        {
            string path = Gen + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // A tinted copy of the package's coral material.
        static Material Tinted(string name, Color color)
        {
            string path = Gen + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(SW + "_Demo/Underwater/DemoAssets/Materials/DemoCoral.mat", path);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Save(Mesh mesh, string name)
        {
            string path = Gen + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }
    }
}
