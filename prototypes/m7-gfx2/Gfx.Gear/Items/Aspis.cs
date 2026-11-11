using Gfx.Gear.Geometry;
using Gfx.Gear.Maps;
using Microsoft.Xna.Framework;
using static Gfx.Gear.Maps.Noise;

namespace Gfx.Gear.Items;

/// <summary>Parameters of one aspis (variation multiplies the one authored design; it never lowers its detail).</summary>
public sealed record AspisSpec
{
    public uint Seed { get; init; } = 1;
    /// <summary>Map size (texels per side): 2048 for the hero bake, 1024 for soldiers.</summary>
    public int MapSize { get; init; } = 2048;
    /// <summary>0 = full detail (LOD0), 1 = fewer segments and no cord festoons, 2 = silhouette only.</summary>
    public int Lod { get; init; }
    /// <summary>Device painted on the bronze: "sun" (force A), "crescent", "wheel", "none".</summary>
    public string Device { get; init; } = "sun";
    /// <summary>Device paint (sRGB hex).</summary>
    public string Paint { get; init; } = "#5C1A12";
    /// <summary>Overall wear 0..1 (dents, scratches, tarnish, paint loss).</summary>
    public double Wear { get; init; } = 0.55;
    /// <summary>true = bronze-faced (the hero); false = painted wooden face with a bronze rim.</summary>
    public bool BronzeFace { get; init; } = true;
    /// <summary>Field colour of a painted (not bronze-faced) shield (sRGB hex).</summary>
    public string Field { get; init; } = "#2E3F63";
    /// <summary>Device rotation (radians), so a line of shields is not stamped from one template.</summary>
    public double DeviceTurn { get; init; }
}

/// <summary>
/// The hero asset of stage kit: a hoplite aspis (Greek, about 500-350 BC), authored in code after the historical form:
/// a deep wooden bowl about 0.92 m across with a flat offset rim (the itys), faced with a thin bronze sheet; a cast
/// cable (guilloche) between bead rows on the rim; inside, a bronze arm band (porpax) on riveted plates at the centre
/// and a cord hand grip (antilabe) near the rim; a carrying cord in festoons from bronze staples round the inner rim;
/// the bronze facing turned over the edge and nailed at the back. One mesh, one material, one 2048 texel atlas:
/// base colour, normal and ORM painted in code with authored wear (planishing facets, a painted device with chips,
/// polished high points, tarnish and verdigris in the recesses, dents with cracked paint, scratches, dust low on the
/// face, handling grime on the grip). No asset or likeness of any game is recreated. PRESENTATION ONLY.
/// </summary>
public static class Aspis
{
    // ---------------------------------------------------------------- dimensions (metres, item space: +Z out of the face, +Y up)
    public const double Rb = 0.384;        // bowl edge (outer)
    public const double H = 0.128;         // bowl depth above the rim face
    public const double RimZ = 0.004;      // rim face height
    public const double BeadR = 0.3935;    // bead at the rim's inner edge
    public const double RimOut = 0.452;    // start of the rolled lip
    public const double LipR = 0.007;      // lip roll radius
    public const double BackZ = -0.010;    // the rim's back plane
    public const double RbIn = 0.370;      // inner bowl edge
    public const double HIn = 0.116;       // inner apex height
    public const double Outer = RimOut + LipR;
    public const double BronzeBackR = 0.414; // the bronze facing turned over the back ends here
    public const double AntilabeX = -0.262;  // antilabe (hand grip) centre along -X (the forearm runs from the centre towards it)
    public const double AntilabeDepth = 0.062; // the fist's centre below the inner face at the centre (forearm half-thickness + palm)

    // ---------------------------------------------------------------- atlas (fractions of the map)
    public static readonly UvRect FrontRect = new(0.000f, 0.000f, 0.625f, 0.625f);
    public static readonly UvRect BackRect = new(0.625f, 0.000f, 1.000f, 0.375f);
    public static readonly UvRect FittingsRect = new(0.625f, 0.375f, 1.000f, 0.625f);
    public static readonly UvRect LipRect = new(0.000f, 0.640f, 1.000f, 0.700f);
    public static readonly UvRect CordRect = new(0.000f, 0.715f, 1.000f, 0.765f);
    public static readonly UvRect RivetRect = new(0.000f, 0.780f, 0.120f, 0.840f);
    const double D = 2 * Outer + 0.004;      // the planar projections' extent (m)
    const double CordMetres = 0.30;          // cord length per atlas width (every cord piece is shorter)

    // part ids in the mesh mask (R channel)
    const int PartFace = 10, PartLip = 20, PartBack = 30, PartBand = 40, PartPlate = 50, PartLining = 60, PartCord = 70, PartRivet = 80, PartStaple = 90;

    public static double BowlZ(double r) => r >= Rb ? RimZ : RimZ + (H - RimZ) * Math.Pow(Math.Max(0, 1 - Math.Pow(r / Rb, 2.6)), 0.55);
    public static double InnerZ(double r) => r >= RbIn ? BackZ : BackZ + (HIn - BackZ) * Math.Pow(Math.Max(0, 1 - Math.Pow(r / RbIn, 2.6)), 0.55);

    /// <summary>Front profile: bowl apex to the start of the lip roll, with a concave fillet at the wall foot and the bead.</summary>
    static List<Vector2d> FrontProfile(int lod)
    {
        var pts = new List<Vector2d>();
        int n = lod == 0 ? 40 : lod == 1 ? 22 : 10;
        // denser towards the steep wall: r = Rb * (1 - (1 - s)^2)
        double wallTop = RimZ + 0.009;
        for (int i = 0; i <= n; i++)
        {
            double s = (double)i / n;
            double r = Rb * (1 - (1 - s) * (1 - s));
            double z = BowlZ(r);
            if (z < wallTop) break;
            pts.Add(new Vector2d(r, z));
        }
        // the wall from wallTop down to the fillet: solve r for z
        double rWall = Rb * Math.Pow(1 - Math.Pow((wallTop - RimZ) / (H - RimZ), 1 / 0.55), 1 / 2.6);
        pts.Add(new Vector2d(rWall, wallTop));
        // concave fillet into the rim face (radius 3 mm), then the bead (half round, 2.5 mm), then the flat rim
        var corner = new List<Vector2d> { pts[^2], new(Rb + 0.0002, RimZ), new(BeadR - 0.0026, RimZ) };
        var fil = Shapes.Fillet(corner, new[] { 0.0, 0.003, 0.0 }, lod == 0 ? 5 : 2);
        pts.RemoveAt(pts.Count - 1);
        foreach (var p in fil.Skip(1)) pts.Add(p);
        if (lod < 2)
            foreach (var p in Shapes.Arc(BeadR, RimZ, 0.0025, Math.PI, 0, lod == 0 ? 8 : 4).Skip(1)) pts.Add(p);
        // flat rim, very slightly crowned, out to the lip roll
        int nr = lod == 0 ? 6 : 3;
        double r0 = BeadR + 0.0025;
        for (int i = 1; i <= nr; i++)
        {
            double t = (double)i / nr;
            double r = r0 + (RimOut - r0) * t;
            pts.Add(new Vector2d(r, RimZ + 0.0008 * Math.Sin(Math.PI * t)));
        }
        return pts;
    }

