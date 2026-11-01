using System.Collections;
using DiveGame.Gas;
using DiveGame.Questions;
using DiveGame.Study;
using DiveGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Plays one dive (GameFlow.RequestedDive): its story, the gas changing on the side panel as the
// dive's law says, then its questions, then sends the log rows and moves on.
// For now each dive is a stand-in with no scenery: the scene's background darkens with depth.
// Every word here is the same in both versions; only the side panel differs.
namespace DiveGame.Dives
{
    public class DiveDirector : MonoBehaviour
    {
        public static DiveDirector Current { get; private set; }
        public int Dive { get; private set; }
        public GasModel Model { get; private set; }
        public SidePanel Panel { get; private set; }
        public string Phase { get; private set; } = "";
        public static bool FastForTests;          // tests run the dives quickly and press the story buttons

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { FastForTests = false; Current = null; }   // not "Reset": Unity calls that on components

        Canvas canvas;
        RectTransform story;
        Text storyText;
        QuestionCard questions;
        Camera cam;
        bool pressed;                             // the player pressed the dive's action button
        Button actionButton;
        static readonly Color Shallow = new Color32(0x1E, 0x7F, 0xA8, 0xFF), Deep = new Color32(0x08, 0x2A, 0x45, 0xFF);

        IEnumerator Start()
        {
            if (GameFlow.NeedsTitle()) yield break;
            Current = this;
            Dive = GameFlow.RequestedDive;
            cam = Camera.main;
            UiKit.EnsureEventSystem();
            canvas = UiKit.MakeCanvas("Dive UI");
            yield return QuestionBank.Load();

            Model = NewModel(Dive);
            Panel = SidePanel.Create(canvas.transform, Model, SidePanel.ParticlesForThisPlayer);
            questions = QuestionCard.Create(canvas.transform);
            BuildStory();
            Tint(Model.depthM);

            DiveClock.Start(Dive);
            switch (Dive)
            {
                case 1: yield return Descent(); break;
                case 2: yield return Thermocline(); break;
                case 5: yield return Ascent(); break;
                default: yield return Say("Dive " + Dive + " is not built yet.", "Continue"); break;
            }

            Phase = "questions";
            ShowStory(false);
            bool done = false;
            questions.Ask(Dive, QuestionBank.ForDive(Dive), () => done = true);
            while (!done) yield return null;
            Panel.EndCompare();

            Phase = "finished";
            StudySender.Flush();   // the end of every dive (handoff Section 9)
            yield return Say("Dive " + Dive + " complete. Well done.", "Continue");
            GameFlow.AfterDive(Dive);
        }

        // ---------- the dives ----------

        public const float Dive1VestL = 5.0f;

        static GasModel NewModel(int dive)
        {
            var m = new GasModel();
            switch (dive)
            {
                case 1:   // the vest: 5.0 L at the surface (not 6.0 L: that gives 2.00 L at 20 m, the
                          // answer to D2L item 13), drawn as 30 dots so there is room to add air
                    m.title = "Air in your vest"; m.tempC = 28f; m.depthM = 0f;
                    m.moles = GasModel.MolesFor(Dive1VestL, 1f, 28f);
                    m.molesRef = m.moles * ParticleBox.StandardDots / 30f;
                    m.volumeScaleL = 5.5f;
                    break;
                case 2:   // the vest at 15 m in warm water
                    m.title = "Air in your vest"; m.tempC = DiveWater.Dive2WarmC; m.depthM = 15f;
                    m.moles = m.molesRef = GasModel.MolesFor(3.5f, GasModel.PressureAtDepth(15f), DiveWater.Dive2WarmC);
                    m.volumeScaleL = 3.8f;
                    break;
                case 5:   // the lungs at 30 m in the deep water
                    m.title = "Air in your lungs"; m.tempC = DiveWater.Dive5DeepC; m.depthM = DiveWater.MaxDepthM;
                    m.moles = m.molesRef = GasModel.MolesFor(2.0f, GasModel.PressureAtDepth(DiveWater.MaxDepthM), DiveWater.Dive5DeepC);
                    m.volumeScaleL = 4.0f;
                    break;
            }
            return m;
        }

