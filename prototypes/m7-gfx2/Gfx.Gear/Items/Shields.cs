using Gfx.Gear.Geometry;
using Gfx.Gear.Maps;
using Microsoft.Xna.Framework;
using static Gfx.Gear.Maps.Noise;

namespace Gfx.Gear.Items;

/// <summary>Parameters of a board shield (variation of one authored design per kind).</summary>
public sealed record ShieldSpec
{
    /// <summary>thureos (oval, central grip, ancient), round (central grip, iron boss), kite (strap, medieval), heater (strap, later medieval).</summary>
    public string Kind { get; init; } = "thureos";
    public uint Seed { get; init; } = 1;
    public int MapSize { get; init; } = 1024;
    public int Lod { get; init; }
    public string Field { get; init; } = "#7B2C24";
    public string Charge { get; init; } = "#D8D0BC";
    /// <summary>Painted device: spiral, wheel, crescent, chevron, saltire, cross, bend, pale, lozenges, none.</summary>
    public string Device { get; init; } = "spiral";
    public double Wear { get; init; } = 0.5;
}

/// <summary>
/// Board shields of the era kit, built as curved boards cut along their outline (a field clip, so the edge follows the
/// outline exactly), given thickness with a rounded edge, bound at the rim (rawhide or iron), with the period's
/// fittings: the thureos (Hellenistic oval: a wooden spine with a strip boss, felt-covered planks, central grip), the
/// round shield (planks, iron dome boss, central grip), the kite and the heater (curved, leather-covered, painted,
/// enarmes and guige straps for the forearm). One mesh and one 1024-texel atlas per shield. Generic historical forms;
/// heraldry is original geometric devices in the forces' colours. PRESENTATION ONLY.
/// </summary>
public static class Shields
{
    // atlas: face (left half), back (right, upper), edge strip, fittings
    static readonly UvRect FaceRect = new(0.00f, 0.00f, 0.50f, 1.00f);
    static readonly UvRect BackRect = new(0.50f, 0.00f, 1.00f, 0.80f);
    static readonly UvRect EdgeRect = new(0.50f, 0.81f, 1.00f, 0.85f);
    static readonly UvRect BindRect = new(0.50f, 0.86f, 1.00f, 0.90f);
    static readonly UvRect FitRect = new(0.50f, 0.91f, 1.00f, 1.00f);
    const int PartFace = 10, PartBack = 20, PartEdge = 30, PartBind = 40, PartBoss = 50, PartLeather = 60, PartWood = 70, PartRivet = 80;

    sealed class Board
    {
        public required List<Vector2d> Outline;
        public required Func<double, double, double> Curve;
        public double W, H, CenterY, Thickness = 0.012;
        public bool Strap, IronRim;
        public double MinX, MaxX, MinY, MaxY;
    }