    static List<Vector2d> LipProfile(int lod) =>
        Shapes.Arc(RimOut, RimZ - LipR, LipR, Math.PI / 2, -Math.PI / 2, lod == 0 ? 12 : lod == 1 ? 6 : 3);

    /// <summary>Back profile: from the lip's end inwards along the back plane and up the inner bowl to the inner apex.</summary>
    static List<Vector2d> BackProfile(int lod)
    {
        var pts = new List<Vector2d> { new(RimOut, BackZ) };
        int nb = lod == 0 ? 6 : 2;
        for (int i = 1; i <= nb; i++) pts.Add(new Vector2d(RimOut + (RbIn + 0.006 - RimOut) * i / nb, BackZ));
        int n = lod == 0 ? 30 : lod == 1 ? 18 : 8;
        var inner = new List<Vector2d>();
        for (int i = 0; i <= n; i++)
        {
            double s = (double)i / n;
            double r = RbIn * (1 - s * s);       // from the edge inwards, denser near the edge
            if (r > RbIn - 0.001) continue;
            inner.Add(new Vector2d(r, InnerZ(r)));
        }
        // fillet between the back plane and the inner wall (radius 6 mm)
        var corner = new List<Vector2d> { pts[^1], new(RbIn, BackZ), inner[0] };
        var fil = Shapes.Fillet(corner, new[] { 0.0, 0.006, 0.0 }, lod == 0 ? 5 : 2);
        foreach (var p in fil.Skip(1)) pts.Add(p);
        foreach (var p in inner.Skip(1)) pts.Add(p);
        if (pts[^1].X > 1e-9) pts.Add(new Vector2d(0, HIn));
        return pts;
    }

    // lathe about +Y with the face along +Z: convert (r, z) profile to lathe (radius, height) and rotate +Y -> +Z
    static readonly Matrix ToFace = Matrix.CreateRotationX(MathHelper.PiOver2);

    static Vector2 Planar(UvRect rect, Vector3d p, bool mirror)
    {
        // p is in lathe space (x, y = height, z); after ToFace: X = x, Y = -z, Z = y
        double fx = p.X, fy = -p.Z;
        if (mirror) fx = -fx;   // the back is seen from behind
        return rect.Map(0.5 + fx / D, 0.5 - fy / D);
    }

    /// <summary>Builds the aspis (mesh, map set, frames).</summary>
    public static KitItem Build(AspisSpec spec)
    {
        var item = new KitItem { Id = $"aspis#{spec.Seed}L{spec.Lod}", Kind = "shield.aspis", Era = "ancient", Label = "Aspis (bronze-faced hoplite shield)" };
        var mesh = BuildMesh(spec);
        item.Parts.Add(new KitPart { Name = "aspis", Mesh = mesh, Maps = "aspis" });
        if (spec.MapSize > 0) item.Maps["aspis"] = Paint(spec, mesh);
        Frames(item);
        item.GripRadius = 0.006f;     // the antilabe cord
        item.CentreOfMass = new Vector3(0, 0, 0.05f);
        return item;
    }

    /// <summary>
    /// Frames: <c>forearm</c> (the porpax: origin on the inner face at the centre, +Y along the forearm towards the
    /// antilabe = item -X, +Z out of the face, so +X = item +Y, the device's up); <c>grip</c> (the antilabe: the left
    /// fist round the cord, thumb up along item +Y, knuckles along item -X, palm facing away from the face);
    /// <c>hang</c> (slung on the back by the carrying cord: face outwards, the centre between the shoulder blades).
    /// </summary>
    public static void Frames(KitItem item)
    {
        var porpax = new Vector3(0, 0, (float)(HIn - 0.004));
        item.Frames["forearm"] = KitItem.MakeFrame(porpax, -Vector3.UnitX, Vector3.UnitZ);
        item.Frames["grip"] = KitItem.MakeFrame(new Vector3((float)AntilabeX, 0, (float)(HIn - AntilabeDepth)), Vector3.UnitY, -Vector3.UnitZ);
        item.Frames["hang"] = KitItem.MakeFrame(new Vector3(0, 0.02f, (float)(BackZ - 0.012)), Vector3.UnitY, Vector3.UnitZ);
    }

    public static KitMesh BuildMesh(AspisSpec spec)
    {
        int lod = spec.Lod;
        int seg = lod == 0 ? 128 : lod == 1 ? 56 : 28;
        var face = new Color(PartFace, 0, 0, 255);
        var m = new KitMesh("aspis");
        var front = Shapes.Lathe(FrontProfile(lod), seg, FrontRect, face, uv: (p, f, s) => Planar(FrontRect, p, false));
        var lip = Shapes.Lathe(LipProfile(lod), seg, LipRect, new Color(PartLip, 0, 0, 255));
        var back = Shapes.Lathe(BackProfile(lod), seg, BackRect, new Color(PartBack, 0, 0, 255), uv: (p, f, s) => Planar(BackRect, p, true));
        m.Append(front); m.Append(lip); m.Append(back);
        m.Transform(ToFace);
        if (lod <= 1) m.Append(Fittings(spec));
        if (lod == 0) m.Append(Festoons(spec));
        m.Name = "aspis";
        return m.Finish(lod == 0 ? 38f : 45f);
    }

    // ---------------------------------------------------------------- fittings: porpax, antilabe, staples, rivets

