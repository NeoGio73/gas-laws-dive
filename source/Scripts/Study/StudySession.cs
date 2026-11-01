using System;
using UnityEngine;

// Who is playing, for this visit only. The username is held in memory and nowhere else: it is
// never saved, logged or sent (IRB plan, A9). The study ID, version, section and session ID are
// what the logs carry.
namespace DiveGame.Study
{
    public static class StudySession
    {
        static string username;   // memory only; needed again for the completion code at the end

        public static bool Started => username != null;
        public static string DisplayName => username ?? "";
        public static string StudyId { get; private set; } = "";
        public static GameVersion Version { get; private set; } = GameVersion.B;
        public static string SessionId { get; private set; } = NewSessionId();
        static string NewSessionId() => Guid.NewGuid().ToString("N").Substring(0, 12);

        // The editor keeps static values between Play sessions in this project; start clean each time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { End(); section = null; SessionId = NewSessionId(); }
        public static string Platform => Application.isMobilePlatform ? "mobile" : "desktop";

        // The section comes from the D2L link (?s=F1). Letters and digits only, at most 10.
        public static string Section
        {
            get
            {
                if (section == null) section = ReadSection(Application.absoluteURL);
                return section;
            }
        }
        static string section;

        public static string ReadSection(string url)
        {
            if (string.IsNullOrEmpty(url)) return "none";
            int q = url.IndexOf('?');
            if (q < 0) return "none";
            string query = url.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0 || pair.Substring(0, eq) != "s") continue;
                string v = Uri.UnescapeDataString(pair.Substring(eq + 1)).ToUpperInvariant();
                var sb = new System.Text.StringBuilder();
                foreach (char c in v) if (char.IsLetterOrDigit(c) && c < 128 && sb.Length < 10) sb.Append(c);
                return sb.Length > 0 ? sb.ToString() : "none";
            }
            return "none";
        }

        // Called once the student has confirmed their username.
        public static void Begin(string typedUsername)
        {
            username = StudyIdentity.Normalize(typedUsername);
            StudyId = StudyIdentity.StudyId(username);
            Version = StudyIdentity.Version(username);
        }

        public static void End() { username = null; StudyId = ""; }

        public static string CompletionCode() =>
            Started ? StudyIdentity.CompletionCode(username, LocalDate.Today()) : "";
    }
}
