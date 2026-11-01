using System.IO;
using DiveGame.Study;
using UnityEditor;
using UnityEngine;

// Dive Game > Check Study Codes: compares the game's C# version, study ID and completion code with
// the JavaScript reference (tools/reference_codes.mjs writes tools/private/reference_codes.json).
// Run the node script first after any change to the secrets or to StudyIdentity.cs.
namespace DiveGame.EditorTools
{
    public static class StudyCodeCheck
    {
        [System.Serializable] class Row { public string username, normalized, version, studyId, day, code; }
        [System.Serializable] class Reference { public string testA, testB; public Row[] rows; }

        [MenuItem("Dive Game/Check Study Codes")]
        public static void Run()
        {
            string path = Path.Combine(Application.dataPath, "../tools/private/reference_codes.json");
            if (!File.Exists(path)) { Debug.LogError("Dive Game: run  node tools/reference_codes.mjs  first."); return; }
            var reference = JsonUtility.FromJson<Reference>(File.ReadAllText(path));
            int bad = 0;
            foreach (var r in reference.rows)
            {
                string v = StudyIdentity.Version(r.username).ToString();
                string id = StudyIdentity.StudyId(r.username);
                string code = StudyIdentity.CompletionCode(r.username, r.day);
                bool ok = v == r.version && id == r.studyId && code == r.code && StudyIdentity.Normalize(r.username) == r.normalized;
                if (!ok) { bad++; Debug.LogError($"Dive Game code check DIFFERS for [{r.username}]: C# {v} {id} {code}, JS {r.version} {r.studyId} {r.code}"); }
            }
            if (bad == 0) Debug.Log($"Dive Game code check: all {reference.rows.Length} usernames match the JavaScript reference " +
                                    $"(test usernames: {reference.testA} = A, {reference.testB} = B).");
        }
    }
}