    static Board Make(ShieldSpec s)
    {
        var o = new List<Vector2d>();
        Board b;
        switch (s.Kind)
        {
            case "round":
            {
                double r = 0.43;
                for (int i = 0; i < 128; i++) { double a = 2 * Math.PI * i / 128; o.Add(new Vector2d(r * Math.Sin(a), r * Math.Cos(a))); }
                b = new Board { Outline = o, Curve = (x, y) => 0.012 * (1 - (x * x + y * y) / (r * r)), Thickness = 0.010 };
                break;
            }
            case "kite":
            {
                // a rounded top and two long sides curving to a point
                for (int i = 0; i <= 40; i++) { double a = Math.PI * i / 40; o.Add(new Vector2d(0.27 * Math.Cos(a), 0.36 + 0.13 * Math.Sin(a))); }
                for (int i = 1; i <= 60; i++) { double t = i / 60.0; o.Add(new Vector2d(-0.27 * Math.Pow(1 - t, 0.85) * (1 - 0.15 * t * (1 - t)), 0.36 - 1.02 * t)); }
                for (int i = 59; i >= 1; i--) { double t = i / 60.0; o.Add(new Vector2d(0.27 * Math.Pow(1 - t, 0.85) * (1 - 0.15 * t * (1 - t)), 0.36 - 1.02 * t)); }
                b = new Board { Outline = o, Curve = (x, y) => 0.075 * (1 - Math.Pow(x / 0.29, 2)) + 0.01 * (1 - Math.Pow((y + 0.1) / 0.6, 2)), Thickness = 0.011, Strap = true };
                break;
            }
            case "heater":
            {
                // flat top, straight sides, quarter-ellipse arcs to the point; counter-clockwise from the point
                for (int i = 0; i <= 40; i++) { double t = 1 - i / 40.0; o.Add(new Vector2d(0.31 * Math.Cos(t * Math.PI / 2), 0.15 - 0.57 * Math.Sin(t * Math.PI / 2))); }
                o.Add(new Vector2d(0.31, 0.33)); o.Add(new Vector2d(-0.31, 0.33));
                for (int i = 0; i < 40; i++) { double t = i / 40.0; o.Add(new Vector2d(-0.31 * Math.Cos(t * Math.PI / 2), 0.15 - 0.57 * Math.Sin(t * Math.PI / 2))); }
                b = new Board { Outline = DedupeClose(o), Curve = (x, y) => 0.06 * (1 - Math.Pow(x / 0.33, 2)), Thickness = 0.012, Strap = true };
                break;
            }
            default: // thureos
            {
                for (int i = 0; i < 128; i++) { double a = 2 * Math.PI * i / 128; o.Add(new Vector2d(0.31 * Math.Sin(a), 0.57 * Math.Cos(a))); }
                b = new Board { Outline = o, Curve = (x, y) => 0.085 * (1 - Math.Pow(x / 0.32, 2)), Thickness = 0.012 };
                break;
            }
        }
        b.MinX = b.Outline.Min(p => p.X); b.MaxX = b.Outline.Max(p => p.X); b.MinY = b.Outline.Min(p => p.Y); b.MaxY = b.Outline.Max(p => p.Y);
        b.W = b.MaxX - b.MinX; b.H = b.MaxY - b.MinY; b.CenterY = 0.5 * (b.MinY + b.MaxY);
        b.IronRim = s.Kind is "round" or "thureos";
        return b;
    }

    static List<Vector2d> DedupeClose(List<Vector2d> pts)
    {
        var o = new List<Vector2d>();
        foreach (var p in pts) if (o.Count == 0 || (o[^1] - p).Length > 1e-5) o.Add(p);
        if ((o[0] - o[^1]).Length < 1e-5) o.RemoveAt(o.Count - 1);
        return o;
    }

    /// <summary>Uniform planar mapping of the board's bounding box into a rect (centred, aspect kept).</summary>
    static Func<double, double, Vector2> Planar(Board b, UvRect r, bool mirror)
    {
        double s = Math.Min(r.Width / b.W, r.Height / b.H) * 0.98;
        double cu = 0.5 * (r.U0 + r.U1), cv = 0.5 * (r.V0 + r.V1);
        double cx = 0.5 * (b.MinX + b.MaxX), cy = b.CenterY;
        return (x, y) => new Vector2((float)(cu + (mirror ? -(x - cx) : (x - cx)) * s), (float)(cv - (y - cy) * s));
    }

    public static KitItem Build(ShieldSpec s)
    {
        var b = Make(s);
        var item = new KitItem
        {
            Id = $"{s.Kind}#{s.Seed}L{s.Lod}", Kind = "shield." + s.Kind, Era = s.Kind is "thureos" or "round" ? "ancient" : "medieval",
            Label = s.Kind switch { "thureos" => "Thureos (oval, central grip)", "round" => "Round shield (central grip, iron boss)", "kite" => "Kite shield (strapped)", _ => "Heater shield (strapped)" },
        };
        var mesh = BuildMesh(s, b);
        item.Parts.Add(new KitPart { Name = "shield", Mesh = mesh, Maps = "shield" });
        if (s.MapSize > 0) item.Maps["shield"] = Paint(s, b, mesh);
        // frames
        double zc = b.Curve(0, 0);
        if (b.Strap)
        {
            // enarmes: the forearm's back rests on the board at the forearm strap; the fist closes on the hand strap,
            // 0.215 m further along the forearm (towards the shield's -X), thumb up
            double fy = b.CenterY + 0.03;
            item.Frames["forearm"] = KitItem.MakeFrame(new Vector3((float)StrapForearmX, (float)fy, (float)(b.Curve(StrapForearmX, fy) - b.Thickness - 0.004)), -Vector3.UnitX, Vector3.UnitZ);
            item.Frames["grip"] = KitItem.MakeFrame(new Vector3((float)StrapHandX, (float)fy, (float)(b.Curve(StrapHandX, fy) - b.Thickness - 0.003 - 0.050)), Vector3.UnitY, -Vector3.UnitZ);
            item.GripRadius = 0.004f;
        }
        else
        {
            // central grip: the bar across the hand hole along item X; the board turns about it (see KitHold)
            item.Frames["grip"] = KitItem.MakeFrame(new Vector3(0, (float)b.CenterY, (float)(zc - b.Thickness - BarDepth - 0.001)), -Vector3.UnitX, -Vector3.UnitZ);
            item.GripRadius = 0.013f;
        }
        item.Frames["hang"] = KitItem.MakeFrame(new Vector3(0, (float)b.CenterY, (float)(zc - b.Thickness - 0.03)), Vector3.UnitY, Vector3.UnitZ);
        item.CentreOfMass = new Vector3(0, (float)b.CenterY, (float)(zc * 0.5));
        return item;
    }

