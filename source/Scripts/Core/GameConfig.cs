// Settings fixed in each build. Change them here, then rebuild.
namespace DiveGame
{
    public static class GameConfig
    {
        // Logged with every row, so results can be traced to the exact build.
        public const string BuildVersion = "v0.1.0-pilot";

        // The dives in this build, in the order they are played. The pilot has Dives 1, 2 and 5;
        // the study build has all five.
        public static readonly int[] Dives = { 1, 2, 5 };

        // Where the play records go: the web app address of the Google Apps Script
        // (docs/LOGGING_SETUP.md, step 4 gives it). While empty, rows are kept in the browser, not sent.
        public const string LogEndpoint = "";

        // Sent with every batch; the Apps Script refuses batches without it. Not a secret (anyone can
        // read it in the build): it only keeps stray posts out of the Sheet.
        public const string LogToken = "c5xizv6atncbcthj";

        // After the study's exam the particle view is unlocked for everyone (IRB plan, A7 step 7):
        // set this to true in that later build.
        public const bool ParticleViewForEveryone = false;
    }
}
