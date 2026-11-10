using Gfx.Engine.Cameras;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Engine.Lighting;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Environment.Structures;
using Gfx.Proc.Environment.Trees;
using Gfx.Proc.Presentation;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Environment;

/// <summary>
/// Base of the environment scenes: generate the authoritative grid from labelled fixture inputs (lane T), build the
/// environment on it (read only) and render. Shots are found on the generated map at load.
/// </summary>
public abstract partial class EnvSceneBase : DemoScene, ITransparentContent, IPlanarReflectionContent
{
    public abstract BattleTerrainInputs Inputs { get; }
    public BattleTerrainGrid Grid { get; private set; } = null!;
    public GenerationTrace Trace { get; private set; } = null!;
    public EnvironmentScenery Env { get; private set; } = null!;
    protected CaptureShot[] ShotList = Array.Empty<CaptureShot>();
    public override IReadOnlyList<CaptureShot> Shots => ShotList;

    /// <summary>Exclusions and set-piece trees for the scene (computed from the grid before the environment is built).</summary>
    protected virtual (IReadOnlyList<Exclusion> exclusions, IReadOnlyList<PlacedTree>? extra, bool forest) Plan(TerrainMeshData mesh) => (Array.Empty<Exclusion>(), null, true);
    protected virtual EnvironmentOptions Options => EnvironmentOptions.Default;
    /// <summary>The settlement of the scene (planned and built on the generated grid, read only), or none.</summary>
    protected virtual Func<TerrainMeshData, WaterField, StructureKit?, SettlementPlan>? Settlement => null;
    protected abstract void MakeShots();

    protected override void OnLoad()
    {
        Grid = BattleTerrainGenerator.Generate(Inputs, out var trace);
        Trace = trace;
        var probe = TerrainMeshData.Build(Grid);
        var (ex, extra, forest) = Plan(probe);
        Env = new EnvironmentScenery(Device, Effects, Grid, ex, Options, extra, forest, trace, Settlement);
        MakeShots();
    }

    public override void Configure(RenderSettings settings)
    {
        base.Configure(settings);
        settings.FogParams = Env.Terrain.Weather.FogParams;
        settings.ShadowCasterBounds = Env.Terrain.CasterBounds;
        settings.Features.Haze = Env.Terrain.Haze;
        settings.Features.StillSamples = Math.Max(settings.Features.StillSamples, 1);
    }

    public override float ShadowRadius => 200f;
    public override void Update(float t) { Env?.Update(t); }
    public override void DrawShadowCasters(RenderContext rc) => Env.DrawShadowCasters(rc);
    public override void DrawOpaque(RenderContext rc) => Env.DrawOpaque(rc);
    public bool NeedsSceneColor(RenderContext rc) => Env.NeedsSceneColor(rc);
    public void DrawTransparent(RenderContext rc) => Env.DrawTransparent(rc);
    public Vector4? ReflectionPlane(RenderContext rc) => Env.ReflectionPlane(rc);
    public float ReflectionScale => 0.5f;
    public override void Dispose() => Env?.Dispose();

    protected Vector3 Ground(float x, float z) => new(x, Env.Terrain.HeightAt(x, z), z);
    protected Vector3 Ground(Vector2 p) => Ground(p.X, p.Y);
    protected CameraPose Eye(Vector2 at, float up, Vector2 look, float lookUp, float fov = 0.8f, float far = 40000f) =>
        new(Ground(at) + new Vector3(0, up, 0), Ground(look) + new Vector3(0, lookUp, 0), fov, 0.3f, far);
}

/// <summary>
/// Species line-up on the temperate fixture's flattest meadow: every temperate tree variant in a row, for close
/// inspection of bark, leaf cards, translucency and shading (no forest around it).
/// </summary>
[DemoScene("env-trees", "env: temperate species line-up (oak, ash, aspen, pine, shrub variants) on the meadow")]
public sealed class EnvTreesScene : EnvSceneBase
{
    public override BattleTerrainInputs Inputs => TerrainTemperateScene.FixtureInputs;
    Vector3 _site; float _yaw;
    // position along the row (m) of each species' first and last tree, for the line-up shots
    readonly Dictionary<string, (float first, float last)> _rowSpan = new(StringComparer.Ordinal);