        // Dive 1 (Boyle): sink to 30 m without adding air, then add air to stay neutral.
        IEnumerator Descent()
        {
            Phase = "intro";
            yield return Say("Dive 1: The descent.\n\nYou and your dive buddy, Sam, roll off the boat above a reef off " +
                             "Jupiter, Florida. Your vest holds 5.0 L of air. You let the water take you down to 30 m " +
                             "without adding any air. Watch the air in your vest as the pressure rises.", "Start the descent");
            Phase = "descending";
            yield return MoveDepth(0f, DiveWater.MaxDepthM, 2f, d =>
                d < 10f ? "Sinking. Every 10 m of water adds about 1 atm of pressure." :
                d < 20f ? "You passed 10 m, where the pressure is twice what it is at the surface. It keeps rising." :
                          "Deeper still. Sam signals OK; you signal back.");
            Phase = "add air";
            yield return Say("You are at 30 m (4.0 atm). The vest has shrunk to a quarter of its surface volume, " +
                             "so you are sinking faster. Add air from your tank to stay neutral.", "Add air");
            float start = Model.moles, target = start * 2f;   // twice the air: the vest back to half its surface size
            yield return Over(3f, t => Model.moles = Mathf.Lerp(start, target, t));
            yield return Say("Now you float at 30 m without sinking or rising. Next: questions about this descent.", "Continue");
        }

        // Dive 2 (Charles): stay at 15 m and swim into the cold layer.
        IEnumerator Thermocline()
        {
            Phase = "intro";
            yield return Say("Dive 2: The cold layer.\n\nYou and Sam swim along the reef at 15 m, where the water is " +
                             DiveWater.Dive2WarmC + " °C. Ahead the water shimmers: a thermocline, where cold water " +
                             "from the deep slides under the warm water. You stay at the same depth and swim in.", "Swim into the cold layer");
            Phase = "crossing";
            Panel.Compare(Model.Copy(), "Warm water");
            ShowStory(true, "Crossing into the cold water. Sam points at the thermometer on their dive computer.", null);
            float warm = DiveWater.Dive2WarmC, cold = DiveWater.Dive2ColdC;
            yield return Over(Fast(8f), t => Model.tempC = Mathf.Lerp(warm, cold, t));
            Phase = "cold";
            yield return Say("The water is now " + cold + " °C. You did not add or let out any air, and the depth did not " +
                             "change. Compare the air in your vest now with the air in warm water.", "Continue");
        }

        // Dive 5 (combined law): rise from 30 m, breathing out on the way, with a safety stop at 5 m.
        IEnumerator Ascent()
        {
            Phase = "intro";
            yield return Say("Dive 5: The ascent.\n\nTime to go up. You and Sam are at 30 m, where the water is " +
                             DiveWater.Dive5DeepC + " °C. Divers rise slowly and breathe out all the way up: never hold " +
                             "your breath on the way up. Press Breathe out when your lungs feel full.", "Start the ascent");
            Phase = "ascending";
            float v0 = Model.State().volumeL;
            float depth = DiveWater.MaxDepthM;
            pressed = false;
            actionButton = null;
            while (depth > 5f)
            {
                float v = Model.State().volumeL;
                bool full = v > v0 * 1.4f;
                if (full)
                {
                    // Safe stand-in: the diver stops rising until they breathe out.
                    if (actionButton == null) ShowStory(true, "Your lungs are filling as the air expands. Stop rising and breathe out!", "Breathe out");
                    if (FastForTests) pressed = true;
                    if (pressed || BreathKey())
                    {
                        float n = Model.moles, keep = n * v0 / v;   // let out enough to return to the starting volume
                        yield return Over(Fast(1.2f), t => Model.moles = Mathf.Lerp(n, keep, t));
                        pressed = false; actionButton = null;
                        ShowStory(true, "Good. Keep rising slowly with Sam.", null);
                    }
                }
                else
                {
                    depth = Mathf.Max(5f, depth - (FastForTests ? 6f : 1f) * Time.deltaTime);   // 1 m every second (real ascents are slower)
                    Model.depthM = depth;
                    Model.tempC = WaterAt(depth);
                    if (actionButton == null) ShowStory(true, "Rising slowly. Depth " + depth.ToString("0") + " m.", null);
                    if (BreathKey() || pressed) pressed = false;   // breathing out early is fine
                }
                Tint(depth);
                yield return null;
            }
            Phase = "safety stop";
            yield return Say("Safety stop at 5 m. You and Sam wait here so your body can adjust before the surface. " +
                             "A real safety stop lasts 3 minutes; here it is shortened.", "Start the stop");
            yield return Over(Fast(15f), t => ShowStory(true, "Safety stop: " + Mathf.CeilToInt((1 - t) * 15f) + " s left.", null));
            Phase = "surfacing";
            float from = Model.depthM;
            float v5 = Model.State().volumeL;
            yield return Over(Fast(5f), t =>
            {
                Model.depthM = Mathf.Lerp(from, 0f, t); Model.tempC = WaterAt(Model.depthM);
                // breathing normally on the way up: the lungs keep about the same volume, so air is let out
                Model.moles = GasModel.MolesFor(v5, GasModel.PressureAtDepth(Model.depthM), Model.tempC);
                Tint(Model.depthM);
            });
            yield return Say("You and Sam reach the surface together and signal the boat. Next: questions about the ascent.", "Continue");
        }

