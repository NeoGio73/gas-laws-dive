using System.Collections.Generic;
using System.IO;
using DiveGame.Study;
using UnityEngine;

// A check of the per-dive measures in the log (frame rates, canvas size): runs the frame
// recorder's sums on known frame times, writes them on a dive's rows the way the dive's end does,
// leaves a survey row unmeasured, and writes the batch the game would send to
// Builds/log_check.json (post it to tools/mock_sheet.py to see the Sheet's columns) and a report to
// Builds/log_check.txt. Run from the command line:
//   Unity.exe -batchmode -quit -projectPath . -executeMethod DiveGame.EditorTools.LogMeasuresCheck.Run
namespace DiveGame.EditorTools
{
    public static class LogMeasuresCheck
    {
        public static void Run()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            var report = new List<string>();
            bool ok = true;
            void Expect(string what, double got, double want)
            {
                bool pass = System.Math.Abs(got - want) < 0.051;
                ok &= pass;
                report.Add((pass ? "ok   " : "FAIL ") + what + ": " + got + " (expected " + want + ")");
            }
            void Below(string what, FrameRecorder.Measures mm)
            {
                bool pass = mm.fps5thPercentile <= mm.fpsAverage;
                ok &= pass;
                report.Add((pass ? "ok   " : "FAIL ") + what + ": fps_p5 " + mm.fps5thPercentile + " <= fps_avg " + mm.fpsAverage);
            }

            // 95 frames at 1/60 s and 5 at 1/20 s, plus a 2 s gap (a hidden tab) that must be left out.
            var frames = new List<float>();
            for (int i = 0; i < 95; i++) frames.Add(1f / 60f);
            for (int i = 0; i < 5; i++) frames.Add(1f / 20f);
            frames.Add(2f);
            var m = FrameRecorder.From(frames);
            double total = 95 / 60.0 + 5 / 20.0;
            Expect("average fps", m.fpsAverage, System.Math.Round(100 / total, 1));   // 54.5
            // The slowest 5% are the five 1/20 s frames: 20 fps. (The first version took the 95th
            // frame time of 100, a fast one, and gave 60, above the average: impossible.)
            Expect("5th-percentile fps with 5 slow frames in 100", m.fps5thPercentile, 20.0);
            Below("5th-percentile below average (5 slow frames)", m);
            var slow = new List<float>();
            for (int i = 0; i < 90; i++) slow.Add(1f / 60f);
            for (int i = 0; i < 10; i++) slow.Add(1f / 15f);
            var ms = FrameRecorder.From(slow);
            Expect("5th-percentile fps with 10% slow frames", ms.fps5thPercentile, 15.0);
            Below("5th-percentile below average (10 slow frames)", ms);
            // Every frame the same: the two are equal (at, not above).
            var even = new List<float>();
            for (int i = 0; i < 200; i++) even.Add(1f / 60f);
            var me = FrameRecorder.From(even);
            Expect("steady 60 fps: average", me.fpsAverage, 60.0);
            Expect("steady 60 fps: 5th percentile", me.fps5thPercentile, 60.0);
            // A rough dive: 1000 frames between 1/90 and 1/12 s, never above the average either.
            var rng = new System.Random(7); var rough = new List<float>();
            for (int i = 0; i < 1000; i++) rough.Add(1f / (12f + (float)rng.NextDouble() * 78f));
            var mr = FrameRecorder.From(rough);
            Below("5th-percentile below average (rough dive, " + mr.fps5thPercentile + " vs " + mr.fpsAverage + ")", mr);
            // Only a few frames: the slowest one alone.
            var few = new List<float> { 1f / 60f, 1f / 60f, 1f / 30f };
            Expect("3 frames: 5th percentile is the slowest frame", FrameRecorder.From(few).fps5thPercentile, 30.0);

            // Rows as a dive leaves them, then the dive's end.
            int before = StudyLog.Waiting.Count;
            StudySession.Begin("logcheckuser");
            StudyLog.Add(StudyLog.NewRow(1, "D1-Q1", "A", true, 1, 12.3f, 80f));
            StudyLog.Add(StudyLog.NewRow(1, "D1-Q2", "C", false, 1, 20f, 100f));
            m.canvasWidth = 1280; m.canvasHeight = 642;
            StudyLog.SetDiveMeasures(1, m);
            StudyLog.Add(StudyLog.NewRow(0, "S-EFFORT", "5", false, 1, 3f, 0f));   // survey: not measured
            var mine = new List<LogRow>();
            for (int i = before; i < StudyLog.Waiting.Count; i++) mine.Add(StudyLog.Waiting[i]);
            Expect("dive row fps_avg", mine[0].fps_avg, m.fpsAverage);
            Expect("dive row fps_p5", mine[0].fps_p5, 20.0);
            Expect("dive row canvas_w", mine[1].canvas_w, 1280);
            Expect("dive row canvas_h", mine[1].canvas_h, 642);
            Expect("survey row fps_avg (not measured)", mine[2].fps_avg, 0);

            var batch = new LogBatch { token = GameConfig.LogToken, rows = mine };
            File.WriteAllText(Path.Combine(root, "Builds/log_check.json"), JsonUtility.ToJson(batch, true));
            StudyLog.Sent(mine);   // leave nothing waiting in this editor's storage
            StudySession.End();

            report.Insert(0, ok ? "PASSED" : "FAILED");
            Directory.CreateDirectory(Path.Combine(root, "Builds"));
            File.WriteAllLines(Path.Combine(root, "Builds/log_check.txt"), report);
            Debug.Log("Log measures check: " + (ok ? "passed" : "FAILED"));
        }
    }
}
