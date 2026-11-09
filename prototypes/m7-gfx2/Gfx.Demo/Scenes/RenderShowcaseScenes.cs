using Gfx.Engine.Cameras;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Presentation;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework;

namespace Gfx.Demo.Scenes;

/// <summary>
/// Stage render1 showcase base: the temperate fixture battlefield (lane T terrain with the G2 photographic materials,
/// far field, sky and shadows) as a stage for renderer close-ups. PRESENTATION ONLY: the grid is generated from
/// labelled fixture inputs and only read.
/// </summary>
public abstract class Render1SceneBase : DemoScene
{
    public BattleTerrainGrid Grid { get; private set; } = null!;
    public TerrainScenery Scenery { get; private set; } = null!;

    protected override void OnLoad()
    {
        Grid = BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs);
        Scenery = new TerrainScenery(Device, Grid);
    }

    public override void Configure(RenderSettings settings)
    {
        base.Configure(settings);
        settings.FogParams = Scenery.Weather.FogParams;
        settings.ShadowCasterBounds = Scenery.CasterBounds;
        settings.Features.Haze = Scenery.Haze;
    }

    public override float ShadowRadius => 60f;
    public override void Update(float t) { if (Scenery != null) Scenery.TimeSeconds = t; }
    public override void DrawShadowCasters(RenderContext rc) => Scenery.DrawShadowCasters(rc);
    public override void DrawOpaque(RenderContext rc) => Scenery.DrawOpaque(rc);
    public override void Dispose() => Scenery?.Dispose();

    protected Vector3 Ground(float x, float z) => new(x, Scenery.HeightAt(x, z), z);
}

/// <summary>
/// PBR calibration spheres: a white dielectric row, a gold row and an iron row, roughness 0 → 1 left to right, on the
/// flattest open spot of the meadow. Shows GGX highlights, split-sum IBL reflections (sky or HDRI) and shadows.
/// </summary>
[DemoScene("render1-materials", "render1: PBR spheres (dielectric, gold, iron; roughness 0..1) on the photographic meadow")]
public sealed class Render1MaterialsScene : Render1SceneBase
{
    GpuMesh? _sphere; InstanceBuffer<InstanceTransform>? _inst; Vector3 _site;
    const float R = 0.42f;
    public static readonly Vector3 Gold = new(1.0f, 0.77f, 0.34f), Iron = new(0.56f, 0.57f, 0.58f), White = new(0.80f, 0.80f, 0.78f);

    protected override void OnLoad()
    {
        base.OnLoad();
        _site = BattleSite.FindStage(Scenery, Vector3.Zero, 220, 9);
        _sphere = GpuMesh.Create(Device, Primitives.Sphere(R, 48, Color.White));
        var list = new List<InstanceTransform>();
        (Vector3 c, float metal)[] rows = { (White, 0), (Gold, 1), (Iron, 1) };
        for (int r = 0; r < rows.Length; r++)
            for (int i = 0; i < 6; i++)
            {
                float x = _site.X + (i - 2.5f) * 1.15f, z = _site.Z - r * 1.3f;
                var p = Ground(x, z) + new Vector3(0, R + 0.02f, 0);
                float rough = i / 5f;
                list.Add(InstanceTransform.From(Matrix.CreateTranslation(p), new Vector4(rows[r].c, rows[r].metal > 0 ? 2 + rough : rough)));
            }
        _inst = new InstanceBuffer<InstanceTransform>(Device, list.Count);
        _inst.Set(list.ToArray());
    }

    public override Vector3 Focus => _site;
    CameraPose Cam(float back, float up, float side = 0) =>
        new(Ground(_site.X + side, _site.Z + back) + new Vector3(0, up, 0), _site + new Vector3(0, 0.55f, -1.3f), 0.62f, 0.2f, 3000f);

    public override IReadOnlyList<CaptureShot> Shots => new[]
    {
        new CaptureShot("noon", Cam(6.6f, 3.0f), 11.5f, 0f),
        new CaptureShot("golden", Cam(6.6f, 3.0f, -1.5f), 17.9f, 0f),
    };

    public override void DrawShadowCasters(RenderContext rc)
    {
        base.DrawShadowCasters(rc);
        _sphere!.DrawInstanced(rc, rc.Effects.Get(EffectNames.InstancedDepth), _inst!);
    }

    public override void DrawOpaque(RenderContext rc)
    {
        base.DrawOpaque(rc);
        // G1 frame: the same spheres through G1's InstancedLit (vertex colour × tint, Lambert)
        var fx = rc.IsG2Frame ? EffectNames.InstancedPbr : EffectNames.InstancedLit;
        _sphere!.DrawInstanced(rc, rc.Effects.Get(fx), _inst!);
    }

    public override void Dispose() { base.Dispose(); _sphere?.Dispose(); _inst?.Dispose(); }
}

/// <summary>
/// Material close-ups found on the generated map (read only): the steepest rock face, a meadow at a grazing angle,
/// and the gravel track. For the per-feature A/B of hex tiling, anisotropic filtering and macro variation.
/// </summary>
[DemoScene("render1-closeups", "render1: rock cliff, grazing meadow and gravel track close-ups (fixture map)")]
public sealed class Render1CloseupsScene : Render1SceneBase
{
    CaptureShot[] _shots = Array.Empty<CaptureShot>();

