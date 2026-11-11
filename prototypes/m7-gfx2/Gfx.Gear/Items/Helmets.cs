using Gfx.Gear.Geometry;
using Gfx.Gear.Maps;
using Gfx.Gear.Rig;
using Microsoft.Xna.Framework;
using static Gfx.Gear.Maps.Noise;

namespace Gfx.Gear.Items;

/// <summary>Parameters of a helmet (fitted to one identity's head measurements).</summary>
public sealed record HelmetSpec
{
    /// <summary>corinthian (bronze, crested), montefortino (bronze, knob, cheek plates), nasal (iron, conical, banded), kettle (iron, brimmed), greathelm (iron, flat-topped).</summary>
    public string Kind { get; init; } = "corinthian";
    public uint Seed { get; init; } = 1;
    public int MapSize { get; init; } = 1024;
    public int Lod { get; init; }
    public double Wear { get; init; } = 0.45;
    /// <summary>Crest colour (sRGB hex) for crested helmets; null = no crest.</summary>
    public string? Crest { get; init; } = "#7E2A20";
}

/// <summary>
/// Helmets of the era kit, each a metal shell fitted to the wearer's head (measured from the identity's body: temple
/// width, skull length, crown and brow) plus padding, cut by smooth fields so every edge (eye openings, cheek guards,
/// rims) is one clean curve, and given thickness with a rolled edge: the Corinthian (bronze, closed face with eye
/// openings and a nasal, flared nape, a horsehair crest on a holder), the Montefortino (bronze bowl, top knob, hinged
/// cheek plates), the nasal helmet (iron cone with riveted bands and a nose guard), the kettle hat (iron bowl, wide
/// brim) and the great helm (iron, flat top, vision slits and breaths). Item space is the head frame (socket "head").
/// Generic historical forms; no game asset or likeness. PRESENTATION ONLY.
/// </summary>
public static class Helmets
{
    const int PartShell = 10, PartEdge = 20, PartInner = 30, PartFitting = 40, PartRivet = 50;
    static readonly UvRect ShellRect = new(0f, 0f, 1f, 0.70f);
    static readonly UvRect InnerRect = new(0f, 0.70f, 0.5f, 0.88f);
    static readonly UvRect EdgeRect = new(0f, 0.89f, 1f, 0.93f);
    static readonly UvRect FitRect = new(0f, 0.94f, 1f, 1f);

    /// <summary>The shell's horizontal section at height y (head frame): half width, front and back reach (m).</summary>
    sealed class Shape
    {
        public double HalfW, Front, Back, Top, Bottom;
        public Func<double, double, double>? Extra; // (azimuth, y) -> extra radius (flares, cones)
        public Func<double, double> SectionScale = _ => 1.0;
        /// <summary>0 = a rounded dome above the brow, 1 = a straight cone to the apex.</summary>
        public double Cone;
        public Vector3d At(double az, double y)
        {
            // y in [Bottom, Top]; above 0 the dome closes like an ellipsoid (or a cone); below, the sides hang with a taper
            double s = y > 0 ? (1 - Cone) * Math.Sqrt(Math.Max(0, 1 - Math.Pow(y / Top, 2))) + Cone * Math.Max(0, 1 - Math.Pow(y / Top, 1.25)) : SectionScale(y);
            double fz = Math.Cos(az) >= 0 ? Front : Back;
            double x = HalfW * Math.Sin(az) * s, z = fz * Math.Cos(az) * s;
            double e = Extra?.Invoke(az, y) ?? 0;
            if (e != 0)
            {
                double l = Math.Sqrt(x * x + z * z);
                if (l > 1e-9) { x += x / l * e; z += z / l * e; }
            }
            return new Vector3d(x, y, z);
        }
    }

