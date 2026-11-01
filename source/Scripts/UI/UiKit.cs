using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Shared pieces for the game's screens, made in code: a canvas, cards, text, buttons and a text
// box, with one look everywhere. High contrast; a thick ring shows which control has the keyboard;
// right and wrong are never shown by colour alone.
namespace DiveGame.UI
{
    public static class UiKit
    {
        public static readonly Color Ink = new Color32(0xF4, 0xF7, 0xFB, 0xFF);
        public static readonly Color Muted = new Color32(0xC3, 0xD1, 0xE2, 0xFF);
        public static readonly Color Card = new Color32(0x0B, 0x22, 0x40, 0xF2);
        public static readonly Color Accent = new Color32(0xF2, 0xC1, 0x4E, 0xFF);   // warm yellow
        public static readonly Color AccentInk = new Color32(0x10, 0x1C, 0x2C, 0xFF);
        public static readonly Color Line = new Color32(0x6F, 0x8F, 0xB5, 0xFF);
        public static readonly Color Secondary = new Color32(0x2A, 0x4C, 0x78, 0xFF);   // second-choice buttons
        public static readonly Color FocusColor = Color.white;                          // the keyboard ring
        public static readonly Color FieldBg = new Color32(0xF4, 0xF7, 0xFB, 0xFF);
        public static readonly Color FieldInk = new Color32(0x10, 0x1C, 0x2C, 0xFF);

        static Font font;
        public static Font Font => font ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        // An event system that reads the keyboard, mouse and touch through the new Input System.
        public static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.AddComponent<TabNavigation>();
        }

        public static Canvas MakeCanvas(string name, int order = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform r, float inset = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Box(Transform parent, string name, Color color)
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        // A centred card that grows with what is in it, its children stacked top to bottom.
        public static RectTransform CardPanel(Transform parent, float width)
        {
            var img = Box(parent, "Card", Card);
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(width, 100);
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = Line; outline.effectDistance = new Vector2(2, -2);
            var col = img.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(48, 48, 40, 40);
            col.spacing = 18;
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true; col.childControlHeight = true;
            col.childForceExpandWidth = true; col.childForceExpandHeight = false;
            var fit = img.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return r;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var r = Rect("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
            t.alignment = align; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.15f;
            return t;
        }

        public static RectTransform Row(Transform parent, float spacing = 16)
        {
            var r = Rect("Row", parent);
            var row = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing; row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            return r;
        }

        public static Button MakeButton(Transform parent, string text, bool primary, Action onClick, float minWidth = 220)
        {
            var img = Box(parent, "Button " + text, primary ? Accent : Secondary);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.selectedColor = Color.white; colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = colors;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minWidth = minWidth; le.minHeight = 56;
            var pad = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            pad.padding = new RectOffset(28, 28, 12, 12);
            pad.childAlignment = TextAnchor.MiddleCenter;
            pad.childControlWidth = true; pad.childControlHeight = true;
            var t = Label(img.transform, text, 24, primary ? AccentInk : Ink, FontStyle.Bold);
            t.raycastTarget = false;
            btn.onClick.AddListener(() => onClick());
            img.gameObject.AddComponent<FocusRing>();
            return btn;
        }

        public static InputField MakeInput(Transform parent, string placeholder, int maxChars = 64, int lines = 1)
        {
            var img = Box(parent, "Input", FieldBg);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = lines == 1 ? 60 : 24 + 30 * lines;
            var input = img.gameObject.AddComponent<InputField>();
            input.targetGraphic = img;

            var text = Label(img.transform, "", 28, FieldInk, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(18, 4); text.rectTransform.offsetMax = new Vector2(-18, -4);
            var ph = Label(img.transform, placeholder, 26, new Color32(0x5A, 0x67, 0x78, 0xFF), FontStyle.Italic, TextAnchor.MiddleLeft);
            Stretch(ph.rectTransform); ph.rectTransform.offsetMin = new Vector2(18, 4); ph.rectTransform.offsetMax = new Vector2(-18, -4);

            input.textComponent = text; input.placeholder = ph;
            input.characterLimit = maxChars;
            input.lineType = lines == 1 ? InputField.LineType.SingleLine : InputField.LineType.MultiLineNewline;
            if (lines > 1)
            {
                text.alignment = ph.alignment = TextAnchor.UpperLeft;
                text.fontSize = 22; ph.fontSize = 20;
                text.rectTransform.offsetMin = ph.rectTransform.offsetMin = new Vector2(14, 8);
                text.rectTransform.offsetMax = ph.rectTransform.offsetMax = new Vector2(-14, -8);
            }
            input.contentType = InputField.ContentType.Standard;
            input.caretColor = FieldInk; input.customCaretColor = true;
            input.caretWidth = 2;
            img.gameObject.AddComponent<FocusRing>();
            return input;
        }

        public static Toggle MakeToggle(Transform parent, string text, bool on, Action<bool> changed)
        {
            var row = Row(parent, 14);
            var box = Box(row, "Box", FieldBg);
            var ble = box.gameObject.AddComponent<LayoutElement>(); ble.minWidth = ble.preferredWidth = 32; ble.minHeight = ble.preferredHeight = 32;
            var tick = Box(box.transform, "Tick", FieldInk);
            Stretch(tick.rectTransform, 7);
            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box; toggle.graphic = tick; toggle.isOn = on;
            var label = Label(row, text, 22, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.raycastTarget = false;
            row.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);   // so the whole row takes a click
            toggle.onValueChanged.AddListener(v => changed(v));
            box.gameObject.AddComponent<FocusRing>().Watch(toggle);
            return toggle;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }

    // A thick white ring round the control that has the keyboard, so keyboard players always see
    // where they are (white shows against the dark card and against the yellow buttons).
    public class FocusRing : MonoBehaviour
    {
        Outline ring;
        Selectable watched;

        public void Watch(Selectable s) { watched = s; }

        void Awake()
        {
            ring = gameObject.AddComponent<Outline>();
            ring.effectColor = UiKit.FocusColor;
            ring.effectDistance = new Vector2(5, -5);
            ring.enabled = false;
            if (!watched) watched = GetComponent<Selectable>();
        }

        void Update()
        {
            var es = EventSystem.current;
            ring.enabled = es && watched && es.currentSelectedGameObject == watched.gameObject;
        }
    }

    // Tab and Shift+Tab move between the controls on screen, top to bottom and left to right.
    public class TabNavigation : MonoBehaviour
    {
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.tabKey.wasPressedThisFrame) return;
            var list = new List<Selectable>();
            foreach (var s in Selectable.allSelectablesArray)
                if (s.IsInteractable() && s.isActiveAndEnabled) list.Add(s);
            if (list.Count == 0) return;
            list.Sort((a, b) =>
            {
                Vector3 pa = a.transform.position, pb = b.transform.position;
                if (Mathf.Abs(pa.y - pb.y) > 8) return pb.y.CompareTo(pa.y);
                return pa.x.CompareTo(pb.x);
            });
            var current = EventSystem.current.currentSelectedGameObject;
            int i = list.FindIndex(s => s.gameObject == current);
            bool back = kb.shiftKey.isPressed;
            int next = i < 0 ? 0 : (i + (back ? -1 : 1) + list.Count) % list.Count;
            list[next].Select();
        }
    }
}