    protected override (IReadOnlyList<Exclusion>, IReadOnlyList<PlacedTree>?, bool) Plan(TerrainMeshData mesh)
    {
        (_site, _yaw) = FlatStrip(mesh, 150, 16);
        var list = new List<PlacedTree>();
        var dir = new Vector3(MathF.Cos(_yaw), 0, MathF.Sin(_yaw)); var side = new Vector3(-dir.Z, 0, dir.X);
        PlacedTree At(TreeSpecies sp, int v, Vector3 p, float yaw, float phase) =>
            new(sp, v, new Vector3(p.X, mesh.HeightAt(p.X, p.Z) - 0.1f, p.Z), yaw, 1f, Vector3.One, phase, ForestLayout.ZoneOpen);
        // line-up row (34 m to one side): every temperate tree variant, 14 m apart, inside the flat strip (±150 m);
        // the shrubs on a short row 14 m behind it
        float t = -154f, ts = -14f; int k = 0;
        foreach (var sp in TreeSpecies.Temperate)
            for (int v = 0; v < sp.VariantCount; v++)
            {
                float at; Vector3 p;
                if (sp.IsShrub) { ts += 7f; at = ts; p = _site + side * 48f + dir * at; }
                else { t += 14f; at = t; p = _site + side * 34f + dir * at; }
                list.Add(At(sp, v, p, 0.9f * k, 0.13f * k));
                _rowSpan[sp.Name] = _rowSpan.TryGetValue(sp.Name, out var span) ? (span.first, at) : (at, at);
                k++;
            }
        // LOD sweep row: one oak variant every 16 m receding from the camera (15 m to 290 m)
        for (int j = 0; j < 18; j++) list.Add(At(TreeSpecies.Oak, 1, _site - dir * 140f + dir * (15f + 16f * j) + side * 2f, 0.7f * j, 0.21f * j));
        return (Array.Empty<Exclusion>(), list, false);
    }

    /// <summary>Flattest dry open strip (centre, yaw) near the map centre (read-only search).</summary>
    static (Vector3, float) FlatStrip(TerrainMeshData m, float halfLen, float halfWid)
    {
        float best = float.MaxValue; var bc = Vector3.Zero; float by = 0;
        for (int a = 0; a < 8; a++)
        {
            float yaw = a * MathF.PI / 8;
            var dir = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw)); var side = new Vector2(-dir.Y, dir.X);
            for (float cx = -320; cx <= 320; cx += 20)
                for (float cz = -320; cz <= 320; cz += 20)
                {
                    float lo = float.MaxValue, hi = float.MinValue, pen = 0;
                    for (float t = -halfLen; t <= halfLen; t += 8)
                        for (float s = -halfWid; s <= halfWid + 40; s += 9)
                        {
                            var p = new Vector2(cx, cz) + dir * t + side * s;
                            if (MathF.Abs(p.X) > m.HalfExtentX - 20 || MathF.Abs(p.Y) > m.HalfExtentZ - 20) { pen += 1e5f; continue; }
                            var c = m.ClassAt(p.X, p.Y);
                            if (m.IsWet(p.X, p.Y) || c == TerrainClass.Water || c == TerrainClass.Road) pen += 500;
                            if (c == TerrainClass.Forest || c == TerrainClass.Mountain) pen += 60;
                            float h = m.HeightAt(p.X, p.Y); lo = MathF.Min(lo, h); hi = MathF.Max(hi, h);
                        }
                    float sc = pen + (hi - lo) * 30;
                    if (sc < best) { best = sc; bc = new Vector3(cx, 0, cz); by = yaw; }
                }
        }
        bc.Y = m.HeightAt(bc.X, bc.Z);
        return (bc, by);
    }

    protected override void MakeShots()
    {
        var dir = new Vector2(MathF.Cos(_yaw), MathF.Sin(_yaw)); var side = new Vector2(-dir.Y, dir.X);
        var c = new Vector2(_site.X, _site.Z);
        var row = c + side * 34f;
        // each line-up shot looks at the boundary between two species: the last two of one and the first two of the next
        float Between(string a, string b) => (_rowSpan[a].last + _rowSpan[b].first) * 0.5f;
        float oakAsh = Between(TreeSpecies.Oak.Name, TreeSpecies.Ash.Name), aspenPine = Between(TreeSpecies.Aspen.Name, TreeSpecies.Pine.Name);
        float shrubs = (_rowSpan[TreeSpecies.Shrub.Name].first + _rowSpan[TreeSpecies.Shrub.Name].last) * 0.5f;
        float oak0 = _rowSpan[TreeSpecies.Oak.Name].first, oakMid = (oak0 + _rowSpan[TreeSpecies.Oak.Name].last) * 0.5f;
        ShotList = new[]
        {
            // line-up cameras stand between the LOD-sweep row (2 m to the side) and the line-up row (34 m)
            new CaptureShot("lineup-oak-ash", Eye(row - side * 26f + dir * oakAsh, 5f, row + dir * oakAsh, 9f, 0.95f), 10.5f, 2f),
            new CaptureShot("lineup-aspen-pine", Eye(row - side * 26f + dir * aspenPine, 5f, row + dir * aspenPine, 9f, 0.95f), 10.5f, 2f),
            new CaptureShot("lineup-shrubs", Eye(row + side * 30f + dir * shrubs, 2.2f, row + side * 14f + dir * shrubs, 1.5f, 0.95f), 10.5f, 2f),
            new CaptureShot("oak-close", Eye(row - side * 16f + dir * (oak0 + 8f), 2.0f, row + dir * oak0, 7f, 0.9f), 15.5f, 2f),
            new CaptureShot("backlit", Eye(row + side * 30f + dir * oakMid, 3f, row + dir * oakMid, 9f, 0.95f), 17.4f, 2f),
            new CaptureShot("lod-sweep", Eye(c - dir * 140f + side * 9f, 4f, c - dir * 140f + dir * 200f + side * 2f, 6f, 0.55f), 11f, 2f),
        };
    }
}