    public static KitItem Build(HelmetSpec spec, BodyMeasure m)
    {
        double pad = 0.014;
        // head frame: origin at the socket "head" = (0, eye + 0.03, skull mid-depth)
        double cz = m.HeadCentre.Z, cy = m.HeadCentre.Y;
        var sh = new Shape
        {
            HalfW = m.TempleHalfWidth + pad, Front = m.BrowZ - cz + pad, Back = cz - m.SkullBackZ + pad, Top = m.CrownY - cy + pad,
        };
        var item = new KitItem
        {
            Id = $"helmet.{spec.Kind}#{spec.Seed}L{spec.Lod}", Kind = "helmet." + spec.Kind,
            Era = spec.Kind is "corinthian" or "montefortino" ? "ancient" : "medieval",
            Label = spec.Kind switch
            {
                "corinthian" => "Corinthian helmet (bronze, crested)", "montefortino" => "Montefortino helmet (bronze, cheek plates)",
                "nasal" => "Nasal helmet (iron, banded)", "kettle" => "Kettle hat (iron)", _ => "Great helm (iron)",
            },
        };
        bool bronze = spec.Kind is "corinthian" or "montefortino";
        double eye = m.EyeY - cy;           // eye line in the head frame (about -0.03)
        Func<double, double, double> keep;  // (azimuth, y) -> field, <= 0 keeps
        double bottom;
        switch (spec.Kind)
        {
            case "corinthian":
            {
                bottom = m.ChinY - cy - 0.005;
                sh.Top += 0.006;
                sh.Extra = (az, y) =>
                {
                    double back = Smoothstep(1.9, 2.6, Math.Abs(az));
                    return 0.004 + back * Smoothstep(eye + 0.01, eye - 0.07, y) * 0.022     // the nape flares out
                        + (1 - back) * Smoothstep(eye - 0.01, eye - 0.08, y) * 0.004;       // cheek guards stand off the jaw
                };
                sh.SectionScale = y => 1 - 0.10 * Smoothstep(eye, bottom, y);
                keep = (az, y) =>
                {
                    double a = Math.Abs(az);
                    // lower edge: deep cheek guards at the sides, an arch over the ear's line, a flared nape behind
                    double low = bottom + 0.015 * Smoothstep(0.9, 1.5, a) + 0.045 * Smoothstep(1.45, 1.75, a) * (1 - Smoothstep(2.0, 2.5, a)) + 0.04 * Smoothstep(2.2, 2.9, a);
                    double f = low - y;
                    // the face opening (front): almond eye openings either side of the nasal, joined below it to a
                    // slot that runs down between the cheek guards and widens towards the chin
                    if (Math.Cos(az) > 0.15)
                    {
                        double fx = Math.Abs(Math.Sin(az) * sh.HalfW), fy = y;
                        double ex = (fx - 0.034) / 0.027, ey = (fy - eye - 0.002) / 0.0145;
                        double eyeSdf = (Math.Sqrt(ex * ex + ey * ey) - 1) * 0.0145;
                        double slotHalf = 0.011 + 0.022 * Smoothstep(eye - 0.045, bottom, fy);
                        double slot = Math.Max(fx - slotHalf, fy - (eye - 0.040));
                        double join = Math.Max(fx - 0.019, Math.Max(fy - (eye - 0.004), (eye - 0.047) - fy));
                        double open = Math.Min(eyeSdf, Math.Min(slot, join));
                        double nasal = Math.Max(fx - 0.0085, (eye - 0.052) - fy);       // < 0 inside the nasal
                        open = Math.Max(open, -nasal);
                        f = Math.Max(f, -open);
                    }
                    return f;
                };
                break;
            }
            case "montefortino":
                bottom = eye + 0.008;
                sh.Top += 0.010;
                sh.Extra = (az, y) => 0.003 + 0.016 * Smoothstep(2.2, 3.1, Math.Abs(az)) * Smoothstep(eye + 0.05, eye + 0.008, y);
                keep = (az, y) => (bottom + 0.002 * Math.Cos(az)) - y;
                break;
            case "nasal":
                bottom = eye + 0.022;
                sh.Top += 0.045; sh.Cone = 0.75;                                                   // a tall conical crown
                sh.Extra = (az, y) => 0.004 * (1 - Math.Max(0, y / sh.Top));
                keep = (az, y) => bottom - y;
                break;
            case "kettle":
                bottom = eye + 0.030;
                sh.Extra = (az, y) => 0.006 + 0.012 * Math.Pow(Math.Max(0, y / sh.Top), 3);
                keep = (az, y) => bottom - y;
                break;
            default: // greathelm
                bottom = m.ChinY - cy - 0.025;
                sh.Top += 0.004;
                sh.Extra = (az, y) => 0.012 + 0.010 * Smoothstep(eye, bottom, y);
                sh.SectionScale = _ => 1.0;
                keep = (az, y) =>
                {
                    double f = bottom - y;
                    if (Math.Cos(az) > 0.3)
                    {
                        double fx = Math.Sin(az) * sh.HalfW;
                        // two vision slits either side of a central strip; breaths on the right cheek
                        double slit = Math.Max(Math.Abs(y - (eye + 0.004)) - 0.006, Math.Abs(Math.Abs(fx) - 0.045) - 0.034);
                        double breath = 9;
                        for (int i = 0; i < 6; i++)
                            for (int j = 0; j < 3; j++)
                            {
                                double bx = -0.035 - 0.010 * j, by = eye - 0.045 - 0.013 * i;
                                breath = Math.Min(breath, Math.Sqrt((fx - bx) * (fx - bx) + (y - by) * (y - by)) - 0.0028);
                            }
                        f = Math.Max(f, -Math.Min(slit, breath));
                    }
                    return f;
                };
                break;
        }
        sh.Bottom = bottom;
        if (spec.Kind == "greathelm") sh.Top = Math.Min(sh.Top, 0.105);
        var mesh = BuildShell(spec, sh, keep);
        if (spec.Kind == "greathelm") mesh.Append(GreatHelmTop(spec, sh));
        mesh.Append(Fittings(spec, sh, m, eye));
        mesh.Finish(spec.Kind == "greathelm" ? 40f : 55f);
        item.Parts.Add(new KitPart { Name = "helmet", Mesh = mesh, Maps = "helmet" });
        if (spec.Crest != null && spec.Kind is "corinthian" or "montefortino")
            item.Parts.Add(new KitPart { Name = "crest", Mesh = Crest(spec, sh), Material = "hair", HairColour = spec.Crest });
        if (spec.MapSize > 0) item.Maps["helmet"] = Paint(spec, mesh, bronze);
        item.Frames["head"] = Matrix.Identity;
        _covers[item.Id] = (sh, keep, bottom);
        return item;
    }

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (Shape sh, Func<double, double, double> keep, double bottom)> _covers = new();

