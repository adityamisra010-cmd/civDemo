using Gfx.Proc.Presentation;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Environment.Structures;

/// <summary>An opening in a wall: centre along the wall (m from its start), sill height above the floor, width,
/// height, arched top (semicircular) or square; filled with a set-back plank door or shutter, or left open (dark).</summary>
public readonly record struct Opening(float At, float Sill, float Width, float Height, bool Arch = false, bool Door = false, bool Open = false);

/// <summary>
/// Parametric structure pieces fitted to the terrain (stage env; PRESENTATION ONLY). Everything is built in world
/// space from the ground function: floors sit at the highest ground under a footprint plus a step, plinths and
/// foundations reach below the lowest ground under it, so nothing floats and nothing is buried. Openings are real
/// recesses (reveals, lintels, sills, set-back doors and shutters), not dark decals.
/// </summary>
public sealed class StructureKit
{
    public StructureBuilder B { get; } = new();
    readonly Func<float, float, float> _g;
    readonly uint _seed;
    uint _n;

    public StructureKit(Func<float, float, float> ground, uint seed) { _g = ground; _seed = seed; }

    public float Ground(Vector2 p) => _g(p.X, p.Y);
    float Rnd() => PNoise.U01(PNoise.Hash(_seed * 0x9E3779B9u + _n++));
    Vector2 UvOff() => new(Rnd() * 7.3f, Rnd() * 5.1f);
    static Vector3 V3(Vector2 p, float y) => new(p.X, y, p.Y);
    static Vector2 Perp(Vector2 d) => new(-d.Y, d.X);

