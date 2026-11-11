using Gfx.Gear.Geometry;
using Gfx.Gear.Maps;
using Microsoft.Xna.Framework;
using static Gfx.Gear.Maps.Noise;

namespace Gfx.Gear.Items;

/// <summary>Parameters of a weapon of the era kit.</summary>
public sealed record WeaponSpec
{
    /// <summary>spear, xiphos, longsword (Celtic, La Tene), arming (medieval sword), axe, bow, arrow, quiver, scabbard, sling, musket.</summary>
    public string Kind { get; init; } = "spear";
    public uint Seed { get; init; } = 1;
    public int MapSize { get; init; } = 512;
    public int Lod { get; init; }
    public double Wear { get; init; } = 0.4;
    /// <summary>Spearheads and sword fittings: "iron" or "bronze".</summary>
    public string Metal { get; init; } = "iron";
}

/// <summary>
/// Weapons of the era kit, generated in code from cross-sections lofted along their length (blades with a midrib or a
/// fuller and bevelled edges, sockets, shafts, guards, grips and pommels): the hoplite spear (dory) with its leaf head
/// and bronze butt spike, the xiphos, the Celtic long sword, the medieval arming sword, the bearded axe, the self bow
/// (a small skeleton: grip, two limbs and the string's nock, so it can be drawn), arrows and a quiver, a scabbard, a
/// sling and a matchlock musket. Item space: the weapon's length along +Y; frames <c>grip</c> (main hand, +Y along
/// the grip towards the business end), <c>grip2</c> (off hand), <c>hang</c> (carried). PRESENTATION ONLY.
/// </summary>
public static class Weapons
{
    const int PartBlade = 10, PartWood = 20, PartLeather = 30, PartFitting = 40, PartString = 50, PartFeather = 60, PartIron = 70;
    static readonly UvRect R0 = new(0f, 0f, 0.5f, 1f), R1 = new(0.5f, 0f, 0.75f, 1f), R2 = new(0.75f, 0f, 1f, 0.5f), R3 = new(0.75f, 0.5f, 1f, 1f);