    static KitMesh BuildMesh(ShieldSpec s, Board b)
    {
        bool hi = s.Lod == 0;
        int nu = hi ? 40 : 14, nv = (int)Math.Round(nu * b.H / b.W);
        var face = Planar(b, FaceRect, false);
        var back = Planar(b, BackRect, true);
        double inset = b.IronRim || b.Strap ? 0.004 : 0.0;
        var grid = Shapes.Grid(nu, nv, (u, v) =>
        {
            double x = b.MinX - 0.01 + (b.W + 0.02) * u, y = b.MaxY + 0.01 - (b.H + 0.02) * v;
            return new Vector3d(x, y, b.Curve(x, y));
        }, UvRect.Full, new Color(PartFace, 0, 0, 255));
        bool hole = !b.Strap;
        var clipped = Shapes.Clip(grid, (i, p) =>
        {
            double f = Shapes.PolygonSdf(b.Outline, new Vector2d(p.X, p.Y)) + inset;
            if (hole) { double hx = p.X / 0.060, hy = (p.Y - b.CenterY) / 0.070; f = Math.Max(f, (1 - Math.Sqrt(hx * hx + hy * hy)) * 0.060); }
            return f;
        });
        for (int i = 0; i < clipped.VertexCount; i++) { var p = clipped.Positions[i]; clipped.Uvs[i] = face(p.X, p.Y); }
        clipped.ComputeNormals(60f);
        Shapes.OrientOutward(clipped, new Vector3(0, (float)b.CenterY, -1f));
        clipped.ComputeNormals(60f);
        var solid = Shapes.Solidify(clipped, b.Thickness, null, EdgeRect, hi ? 4 : 2, 0, 4.0,
            innerUv: uv => uv, innerMask: new Color(PartBack, 0, 0, 255));
        // remap the inner surface's UVs to the back rect (mirrored planar), using positions
        int nOuter = clipped.VertexCount;
        for (int i = nOuter; i < 2 * nOuter; i++) { var p = solid.Positions[i]; solid.Uvs[i] = back(p.X, p.Y); }
        var m = new KitMesh("shield");
        m.Append(solid);
        // rim binding along the outline (rawhide or iron edging wrapping the edge)
        var loop = Shapes.BoundaryLoops(clipped).OrderByDescending(l => LoopLength(clipped, l)).First();
        var path = new List<Vector3d>();
        foreach (var vi in loop) path.Add(new Vector3d(clipped.Positions[vi] - clipped.Normals[vi] * (float)(b.Thickness * 0.5)));
        var sec = Aspis.RoundedRect(b.Thickness + 0.004, b.IronRim ? 0.016 : 0.022, 0.0016, hi ? 2 : 1);
        // the section's x runs along the frame normal (the surface normal), y along the binormal (in the board's plane)
        var n0 = new Vector3d(clipped.Normals[loop[0]]);
        var bind = Shapes.Sweep(path, sec.Select(q => new Vector2d(q.X, q.Y - (b.IronRim ? 0.005 : 0.007))).ToList(), true, BindRect, new Color(PartBind, 0, 0, 255),
            up: n0, closedPath: true, vRepeat: 1.0);
        m.Append(bind);
        // fittings
        double zc = b.Curve(0, b.CenterY);
        if (s.Kind == "round")
        {
            // iron dome boss with a flange, five rivets
            var prof = new List<Vector2d> { new(0, 0.072), new(0.030, 0.068), new(0.055, 0.052), new(0.068, 0.030), new(0.072, 0.012), new(0.074, 0.008), new(0.098, 0.006), new(0.100, 0.0) };
            var boss = Shapes.Lathe(Shapes.Fillet(prof, new[] { 0, 0.01, 0.01, 0.01, 0.004, 0.003, 0.0, 0 }, 3), hi ? 48 : 16, FitRect, new Color(PartBoss, 0, 0, 255),
                // the right half of the fittings rectangle (the grip bar's wood takes the left half)
                uv: (p, f, t) => Sub(FitRect, 0.5, 0, 1, 1).Map(0.5 + p.X / 0.22, 0.5 + p.Z / 0.22));
            boss.Transform(Matrix.CreateRotationX(MathHelper.PiOver2) * Matrix.CreateTranslation(0, (float)b.CenterY, (float)zc));
            m.Append(boss);
        }
        if (s.Kind == "thureos")
        {
            // the spine: a wooden rib along the long axis, swelling into a strip boss (iron) at the centre
            var path2 = new List<Vector3d>();
            int n = hi ? 48 : 16;
            for (int i = 0; i <= n; i++) { double y = b.MaxY - 0.03 - (b.H - 0.06) * i / n; path2.Add(new Vector3d(0, y, b.Curve(0, y) - 0.002)); }
            var ribSec = new List<Vector2d>();
            for (int i = 0; i <= 10; i++) { double a = Math.PI * i / 10; ribSec.Add(new Vector2d(0.016 * Math.Sin(a), -0.030 * Math.Cos(a))); }
            var rib = Shapes.Sweep(path2, ribSec, false, Sub(FitRect, 0, 0, 0.5, 1), new Color(PartWood, 0, 0, 255), up: new Vector3d(0, 0, 1),
                scale: i => { double y = path2[i].Y - b.CenterY; return 1 + 0.9 * Math.Exp(-y * y / (0.08 * 0.08)); });
            m.Append(rib);
            // iron strip boss over the rib's swelling: a narrow plate with rounded ends, nailed
            var plate = new List<Vector3d>();
            for (int i = 0; i <= 16; i++) { double y = b.CenterY + 0.11 - 0.22 * i / 16; plate.Add(new Vector3d(0, y, b.Curve(0, y) + 0.020 * Math.Exp(-Math.Pow((y - b.CenterY) / 0.08, 2)) + 0.004)); }
            var strip = Shapes.Sweep(plate, Aspis.RoundedRect(0.050, 0.003, 0.0012, 1), true, Sub(FitRect, 0.5, 0, 1, 1), new Color(PartBoss, 0, 0, 255), up: new Vector3d(1, 0, 0));
            m.Append(strip);
        }
        if (b.Strap)
        {
            // enarmes: a forearm strap and a hand strap, both across the forearm (vertical), standing off the back;
            // the guige: a long carrying strap near the top
            double fy = b.CenterY + 0.03;
            m.Append(Strap(b, StrapForearmX, fy + 0.07, StrapForearmX, fy - 0.07, 0.030, hi, 0.072));
            m.Append(Strap(b, StrapHandX, fy + 0.06, StrapHandX, fy - 0.06, 0.024, hi, 0.052));
            m.Append(Strap(b, 0.22, b.MaxY - 0.12, -0.22, b.MaxY - 0.12, 0.030, hi, 0.07));
        }
        else
        {
            // central grip bar across the hand hole (wood), just behind the board
            var bar = new List<Vector3d>();
            for (int i = 0; i <= 8; i++) { double x = -0.075 + 0.15 * i / 8; bar.Add(new Vector3d(x, b.CenterY, b.Curve(x, b.CenterY) - b.Thickness - BarDepth * Math.Sin(Math.PI * i / 8) - 0.001)); }
            m.Append(Shapes.Tube(bar, 0.013, hi ? 10 : 6, Sub(FitRect, 0, 0, 0.5, 1), new Color(PartWood, 0, 0, 255), up: new Vector3d(0, 1, 0), caps: true));
        }
        return m.Finish(48f);
    }