public abstract partial class EnvSceneBase
{
    /// <summary>
    /// The hour (7.5–17) whose sun is behind the camera and about 35° to one side at 18° elevation or more, so the
    /// faces the camera looks at are lit with some modelling (presentation choice of the time of day per shot).
    /// </summary>
    protected static float SunBehind(Vector3 from, Vector3 to, float offAxisDeg = 35f, float minElevDeg = 18f)
    {
        var v = new Vector2(to.X - from.X, to.Z - from.Z); v = Vector2.Normalize(v + new Vector2(1e-5f, 0));
        float best = float.MaxValue, bh = 10.5f;
        for (float h = 7.5f; h <= 17.01f; h += 0.25f)
        {
            var sun = SunModel.At(h);
            var d = sun.SunDirectionOrLight;
            float elev = MathHelper.ToDegrees(MathF.Asin(Math.Clamp(d.Y, -1f, 1f)));
            if (elev < minElevDeg) continue;
            var sh = Vector2.Normalize(new Vector2(d.X, d.Z) + new Vector2(1e-5f, 0));
            float ang = MathHelper.ToDegrees(MathF.Acos(Math.Clamp(Vector2.Dot(sh, -v), -1f, 1f)));
            float sc = MathF.Abs(ang - offAxisDeg) + MathF.Max(0, elev - 55f);
            if (sc < best) { best = sc; bh = h; }
        }
        return bh;
    }

