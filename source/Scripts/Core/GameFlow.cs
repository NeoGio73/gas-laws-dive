using DiveGame.Study;
using UnityEngine;
using UnityEngine.SceneManagement;

// The order of the game: Title -> each dive of this build in turn -> End (survey, then the code).
// One "Dive" scene plays whichever dive RequestedDive names.
namespace DiveGame
{
    public static class GameFlow
    {
        public const string TitleScene = "Title";
        public const string DiveScene = "Dive";
        public const string EndScene = "End";

        // The dive the Dive scene should play (0 means the end).
        public static int RequestedDive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { RequestedDive = 0; }

        public static void StartDive(int dive)
        {
            if (dive <= 0) { SceneManager.LoadScene(EndScene); return; }
            RequestedDive = dive;
            SceneManager.LoadScene(DiveScene);
        }

        // After a dive: the next one of this build that is not finished, or the end.
        public static void AfterDive(int dive)
        {
            Progress.MarkDone(StudySession.StudyId, dive);
            StartDive(Progress.NextDive(StudySession.StudyId));
        }

        // Without a confirmed username (for example after a page reload) the game starts again at the title.
        public static bool NeedsTitle()
        {
            if (StudySession.Started) return false;
            SceneManager.LoadScene(TitleScene);
            return true;
        }

        public static int DiveNumberInBuild(int dive) => System.Array.IndexOf(GameConfig.Dives, dive) + 1;
    }
}