    /// <summary>Lowest and highest ground over a rectangle (centre, axes, half sizes), sampled every metre.</summary>
    public (float lo, float hi) GroundRange(Vector2 c, Vector2 ax, Vector2 half)
    {
        var az = Perp(ax); float lo = float.MaxValue, hi = float.MinValue;
        int nx = Math.Max(1, (int)(half.X * 2)), nz = Math.Max(1, (int)(half.Y * 2));
        for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                var p = c + ax * ((i / (float)nx * 2 - 1) * half.X) + az * ((j / (float)nz * 2 - 1) * half.Y);
                float h = Ground(p); lo = MathF.Min(lo, h); hi = MathF.Max(hi, h);
            }
        return (lo, hi);
    }

    // ------------------------------------------------------------------------------------------------ walls

    /// <summary>
    /// A straight wall from a to b (plan), from <paramref name="baseY"/> up <paramref name="height"/> m, thickness t,
    /// outer face on the left of a→b. Openings are cut through both faces with reveals; doors and shutters are set back.
    /// </summary>
    public void Wall(StMat m, Vector2 a, Vector2 b, float baseY, float height, float t, Vector3 tint, IReadOnlyList<Opening>? openings = null, bool cap = true)
    {
        var dir = Vector2.Normalize(b - a); float len = Vector2.Distance(a, b);
        var outN = -Perp(dir);                     // outer face normal (left of a→b in this convention)
        var x3 = new Vector3(dir.X, 0, dir.Y); var o3 = new Vector3(outN.X, 0, outN.Y);
        Vector3 Pt(float along, float y, float depth) => V3(a + dir * along, baseY + y) + o3 * depth; // depth: +t/2 outer face, -t/2 inner
        var uv = UvOff();
        var ops = (openings ?? Array.Empty<Opening>()).OrderBy(o => o.At).ToArray();
        // horizontal intervals between openings: full-height piers
        float x = 0;
        foreach (var op in ops)
        {
            float x0 = op.At - op.Width / 2, x1 = op.At + op.Width / 2;
            if (x0 > x) Panel(m, Pt, x, x0, 0, height, t, tint, uv);
            // below the sill and above the lintel / arch
            if (op.Sill > 0) Panel(m, Pt, x0, x1, 0, op.Sill, t, tint, uv);
            float top = op.Sill + op.Height;
            if (op.Arch)
            {
                float r = op.Width / 2, spring = top - r;
                ArchFill(m, Pt, op.At, r, spring, height, t, tint, uv);
                Reveal(m, Pt, x0, x1, op.Sill, spring, t, tint, uv, arch: r);
            }
            else
            {
                if (top < height) Panel(m, Pt, x0, x1, top, height, t, tint, uv);
                Reveal(m, Pt, x0, x1, op.Sill, top, t, tint, uv, arch: 0);
                // timber lintel showing on the outer face
                if (m is StMat.Plaster or StMat.Daub) B.Box(StMat.Wood, Pt(op.At, top + 0.09f, t / 2 - 0.05f), new Vector3(op.Width / 2 + 0.2f, 0.09f, 0.06f), x3, Vector3.One, UvOff(), 0.9f);
            }
            if (!op.Open)
            {
                // set-back infill: plank door or closed shutters, 0.12 m inside the outer face
                float setback = MathF.Min(0.14f, t * 0.4f);
                float ih = op.Arch ? op.Height - op.Width / 2 : op.Height;
                var c0 = Pt(x0, op.Sill, t / 2 - setback); var c1 = Pt(x1, op.Sill, t / 2 - setback);
                B.Quad(StMat.Planks, c0, c1, c1 + Vector3.Up * ih, c0 + Vector3.Up * ih, new Vector3(0.85f), UvOff(), 0.55f, 0.55f, 0.75f, 0.75f, new Vector2(1, -1));
                if (op.Arch) ArchInfill(Pt, op.At, op.Width / 2, op.Sill + ih, t / 2 - setback);
            }
            else
            {
                // open: a dark interior plane at the inner face
                var c0 = Pt(x0, op.Sill, -t / 2 + 0.02f); var c1 = Pt(x1, op.Sill, -t / 2 + 0.02f);
                B.Quad(StMat.Plaster, c0, c1, c1 + Vector3.Up * op.Height, c0 + Vector3.Up * op.Height, new Vector3(0.12f), UvOff(), 0.2f, 0.2f, 0.2f, 0.2f);
            }
            x = x1;
        }
        if (x < len) Panel(m, Pt, x, len, 0, height, t, tint, uv);
        if (cap) B.Quad(m, Pt(0, height, t / 2), Pt(len, height, t / 2), Pt(len, height, -t / 2), Pt(0, height, -t / 2), tint, uv);
        // ends
        B.Quad(m, Pt(0, 0, -t / 2), Pt(0, 0, t / 2), Pt(0, height, t / 2), Pt(0, height, -t / 2), tint, uv, 0.8f, 0.8f, 1, 1, new Vector2(1, -1));
        B.Quad(m, Pt(len, 0, t / 2), Pt(len, 0, -t / 2), Pt(len, height, -t / 2), Pt(len, height, t / 2), tint, uv, 0.8f, 0.8f, 1, 1, new Vector2(1, -1));
    }

    /// <summary>Wall-frame uv: u along the wall, v down (metres / repeat), so piers, fills and arches share one texture.</summary>
    static Vector2 WUv(StMat m, float along, float y, Vector2 off) { float ws = StructureBuilder.WorldSize(m); return new Vector2(along / ws, -y / ws) + off; }

    void Panel(StMat m, Func<float, float, float, Vector3> Pt, float x0, float x1, float y0, float y1, float t, Vector3 tint, Vector2 uv)
    {
        float ao0 = y0 < 0.01f ? 0.62f : 1f;
        B.QuadUv(m, Pt(x0, y0, t / 2), Pt(x1, y0, t / 2), Pt(x1, y1, t / 2), Pt(x0, y1, t / 2),
            WUv(m, x0, y0, uv), WUv(m, x1, y0, uv), WUv(m, x1, y1, uv), WUv(m, x0, y1, uv), tint, ao0, ao0, 1, 1);
        var u2 = uv + new Vector2(0.5f, 0.21f);
        B.QuadUv(m, Pt(x1, y0, -t / 2), Pt(x0, y0, -t / 2), Pt(x0, y1, -t / 2), Pt(x1, y1, -t / 2),
            WUv(m, -x1, y0, u2), WUv(m, -x0, y0, u2), WUv(m, -x0, y1, u2), WUv(m, -x1, y1, u2), tint, ao0, ao0, 0.9f, 0.9f);
    }

    /// <summary>The three inner faces of an opening (sides and head or the arch intrados) and the sill.</summary>
    void Reveal(StMat m, Func<float, float, float, Vector3> Pt, float x0, float x1, float y0, float y1, float t, Vector3 tint, Vector2 uv, float arch)
    {
        const float ao = 0.6f;
        B.Quad(m, Pt(x0, y0, t / 2), Pt(x0, y0, -t / 2), Pt(x0, y1, -t / 2), Pt(x0, y1, t / 2), tint, uv, ao, ao, ao, ao, new Vector2(1, -1));
        B.Quad(m, Pt(x1, y0, -t / 2), Pt(x1, y0, t / 2), Pt(x1, y1, t / 2), Pt(x1, y1, -t / 2), tint, uv, ao, ao, ao, ao, new Vector2(1, -1));
        if (y0 > 0.01f) B.Quad(m, Pt(x0, y0, -t / 2), Pt(x1, y0, -t / 2), Pt(x1, y0, t / 2), Pt(x0, y0, t / 2), tint, uv, ao, ao, ao, ao);
        if (arch <= 0)
            B.Quad(m, Pt(x0, y1, t / 2), Pt(x1, y1, t / 2), Pt(x1, y1, -t / 2), Pt(x0, y1, -t / 2), tint, uv, ao, ao, ao, ao);
        else
        {
            // semicircular intrados (barrel vault through the wall thickness)
            int seg = 14; float cx = (x0 + x1) / 2;
            for (int i = 0; i < seg; i++)
            {
                float a0 = MathF.PI * i / seg, a1 = MathF.PI * (i + 1) / seg;
                float px0 = cx + MathF.Cos(a0) * arch, py0 = y1 + MathF.Sin(a0) * arch, px1 = cx + MathF.Cos(a1) * arch, py1 = y1 + MathF.Sin(a1) * arch;
                B.Quad(m, Pt(px0, py0, t / 2), Pt(px1, py1, t / 2), Pt(px1, py1, -t / 2), Pt(px0, py0, -t / 2), tint, uv, ao, ao, ao * 0.9f, ao * 0.9f);
            }
        }
    }

    /// <summary>The wall face around a semicircular arch head (both faces), and a ring of voussoirs on the outer face.</summary>
    void ArchFill(StMat m, Func<float, float, float, Vector3> Pt, float cx, float r, float spring, float height, float t, Vector3 tint, Vector2 uv)
    {
        int seg = 14;
        for (int face = 0; face < 2; face++)
        {
            float d = face == 0 ? t / 2 : -t / 2;
            for (int i = 0; i < seg; i++)
            {
                float a0 = MathF.PI * i / seg, a1 = MathF.PI * (i + 1) / seg;
                float x0 = cx + MathF.Cos(a0) * r, y0 = spring + MathF.Sin(a0) * r, x1 = cx + MathF.Cos(a1) * r, y1 = spring + MathF.Sin(a1) * r;
                // from the arch edge straight up to the wall top, in the wall's own texture frame
                var u0 = face == 0 ? uv : uv + new Vector2(0.5f, 0.21f);
                float sx = face == 0 ? 1 : -1;
                if (face == 0) B.QuadUv(m, Pt(x1, y1, d), Pt(x0, y0, d), Pt(x0, height, d), Pt(x1, height, d),
                    WUv(m, x1 * sx, y1, u0), WUv(m, x0 * sx, y0, u0), WUv(m, x0 * sx, height, u0), WUv(m, x1 * sx, height, u0), tint);
                else B.QuadUv(m, Pt(x0, y0, d), Pt(x1, y1, d), Pt(x1, height, d), Pt(x0, height, d),
                    WUv(m, x0 * sx, y0, u0), WUv(m, x1 * sx, y1, u0), WUv(m, x1 * sx, height, u0), WUv(m, x0 * sx, height, u0), tint);
            }
            // the parts beside the arch between the springing line and the top are covered by the outer arch segments
        }
        // voussoirs: wedge stones standing slightly proud of the outer face
        int vs = 11;
        for (int i = 0; i < vs; i++)
        {
            float a0 = MathF.PI * i / vs + 0.004f, a1 = MathF.PI * (i + 1) / vs - 0.004f;
            float r0 = r, r1 = r + 0.55f;
            var p00 = Pt(cx + MathF.Cos(a0) * r0, spring + MathF.Sin(a0) * r0, t / 2 + 0.05f);
            var p01 = Pt(cx + MathF.Cos(a1) * r0, spring + MathF.Sin(a1) * r0, t / 2 + 0.05f);
            var p10 = Pt(cx + MathF.Cos(a0) * r1, spring + MathF.Sin(a0) * r1, t / 2 + 0.05f);
            var p11 = Pt(cx + MathF.Cos(a1) * r1, spring + MathF.Sin(a1) * r1, t / 2 + 0.05f);
            var vt = tint * (0.92f + 0.12f * Rnd());
            B.Quad(StMat.Ashlar, p01, p00, p10, p11, vt, UvOff());
        }
    }

    void ArchInfill(Func<float, float, float, Vector3> Pt, float cx, float r, float spring, float d)
    {
        int seg = 12;
        for (int i = 0; i < seg; i++)
        {
            float a0 = MathF.PI * i / seg, a1 = MathF.PI * (i + 1) / seg;
            var c = Pt(cx, spring, d);
            var p0 = Pt(cx + MathF.Cos(a0) * r, spring + MathF.Sin(a0) * r, d);
            var p1 = Pt(cx + MathF.Cos(a1) * r, spring + MathF.Sin(a1) * r, d);
            B.Quad(StMat.Planks, p1, p0, c, c, new Vector3(0.85f), UvOff(), 0.6f, 0.6f, 0.6f, 0.6f);
        }
    }

    // ------------------------------------------------------------------------------------------------ roofs

    /// <summary>
    /// A gable roof over a rectangle (centre at eaves height, axis along the ridge, half sizes), pitch in degrees,
    /// overhang, with a thick eaves edge (fascia), a ridge cap and gable end walls of <paramref name="gableMat"/>.
    /// </summary>
    public void GableRoof(StMat m, Vector3 c, Vector2 ax, Vector2 half, float pitchDeg, float overhang, StMat gableMat, Vector3 gableTint, Vector3 roofTint, float thick = 0.14f)
    {
        var az = Perp(ax);
        var x3 = new Vector3(ax.X, 0, ax.Y); var z3 = new Vector3(az.X, 0, az.Y);
        float rise = half.Y * MathF.Tan(MathHelper.ToRadians(pitchDeg));
        float hx = half.X + overhang, hz = half.Y + overhang;
        float eaveDrop = overhang * MathF.Tan(MathHelper.ToRadians(pitchDeg));
        var ridgeA = c - x3 * hx + Vector3.Up * rise; var ridgeB = c + x3 * hx + Vector3.Up * rise;
        var uv = UvOff();
        // thatch is a thick layer: its cut edges at the eaves and along the verges are straw, not a thin board
        bool thatch = m == StMat.Thatch;
        if (thatch) thick = MathF.Max(thick, 0.38f);
        var edgeMat = thatch ? StMat.Thatch : StMat.Wood;
        var edgeTint = thatch ? roofTint * 0.8f : new Vector3(0.7f);
        foreach (int s in new[] { 1, -1 })
        {
            var eA = c - x3 * hx + z3 * (s * hz) - Vector3.Up * eaveDrop; var eB = c + x3 * hx + z3 * (s * hz) - Vector3.Up * eaveDrop;
            // a→d down the slope: ridge first
            if (s == 1) B.Quad(m, ridgeB, ridgeA, eA, eB, roofTint, uv, 0.95f, 0.95f, 1, 1);
            else B.Quad(m, ridgeA, ridgeB, eB, eA, roofTint, uv + new Vector2(0.4f, 0), 0.95f, 0.95f, 1, 1);
            // eaves edge (roof thickness; thatch is trimmed with a slight outward slope), darker underside
            var dn = -Vector3.Up * thick + (thatch ? z3 * (s * 0.1f) : Vector3.Zero);
            B.Quad(edgeMat, eA + dn, eB + dn, eB, eA, edgeTint, UvOff(), 0.6f, 0.6f, 0.8f, 0.8f);
            B.Quad(StMat.Wood, eB + dn, eA + dn, c - x3 * hx + Vector3.Up * (rise - thick), c + x3 * hx + Vector3.Up * (rise - thick), new Vector3(0.45f), UvOff(), 0.35f, 0.35f, 0.5f, 0.5f);
            if (thatch)
            {
                // verges: the thatch's thickness along both gable edges of this slope
                foreach (int sx in new[] { 1, -1 })
                {
                    var r = c + x3 * (sx * hx) + Vector3.Up * rise; var e = c + x3 * (sx * hx) + z3 * (s * hz) - Vector3.Up * eaveDrop;
                    var d2 = -Vector3.Up * thick;
                    B.Quad(StMat.Thatch, r, e, e + dn, r + d2, edgeTint, UvOff(), 0.85f, 0.7f, 0.6f, 0.7f);
                }
            }
        }
        // ridge cap: a half cylinder of the roof material
        if (m == StMat.Tiles) B.Tube(StMat.Tiles, ridgeA + Vector3.Up * 0.04f, ridgeB + Vector3.Up * 0.04f, 0.13f, 0.13f, 8, roofTint * 0.92f, UvOff(), 1, 0.9f, 0.9f);
        else B.Tube(m, ridgeA + Vector3.Up * 0.05f, ridgeB + Vector3.Up * 0.05f, thatch ? 0.3f : 0.22f, thatch ? 0.3f : 0.22f, 7, roofTint * 0.85f, UvOff(), 1, 0.9f, 0.9f);
        // gable ends (triangles of wall material, set in from the overhang)
        foreach (int s in new[] { 1, -1 })
        {
            var g0 = c + x3 * (s * half.X) - z3 * half.Y; var g1 = c + x3 * (s * half.X) + z3 * half.Y; var gt = c + x3 * (s * half.X) + Vector3.Up * rise;
            int i0 = B.Vertex(gableMat, g0, x3 * s, z3, 1, new Vector2(0, 0) + UvOff(), gableTint, 0.9f);
            int i1 = B.Vertex(gableMat, g1, x3 * s, z3, 1, new Vector2(2 * half.Y / StructureBuilder.WorldSize(gableMat), 0), gableTint, 0.9f);
            int i2 = B.Vertex(gableMat, gt, x3 * s, z3, 1, new Vector2(half.Y / StructureBuilder.WorldSize(gableMat), -rise / StructureBuilder.WorldSize(gableMat)), gableTint, 0.75f);
            B.Tri(gableMat, i0, i1, i2);
        }
    }

    /// <summary>A pyramid (hip) roof over a square or rectangle, eaves at c.Y.</summary>
    public void HipRoof(StMat m, Vector3 c, Vector2 ax, Vector2 half, float pitchDeg, float overhang, Vector3 tint)
    {
        var az = Perp(ax); var x3 = new Vector3(ax.X, 0, ax.Y); var z3 = new Vector3(az.X, 0, az.Y);
        float hx = half.X + overhang, hz = half.Y + overhang;
        float rise = MathF.Min(half.X, half.Y) * MathF.Tan(MathHelper.ToRadians(pitchDeg));
        float drop = overhang * MathF.Tan(MathHelper.ToRadians(pitchDeg));
        float ridge = MathF.Max(0, half.X - half.Y);
        var r0 = c - x3 * ridge + Vector3.Up * rise; var r1 = c + x3 * ridge + Vector3.Up * rise;
        Vector3 E(float sx, float sz) => c + x3 * (sx * hx) + z3 * (sz * hz) - Vector3.Up * drop;
        var uv = UvOff();
        B.Quad(m, r1, r0, E(-1, 1), E(1, 1), tint, uv);                // long side +z
        B.Quad(m, r0, r1, E(1, -1), E(-1, -1), tint, uv + new Vector2(0.3f, 0));
        // hips: triangles at the ends
        Tri(m, r1, E(1, 1), E(1, -1), tint);
        Tri(m, r0, E(-1, -1), E(-1, 1), tint);
        var dn = -Vector3.Up * 0.14f;
        foreach (var (p, q) in new[] { (E(-1, 1), E(1, 1)), (E(1, 1), E(1, -1)), (E(1, -1), E(-1, -1)), (E(-1, -1), E(-1, 1)) })
            B.Quad(StMat.Wood, p + dn, q + dn, q, p, new Vector3(0.7f), UvOff(), 0.6f, 0.6f, 0.8f, 0.8f);
        // soffit
        B.Quad(StMat.Wood, E(1, 1) + dn, E(-1, 1) + dn, E(-1, -1) + dn, E(1, -1) + dn, new Vector3(0.4f), UvOff(), 0.35f, 0.35f, 0.35f, 0.35f);
    }

    void Tri(StMat m, Vector3 top, Vector3 a, Vector3 b, Vector3 tint)
    {
        var n = Vector3.Normalize(Vector3.Cross(a - top, b - top));
        var t = Vector3.Normalize(b - a);
        var down = Vector3.Normalize((a + b) * 0.5f - top);
        float ws = StructureBuilder.WorldSize(m);
        float sign = Vector3.Dot(Vector3.Cross(n, t), down) >= 0 ? 1 : -1;
        int i0 = B.Vertex(m, top, n, t, sign, new Vector2(0, 0), tint, 0.95f);
        int i1 = B.Vertex(m, a, n, t, sign, new Vector2(-Vector3.Distance(a, b) / (2 * ws), Vector3.Distance(top, (a + b) * 0.5f) / ws), tint, 1);
        int i2 = B.Vertex(m, b, n, t, sign, new Vector2(Vector3.Distance(a, b) / (2 * ws), Vector3.Distance(top, (a + b) * 0.5f) / ws), tint, 1);
        B.Tri(m, i0, i1, i2);
    }

    /// <summary>
    /// A thatched cone roof: outer cone from a ragged, thick eaves edge to the apex, an underside cone, and the eaves
    /// band joining them; straw runs down the slope; the apex is smoke-darkened.
    /// </summary>
    public void ConeRoof(Vector2 c, float eavesY, float radius, float pitchDeg, float thick, Vector3 tint)
    {
        int seg = 28;
        float rise = radius * MathF.Tan(MathHelper.ToRadians(pitchDeg));
        var apex = V3(c, eavesY + rise);
        float ws = StructureBuilder.WorldSize(StMat.Thatch);
        float slant = MathF.Sqrt(radius * radius + rise * rise);
        var outer = new Vector3[seg + 1]; var inner = new Vector3[seg + 1];
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * MathHelper.TwoPi;
            int k = i % seg;
            float rag = 1f + 0.035f * (PNoise.U01(PNoise.Hash(k, 3, _seed)) - 0.5f) * 2f;
            float dy = 0.06f * (PNoise.U01(PNoise.Hash(k, 7, _seed)) - 0.5f);
            var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            outer[i] = V3(c, eavesY + dy) + dir * radius * rag;
            inner[i] = V3(c, eavesY + dy + thick * 0.4f) + dir * (radius * rag - thick * 1.1f);
        }
        float circumference = MathHelper.TwoPi * radius;
        for (int i = 0; i < seg; i++)
        {
            float u0 = i / (float)seg * circumference / ws, u1 = (i + 1) / (float)seg * circumference / ws;
            var n0 = Vector3.Normalize(new Vector3(outer[i].X - c.X, 0, outer[i].Z - c.Y) * rise / radius + Vector3.Up);
            var n1 = Vector3.Normalize(new Vector3(outer[i + 1].X - c.X, 0, outer[i + 1].Z - c.Y) * rise / radius + Vector3.Up);
            var t0 = Vector3.Normalize(Vector3.Cross(Vector3.Up, n0)); var t1 = Vector3.Normalize(Vector3.Cross(Vector3.Up, n1));
            // four rings from apex to eaves (a slightly convex, bell-like profile); smoke darkening and occlusion vary down the slope
            Vector3 Prof(Vector3 eave, float f)
            {
                var p = Vector3.Lerp(apex, eave, f);
                float bulge = MathF.Sin(f * MathF.PI) * 0.045f * radius;
                var outDir = new Vector3(eave.X - c.X, 0, eave.Z - c.Y); outDir = Vector3.Normalize(outDir);
                return p + outDir * bulge + Vector3.Up * bulge * 0.6f;
            }
            for (int r = 0; r < 4; r++)
            {
                float f0 = r / 4f, f1 = (r + 1) / 4f;
                var a0 = Prof(outer[i], f0); var a1 = Prof(outer[i + 1], f0);
                var b0 = Prof(outer[i], f1); var b1 = Prof(outer[i + 1], f1);
                float s0 = 0.55f + 0.45f * MathF.Min(1, f0 * 2.2f), s1 = 0.55f + 0.45f * MathF.Min(1, f1 * 2.2f);
                int ia0 = B.Vertex(StMat.Thatch, a0, n0, t0, 1, new Vector2(u0 * MathF.Max(f0, 0.05f), f0 * slant / ws), tint * s0, 0.95f);
                int ia1 = B.Vertex(StMat.Thatch, a1, n1, t1, 1, new Vector2(u1 * MathF.Max(f0, 0.05f), f0 * slant / ws), tint * s0, 0.95f);
                int ib0 = B.Vertex(StMat.Thatch, b0, n0, t0, 1, new Vector2(u0 * f1, f1 * slant / ws), tint * s1, 1f);
                int ib1 = B.Vertex(StMat.Thatch, b1, n1, t1, 1, new Vector2(u1 * f1, f1 * slant / ws), tint * s1, 1f);
                B.Tri(StMat.Thatch, ia0, ib0, ib1); B.Tri(StMat.Thatch, ia0, ib1, ia1);
            }
            // eaves band (thickness) and underside
            B.Quad(StMat.Thatch, outer[i], outer[i + 1], inner[i + 1], inner[i], tint * 0.7f, new Vector2(u0, 0), 0.7f, 0.7f, 0.5f, 0.5f);
            var apexIn = apex - Vector3.Up * thick * 1.5f;
            B.Quad(StMat.Thatch, inner[i + 1], inner[i], apexIn, apexIn, tint * 0.35f, new Vector2(u0, 0.3f), 0.3f, 0.3f, 0.2f, 0.2f);
        }
    }

    // ------------------------------------------------------------------------------------------------ Iron Age

    /// <summary>
    /// A palisade of sharpened logs along a closed ring (plan points), skipping the gate span; every log is set 0.7 m
    /// into the ground at its own position, with its own height, radius, lean and bark offset; two rails tie it inside.
    /// </summary>
    public void Palisade(IReadOnlyList<Vector2> ring, float height, Vector2 gate, float gateWidth)
    {
        int n = ring.Count;
        var rails = new List<(Vector3 lo, Vector3 hi)>();
        for (int s = 0; s < n; s++)
        {
            var a = ring[s]; var b = ring[(s + 1) % n];
            float len = Vector2.Distance(a, b);
            var dir = (b - a) / len; var inside = Perp(dir);
            float spacing = 0.29f;
            for (float t = 0; t < len; t += spacing)
            {
                var p = a + dir * t;
                if (Vector2.Distance(p, gate) < gateWidth / 2 + 0.1f) continue;
                float r = 0.12f + 0.045f * Rnd();
                float h = height + (Rnd() - 0.5f) * 0.6f;
                float g = Ground(p);
                var lean = new Vector3((Rnd() - 0.5f) * 0.06f, 0, (Rnd() - 0.5f) * 0.06f);
                var bot = V3(p, g - 0.7f); var top = V3(p, g + h) + lean * h;
                var tint = new Vector3(0.85f + 0.25f * Rnd());
                B.Tube(StMat.Logs, bot, top, r, r * 0.92f, 7, tint, UvOff(), 1, 0.45f, 0.95f);
                // the sharpened point shows pale hewn wood
                B.Cone(StMat.Wood, top, top + Vector3.Normalize(top - bot) * (0.35f + 0.15f * Rnd()), r * 0.92f, 7, new Vector3(1.25f), UvOff(), 0.95f);
            }
            // rails every few metres along the inside
            for (float t = 0; t + 2.6f <= len; t += 2.6f)
            {
                var p0 = a + dir * t + inside * 0.24f; var p1 = a + dir * (t + 2.6f) + inside * 0.24f;
                if (Vector2.Distance((p0 + p1) / 2, gate) < gateWidth / 2 + 1.5f) continue;
                foreach (float y in new[] { 0.9f, 2.1f })
                    B.Tube(StMat.Wood, V3(p0, Ground(p0) + y), V3(p1, Ground(p1) + y), 0.07f, 0.07f, 6, new Vector3(0.9f), UvOff(), 1, 0.8f, 0.8f);
            }
        }
    }

    /// <summary>
    /// The village gate: a timber gate tower over the entrance (four posts, a planked fighting platform with a breast
    /// work, a thatched gable roof) and two plank gate leaves standing open inwards.
    /// </summary>
    public void IronAgeGate(Vector2 c, Vector2 outward, float width)
    {
        var ax = Perp(outward); var x3 = new Vector3(ax.X, 0, ax.Y); var z3 = new Vector3(outward.X, 0, outward.Y);
        float g = Ground(c);
        float hw = width / 2 + 0.45f, hd = 1.6f;
        float deck = 4.2f, postTop = 7.0f;
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            var p = c + ax * (sx * hw) + outward * (sz * hd);
            float gp = Ground(p);
            B.Tube(StMat.Logs, V3(p, gp - 0.9f), V3(p, gp + postTop), 0.24f, 0.21f, 9, new Vector3(0.9f), UvOff(), 1, 0.5f, 0.9f);
        }
        // tie beams and the deck
        foreach (int sz in new[] { -1, 1 })
            B.Box(StMat.Wood, V3(c + outward * (sz * hd), g + deck - 0.2f), new Vector3(hw + 0.4f, 0.16f, 0.14f), x3, new Vector3(0.9f), UvOff(), 0.85f);
        B.Box(StMat.Planks, V3(c, g + deck), new Vector3(hw + 0.5f, 0.07f, hd + 0.5f), x3, new Vector3(0.95f), UvOff(), 0.8f);
        // breastwork of planks around the deck (gaps for spears)
        foreach (int sz in new[] { -1, 1 })
            for (float t = -hw - 0.4f; t < hw + 0.4f; t += 0.42f)
                B.Box(StMat.Planks, V3(c + ax * (t + 0.18f) + outward * (sz * (hd + 0.45f)), g + deck + 0.65f), new Vector3(0.18f, 0.6f, 0.04f), x3, new Vector3(0.85f + 0.2f * Rnd()), UvOff(), 0.8f);
        foreach (int sx in new[] { -1, 1 })
            B.Box(StMat.Planks, V3(c + ax * (sx * (hw + 0.45f)), g + deck + 0.65f), new Vector3(0.04f, 0.6f, hd + 0.45f), x3, new Vector3(0.9f), UvOff(), 0.8f);
        // gable roof of thatch over the tower, ridge across the entrance
        GableRoof(StMat.Thatch, V3(c, g + postTop - 0.1f), ax, new Vector2(hw + 0.2f, hd + 0.2f), 42f, 0.55f, StMat.Planks, new Vector3(0.8f), new Vector3(0.95f), 0.25f);
        // gate leaves, open inwards (about 70°), hinged at the inner posts
        foreach (int sx in new[] { -1, 1 })
        {
            var hinge = c + ax * (sx * (width / 2)) - outward * (hd - 0.3f);
            float ang = sx * 1.2f;
            var leafDir = Vector2.Normalize(-ax * sx * MathF.Cos(1.2f) - outward * MathF.Sin(1.2f));
            var leafEnd = hinge + leafDir * (width / 2 - 0.05f);
            float gh = Ground(hinge);
            var a = V3(hinge, gh + 0.05f); var b = V3(leafEnd, gh + 0.05f);
            B.Quad(StMat.Planks, a, b, b + Vector3.Up * 3.1f, a + Vector3.Up * 3.1f, new Vector3(0.8f), UvOff(), 0.6f, 0.6f, 0.9f, 0.9f, new Vector2(1, -1));
            B.Quad(StMat.Planks, b, a, a + Vector3.Up * 3.1f, b + Vector3.Up * 3.1f, new Vector3(0.75f), UvOff(), 0.6f, 0.6f, 0.9f, 0.9f, new Vector2(1, -1));
            // braces
            B.Tube(StMat.Wood, a + Vector3.Up * 0.5f + new Vector3(leafDir.X, 0, leafDir.Y) * 0.1f, b + Vector3.Up * 2.6f, 0.06f, 0.06f, 5, new Vector3(0.8f), UvOff());
            _ = ang;
        }
    }

    /// <summary>
    /// A roundhouse: wattle-and-daub wall on a low stone footing (reaching below the lowest ground of the footprint),
    /// a door with a timber frame and an open dark interior, and a thick, ragged-eaved thatch cone.
    /// </summary>
    public void Roundhouse(Vector2 c, float r, Vector2 door, Vector3 daubTint, Vector3 thatchTint)
    {
        int seg = 22;
        var (lo, hi) = GroundRange(c, Vector2.UnitX, new Vector2(r, r));
        float floor = hi + 0.12f;
        float wallH = 1.75f + 0.25f * Rnd();
        float doorA = MathF.Atan2(door.Y, door.X), doorHalf = 0.62f / r;
        // footing ring (rubble) from below the lowest ground up to the floor
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * MathHelper.TwoPi / seg, a1 = (i + 1) * MathHelper.TwoPi / seg;
            var p0 = c + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * (r + 0.08f); var p1 = c + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * (r + 0.08f);
            B.Quad(StMat.Rubble, V3(p1, lo - 0.3f), V3(p0, lo - 0.3f), V3(p0, floor + 0.15f), V3(p1, floor + 0.15f), new Vector3(0.9f), UvOff(), 0.6f, 0.6f, 0.8f, 0.8f, new Vector2(1, -1));
        }
        // daub wall: outer and inner faces, the door gap with a frame
        float t = 0.28f;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * MathHelper.TwoPi / seg, a1 = (i + 1) * MathHelper.TwoPi / seg;
            float mid = (a0 + a1) / 2;
            float dd = MathF.Abs(MathHelper.WrapAngle(mid - doorA));
            bool isDoor = dd < doorHalf + MathHelper.TwoPi / seg * 0.5f;
            var d0 = new Vector2(MathF.Cos(a0), MathF.Sin(a0)); var d1 = new Vector2(MathF.Cos(a1), MathF.Sin(a1));
            float top0 = wallH, h0 = isDoor ? 1.62f : 0f;
            var o0 = c + d0 * r; var o1 = c + d1 * r; var i0 = c + d0 * (r - t); var i1 = c + d1 * (r - t);
            if (isDoor)
            {
                // above the door: a lintel band of daub; the opening shows the dark interior
                B.Quad(StMat.Daub, V3(o1, floor + h0), V3(o0, floor + h0), V3(o0, floor + top0), V3(o1, floor + top0), daubTint, UvOff(), 0.8f, 0.8f, 1, 1, new Vector2(1, -1));
                B.Quad(StMat.Plaster, V3(i1, floor), V3(i0, floor), V3(i0, floor + h0), V3(i1, floor + h0), new Vector3(0.05f), UvOff(), 0.2f, 0.2f, 0.2f, 0.2f);
                continue;
            }
            B.Quad(StMat.Daub, V3(o1, floor + 0.1f), V3(o0, floor + 0.1f), V3(o0, floor + top0), V3(o1, floor + top0), daubTint, UvOff(), 0.55f, 0.55f, 0.85f, 0.85f, new Vector2(1, -1));
        }
        // door frame posts and lintel
        var dl = c + new Vector2(MathF.Cos(doorA - doorHalf), MathF.Sin(doorA - doorHalf)) * (r + 0.02f);
        var dr = c + new Vector2(MathF.Cos(doorA + doorHalf), MathF.Sin(doorA + doorHalf)) * (r + 0.02f);
        B.Tube(StMat.Wood, V3(dl, floor - 0.2f), V3(dl, floor + 1.75f), 0.09f, 0.08f, 6, new Vector3(0.85f), UvOff(), 1, 0.6f, 0.9f);
        B.Tube(StMat.Wood, V3(dr, floor - 0.2f), V3(dr, floor + 1.75f), 0.09f, 0.08f, 6, new Vector3(0.85f), UvOff(), 1, 0.6f, 0.9f);
        B.Tube(StMat.Wood, V3(dl, floor + 1.68f), V3(dr, floor + 1.68f), 0.08f, 0.08f, 6, new Vector3(0.85f), UvOff());
        // roof: thatch of different ages (fresh golden to weathered grey-brown), a bound ridge cap at the apex
        float pitch = 47f + 4f * Rnd();
        float age = Rnd();
        var aged = Vector3.Lerp(new Vector3(1.12f, 1.0f, 0.78f), new Vector3(0.78f, 0.76f, 0.70f), age);
        float eaves = floor + wallH - 0.1f;
        ConeRoof(c, eaves, r + 0.75f, pitch, 0.32f, thatchTint * aged);
        float rise = (r + 0.75f) * MathF.Tan(MathHelper.ToRadians(pitch));
        B.Tube(StMat.Thatch, V3(c, eaves + rise - 0.55f), V3(c, eaves + rise + 0.25f), 0.42f, 0.18f, 10, thatchTint * aged * 0.7f, UvOff(), 1, 0.7f, 0.8f, capTop: true);
    }

    /// <summary>A raised four-post granary: posts with stone caps, a planked floor, daub walls, a thatched gable roof.</summary>
    public void Granary(Vector2 c, Vector2 ax, Vector3 thatchTint)
    {
        var az = Perp(ax); var x3 = new Vector3(ax.X, 0, ax.Y);
        var (lo, hi) = GroundRange(c, ax, new Vector2(1.3f, 1.3f));
        float floor = hi + 1.0f;
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            var p = c + ax * (sx * 1.15f) + az * (sz * 1.15f);
            B.Tube(StMat.Wood, V3(p, Ground(p) - 0.3f), V3(p, floor), 0.12f, 0.11f, 6, new Vector3(0.85f), UvOff(), 1, 0.6f, 0.9f);
        }
        B.Box(StMat.Planks, V3(c, floor + 0.06f), new Vector3(1.4f, 0.06f, 1.4f), x3, new Vector3(0.9f), UvOff());
        var corners = new[] { c + ax * -1.25f + az * -1.25f, c + ax * 1.25f + az * -1.25f, c + ax * 1.25f + az * 1.25f, c + ax * -1.25f + az * 1.25f };
        for (int i = 0; i < 4; i++)
            Wall(StMat.Daub, corners[i], corners[(i + 1) % 4], floor + 0.1f, 1.3f, 0.12f, new Vector3(0.95f), i == 0 ? new[] { new Opening(1.25f, 0.2f, 0.7f, 0.9f) } : null);
        GableRoof(StMat.Thatch, V3(c, floor + 1.35f), ax, new Vector2(1.25f, 1.25f), 45f, 0.45f, StMat.Daub, new Vector3(0.9f), thatchTint, 0.22f);
        _ = lo;
    }

    /// <summary>A wattle fence (posts and three rails) along a polyline.</summary>
    public void Fence(IReadOnlyList<Vector2> line)
    {
        for (int i = 0; i + 1 < line.Count; i++)
        {
            var a = line[i]; var b = line[i + 1];
            float len = Vector2.Distance(a, b);
            for (float t = 0; t <= len; t += 1.6f)
            {
                var p = Vector2.Lerp(a, b, t / len);
                B.Tube(StMat.Wood, V3(p, Ground(p) - 0.3f), V3(p, Ground(p) + 1.15f), 0.05f, 0.045f, 5, new Vector3(0.8f), UvOff(), 1, 0.6f, 0.9f);
            }
            foreach (float y in new[] { 0.35f, 0.7f, 1.0f })
                B.Tube(StMat.Wood, V3(a, Ground(a) + y), V3(b, Ground(b) + y), 0.03f, 0.03f, 4, new Vector3(0.75f), UvOff());
        }
    }

    /// <summary>A haystack: a straw cylinder with a thatch cone top.</summary>
    public void Haystack(Vector2 c, float r, Vector3 tint)
    {
        float g = Ground(c);
        B.Tube(StMat.Thatch, V3(c, g - 0.2f), V3(c, g + 1.3f), r, r * 1.05f, 14, tint, UvOff(), 2, 0.6f, 0.95f);
        ConeRoof(c, g + 1.3f, r * 1.12f, 40f, 0.2f, tint);
    }

    // ------------------------------------------------------------------------------------------------ Classical

    /// <summary>
    /// A Mediterranean town house: stone plinth below the highest ground of the footprint, lime-plastered walls in the
    /// house's own colour, recessed openings (plank door, closed shutters), a projecting timber cornice, and a low
    /// terracotta tile roof (hip or gable) with eaves.
    /// </summary>
    public void House(Vector2 c, Vector2 ax, Vector2 half, int floors, Vector3 plaster, Vector3 roofTint, int doorSide, bool hip)
    {
        var az = Perp(ax);
        var (lo, hi) = GroundRange(c, ax, half);
        float floor = hi + 0.25f;
        float h = floors == 1 ? 3.6f : 6.6f;
        var x3 = new Vector3(ax.X, 0, ax.Y);
        // plinth (ashlar) from below the lowest ground to the floor
        B.Box(StMat.Ashlar, V3(c, (lo - 0.4f + floor) / 2), new Vector3(half.X + 0.12f, (floor - lo + 0.4f) / 2, half.Y + 0.12f), x3, new Vector3(0.95f), UvOff(), 0.7f);
        var corners = new[] { c - ax * half.X - az * half.Y, c + ax * half.X - az * half.Y, c + ax * half.X + az * half.Y, c - ax * half.X + az * half.Y };
        for (int i = 0; i < 4; i++)
        {
            var a = corners[i]; var b = corners[(i + 1) % 4]; float len = Vector2.Distance(a, b);
            var ops = new List<Opening>();
            if (i == doorSide) ops.Add(new Opening(len * (0.3f + 0.4f * Rnd()), 0, 1.15f, 2.15f, Door: true));
            int win = (int)(len / 3.2f);
            for (int k = 0; k < win; k++)
            {
                float at = (k + 0.5f) * len / win;
                if (ops.Count > 0 && MathF.Abs(at - ops[0].At) < 1.4f) continue;
                if (floors == 1 && Rnd() < 0.5f) continue;
                ops.Add(new Opening(at, floors == 1 ? 1.5f : 4.2f, 0.75f, 1.0f));
            }
            // corners: walls overlap by their thickness so the corner is closed
            var dir = (b - a) / len;
            Wall(StMat.Plaster, a - dir * 0.25f, b + dir * 0.25f, floor, h, 0.5f, plaster, ops, cap: false);
        }
        // timber cornice band under the eaves
        B.Box(StMat.Wood, V3(c, floor + h - 0.08f), new Vector3(half.X + 0.33f, 0.09f, half.Y + 0.33f), x3, new Vector3(0.6f), UvOff(), 0.6f);
        if (hip) HipRoof(StMat.Tiles, V3(c, floor + h), ax, half + new Vector2(0.25f), 23f, 0.5f, roofTint);
        else GableRoof(StMat.Tiles, V3(c, floor + h), ax, half + new Vector2(0.25f), 22f, 0.5f, StMat.Plaster, plaster, roofTint);
    }

    /// <summary>
    /// A temple: an ashlar podium (to below the lowest ground) with steps at the front, a peristyle of fluted columns
    /// with capitals, an entablature (architrave, frieze, cornice), a stone cella with a door, and a low gable roof of
    /// tiles over pediments.
    /// </summary>
    public void Temple(Vector2 c, Vector2 ax, int colsFront, int colsSide)
    {
        var az = Perp(ax); var x3 = new Vector3(ax.X, 0, ax.Y);
        float spacing = 2.7f, colR = 0.42f, colH = 7.2f;
        var half = new Vector2((colsFront - 1) * spacing / 2 + 1.4f, (colsSide - 1) * spacing / 2 + 1.4f);
        var (lo, hi) = GroundRange(c, ax, half + new Vector2(0, 2.5f));
        float podium = hi + 1.5f;
        var stone = new Vector3(1.05f, 1.02f, 0.96f);
        B.Box(StMat.Ashlar, V3(c, (lo - 0.5f + podium) / 2), new Vector3(half.X, (podium - lo + 0.5f) / 2, half.Y), x3, stone, UvOff(), 0.7f, StMat.Paving);
        // steps across the front (the +z side faces the approach)
        int steps = 5;
        for (int s = 0; s < steps; s++)
        {
            float top = podium - s * (podium - hi) / steps;
            var sc = c + az * (half.Y + 0.4f + s * 0.45f);
            B.Box(StMat.Ashlar, V3(sc, (top + lo - 0.5f) / 2), new Vector3(half.X - 0.2f, (top - lo + 0.5f) / 2, 0.24f), x3, stone, UvOff(), 0.8f, StMat.Paving);
        }
        // columns: 20 flutes approximated by alternating radii, capital (echinus + abacus), base
        for (int i = 0; i < colsFront; i++)
            for (int j = 0; j < colsSide; j++)
            {
                if (i != 0 && i != colsFront - 1 && j != 0 && j != colsSide - 1) continue;
                var p = c + ax * ((i - (colsFront - 1) / 2f) * spacing) + az * ((j - (colsSide - 1) / 2f) * spacing);
                Column(V3(p, podium), colR, colH, stone);
            }
        // entablature
        float eTop = podium + colH;
        var eh = new Vector2((colsFront - 1) * spacing / 2 + colR + 0.3f, (colsSide - 1) * spacing / 2 + colR + 0.3f);
        B.Box(StMat.Ashlar, V3(c, eTop + 0.45f), new Vector3(eh.X, 0.45f, eh.Y), x3, stone, UvOff(), 0.85f);            // architrave
        B.Box(StMat.Ashlar, V3(c, eTop + 1.25f), new Vector3(eh.X - 0.05f, 0.35f, eh.Y - 0.05f), x3, stone * 0.93f, UvOff(), 0.8f); // frieze
        B.Box(StMat.Ashlar, V3(c, eTop + 1.72f), new Vector3(eh.X + 0.35f, 0.12f, eh.Y + 0.35f), x3, stone, UvOff(), 0.7f); // cornice
        // cella
        var ch = new Vector2(eh.X - 2.6f, eh.Y - 3.4f);
        var cc = c - az * 0.6f;
        var corners = new[] { cc - ax * ch.X - az * ch.Y, cc + ax * ch.X - az * ch.Y, cc + ax * ch.X + az * ch.Y, cc - ax * ch.X + az * ch.Y };
        for (int k = 0; k < 4; k++)
        {
            var a = corners[k]; var b = corners[(k + 1) % 4]; float len = Vector2.Distance(a, b);
            var dir = (b - a) / len;
            var ops = k == 2 ? new[] { new Opening(len / 2, 0, 2.2f, 4.6f, Door: true) } : null;
            Wall(StMat.Ashlar, a - dir * 0.35f, b + dir * 0.35f, podium, colH + 0.2f, 0.7f, stone * 0.97f, ops, cap: false);
        }
        // roof: low gable, ridge along the long axis (az), pediments at the ends
        GableRoof(StMat.Tiles, V3(c, eTop + 1.84f), az, new Vector2(eh.Y + 0.35f, eh.X + 0.35f), 15f, 0.25f, StMat.Ashlar, stone, new Vector3(0.95f), 0.2f);
    }

    void Column(Vector3 baseP, float r, float h, Vector3 tint)
    {
        // base (torus-like ring), fluted shaft with a slight entasis, echinus and abacus
        B.Tube(StMat.Ashlar, baseP, baseP + Vector3.Up * 0.25f, r * 1.32f, r * 1.25f, 20, tint, UvOff(), 1, 0.7f, 0.85f);
        int seg = 40;
        var a = baseP + Vector3.Up * 0.25f; var b = baseP + Vector3.Up * (h - 0.75f);
        float ws = StructureBuilder.WorldSize(StMat.Ashlar);
        int v0 = B.Verts[(int)StMat.Ashlar].Count;
        var uvo = UvOff();
        int rings = 5;
        for (int k = 0; k <= rings; k++)
        {
            float f = k / (float)rings;
            float rr = r * (1f - 0.14f * f + 0.05f * MathF.Sin(f * MathF.PI));
            var centre = Vector3.Lerp(a, b, f);
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg * MathHelper.TwoPi;
                float flute = (i % 2 == 0) ? 1f : 0.93f;
                var dir = new Vector3(MathF.Cos(t), 0, MathF.Sin(t));
                var nrm = Vector3.Normalize(dir + new Vector3(-MathF.Sin(t), 0, MathF.Cos(t)) * ((i % 2 == 0) ? 0f : 0.0f));
                B.Vertex(StMat.Ashlar, centre + dir * rr * flute, nrm, new Vector3(-MathF.Sin(t), 0, MathF.Cos(t)), 1,
                    new Vector2(i / (float)seg * 2 * MathF.PI * r / ws + uvo.X, f * (b - a).Length() / ws + uvo.Y), tint, k == 0 ? 0.75f : 0.95f);
            }
        }
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < seg; i++)
            {
                int p = v0 + k * (seg + 1) + i;
                B.Tri(StMat.Ashlar, p, p + seg + 1, p + 1); B.Tri(StMat.Ashlar, p + 1, p + seg + 1, p + seg + 2);
            }
        var top = b;
        B.Tube(StMat.Ashlar, top, top + Vector3.Up * 0.4f, r * 0.88f, r * 1.3f, 20, tint, UvOff(), 1, 0.85f, 0.95f);
        B.Box(StMat.Ashlar, top + Vector3.Up * 0.55f, new Vector3(r * 1.4f, 0.15f, r * 1.4f), Vector3.UnitX, tint, UvOff(), 0.9f);
    }

    /// <summary>
    /// A curtain-wall section a→b with the outer face on the left: ashlar from a stepped foundation (below the lowest
    /// ground along it) up to a level wall walk, a crenellated parapet on the outer edge (merlons and embrasures) and a
    /// low inner parapet.
    /// </summary>
    public void CurtainWall(Vector2 a, Vector2 b, float walkY, float thick, Vector3 tint)
    {
        var dir = Vector2.Normalize(b - a); float len = Vector2.Distance(a, b);
        var outN = -Perp(dir); var x3 = new Vector3(dir.X, 0, dir.Y);
        // foundation steps every 4 m follow the ground
        for (float t = 0; t < len; t += 4f)
        {
            float t1 = MathF.Min(len, t + 4f);
            var p0 = a + dir * t; var p1 = a + dir * t1;
            float g = MathF.Min(Ground(p0), Ground(p1));
            for (float s = t; s <= t1; s += 1f) g = MathF.Min(g, Ground(a + dir * s));
            var mid = (p0 + p1) / 2;
            B.Box(StMat.Ashlar, V3(mid, (g - 0.8f + walkY) / 2), new Vector3((t1 - t) / 2 + 0.01f, (walkY - g + 0.8f) / 2, thick / 2), x3, tint * (0.96f + 0.06f * Rnd()), UvOff() + new Vector2(t / 6f, 0), 0.65f, StMat.Paving);
        }
        // crenellated outer parapet: merlons 1.3 m wide, embrasures 0.8 m, 1.7 m high; a low inner parapet
        float merlon = 1.3f, gap = 0.8f, ph = 1.7f;
        for (float t = 0.2f; t + merlon <= len; t += merlon + gap)
        {
            var p = a + dir * (t + merlon / 2) + outN * (thick / 2 - 0.3f);
            B.Box(StMat.Ashlar, V3(p, walkY + ph / 2), new Vector3(merlon / 2, ph / 2, 0.3f), x3, tint, UvOff(), 0.85f);
        }
        B.Box(StMat.Ashlar, V3(a + dir * (len / 2) + outN * (thick / 2 - 0.3f), walkY + 0.45f), new Vector3(len / 2, 0.45f, 0.31f), x3, tint, UvOff(), 0.8f);
        B.Box(StMat.Ashlar, V3(a + dir * (len / 2) - outN * (thick / 2 - 0.2f), walkY + 0.4f), new Vector3(len / 2, 0.4f, 0.2f), x3, tint, UvOff(), 0.8f);
    }

    /// <summary>
    /// A square wall tower projecting outwards: ashlar to two storeys above the wall walk, a string course, arched windows
    /// on the upper storey (real recesses with shutters), and a pyramidal tile roof with eaves — the roofed tower of
    /// Hellenistic and Roman town walls rather than an open castle turret.
    /// </summary>
    public void Tower(Vector2 c, Vector2 outward, float size, float walkY, Vector3 tint)
    {
        var ax = -Perp(outward); float half = size / 2;
        var (lo, _) = GroundRange(c, ax, new Vector2(half, half));
        float top = walkY + 5.2f;
        var corners = new[] { c - ax * half - outward * half, c + ax * half - outward * half, c + ax * half + outward * half, c - ax * half + outward * half };
        for (int i = 0; i < 4; i++)
        {
            var a = corners[i]; var b = corners[(i + 1) % 4]; float len = Vector2.Distance(a, b);
            var dir = (b - a) / len;
            var ops = new[] { new Opening(len * 0.3f, walkY + 1.4f - (lo - 0.8f), 0.9f, 1.9f, Arch: true), new Opening(len * 0.7f, walkY + 1.4f - (lo - 0.8f), 0.9f, 1.9f, Arch: true) };
            Wall(StMat.Ashlar, a - dir * 0.6f, b + dir * 0.6f, lo - 0.8f, top - (lo - 0.8f), 1.2f, tint * (0.97f + 0.06f * Rnd()), i == 1 ? null : ops, cap: false);
        }
        // string course at wall-walk level
        B.Box(StMat.Ashlar, V3(c, walkY + 0.12f), new Vector3(half + 0.75f, 0.12f, half + 0.75f), new Vector3(ax.X, 0, ax.Y), tint * 1.04f, UvOff(), 0.8f);
        HipRoof(StMat.Tiles, V3(c, top), ax, new Vector2(half + 0.6f, half + 0.6f), 26f, 0.55f, new Vector3(0.95f));
    }

    /// <summary>
    /// The town gate: a gatehouse between two towers with a round-arched gateway (voussoirs, a barrel-vaulted passage
    /// through the wall) and two studded plank gates standing half open.
    /// </summary>
    public void TownGate(Vector2 c, Vector2 outward, float width, float walkY, float thick, Vector3 tint)
    {
        var ax = Perp(outward);
        float span = width + 3.2f;
        var a = c - ax * (span / 2); var b = c + ax * (span / 2);
        float g = MathF.Min(Ground(a), MathF.Min(Ground(b), Ground(c)));
        float baseY = g - 0.8f;
        // the gate wall: outer face towards +outward; opening at the centre
        var op = new Opening(span / 2, Ground(c) - baseY, width, 5.6f, Arch: true, Open: true);
        Wall(StMat.Ashlar, a, b, baseY, walkY + 1.8f - baseY, thick, tint, new[] { op with { At = span / 2 } }, cap: true);
        // flanking towers
        Tower(a - ax * 3.2f + outward * 1.2f, outward, 6.2f, walkY, tint);
        Tower(b + ax * 3.2f + outward * 1.2f, outward, 6.2f, walkY, tint);
        // gate leaves half open inwards
        foreach (int s in new[] { -1, 1 })
        {
            var hinge = c + ax * (s * width / 2) - outward * (thick / 2 - 0.4f);
            var leaf = Vector2.Normalize(-ax * s * MathF.Cos(0.9f) - outward * MathF.Sin(0.9f));
            var e = hinge + leaf * (width / 2);
            float gy = Ground(c) + 0.02f;
            var p0 = V3(hinge, gy); var p1 = V3(e, gy);
            B.Quad(StMat.Planks, p0, p1, p1 + Vector3.Up * 4.0f, p0 + Vector3.Up * 4.0f, new Vector3(0.7f), UvOff(), 0.55f, 0.55f, 0.8f, 0.8f, new Vector2(1, -1));
            B.Quad(StMat.Planks, p1, p0, p0 + Vector3.Up * 4.0f, p1 + Vector3.Up * 4.0f, new Vector3(0.65f), UvOff(), 0.55f, 0.55f, 0.8f, 0.8f, new Vector2(1, -1));
            for (float y = 0.6f; y < 4f; y += 1.1f)
                B.Tube(StMat.Wood, p0 + Vector3.Up * y + new Vector3(0, 0, 0), p1 + Vector3.Up * y, 0.05f, 0.05f, 5, new Vector3(0.3f), UvOff());
        }
    }

    /// <summary>A paved strip (street) from a to b of a width, draped on the ground in 2 m steps.</summary>
    public void Street(Vector2 a, Vector2 b, float width, Vector3 tint)
    {
        var dir = Vector2.Normalize(b - a); var side = Perp(dir) * (width / 2); float len = Vector2.Distance(a, b);
        var uv = UvOff();
        for (float t = 0; t < len; t += 2f)
        {
            float t1 = MathF.Min(len, t + 2f);
            var p0 = a + dir * t; var p1 = a + dir * t1;
            Vector3 P(Vector2 q) => V3(q, Ground(q) + 0.04f);
            B.Quad(StMat.Paving, P(p0 - side), P(p0 + side), P(p1 + side), P(p1 - side), tint, uv, 0.95f, 0.95f, 0.95f, 0.95f);
        }
    }
}