    /// <summary>
    /// True when a point (item space = head frame) lies inside the helmet: within the shell's section at its height
    /// and not in an opening. Used to hide the wearer's hair under it.
    /// </summary>
    public static bool Covers(KitItem item, Vector3 p)
    {
        if (!_covers.TryGetValue(item.Id, out var c)) return false;
        double y = p.Y;
        if (y < c.bottom - 0.004 || y > c.sh.Top + 0.02) return false;
        double az = Math.Atan2(p.X, p.Z);
        if (c.keep(az, y) > 0.002) return false;                 // in an opening or below the edge
        var q = c.sh.At(az, Math.Min(y, c.sh.Top - 1e-4));
        double rShell = Math.Sqrt(q.X * q.X + q.Z * q.Z), r = Math.Sqrt(p.X * p.X + p.Z * p.Z);
        return r < rShell + 0.004;
    }

    static KitMesh BuildShell(HelmetSpec spec, Shape sh, Func<double, double, double> keep)
    {
        bool hi = spec.Lod == 0;
        int nu = hi ? 112 : 40, nv = hi ? 56 : 20;
        // v from the top (pole) to below the lowest edge; the azimuth seam at the back
        double yTop = sh.Top, yBot = sh.Bottom - 0.02;
        var grid = Shapes.Grid(nu, nv, (u, v) =>
        {
            double az = Math.PI * (2 * u - 1);                  // -pi (back) .. pi (back)
            // ease v so rows bunch near the top (the dome) and stay even down the sides
            double y = yTop - (yTop - yBot) * (v * v * 0.35 + v * 0.65);
            if (v == 0) y = yTop - 1e-5;
            return sh.At(az, y);
        }, ShellRect, new Color(PartShell, 0, 0, 255));
        var clipped = Shapes.Clip(grid, (i, p) => keep(Math.Atan2(p.X, p.Z), p.Y), minIsland: 30);
        clipped.ComputeNormals(70f);
        Shapes.OrientOutward(clipped, new Vector3(0, (float)(sh.Bottom * 0.5), 0));
        clipped.ComputeNormals(70f);
        var solid = Shapes.Solidify(clipped, spec.Kind is "corinthian" or "montefortino" ? 0.0018 : 0.0022, null, EdgeRect, hi ? 5 : 2,
            bead: spec.Kind is "corinthian" ? 0.0012 : 0.0016, edgeMetresPerU: 2.0, innerUv: uv => InnerRect.Map((uv.X - ShellRect.U0) / ShellRect.Width, (uv.Y - ShellRect.V0) / ShellRect.Height),
            innerMask: new Color(PartInner, 0, 0, 255));
        return solid;
    }