        // Dive 5's water: deep water below 15 m, warm from 10 m up, a smooth change in between.
        static float WaterAt(float depth)
        {
            float t = Mathf.InverseLerp(DiveWater.Dive5DeepBelowM, DiveWater.Dive5ShallowAboveM, depth);
            return Mathf.Lerp(DiveWater.Dive5DeepC, DiveWater.Dive5ShallowC, t);
        }

        bool BreathKey()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.bKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
        }

        // ---------- helpers ----------

        IEnumerator MoveDepth(float from, float to, float metresPerSecond, System.Func<float, string> caption)
        {
            float d = from;
            while (d < to)
            {
                d = Mathf.Min(to, d + (FastForTests ? 15f : metresPerSecond) * Time.deltaTime);
                Model.depthM = d;
                Tint(d);
                ShowStory(true, caption(d), null);
                yield return null;
            }
        }

        IEnumerator Over(float seconds, System.Action<float> step)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) { step(t / seconds); yield return null; }
            step(1f);
        }

        float Fast(float seconds) => FastForTests ? Mathf.Min(seconds, 0.5f) : seconds;

        void Tint(float depth)
        {
            if (cam) cam.backgroundColor = Color.Lerp(Shallow, Deep, depth / DiveWater.MaxDepthM);
        }

        // A message with one button; waits until it is pressed.
        IEnumerator Say(string text, string button)
        {
            pressed = false;
            ShowStory(true, text, button);
            if (FastForTests) { yield return null; pressed = true; }
            while (!pressed) yield return null;
            pressed = false;
            actionButton = null;
        }

        void BuildStory()
        {
            var area = UiKit.Rect("Story Area", canvas.transform);
            UiKit.Stretch(area);
            area.offsetMax = new Vector2(-(SidePanel.Width + 2 * SidePanel.Margin), 0);   // never over the side panel
            story = UiKit.CardPanel(area, 700);
            story.anchorMin = story.anchorMax = new Vector2(0.5f, 0f); story.pivot = new Vector2(0.5f, 0f);
            story.anchoredPosition = new Vector2(0, 30);
            storyText = UiKit.Label(story, "", 24, UiKit.Ink, FontStyle.Normal, TextAnchor.MiddleLeft);
        }

        void ShowStory(bool show, string text = "", string button = null)
        {
            story.gameObject.SetActive(show);
            if (!show) return;
            if (storyText.text == text && (button == null) == (actionButton == null)) return;
            storyText.text = text;
            for (int i = story.childCount - 1; i >= 1; i--) Destroy(story.GetChild(i).gameObject);
            actionButton = null;
            if (button != null)
            {
                actionButton = UiKit.MakeButton(UiKit.Row(story), button, true, () => pressed = true);
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(actionButton.gameObject);
            }
        }

        void OnDestroy() { if (Current == this) Current = null; }
    }
}