    /// <summary>
    /// Settlement shots: an elevated three-quarter view from outside (sun behind the camera) and the gate at eye
    /// level, a little off its axis so the gate's depth reads.
    /// </summary>
    protected IEnumerable<CaptureShot> SettlementShots(string wide, string gate, float hour = 10.5f, float gateHour = 10.5f)
    {
        var s = Env.Settlement;
        if (s == null) yield break;
        var m = Env.Terrain.Mesh;
        var o = s.GateOut; var side = new Vector2(-o.Y, o.X);
        // wide: from outside, 2.6 radii out, 40° round from the gate, raised
        var wp = s.Centre + Vector2.Normalize(o * 0.75f + side * 0.65f) * (s.Radius * 2.4f);
        float wy = MathF.Max(m.HeightAt(wp.X, wp.Y) + 2f, m.HeightAt(s.Centre.X, s.Centre.Y) + s.Radius * 0.55f);
        var wEye = new Vector3(wp.X, wy, wp.Y); var wAt = new Vector3(s.Centre.X, m.HeightAt(s.Centre.X, s.Centre.Y) + 2f, s.Centre.Y);
        yield return new CaptureShot(wide, new CameraPose(wEye, wAt, 0.8f, 0.3f, 40000f), SunBehind(wEye, wAt), 4f);
        // gate: out on the approach, a little to the side; eye level, not below the threshold
        bool town = s.WalkY > 0;
        var gp = s.Gate + o * (town ? 40f : 21f) + side * (town ? 10f : 7f);
        float gGround = m.HeightAt(s.Gate.X, s.Gate.Y);
        var gy = MathF.Max(m.HeightAt(gp.X, gp.Y) + 1.7f, gGround + (town ? 2.6f : 1.2f));
        // see the threshold: raise the eye until the sight line to the foot of the gate clears every rise between
        // (a crest on the approach otherwise hides the foot of the wall), at most 12 m above the gate
        float foot = gGround + 0.3f;
        for (float f = 0.05f; f <= 0.9f; f += 0.05f)
        {
            var q = Vector2.Lerp(gp, s.Gate, f);
            float need = (m.HeightAt(q.X, q.Y) + 0.25f - f * foot) / (1f - f);
            gy = MathF.Max(gy, MathF.Min(need, gGround + 12f));
        }
        var gt = s.Gate - o * 4f;
        var gEye = new Vector3(gp.X, gy, gp.Y); var gAt = new Vector3(gt.X, gGround + (town ? 5.0f : 3.2f), gt.Y);
        yield return new CaptureShot(gate, new CameraPose(gEye, gAt, town ? 0.9f : 0.85f, 0.3f, 40000f), SunBehind(gEye, gAt, 25f), 4f);
    }

    /// <summary>River shots from the generation trace: on the bank looking downstream, and a higher view across.</summary>
    protected IEnumerable<CaptureShot> RiverShots(float at = 0.42f, string name = "river")
    {
        var path = Trace.RiverCells;
        if (path.Length < 20) yield break;
        var m = Env.Terrain.Mesh;
        int k = (int)(path.Length * at);
        Vector2 P(int i) { var s = m.SamplePosition(path[Math.Clamp(i, 0, path.Length - 1)] % Grid.Width, path[Math.Clamp(i, 0, path.Length - 1)] / Grid.Width); return new Vector2(s.X, s.Z); }
        var c = P(k);
        var down = Vector2.Normalize(P(k + 6) - P(k - 6));
        var side = new Vector2(-down.Y, down.X);
        float surf = Env.Water.SurfaceAt(c.X, c.Y);
        // stand on the bank: walk sideways until dry ground 1.5 m above the water
        var bank = c;
        for (float d = 4; d < 40; d += 1f)
        {
            var q = c + side * d;
            if (!m.IsWet(q.X, q.Y) && m.HeightAt(q.X, q.Y) > surf + 0.6f) { bank = q + side * 1.5f; break; }
        }
        float gy = m.HeightAt(bank.X, bank.Y);
        var eye = new Vector3(bank.X, MathF.Max(gy + 1.7f, surf + 2.2f), bank.Y);
        var look = c + down * 38f - side * 2f;
        var lookAt = new Vector3(look.X, surf + 0.5f, look.Y);
        // the sun behind the viewer's shoulder: looking into the sun the far reach is one blown-out glitter sheet
        yield return new CaptureShot(name, new CameraPose(eye, lookAt, 0.85f, 0.3f, 40000f), SunBehind(eye, lookAt, 40f, 25f), 5f);
        var high = c - down * 30f + side * 45f;
        var hy = MathF.Max(m.HeightAt(high.X, high.Y) + 2f, surf + 26f);
        yield return new CaptureShot(name + "-high", new CameraPose(new Vector3(high.X, hy, high.Y), new Vector3(c.X + down.X * 40f, surf, c.Y + down.Y * 40f), 0.8f, 0.3f, 40000f), 15.6f, 5f);
    }
}

/// <summary>
/// The temperate battlefield environment: forest with clearings and edge mantle, river, settlement (later steps),
/// with the shots of the environment brief.
/// </summary>
[DemoScene("env-temperate", "env: temperate battlefield environment (forest, river, Iron Age village)")]
public sealed class EnvTemperateScene : EnvSceneBase
{
    public override BattleTerrainInputs Inputs => TerrainTemperateScene.FixtureInputs;
    protected override Func<TerrainMeshData, WaterField, StructureKit?, SettlementPlan>? Settlement =>
        (m, w, kit) => Settlements.IronAgeVillage(m, w, (uint)(Grid.Seed ^ (Grid.Seed >> 32)), kit);