    static KitMesh GreatHelmTop(HelmetSpec spec, Shape sh)
    {
        // a flat, slightly domed top plate riveted over the shell's top edge
        var prof = new List<Vector2d>();
        for (int i = 0; i <= 8; i++) { double t = i / 8.0; prof.Add(new Vector2d(t, 0.012 * (1 - t * t))); }
        var top = Shapes.Lathe(prof, spec.Lod == 0 ? 64 : 24, FitRect, new Color(PartFitting, 0, 0, 255),
            radiusAt: null, uv: (p, f, s) => FitRect.Map(f, s));
        // scale the unit disk to the section at the top
        var q = sh.At(Math.PI / 2, sh.Top - 0.002);
        var qf = sh.At(0, sh.Top - 0.002); var qb = sh.At(Math.PI, sh.Top - 0.002);
        for (int i = 0; i < top.VertexCount; i++)
        {
            var p = top.Positions[i];
            float zs = p.Z >= 0 ? (float)qf.Z : (float)-qb.Z;
            top.Positions[i] = new Vector3(p.X * (float)q.X, (float)(sh.Top - 0.004) + p.Y, p.Z * zs);
        }
        return top;
    }

    static KitMesh Fittings(HelmetSpec spec, Shape sh, BodyMeasure m, double eye)
    {
        var o = new KitMesh();
        bool hi = spec.Lod == 0;
        var fit = new Color(PartFitting, 0, 0, 255);
        switch (spec.Kind)
        {
            case "corinthian":
            {
                // the crest holder: a low bronze stilt along the crown from front to back
                var path = new List<Vector3d>();
                for (int i = 0; i <= 24; i++) { double az = i / 24.0; double y = sh.Top * (0.80 + 0.2 * Math.Sin(Math.PI * az)); path.Add(new Vector3d(0, sh.At(0, Math.Min(y, sh.Top - 1e-4)).Y + 0.004, (sh.Front * 0.55) - (sh.Front * 0.55 + sh.Back * 0.75) * az)); }
                for (int i = 0; i < path.Count; i++) { var p = path[i]; double s = Math.Sqrt(Math.Max(0, 1 - Math.Pow(p.Z / (p.Z >= 0 ? sh.Front : sh.Back), 2))); path[i] = new Vector3d(0, sh.Top * s + 0.006, p.Z); }
                o.Append(Shapes.Sweep(path, Aspis.RoundedRect(0.008, 0.012, 0.002, 2), true, FitRect, fit, up: new Vector3d(1, 0, 0)));
                break;
            }
            case "montefortino":
            {
                // the top knob (a cast finial) and two cheek plates hanging from the rim on hinges
                var prof = new List<Vector2d> { new(0, 0.030), new(0.008, 0.028), new(0.011, 0.020), new(0.006, 0.014), new(0.010, 0.006), new(0.014, 0.0) };
                var knob = Shapes.Lathe(Shapes.Fillet(prof, new[] { 0, 0.003, 0.003, 0.002, 0.002, 0 }, 3), hi ? 20 : 8, FitRect, fit);
                knob.Transform(Matrix.CreateTranslation(0, (float)(sh.Top + 0.006), 0));
                o.Append(knob);
                foreach (double side in new[] { 1.0, -1.0 })
                {
                    // a three-lobed plate following the cheek, 2 mm thick
                    var plate = Shapes.Grid(hi ? 16 : 6, hi ? 20 : 8, (u, v) =>
                    {
                        double az = side * (0.75 + 0.85 * u);
                        double y = eye + 0.006 - 0.080 * v;
                        var p = sh.At(az, eye + 0.004);
                        double taper = 1 - 0.12 * v;
                        return new Vector3d(p.X * taper + side * 0.003, y, p.Z * taper);
                    }, UvRect.Full, fit);
                    var cut = Shapes.Clip(plate, (i, p) =>
                    {
                        double u = (Math.Abs(Math.Atan2(p.X, p.Z)) - 0.75) / 0.85, v = (eye + 0.006 - p.Y) / 0.080;
                        double lobes = 0.08 * Math.Cos(v * Math.PI * 3);
                        return Math.Max(Math.Abs(u - 0.5) - (0.42 + lobes) * (1 - 0.35 * v), Math.Max(-v, v - 1));
                    });
                    for (int i = 0; i < cut.VertexCount; i++) cut.Uvs[i] = FitRect.Map(0.5 + cut.Positions[i].Z / 0.3, 0.5 - cut.Positions[i].Y / 0.3);
                    cut.ComputeNormals(60f);
                    Shapes.OrientOutward(cut, new Vector3(0, (float)(eye - 0.05), 0));
                    cut.ComputeNormals(60f);
                    o.Append(Shapes.Solidify(cut, 0.002, null, EdgeRect, hi ? 3 : 1, 0.0008, 1.0));
                }
                break;
            }
            case "nasal":
            {
                // the nose guard and the riveted bands: a brow band round the rim and four bands to the apex
                var front = sh.At(0, sh.Bottom + 0.004);
                var nasal = new List<Vector3d> { front + new Vector3d(0, 0.012, 0.004), front + new Vector3d(0, -0.020, 0.010), front + new Vector3d(0, -0.052, 0.012) };
                o.Append(Shapes.Sweep(nasal, Aspis.RoundedRect(0.004, 0.022, 0.0015, 2), true, FitRect, fit, up: new Vector3d(0, 0, 1),
                    scale: i => 1 - 0.25 * i / 2.0));
                var band = new List<Vector3d>();
                for (int i = 0; i < 96; i++) { double az = 2 * Math.PI * i / 96; var p = sh.At(az, sh.Bottom + 0.010); var l = Math.Sqrt(p.X * p.X + p.Z * p.Z); band.Add(new Vector3d(p.X * (1 + 0.0025 / l), p.Y, p.Z * (1 + 0.0025 / l))); }
                o.Append(Shapes.Sweep(band, Aspis.RoundedRect(0.0035, 0.020, 0.0012, 2), true, FitRect, fit, up: new Vector3d(0, 1, 0), closedPath: true));
                for (int k = 0; k < 4; k++)
                {
                    double az = Math.PI / 4 + Math.PI / 2 * k;
                    var path = new List<Vector3d>();
                    for (int i = 0; i <= 16; i++) { double y = sh.Bottom + 0.018 + (sh.Top - 0.004 - sh.Bottom - 0.018) * i / 16.0; var p = sh.At(az, y); var l = Math.Sqrt(p.X * p.X + p.Z * p.Z) + 1e-9; path.Add(new Vector3d(p.X * (1 + 0.002 / l), p.Y + 0.002 * (double)i / 16, p.Z * (1 + 0.002 / l))); }
                    o.Append(Shapes.Sweep(path, Aspis.RoundedRect(0.003, 0.016, 0.0010, 2), true, FitRect, fit, up: new Vector3d(Math.Sin(az), 0, Math.Cos(az))));
                }
                break;
            }
            case "kettle":
            {
                // the brim: a sloping ring out from the rim, rolled at its edge
                var prof = new List<Vector2d> { new(0, 0), new(0.030, -0.012), new(0.058, -0.026), new(0.066, -0.030) };
                var path = new List<Vector3d>();
                int n = hi ? 96 : 32;
                var rings = new List<Vector3d[]>();
                for (int r = 0; r < prof.Count; r++) rings.Add(new Vector3d[n]);
                for (int i = 0; i < n; i++)
                {
                    double az = 2 * Math.PI * i / n;
                    var p = sh.At(az, sh.Bottom + 0.002); double l = Math.Sqrt(p.X * p.X + p.Z * p.Z);
                    var dir = new Vector3d(p.X / l, 0, p.Z / l);
                    for (int r = 0; r < prof.Count; r++) rings[r][i] = p + dir * prof[r].X + new Vector3d(0, prof[r].Y, 0);
                }
                var brim = Shapes.Loft(rings, true, FitRect, fit);
                brim.ComputeNormals(60f);
                Shapes.OrientOutward(brim, new Vector3(0, (float)(sh.Bottom - 0.2), 0));
                brim.ComputeNormals(60f);
                o.Append(Shapes.Solidify(brim, 0.002, null, EdgeRect, hi ? 4 : 2, 0.0014, 1.5));
                break;
            }
            default:
            {
                // great helm: a cross of reinforcing strips down the face
                var path = new List<Vector3d>();
                for (int i = 0; i <= 12; i++) { double y = sh.Top - 0.01 - (sh.Top - sh.Bottom - 0.02) * i / 12.0; var p = sh.At(0, y); path.Add(new Vector3d(0, y, p.Z + 0.003)); }
                o.Append(Shapes.Sweep(path, Aspis.RoundedRect(0.003, 0.018, 0.001, 2), true, FitRect, fit, up: new Vector3d(0, 0, 1)));
                var arm = new List<Vector3d>();
                for (int i = 0; i <= 12; i++) { double az = -0.6 + 1.2 * i / 12; var p = sh.At(az, eye - 0.035); var l = Math.Sqrt(p.X * p.X + p.Z * p.Z); arm.Add(new Vector3d(p.X * (1 + 0.003 / l), p.Y, p.Z * (1 + 0.003 / l))); }
                o.Append(Shapes.Sweep(arm, Aspis.RoundedRect(0.003, 0.016, 0.001, 2), true, FitRect, fit, up: new Vector3d(0, 1, 0)));
                break;
            }
        }
        _ = m;
        return o;
    }

