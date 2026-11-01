using System.Security.Cryptography;
using System.Text;

// Everything the game works out from the student's D2L username. The username is used only here,
// inside the browser: it is never saved, logged, sent or put in a web address.
//   - Version: A (particle view) or B (gauges only), from SHA-256 of StudySecret|username.
//   - Study ID: the first 10 hex characters of that same hash; the only identity in the logs.
//   - Completion code: DIV-XXXX-XXXX, from SHA-256 of CodeSecret|username|DIV|YYYY-MM-DD, the same
//     steps as the other games' codes, so the private checker page verifies it.
// Plain C# with no Unity calls, so it can be checked outside a scene.
namespace DiveGame.Study
{
    public enum GameVersion { A, B }

    public static class StudyIdentity
    {
        public const string CodePrefix = "DIV";
        const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";   // no 0/O or 1/I

        // Lowercase, no spaces, and only the part before an @ ("JDoe@students.example.edu" = "jdoe").
        // Same steps as normalizeUser in the other games and the checker page.
        public static string Normalize(string username)
        {
            if (username == null) return "";
            string u = username.Trim().ToLowerInvariant().Replace(" ", "");
            int at = u.IndexOf('@');
            if (at >= 0) u = u.Substring(0, at);
            return u;
        }

        static byte[] Sha256(string text)
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        }

        static byte[] StudyHash(string username) => Sha256(StudySecrets.StudySecret + "|" + Normalize(username));

        // Even last byte of the study hash gives A, odd gives B: about half each, and the same
        // username always gets the same version on any device.
        public static GameVersion Version(string username) =>
            (StudyHash(username)[31] & 1) == 0 ? GameVersion.A : GameVersion.B;

        public static string StudyId(string username)
        {
            byte[] h = StudyHash(username);
            var sb = new StringBuilder(10);
            for (int i = 0; i < 5; i++) sb.Append(h[i].ToString("x2"));
            return sb.ToString();
        }

        // day is the student's local date as YYYY-MM-DD (LocalDate.Today in the game).
        public static string CompletionCode(string username, string day) =>
            Code(CodePrefix, StudySecrets.CodeSecret, username, new[] { CodePrefix, day });

        // The checker's makeCode: secret|username, then |field|field..., first 40 bits as PREFIX-XXXX-XXXX.
        public static string Code(string prefix, string secret, string username, string[] fields)
        {
            string extra = fields != null && fields.Length > 0 ? "|" + string.Join("|", fields) : "";
            byte[] h = Sha256(secret + "|" + Normalize(username) + extra);
            ulong v = ((ulong)h[0] << 32) | ((ulong)h[1] << 24) | ((ulong)h[2] << 16) | ((ulong)h[3] << 8) | h[4];
            var sb = new StringBuilder(prefix).Append('-');
            for (int i = 0; i < 8; i++)
            {
                if (i == 4) sb.Append('-');
                sb.Append(Alphabet[(int)((v >> (35 - 5 * i)) & 31)]);
            }
            return sb.ToString();
        }
    }
}
