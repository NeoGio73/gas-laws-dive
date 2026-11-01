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
        Dot NewDot(bool fromInflator) => RandomDot(rng, inner, tempK, fromInflator);

        static Dot RandomDot(System.Random rng, Vector2 inner, float tempK, bool fromInflator)
        {
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float half = DotRadius;
            Vector2 pos = fromInflator
                ? new Vector2(-inner.x / 2 + half + 2 + Rand(0, 4), inner.y / 2 - half - 2 - Rand(0, 4))   // a little apart, so they never sit exactly on top of each other
                : new Vector2(Rand(-inner.x / 2 + half, inner.x / 2 - half), Rand(-inner.y / 2 + half, inner.y / 2 - half));
            // 2D speeds of an ideal gas follow a Rayleigh curve; scaled so the average is BaseSpeed at 300 K.
            float u = Mathf.Max(1e-4f, (float)rng.NextDouble());
            float s = Mathf.Min(2.5f, Mathf.Sqrt(-2f * Mathf.Log(u)) / 1.2533f);
            float ang = Rand(0, Mathf.PI * 2);
            return new Dot { pos = pos, vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * s * BaseSpeed * Mathf.Sqrt(tempK / 300f) };
        }

        public const float CollideAt = DotRadius;   // two dots bounce when their centres are this close
        const float MaxStep = 1f / 60f;   // long frames are split, so fast dots never pass through each other

        void Update()
        {
            if (!Application.isPlaying) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / MaxStep));
            for (int k = 0; k < steps; k++) Step(dots, inner, dt / steps, this);
            Tally(dt);
            marks.RemoveAll(m => Time.time - m.born > Mathf.Max(FlashLife, 1f));
            SetVerticesDirty();
        }

        // One step of the motion, the same in the game and in SpreadCheck:
        //   - each dot moves in a straight line;
        //   - at a wall it bounces like a mirror (elastic: the part of its velocity across the wall turns
        //     round, its speed is unchanged, and it travels the right distance back from the wall);
        //   - two dots that touch while coming together bounce elastically (equal masses: they swap
        //     the parts of their velocities along the line between them). This keeps energy and
        //     momentum, so the speeds still follow the temperature, and it keeps scrambling directions,
        //     as collisions do in a real gas. Without it a dot that started almost along a wall would
        //     crawl along that wall for minutes, which looked as if particles gather at walls.
        static void Step(List<Dot> dots, Vector2 inner, float dt, ParticleBox owner, bool collide = true, float touch = CollideAt)
        {
            float hx = inner.x / 2 - DotRadius, hy = inner.y / 2 - DotRadius;
            if (hx <= 0f || hy <= 0f) return;
            for (int i = 0; i < dots.Count; i++)
            {
                var d = dots[i];
                d.pos += d.vel * dt;
                if (d.pos.x > hx) { d.pos.x = 2 * hx - d.pos.x; d.vel.x = -Mathf.Abs(d.vel.x); if (owner) owner.Hit(1, d.pos.y / inner.y); }
                else if (d.pos.x < -hx) { d.pos.x = -2 * hx - d.pos.x; d.vel.x = Mathf.Abs(d.vel.x); if (owner) owner.Hit(0, d.pos.y / inner.y); }
                if (d.pos.y > hy) { d.pos.y = 2 * hy - d.pos.y; d.vel.y = -Mathf.Abs(d.vel.y); if (owner) owner.Hit(3, d.pos.x / inner.x); }
                else if (d.pos.y < -hy) { d.pos.y = -2 * hy - d.pos.y; d.vel.y = Mathf.Abs(d.vel.y); if (owner) owner.Hit(2, d.pos.x / inner.x); }
                d.pos.x = Mathf.Clamp(d.pos.x, -hx, hx);   // only matters for a box smaller than one step
                d.pos.y = Mathf.Clamp(d.pos.y, -hy, hy);
                dots[i] = d;
            }
            // Dots bounce when their centres come within one dot radius (half their drawn width). Using
            // the full width made crowded boxes (Dive 1 at 30 m) hold about 10% more dots along the
            // walls, as real crowded particles do; this keeps the spread even and still scrambles the
            // directions within a second or two.
            if (!collide) return;
            float touch2 = touch * touch;
            for (int i = 0; i < dots.Count; i++)
                for (int j = i + 1; j < dots.Count; j++)
                {
                    var a = dots[i]; var b = dots[j];
                    Vector2 gap = b.pos - a.pos;
                    float dist2 = gap.sqrMagnitude;
                    if (dist2 >= touch2 || dist2 < 1e-6f) continue;
                    Vector2 n = gap / Mathf.Sqrt(dist2);
                    float closing = Vector2.Dot(b.vel - a.vel, n);
                    if (closing >= 0f) continue;          // already moving apart
                    a.vel += closing * n; b.vel -= closing * n;
                    dots[i] = a; dots[j] = b;
                }
        }

        // For the checks: runs count dots in a box of the given inside size for the given time (with
        // the game's own step) and reports how evenly they spread: the share of time in each of six
        // columns and six rows, and in the band one dot wide along the walls, against an even spread.
        public static string SpreadCheck(int count, Vector2 size, float seconds, int seed = 1234, bool collide = true, float touch = CollideAt)
        {
            var rng = new System.Random(seed);
            var list = new List<Dot>();
            for (int i = 0; i < count; i++) list.Add(RandomDot(rng, size, 300f, false));
            float hx = size.x / 2 - DotRadius, hy = size.y / 2 - DotRadius;
            var cols = new double[6]; var rows = new double[6]; double band = 0, samples = 0;
            double energy0 = 0; foreach (var d in list) energy0 += d.vel.sqrMagnitude;
            int steps = Mathf.CeilToInt(seconds / MaxStep);
            var hugging = new float[count]; float longestHug = 0;   // time a dot stays within one dot width of the same wall
            var hugWall = new int[count];
            for (int k = 0; k < steps; k++)
            {
                Step(list, size, MaxStep, null, collide, touch);   // collide: false shows the old motion, for comparison
                for (int i = 0; i < count; i++)
                {
                    var p = list[i].pos; float w = 2 * DotRadius;
                    int wall = p.x > hx - w ? 1 : p.x < -hx + w ? 0 : p.y > hy - w ? 3 : p.y < -hy + w ? 2 : -1;
                    if (wall >= 0 && wall == hugWall[i]) hugging[i] += MaxStep; else hugging[i] = 0;
                    hugWall[i] = wall;
                    if (wall >= 0) longestHug = Mathf.Max(longestHug, hugging[i]);
                }
                if (k % 6 != 0) continue;
                foreach (var d in list)
                {
                    cols[Mathf.Clamp((int)((d.pos.x + hx) / (2 * hx) * 6), 0, 5)]++;
                    rows[Mathf.Clamp((int)((d.pos.y + hy) / (2 * hy) * 6), 0, 5)]++;
                    if (Mathf.Abs(d.pos.x) > hx - 2 * DotRadius || Mathf.Abs(d.pos.y) > hy - 2 * DotRadius) band++;
                    samples++;
                }
            }
            double energy1 = 0; Vector2 drift = Vector2.zero;
            foreach (var d in list) { energy1 += d.vel.sqrMagnitude; drift += d.pos; }
            double evenBand = 1 - (2 * hx - 4 * DotRadius) * (2 * hy - 4 * DotRadius) / (4.0 * hx * hy);
            string Pct(double[] a) { var s = new string[a.Length]; for (int i = 0; i < a.Length; i++) s[i] = (100 * a[i] / samples).ToString("0.0"); return string.Join(" ", s); }
            return "columns % " + Pct(cols) + " | rows % " + Pct(rows) + " (even 16.7)" +
                   " | along walls " + (100 * band / samples).ToString("0.0") + "% (even " + (100 * evenBand).ToString("0.0") + "%)" +
                   " | longest stay by one wall " + longestHug.ToString("0.0") + " s" +
                   " | energy kept " + (energy1 / energy0).ToString("0.0000") + " | centre of dots " + (drift / count).ToString("0.0");
        }

        // A running tally of where the dots are while the game plays, for the checks (cheap: one pass
        // over at most 60 dots a frame). Time is in dot-seconds; "by a wall" means within one dot width.
        double tallyTime, leftHalf, rightHalf;
        readonly double[] byWall = new double[4];        // left, right, bottom, top
        readonly int[] mostByWall = new int[4];          // most dots by each wall at one moment

        void Tally(float dt)
        {
            float hx = inner.x / 2 - DotRadius, hy = inner.y / 2 - DotRadius, w = 2 * DotRadius;
            if (hx <= 0f || hy <= 0f || dots.Count == 0) return;
            var now = new int[4];
            foreach (var d in dots)
            {
                if (d.pos.x < 0) leftHalf += dt; else rightHalf += dt;
                if (d.pos.x < -hx + w) now[0]++;
                if (d.pos.x > hx - w) now[1]++;
                if (d.pos.y < -hy + w) now[2]++;
                if (d.pos.y > hy - w) now[3]++;
            }
            for (int i = 0; i < 4; i++) { byWall[i] += now[i] * dt; mostByWall[i] = Mathf.Max(mostByWall[i], now[i]); }
            tallyTime += dt;
        }

        public void ResetTally()
        {
            tallyTime = leftHalf = rightHalf = 0;
            for (int i = 0; i < 4; i++) { byWall[i] = 0; mostByWall[i] = 0; }
        }

        public string TallyReport()
        {
            double all = leftHalf + rightHalf;
            if (all <= 0) return "no data";
            return tallyTime.ToString("0") + " s, " + DotCount + " dots: left half " + (100 * leftHalf / all).ToString("0.0") +
                   "%, right half " + (100 * rightHalf / all).ToString("0.0") +
                   "% | average dots by each wall L/R/B/T " + (byWall[0] / tallyTime).ToString("0.00") + " / " +
                   (byWall[1] / tallyTime).ToString("0.00") + " / " + (byWall[2] / tallyTime).ToString("0.00") + " / " +
                   (byWall[3] / tallyTime).ToString("0.00") + " | most at once L/R/B/T " +
                   mostByWall[0] + " / " + mostByWall[1] + " / " + mostByWall[2] + " / " + mostByWall[3];
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
