using DiveGame.Study;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The first screen. The student types their D2L username, checks it, and either starts or picks up
// where they left off. The version (A or B) is worked out here and never shown.
namespace DiveGame.UI
{
    public class TitleScreen : MonoBehaviour
    {

        RectTransform card;
        InputField nameBox;
        Text nameError;
        string typed = "";

        void Start()
        {
            UiKit.EnsureEventSystem();
            var canvas = UiKit.MakeCanvas("Title UI");
            var shade = UiKit.Box(canvas.transform, "Shade", new Color(0.02f, 0.07f, 0.14f, 0.35f));
            UiKit.Stretch(shade.rectTransform);
            card = UiKit.CardPanel(canvas.transform, 760);
            ShowNameEntry();
        }

        // ---------- 1. Username ----------
        void ShowNameEntry()
        {
            UiKit.Clear(card);
            UiKit.Label(card, "Gas Laws Dive", 54, UiKit.Ink, FontStyle.Bold);
            UiKit.Label(card, "Five dives off the Florida coast, one gas law on each.", 24, UiKit.Muted);
            Spacer(6);
            UiKit.Label(card, "Your " + SiteWords.Username, 26, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            nameBox = UiKit.MakeInput(card, "the name you use to sign in to " + SiteWords.Site);
            nameBox.text = typed;
            nameBox.onSubmit.AddListener(_ => OnNameEntered());
            nameError = UiKit.Label(card, "", 22, UiKit.Accent, FontStyle.Bold, TextAnchor.MiddleLeft);
            nameError.gameObject.SetActive(false);
            UiKit.Label(card,
                "The game uses your username only to set up your game and to make your completion code " +
                "at the end. It stays on this device: it is never saved or sent anywhere.",
                20, UiKit.Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
            UiKit.MakeToggle(card, "Reduce motion (no camera sway or bobbing)", PlayerChoices.ReduceMotion,
                             v => PlayerChoices.ReduceMotion = v);
            var row = UiKit.Row(card);
            UiKit.MakeButton(row, "Continue", true, OnNameEntered);
            string course = GameConfig.CourseLine(StudySession.Section);
            if (course != null) UiKit.Label(card, course, 18, UiKit.Muted);
            Focus(nameBox);
        }

        void OnNameEntered()
        {
            typed = nameBox ? nameBox.text : typed;
            if (StudyIdentity.Normalize(typed).Length == 0)
            {
                nameError.text = "Type your " + SiteWords.Username + " first.";
                nameError.gameObject.SetActive(true);
                Focus(nameBox);
                return;
            }
            ShowConfirm();
        }

        // ---------- 2. Is this right? ----------
        void ShowConfirm()
        {
            UiKit.Clear(card);
            string name = StudyIdentity.Normalize(typed);
            UiKit.Label(card, "Welcome, " + name + ".", 44, UiKit.Ink, FontStyle.Bold);
            UiKit.Label(card, "Is this right?", 32, UiKit.Ink);
            UiKit.Label(card,
                "Check the spelling carefully. Your completion code works only with this exact " + SiteWords.Username + ".",
                22, UiKit.Muted);
            var row = UiKit.Row(card);
            var no = UiKit.MakeButton(row, "No, change it", false, ShowNameEntry);
            var yes = UiKit.MakeButton(row, "Yes, that's me", true, OnConfirmed);
            Focus(yes);
        }

        void OnConfirmed()
        {
            StudySession.Begin(typed);
            if (Progress.CountDone(StudySession.StudyId) > 0) ShowResume();
            else StartDive(GameConfig.Dives[0]);
        }

        // ---------- 3. Welcome back ----------
        void ShowResume()
        {
            UiKit.Clear(card);
            string id = StudySession.StudyId;
            int done = Progress.CountDone(id), total = GameConfig.Dives.Length, next = Progress.NextDive(id);
            UiKit.Label(card, "Welcome back, " + StudySession.DisplayName + ".", 40, UiKit.Ink, FontStyle.Bold);
            UiKit.Label(card, "You have finished " + done + " of " + total + " dives on this device.", 26, UiKit.Ink);
            var row = UiKit.Row(card);
            UiKit.MakeButton(row, "Start over", false, () => { Progress.Clear(id); Survey.ClearDone(id); StartDive(GameConfig.Dives[0]); });
            var go = next > 0
                ? UiKit.MakeButton(row, "Go on to Dive " + next, true, () => StartDive(next))
                : UiKit.MakeButton(row, "Go to the end", true, () => StartDive(0));
            Focus(go);
        }

        // ---------- Start ----------
        void StartDive(int dive) => GameFlow.StartDive(dive);

        // ---------- helpers ----------
        void Spacer(float h)
        {
            var r = UiKit.Rect("Spacer", card);
            r.gameObject.AddComponent<LayoutElement>().minHeight = h;
        }

        void Focus(Selectable s)
        {
            if (!s || !EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(null);
            s.Select();
            if (s is InputField f) f.ActivateInputField();
        }
    }
}
