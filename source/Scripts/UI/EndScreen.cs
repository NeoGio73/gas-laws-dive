using System.Collections.Generic;
using System.Runtime.InteropServices;
using DiveGame.Study;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The end of the game: the short survey (IRB Part C3), then the completion code.
// Both look exactly the same in both versions, and neither says which version was played.
namespace DiveGame.UI
{
    public static class Survey
    {
        static string Key(string id) => "dive.survey." + id;
        public static bool Done(string id) { try { return PlayerPrefs.GetInt(Key(id), 0) == 1; } catch { return false; } }
        public static void MarkDone(string id) { try { PlayerPrefs.SetInt(Key(id), 1); PlayerPrefs.Save(); } catch { } }
        public static void ClearDone(string id) { try { PlayerPrefs.DeleteKey(Key(id)); PlayerPrefs.Save(); } catch { } }
    }

    public class EndScreen : MonoBehaviour
    {
        // Paas (1992), used as published.
        static readonly string[] EffortLabels =
        {
            "very, very low mental effort", "very low mental effort", "low mental effort", "rather low mental effort",
            "neither low nor high mental effort", "rather high mental effort", "high mental effort",
            "very high mental effort", "very, very high mental effort",
        };
        static readonly string[] RatingItems =
        {
            "I enjoyed playing this game.",
            "The dives held my attention.",
            "I would like more games like this in this course.",
            "The questions in the game were difficult.",
        };

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DiveCopy(string text);
#else
        static void DiveCopy(string text) { GUIUtility.systemCopyBuffer = text; }
#endif

        RectTransform card;
        Text sendStatus;
        int effort;
        readonly int[] ratings = new int[4];
        float pageShown;
        public string Page { get; private set; } = "";
        public string CodeShown { get; private set; } = "";

        void Start()
        {
            if (GameFlow.NeedsTitle()) return;
            UiKit.EnsureEventSystem();
            var canvas = UiKit.MakeCanvas("End UI");
            var area = UiKit.Rect("Area", canvas.transform); UiKit.Stretch(area);
            card = UiKit.CardPanel(area, 860);
            DiveClock.Start(0);
            if (Survey.Done(StudySession.StudyId)) ShowCode(); else ShowEffort();
        }

        // ---------- Part 1: mental effort ----------
        void ShowEffort()
        {
            Begin("effort");
            UiKit.Label(card, "Before your code: a few short questions about the game (about 3 minutes). " +
                              "You may skip any of them; your code does not depend on your answers.", 22, UiKit.Muted, FontStyle.Bold);
            UiKit.Label(card, "How much mental effort did you invest in this game?", 30, UiKit.Ink, FontStyle.Bold);
            var chosen = Scale(9, true, "1 = very, very low", "9 = very, very high", v => effort = v,
                               v => "You chose " + v + ": " + EffortLabels[v - 1] + ".");
            Next("Next", () =>
            {
                LogChoice("S-EFFORT", effort);
                ShowRatings();
            }, chosen);
        }

