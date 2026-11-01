using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Sends the waiting log rows to the Google Sheet (handoff Section 9).
//   - Called at the end of each dive and after the survey (Flush), not on every click.
//   - Posted as JSON with Content-Type text/plain, so the browser makes no CORS preflight request,
//     which Apps Script cannot answer.
//   - In batches of up to 50. A row leaves the waiting list only once the Sheet says it has it; on a
//     failure the game waits (5 s, 10 s, 20 s, up to 60 s) and tries again for as long as it runs.
//   - The waiting rows are also handed to the page, which sends them with navigator.sendBeacon if
//     the tab is closed or hidden. A row may then arrive twice; the Sheet keeps one (row_id).
namespace DiveGame.Study
{
    public class StudySender : MonoBehaviour
    {
        public const int MaxBatch = 50;
        static StudySender instance;

        public static bool Sending { get; private set; }
        public static string LastResult { get; private set; } = "";
        public static int RowsSent { get; private set; }

        // Normally GameConfig.LogEndpoint; the test bench points it at a stand-in Sheet.
        public static string Endpoint = GameConfig.LogEndpoint;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DiveSetBackup(string url, string json);
#else
        static void DiveSetBackup(string url, string json) { }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; Sending = false; LastResult = ""; RowsSent = 0; Endpoint = GameConfig.LogEndpoint; }

        // Starts with the game, so rows left over from an earlier visit go out straight away.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => Ensure();

        static StudySender Ensure()
        {
            if (instance) return instance;
            var go = new GameObject("Study Sender");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<StudySender>();
            return instance;
        }

        public static void Flush()
        {
            var s = Ensure();
            if (!Sending) s.StartCoroutine(s.SendAll());
        }

        void Start()
        {
            StudyLog.Changed += PushBackup;
            PushBackup();
            if (StudyLog.Waiting.Count > 0) Flush();
        }

        void OnDestroy() { StudyLog.Changed -= PushBackup; }

        static void PushBackup()
        {
            if (string.IsNullOrEmpty(Endpoint)) return;
            try { DiveSetBackup(Endpoint, StudyLog.Waiting.Count > 0 ? StudyLog.WaitingJson(GameConfig.LogToken) : ""); } catch { }
        }

        IEnumerator SendAll()
        {
            Sending = true;
            int failures = 0;
            while (StudyLog.Waiting.Count > 0)
            {
                if (string.IsNullOrEmpty(Endpoint)) { LastResult = "No log address set; rows kept in the browser."; break; }
                List<LogRow> batch = StudyLog.Peek(MaxBatch);
                string body = JsonUtility.ToJson(new LogBatch { token = GameConfig.LogToken, rows = batch });
                using (var req = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)) { contentType = "text/plain;charset=utf-8" };
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.timeout = 30;
                    yield return req.SendWebRequest();
                    string reply = req.downloadHandler != null ? req.downloadHandler.text : "";
                    bool ok = req.result == UnityWebRequest.Result.Success && reply.Replace(" ", "").Contains("\"ok\":true");
                    if (ok)
                    {
                        StudyLog.Sent(batch);
                        RowsSent += batch.Count;
                        failures = 0;
                        LastResult = "Sent " + batch.Count + " rows.";
                        continue;
                    }
                    failures++;
                    LastResult = "Send failed (" + (req.error ?? reply) + "); trying again.";
                    Debug.LogWarning("Dive Game log: " + LastResult);
                }
                yield return new WaitForSecondsRealtime(Mathf.Min(60f, 5f * Mathf.Pow(2f, failures - 1)));
            }
            Sending = false;
        }
    }
}
