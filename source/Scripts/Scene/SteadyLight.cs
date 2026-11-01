using StylizedWater3;
using StylizedWater3.UnderwaterRendering;
using UnityEngine;

// Keeps the light calm (his rule, 2026-11-01: the light ripples and sun rays never flicker, and
// "Reduce motion" calms them too). The scene builder already sets slow ripples and long, gentle
// fades for the sun rays. With Reduce motion on, this works on a copy of the water material (so the
// project's material is never changed) and makes the ripples almost still, the water surface
// overhead slower, and the sun rays and plankton drift more slowly.
namespace DiveGame.Scene
{
    public class SteadyLight : MonoBehaviour
    {
        public WaterGrid water;
        public UnderwaterArea area;
        public ParticleSystem sunRays;
        public ParticleSystem plankton;

        public const float CalmCausticsSpeed = 0.012f;

        void Start()
        {
            if (!PlayerChoices.ReduceMotion) return;
            if (water && water.material)
            {
                var calm = new Material(water.material) { name = water.material.name + " (calm)" };
                calm.SetFloat("_CausticsSpeed", CalmCausticsSpeed);
                Scale(calm, "_WaveSpeed", 0.3f);
                Scale(calm, "_NormalSpeed", 0.3f);
                Scale(calm, "_NormalSubSpeed", 0.3f);
                water.material = calm;
                water.SetMaterial(calm);
                if (area) area.waterMaterial = calm;   // the underwater view copies its settings from this material
            }
            if (sunRays) { var m = sunRays.main; m.simulationSpeed = 0.35f; }
            if (plankton) { var m = plankton.main; m.simulationSpeed = 0.5f; }
        }

        static void Scale(Material m, string property, float k)
        {
            if (m.HasProperty(property)) m.SetFloat(property, m.GetFloat(property) * k);
        }
    }
}