        // ---------- Part 2: four ratings ----------
        void ShowRatings()
        {
            Begin("ratings");
            compact = true;
            var col = card.GetComponent<VerticalLayoutGroup>();
            col.spacing = 6; col.padding = new RectOffset(48, 48, 22, 22);
            UiKit.Label(card, "How much do you agree? 1 = strongly disagree, 5 = strongly agree", 22, UiKit.Muted, FontStyle.Bold);
            Selectable first = null;
            for (int i = 0; i < RatingItems.Length; i++)
            {
                int k = i;
                var itemLabel = UiKit.Label(card, RatingItems[i], 22, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
                if (i > 0) itemLabel.gameObject.AddComponent<LayoutElement>().minHeight = 40;   // a little air above each item
                var s = Scale(5, false, null, null, v => ratings[k] = v, v => "You chose " + v + " of 5.");
                if (first == null) first = s;
            }
            Next("Next", () =>
            {
                for (int i = 0; i < ratings.Length; i++) LogChoice("S-R" + (i + 1), ratings[i]);
                ShowOpen();
            }, first);
        }

        // ---------- Part 3: two optional questions ----------
        void ShowOpen()
        {
            Begin("open");
            UiKit.Label(card, "Two last questions. Both are optional. Please do not type your name.", 22, UiKit.Muted, FontStyle.Bold);
            UiKit.Label(card, "What, if anything, did the game help you understand about gases?", 24, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            var o1 = UiKit.MakeInput(card, "optional", 1000, 3);
            UiKit.Label(card, "What was confusing, frustrating, or not useful?", 24, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            var o2 = UiKit.MakeInput(card, "optional", 1000, 3);
            Next("Finish and get my code", () =>
            {
                if (o1.text.Trim().Length > 0) Log("S-O1", o1.text.Trim());
                if (o2.text.Trim().Length > 0) Log("S-O2", o2.text.Trim());
                Survey.MarkDone(StudySession.StudyId);
                StudySender.Flush();
                ShowCode();
            }, o1);
        }

        // ---------- The completion code ----------
        void ShowCode()
        {
            Begin("code");
            CodeShown = StudySession.CompletionCode();
            UiKit.Label(card, "You finished the Gas Laws Dive!", 40, UiKit.Ink, FontStyle.Bold);
            UiKit.Label(card, "Your completion code", 24, UiKit.Muted, FontStyle.Bold);
            var code = UiKit.Box(card, "Code", UiKit.FieldBg);
            code.gameObject.AddComponent<LayoutElement>().minHeight = 96;
            var codeText = UiKit.Label(code.transform, CodeShown, 60, UiKit.FieldInk, FontStyle.Bold);
            UiKit.Stretch(codeText.rectTransform);
            UiKit.Label(card, "for the " + SiteWords.Username + " " + StudySession.DisplayName, 22, UiKit.Muted);
            UiKit.Label(card,
                "To get your extra credit, open " + SiteWords.Assignment + " and type in your " + SiteWords.Username +
                " and this code. The code works only with your username.", 22, UiKit.Ink, FontStyle.Normal, TextAnchor.MiddleLeft);
            var row = UiKit.Row(card);
            Text copied = null;
            var copy = UiKit.MakeButton(row, "Copy the code", true, () =>
            {
                try { DiveCopy(CodeShown); copied.text = "Copied. You can paste it into " + SiteWords.Site + "."; }
                catch { copied.text = "Copy did not work here: please write the code down."; }
            });
            UiKit.MakeButton(row, "Credits", false, ShowCredits, 160);
            copied = UiKit.Label(card, "", 20, UiKit.Muted);
            sendStatus = UiKit.Label(card, "", 20, UiKit.Muted);
            EventSystem.current?.SetSelectedGameObject(copy.gameObject);
        }

        // Every creator whose work is in the game (handoff rule 5; ASSET_LICENSES.md has the detail).
        void ShowCredits()
        {
            Begin("credits");
            UiKit.Label(card, "Credits", 36, UiKit.Ink, FontStyle.Bold);
            UiKit.Label(card,
                "Game and questions: Prof. John Picione, Daytona State College.\n" +
                "Sam the dive buddy: \"Adventurer\" by Quaternius (CC0), with scuba gear made for this game; his kick from \"Swimming\" by Mixamo (Adobe).\n\n" +
                "Fish (lionfish, puffer, butterflyfish, flatfish, school fish): Quaternius, from Poly Pizza (CC0).\n" +
                "Low Poly Sea Turtle and Low Poly Stingray: chroma 3D, from BlenderKit.\n" +
                "Water and underwater light: Stylized Water 3 and its Underwater Rendering, by Staggart Creations.\n" +
                "Reef corals: from the Underwater Rendering package. Sand, rocks, sea rods and sea plumes: made for this game.\n" +
                "Made with Unity.",
                20, UiKit.Ink, FontStyle.Normal, TextAnchor.MiddleLeft);
            var back = UiKit.MakeButton(UiKit.Row(card), "Back to my code", true, ShowCode);
            EventSystem.current?.SetSelectedGameObject(back.gameObject);
        }

        void Update()
        {
            if (sendStatus == null) return;
            string s = StudyLog.Waiting.Count == 0 ? "Your answers are saved."
                     : StudySender.Sending ? "Saving your answers..."
                     : "Some answers are not sent yet. Please keep this page open for a minute.";
            if (sendStatus.text != s) sendStatus.text = s;
        }

        // ---------- helpers ----------
        bool compact;   // the ratings page: tighter, so all four items fit on a 1280x720 screen

        void Begin(string page)
        {
            Page = page;
            compact = false;
            var col = card.GetComponent<VerticalLayoutGroup>();
            col.spacing = 18; col.padding = new RectOffset(48, 48, 40, 40);   // the usual card spacing
            UiKit.Clear(card);
            sendStatus = null;
            pageShown = Time.realtimeSinceStartup;
        }

        void Log(string id, string answer)
        {
            StudyLog.Add(StudyLog.NewRow(0, id, answer, false, 1, Time.realtimeSinceStartup - pageShown, DiveClock.Seconds));
        }

        public const int PreferNot = -1;
        public const string PreferNotText = "prefer not to answer";

        // A rating left untouched is not logged; "Prefer not to answer" is logged as such.
        void LogChoice(string id, int value)
        {
            if (value == PreferNot) Log(id, PreferNotText);
            else if (value > 0) Log(id, value.ToString());
        }

        void Next(string label, System.Action go, Selectable focus)
        {
            UiKit.MakeButton(UiKit.Row(card), label, true, go);
            if (focus) EventSystem.current?.SetSelectedGameObject(focus.gameObject);
        }

        // A row of numbered buttons and a "Prefer not to answer" button (after the numbers, or under
        // them when the row is long). The choice is shown in words under it, not only by colour.
        // Returns the first button (for the keyboard focus).
        Selectable Scale(int count, bool preferBelow, string leftLabel, string rightLabel, System.Action<int> set, System.Func<int, string> said)
        {
            var row = UiKit.Row(card, 8);
            var buttons = new List<Button>();
            var labels = new List<string>();
            var values = new List<int>();
            Text caption = null;
            void Pick(int value)
            {
                set(value);
                caption.gameObject.SetActive(true);
                caption.text = value == PreferNot ? "You chose: prefer not to answer." : said(value);
                for (int i = 0; i < buttons.Count; i++)
                {
                    bool on = values[i] == value;
                    ((Image)buttons[i].targetGraphic).color = on ? UiKit.Accent : UiKit.Secondary;
                    var t = buttons[i].GetComponentInChildren<Text>();
                    t.color = on ? UiKit.AccentInk : UiKit.Ink;
                    t.text = on ? "[" + labels[i] + "]" : labels[i];
                }
            }
            for (int v = 1; v <= count; v++)
            {
                int value = v;
                buttons.Add(UiKit.MakeButton(row, v.ToString(), false, () => Pick(value), 60));
                labels.Add(v.ToString()); values.Add(v);
            }
            var preferRow = preferBelow ? UiKit.Row(card, 8) : row;
            var prefer = UiKit.MakeButton(preferRow, "Prefer not to answer", false, () => Pick(PreferNot), 0);
            prefer.GetComponentInChildren<Text>().fontSize = 20;
            buttons.Add(prefer); labels.Add("Prefer not to answer"); values.Add(PreferNot);
            caption = UiKit.Label(card, "", 20, UiKit.Muted);
            caption.gameObject.SetActive(false);   // shown once something is chosen, so empty lines take no room
            foreach (var b in buttons) b.GetComponent<LayoutElement>().minHeight = compact ? 44 : 56;
            if (leftLabel != null)
            {
                var ends = UiKit.Row(card, 20);
                ends.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                UiKit.Label(ends, leftLabel, 18, UiKit.Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
                UiKit.Label(ends, rightLabel, 18, UiKit.Muted, FontStyle.Normal, TextAnchor.MiddleRight);
                ends.SetSiblingIndex(row.GetSiblingIndex() + 1);   // right under the numbers
            }
            return buttons[0];
        }
    }
}
