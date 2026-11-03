using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Times every frame of one dive, for the log (his request, 2026-11-03): the average frame rate, the
// 5th-percentile frame rate (the frame rate of the slowest 5% of frames), and the size of the game's
// canvas in CSS pixels (the browser window's game area, not the whole screen). Nothing else about
// the device is recorded.
// Frames longer than half a second are left out: they are a hidden tab (the browser stops drawing
// it) or a scene loading, not the game running slowly.
namespace DiveGame.Study
{
    public class FrameRecorder : MonoBehaviour
    {
        const float LongestFrame = 0.5f;
        readonly List<float> frames = new List<float>();
        bool first = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int DiveCanvasWidth();
        [DllImport("__Internal")] static extern int DiveCanvasHeight();
#else
        static int DiveCanvasWidth() => Screen.width;
        static int DiveCanvasHeight() => Screen.height;
#endif

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (first) { first = false; return; }   // the first frame carries the scene's loading
            if (dt > 0f && dt <= LongestFrame) frames.Add(dt);
        }

        public struct Measures { public double fpsAverage, fps5thPercentile; public int canvasWidth, canvasHeight; }

        public Measures Summary()
        {
            var m = new Measures();
            try { m.canvasWidth = DiveCanvasWidth(); m.canvasHeight = DiveCanvasHeight(); } catch { }
            if (frames.Count == 0) return m;
            double total = 0;
            foreach (var f in frames) total += f;
            m.fpsAverage = System.Math.Round(frames.Count / total, 1);
            // The slowest 5% of frames (at least one), and their frame rate taken together: how many
            // there are over the time they took. Never above the average. (Until 2026-11-03 this took
            // the 95th frame time of 100, which with exactly 5 slow frames was still a fast one.)
            var sorted = new List<float>(frames);
            sorted.Sort();
            int slowest = Mathf.Max(1, Mathf.CeilToInt(0.05f * sorted.Count));
            double slowTime = 0;
            for (int i = sorted.Count - slowest; i < sorted.Count; i++) slowTime += sorted[i];
            m.fps5thPercentile = System.Math.Round(slowest / slowTime, 1);
            return m;
        }

        // For the checks: the same sums on a list of frame times.
        public static Measures From(IEnumerable<float> frameTimes)
        {
            var go = new GameObject("FrameRecorder check");
            var r = go.AddComponent<FrameRecorder>();
            foreach (var f in frameTimes) if (f > 0f && f <= LongestFrame) r.frames.Add(f);
            var m = r.Summary();
            Object.DestroyImmediate(go);
            return m;
        }
    }
}