    static KitMesh Fittings(AspisSpec spec)
    {
        var m = new KitMesh();
        bool hi = spec.Lod == 0;
        double zIn(double x, double y) => InnerZ(Math.Sqrt(x * x + y * y));
        // porpax band: a loop across the forearm, 34 mm wide, standing off the inner face; plates at both ends
        {
            int n = hi ? 40 : 14;
            var path = new List<Vector3d>();
            // the loop: in the plane x = 0 (across the forearm, which runs along -X), from plate A (y = +0.075) round
            // the forearm (centre 46 mm off the inner face, radius 44 mm) to plate B (y = -0.075)
            double cz = HIn - 0.044, rad = 0.047;
            for (int i = 0; i <= n; i++)
            {
                double a = Math.PI * (0.06 + 0.88 * i / n);   // from the top round the back to the bottom
                double y = rad * Math.Cos(a) * 1.25, z = cz - rad * Math.Sin(a) * 0.95;
                path.Add(new Vector3d(0, y, z));
            }
            // ends meet the inner face at the plates
            path.Insert(0, new Vector3d(0, 0.075, zIn(0, 0.075) - 0.002));
            path.Add(new Vector3d(0, -0.075, zIn(0, -0.075) - 0.002));
            var section = RoundedRect(0.030, 0.0024, 0.0011, hi ? 3 : 1);
            var band = Shapes.Sweep(path, section, true, Sub(FittingsRect, 0, 0, 0.5, 0.5), new Color(PartBand, 0, 0, 255), up: new Vector3d(1, 0, 0), vRepeat: 1.0);
            m.Append(band);
            // leather lining inside the loop (slightly smaller, dark)
            var lining = Shapes.Sweep(path.Skip(2).Take(path.Count - 4).ToList(), RoundedRect(0.027, 0.0014, 0.0006, 1), true,
                Sub(FittingsRect, 0.5, 0, 1, 0.25), new Color(PartLining, 0, 0, 255), up: new Vector3d(1, 0, 0));
            // push the lining towards the loop centre by 3 mm
            for (int i = 0; i < lining.VertexCount; i++)
            {
                var p = lining.Positions[i]; var c = new Vector3(0, 0, (float)cz);
                var d = p - c; d.X = 0;
                lining.Positions[i] = p - Vector3.Normalize(d) * 0.003f;
            }
            m.Append(lining);
            // terminal plates: palmette-ended bronze plates riveted to the inner face
            foreach (double sy in new[] { 1.0, -1.0 })
            {
                var plate = Plate(sy, hi);
                m.Append(plate);
            }
        }
        // antilabe: a cord loop from two staples near the rim, hanging to where the fist closes on it
        {
            double x = AntilabeX, y0 = 0.060;
            double zs = zIn(x, y0);
            double zf = HIn - AntilabeDepth;              // the fist's centre (frame "grip")
            var path = new List<Vector3d>();
            int n = hi ? 28 : 10;
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n;                   // 0 at the upper staple, 1 at the lower
                double y = y0 * Math.Cos(Math.PI * t);
                double sag = Math.Sin(Math.PI * t);
                double z = zs - 0.004 - (zs - 0.004 - zf) * Math.Pow(sag, 0.6);
                path.Add(new Vector3d(x + 0.004 * Math.Sin(2 * Math.PI * t), y, z));
            }
            m.Append(Cord(path, 0.0045, hi, 0));
            foreach (double sy in new[] { y0, -y0 }) m.Append(Staple(x, sy, zIn(x, sy), hi));
        }
        // rivets: the bronze facing nailed through the back rim (every 7.5 degrees), four on each porpax plate
        {
            int count = hi ? 48 : 0;
            for (int i = 0; i < count; i++)
            {
                double a = 2 * Math.PI * (i + 0.5) / count;
                double r = 0.4335;
                m.Append(Rivet(new Vector3d(r * Math.Cos(a), r * Math.Sin(a), BackZ - 0.0002), new Vector3d(0, 0, -1), 0.0042, hi, i));
            }
            if (hi)
                foreach (double sy in new[] { 1.0, -1.0 })
                    foreach (var (dx, dy) in new[] { (-0.018, 0.098), (0.018, 0.098), (0.0, 0.122) })
                    {
                        double x = dx, y = sy * dy;
                        m.Append(Rivet(new Vector3d(x, y, zIn(x, y) - 0.0024), Normal(x, y), 0.0034, hi, 100 + (int)(10 * (sy + 2) + dx * 100)));
                    }
        }
        return m;
    }

    static Vector3d Normal(double x, double y)
    {
        // inner surface normal (pointing away from the face, -Z mostly)
        double e = 1e-4, r = Math.Sqrt(x * x + y * y);
        double dzdx = (InnerZ(Math.Sqrt((x + e) * (x + e) + y * y)) - InnerZ(Math.Sqrt((x - e) * (x - e) + y * y))) / (2 * e);
        double dzdy = (InnerZ(Math.Sqrt(x * x + (y + e) * (y + e))) - InnerZ(Math.Sqrt(x * x + (y - e) * (y - e)))) / (2 * e);
        _ = r;
        return new Vector3d(dzdx, dzdy, -1).Normalized();
    }

    static UvRect Sub(UvRect r, double u0, double v0, double u1, double v1) =>
        new((float)(r.U0 + r.Width * u0), (float)(r.V0 + r.Height * v0), (float)(r.U0 + r.Width * u1), (float)(r.V0 + r.Height * v1));

    /// <summary>A rounded rectangle section (width w, thickness t, corner radius c), counter-clockwise, closed.</summary>
    public static List<Vector2d> RoundedRect(double w, double t, double c, int steps)
    {
        var l = new List<Vector2d>();
        double hw = w / 2, ht = t / 2; c = Math.Min(c, Math.Min(hw, ht) * 0.99);
        (double cx, double cy, double a0)[] corners = { (hw - c, ht - c, 0), (-hw + c, ht - c, Math.PI / 2), (-hw + c, -ht + c, Math.PI), (hw - c, -ht + c, 3 * Math.PI / 2) };
        foreach (var (cx, cy, a0) in corners)
            for (int k = 0; k <= steps; k++) { double a = a0 + Math.PI / 2 * k / steps; l.Add(new Vector2d(cx + c * Math.Cos(a), cy + c * Math.Sin(a))); }
        return l;
    }

    /// <summary>A porpax terminal plate: a bronze plate (palmette end) lying on the inner face, 2.5 mm thick.</summary>
    static KitMesh Plate(double sy, bool hi)
    {
        // outline in (x, y'): y' from 0.060 to 0.135 along the plate, a palmette (five lobes) at the far end
        var o = new List<Vector2d>();
        int n = hi ? 64 : 20;
        for (int i = 0; i < n; i++)
        {
            double a = 2 * Math.PI * i / n;
            // a long oval with a lobed end
            double ex = 0.026, ey = 0.040;
            double x = ex * Math.Sin(a);
            double y = 0.097 + ey * Math.Cos(a);
            if (Math.Cos(a) > 0.2) { double lobe = 0.006 * Math.Max(0, Math.Cos(5 * a)); x *= 1 + lobe / ex; y += lobe * Math.Cos(a); }
            o.Add(new Vector2d(x, y));
        }
        var rect = Sub(FittingsRect, 0, 0.5, 0.5, 1.0);
        var grid = Shapes.Grid(hi ? 24 : 8, hi ? 36 : 12, (u, v) => new Vector3d(-0.032 + 0.064 * u, 0.050 + 0.095 * v, 0), rect, new Color(PartPlate, 0, 0, 255));
        var clipped = Shapes.Clip(grid, (i, p) => Shapes.PolygonSdf(o, new Vector2d(p.X, p.Y)));
        // lay it on the inner face (z from the bowl), facing away from the face
        for (int i = 0; i < clipped.VertexCount; i++)
        {
            var p = clipped.Positions[i];
            double y = sy * p.Y;
            clipped.Positions[i] = new Vector3(p.X, (float)y, (float)(InnerZ(Math.Sqrt(p.X * p.X + y * y)) - 0.0029));
        }
        clipped.ComputeNormals(60f);
        // the plate's top faces away from the shield's face (-Z); the solid grows 2.5 mm back to the wood
        if (clipped.Normals.Count > 0 && clipped.Normals[0].Z > 0) { clipped.FlipWinding(); clipped.ComputeNormals(60f); }
        return Shapes.Solidify(clipped, 0.0025, null, Sub(FittingsRect, 0.5, 0.25, 1.0, 0.3), hi ? 4 : 2, 0, 0.40);
    }

    static KitMesh Staple(double x, double y, double z, bool hi)
    {
        // a small bronze loop (half torus) standing on the inner face
        var path = new List<Vector3d>();
        int n = hi ? 12 : 6;
        for (int i = 0; i <= n; i++)
        {
            double a = Math.PI * i / n;
            path.Add(new Vector3d(x + 0.009 * Math.Cos(a), y, z + 0.0005 - 0.007 * Math.Sin(a)));
        }
        return Shapes.Tube(path, 0.0022, hi ? 10 : 6, Sub(FittingsRect, 0.5, 0.3, 1.0, 0.35), new Color(PartStaple, 0, 0, 255), up: new Vector3d(0, 1, 0));
    }

    /// <summary>A dome-headed nail or rivet (radius r) on a surface point with outward normal n.</summary>
    static KitMesh Rivet(Vector3d at, Vector3d n, double r, bool hi, int index)
    {
        var prof = new List<Vector2d>();
        int k = hi ? 4 : 3;
        for (int i = 0; i <= k; i++) { double a = Math.PI / 2 * (1 - (double)i / k); prof.Add(new Vector2d(r * Math.Cos(a), r * 0.55 * Math.Sin(a))); }
        prof.Add(new Vector2d(r * 1.02, -0.0006));
        var cell = RivetRect;
        // each rivet uses the whole rivet area (planar over its head)
        var mesh = Shapes.Lathe(prof, hi ? 10 : 6, cell, new Color(PartRivet, index & 255, 0, 255),
            uv: (p, f, s) => cell.Map(0.5 + p.X / (2.4 * r), 0.5 + p.Z / (2.4 * r)));
        // lathe axis +Y -> n
        var up = new Vector3d(0, 1, 0);
        var axis = Vector3d.Cross(up, n); double sn = axis.Length, cs = up.Dot(n);
        Matrix rot = Matrix.Identity;
        if (sn > 1e-9) rot = Matrix.CreateFromAxisAngle(axis.Normalized().ToVector3(), (float)Math.Atan2(sn, cs));
        else if (cs < 0) rot = Matrix.CreateRotationX(MathHelper.Pi);
        mesh.Transform(rot * Matrix.CreateTranslation(at.ToVector3()));
        return mesh;
    }

    /// <summary>A twisted cord (two plies, painted in the cord strip) along a path.</summary>
    static KitMesh Cord(IReadOnlyList<Vector3d> path, double radius, bool hi, int index)
    {
        var tube = Shapes.Tube(path, radius, hi ? 8 : 6, UvRect.Full, new Color(PartCord, index & 255, 0, 255), up: new Vector3d(0, 0, 1),
            vRepeat: CordMetres, vMetric: true);
        for (int i = 0; i < tube.VertexCount; i++) { var uv = tube.Uvs[i]; tube.Uvs[i] = CordRect.Map(uv.Y, uv.X); }
        return tube;
    }

    /// <summary>The carrying cord in festoons between staples round the inner rim (16 staples, sagging loops).</summary>
    static KitMesh Festoons(AspisSpec spec)
    {
        var m = new KitMesh();
        int staples = 16;
        double r = 0.398, zs = BackZ - 0.004;
        for (int i = 0; i < staples; i++)
        {
            double a0 = 2 * Math.PI * i / staples + 0.12, a1 = 2 * Math.PI * (i + 1) / staples + 0.12;
            // skip the loops where the hand grip and the arm come in (around -X) so the arm is never inside a cord
            double mid = 0.5 * (a0 + a1);
            if (Math.Cos(mid) < -0.80) continue;
            var path = new List<Vector3d>();
            int n = 14;
            for (int k = 0; k <= n; k++)
            {
                double t = (double)k / n;
                double a = a0 + (a1 - a0) * t;
                double sag = 0.022 * Math.Sin(Math.PI * t);
                // sag inwards (towards the centre) and slightly off the back plane
                double rr = r - sag * 0.85;
                path.Add(new Vector3d(rr * Math.Cos(a), rr * Math.Sin(a), zs - 0.006 * Math.Sin(Math.PI * t)));
            }
            m.Append(Cord(path, 0.0028, true, 1 + i));
            m.Append(Staple2(r * Math.Cos(a0), r * Math.Sin(a0), a0));
        }
        return m;
    }

    static KitMesh Staple2(double x, double y, double a)
    {
        var path = new List<Vector3d>();
        var t = new Vector3d(-Math.Sin(a), Math.Cos(a), 0);
        for (int i = 0; i <= 8; i++)
        {
            double s = Math.PI * i / 8;
            var p = new Vector3d(x, y, BackZ) + t * (0.006 * Math.Cos(s)) + new Vector3d(0, 0, -0.0055 * Math.Sin(s));
            path.Add(p);
        }
        return Shapes.Tube(path, 0.0017, 8, Sub(FittingsRect, 0.5, 0.3, 1.0, 0.35), new Color(PartStaple, 0, 0, 255), up: new Vector3d(0, 0, 1));
    }

    // ---------------------------------------------------------------- the maps

    /// <summary>Wear features shared by the painters (deterministic from the seed).</summary>
    sealed class Wear
    {
        public readonly (double x, double y, double r, double depth)[] Dents;
        public readonly (double x0, double y0, double x1, double y1, double w, double d)[] Scratches;
        public Wear(AspisSpec s)
        {
            uint seed = s.Seed * 7919u + 13u;
            int nd = (int)Math.Round(3 + 5 * s.Wear);
            Dents = new (double, double, double, double)[nd];
            for (int i = 0; i < nd; i++)
            {
                // blows land mostly on the upper and outer face (the side turned to the enemy)
                double a = Math.PI * (0.05 + 0.9 * Rand(i, 1, 0, seed)) + (Rand(i, 2, 0, seed) < 0.25 ? Math.PI : 0);
                double rr = 0.08 + 0.32 * Math.Sqrt(Rand(i, 3, 0, seed));
                Dents[i] = (rr * Math.Cos(a), rr * Math.Sin(a), 0.010 + 0.022 * Rand(i, 4, 0, seed), 0.0006 + 0.0014 * Rand(i, 5, 0, seed) * s.Wear);
            }
            int ns = (int)Math.Round(30 + 90 * s.Wear);
            Scratches = new (double, double, double, double, double, double)[ns];
            for (int i = 0; i < ns; i++)
            {
                double cx = Rand11(i, 11, 0, seed) * 0.42, cy = Rand11(i, 12, 0, seed) * 0.42 + 0.06;
                // clusters run in a few dominant directions (blows from the right, from above)
                double dir = (Rand(i, 13, 0, seed) < 0.6 ? 0.35 : -0.9) + 0.5 * Rand11(i, 14, 0, seed);
                double len = 0.02 + 0.13 * Math.Pow(Rand(i, 15, 0, seed), 1.6);
                Scratches[i] = (cx - Math.Cos(dir) * len / 2, cy - Math.Sin(dir) * len / 2, cx + Math.Cos(dir) * len / 2, cy + Math.Sin(dir) * len / 2,
                    0.0003 + 0.0009 * Rand(i, 16, 0, seed), 0.00004 + 0.00012 * Rand(i, 17, 0, seed));
            }
        }

        public (double h, double fresh, double dentness) At(double x, double y)
        {
            double h = 0, fresh = 0, dn = 0;
            foreach (var (dx, dy, r, d) in Dents)
            {
                double q = Math.Sqrt((x - dx) * (x - dx) + (y - dy) * (y - dy)) / r;
                if (q > 1.6) continue;
                // a smooth crater with a slightly raised lip
                h += -d * Math.Exp(-q * q * 2.2) + d * 0.18 * Math.Exp(-(q - 1.15) * (q - 1.15) * 12);
                dn = Math.Max(dn, Math.Exp(-q * q * 1.2));
                fresh = Math.Max(fresh, Math.Exp(-q * q * 6) * 0.6);
            }
            foreach (var (x0, y0, x1, y1, w, d) in Scratches)
            {
                double ex = x1 - x0, ey = y1 - y0, l2 = ex * ex + ey * ey;
                double t = Math.Clamp(((x - x0) * ex + (y - y0) * ey) / l2, 0, 1);
                double px = x0 + ex * t - x, py = y0 + ey * t - y;
                double dist = Math.Sqrt(px * px + py * py);
                if (dist > w * 2) continue;
                double taper = Math.Sin(Math.PI * t);
                double v = Math.Exp(-(dist / w) * (dist / w) * 3) * taper;
                h -= d * v;
                fresh = Math.Max(fresh, v);
            }
            return (h, fresh, dn);
        }
    }

    /// <summary>The device's coverage at (x, y) in metres on the face (smooth-edged, 0..1).</summary>
    public static double Device(string kind, double x, double y, double turn)
    {
        double c = Math.Cos(turn), s = Math.Sin(turn);
        double u = c * x - s * y, v = s * x + c * y;
        double r = Math.Sqrt(u * u + v * v), a = Math.Atan2(v, u);
        double aa = 0.0012; // anti-aliasing width (m)
        switch (kind)
        {
            case "sun":
            {
                // a disc in a ring and twelve rays: six straight wedges alternating with six wavy flames
                double disc = Smoothstep(0.058 + aa, 0.058 - aa, r);
                double ring = Smoothstep(0.076 + aa, 0.076 - aa, r) * Smoothstep(0.068 - aa, 0.068 + aa, r);
                double best = 0;
                for (int k = 0; k < 12; k++)
                {
                    bool straight = k % 2 == 0;
                    double len = straight ? 0.300 : 0.255;
                    double r0 = 0.088;
                    double t = (r - r0) / (len - r0);
                    if (t < -0.05 || t > 1.05) continue;
                    double ac = 2 * Math.PI * k / 12;
                    if (!straight) ac += 0.045 * Math.Sin(t * 3.3 * Math.PI) * (0.35 + t);
                    double da = Math.IEEERemainder(a - ac, 2 * Math.PI);
                    double halfW = straight ? 0.024 * (1 - Math.Clamp(t, 0, 1)) + 0.0012 : 0.010 * (1 - 0.65 * Math.Clamp(t, 0, 1)) + 0.0018;
                    double dist = Math.Abs(da) * r - halfW;
                    double cov = Smoothstep(aa, -aa, dist) * Smoothstep(-0.02, 0.0, t) * Smoothstep(1.0 + aa / len, 1.0 - aa / len, t);
                    best = Math.Max(best, cov);
                }
                return Math.Max(Math.Max(disc, ring), best);
            }
            case "crescent":
            {
                double outer = Smoothstep(0.22 + aa, 0.22 - aa, r);
                double ix = u - 0.07, iy = v + 0.02;
                double inner = Smoothstep(0.19 + aa, 0.19 - aa, Math.Sqrt(ix * ix + iy * iy));
                double star = 0;
                for (int k = 0; k < 8; k++)
                {
                    double sa = Math.Atan2(v + 0.02, u + 0.10) - 2 * Math.PI * k / 8;
                    double sr = Math.Sqrt((u + 0.10) * (u + 0.10) + (v + 0.02) * (v + 0.02));
                    double w = 0.012 * (1 - sr / 0.06);
                    star = Math.Max(star, Smoothstep(aa, -aa, Math.Abs(Math.IEEERemainder(sa, 2 * Math.PI)) * sr - w) * Smoothstep(0.06, 0.05, sr));
                }
                return Math.Max(outer * (1 - inner), star);
            }
            case "wheel":
            {
                double rim = Smoothstep(0.25 + aa, 0.25 - aa, r) * Smoothstep(0.21 - aa, 0.21 + aa, r);
                double hub = Smoothstep(0.05 + aa, 0.05 - aa, r);
                double spokes = 0;
                for (int k = 0; k < 8; k++)
                {
                    double da = Math.IEEERemainder(a - 2 * Math.PI * k / 8, 2 * Math.PI);
                    spokes = Math.Max(spokes, Smoothstep(aa, -aa, Math.Abs(da) * r - 0.011) * Smoothstep(0.215, 0.21, r));
                }
                return Math.Max(rim, Math.Max(hub, spokes));
            }
            default: return 0;
        }
    }

    /// <summary>Guilloche on the rim band: height (m) and a strand mask (1 on a strand's crown) for a point at (r, a).</summary>
    static (double h, double crown, double groove) Guilloche(double r, double a)
    {
        double r0 = 0.3995, r1 = 0.4465;              // band between the bead rows
        if (r < r0 - 0.003 || r > r1 + 0.003) return (0, 0, 0);
        double rc = 0.5 * (r0 + r1), amp = (r1 - r0) * 0.30;
        int twists = 62;
        double ph = a * twists;
        double sA = rc + amp * Math.Sin(ph), sB = rc - amp * Math.Sin(ph);
        double w = (r1 - r0) * 0.19;
        // which strand is over at this phase (they alternate at each crossing)
        bool aOver = Math.Cos(ph) > 0;
        double dA = Math.Abs(r - sA) / w, dB = Math.Abs(r - sB) / w;
        double hA = dA < 1 ? Math.Sqrt(1 - dA * dA) : 0, hB = dB < 1 ? Math.Sqrt(1 - dB * dB) : 0;
        // the under strand dips near the crossing
        double dip = Math.Exp(-Math.Pow(Math.Cos(ph) * 3.0, 2));
        double hUnder = 0.55 + 0.45 * (1 - dip);
        double h = aOver ? Math.Max(hA, hB * hUnder) : Math.Max(hA * hUnder, hB);
        // eyes: a dot in the middle of each loop
        double eyePh = Math.IEEERemainder(ph - Math.PI / 2, Math.PI);
        double eye = Math.Exp(-(eyePh * eyePh) * 6 - Math.Pow((r - rc) / (w * 0.7), 2) * 2) * 0.75;
        h = Math.Max(h, eye);
        // band edges: bead rows of small domes
        double beadH = 0;
        foreach (double rb in new[] { r0 - 0.0005, r1 + 0.0005 })
        {
            double pitch = 0.0062;
            double s = a * rb / pitch;
            double ds = (s - Math.Round(s)) * pitch;
            double dr = r - rb;
            double q = Math.Sqrt(ds * ds + dr * dr) / 0.0023;
            if (q < 1) beadH = Math.Max(beadH, Math.Sqrt(1 - q * q));
        }
        h = Math.Max(h, beadH);
        double groove = 1 - h;
        return (h * 0.0008, h, groove * Smoothstep(r0 - 0.003, r0, r) * Smoothstep(r1 + 0.003, r1, r));
    }

    static readonly Vector3 BronzeBright = new(0.790f, 0.585f, 0.320f);
    static readonly Vector3 BronzeAged = new(0.600f, 0.415f, 0.215f);
    static readonly Vector3 Tarnish = new(0.200f, 0.135f, 0.072f);
    static readonly Vector3 Verdigris = new(0.120f, 0.230f, 0.180f);
    static readonly Vector3 Dust = new(0.200f, 0.165f, 0.120f);

    /// <summary>Bronze at a point: mottle, tarnish (0..1), verdigris (0..1), polish (0..1), fresh metal (0..1).</summary>
    static Texel BronzeAt(double mottle, double tarnish, double verd, double polish, double fresh, double dust)
    {
        var col = Vector3.Lerp(BronzeAged, BronzeBright, (float)Saturate(0.35 + 0.65 * mottle + 0.35 * polish));
        col = Vector3.Lerp(col, Tarnish, (float)Saturate(tarnish * (1 - 0.6 * polish)));
        col = Vector3.Lerp(col, new Vector3(0.90f, 0.66f, 0.47f), (float)(fresh * 0.75));
        float rough = (float)(0.30 + 0.18 * (1 - mottle) * 0.5 - 0.14 * polish + 0.30 * tarnish - 0.14 * fresh);
        float metal = 1f;
        var t = Texel.Of(col, Math.Clamp(rough, 0.10f, 0.9f), metal);
        if (verd > 0) { t.Albedo = Vector3.Lerp(t.Albedo, Verdigris, (float)verd); t.Rough = MathHelper.Lerp(t.Rough, 0.85f, (float)verd); t.Metal = MathHelper.Lerp(1f, 0f, (float)verd); }
        if (dust > 0) { t.Albedo = Vector3.Lerp(t.Albedo, Dust, (float)dust); t.Rough = MathHelper.Lerp(t.Rough, 0.92f, (float)dust); t.Metal = MathHelper.Lerp(t.Metal, 0f, (float)dust); }
        return t;
    }

    public static MapSet Paint(AspisSpec spec, KitMesh mesh)
    {
        var maps = new MapSet("aspis", spec.MapSize, spec.MapSize) { CavityStrength = 0.65f, CavityRadius = Math.Max(2, spec.MapSize / 340), CavityDepth = 0.0005f };
        var wear = new Wear(spec);
        uint seed = spec.Seed * 104729u + 7u;
        var paint = MapSet.Hex(spec.Paint);
        var field = MapSet.Hex(spec.Field);
        double w = spec.Wear;

        // ---------------- front: face, wall and rim band (planar projection)
        maps.PaintRect(FrontRect, D, D, 1, (u, v) =>
        {
            double x = (u - 0.5) * D, y = -(v - 0.5) * D;
            double r = Math.Sqrt(x * x + y * y), a = Math.Atan2(y, x);
            double h = 0;
            // planishing: hammer facets (Worley cells ~9 mm) and fine polishing rings
            var (f1, f2, id, _, _) = Worley(x / 0.009, y / 0.009, seed + 3, 0.9);
            double facet = f1 * f1 * 0.55 - 0.12 * Smoothstep(0.0, 0.08, f2 - f1);
            h += 0.00005 * facet * (r < Rb - 0.004 ? 1 : 0.35);
            // the sheet's slight waviness from beating it over the core (a few millimetres of drift over the face)
            h += 0.00025 * (Fbm(x * 7 + 3, y * 7, 3, seed + 4) - 0.5);
            double mottle = Fbm(x * 9, y * 9, 4, seed + 5) * 0.7 + 0.3 * Rand((int)id, 0, 0, seed);
            // wear features
            var (wh, fresh, dentness) = wear.At(x, y);
            h += wh;
            double tarnish = 0.12 + 0.60 * w * Smoothstep(0.42, 0.78, Fbm(x * 3.2 + 7, y * 3.2, 5, seed + 6))
                + 0.25 * w * Smoothstep(0.55, 0.85, Fbm(x * 11, y * 11, 4, seed + 7)) + 0.20 * Smoothstep(0.25, 0.37, r);
            double polish = 0;
            double verd = 0;
            // the dome's crown and the rim's high points are rubbed bright; the wall foot and the groove gather tarnish
            double slopeZone = Smoothstep(Rb - 0.03, Rb - 0.002, r) * Smoothstep(Rb + 0.006, Rb + 0.001, r);
            tarnish += 0.45 * slopeZone;
            polish += 0.35 * Smoothstep(0.20, 0.0, r);
            // rim band: guilloche relief, polished crowns, tarnish and verdigris in the grooves
            var (gh, crown, groove) = Guilloche(r, a);
            if (gh > 0 || groove > 0)
            {
                h += gh;
                polish = Math.Max(polish, 0.75 * crown * crown);
                tarnish = Math.Max(tarnish, 0.75 * groove);
                verd = Math.Max(verd, 0.55 * w * groove * groove * Smoothstep(0.55, 0.75, Fbm(x * 40, y * 40, 3, seed + 8)));
            }
            // the bead at the rim's inner edge
            double bq = Math.Abs(r - BeadR) / 0.0025;
            if (bq < 1) polish = Math.Max(polish, 0.6 * (1 - bq));
            // dust and mud on the lower face (the side that rests on the ground)
            double low = Smoothstep(-0.18, -0.44, y);
            double dust = w * 0.40 * low * Smoothstep(0.45, 0.75, Fbm(x * 38, y * 38, 4, seed + 9)) + 0.15 * w * low * low * (1 - polish);
            double splash = w * Smoothstep(-0.05, -0.42, y) * Smoothstep(0.80, 0.88, Fbm(x * 90, y * 90, 2, seed + 10));
            dust = Saturate(dust + splash * 0.85);
            Texel t;
            bool bronzeFace = spec.BronzeFace || r > 0.3885;
            if (bronzeFace) t = BronzeAt(mottle, Saturate(tarnish), Saturate(verd), Saturate(polish), fresh * (1 - 0.5 * tarnish), 0);
            else
            {
                // a painted wooden field (cheaper shields): paint over gesso, chipped to wood at the blows
                double chipF = Smoothstep(0.80 - 0.12 * w, 0.83 - 0.12 * w, 0.55 * Fbm(x * 130, y * 130, 4, seed + 11) + 0.45 * Fbm(x * 22, y * 22, 3, seed + 39)
                    + 0.35 * dentness + 0.3 * Smoothstep(0.36, 0.385, r));
                var col = field * (float)(0.85 + 0.25 * Fbm(x * 6, y * 6, 3, seed + 12));
                t = Texel.Of(col, 0.62f, 0f, 0.00015f);
                if (chipF > 0.5) { t = Texel.Of(new Vector3(0.36f, 0.25f, 0.14f) * (float)(0.8 + 0.4 * Fbm(x * 80, y * 3, 3, seed + 13)), 0.75f, 0f, -0.0001f); }
            }
            // the painted device (on bronze faces; on painted fields it is a second colour)
            if (spec.Device != "none")
            {
                double dev = Device(spec.Device, x, y, spec.DeviceTurn);
                if (dev > 0)
                {
                    // paint loss: speckled flakes everywhere, large flakes at blows and at the device's edges
                    double edge = 1 - Smoothstep(0.0, 0.6, dev);
                    double flake = Fbm(x * 70, y * 70, 4, seed + 14) + 0.45 * dentness + 0.18 * edge + 0.25 * fresh;
                    double keep = Smoothstep(0.80 - 0.18 * w, 0.74 - 0.18 * w, flake);
                    double brush = 0.9 + 0.2 * Fbm(x * 220 + a * 3, y * 12, 3, seed + 15);
                    var pcol = (spec.BronzeFace ? paint : MapSet.Hex("#C9A24A")) * (float)brush;
                    pcol = Vector3.Lerp(pcol, Dust, (float)(dust * 0.6));
                    float k = (float)(dev * keep);
                    t.Albedo = Vector3.Lerp(t.Albedo, pcol, k);
                    t.Rough = MathHelper.Lerp(t.Rough, (float)(0.55 + 0.15 * Fbm(x * 90, y * 90, 2, seed + 16)), k);
                    t.Metal = MathHelper.Lerp(t.Metal, 0f, k);
                    h += 0.00012 * dev * keep;
                    // paint residue darkens the bronze where it flaked
                    if (dev > 0.5 && keep < 0.5) t.Albedo *= 0.82f;
                }
            }
            if (dust > 0) { t.Albedo = Vector3.Lerp(t.Albedo, Dust, (float)dust); t.Rough = MathHelper.Lerp(t.Rough, 0.92f, (float)dust); t.Metal = MathHelper.Lerp(t.Metal, 0f, (float)dust); }
            t.Height = (float)h;
            return t;
        }, (u, v) => { double x = (u - 0.5) * D, y = -(v - 0.5) * D; return x * x + y * y <= (RimOut + 0.002) * (RimOut + 0.002); });

        // ---------------- lip (rolled edge): u = azimuth, v = across the roll (front to back)
        maps.PaintRect(LipRect, 2 * Math.PI * Outer, Math.PI * LipR, 2, (u, v) =>
        {
            double a = 2 * Math.PI * u;
            double x = Outer * Math.Sin(a), y = -Outer * Math.Cos(a);
            double h = 0;
            // nicks from blows on the edge, most at the top
            double nick = Smoothstep(0.86, 0.95, Fbm(u * 900, 0.5, 2, seed + 20)) * (0.5 + 0.5 * Smoothstep(-0.2, 0.4, y)) * w;
            h -= 0.0006 * nick * Math.Sin(Math.PI * v);
            double crest = Math.Sin(Math.PI * v);     // the outermost line of the roll
            double polish = 0.55 * crest * crest;
            double tarnish = 0.25 * (1 - crest) + 0.25 * w * Smoothstep(0.5, 0.8, Fbm(u * 60, v * 3, 3, seed + 21));
            double dust = w * 0.6 * Smoothstep(0.0, -0.40, y) * (1 - crest * 0.5);
            var t = BronzeAt(Fbm(u * 200, v * 5, 3, seed + 22), Saturate(tarnish), 0, polish, nick * 0.8, Saturate(dust));
            t.Height = (float)h;
            return t;
        });

        // ---------------- back: wood core, the bronze facing turned over the back rim, handling grime
        maps.PaintRect(BackRect, D, D, 3, (u, v) =>
        {
            double x = -(u - 0.5) * D, y = -(v - 0.5) * D;   // mirrored: seen from behind
            double r = Math.Sqrt(x * x + y * y);
            double h = 0;
            Texel t;
            if (r > BronzeBackR)
            {
                double polish = 0.15, tarnish = 0.45 + 0.35 * Fbm(x * 8, y * 8, 4, seed + 30);
                var (f1, _, _, _, _) = Worley(x / 0.012, y / 0.012, seed + 31, 0.9);
                h += 0.00004 * f1 * f1;
                double verd = 0.35 * w * Smoothstep(0.62, 0.8, Fbm(x * 25, y * 25, 3, seed + 32));
                t = BronzeAt(Fbm(x * 9, y * 9, 3, seed + 33), Saturate(tarnish), verd, polish, 0, 0);
                // the turned-over edge of the facing: a hammered seam line
                double seam = Smoothstep(0.003, 0.0, Math.Abs(r - BronzeBackR - 0.002));
                h -= 0.00025 * seam;
            }
            else
            {
                // poplar, carved hollow and oiled dark with age: straight grain along y, adze facets stretched along it
                double gx = x + 0.010 * Perlin(y * 3, x * 0.5, seed + 34);
                double grain = Fbm(gx * 150, y * 5, 4, seed + 35);
                double fig = Ridged(gx * 38, y * 1.6, 3, seed + 36);
                var (g1, _, _, _, _) = Worley(x / 0.022, y / 0.055, seed + 37, 0.9);
                double gouge = g1 * g1;
                var col = Vector3.Lerp(new Vector3(0.16f, 0.095f, 0.045f), new Vector3(0.34f, 0.22f, 0.11f), (float)(0.5 * grain + 0.5 * fig));
                // the adze facets shade continuously (darker towards each facet's rim, where the cuts meet), with no
                // per-facet tone step: a step drew the cells as flat polygons on the albedo
                col *= (float)(1.0 - 0.08 * gouge);
                // hand-polish and grime where the arm and the hand rub: small, darker and smoother
                double plates = Math.Exp(-(x * x) / (0.035 * 0.035) - Math.Pow((Math.Abs(y) - 0.10) / 0.035, 2));
                double hand = Math.Exp(-((x - AntilabeX) * (x - AntilabeX) + y * y) / (0.035 * 0.035));
                double grime = Saturate(0.35 * plates + 0.45 * hand) * (0.6 + 0.4 * w);
                col = Vector3.Lerp(col, col * 0.55f, (float)grime);
                col = Vector3.Lerp(col, col * 0.7f, (float)(Smoothstep(BronzeBackR - 0.025, BronzeBackR, r) * 0.6));
                t = Texel.Of(col, (float)(0.68 - 0.25 * grime - 0.08 * fig), 0f);
                h += 0.00008 * (grain - 0.5) + 0.00010 * fig + 0.00035 * gouge * Smoothstep(0.05, 0.15, r);
                // a few gouges and splits in the wood
                double split = Smoothstep(0.93, 0.97, Fbm(gx * 400, y * 2, 2, seed + 38)) * w;
                h -= 0.0004 * split; t.Albedo *= (float)(1 - 0.5 * split);
            }
            t.Height = (float)h;
            return t;
        }, (u, v) => { double x = (u - 0.5) * D, y = -(v - 0.5) * D; return x * x + y * y <= (RimOut + 0.002) * (RimOut + 0.002); });

        // ---------------- cord strip: two-ply twisted flax cord, darkened by hands and dirt
        maps.PaintRect(CordRect, CordMetres, 0.028, 4, (u, v) =>
        {
            // u along the cord (CordMetres per strip), v around its section; two plies twisted at a 7 mm pitch
            double ph = 2 * Math.PI * (2 * v + u * CordMetres / 0.007);
            double ply = Math.Abs(Math.Sin(ph * 0.5));
            double fib = Fbm(u * 1800, v * 30, 3, seed + 40);
            var col = Vector3.Lerp(new Vector3(0.085f, 0.065f, 0.042f), new Vector3(0.24f, 0.19f, 0.12f), (float)(0.55 * ply + 0.45 * fib));
            var t = Texel.Of(col, 0.85f, 0f, (float)(0.0007 * ply + 0.00005 * fib));
            return t;
        });

        // ---------------- fittings and rivets (rasterised from the mesh)
        maps.PaintMesh(mesh, 5, s =>
        {
            int part = (int)Math.Round(s.Mask.X * 255);
            var p = s.Position;
            double n1 = Fbm(p.X * 60, p.Y * 60 + p.Z * 30, 4, seed + 50);
            switch (part)
            {
                case PartLining:
                {
                    double grain = Fbm(p.X * 400, p.Y * 400 + p.Z * 200, 3, seed + 51);
                    var t = Texel.Of(Vector3.Lerp(new Vector3(0.055f, 0.034f, 0.022f), new Vector3(0.11f, 0.07f, 0.045f), (float)grain), (float)(0.55 + 0.2 * grain), 0f, (float)(0.0001 * grain));
                    return t;
                }
                case PartRivet:
                {
                    // dome heads: polished tops, tarnish round the foot
                    double rr = Math.Sqrt(Math.Pow((s.Uv.X - RivetRect.U0) / RivetRect.Width - 0.5, 2) + Math.Pow((s.Uv.Y - RivetRect.V0) / RivetRect.Height - 0.5, 2)) * 2;
                    var t = BronzeAt(n1, Saturate(0.25 + 0.6 * Smoothstep(0.5, 0.9, rr)), 0.3 * w * Smoothstep(0.75, 0.95, rr), 0.5 * Smoothstep(0.5, 0.1, rr), 0, 0);
                    t.Height = (float)(-0.00005 * Fbm(p.X * 900, p.Y * 900, 2, seed + 52));
                    return t;
                }
                case PartPlate:
                case PartBand:
                case PartStaple:
                {
                    // cast and filed bronze: engraved border line on the plates, the band's inside rubbed bright
                    double tarnish = 0.30 + 0.35 * Smoothstep(0.4, 0.8, n1);
                    double polish = part == PartBand ? 0.35 : 0.15;
                    var t = BronzeAt(Fbm(p.X * 30, p.Y * 30, 3, seed + 53), Saturate(tarnish), 0.25 * w * Smoothstep(0.7, 0.9, n1), polish, 0, 0);
                    t.Height = (float)(0.00003 * (Fbm(p.X * 300, p.Y * 300 + p.Z * 300, 3, seed + 54) - 0.5));
                    return t;
                }
                default:
                    return Texel.Of(new Vector3(0.5f), 0.5f, 0f);
            }
        }, t =>
        {
            int i0 = mesh.Indices[3 * t];
            int part = mesh.Masks[i0].R;
            return part == PartLining || part == PartRivet || part == PartPlate || part == PartBand || part == PartStaple;
        });
        return maps.Finish(dilatePasses: 6);
    }
}