    public static KitItem Build(WeaponSpec s)
    {
        var item = new KitItem { Id = $"{s.Kind}#{s.Seed}L{s.Lod}", Kind = "weapon." + s.Kind, Era = s.Kind is "spear" or "xiphos" or "longsword" or "sling" ? "ancient" : s.Kind == "musket" ? "early_modern" : "medieval" };
        var m = new KitMesh(s.Kind);
        bool hi = s.Lod == 0;
        switch (s.Kind)
        {
            case "spear":
            {
                item.Label = "Spear (ash shaft, leaf head, butt spike)";
                double L = 2.45;
                // shaft from the butt spike's socket (0.18) to the head's socket (2.26)
                m.Append(Shaft(0.17, 2.27, 0.0135, 0.0118, hi ? 12 : 6, R1));
                m.Append(LeafBlade(2.24, 0.24, 0.024, 0.0065, hi, socketR: 0.0145, socketLen: 0.075, R0));
                m.Append(ButtSpike(0.0, 0.20, hi));
                m.Append(Wrap(0.92, 1.12, 0.0142, hi));
                item.Frames["grip"] = KitItem.MakeFrame(new Vector3(0, 1.02f, 0), Vector3.UnitY, Vector3.UnitZ);
                item.Frames["grip2"] = KitItem.MakeFrame(new Vector3(0, 1.62f, 0), Vector3.UnitY, Vector3.UnitZ);
                // overhand: the thumb towards the butt, the head leaving the fist on the little-finger side
                item.Frames["grip_over"] = KitItem.MakeFrame(new Vector3(0, 1.02f, 0), -Vector3.UnitY, Vector3.UnitZ);
                item.Frames["hang"] = item.Frames["grip"];
                item.GripRadius = 0.0142f; item.Grip2Radius = 0.0128f;
                item.CentreOfMass = new Vector3(0, 1.05f, 0);
                _ = L;
                break;
            }
            case "xiphos":
            case "longsword":
            case "arming":
            {
                var (blade, guardW, grip, label) = s.Kind switch
                {
                    "xiphos" => (0.60, 0.11, 0.105, "Xiphos (leaf-bladed short sword)"),
                    "longsword" => (0.76, 0.07, 0.115, "Long sword (Celtic, parallel blade)"),
                    _ => (0.80, 0.20, 0.110, "Arming sword (cruciform hilt)"),
                };
                item.Label = label;
                m.Append(SwordBlade(s.Kind, blade, hi));
                m.Append(Guard(s.Kind, guardW, hi));
                m.Append(GripTube(-grip, -0.002, s.Kind == "xiphos" ? 0.015 : 0.0145, hi));
                m.Append(Pommel(s.Kind, -grip, hi));
                item.Frames["grip"] = KitItem.MakeFrame(new Vector3(0, (float)(-grip * 0.5), 0), Vector3.UnitY, Vector3.UnitZ);
                item.Frames["hang"] = KitItem.MakeFrame(new Vector3(0, 0.02f, 0), Vector3.UnitY, Vector3.UnitZ);
                item.GripRadius = 0.0145f;
                item.CentreOfMass = new Vector3(0, (float)(blade * 0.18), 0);
                break;
            }
            case "axe":
            {
                item.Label = "Bearded axe";
                m.Append(Shaft(0.0, 0.80, 0.0150, 0.0140, hi ? 10 : 6, R1, oval: 0.75));
                m.Append(AxeHead(0.70, hi));
                m.Append(Wrap(0.06, 0.24, 0.0158, hi));
                item.Frames["grip"] = KitItem.MakeFrame(new Vector3(0, 0.14f, 0), Vector3.UnitY, Vector3.UnitZ);
                item.Frames["hang"] = item.Frames["grip"];
                item.GripRadius = 0.0155f;
                item.CentreOfMass = new Vector3(0.03f, 0.55f, 0);
                break;
            }
            case "bow":
            {
                item.Label = "Self bow (D-section stave, linen string)";
                m.Append(BowStave(hi));
                item.Frames["grip"] = KitItem.MakeFrame(new Vector3(0, 0, 0), Vector3.UnitY, Vector3.UnitZ);
                item.Frames["hang"] = item.Frames["grip"];
                item.GripRadius = 0.017f;
                item.CentreOfMass = Vector3.Zero;
                break;
            }
            case "arrow":
            {
                item.Label = "Arrow";
                m.Append(Arrow(hi, Vector3.Zero, 0));
                item.Frames["grip"] = KitItem.MakeFrame(new Vector3(0, 0.02f, 0), Vector3.UnitY, Vector3.UnitZ);
                item.GripRadius = 0.004f;
                break;
            }
            case "quiver":
            {
                item.Label = "Quiver with arrows";
                m.Append(Quiver(hi, s.Seed));
                item.Frames["hang"] = KitItem.MakeFrame(new Vector3(0, 0.30f, -0.045f), Vector3.UnitY, -Vector3.UnitZ);
                break;
            }
            case "scabbard":
            {
                item.Label = "Scabbard (leather over wood, bronze chape)";
                m.Append(Scabbard(hi));
                item.Frames["hang"] = KitItem.MakeFrame(new Vector3(0, -0.04f, -0.012f), Vector3.UnitY, -Vector3.UnitX);
                break;
            }
            case "sling":
            {
                item.Label = "Sling (plaited cords, leather pouch)";
                m.Append(Sling(hi));
                item.Frames["grip"] = KitItem.MakeFrame(new Vector3(0, 0, 0), Vector3.UnitY, Vector3.UnitZ);
                item.GripRadius = 0.004f;
                break;
            }
            default: // musket
            {
                item.Label = "Matchlock musket";
                m.Append(Musket(hi));
                // the right hand on the wrist of the stock, the left under the fore-end
                item.Frames["grip"] = KitItem.MakeFrame(new Vector3(0, 0.30f, -0.035f), Vector3.UnitY, Vector3.UnitZ);
                item.Frames["grip2"] = KitItem.MakeFrame(new Vector3(0, 0.78f, -0.012f), Vector3.UnitY, -Vector3.UnitX);
                item.Frames["hang"] = item.Frames["grip"];
                item.GripRadius = 0.020f; item.Grip2Radius = 0.022f;
                item.CentreOfMass = new Vector3(0, 0.55f, 0);
                break;
            }
        }
        m.Name = s.Kind;
        m.Finish(45f);
        item.Parts.Add(new KitPart { Name = s.Kind, Mesh = m, Maps = "w", AlphaTest = s.Kind is "quiver" or "arrow" });
        if (s.MapSize > 0) item.Maps["w"] = Paint(s, m);
        return item;
    }

    // ---------------------------------------------------------------- parts

    static KitMesh Shaft(double y0, double y1, double r0, double r1, int sides, UvRect rect, double oval = 1.0)
    {
        var path = new List<Vector3d>();
        int n = Math.Max(2, (int)((y1 - y0) / 0.05));
        for (int i = 0; i <= n; i++) path.Add(new Vector3d(0, y0 + (y1 - y0) * i / n, 0));
        var sec = Shapes.Circle(r0, r0 * oval, sides);
        var mesh = Shapes.Sweep(path, sec, true, rect, new Color(PartWood, 0, 0, 255), up: new Vector3d(0, 0, 1),
            scale: i => 1 + (r1 / r0 - 1) * i / n, vRepeat: 1.0, caps: true);
        return mesh;
    }

