using UnityEngine;

namespace DiveGame.Scene
{
    // Sam, the dive buddy: swims beside the diver, a little ahead and to the left, with a slow
    // flutter kick (legs swing alternately at the hips). Never below the sand.
    public class Buddy : MonoBehaviour
    {
        public Vector3 offset = new Vector3(-2.4f, -0.65f, 5.6f);
        public Transform hipLeft, hipRight;
        public float kickDegrees = 12f, kickPerSecond = 0.7f;
        Transform diver;
        Quaternion hipL0, hipR0;

        void Start()
        {
            diver = Camera.main ? Camera.main.transform : null;
            if (hipLeft) hipL0 = hipLeft.localRotation;
            if (hipRight) hipR0 = hipRight.localRotation;
            if (diver) transform.position = Target();
        }

        Vector3 Target()
        {
            Vector3 p = new Vector3(offset.x, diver.position.y + offset.y, diver.position.z + offset.z);
            p.y = Mathf.Max(p.y, DiveSand.HeightAt(p.x, p.z) + 0.6f);
            return p;
        }

        void LateUpdate()
        {
            if (diver) transform.position = Vector3.Lerp(transform.position, Target(), 1f - Mathf.Exp(-3f * Time.deltaTime));
            float k = Mathf.Sin(Time.timeSinceLevelLoad * kickPerSecond * Mathf.PI * 2f) * kickDegrees;
            if (hipLeft) hipLeft.localRotation = hipL0 * Quaternion.Euler(k, 0f, 0f);
            if (hipRight) hipRight.localRotation = hipR0 * Quaternion.Euler(-k, 0f, 0f);
        }
    }
}
