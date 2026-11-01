using UnityEngine;

namespace DiveGame.Scene
{
    // Swims a slow, flat ellipse. Its centre either travels with the diver (relative to the camera's
    // place) or stays put on the reef. Driven only by the clock, so it is the same for everyone.
    public class Swimmer : MonoBehaviour
    {
        public bool followDiver = true;
        public Vector3 centre;               // relative to the diver, or a world position
        public Vector2 radius = new Vector2(3, 5);
        public float period = 40f;           // seconds per lap
        public float phase;                  // 0..1 where on the lap it starts
        public float bob = 0.15f;
        public float minAboveSand = 0.6f;
        public float faceOffset;             // degrees, for models that do not face +z
        public bool hugSand;                 // keeps just above the bottom (the stingray), whatever the diver's depth

        Transform diver;

        void Start() { diver = Camera.main ? Camera.main.transform : null; }

        void Update()
        {
            float t = Time.timeSinceLevelLoad;
            float a = (t / period + phase) * Mathf.PI * 2f;
            Vector3 c = centre;
            if (followDiver && diver) c += new Vector3(0f, diver.position.y, diver.position.z);
            Vector3 p = c + new Vector3(Mathf.Cos(a) * radius.x, Mathf.Sin(t * 0.7f + phase * 10f) * bob, Mathf.Sin(a) * radius.y);
            float sand = DiveSand.HeightAt(p.x, p.z);
            p.y = hugSand ? sand + minAboveSand + p.y - c.y : Mathf.Max(p.y, sand + minAboveSand);
            Vector3 dir = new Vector3(-Mathf.Sin(a) * radius.x, 0f, Mathf.Cos(a) * radius.y);   // along the lap
            if (dir.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, faceOffset, 0f);
            transform.position = p;
        }
    }
}