    /// <summary>A horsehair crest: hair cards standing on the crest holder, falling back.</summary>
    static KitMesh Crest(HelmetSpec spec, Shape sh)
    {
        var m = new KitMesh("crest");
        int n = spec.Lod == 0 ? 56 : 20;
        for (int i = 0; i < n; i++)
        {
            double t = -0.85 + 1.75 * i / (n - 1);
            double z = t >= 0 ? -t * sh.Back * 0.9 : -t * sh.Front * 0.62;
            double s = Math.Sqrt(Math.Max(0.02, 1 - Math.Pow(z / (z >= 0 ? sh.Front : sh.Back), 2)));
            var root = new Vector3d(0, sh.Top * s + 0.014, z);
            double hgt = 0.095 + 0.045 * (1 - Math.Abs(t + 0.1));
            var tip = root + new Vector3d(0, hgt, 0) + new Vector3d(0, 0, -0.09 * (t + 0.7) - 0.03);
            double wid = 0.014 + 0.006 * Rand(i, 1, 0, spec.Seed);
            for (int side = 0; side < 2; side++)
            {
                var off = new Vector3d((side == 0 ? 1 : -1) * 0.002, 0, 0);
                var a = root + off + new Vector3d(-wid, 0, 0) * 0; var b = root + off;
                var across = new Vector3d(0.0, 0, 1) * 0.0 + new Vector3d(wid, 0, 0);
                double u0 = 0.2 * Rand(i, side + 2, 0, spec.Seed);
                int k0 = m.Add((root - across + off).ToVector3(), new Vector2((float)u0, 0f), new Color(255, 255, 255, 230));
                int k1 = m.Add((root + across + off).ToVector3(), new Vector2((float)u0 + 0.05f, 0f), new Color(255, 255, 255, 230));
                int k2 = m.Add((tip - across * 1.6 + off).ToVector3(), new Vector2((float)u0, 1f), new Color(255, 255, 255, 230));
                int k3 = m.Add((tip + across * 1.6 + off).ToVector3(), new Vector2((float)u0 + 0.05f, 1f), new Color(255, 255, 255, 230));
                m.Tri(k0, k2, k1); m.Tri(k1, k2, k3);
                _ = a; _ = b;
            }
        }
        // cards face sideways (their plane contains +Y and +Z): normals along X
        return m.Finish(80f);
    }

