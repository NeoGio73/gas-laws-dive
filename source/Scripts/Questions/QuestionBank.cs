using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// The questions, read from StreamingAssets/questions.json so they can be edited without touching
// code (docs/QUESTIONS_README.md). Both versions read the same file, so prompts, options, order and
// feedback are word for word the same.
namespace DiveGame.Questions
{
    [Serializable]
    public class Question
    {
        public string id;          // "D1-Q2"
        public int dive;
        public string type;        // "particle" or "calculation"
        public string prompt;
        public string image;       // optional picture in StreamingAssets/question-images/ (for particle diagrams)
        public string[] options;
        public string correct;     // "A", "B", ...
        public string[] feedback;  // one line per option, word for word the same in both versions

        public int CorrectIndex => string.IsNullOrEmpty(correct) ? -1 : char.ToUpperInvariant(correct.Trim()[0]) - 'A';
        public static string Letter(int i) => ((char)('A' + i)).ToString();
    }

    [Serializable]
    class QuestionFile { public Question[] questions; }

    public static class QuestionBank
    {
        public static bool Loaded { get; private set; }
        public static string Error { get; private set; }
        static List<Question> all = new List<Question>();

        public static List<Question> ForDive(int dive) => all.FindAll(q => q.dive == dive);

        // The editor keeps static values between Play sessions in this project; start clean each time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Loaded = false; Error = null; all = new List<Question>(); }

        // Reads the file. In a browser build StreamingAssets is fetched from the web server.
        public static IEnumerator Load()
        {
            if (Loaded) yield break;
            string path = Path.Combine(Application.streamingAssetsPath, "questions.json");
            if (!path.Contains("://")) path = "file://" + path;
            path += (path.Contains("?") ? "&" : "?") + "v=" + GameConfig.BuildVersion;   // a new build is never served an old copy
            using (var req = UnityWebRequest.Get(path))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Error = "Could not load the questions (" + req.error + ")."; yield break; }
                Parse(req.downloadHandler.text);
            }
        }

        public static void Parse(string json)
        {
            try
            {
                var file = JsonUtility.FromJson<QuestionFile>(json);
                all = new List<Question>(file?.questions ?? new Question[0]);
                foreach (var q in all)
                {
                    string why = Problem(q);
                    if (why != null) { Error = "Question " + q.id + ": " + why; Debug.LogError("Dive Game: " + Error); }
                }
                Loaded = Error == null;
            }
            catch (Exception e) { Error = "questions.json could not be read: " + e.Message; }
        }

        static string Problem(Question q)
        {
            if (string.IsNullOrEmpty(q.id) || string.IsNullOrEmpty(q.prompt)) return "missing id or prompt";
            if (q.options == null || q.options.Length < 2) return "needs at least two options";
            if (q.CorrectIndex < 0 || q.CorrectIndex >= q.options.Length) return "correct answer letter is not one of the options";
            if (q.feedback == null || q.feedback.Length != q.options.Length) return "needs one feedback line per option";
            return null;
        }
    }
}
