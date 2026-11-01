using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Version A's particle view: a flat box of moving dots, drawn as one mesh. Not a full physics
// simulation, but every rule matches the gas laws (handoff Section 6):
//   - The number of dots follows the amount of air (moles); it changes only when air is added or let out.
//   - The dots' speed follows the square root of the Kelvin temperature.
//   - The box's area follows the volume, so the dots spread out or crowd together.
//   - When a dot hits a wall, a small soft mark may show there. The marks come more often at higher
//     pressure but never more than MaxFlashesPerSecond (WCAG 2.3.1: nothing flashes over 3 times a second).
//   - The dots never change size, whatever happens to the box or the temperature.
namespace DiveGame.UI
{
    public class ParticleBox : MaskableGraphic
    {
        public const float DotRadius = 5f;          // pixels at the 1280x720 reference size; never changes
        public const float BaseSpeed = 70f;         // average pixels per second at 300 K
        public const int StandardDots = 40;         // dots drawn for molesRef of air
        public const int MaxDots = 60;
        public const float MaxFlashesPerSecond = 2.8f;
        const float WallThickness = 3f;
        const float FlashLife = 0.35f;

        public Color dotColor = new Color32(0xE8, 0xF1, 0xFB, 0xFF);
        public Color wallColor = new Color32(0x9F, 0xB6, 0xD2, 0xFF);
        public Color flashColor = new Color32(0xF2, 0xC1, 0x4E, 0x80);   // soft yellow, half see-through

        struct Dot { public Vector2 pos, vel; }
        // A mark sits on a wall (0 left, 1 right, 2 bottom, 3 top) at a fraction along it, so it moves with the wall.
        struct Mark { public int wall; public float along; public float born; }

        readonly List<Dot> dots = new List<Dot>();
        readonly List<Mark> marks = new List<Mark>();
        Vector2 inner;              // the inside of the box, in local pixels
        float tempK = 300f;
        float flashRate = 0.6f;
        float lastFlash = -10f;
        System.Random rng = new System.Random(1234);
        static Texture2D disc;

        public int DotCount => dots.Count;
        public int TotalMarks { get; private set; }   // wall marks shown since the start (for the self-test)
        public Vector2 InnerSize => inner;
        public float FlashRate => flashRate;
        public float AverageSpeed
        {
            get { if (dots.Count == 0) return 0; float s = 0; foreach (var d in dots) s += d.vel.magnitude; return s / dots.Count; }
        }
        // Counted for the self-test: marks shown in the last second.
        public int MarksInLastSecond { get { int n = 0; foreach (var m in marks) if (Time.time - m.born <= 1f) n++; return n; } }

        public override Texture mainTexture => Disc;

        static Texture2D Disc
        {
            get
            {
                if (disc) return disc;
                const int n = 64;
                disc = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Particle disc" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        byte a = (byte)(Mathf.Clamp01((1f - r) * n / 2f) * 255);   // smooth edge
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                disc.SetPixels32(px); disc.Apply();
                return disc;
            }
        }

        // Called every frame by the side panel.
        //   count: dots for the amount of air; newTempK: temperature; size: inside of the box;
        //   pressureRatio: pressure over the container's normal pressure.
        public void SetGas(int count, float newTempK, Vector2 size, float pressureRatio)
        {
            // When the box changes size every dot moves with it, so they all crowd together (or spread
            // out) evenly instead of piling up against the walls.
            if (inner.x > 1f && inner.y > 1f && size != inner)
            {
                float kx = (size.x - 2 * DotRadius) / Mathf.Max(1f, inner.x - 2 * DotRadius);
                float ky = (size.y - 2 * DotRadius) / Mathf.Max(1f, inner.y - 2 * DotRadius);
                for (int i = 0; i < dots.Count; i++) { var d = dots[i]; d.pos = new Vector2(d.pos.x * kx, d.pos.y * ky); dots[i] = d; }
            }
            inner = size;
            rectTransform.sizeDelta = size + Vector2.one * (2 * WallThickness);
            count = Mathf.Clamp(count, 0, MaxDots);

            if (newTempK > 1f && Mathf.Abs(newTempK - tempK) > 0.001f)
            {
                float k = Mathf.Sqrt(newTempK / tempK);   // speed follows √T
                for (int i = 0; i < dots.Count; i++) { var d = dots[i]; d.vel *= k; dots[i] = d; }
                tempK = newTempK;
            }
            bool firstFill = dots.Count == 0;
            while (dots.Count < count) dots.Add(NewDot(!firstFill));
            while (dots.Count > count) dots.RemoveAt(rng.Next(dots.Count));

            flashRate = Mathf.Clamp(0.6f * pressureRatio, 0.2f, MaxFlashesPerSecond);
        }