    protected override void OnLoad()
    {
        base.OnLoad();
        var m = Scenery.Mesh;
        Vector3 Pos(int ix, int iy) => m.SamplePosition(ix, iy);
        // 1. steepest rock (mountain or hills) within 380 m of the centre
        float best = -1; int bx = 128, by = 128;
        for (int y = 30; y < Grid.Height - 30; y++)
            for (int x = 30; x < Grid.Width - 30; x++)
            {
                var p = Pos(x, y);
                if (p.X * p.X + p.Z * p.Z > 380f * 380f) continue;
                var c = Grid.Class[Grid.Index(x, y)];
                if (c != TerrainClass.Mountain && c != TerrainClass.Hills) continue;
                float slope = 1 - m.NormalAt(p.X, p.Z).Y;
                if (slope > best) { best = slope; bx = x; by = y; }
            }
        var cliff = Pos(bx, by);
        var cn = m.NormalAt(cliff.X, cliff.Z);
        var out2 = Vector3.Normalize(new Vector3(cn.X, 0, cn.Z) + new Vector3(1e-4f, 0, 0));
        var cliffCam = Ground(cliff.X + out2.X * 16, cliff.Z + out2.Z * 16) + new Vector3(0, 4.5f, 0);
        // 2. meadow: the flattest open spot near the centre
        var meadow = BattleSite.FindStage(Scenery, Vector3.Zero, 220, 9);
        // 3. gravel track: the road cell nearest the centre
        Vector3 road = meadow; float rd = float.MaxValue; int rx = 0, ry = 0;
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
                if (Grid.Class[Grid.Index(x, y)] == TerrainClass.Road)
                {
                    var p = Pos(x, y); float d = p.X * p.X + p.Z * p.Z;
                    if (d < rd) { rd = d; road = p; rx = x; ry = y; }
                }
        // direction along the road: towards the next road cell a few cells away
        var along = Vector3.UnitX;
        for (int k = 6; k >= 2; k--)
        {
            foreach (var (dx, dy) in new[] { (k, 0), (-k, 0), (0, k), (0, -k), (k, k), (-k, -k), (k, -k), (-k, k) })
            {
                int x = rx + dx, y = ry + dy;
                if (x < 0 || y < 0 || x >= Grid.Width || y >= Grid.Height || Grid.Class[Grid.Index(x, y)] != TerrainClass.Road) continue;
                along = Vector3.Normalize(Pos(x, y) - road); along.Y = 0; along = Vector3.Normalize(along); k = 0; break;
            }
        }
        _shots = new[]
        {
            new CaptureShot("cliff", new CameraPose(cliffCam, cliff + new Vector3(0, 2, 0), 0.75f, 0.2f, 3000f), 10.5f, 0),
            new CaptureShot("meadow-grazing", new CameraPose(Ground(meadow.X, meadow.Z + 6) + new Vector3(0, 1.6f, 0), Ground(meadow.X + 3, meadow.Z - 30), 0.8f, 0.2f, 3000f), 15.5f, 0),
            new CaptureShot("gravel-track", new CameraPose(road - along * 9 + new Vector3(0, 2.2f, 0), road + along * 12, 0.8f, 0.2f, 3000f), 10.5f, 0),
        };
    }

    public override IReadOnlyList<CaptureShot> Shots => _shots;
}

/// <summary>
/// The arid coast fixture (lane T inputs, read only): a beach looking out over the map's sea and the far sea, and a
/// high view along the coast. For the distance-filtered water (no moiré), sky reflections and the far field.
/// </summary>
[DemoScene("render1-coast", "render1: beach and coast views over the in-map and far sea (arid fixture)")]
public sealed class Render1CoastScene : TerrainSceneBase
{
    public override BattleTerrainInputs Inputs => TerrainAridScene.FixtureInputs;
    CaptureShot[] _shots = Array.Empty<CaptureShot>();

    protected override void OnLoad()
    {
        base.OnLoad();
        var m = Scenery.Mesh;
        // the beach cell nearest the map's centre line (x = 0) that borders water
        Vector3 beach = Vector3.Zero, sea = new(0, 0, 400); float best = float.MaxValue;
        for (int y = 1; y < Grid.Height - 1; y++)
            for (int x = 1; x < Grid.Width - 1; x++)
            {
                if (Grid.Class[Grid.Index(x, y)] != TerrainClass.Beach || Grid.WaterDepthMm[Grid.Index(x, y + 1)] <= 0) continue;
                var p = m.SamplePosition(x, y); float d = MathF.Abs(p.X);
                if (d < best) { best = d; beach = p; sea = m.SamplePosition(x, Math.Min(Grid.Height - 1, y + 40)); }
            }
        var dir = Vector3.Normalize(new Vector3(sea.X - beach.X, 0, sea.Z - beach.Z));
        _shots = new[]
        {
            new CaptureShot("beach", new CameraPose(beach - dir * 6 + new Vector3(0, 2.2f, 0), new Vector3(sea.X, 0.5f, sea.Z), 0.8f, 0.2f, 40000f), 16.5f, 0),
            new CaptureShot("coast-high", new CameraPose(beach - dir * 120 + new Vector3(-90, 75, 0), beach + dir * 220 + new Vector3(260, 0, 0), 0.85f, 0.5f, 40000f), 16.5f, 0),
        };
    }

    public override IReadOnlyList<CaptureShot> Shots => _shots;
}
