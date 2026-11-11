using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Gfx.Gear.Geometry;
using Gfx.Gear.Maps;
using Gfx.Gear.Runtime;
using Microsoft.Xna.Framework;
using static Gfx.Gear.Maps.Noise;

namespace Gfx.Gear.Items;

/// <summary>Parameters of a standard: a cloth hung from a crossbar on a pole, with a painted device and a fringe.</summary>
public sealed record BannerSpec
{
    public uint Seed { get; init; } = 1;
    public int MapSize { get; init; } = 1024;
    public string Field { get; init; } = "#2B4373";
    public string Charge { get; init; } = "#C29A45";
    /// <summary>A device from <see cref="Aspis.Device"/> (sun, crescent, wheel) or <see cref="Shields.Device"/> (spiral, chevron, ...).</summary>
    public string Device { get; init; } = "sun";
    public string Fringe { get; init; } = "#C29A45";
}

/// <summary>
/// A standard of the era kit: an ash pole with a bronze finial, a crossbar, and a wool cloth (0.62 x 0.82 m) painted
/// with the force's device, edged with a woven band and a fringe of cut threads. The cloth is skinned to a chain of
/// five bones from the crossbar down, which <see cref="Pose"/> swings with a travelling wave (a deterministic function
/// of scene time and the wind), so the cloth ripples without a cloth simulation. PRESENTATION ONLY.
/// </summary>
public sealed class Banner
{
    public const int Rows = 5;
    public const double W = 0.62, H = 0.82, PoleH = 2.70, BarY = 2.42;
    public KitItem Item { get; }
    public CharInstance Instance { get; }

    Banner(KitItem item, CharInstance inst) { Item = item; Instance = inst; }

    static readonly UvRect ClothRect = new(0f, 0f, 0.62f, 0.82f), ClothBack = new(0.62f, 0f, 1f, 0.5f), PoleRect = new(0.62f, 0.5f, 1f, 0.8f), FringeRect = new(0f, 0.84f, 0.62f, 1f), MetalRect = new(0.62f, 0.8f, 1f, 1f);
    const int PartCloth = 10, PartPole = 20, PartFringe = 30, PartMetal = 40;

    public static KitItem Build(BannerSpec s)
    {
        var item = new KitItem { Id = $"banner#{s.Seed}", Kind = "banner", Era = "ancient", Label = "Standard (painted wool cloth on a crossbar)" };
        var m = new KitMesh("banner");
        // pole and crossbar
        m.Append(Shapes.Tube(new List<Vector3d> { new(0, 0, 0), new(0, PoleH, 0) }, 0.020, 12, PoleRect, new Color(PartPole, 0, 0, 255), up: new Vector3d(0, 0, 1), caps: true));
        m.Append(Shapes.Tube(new List<Vector3d> { new(-W / 2 - 0.05, BarY, 0.02), new(W / 2 + 0.05, BarY, 0.02) }, 0.012, 10, PoleRect, new Color(PartPole, 0, 0, 255), up: new Vector3d(0, 1, 0), caps: true));
        // finial: a bronze leaf point
        var fin = Shapes.Lathe(new List<Vector2d> { new(0, 0.20), new(0.012, 0.15), new(0.030, 0.07), new(0.022, 0.02), new(0.016, 0.0) }, 16, MetalRect, new Color(PartMetal, 0, 0, 255));
        fin.Transform(Matrix.CreateScale(1f, 1f, 0.35f) * Matrix.CreateTranslation(0, (float)PoleH, 0));
        m.Append(fin);
        // cloth: a grid hanging from the crossbar (front and back faces), with a slight rest wave
        int nu = 24, nv = 32;
        foreach (bool back in new[] { false, true })
        {
            var grid = Shapes.Grid(nu, nv, (u, v) => new Vector3d(-W / 2 + W * u, BarY - 0.01 - H * v, 0.035 + 0.012 * Math.Sin(u * Math.PI * 2.2 + v * 1.3) + (back ? -0.0015 : 0.0)),
                back ? ClothBack : ClothRect, new Color(PartCloth, 0, back ? 255 : 0, 255));
            if (back) { for (int i = 0; i < grid.VertexCount; i++) { var uv = grid.Uvs[i]; grid.Uvs[i] = new Vector2(ClothBack.U1 - (uv.X - ClothBack.U0), uv.Y); } grid.FlipWinding(); }
            m.Append(grid);
        }
        // fringe: cut-thread cards along the hem (alpha tested)
        for (int i = 0; i < 2; i++)
        {
            var f = Shapes.Grid(24, 1, (u, v) => new Vector3d(-W / 2 + W * u, BarY - 0.01 - H - 0.07 * v, 0.035 + 0.012 * Math.Sin(u * Math.PI * 2.2 + 1.3) + (i == 1 ? -0.0015 : 0)), FringeRect, new Color(PartFringe, 0, 0, 255));
            if (i == 1) f.FlipWinding();
            m.Append(f);
        }
        m.Finish(60f);
        item.Parts.Add(new KitPart { Name = "banner", Mesh = m, Maps = "banner", AlphaTest = true, Sheen = 0.5f });
        if (s.MapSize > 0) item.Maps["banner"] = Paint(s, m);
        item.Frames["grip"] = KitItem.MakeFrame(new Vector3(0, 1.1f, 0), Vector3.UnitY, Vector3.UnitZ);
        item.Frames["ground"] = Matrix.Identity;
        return item;
    }