    protected override void MakeShots()
    {
        var m = Env.Terrain.Mesh;
        // forest edge: an open sample just outside the warped edge, near the centre, looking along the edge
        Vector2 best = Vector2.Zero; float bestScore = float.MaxValue;
        for (int y = 40; y < Grid.Height - 40; y += 2)
            for (int x = 40; x < Grid.Width - 40; x += 2)
            {
                float f = Env.Forest.ForestField[y * Grid.Width + x];
                var p = m.SamplePosition(x, y);
                if (f > -16f || f < -24f || m.IsWet(p.X, p.Z) || m.ClassAt(p.X, p.Z) != TerrainClass.Open) continue;
                float sc = new Vector2(p.X, p.Z).Length() + MathF.Abs(1 - m.NormalAt(p.X, p.Z).Y) * 800;
                if (sc < bestScore) { bestScore = sc; best = new Vector2(p.X, p.Z); }
            }
        // direction towards the forest: gradient of the field
        float F(Vector2 q) => ForestLayout.Sample(Env.Forest.ForestField, m, q.X, q.Y);
        var grad = new Vector2(F(best + new Vector2(4, 0)) - F(best - new Vector2(4, 0)), F(best + new Vector2(0, 4)) - F(best - new Vector2(0, 4)));
        var toForest = grad.LengthSquared() > 1e-6f ? Vector2.Normalize(grad) : Vector2.UnitX;
        var along = new Vector2(-toForest.Y, toForest.X);
        var shots = new List<CaptureShot>
        {
            new("forest-edge", Eye(best - toForest * 4f, 1.8f, best + toForest * 24f + along * 40f, 5f, 0.85f), 10f, 3f),
            new("vista", Eye(new Vector2(-380, 420), 140f, new Vector2(120, -80), 0f, 0.8f), 10.5f, 3f),
        };
        shots.AddRange(RiverShots());
        shots.AddRange(SettlementShots("settlement", "settlement-gate"));
        ShotList = shots.ToArray();
    }
}

/// <summary>
/// The arid battlefield environment: maquis scrub, juniper and pines on the coast fixture, and a Classical walled town
/// with its gate.
/// </summary>
[DemoScene("env-arid", "env: arid battlefield environment (maquis, Classical walled town and gate, coast)")]
public sealed class EnvAridScene : EnvSceneBase
{
    public override BattleTerrainInputs Inputs => TerrainAridScene.FixtureInputs;
    protected override Func<TerrainMeshData, WaterField, StructureKit?, SettlementPlan>? Settlement =>
        (m, w, kit) => Settlements.ClassicalTown(m, w, (uint)(Grid.Seed ^ (Grid.Seed >> 32)), kit);

    protected override void MakeShots()
    {
        var shots = new List<CaptureShot>();
        shots.AddRange(SettlementShots("fortification", "fortification-gate", 9.8f, 9.8f));
        var s = Env.Settlement!;
        var m = Env.Terrain.Mesh;
        // inside the town: along the main street towards the temple
        var inside = s.Inside;
        var o = s.GateOut; var side = new Vector2(-o.Y, o.X);
        var ip = inside + o * 20f + side * 3f;
        var sEye = new Vector3(ip.X, m.HeightAt(ip.X, ip.Y) + 1.7f, ip.Y);
        var sAt = new Vector3(inside.X - o.X * 30 - side.X * 14, m.HeightAt(inside.X, inside.Y) + 4f, inside.Y - o.Y * 30 - side.Y * 14);
        shots.Add(new CaptureShot("town-street", new CameraPose(sEye, sAt, 0.9f, 0.3f, 40000f), SunBehind(sEye, sAt, 30f, 38f), 4f));
        // maquis close to the town
        var mp = s.Centre + o * (s.Radius + 60f) - side * 40f;
        shots.Add(new CaptureShot("maquis", new CameraPose(new Vector3(mp.X, m.HeightAt(mp.X, mp.Y) + 1.8f, mp.Y),
            new Vector3(s.Centre.X, m.HeightAt(s.Centre.X, s.Centre.Y) + 6f, s.Centre.Y), 0.85f, 0.3f, 40000f), 16.2f, 4f));
        shots.Add(new CaptureShot("vista", Eye(s.Centre + o * 330f + side * 200f, 120f, s.Centre, 0f, 0.8f), 10.5f, 4f));
        ShotList = shots.ToArray();
    }
}
