using DiveGame.Gas;
using DiveGame.Study;
using UnityEngine;
using UnityEngine.UI;

// The side panel: the one thing that differs between the two versions (handoff Section 6).
// Same size, same place and the same readouts in both. Version A adds the particle box above the
// readouts; Version B shows the same readouts larger in the same space. Nothing on it says which
// version it is.
//
// Comparison (Dive 2, his choice 2026-10-31): a dive can hold the air's earlier state (for example
// "Warm water") beside "Now". Both versions get it, each in its own form, so the versions still
// differ only in the particles: Version A shows a second particle box running at the earlier
// conditions, at the same scale, and Version B a second column of readouts. Both versions show the
// earlier readouts.
namespace DiveGame.UI
{
    public class SidePanel : MonoBehaviour
    {
        public const float Width = 380f;              // reference pixels (1280x720), both versions
        public const float Margin = 20f;
        const float CaptionHeight = 24f;

        // Version A sees particles; after the exam everyone does (GameConfig).
        public static bool ParticlesForThisPlayer =>
            GameConfig.ParticleViewForEveryone || StudySession.Version == GameVersion.A;

        public GasModel model;
        public bool ShowsParticles { get; private set; }
        public ParticleBox Box { get; private set; }        // "Now"
        public ParticleBox RefBox { get; private set; }     // the earlier state, while comparing
        public bool Comparing => refModel != null;

        class Readout { public GameObject root; public Text now, before; }

        GasModel refModel;
        string refLabel = "";
        Text title;
        Readout depth, pressure, volume, temperature, o2, n2;
        GameObject header;
        Text headerBefore, headerNow;
        RectTransform boxArea, nowSlot, refSlot;
        Text nowCaption, refCaption;
        float shownSide = -1, refShownSide = -1;
        int size;

        // Large text for small screens (UiKit.LargeText, the same switch as the question card): every
        // text that would be drawn under about 12 screen pixels goes up to LargeMin units (12.5 px at
        // 844 x 390). The panel's size and place never change, in either version. Every label fits at
        // that size in full (measured 2026-11-01); a label that ever does not gets a short form
        // (Track's shortWords), never smaller text.
        public const int LargeMin = 23;
        class Sized { public Text text; public int normal; public string normalWords, shortWords; public LayoutElement le; public float leNormal, leLarge; }
        const float TwoLines = 60f;   // room for two lines of large text (2 x 23 x 1.15, plus a little)
        readonly System.Collections.Generic.List<Sized> sized = new System.Collections.Generic.List<Sized>();
        bool large;
        float captionHeight = CaptionHeight;

        Text Track(Text t, string shortWords = null, LayoutElement le = null, float leLarge = LargeMin + 8)
        {
            sized.Add(new Sized { text = t, normal = t.fontSize, normalWords = t.text, shortWords = shortWords, le = le, leNormal = le ? le.minHeight : 0, leLarge = leLarge });
            return t;
        }

        void ApplyLarge(bool on)
        {
            large = on;
            foreach (var s in sized)
            {
                s.text.fontSize = on ? Mathf.Max(s.normal, LargeMin) : s.normal;
                if (s.shortWords != null) s.text.text = on ? s.shortWords : s.normalWords;
                if (s.le) s.le.minHeight = on ? Mathf.Max(s.leNormal, s.leLarge) : s.leNormal;
            }
            captionHeight = on ? LargeMin + 6 : CaptionHeight;
            if (nowCaption) nowCaption.rectTransform.sizeDelta = refCaption.rectTransform.sizeDelta = new Vector2(0, captionHeight);
            shownSide = refShownSide = -1;
        }

        public static SidePanel Create(Transform canvas, GasModel model, bool particles)
        {
            var bg = UiKit.Box(canvas, "Side Panel", UiKit.Card);
            var r = bg.rectTransform;
            r.anchorMin = new Vector2(1, 0); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 0.5f);
            r.sizeDelta = new Vector2(Width, -2 * Margin);
            r.anchoredPosition = new Vector2(-Margin, 0);
            var line = bg.gameObject.AddComponent<Outline>(); line.effectColor = UiKit.Line; line.effectDistance = new Vector2(2, -2);
            var panel = bg.gameObject.AddComponent<SidePanel>();
            panel.model = model;
            panel.Build(particles);
            return panel;
        }

        // Hold an earlier state of the air beside "Now". reference is a model that no longer changes.
        public void Compare(GasModel reference, string label) { refModel = reference; refLabel = label; refShownSide = -1; }
        public void EndCompare() { refModel = null; SetNote(null); }