    static MapSet Paint(BannerSpec s, KitMesh mesh)
    {
        var maps = new MapSet("banner", s.MapSize, s.MapSize) { CavityStrength = 0.3f, CavityRadius = 2, CavityDepth = 0.0004f };
        var field = MapSet.Hex(s.Field); var charge = MapSet.Hex(s.Charge); var fringe = MapSet.Hex(s.Fringe);
        uint seed = s.Seed * 40503u + 3u;
        maps.PaintMesh(mesh, 1, sf =>
        {
            int part = (int)Math.Round(sf.Mask.X * 255);
            var p = sf.Position;
            switch (part)
            {
                case PartCloth:
                {
                    double x = p.X, y = p.Y - (BarY - 0.01 - H / 2);
                    double border = Math.Min(W / 2 - Math.Abs(x), H / 2 - Math.Abs(y));
                    bool backSide = sf.Mask.Y > 0.5;
                    double dev = s.Device is "sun" or "crescent" or "wheel" ? Aspis.Device(s.Device, backSide ? -x : x, y, 0) * 1.0 : Shields.Device(s.Device, backSide ? -x : x, y, 1.6);
                    // scale the aspis devices down to the cloth
                    if (s.Device is "sun" or "crescent" or "wheel") dev = Aspis.Device(s.Device, (backSide ? -x : x) * 1.45, y * 1.45, 0);
                    var col = Vector3.Lerp(field, charge, (float)dev);
                    if (border < 0.035) col = Vector3.Lerp(charge * 0.9f, field * 0.6f, (float)(0.5 + 0.5 * Math.Sin(x * 260 + y * 260)));
                    var t = Materials.Cloth(x, y, seed, col, 0.0016);
                    t.Rough = 0.88f;
                    double fade = 0.12 * Fbm(x * 6, y * 6, 3, seed + 1);
                    t.Albedo *= (float)(1 - fade);
                    return t;
                }
                case PartFringe:
                {
                    double u = (sf.Uv.X - FringeRect.U0) / FringeRect.Width, v = (sf.Uv.Y - FringeRect.V0) / FringeRect.Height;
                    double thread = 0.5 + 0.5 * Math.Sin(u * Math.PI * 2 * 90);
                    var t = Texel.Of(fringe * (float)(0.7 + 0.3 * thread), 0.9f, 0f);
                    t.Alpha = (float)(Smoothstep(0.35, 0.6, thread) * Smoothstep(1.0, 0.75 + 0.2 * Rand((int)(u * 90), 0, 0, seed), v));
                    return t;
                }
                case PartMetal: return Materials.Bronze(Fbm(p.X * 80, p.Y * 80, 3, seed + 2), 0.3, 0.1, 0.4, 0, 0);
                default: return Materials.Wood(p.X + p.Z, p.Y, seed + 3, new Vector3(0.40f, 0.28f, 0.16f), new Vector3(0.20f, 0.13f, 0.07f));
            }
        });
        return maps.Finish(4);
    }

    /// <summary>Adds the banner as a rigged prop (cloth on a bone chain) at a world transform (the pole's foot).</summary>
    public static Banner Create(KitWorld world, KitItem item, Matrix at)
    {
        var f = new CharacterFile();
        f.Info["id"] = "prop:" + item.Id + ":rig";
        f.Bones.Add(new BoneDef("root", -1, Vector3.Zero, Vector3.UnitY));
        for (int r = 0; r < Rows; r++)
        {
            float y = (float)(BarY - 0.01 - H * r / Rows);
            f.Bones.Add(new BoneDef("row" + r, r, new Vector3(0, y, 0.035f), new Vector3(0, y - (float)(H / Rows), 0.035f)));
        }
        var mesh = item.Parts[0].Mesh;
        var def = KitGpu.ToMeshDef(mesh, "banner", "kit", 0, skin: i =>
        {
            int part = mesh.Masks[i].R;
            if (part != PartCloth && part != PartFringe) return new[] { (0, 1f) };
            double fy = (BarY - 0.01 - mesh.Positions[i].Y) / H * Rows;   // 0 at the bar .. Rows at the hem
            int r0 = Math.Clamp((int)Math.Floor(fy), 0, Rows - 1);
            float w = (float)Math.Clamp(fy - r0, 0, 1);
            if (r0 == Rows - 1) return new[] { (1 + r0, 1f) };
            return new[] { (1 + r0, 1 - w), (2 + r0, w) };
        });
        f.Meshes.Add(def);
        f.Info["materials"] = new System.Text.Json.Nodes.JsonObject { ["banner"] = new System.Text.Json.Nodes.JsonObject { ["kind"] = "kit", ["maps"] = "banner", ["normal"] = 1.0, ["alpha"] = 1.0, ["sheen"] = 0.5 } };
        var inst = world.AddAsset(f, item, 3.0f);
        inst.World = at;
        inst.BoundsCentre = new Vector3(0, 1.5f, 0);
        return new Banner(item, inst);
    }

    /// <summary>Swings the cloth: each row bone turns about the crossbar's axis by a travelling wave (wind 0..1).</summary>
    public void Pose(float t, float wind = 0.6f)
    {
        var p = Instance.Pose;
        p.Reset();
        for (int r = 0; r < Rows; r++)
        {
            float phase = t * 2.1f - r * 0.75f;
            float swing = wind * (0.10f + 0.05f * r) * MathF.Sin(phase) + wind * 0.08f;   // a mean lift away from the pole too
            float twist = wind * 0.05f * MathF.Sin(phase * 0.7f + 1.3f);
            p.Local[1 + r] = Q.Mul(Quaternion.CreateFromAxisAngle(Vector3.UnitY, twist), Quaternion.CreateFromAxisAngle(Vector3.UnitX, swing));
        }
    }
}