    static MapSet Paint(HelmetSpec spec, KitMesh mesh, bool bronze)
    {
        var maps = new MapSet("helmet", spec.MapSize, spec.MapSize) { CavityStrength = 0.5f, CavityRadius = Math.Max(2, spec.MapSize / 300), CavityDepth = 0.0004f };
        uint seed = spec.Seed * 747796405u + 29u;
        double w = spec.Wear;
        maps.PaintMesh(mesh, 1, s =>
        {
            var p = s.Position;
            int part = (int)Math.Round(s.Mask.X * 255);
            bool inner = part == PartInner;
            double mottle = Fbm3(p.X * 30, p.Y * 30, p.Z * 30, 4, seed);
            double fine = Fbm3(p.X * 220, p.Y * 220, p.Z * 220, 3, seed + 1);
            // hammer facets (raised work): cellular noise on the shell
            var (f1, _, _, _, _) = Worley(Math.Atan2(p.X, p.Z) * 0.09 / 0.008, p.Y / 0.008, seed + 2, 0.9);
            double facet = f1 * f1;
            double edge = s.Mask.Z; // edge strips (rolled edges) get the most wear
            double az = Math.Atan2(p.X, p.Z) * 0.09;
            double scratch = Materials.Scratches(az, p.Y, seed + 3, 0.25 + 0.5 * w) * w;
            if (inner)
            {
                // inside: rough, darker, tarnished metal (padding is not modelled)
                var ti = bronze ? Materials.Bronze(mottle, 0.85, 0.15 * w, 0, 0, 0) : Materials.Iron(p.X * 3, p.Y * 3 + p.Z, seed + 4, 0.6 * w + 0.2, 0);
                ti.Albedo *= 0.55f; ti.Rough = Math.Min(1f, ti.Rough + 0.2f);
                return ti;
            }
            Texel t;
            if (bronze)
            {
                double tarnish = 0.15 + 0.5 * w * Smoothstep(0.45, 0.8, mottle) + 0.2 * Smoothstep(0.0, -0.08, p.Y);
                double polish = 0.35 * Smoothstep(0.03, 0.10, p.Y) + 0.6 * edge;
                double verd = 0.35 * w * Smoothstep(0.75, 0.9, fine * 0.5 + mottle * 0.5) * (1 - edge);
                t = Materials.Bronze(mottle, Saturate(tarnish), Saturate(verd), Saturate(polish), scratch * 0.8, 0);
            }
            else
            {
                double rust = (0.15 + 0.55 * w) * (1 - edge * 0.7) * (0.6 + 0.6 * Smoothstep(0.05, -0.05, p.Y));
                t = Materials.Iron(p.X * 2 + p.Z, p.Y * 2, seed + 5, rust, 0.3 * edge + 0.15, scratch, steel: spec.Kind == "greathelm");
            }
            t.Height += (float)(0.00003 * facet + 0.00001 * fine) - (float)(0.00004 * scratch);
            return t;
        });
        return maps.Finish(4);
    }
}