        // A sentence under the comparison, the same words in both versions (null hides it).
        public void SetNote(string text)
        {
            if (!note) return;
            note.text = text ?? "";
            note.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
        Text note;
        LayoutElement noteLe;   // holds the note's full height, so the boxes (not the words) give way
        Text legendText;        // Version A's "Each dot stands for..." line

        void Build(bool particles)
        {
            ShowsParticles = particles;
            var col = gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(24, 24, 20, 24);
            col.spacing = particles ? 8 : 6;
            col.childControlWidth = true; col.childControlHeight = true;
            col.childForceExpandWidth = true; col.childForceExpandHeight = false;

            title = UiKit.Label(transform, "", particles ? 26 : 30, UiKit.Ink, FontStyle.Bold);
            title.gameObject.AddComponent<LayoutElement>().minHeight = 40;

            if (particles)
            {
                boxArea = UiKit.Rect("Particle Area", transform);
                var le = boxArea.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 200; le.preferredHeight = 330; le.flexibleHeight = 1;
                refSlot = Slot("Earlier", out var rb, out refCaption); RefBox = rb;
                nowSlot = Slot("Now", out var nb, out nowCaption); Box = nb;
                var legend = UiKit.Label(transform, "Each dot stands for a huge number of air particles.", 16, UiKit.Muted);
                var legendLe = legend.gameObject.AddComponent<LayoutElement>(); legendLe.minHeight = 22;
                Track(legend, null, legendLe, TwoLines);   // two lines in large text
                legendText = legend;
            }

            size = particles ? 22 : 34;   // Version B: the same readouts, larger
            header = BuildHeader(particles);
            depth = Row("Depth", particles);
            pressure = Row("Pressure", particles);
            volume = Row("Volume", particles);
            temperature = Row("Temperature", particles);
            o2 = Row("O2 pressure", particles);
            n2 = Row("N2 pressure", particles);

            // The note under the comparison: same size, colour and place in both versions.
            note = UiKit.Label(transform, "", 17, UiKit.Ink, FontStyle.Normal, TextAnchor.UpperLeft);
            note.raycastTarget = false;
            noteLe = note.gameObject.AddComponent<LayoutElement>();
            Track(note);
            note.gameObject.SetActive(false);
        }

        RectTransform Slot(string name, out ParticleBox box, out Text caption)
        {
            var slot = UiKit.Rect(name + " Slot", boxArea);
            var boxRect = UiKit.Rect(name + " Box", slot);
            boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.anchoredPosition = new Vector2(0, CaptionHeight / 2);
            boxRect.gameObject.AddComponent<CanvasRenderer>();
            box = boxRect.gameObject.AddComponent<ParticleBox>();
            box.raycastTarget = false;
            caption = Track(UiKit.Label(slot, "", 18, UiKit.Ink, FontStyle.Bold));
            var cr = caption.rectTransform;
            cr.anchorMin = new Vector2(0, 0.5f); cr.anchorMax = new Vector2(1, 0.5f); cr.pivot = new Vector2(0.5f, 1f);
            cr.sizeDelta = new Vector2(0, CaptionHeight);   // placed under its box in DrawBoxes
            return slot;
        }

        // Column titles, shown only while comparing: "Warm water" and "Now".
        GameObject BuildHeader(bool particles)
        {
            var row = UiKit.Row(transform, particles ? 8 : 12);   // same spacing as the readouts below
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            var rowLe = row.gameObject.AddComponent<LayoutElement>(); rowLe.minHeight = 26;
            if (particles) UiKit.Label(row, "", 18, UiKit.Muted).raycastTarget = false;   // over the names
            headerBefore = Track(UiKit.Label(row, "", 18, UiKit.Muted, FontStyle.Bold, particles ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft), null, rowLe);
            headerNow = Track(UiKit.Label(row, "Now", 18, UiKit.Ink, FontStyle.Bold, particles ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft));
            headerBefore.raycastTarget = headerNow.raycastTarget = false;
            row.gameObject.SetActive(false);
            return row.gameObject;
        }

        Readout Row(string label, bool particles)
        {
            var r = new Readout();
            if (!particles)
            {
                // Version B: the name small; the values large under it, filling the panel.
                var cell = UiKit.Rect("Readout " + label, transform);
                var v = cell.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 0; v.childControlWidth = true; v.childControlHeight = true;
                v.childForceExpandWidth = true; v.childForceExpandHeight = false;
                v.childAlignment = TextAnchor.MiddleLeft;
                cell.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
                var name = UiKit.Label(cell, label, 20, UiKit.Muted, FontStyle.Normal, TextAnchor.LowerLeft);
                name.raycastTarget = false;
                Track(name);
                var values = UiKit.Row(cell, 12);
                values.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                r.before = Track(UiKit.Label(values, "", size, UiKit.Muted, FontStyle.Bold, TextAnchor.UpperLeft));
                r.now = Track(UiKit.Label(values, "", size, UiKit.Ink, FontStyle.Bold, TextAnchor.UpperLeft));
                r.root = cell.gameObject;
            }
            else
            {
                var row = UiKit.Row(transform, 8);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                var rowLe = row.gameObject.AddComponent<LayoutElement>(); rowLe.minHeight = size + 10;
                var name = UiKit.Label(row, label, size, UiKit.Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
                name.raycastTarget = false;
                // The temperature shows °C and K on two lines while comparing.
                Track(name, null, rowLe, label == "Temperature" ? TwoLines : LargeMin + 8);
                r.before = Track(UiKit.Label(row, "", size, UiKit.Muted, FontStyle.Bold, TextAnchor.MiddleRight));
                r.now = Track(UiKit.Label(row, "", size, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleRight));
                r.root = row.gameObject;
            }
            r.before.raycastTarget = r.now.raycastTarget = false;
            r.before.gameObject.SetActive(false);
            return r;
        }

        void LateUpdate()
        {
            if (model == null) return;
            bool want = UiKit.LargeText(GetComponentInParent<Canvas>());
            if (want != large) ApplyLarge(want);
            var s = model.State();
            var b = Comparing ? refModel.State() : null;

            Set(title, s.title);
            if (header.activeSelf != Comparing) header.SetActive(Comparing);
            if (Comparing) Set(headerBefore, refLabel);
            Show(depth, s, b, x => x.depthM.ToString("0.0") + " m");
            Show(pressure, s, b, x => x.pressureAtm.ToString(x.pressureAtm >= 100 ? "0" : "0.00") + " atm");
            Show(volume, s, b, x => x.volumeL.ToString("0.00") + " L");
            Show(temperature, s, b, x => Mathf.RoundToInt(x.tempC) + " °C" + (Comparing ? "\n" : "  (") +
                                         Mathf.RoundToInt(x.TempK) + " K" + (Comparing ? "" : ")"));
            bool partials = s.showPartials;
            if (o2.root.activeSelf != partials) { o2.root.SetActive(partials); n2.root.SetActive(partials); }
            if (partials)
            {
                Show(o2, s, b, x => x.ppO2.ToString("0.00") + " atm");
                Show(n2, s, b, x => x.ppN2.ToString("0.00") + " atm");
            }

            if (note && note.gameObject.activeSelf)
            {
                float h = note.preferredHeight;
                if (Mathf.Abs(noteLe.minHeight - h) > 0.5f) noteLe.minHeight = h;
            }
            // In large text the legend steps aside while the note shows, so the boxes keep their size.
            if (legendText)
            {
                bool showLegend = !(large && note && note.gameObject.activeSelf);
                if (legendText.gameObject.activeSelf != showLegend) legendText.gameObject.SetActive(showLegend);
            }
            if (boxArea)
            {
                // With the note showing, the particle boxes may shrink a little further to make room.
                var le = boxArea.GetComponent<LayoutElement>();
                float min = note && note.gameObject.activeSelf ? 120f : 200f;
                if (le.minHeight != min) le.minHeight = min;
            }

            if (Box) DrawBoxes(s, b);
        }

        void Show(Readout r, GasState now, GasState before, System.Func<GasState, string> text)
        {
            Set(r.now, text(now));
            if (r.before.gameObject.activeSelf != (before != null)) r.before.gameObject.SetActive(before != null);
            if (before != null) Set(r.before, text(before));
        }

        void DrawBoxes(GasState s, GasState b)
        {
            // Slots: the whole area for "Now" alone; left and right halves while comparing.
            if (refSlot.gameObject.activeSelf != Comparing) refSlot.gameObject.SetActive(Comparing);
            Place(refSlot, 0f, 0.5f);
            Place(nowSlot, Comparing ? 0.5f : 0f, 1f);
            nowCaption.gameObject.SetActive(Comparing);
            if (Comparing) { Set(refCaption, refLabel); Set(nowCaption, "Now"); }

            // Both boxes use the same scale, so their sizes can be compared directly.
            float width = Comparing ? boxArea.rect.width / 2f : boxArea.rect.width;
            float height = boxArea.rect.height - (Comparing ? captionHeight + 4 : 0);
            float maxSide = Mathf.Max(0f, Mathf.Min(width, height) - 12f);
            float boxY = Comparing ? captionHeight / 2 : 0;
            ((RectTransform)Box.transform).anchoredPosition = new Vector2(0, boxY);
            // Each label sits just under the largest its box can be, so the labels never move.
            var under = new Vector2(0, boxY - maxSide / 2 - 8);
            nowCaption.rectTransform.anchoredPosition = under;
            refCaption.rectTransform.anchoredPosition = under;

            Feed(Box, s, maxSide, ref shownSide);
            if (Comparing) Feed(RefBox, b, maxSide, ref refShownSide);
        }

        static void Place(RectTransform slot, float x0, float x1)
        {
            slot.anchorMin = new Vector2(x0, 0); slot.anchorMax = new Vector2(x1, 1);
            slot.offsetMin = slot.offsetMax = Vector2.zero;
        }

        static void Feed(ParticleBox box, GasState s, float maxSide, ref float shown)
        {
            // The box's area follows the volume; its side follows the square root of the volume.
            float target = maxSide * Mathf.Sqrt(Mathf.Clamp01(s.volumeL / s.volumeScaleL));
            target = Mathf.Max(target, 4 * ParticleBox.DotRadius + 6);
            shown = shown < 0 ? target : Mathf.MoveTowards(shown, target, Mathf.Max(maxSide, 50f) * 1.5f * Time.deltaTime);
            int dots = Mathf.RoundToInt(ParticleBox.StandardDots * s.moles / s.molesRef);
            box.SetGas(dots, s.TempK, Vector2.one * shown, s.pressureAtm / s.pressureRef);
        }

        static void Set(Text t, string v) { if (t.text != v) t.text = v; }
    }
}
