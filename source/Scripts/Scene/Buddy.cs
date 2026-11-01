using UnityEngine;

namespace DiveGame.Scene
{
    // Sam, the dive buddy: swims beside the diver, a little ahead and to the left. Never below the
    // sand. His pose and kick are SamSwim's; his breath bubbles leave from his regulator.
    public class Buddy : MonoBehaviour
    {
        public Vector3 offset = new Vector3(-2.4f, -0.65f, 5.6f);
        public Transform mouth, bubbles;   // the regulator, and the bubble emitter kept at it
        Transform diver;

        void Start()
        {
            diver = Camera.main ? Camera.main.transform : null;
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
            if (mouth && bubbles) bubbles.position = mouth.position;
        }
    }
}
