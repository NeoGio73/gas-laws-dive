using System.Runtime.InteropServices;
using UnityEngine;

// The player's own settings. Reduce motion turns off camera sway and bobbing; it starts on when the
// browser says the player asked for reduced motion, and the player can change it.
namespace DiveGame
{
    public static class PlayerChoices
    {
        const string MotionKey = "dive.reduceMotion";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int DivePrefersReducedMotion();
#else
        static int DivePrefersReducedMotion() => 0;
#endif

        public static bool ReduceMotion
        {
            get
            {
                int saved = -1;
                try { saved = PlayerPrefs.GetInt(MotionKey, -1); } catch { }
                if (saved >= 0) return saved == 1;
                try { return DivePrefersReducedMotion() == 1; } catch { return false; }
            }
            set
            {
                try { PlayerPrefs.SetInt(MotionKey, value ? 1 : 0); PlayerPrefs.Save(); } catch { }
            }
        }
    }
}
