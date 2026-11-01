using UnityEngine;

namespace DiveGame.Scene
{
    // A school: many small fish in loose formation round their parent, which swims like a Swimmer.
    // Fixed seed, so the formation is the same for everyone.
    public class School : MonoBehaviour
    {
        public float spread = 1.6f;
        public float faceOffset;   // degrees, for fish models that do not face +z
        Vector3[] offsets;
        float[] speeds;

        void Start()
        {
            var rng = new System.Random(777);
            offsets = new Vector3[transform.childCount];
            speeds = new float[transform.childCount];
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = new Vector3((float)rng.NextDouble() - 0.5f, ((float)rng.NextDouble() - 0.5f) * 0.6f, (float)rng.NextDouble() - 0.5f) * spread * 2f;
                speeds[i] = 0.8f + (float)rng.NextDouble() * 0.4f;
                var anim = transform.GetChild(i).GetComponentInChildren<Animation>();
                if (anim && anim.clip) { var st = anim[anim.clip.name]; if (st) { st.time = (float)rng.NextDouble() * st.length; st.speed = speeds[i]; } }
            }
        }

        void Update()
        {
            float t = Time.timeSinceLevelLoad;
            for (int i = 0; i < offsets.Length && i < transform.childCount; i++)
            {
                Vector3 wobble = new Vector3(Mathf.Sin(t * 0.5f * speeds[i] + i), Mathf.Sin(t * 0.4f + i * 1.7f) * 0.4f, Mathf.Cos(t * 0.45f + i * 0.9f)) * 0.25f;
                var fish = transform.GetChild(i);
                fish.localPosition = offsets[i] + wobble;
                fish.localRotation = Quaternion.Euler(0f, faceOffset, 0f);
            }
        }
    }
}
