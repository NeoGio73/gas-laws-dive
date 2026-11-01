using DiveGame.Dives;
using UnityEngine;

// The diver's view. It sinks and rises with the dive's depth (DiveDirector's gas model) and drifts
// slowly forward along the reef. Gentle sway and bobbing, unless the player chose reduced motion.
// The view looks a little to the right, so the reef sits in the left part of the screen, away from
// the side panel.
namespace DiveGame.Scene
{
    [RequireComponent(typeof(Camera))]
    public class DiveCamera : MonoBehaviour
    {
        public float swimSpeed = 0.35f;       // m/s along the reef
        public float pitch = 14f;             // degrees down
        public float yaw = 4f;                // degrees right (the reef lies left of the path)
        public float surfaceSun = 1.4f;       // sunlight just under the surface
        public float lightFadeDepth = 28f;    // sunlight falls to about a third by this depth
        public float eyeBelowSurface = 1.5f;  // at "0 m" the eyes are just under the surface (clear of the waterline)

        float shownDepth = -1f;
        float z;
        bool reduceMotion;

        void Start() { reduceMotion = PlayerChoices.ReduceMotion; }

        void LateUpdate()
        {
            float target = DiveDirector.Current && DiveDirector.Current.Model != null ? DiveDirector.Current.Model.depthM : 0f;
            shownDepth = shownDepth < 0 ? target : Mathf.Lerp(shownDepth, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
            z += swimSpeed * Time.deltaTime;

            float y = -Mathf.Max(shownDepth, eyeBelowSurface);
            Vector3 pos = new Vector3(0f, y, z);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            if (!reduceMotion)
            {
                float t = Time.time;
                pos += new Vector3(Mathf.Sin(t * 0.37f) * 0.12f, Mathf.Sin(t * 0.61f) * 0.08f, 0f);
                rot *= Quaternion.Euler(Mathf.Sin(t * 0.43f) * 0.8f, Mathf.Sin(t * 0.29f) * 1.2f, Mathf.Sin(t * 0.33f) * 1.0f);
            }
            transform.SetPositionAndRotation(pos, rot);

            // Water absorbs sunlight: dimmer with depth (the same in both versions).
            if (RenderSettings.sun) RenderSettings.sun.intensity = surfaceSun * Mathf.Exp(-shownDepth / lightFadeDepth);
        }
    }
}
