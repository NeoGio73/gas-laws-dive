using UnityEngine;

// Which dives a student has finished, kept in this browser so they can come back later.
// Filed under the study ID, never the username. Browser storage can be blocked or cleared, so
// every read and write is guarded and the game works without it (the student then starts again).
namespace DiveGame
{
    public static class Progress
    {
        static string Key(string studyId) => "dive.done." + studyId;

        public static int DoneMask(string studyId)
        {
            try { return PlayerPrefs.GetInt(Key(studyId), 0); } catch { return 0; }
        }

        public static bool IsDone(string studyId, int dive) => (DoneMask(studyId) & (1 << dive)) != 0;

        public static int CountDone(string studyId)
        {
            int n = 0;
            foreach (int d in GameConfig.Dives) if (IsDone(studyId, d)) n++;
            return n;
        }

        // The first dive of this build not yet finished, or 0 if all are done.
        public static int NextDive(string studyId)
        {
            foreach (int d in GameConfig.Dives) if (!IsDone(studyId, d)) return d;
            return 0;
        }

        public static void MarkDone(string studyId, int dive)
        {
            try { PlayerPrefs.SetInt(Key(studyId), DoneMask(studyId) | (1 << dive)); PlayerPrefs.Save(); } catch { }
        }

        public static void Clear(string studyId)
        {
            try { PlayerPrefs.DeleteKey(Key(studyId)); PlayerPrefs.Save(); } catch { }
        }
    }
}
