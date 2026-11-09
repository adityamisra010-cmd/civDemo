using Gfx.Engine.Cameras;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Presentation;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Scenes;

/// <summary>Base for lane T demo scenes: generate the authoritative grid from labelled fixture inputs, render it.</summary>
public abstract class TerrainSceneBase : DemoScene
{
    public abstract BattleTerrainInputs Inputs { get; }
    public BattleTerrainGrid Grid { get; private set; } = null!;
    public TerrainScenery Scenery { get; private set; } = null!;

    protected override void OnLoad()
    {
        Grid = BattleTerrainGenerator.Generate(Inputs);
        Scenery = new TerrainScenery(Device, Grid);
    }

    public override void Configure(RenderSettings settings)
    {
        base.Configure(settings);
        settings.FogParams = Scenery.Weather.FogParams;
    }

    public override float ShadowRadius => 260f;
    public override void Update(float t) { if (Scenery != null) Scenery.TimeSeconds = t; }
    public override void DrawShadowCasters(RenderContext rc) => Scenery.DrawShadowCasters(rc);
    public override void DrawOpaque(RenderContext rc) => Scenery.DrawOpaque(rc);

    /// <summary>Camera at (x, ground + up, z) looking at (tx, ground + tUp, tz).</summary>
    protected CameraPose Pose(float x, float z, float up, float tx, float tz, float tUp = 0, float fov = 0.8f)
    {
        float g(float a, float b) => Scenery?.HeightAt(a, b) ?? 0f;
        return new CameraPose(new Vector3(x, g(x, z) + up, z), new Vector3(tx, g(tx, tz) + tUp, tz), fov, 0.5f, 3000f);
    }

    public override void Dispose() => Scenery?.Dispose();
}

/// <summary>Temperate hill country with a river and a road (LABELLED FIXTURE inputs, not a campaign cell).</summary>
[DemoScene("terrain-temperate", "Lane T: temperate hills, river, road, forests (fixture inputs)")]
public sealed class TerrainTemperateScene : TerrainSceneBase
{
    public static readonly BattleTerrainInputs FixtureInputs = new(WorldSeed: 42, CampaignCell: 1234, ElevationM: 180,
        MoveCostPermille: 2200, TemperatureDeciC: 120, MoisturePermille: 650, CoastEdge: -1, River: true);
    public override BattleTerrainInputs Inputs => FixtureInputs;
    public override Vector3 Focus => Vector3.Zero;
    public override IReadOnlyList<CaptureShot> Shots => new[]
    {
        new CaptureShot("day", Pose(-320, 460, 200, 200, -60), 10.5f, 0f),
        new CaptureShot("dusk", Pose(-420, -320, 150, 80, 60), 18.6f, 0f),
        new CaptureShot("tactical-low", Pose(-160, 30, 9, 60, -10, 2, 0.9f), 9.0f, 0f),
    };
}

/// <summary>Arid coast (south) with a shallow (wet) river into the sea (LABELLED FIXTURE inputs).</summary>
[DemoScene("terrain-arid", "Lane T: arid coast, beach, shallow river, desert scrub (fixture inputs)")]
public sealed class TerrainAridScene : TerrainSceneBase
{
    public static readonly BattleTerrainInputs FixtureInputs = new(WorldSeed: 42, CampaignCell: 4321, ElevationM: 40,
        MoveCostPermille: 2600, TemperatureDeciC: 260, MoisturePermille: 180, CoastEdge: 2, River: true);
    public override BattleTerrainInputs Inputs => FixtureInputs;
    public override IReadOnlyList<CaptureShot> Shots => new[]
    {
        new CaptureShot("day", Pose(-420, -440, 200, 0, 140), 11f, 0f),
        new CaptureShot("dusk", Pose(-460, 60, 120, 150, 260), 18.6f, 0f),
        new CaptureShot("tactical-low", Pose(60, 150, 8, -80, 380, 0, 0.9f), 16f, 0f),
    };
}
