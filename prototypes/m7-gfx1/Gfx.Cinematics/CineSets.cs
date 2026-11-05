using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;

namespace Gfx.Cinematics;

/// <summary>
/// Procedural backdrops per <see cref="SetKind"/> (placeholder grade): battlefield aftermath, fallen fortress,
/// naval dock with ships. Placement is a pure function of (set, seed) through an integer hash — presentation only.
/// The integration lane would replace the flat ground with the battle terrain the war actually ended on.
/// </summary>
public static class CineSets
{
    static ulong Mix(ulong z) { unchecked { z += 0x9E3779B97F4A7C15UL; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }

    sealed class R(ulong seed)
    {
        ulong _s = seed;
        public float F() { _s = Mix(_s); return (_s >> 40) / (float)(1UL << 24); }
        public float F(float a, float b) => a + (b - a) * F();
    }

    sealed class B
    {
        public readonly List<VertexPNC> V = new(); public readonly List<int> I = new();
        public void Box(Vector3 c, Vector3 e, Color col, float yaw = 0, float pitch = 0, float roll = 0)
        {
            var tv = new List<VertexPNC>(24); var ti = new List<int>(36);
            Primitives.AddBox(tv, ti, Vector3.Zero, e, col);
            var m = Matrix.CreateFromYawPitchRoll(yaw, pitch, roll);
            int b = V.Count;
            foreach (var v in tv) V.Add(new VertexPNC(Vector3.Transform(v.Position, m) + c, Vector3.TransformNormal(v.Normal, m), col));
            foreach (var x in ti) I.Add(b + x);
        }
        public void Plane(float size, float y, Color col)
        {
            var p = Primitives.Plane(size, 40, col); int b = V.Count;
            foreach (var v in p.Vertices) V.Add(new VertexPNC(v.Position + new Vector3(0, y, 0), v.Normal, col));
            foreach (var x in p.Indices) I.Add(b + x);
        }
    }

    static Color Vary(Color c, R r, float amt = 0.15f) => Color.Lerp(c, r.F() < 0.5f ? Color.Black : Color.White, r.F() * amt);

    /// <param name="ground">false omits the field set's flat ground plane (integration: the battle terrain is the ground).</param>
    public static MeshData<VertexPNC> Build(SetKind set, ulong seed, Color playerColour, Color opponentColour, bool ground = true)
    {
        var b = new B(); var r = new R(Mix(seed ^ (ulong)set * 0x51ED27UL));
        switch (set)
        {
            case SetKind.Naval: Naval(b, r, playerColour, opponentColour); break;
            case SetKind.Fortress: Fortress(b, r); Debris(b, r, 40, playerColour, opponentColour, 3.5f, 11f); break;
            default:
                if (ground) b.Plane(260, 0, new Color(96, 104, 68));
                for (int i = 0; i < 40; i++) b.Box(new(r.F(-30, 30), 0.004f, r.F(-25, 20)), new(r.F(1, 4), 0.004f, r.F(1, 3)), Vary(new Color(84, 70, 52), r), r.F(0, 3.14f));
                Debris(b, r, 140, playerColour, opponentColour, 3.5f, 32f);
                Trees(b, r);
                break;
        }
        return new MeshData<VertexPNC>(b.V.ToArray(), b.I.ToArray());
    }

    /// <summary>Broken spears, dropped shields, stakes: the aftermath field (kept clear of the staging circle).</summary>
    static void Debris(B b, R r, int n, Color pc, Color oc, float clear, float radius)
    {
        for (int i = 0; i < n; i++)
        {
            float a = r.F(0, MathHelper.TwoPi), d = r.F(clear, radius);
            var p = new Vector3(MathF.Cos(a) * d, 0, MathF.Sin(a) * d * 0.8f - 1.5f);
            switch (i % 4)
            {
                case 0: case 1: // spear shaft
                    float len = r.F(0.5f, 1.3f);
                    b.Box(p + new Vector3(0, 0.03f, 0), new(0.018f, 0.018f, len), Vary(new Color(110, 84, 56), r), r.F(0, 3.14f), r.F(-0.08f, 0.08f));
                    break;
                case 2: // shield
                    b.Box(p + new Vector3(0, 0.03f, 0), new(0.28f, 0.025f, 0.36f), Vary(r.F() < 0.5f ? pc : oc, r, 0.3f), r.F(0, 3.14f), r.F(-0.25f, 0.25f), r.F(-0.2f, 0.2f));
                    break;
                default: // stake / broken standard
                    b.Box(p + new Vector3(0, 0.5f, 0), new(0.04f, r.F(0.4f, 0.8f), 0.04f), Vary(new Color(80, 62, 44), r), r.F(0, 3.14f), r.F(-0.5f, 0.5f), r.F(-0.5f, 0.5f));
                    break;
            }
        }
    }

    static void Trees(B b, R r)
    {
        for (int i = 0; i < 90; i++)
        {
            float a = r.F(0, MathHelper.TwoPi), d = r.F(45, 85);
            var p = new Vector3(MathF.Cos(a) * d, 0, MathF.Sin(a) * d);
            float h = r.F(6, 12);
            b.Box(p + new Vector3(0, h * 0.25f, 0), new(0.25f, h * 0.25f, 0.25f), new Color(70, 52, 36));
            b.Box(p + new Vector3(0, h * 0.65f, 0), new(r.F(1.6f, 2.6f), h * 0.35f, r.F(1.6f, 2.6f)), Vary(new Color(46, 70, 40), r), r.F(0, 3.14f));
        }
    }

    static void Fortress(B b, R r)
    {
        b.Plane(260, 0, new Color(118, 106, 82));
        var stone = new Color(150, 140, 118);
        const float wallZ = -13f;
        for (int s = -10; s <= 10; s++)
        {
            float x = s * 3f;
            bool gate = s == 0; bool breach = !gate && r.F() < 0.22f;
            float h = gate ? 0 : breach ? r.F(0.6f, 2.5f) : r.F(5.5f, 7.5f);
            if (h > 0) b.Box(new(x, h / 2, wallZ), new(1.5f, h / 2, 1.3f), Vary(stone, r, 0.12f));
            if (!breach && !gate) for (int c = 0; c < 2; c++) if (r.F() < 0.75f) b.Box(new(x - 0.75f + c * 1.5f, h + 0.35f, wallZ + 1.0f), new(0.4f, 0.35f, 0.3f), Vary(stone, r, 0.12f));
            if (breach || gate)
                for (int k = 0; k < 14; k++) b.Box(new(x + r.F(-2.5f, 2.5f), r.F(0.1f, 0.4f), wallZ + r.F(1.0f, 5.5f)), new(r.F(0.2f, 0.6f), r.F(0.15f, 0.4f), r.F(0.2f, 0.6f)), Vary(stone, r, 0.2f), r.F(0, 3.14f), r.F(-0.3f, 0.3f));
        }
        foreach (float tx in new[] { -21f, -6f, 6f, 21f })
        {
            float h = r.F(6f, 12.5f);
            b.Box(new(tx, h / 2, wallZ), new(2.6f, h / 2, 2.6f), Vary(stone, r, 0.1f));
            if (h < 9) for (int k = 0; k < 10; k++) b.Box(new(tx + r.F(-3, 3), r.F(0.2f, 0.5f), wallZ + r.F(2.5f, 6)), new(r.F(0.3f, 0.7f)), Vary(stone, r, 0.2f), r.F(0, 3), r.F(0, 1));
        }
        // smoke-blackened gatehouse frame + fallen gate
        b.Box(new(0, 0.12f, wallZ + 3.5f), new(1.6f, 0.08f, 2.2f), new Color(70, 52, 36), 0.1f, 0, 0.05f);
        Trees(b, r);
    }

    static void Naval(B b, R r, Color pc, Color oc)
    {
        b.Plane(600, -0.9f, new Color(38, 66, 88));
        var wood = new Color(118, 88, 60);
        for (int k = 0; k < 12; k++) b.Box(new(0, -0.16f, -4.5f + k * 0.65f), new(8.5f, 0.12f, 0.3f), Vary(wood, r, 0.15f));
        for (int k = 0; k < 10; k++) b.Box(new(-8 + (k % 5) * 4, -0.8f, k < 5 ? -4.6f : 3.0f), new(0.18f, 0.8f, 0.18f), new Color(80, 60, 42));
        for (int i = 0; i < 5; i++)
        {
            float x = r.F(-40, 40), z = -r.F(18, 60), yaw = r.F(-0.5f, 0.5f);
            bool wreck = i == 0;
            var c = new Vector3(x, -0.3f - (wreck ? 0.9f : 0), z); float roll = wreck ? 0.45f : r.F(-0.03f, 0.03f);
            var col = i % 2 == 0 ? pc : oc;
            b.Box(c, new(1.4f, 0.9f, 6.0f), Vary(new Color(92, 64, 44), r), yaw, 0, roll);
            b.Box(c + new Vector3(0, 0.95f, 0), new(1.2f, 0.08f, 5.6f), Vary(wood, r), yaw, 0, roll);
            var m = Matrix.CreateFromYawPitchRoll(yaw, 0, roll);
            var mast = Vector3.Transform(new Vector3(0, 6.0f, 0), m) + c;
            b.Box(mast, new(0.12f, 5.5f, 0.12f), new Color(70, 52, 36), yaw, 0, roll);
            if (!wreck) b.Box(mast + Vector3.Transform(new Vector3(0, 0.8f, 0.4f), m), new(0.04f, 2.6f, 2.4f), Color.Lerp(new Color(220, 212, 190), col, 0.25f), yaw + 1.5708f, 0, roll);
        }
        for (int i = 0; i < 60; i++) b.Box(new(r.F(-30, 30), -0.86f, -r.F(6, 40)), new(r.F(0.1f, 0.6f), 0.05f, r.F(0.1f, 0.9f)), Vary(wood, r, 0.3f), r.F(0, 3.14f));
    }
}