        // New air comes in at the top left, where the vest's inflator would be; the first fill is spread evenly.
        Dot NewDot(bool fromInflator)
        {
            float half = DotRadius;
            Vector2 pos = fromInflator
                ? new Vector2(-inner.x / 2 + half + 2, inner.y / 2 - half - 2)
                : new Vector2(Rand(-inner.x / 2 + half, inner.x / 2 - half), Rand(-inner.y / 2 + half, inner.y / 2 - half));
            // 2D speeds of an ideal gas follow a Rayleigh curve; scaled so the average is BaseSpeed at 300 K.
            float u = Mathf.Max(1e-4f, (float)rng.NextDouble());
            float s = Mathf.Min(2.5f, Mathf.Sqrt(-2f * Mathf.Log(u)) / 1.2533f);
            float ang = Rand(0, Mathf.PI * 2);
            return new Dot { pos = pos, vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * s * BaseSpeed * Mathf.Sqrt(tempK / 300f) };
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Update()
        {
            if (!Application.isPlaying) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float hx = inner.x / 2 - DotRadius, hy = inner.y / 2 - DotRadius;
            for (int i = 0; i < dots.Count; i++)
            {
                var d = dots[i];
                d.pos += d.vel * dt;
                if (d.pos.x < -hx) { d.pos.x = -hx; d.vel.x = Mathf.Abs(d.vel.x); Hit(0, d.pos.y / inner.y); }
                else if (d.pos.x > hx) { d.pos.x = hx; d.vel.x = -Mathf.Abs(d.vel.x); Hit(1, d.pos.y / inner.y); }
                if (d.pos.y < -hy) { d.pos.y = -hy; d.vel.y = Mathf.Abs(d.vel.y); Hit(2, d.pos.x / inner.x); }
                else if (d.pos.y > hy) { d.pos.y = hy; d.vel.y = -Mathf.Abs(d.vel.y); Hit(3, d.pos.x / inner.x); }
                dots[i] = d;
            }
            marks.RemoveAll(m => Time.time - m.born > Mathf.Max(FlashLife, 1f));
            SetVerticesDirty();
        }

        void Hit(int wall, float along)
        {
            if (Time.time - lastFlash < 1f / flashRate) return;
            lastFlash = Time.time;
            TotalMarks++;
            marks.Add(new Mark { wall = wall, along = along, born = Time.time });
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // Walls (drawn from the disc's solid middle)
            float w = inner.x / 2, h = inner.y / 2, t = WallThickness;
            Solid(vh, new Rect(-w - t, h, inner.x + 2 * t, t), wallColor);
            Solid(vh, new Rect(-w - t, -h - t, inner.x + 2 * t, t), wallColor);
            Solid(vh, new Rect(-w - t, -h, t, inner.y), wallColor);
            Solid(vh, new Rect(w, -h, t, inner.y), wallColor);
            // Soft marks where dots hit the walls: fade in and out, never on/off
            foreach (var m in marks)
            {
                float age = (Time.time - m.born) / FlashLife;
                if (age > 1) continue;
                var c = flashColor; c.a *= Mathf.Sin(age * Mathf.PI);
                bool side = m.wall < 2;
                Vector2 at = side ? new Vector2(m.wall == 0 ? -w : w, m.along * inner.y)
                                  : new Vector2(m.along * inner.x, m.wall == 2 ? -h : h);
                Vector2 half = side ? new Vector2(5f, 12f) : new Vector2(12f, 5f);
                Quad(vh, at - half, at + half, c, new Vector2(0, 0), new Vector2(1, 1));
            }
            // Dots, all the same size
            foreach (var d in dots)
                Quad(vh, d.pos - Vector2.one * DotRadius, d.pos + Vector2.one * DotRadius, dotColor, Vector2.zero, Vector2.one);
        }

        static void Solid(VertexHelper vh, Rect r, Color c) =>
            Quad(vh, r.min, r.max, c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Color c, Vector2 uvA, Vector2 uvB)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(a.x, a.y), c, new Vector2(uvA.x, uvA.y));
            vh.AddVert(new Vector3(a.x, b.y), c, new Vector2(uvA.x, uvB.y));
            vh.AddVert(new Vector3(b.x, b.y), c, new Vector2(uvB.x, uvB.y));
            vh.AddVert(new Vector3(b.x, a.y), c, new Vector2(uvB.x, uvA.y));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