    static UvRect Sub(UvRect r, double u0, double v0, double u1, double v1) =>
        new((float)(r.U0 + r.Width * u0), (float)(r.V0 + r.Height * v0), (float)(r.U0 + r.Width * u1), (float)(r.V0 + r.Height * v1));

    static List<Vector3d> Smooth(List<Vector3d> pts, int sub)
    {
        // Catmull-Rom through the points
        var o = new List<Vector3d>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[Math.Max(0, i - 1)]; var p1 = pts[i]; var p2 = pts[i + 1]; var p3 = pts[Math.Min(pts.Count - 1, i + 2)];
            for (int k = 0; k < sub; k++)
            {
                double t = (double)k / sub, t2 = t * t, t3 = t2 * t;
                o.Add((p1 * 2 + (p2 - p0) * t + (p0 * 2 - p1 * 5 + p2 * 4 - p3) * t2 + (p1 * 3 - p0 - p2 * 3 + p3) * t3) * 0.5);
            }
        }
        o.Add(pts[^1]);
        return o;
    }

    const double StrapForearmX = 0.05, StrapHandX = -0.165, BarDepth = 0.010;

    static double LoopLength(KitMesh m, List<int> loop)
    {
        double l = 0;
        for (int i = 0; i < loop.Count; i++) l += (m.Positions[loop[(i + 1) % loop.Count]] - m.Positions[loop[i]]).Length();
        return l;
    }

    /// <summary>
    /// A leather strap on the back from (x0, y0) to (x1, y1), standing off by <paramref name="loft"/> in the middle (an
    /// arch the forearm or the fist goes through).
    /// </summary>
    static KitMesh Strap(Board b, double x0, double y0, double x1, double y1, double w, bool hi, double loft)
    {
        var path = new List<Vector3d>();
        int n = hi ? 16 : 6;
        for (int i = 0; i <= n; i++)
        {
            double t = (double)i / n;
            double arch = Math.Pow(Math.Sin(Math.PI * t), 0.6);
            double x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
            path.Add(new Vector3d(x, y, b.Curve(x, y) - b.Thickness - 0.003 - loft * arch));
        }
        var dir = new Vector3d(x1 - x0, y1 - y0, 0).Normalized();
        var up = Vector3d.Cross(new Vector3d(0, 0, 1), dir);
        return Shapes.Sweep(path, Aspis.RoundedRect(w, 0.004, 0.0015, 1), true, Sub(FitRect, 0, 0, 0.5, 1), new Color(PartLeather, 0, 0, 255), up: up);
    }

    // ---------------------------------------------------------------- devices (original geometric heraldry)

    public static double Device(string kind, double x, double y, double size)
    {
        double aa = 0.002;
        double r = Math.Sqrt(x * x + y * y), a = Math.Atan2(y, x);
        switch (kind)
        {
            case "spiral":
            {
                // three arms spiralling out from a hub (a common ancient motif, drawn here as an original figure)
                double best = Smoothstep(0.045 * size + aa, 0.045 * size - aa, r);
                for (int k = 0; k < 3; k++)
                {
                    double a0 = 2 * Math.PI * k / 3;
                    // arm centre line: a logarithmic-ish spiral r(theta)
                    double th = Math.IEEERemainder(a - a0, 2 * Math.PI);
                    for (int turn = 0; turn < 2; turn++)
                    {
                        double tt = th + 2 * Math.PI * turn;
                        if (tt < 0) continue;
                        double rc = 0.045 * size + 0.075 * size * tt;
                        double wdt = 0.022 * size * (1 - 0.18 * tt);
                        if (tt > 3.6 || wdt <= 0) continue;
                        best = Math.Max(best, Smoothstep(aa, -aa, Math.Abs(r - rc) - wdt) * Smoothstep(3.6, 3.3, tt));
                    }
                }
                return best;
            }
            case "wheel":
            {
                double rim = Smoothstep(0.17 * size + aa, 0.17 * size - aa, r) * Smoothstep(0.135 * size - aa, 0.135 * size + aa, r);
                double hub = Smoothstep(0.04 * size + aa, 0.04 * size - aa, r);
                double sp = 0;
                for (int k = 0; k < 6; k++) { double da = Math.IEEERemainder(a - Math.PI * k / 3, 2 * Math.PI); sp = Math.Max(sp, Smoothstep(aa, -aa, Math.Abs(da) * r - 0.012 * size) * Smoothstep(0.14 * size, 0.135 * size, r)); }
                return Math.Max(rim, Math.Max(hub, sp));
            }
            case "crescent":
            {
                double outer = Smoothstep(0.16 * size + aa, 0.16 * size - aa, r);
                double ix = x - 0.06 * size, iy = y + 0.01 * size;
                double inner = Smoothstep(0.14 * size + aa, 0.14 * size - aa, Math.Sqrt(ix * ix + iy * iy));
                return outer * (1 - inner);
            }
            case "chevron": return Smoothstep(aa, -aa, Math.Abs(y + 0.04 - 0.55 * Math.Abs(x)) - 0.045);
            case "saltire": return Math.Max(Smoothstep(aa, -aa, Math.Abs(y - x * 1.6) / 1.9 - 0.035), Smoothstep(aa, -aa, Math.Abs(y + x * 1.6) / 1.9 - 0.035));
            case "cross": return Math.Max(Smoothstep(aa, -aa, Math.Abs(x) - 0.040), Smoothstep(aa, -aa, Math.Abs(y - 0.06) - 0.040));
            case "bend": return Smoothstep(aa, -aa, Math.Abs(y - x * 1.1) / 1.49 - 0.06);
            case "pale": return Smoothstep(aa, -aa, Math.Abs(x) - 0.075);
            case "lozenges":
            {
                double gx = x / 0.10, gy = y / 0.14;
                double fx = gx - Math.Round(gx), fy = gy - Math.Round(gy);
                return Smoothstep(0.02, -0.02, Math.Abs(fx) + Math.Abs(fy) * 0.75 - 0.33);
            }
            default: return 0;
        }
    }

    // ---------------------------------------------------------------- maps

    static MapSet Paint(ShieldSpec s, Board b, KitMesh mesh)
    {
        var maps = new MapSet(s.Kind, s.MapSize, s.MapSize) { CavityStrength = 0.5f, CavityRadius = Math.Max(2, s.MapSize / 300), CavityDepth = 0.0006f };
        uint seed = s.Seed * 2654435761u + 17u;
        var field = MapSet.Hex(s.Field); var charge = MapSet.Hex(s.Charge);
        double w = s.Wear;
        // impact sites (blows land on the upper half and towards the side facing the enemy)
        var hits = Enumerable.Range(0, 3 + (int)(4 * w)).Select(i => (x: Rand11(i, 1, 0, seed) * b.W * 0.4, y: b.CenterY + Rand11(i, 2, 0, seed) * b.H * 0.4, r: 0.010 + 0.014 * Rand(i, 3, 0, seed))).ToArray();
        maps.PaintMesh(mesh, 1, sf =>
        {
            int part = (int)Math.Round(sf.Mask.X * 255);
            var p = sf.Position;
            double x = p.X, y = p.Y;
            switch (part)
            {
                case PartFace:
                {
                    // ground: felt or leather over planks (thureos, kite, heater) or bare planks (round)
                    Texel ground = s.Kind == "round"
                        ? Materials.Wood(x + 1, y, seed + 1, new Vector3(0.42f, 0.30f, 0.17f), new Vector3(0.22f, 0.14f, 0.07f), board: 0.13)
                        : Materials.Leather(x, y, seed + 2, new Vector3(0.32f, 0.22f, 0.13f), 0.2);
                    double edgeD = -Shapes.PolygonSdf(b.Outline, new Vector2d(x, y));
                    double hit = 0;
                    foreach (var (hx, hy, hr) in hits) hit = Math.Max(hit, Math.Exp(-((x - hx) * (x - hx) + (y - hy) * (y - hy)) / (hr * hr)));
                    // paint loss: fine flakes in the field, worn through along the rim and where blows landed
                    double chipN = 0.55 * Fbm(x * 130, y * 130, 4, seed + 3) + 0.45 * Fbm(x * 22, y * 22, 3, seed + 33)
                        + 0.35 * Smoothstep(0.035, 0.0, edgeD) + 0.40 * hit;
                    double chip = Smoothstep(0.80 - 0.12 * w, 0.83 - 0.12 * w, chipN);
                    double dev = Device(s.Device, x, y - b.CenterY - (s.Kind is "kite" ? 0.12 : 0.0), s.Kind is "kite" or "heater" ? 1.6 : 2.2);
                    var col = Vector3.Lerp(field, charge, (float)dev);
                    var t = Materials.Paint(ground, col, x, y, seed + 4, chip);
                    // sword cuts: short gouges through the paint near the top and the leading edge
                    double cut = 0;
                    for (int i = 0; i < 3 + (int)(5 * w); i++)
                    {
                        double cx = Rand11(i, 5, 0, seed) * b.W * 0.4, cy = b.MaxY - 0.05 - b.H * 0.35 * Rand(i, 6, 0, seed);
                        double ang = Rand11(i, 7, 0, seed) * 0.8 - 0.4, len = 0.03 + 0.07 * Rand(i, 8, 0, seed);
                        double ex = Math.Cos(ang), ey = Math.Sin(ang);
                        double along = (x - cx) * ex + (y - cy) * ey, across = -(x - cx) * ey + (y - cy) * ex;
                        if (Math.Abs(along) < len) cut = Math.Max(cut, Math.Exp(-across * across / (0.0012 * 0.0012)) * (1 - Math.Abs(along) / len));
                    }
                    t.Height -= (float)(0.0012 * cut);
                    if (cut > 0.3) { t.Albedo = Vector3.Lerp(t.Albedo, new Vector3(0.45f, 0.33f, 0.2f), (float)cut); t.Rough = 0.8f; }
                    double low = Smoothstep(b.CenterY - b.H * 0.15, b.MinY, y);
                    double dust = w * (0.4 * low * Smoothstep(0.4, 0.75, Fbm(x * 30, y * 30, 3, seed + 9)) + 0.25 * low * low);
                    Materials.Dusty(ref t, Saturate(dust));
                    return t;
                }
                case PartBack:
                    return Materials.Wood(x, y, seed + 10, new Vector3(0.40f, 0.29f, 0.17f), new Vector3(0.20f, 0.13f, 0.07f), board: 0.11, rough: 0.75);
                case PartEdge:
                    return Materials.Wood(x * 3, y, seed + 11, new Vector3(0.36f, 0.25f, 0.14f), new Vector3(0.18f, 0.11f, 0.06f));
                case PartBind:
                {
                    if (b.IronRim) return Materials.Iron(x + y * 0.3, y * 2 + x, seed + 12, 0.35 + 0.5 * w, 0.2);
                    // rawhide binding, stitched: a row of stitch holes along the binding
                    var t = Materials.Leather(x, y, seed + 13, new Vector3(0.36f, 0.26f, 0.15f), 0.5);
                    double st = Math.Sin((sf.Uv.X - BindRect.U0) / BindRect.Width * Math.PI * 2 * 260);
                    double row = Math.Abs((sf.Uv.Y - BindRect.V0) / BindRect.Height - 0.75);
                    if (st > 0.6 && row < 0.08) { t.Height -= 0.0003f; t.Albedo *= 0.5f; }
                    return t;
                }
                case PartBoss: return Materials.Iron(x * 2, y * 2 + p.Z, seed + 14, 0.25 + 0.5 * w, 0.35);
                case PartLeather: return Materials.Leather(x * 2, y * 2, seed + 15, new Vector3(0.20f, 0.12f, 0.065f), 0.6);
                case PartWood:
                {
                    var wd = Materials.Wood(x * 0.5, y * 3 + p.Z, seed + 16, new Vector3(0.42f, 0.30f, 0.17f), new Vector3(0.24f, 0.16f, 0.08f));
                    if (s.Kind != "thureos" || p.Z < b.Curve(x, y) - 0.004) return wd;   // the grip bar stays bare wood
                    // the spine is painted with the field, worn to the wood on its crest
                    double crest = Smoothstep(0.012, 0.0, Math.Abs(x));
                    double chip = Smoothstep(0.72, 0.78, Fbm(x * 200, y * 60, 3, seed + 34) + 0.08 * crest);
                    return Materials.Paint(wd, field, x, y, seed + 35, chip);
                }
                default: return Texel.Of(new Vector3(0.5f), 0.6f, 0f);
            }
        });
        return maps.Finish(4);
    }
}
