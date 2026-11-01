using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// The game's record of play (handoff Section 9; IRB Part C2): one row per answered question, every
// attempt included, plus survey rows. Rows wait here until the sender (StudySender) gets them to
// the Google Sheet at the end of each dive. Waiting rows are also kept in this browser's storage,
// so a dropped connection or a closed tab does not lose them: they go out on the next visit.
// No row ever holds the username: only the study ID.
namespace DiveGame.Study
{
    [Serializable]
    public class LogRow
    {
        public string row_id;          // session_id + "-" + a counter: lets the Sheet ignore a row sent twice
        public string study_id;
        public string section;
        public string version;
        public string build_version;
        public string session_id;
        public int dive_id;
        public string question_id;
        public string answer;
        public bool correct;
        public int attempt_number;
        public double time_on_question_s;   // tenths of a second (double, so 2.9 is written as 2.9)
        public double time_in_dive_s;
        public string platform;
        public string timestamp;
    }

    [Serializable]
    public class LogBatch { public string token; public List<LogRow> rows = new List<LogRow>(); }

    public static class StudyLog
    {
        const string StoreKey = "dive.pending";
        static List<LogRow> waiting = new List<LogRow>();
        static int counter;
        public static IReadOnlyList<LogRow> Waiting => waiting;
        public static event Action<LogRow> RowAdded;
        public static event Action Changed;   // the waiting list changed (the page's close-time backup listens)

        // The editor keeps static values between Play sessions in this project; start clean each time,
        // then pick up any rows a previous visit could not send.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { waiting = new List<LogRow>(); counter = 0; RowAdded = null; Changed = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Restore()
        {
            try
            {
                string json = PlayerPrefs.GetString(StoreKey, "");
                if (json.Length == 0) return;
                var saved = JsonUtility.FromJson<LogBatch>(json);
                if (saved?.rows != null) waiting.InsertRange(0, saved.rows);
            }
            catch { }
        }

        static void Save()
        {
            try
            {
                if (waiting.Count == 0) PlayerPrefs.DeleteKey(StoreKey);
                else PlayerPrefs.SetString(StoreKey, JsonUtility.ToJson(new LogBatch { rows = waiting }));
                PlayerPrefs.Save();
            }
            catch { }
            Changed?.Invoke();
        }

        public static LogRow NewRow(int dive, string questionId, string answer, bool correct, int attempt,
                                    float secondsOnQuestion, float secondsInDive)
        {
            return new LogRow
            {
                row_id = StudySession.SessionId + "-" + (++counter).ToString("D4"),
                study_id = StudySession.StudyId,
                section = StudySession.Section,
                version = StudySession.Version.ToString(),
                build_version = GameConfig.BuildVersion,
                session_id = StudySession.SessionId,
                dive_id = dive,
                question_id = questionId,
                answer = answer,
                correct = correct,
                attempt_number = attempt,
                time_on_question_s = Math.Round((double)secondsOnQuestion, 1),
                time_in_dive_s = Math.Round((double)secondsInDive, 1),
                platform = StudySession.Platform,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            };
        }

        public static void Add(LogRow row)
        {
            waiting.Add(row);
            Save();
            RowAdded?.Invoke(row);
        }

        // For the sender: up to max rows, oldest first. They stay waiting (and saved) until Sent.
        public static List<LogRow> Peek(int max) => waiting.GetRange(0, Mathf.Min(max, waiting.Count));

        // The Sheet has these rows: drop them.
        public static void Sent(List<LogRow> rows)
        {
            var ids = new HashSet<string>();
            foreach (var r in rows) ids.Add(r.row_id);
            waiting.RemoveAll(r => ids.Contains(r.row_id));
            Save();
        }

        public static string WaitingJson(string token) =>
            JsonUtility.ToJson(new LogBatch { token = token, rows = waiting });
    }

    // Seconds since the dive began (for time_in_dive_s). Real time, so a paused or hidden tab still counts.
    public static class DiveClock
    {
        static float started = -1;
        public static int Dive { get; private set; }
        public static void Start(int dive) { Dive = dive; started = Time.realtimeSinceStartup; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Dive = 0; started = -1; }
        public static float Seconds => started < 0 ? 0 : Time.realtimeSinceStartup - started;
    }
}
