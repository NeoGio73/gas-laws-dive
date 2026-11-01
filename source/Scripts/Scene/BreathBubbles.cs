using DiveGame.Gas;
using UnityEngine;

// The diver's exhaled bubbles, the same in both versions. A puff every few seconds rises from the
// regulator, below and in front of the view. Each bubble keeps the same amount of air, so as it
// rises its volume follows Boyle's law: V = V0 × P0 / P, so its width grows with the cube root of
// P0 / P. (Close to the surface the growth is easy to see; at 30 m it is only a few percent.)
namespace DiveGame.Scene
{
    [RequireComponent(typeof(ParticleSystem))]
    public class BreathBubbles : MonoBehaviour
    {
        public float everySeconds = 4.5f;
        public int perBreath = 18;
        public float riseSpeed = 0.8f;      // m/s
        public float baseSize = 0.06f;      // m across, as they leave the regulator (drawn a little large, to be seen)

        public float firstDelay = 1.5f;     // seconds before the first breath (the two divers breathe out of step)

        ParticleSystem ps;
        ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[256];
        float next;

        void Awake() { ps = GetComponent<ParticleSystem>(); next = firstDelay; }

        void Update()
        {
            next -= Time.deltaTime;
            if (next <= 0f)
            {
                next = everySeconds;
                // Underwater only: no bubbles from a diver at the surface.
                if (transform.position.y < -0.5f) ps.Emit(perBreath);
            }

            int n = ps.GetParticles(buffer);
            for (int i = 0; i < n; i++)
            {
                var p = buffer[i];
                float age = p.startLifetime - p.remainingLifetime;
                float bornAtY = p.position.y - riseSpeed * age;              // where it left the regulator
                float pBorn = GasModel.PressureAtDepth(-bornAtY);
                float pNow = GasModel.PressureAtDepth(-p.position.y);
                float grow = Mathf.Pow(pBorn / pNow, 1f / 3f);
                float seed = (p.randomSeed % 1000) / 1000f;                 // bubbles differ a little in size
                p.startSize = baseSize * (0.6f + 0.8f * seed) * grow;
                if (p.position.y > -0.05f) p.remainingLifetime = 0f;         // they burst at the surface
                buffer[i] = p;
            }
            ps.SetParticles(buffer, n);
        }
    }
}