    /// <summary>Rings of a lofted part along +Y: section(t) returns the ring (x, z) at fraction t; masks carry an edge factor (G).</summary>
    static KitMesh LoftY(double y0, double y1, int rows, int cols, Func<double, int, (double x, double z, double edge)> ring, UvRect rect, int part, bool cap0, bool cap1)
    {
        var m = new KitMesh();
        int c1 = cols + 1;
        for (int j = 0; j <= rows; j++)
        {
            double t = (double)j / rows, y = y0 + (y1 - y0) * t;
            for (int i = 0; i <= cols; i++)
            {
                var (x, z, e) = ring(t, i % cols);
                m.Add(new Vector3((float)x, (float)y, (float)z), rect.Map((double)i / cols, t), new Color(part, (int)(255 * Saturate(e)), 0, 255));
            }
        }
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                int a = j * c1 + i, b = a + 1, c = a + c1 + 1, d = a + c1;
                m.Quad(a, b, c, d);
            }
        // caps (fans)
        foreach (var (j, flip) in new[] { (0, cap0), (rows, cap1) })
        {
            if (!flip) continue;
            var ctr = Vector3.Zero;
            for (int i = 0; i < cols; i++) ctr += m.Positions[j * c1 + i];
            ctr /= cols;
            int cc = m.Add(ctr, rect.Map(0.5, j == 0 ? 0 : 1), new Color(part, 0, 0, 255));
            for (int i = 0; i < cols; i++) { int a = j * c1 + i, b = j * c1 + i + 1; if (j == 0) m.Tri(cc, b, a); else m.Tri(cc, a, b); }
        }
        Shapes.OrientOutward(m, new Vector3(0, (float)(0.5 * (y0 + y1)), 0));
        return m;
    }

    /// <summary>A double-edged blade section: lens with a midrib, half width w, half thickness th; i in [0, cols).</summary>
    static (double x, double z, double edge) BladeSection(int i, int cols, double w, double th, double fuller = 0)
    {
        // parameter around the section: 0..cols/2 on one face (edge to edge), the rest on the other
        double a = 2 * Math.PI * i / cols;
        double x = w * Math.Cos(a);
        double u = Math.Abs(x) / Math.Max(w, 1e-9);
        double z = th * Math.Pow(1 - u, 0.62) * Math.Sign(Math.Sin(a) == 0 ? 1 : Math.Sin(a));
        if (fuller > 0) z *= 1 - fuller * Math.Exp(-u * u / 0.06);
        return (x, z, Smoothstep(0.75, 1.0, u));
    }

    static KitMesh LeafBlade(double y0, double len, double halfW, double halfTh, bool hi, double socketR, double socketLen, UvRect rect)
    {
        var m = new KitMesh();
        int cols = hi ? 24 : 8;
        // the socket (a cone) then the leaf: widest at 40 % of the blade, ogival tip
        m.Append(LoftY(y0, y0 + socketLen, hi ? 6 : 2, cols, (t, i) =>
        {
            double r = socketR * (1 - 0.35 * t);
            double a = 2 * Math.PI * i / cols;
            return (r * Math.Cos(a), r * Math.Sin(a), 0);
        }, new UvRect(rect.U0, rect.V0, rect.U1, rect.V0 + rect.Height * 0.3f), PartIron, true, false));
        double yb = y0 + socketLen - 0.005;
        m.Append(LoftY(yb, yb + len, hi ? 28 : 8, cols, (t, i) =>
        {
            double w = halfW * Math.Pow(Math.Sin(Math.PI * Math.Min(1, t / 0.80) * 0.5 + (t < 0.4 ? 0 : 0)), 0.8) * (t < 0.4 ? Math.Sin(Math.PI * 0.5 * t / 0.4) : Math.Pow(Math.Cos(Math.PI * 0.5 * (t - 0.4) / 0.6), 0.85));
            w = Math.Max(w, 0.0004 + socketR * 0.6 * Math.Max(0, 1 - t * 6));
            double th = halfTh * (1 - 0.85 * t) + 0.0006;
            return BladeSection(i, cols, w, Math.Min(th, w * 0.9));
        }, new UvRect(rect.U0, rect.V0 + rect.Height * 0.3f, rect.U1, rect.V1), PartBlade, false, true));
        return m;
    }

    static KitMesh ButtSpike(double y0, double len, bool hi)
    {
        // a bronze sauroter: square section tapering to a point, a socket at the top
        int cols = 4 * (hi ? 3 : 1);
        return LoftY(y0, y0 + len, hi ? 12 : 4, cols, (t, i) =>
        {
            double a = 2 * Math.PI * i / cols + Math.PI / 4;
            double r = (0.003 + 0.0135 * Math.Pow(t, 0.7)) * (t > 0.85 ? 1.1 : 1.0);
            // squarish: superellipse
            double cs = Math.Cos(a), sn = Math.Sin(a);
            double k = 0.6;
            double x = r * Math.Sign(cs) * Math.Pow(Math.Abs(cs), k), z = r * Math.Sign(sn) * Math.Pow(Math.Abs(sn), k);
            return (x, z, t < 0.1 ? 1 : 0);
        }, R2, PartFitting, true, true);
    }

    static KitMesh Wrap(double y0, double y1, double r, bool hi, UvRect? rect = null)
    {
        // a leather grip wrap: a slightly larger tube with a spiral seam (painted)
        var path = new List<Vector3d>();
        int n = 8;
        for (int i = 0; i <= n; i++) path.Add(new Vector3d(0, y0 + (y1 - y0) * i / n, 0));
        return Shapes.Sweep(path, Shapes.Circle(r, r, hi ? 14 : 8), true, rect ?? R3, new Color(PartLeather, 0, 0, 255), up: new Vector3d(0, 0, 1), caps: true);
    }

    static KitMesh SwordBlade(string kind, double len, bool hi)
    {
        int cols = hi ? 24 : 8;
        return LoftY(0.0, len, hi ? 36 : 10, cols, (t, i) =>
        {
            double w, th, fuller = 0;
            switch (kind)
            {
                case "xiphos": // leaf-shaped: narrow at the hilt, widest at 2/3, then the point
                    w = 0.021 + 0.009 * Math.Sin(Math.PI * Math.Min(1, t / 0.66) * 0.5) - 0.0;
                    if (t > 0.66) w = 0.030 * Math.Pow(Math.Cos(Math.PI * 0.5 * (t - 0.66) / 0.34), 0.75);
                    th = 0.0045 * (1 - 0.6 * t) + 0.0005; break;
                case "longsword": // parallel edges, a rounded point
                    w = 0.025 * (1 - 0.08 * t);
                    if (t > 0.9) w *= Math.Sqrt(Math.Max(0, 1 - Math.Pow((t - 0.9) / 0.1, 2)));
                    th = 0.0035 * (1 - 0.4 * t) + 0.0005; break;
                default: // arming sword: tapering, a fuller over the first two thirds
                    w = 0.025 * (1 - 0.55 * t * t);
                    if (t > 0.88) w *= Math.Pow(Math.Max(0, 1 - (t - 0.88) / 0.12), 0.8);
                    th = 0.0040 * (1 - 0.6 * t) + 0.0005;
                    fuller = 0.55 * Smoothstep(0.75, 0.6, t) * Smoothstep(0.0, 0.05, t); break;
            }
            return BladeSection(i, cols, Math.Max(w, 0.0004), Math.Min(th, Math.Max(w, 0.0004) * 0.9), fuller);
        }, R0, PartBlade, true, true);
    }

    static KitMesh Guard(string kind, double halfW, bool hi)
    {
        // a bar across the blade's root (rounded rectangle section swept across X), slightly curved for the arming sword
        var path = new List<Vector3d>();
        int n = hi ? 12 : 4;
        double w = kind == "arming" ? halfW * 0.5 : halfW * 0.5;
        for (int i = 0; i <= n; i++) { double x = -w + 2 * w * i / n; path.Add(new Vector3d(x, kind == "arming" ? 0.006 * (x / w) * (x / w) : 0, 0)); }
        var sec = Aspis.RoundedRect(kind == "xiphos" ? 0.016 : 0.012, kind == "xiphos" ? 0.026 : 0.020, 0.004, hi ? 3 : 1);
        return Shapes.Sweep(path, sec, true, R2, new Color(PartFitting, 0, 0, 255), up: new Vector3d(0, 1, 0), caps: true,
            scale: i => kind == "arming" ? 1 + 0.25 * Math.Abs(2.0 * i / n - 1) : 1);
    }

    static KitMesh GripTube(double y0, double y1, double r, bool hi)
    {
        var path = new List<Vector3d>();
        int n = 10;
        for (int i = 0; i <= n; i++) path.Add(new Vector3d(0, y0 + (y1 - y0) * i / n, 0));
        // a spool shape: swelling in the middle
        return Shapes.Sweep(path, Shapes.Circle(r, r * 0.82, hi ? 14 : 8), true, R3, new Color(PartLeather, 0, 0, 255), up: new Vector3d(0, 0, 1),
            scale: i => 0.88 + 0.16 * Math.Sin(Math.PI * i / n), caps: false);
    }

    static KitMesh Pommel(string kind, double y, bool hi)
    {
        List<Vector2d> prof = kind switch
        {
            "arming" => new() { new(0, 0.0), new(0.018, -0.002), new(0.026, -0.016), new(0.026, -0.024), new(0.018, -0.038), new(0, -0.040) },
            "xiphos" => new() { new(0, 0.0), new(0.016, -0.002), new(0.022, -0.010), new(0.016, -0.018), new(0, -0.020) },
            _ => new() { new(0, 0.0), new(0.014, -0.004), new(0.020, -0.016), new(0.012, -0.026), new(0, -0.028) },
        };
        var lathe = Shapes.Lathe(Shapes.Fillet(prof, prof.Select((_, i) => i == 0 || i == prof.Count - 1 ? 0.0 : 0.004).ToList(), 3), hi ? 20 : 8, R2,
            new Color(PartFitting, 0, 0, 255));
        if (kind == "arming") lathe.Transform(Matrix.CreateScale(1f, 1f, 0.45f)); // a wheel pommel, flattened
        lathe.Transform(Matrix.CreateTranslation(0, (float)y, 0));
        return lathe;
    }

    static KitMesh AxeHead(double y, bool hi)
    {
        // outline in (x, y'): the eye round the haft at x = 0, the cheek tapering to the bearded blade towards +x
        var o = new List<Vector2d>();
        double top = 0.045, bot = -0.035;
        o.Add(new(-0.028, top * 0.6)); o.Add(new(0.02, top)); o.Add(new(0.10, top + 0.012)); o.Add(new(0.145, top + 0.030));
        for (int i = 0; i <= 10; i++) { double t = i / 10.0; o.Add(new(0.145 + 0.012 * Math.Sin(Math.PI * t), top + 0.030 - (top + 0.030 - (bot - 0.085)) * t)); }
        o.Add(new(0.12, bot - 0.085)); o.Add(new(0.07, bot - 0.03)); o.Add(new(0.02, bot)); o.Add(new(-0.028, bot * 0.6));
        var grid = Shapes.Grid(hi ? 30 : 10, hi ? 30 : 10, (u, v) => new Vector3d(-0.035 + 0.20 * u, 0, 0.09 - 0.25 * v), UvRect.Full, new Color(PartIron, 0, 0, 255));
        var cut = Shapes.Clip(grid, (i, p) => Shapes.PolygonSdf(o, new Vector2d(p.X, p.Z)));
        // thickness: thick at the eye, thin at the edge; build both faces by offsetting along Y
        var res = new KitMesh();
        foreach (double side in new[] { 1.0, -1.0 })
        {
            var f = new KitMesh();
            for (int i = 0; i < cut.VertexCount; i++)
            {
                var p = cut.Positions[i];
                double th = 0.012 * Math.Exp(-Math.Max(0, p.X) * 22) + 0.0008;
                f.Add(new Vector3(p.X, (float)(side * th), p.Z), R2.Map(0.5 + p.X / 0.4, 0.5 - p.Z / 0.4), new Color(PartIron, (int)(255 * Smoothstep(0.10, 0.145, p.X)), 0, 255));
            }
            f.Indices.AddRange(cut.Indices);
            if (side < 0) f.FlipWinding();
            res.Append(f);
        }
        // stitch the rims
        var loop = Shapes.BoundaryLoops(cut).OrderByDescending(l => l.Count).First();
        int baseTop = 0, baseBot = cut.VertexCount;
        for (int i = 0; i < loop.Count; i++)
        {
            int a = loop[i], b = loop[(i + 1) % loop.Count];
            res.Quad(baseTop + a, baseBot + a, baseBot + b, baseTop + b);
        }
        Shapes.OrientOutward(res, new Vector3(0.05f, 0, -0.02f));
        // stand it on the haft: the head's plane is X-Z with thickness along Y; rotate so the haft (+Y) passes through the eye
        res.Transform(Matrix.CreateRotationX(MathHelper.PiOver2) * Matrix.CreateTranslation(0, (float)y, 0));
        // the eye: a collar round the haft
        var path = new List<Vector3d>();
        for (int i = 0; i <= 6; i++) path.Add(new Vector3d(0, y - 0.04 + 0.08 * i / 6.0, 0));
        res.Append(Shapes.Sweep(path, Shapes.Circle(0.021, 0.026, hi ? 14 : 8), true, R2, new Color(PartIron, 0, 0, 255), up: new Vector3d(1, 0, 0), caps: true));
        return res;
    }

    static KitMesh BowStave(bool hi)
    {
        // a D-section stave 1.80 m long, thickest at the handle, the back flat (facing the target, +Z), the belly round
        var m = LoftY(-0.90, 0.90, hi ? 48 : 16, hi ? 12 : 6, (t, i) =>
        {
            double u = Math.Abs(t * 2 - 1);
            double w = 0.016 * (1 - 0.55 * u * u) + 0.002, d = 0.018 * (1 - 0.6 * u) + 0.003;
            double a = 2 * Math.PI * i / (hi ? 12 : 6);
            double x = w * Math.Cos(a), z = Math.Sin(a) > 0 ? d * 0.25 * Math.Sin(a) : d * Math.Sin(a);
            // the stave is bent slightly when strung (brace height): it bows towards +Z at the tips
            z += 0.07 * (u * u);
            return (x, z, 0);
        }, R1, PartWood, true, true);
        // the string from tip to tip, 0.17 m behind the handle (brace height)
        var str = new List<Vector3d> { new(0, 0.885, 0.065), new(0, 0, -0.165 + 0.07 * 0), new(0, -0.885, 0.065) };
        m.Append(Shapes.Tube(new List<Vector3d> { str[0], Vector3d.Lerp(str[0], str[1], 0.5), str[1], Vector3d.Lerp(str[1], str[2], 0.5), str[2] }, 0.0014, 5, R3, new Color(PartString, 0, 0, 255)));
        m.Append(Wrap(-0.06, 0.06, 0.019, hi, R2));
        return m;
    }

    static KitMesh Arrow(bool hi, Vector3 at, double yaw)
    {
        var m = new KitMesh();
        // shaft 0.76 m (birch), a bodkin head, three fletchings (cut-out cards), a nock
        var path = new List<Vector3d> { new(0, 0, 0), new(0, 0.38, 0), new(0, 0.76, 0) };
        m.Append(Shapes.Tube(path, 0.0042, hi ? 8 : 5, R1, new Color(PartWood, 0, 0, 255), up: new Vector3d(0, 0, 1), caps: true));
        m.Append(LoftY(0.76, 0.84, 6, 4, (t, i) => { double r = 0.0055 * (1 - t) + 0.0003; double a = 2 * Math.PI * i / 4 + Math.PI / 4; return (r * Math.Cos(a), r * Math.Sin(a), t > 0.8 ? 1 : 0); }, R2, PartIron, true, true));
        for (int k = 0; k < 3; k++)
        {
            double a = 2 * Math.PI * k / 3;
            var dir = new Vector3((float)Math.Cos(a), 0, (float)Math.Sin(a));
            // a feather card: 0.13 m long, 0.014 m high, along y from 0.03 to 0.16
            int i0 = m.Add(dir * 0.004f + new Vector3(0, 0.03f, 0), R3.Map(0, 1), new Color(PartFeather, 0, 0, 255));
            int i1 = m.Add(dir * 0.018f + new Vector3(0, 0.05f, 0), R3.Map(1, 0.85), new Color(PartFeather, 0, 0, 255));
            int i2 = m.Add(dir * 0.018f + new Vector3(0, 0.15f, 0), R3.Map(1, 0.15), new Color(PartFeather, 0, 0, 255));
            int i3 = m.Add(dir * 0.004f + new Vector3(0, 0.16f, 0), R3.Map(0, 0), new Color(PartFeather, 0, 0, 255));
            m.Tri(i0, i1, i2); m.Tri(i0, i2, i3);
            m.Tri(i0, i2, i1); m.Tri(i0, i3, i2);
        }
        if (at != Vector3.Zero || yaw != 0) m.Transform(Matrix.CreateRotationY((float)yaw) * Matrix.CreateTranslation(at));
        return m;
    }

    static KitMesh Quiver(bool hi, uint seed)
    {
        // a leather tube 0.62 m with a stitched seam and a wooden base, arrows standing out of its mouth (fletchings up)
        var m = new KitMesh();
        var path = new List<Vector3d>();
        for (int i = 0; i <= 12; i++) path.Add(new Vector3d(0, 0.62 * i / 12, 0));
        m.Append(Shapes.Sweep(path, Shapes.Circle(0.045, 0.040, hi ? 18 : 8), true, R0, new Color(PartLeather, 0, 0, 255), up: new Vector3d(0, 0, 1),
            scale: i => 0.92 + 0.12 * i / 12.0, caps: false));
        for (int k = 0; k < (hi ? 9 : 4); k++)
        {
            double a = 2 * Math.PI * Rand(k, 1, 0, seed), r = 0.025 * Math.Sqrt(Rand(k, 2, 0, seed));
            var arrow = Arrow(hi, Vector3.Zero, Rand(k, 3, 0, seed) * Math.PI);
            // arrows point down into the quiver: flip, stand them with fletchings 0.18 m above the mouth
            arrow.Transform(Matrix.CreateRotationX(MathHelper.Pi) * Matrix.CreateRotationZ((float)(0.05 * Rand11(k, 4, 0, seed))) *
                Matrix.CreateTranslation((float)(r * Math.Cos(a)), 0.62f + 0.18f + 0.02f * (float)Rand(k, 5, 0, seed), (float)(r * Math.Sin(a))));
            m.Append(arrow);
        }
        return m;
    }

    static KitMesh Scabbard(bool hi)
    {
        // a flattened sheath following a 0.62 m blade, a throat band and a bronze chape at the tip
        var m = LoftY(-0.66, 0.0, hi ? 24 : 8, hi ? 16 : 8, (t, i) =>
        {
            double w = 0.028 + 0.004 * t, th = 0.010;
            if (t < 0.12) w *= 0.4 + 0.6 * t / 0.12;
            double a = 2 * Math.PI * i / (hi ? 16 : 8);
            return (w * Math.Cos(a), th * Math.Sin(a), 0);
        }, R0, PartLeather, true, true);
        m.Append(LoftY(-0.67, -0.58, 6, hi ? 16 : 8, (t, i) => { double w = (0.010 + 0.022 * t) * 1.08, th = 0.012; double a = 2 * Math.PI * i / (hi ? 16 : 8); return (w * Math.Cos(a), th * Math.Sin(a), 0); }, R2, PartFitting, true, false));
        m.Append(LoftY(-0.035, 0.0, 3, hi ? 16 : 8, (t, i) => { double w = 0.035, th = 0.013; double a = 2 * Math.PI * i / (hi ? 16 : 8); return (w * Math.Cos(a), th * Math.Sin(a), 0); }, R2, PartFitting, false, false));
        return m;
    }

    static KitMesh Sling(bool hi)
    {
        // two cords (0.55 m) hanging from the fist to a leather pouch with a stone in it
        var m = new KitMesh();
        foreach (double side in new[] { 1.0, -1.0 })
        {
            var path = new List<Vector3d>();
            for (int i = 0; i <= 14; i++) { double t = i / 14.0; path.Add(new Vector3d(side * 0.005 + side * 0.03 * Math.Sin(Math.PI * t * 0.9), -0.55 * t, 0.02 * Math.Sin(Math.PI * t))); }
            m.Append(Shapes.Tube(path, 0.0022, 5, R1, new Color(PartString, 0, 0, 255)));
        }
        var pouch = Shapes.Lathe(new List<Vector2d> { new(0, 0.02), new(0.03, 0.012), new(0.034, 0.0), new(0.03, -0.012), new(0, -0.022) }, hi ? 16 : 8, R3, new Color(PartLeather, 0, 0, 255));
        pouch.Transform(Matrix.CreateScale(1f, 0.7f, 0.8f) * Matrix.CreateTranslation(0, -0.57f, 0.02f));
        m.Append(pouch);
        return m;
    }

    static KitMesh Musket(bool hi)
    {
        var m = new KitMesh();
        // barrel: iron tube from the breech (y 0.38) to the muzzle (y 1.42), octagonal at the breech
        m.Append(LoftY(0.38, 1.42, hi ? 20 : 6, hi ? 16 : 8, (t, i) =>
        {
            double r = 0.0125 - 0.0025 * t;
            double a = 2 * Math.PI * i / (hi ? 16 : 8);
            return (r * Math.Cos(a), r * Math.Sin(a) + 0.005, 0);
        }, R0, PartIron, true, true));
        // stock: a lofted wooden shape from the butt (y 0) to the fore-end (y 1.30): deep at the butt, a slim wrist, a channel under the barrel
        m.Append(LoftY(0.0, 1.30, hi ? 40 : 10, hi ? 16 : 8, (t, i) =>
        {
            double a = 2 * Math.PI * i / (hi ? 16 : 8);
            double depth = t < 0.28 ? 0.075 - 0.13 * t : t < 0.36 ? 0.039 - 0.10 * (t - 0.28) : 0.030 - 0.010 * (t - 0.36);
            double half = t < 0.28 ? 0.022 : 0.017 - 0.004 * (t - 0.28);
            double top = t < 0.28 ? -0.010 + 0.02 * t : -0.004;
            double x = half * Math.Cos(a), z = top - depth * 0.5 + depth * 0.5 * Math.Sin(a);
            return (x, z, 0);
        }, R1, PartWood, true, true));
        // lock plate and the serpentine (match holder) on the right side
        m.Append(LoftY(0.30, 0.46, 3, 4, (t, i) => { double a = 2 * Math.PI * i / 4 + Math.PI / 4; return (-0.019 + 0.0015 * Math.Cos(a), 0.012 * Math.Sin(a) - 0.002, 0); }, R2, PartIron, true, true));
        var serp = new List<Vector3d> { new(-0.022, 0.40, -0.02), new(-0.026, 0.37, 0.0), new(-0.024, 0.35, 0.018) };
        m.Append(Shapes.Tube(serp, 0.003, 6, R2, new Color(PartIron, 0, 0, 255), caps: true));
        // butt plate, ramrod
        m.Append(LoftY(-0.004, 0.0, 1, hi ? 16 : 8, (t, i) => { double a = 2 * Math.PI * i / (hi ? 16 : 8); return (0.023 * Math.Cos(a), -0.047 + 0.040 * Math.Sin(a), 0); }, R2, PartIron, true, true));
        var rod = new List<Vector3d> { new(0, 0.55, -0.026), new(0, 1.0, -0.024), new(0, 1.40, -0.021) };
        m.Append(Shapes.Tube(rod, 0.0045, 6, R1, new Color(PartWood, 0, 0, 255), caps: true));
        return m;
    }

    // ---------------------------------------------------------------- maps

    static MapSet Paint(WeaponSpec s, KitMesh mesh)
    {
        var maps = new MapSet(s.Kind, s.MapSize, s.MapSize) { CavityStrength = 0.4f, CavityRadius = 3, CavityDepth = 0.0003f };
        uint seed = s.Seed * 2246822519u + 31u;
        double w = s.Wear;
        bool bronzeFit = s.Metal == "bronze" || s.Kind is "spear" or "xiphos";
        maps.PaintMesh(mesh, 1, sf =>
        {
            var p = sf.Position;
            int part = (int)Math.Round(sf.Mask.X * 255);
            double edge = sf.Mask.Y;
            switch (part)
            {
                case PartBlade:
                {
                    if (s.Metal == "bronze") return Materials.Bronze(Fbm(p.X * 80, p.Y * 20, 3, seed), 0.25 * w, 0, 0.5 + 0.5 * edge, 0, 0);
                    // forged steel: grinding marks along the blade, polished edges, rust spots away from the edge
                    double grind = Fbm(p.X * 600, p.Y * 8, 3, seed + 1);
                    var t = Materials.Iron(p.X * 4, p.Y, seed + 2, (0.10 + 0.35 * w) * (1 - edge), 0.45 + 0.5 * edge, 0, steel: true);
                    t.Rough = Math.Clamp(t.Rough - 0.08f * (float)edge + 0.05f * (float)(grind - 0.5), 0.08f, 1f);
                    t.Height += (float)(0.00001 * (grind - 0.5));
                    return t;
                }
                case PartIron:
                    return Materials.Iron(p.X * 3 + p.Z, p.Y * 3, seed + 3, 0.25 + 0.45 * w, 0.2 + 0.4 * edge, 0, steel: s.Kind != "axe");
                case PartFitting:
                    return bronzeFit ? Materials.Bronze(Fbm(p.X * 60, p.Y * 60, 3, seed + 4), 0.3 + 0.3 * w, 0.2 * w, 0.3, 0, 0)
                        : Materials.Iron(p.X * 3, p.Y * 3, seed + 5, 0.2 + 0.4 * w, 0.4, 0, steel: true);
                case PartWood:
                {
                    var light = s.Kind == "musket" ? new Vector3(0.30f, 0.16f, 0.07f) : new Vector3(0.48f, 0.35f, 0.21f);
                    var dark = s.Kind == "musket" ? new Vector3(0.12f, 0.055f, 0.025f) : new Vector3(0.26f, 0.17f, 0.09f);
                    // grain runs along the item's length (y); hands darken the grip zones
                    var t = Materials.Wood(Math.Atan2(p.Z, p.X) * 0.02 + p.X, p.Y, seed + 6, light, dark, rough: s.Kind == "musket" ? 0.45 : 0.65);
                    return t;
                }
                case PartLeather:
                    return Materials.Leather(p.X + p.Z * 0.7, p.Y, seed + 7, s.Kind is "quiver" or "scabbard" ? new Vector3(0.22f, 0.13f, 0.07f) : new Vector3(0.13f, 0.08f, 0.05f), 0.5);
                case PartString:
                    return Texel.Of(new Vector3(0.45f, 0.40f, 0.30f) * (float)(0.8 + 0.3 * Fbm(p.Y * 200, 0, 2, seed + 8)), 0.8f, 0f);
                case PartFeather:
                {
                    // a feather card: vane with barbs, alpha cut at the outline
                    double u = (sf.Uv.X - R3.U0) / R3.Width, v = (sf.Uv.Y - R3.V0) / R3.Height;
                    double barbs = 0.5 + 0.5 * Math.Sin((v * 90 + u * 30));
                    var col = Vector3.Lerp(new Vector3(0.55f, 0.50f, 0.42f), new Vector3(0.18f, 0.15f, 0.12f), (float)(0.4 * barbs + 0.3 * Fbm(u * 8, v * 40, 2, seed + 9)));
                    var t = Texel.Of(col, 0.75f, 0f);
                    t.Alpha = (float)(Smoothstep(1.0, 0.85, u + 0.25 * Math.Abs(v - 0.5) * 2) * Smoothstep(0.0, 0.05, u));
                    return t;
                }
                default: return Texel.Of(new Vector3(0.4f), 0.6f, 0f);
            }
        });
        return maps.Finish(4);
    }
}
