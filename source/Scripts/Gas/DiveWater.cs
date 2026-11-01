// Water temperatures the dive computer shows on each dive. The questions in questions.json use the
// same numbers (his choice, 2026-10-31), so change both together.
namespace DiveGame.Gas
{
    public static class DiveWater
    {
        // Dive 2, the thermocline: warm water above, the cold layer below.
        public const float Dive2WarmC = 28f;
        public const float Dive2ColdC = 15f;

        // Dive 5, the ascent from 30 m: deep water below 15 m, warm water from 10 m up to the surface
        // (the safety stop at 5 m is in the warm water).
        public const float Dive5DeepC = 16f;
        public const float Dive5ShallowC = 27f;
        public const float Dive5DeepBelowM = 15f;
        public const float Dive5ShallowAboveM = 10f;

        // No dive goes deeper than this.
        public const float MaxDepthM = 30f;
    }
}
