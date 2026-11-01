using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DiveGame.Questions;
using DiveGame.Study;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

// Asks a dive's questions one at a time, to the left of the side panel (which stays visible).
// Multiple choice, unlimited tries: a wrong choice shows its own feedback and is marked "not this
// one"; the right choice shows its feedback and a Continue button. Every attempt is recorded
// (StudyLog). The words come only from questions.json, so both versions see the same text.
// Keys: A to F or 1 to 6 pick an option; Tab moves; Enter presses the selected button.
namespace DiveGame.UI
{
    public class QuestionCard : MonoBehaviour
    {
        public bool Busy { get; private set; }

        RectTransform area, card;
        readonly List<Button> optionButtons = new List<Button>();
        readonly List<Text> optionLabels = new List<Text>();
        Text feedback;
        Button continueButton;
        Question current;
        int attempts;
        float shownAt;
        bool answered;
        int dive;
        Action next;

        public static QuestionCard Create(Transform canvas)
        {
            var area = UiKit.Rect("Question Area", canvas);
            UiKit.Stretch(area);
            area.offsetMax = new Vector2(-(SidePanel.Width + 2 * SidePanel.Margin), 0);   // never over the side panel
            var qc = area.gameObject.AddComponent<QuestionCard>();
            qc.area = area;
            area.gameObject.SetActive(false);
            return qc;
        }

        // Asks the questions in order, then calls done.
        public void Ask(int diveNumber, List<Question> questions, Action done)
        {
            dive = diveNumber;
            Busy = true;
            area.gameObject.SetActive(true);
            StartCoroutine(Run(questions, done));
        }

        IEnumerator Run(List<Question> questions, Action done)
        {
            for (int i = 0; i < questions.Count; i++)
            {
                bool moveOn = false;
                next = () => moveOn = true;
                Show(questions[i], i + 1, questions.Count);
                while (!moveOn) yield return null;
            }
            area.gameObject.SetActive(false);
            Busy = false;
            done?.Invoke();
        }

        void Show(Question q, int number, int count)
        {
            current = q; attempts = 0; answered = false; shownAt = Time.realtimeSinceStartup;
            if (card) Destroy(card.gameObject);
            optionButtons.Clear(); optionLabels.Clear();

            card = UiKit.CardPanel(area, Mathf.Min(780f, Mathf.Max(420f, area.rect.width - 40f)));
            var col = card.GetComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(36, 36, 26, 28); col.spacing = 12;

            UiKit.Label(card, "Dive " + dive + "  ·  Question " + number + " of " + count, 18, UiKit.Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiKit.Label(card, q.prompt, 24, UiKit.Ink, FontStyle.Normal, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(q.image)) StartCoroutine(LoadImage(q.image));

            for (int i = 0; i < q.options.Length; i++)
            {
                int pick = i;
                var b = UiKit.MakeButton(card, "", false, () => Choose(pick), 0);
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft; label.fontSize = 21; label.fontStyle = FontStyle.Normal;
                label.text = Question.Letter(i) + ".   " + q.options[i];
                var pad = b.GetComponent<HorizontalLayoutGroup>(); pad.padding = new RectOffset(18, 18, 10, 10);
                b.GetComponent<LayoutElement>().minHeight = 48;
                optionButtons.Add(b); optionLabels.Add(label);
            }

            feedback = UiKit.Label(card, "", 21, UiKit.Ink, FontStyle.Normal, TextAnchor.MiddleLeft);
            feedback.gameObject.SetActive(false);
            var row = UiKit.Row(card);
            continueButton = UiKit.MakeButton(row, number < count ? "Next question" : "Continue the dive", true, () => next?.Invoke());
            continueButton.gameObject.SetActive(false);
            Select(optionButtons[0]);
        }

        void Choose(int i)
        {
            if (answered || current == null || !optionButtons[i].interactable) return;
            attempts++;
            bool right = i == current.CorrectIndex;
            float onQuestion = Time.realtimeSinceStartup - shownAt;
            StudyLog.Add(StudyLog.NewRow(dive, current.id, Question.Letter(i), right, attempts, onQuestion, DiveClock.Seconds));

            feedback.text = current.feedback[i];
            feedback.gameObject.SetActive(true);
            if (right)
            {
                answered = true;
                optionLabels[i].text = Question.Letter(i) + ".   " + current.options[i] + "   (correct)";
                optionLabels[i].fontStyle = FontStyle.Bold;
                foreach (var b in optionButtons) b.interactable = false;
                var c = optionButtons[i].colors; c.disabledColor = Color.white; optionButtons[i].colors = c;   // the right answer stays bright
                ((Image)optionButtons[i].targetGraphic).color = UiKit.Accent;
                optionLabels[i].color = UiKit.AccentInk;
                continueButton.gameObject.SetActive(true);
                Select(continueButton);
            }
            else
            {
                // Marked in words, not only by colour.
                optionLabels[i].text = Question.Letter(i) + ".   " + current.options[i] + "   (not this one)";
                optionLabels[i].color = UiKit.Muted;
                optionButtons[i].interactable = false;
                var stillOpen = optionButtons.Find(b => b.interactable);
                if (stillOpen) Select(stillOpen);
            }
        }

        void Update()
        {
            if (!Busy || current == null || answered) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            for (int i = 0; i < optionButtons.Count && i < 6; i++)
            {
                var letterKey = kb[(Key)((int)Key.A + i)];
                var digitKey = kb[(Key)((int)Key.Digit1 + i)];
                if (letterKey.wasPressedThisFrame || digitKey.wasPressedThisFrame) { Choose(i); return; }
            }
        }

        IEnumerator LoadImage(string file)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "question-images", file);
            if (!path.Contains("://")) path = "file://" + path;
            using (var req = UnityWebRequestTexture.GetTexture(path))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success || !card) { Debug.LogWarning("Dive Game: no picture " + file); yield break; }
                var tex = DownloadHandlerTexture.GetContent(req);
                var r = UiKit.Rect("Picture", card);
                r.SetSiblingIndex(2);   // under the prompt
                var img = r.gameObject.AddComponent<RawImage>(); img.texture = tex;
                var fit = r.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight; fit.aspectRatio = (float)tex.width / tex.height;
                var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 200;
            }
        }

        static void Select(Selectable s)
        {
            if (!EventSystem.current || !s) return;
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }
    }
}
