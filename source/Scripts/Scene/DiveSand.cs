using UnityEngine;

namespace DiveGame.Scene
{
    // The sand's height: one formula, used by the scene builder to shape the bottom and by the animals
    // and Sam to keep above it. A reef ridge rises left of the path; along the path (x = 0) a channel
    // keeps the sand at least 31.3 m below the surface, so the diver at 30 m never meets it.
    public static class DiveSand
    {
        public const float BottomY = -31f;
        const float ChannelHalfWidth = 4f;

        public static float HeightAt(float x, float z)
        {
            float h = BottomY + 1.2f * Mathf.PerlinNoise(x * 0.04f + 10f, z * 0.04f + 3f) + 0.35f * Mathf.PerlinNoise(x * 0.25f, z * 0.25f);
            float ridge = Mathf.Exp(-Mathf.Pow((x + 15f) / 9f, 2f));
            h += ridge * (1.5f + 1.2f * Mathf.PerlinNoise(z * 0.05f, 7f));
            float inChannel = Mathf.Clamp01(1f - (Mathf.Abs(x) - ChannelHalfWidth) / 6f);
            float channel = BottomY - 0.3f - 0.3f * Mathf.PerlinNoise(x * 0.3f, z * 0.3f);
            return Mathf.Lerp(h, Mathf.Min(h, channel), inChannel);
        }
    }
}
