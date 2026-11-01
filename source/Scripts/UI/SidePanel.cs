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
        public void EndCompare() { refModel = null; }

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
                legend.gameObject.AddComponent<LayoutElement>().minHeight = 22;
            }

            size = particles ? 22 : 34;   // Version B: the same readouts, larger
            header = BuildHeader(particles);
            depth = Row("Depth", particles);
            pressure = Row("Pressure", particles);
            volume = Row("Volume", particles);
            temperature = Row("Temperature", particles);
            o2 = Row("O2 pressure", particles);
            n2 = Row("N2 pressure", particles);
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
            caption = UiKit.Label(slot, "", 18, UiKit.Ink, FontStyle.Bold);
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
            row.gameObject.AddComponent<LayoutElement>().minHeight = 26;
            if (particles) UiKit.Label(row, "", 18, UiKit.Muted).raycastTarget = false;   // over the names
            headerBefore = UiKit.Label(row, "", 18, UiKit.Muted, FontStyle.Bold, particles ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            headerNow = UiKit.Label(row, "Now", 18, UiKit.Ink, FontStyle.Bold, particles ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
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
                UiKit.Label(cell, label, 20, UiKit.Muted, FontStyle.Normal, TextAnchor.LowerLeft).raycastTarget = false;
                var values = UiKit.Row(cell, 12);
                values.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                r.before = UiKit.Label(values, "", size, UiKit.Muted, FontStyle.Bold, TextAnchor.UpperLeft);
                r.now = UiKit.Label(values, "", size, UiKit.Ink, FontStyle.Bold, TextAnchor.UpperLeft);
                r.root = cell.gameObject;
            }
            else
            {
                var row = UiKit.Row(transform, 8);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                row.gameObject.AddComponent<LayoutElement>().minHeight = size + 10;
                UiKit.Label(row, label, size, UiKit.Muted, FontStyle.Normal, TextAnchor.MiddleLeft).raycastTarget = false;
                r.before = UiKit.Label(row, "", size, UiKit.Muted, FontStyle.Bold, TextAnchor.MiddleRight);
                r.now = UiKit.Label(row, "", size, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleRight);
                r.root = row.gameObject;
            }
            r.before.raycastTarget = r.now.raycastTarget = false;
            r.before.gameObject.SetActive(false);
            return r;
        }

        void LateUpdate()
        {
            if (model == null) return;
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
            float height = boxArea.rect.height - (Comparing ? CaptionHeight + 4 : 0);
            float maxSide = Mathf.Max(0f, Mathf.Min(width, height) - 12f);
            float boxY = Comparing ? CaptionHeight / 2 : 0;
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
