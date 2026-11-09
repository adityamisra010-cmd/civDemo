using Gfx.Cinematics;
using Gfx.Cinematics.Demo;
using Gfx.Engine.Cameras;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Presentation;
using Gfx.Proc.Scenes;
using Gfx.Units;
using Microsoft.Xna.Framework;

namespace Gfx.Demo.Scenes;

/// <summary>
/// Lane I placement (PRESENTATION ONLY): where the fixture battle and the cinematic stage sit on the generated
/// battlefield. The battle fixture has no terrain (Proto.M7 snapshot fights on a flat plane), so the integration
/// searches the authoritative grid READ-ONLY for the flattest dry, open strip and lays the fixture's local frame
/// on it (rotation + translation; soldiers snap to the terrain height). This is a display mapping, not a sim rule:
/// in the real pipeline the battle sim would run ON the grid and positions would already be terrain positions.
/// </summary>
public static class BattleSite
{
    public static readonly Vector2 FixtureCentreMm = new(480_000, 330_000);

    static float Penalty(TerrainMeshData m, Vector3 p)
    {
        if (MathF.Abs(p.X) > m.HalfExtentX - 24 || MathF.Abs(p.Z) > m.HalfExtentZ - 24) return 1e6f;
        if (m.IsWet(p.X, p.Z)) return 1000;
        return m.ClassAt(p.X, p.Z) switch
        {
            TerrainClass.Water or TerrainClass.Beach => 1000,
            TerrainClass.Mountain => 200,
            TerrainClass.Forest or TerrainClass.Jungle => 30,
            TerrainClass.Hills => 4,
            _ => 0,
        } + (1 - m.NormalAt(p.X, p.Z).Y) * 300;
    }

    /// <summary>Best (centre, yaw) for a strip of half-length <paramref name="halfLen"/> m and half-width <paramref name="halfWid"/> m.</summary>
    public static (Vector3 centre, float yaw) FindStrip(TerrainMeshData m, float halfLen, float halfWid)
    {
        float best = float.MaxValue; Vector3 bc = Vector3.Zero; float by = 0;
        for (int a = 0; a < 12; a++)
        {
            float yaw = a * MathF.PI / 12; var rot = Matrix.CreateRotationY(yaw);
            for (float cx = -300; cx <= 300; cx += 25)
                for (float cz = -300; cz <= 300; cz += 25)
                {
                    float s = 0, lo = float.MaxValue, hi = float.MinValue;
                    for (float x = -halfLen; x <= halfLen && s < best; x += 10)
                        for (float z = -halfWid; z <= halfWid; z += halfWid)
                        {
                            var p = Vector3.Transform(new Vector3(x, 0, z), rot) + new Vector3(cx, 0, cz);
                            s += Penalty(m, p); float h = m.HeightAt(p.X, p.Z); lo = MathF.Min(lo, h); hi = MathF.Max(hi, h);
                        }
                    s += (hi - lo) * 2;
                    if (s < best) { best = s; bc = new Vector3(cx, 0, cz); by = yaw; }
                }
        }
        bc.Y = m.HeightAt(bc.X, bc.Z);
        return (bc, by);
    }

    /// <summary>Flattest dry, open, prop-free disc of radius <paramref name="r"/> m within <paramref name="range"/> m of <paramref name="near"/>.</summary>
    public static Vector3 FindStage(TerrainScenery s, Vector3 near, float range, float r)
    {
        var m = s.Mesh; float best = float.MaxValue; var bc = near;
        var props = s.PropGroups.SelectMany(g => g.Spheres).ToArray();
        for (float dx = -range; dx <= range; dx += 6)
            for (float dz = -range; dz <= range; dz += 6)
            {
                var c = near + new Vector3(dx, 0, dz);
                float sc = 0, lo = float.MaxValue, hi = float.MinValue;
                for (float x = -r; x <= r; x += 3)
                    for (float z = -r; z <= r; z += 3)
                    {
                        if (x * x + z * z > r * r) continue;
                        var p = c + new Vector3(x, 0, z); sc += Penalty(m, p);
                        float h = m.HeightAt(p.X, p.Z); lo = MathF.Min(lo, h); hi = MathF.Max(hi, h);
                    }
                foreach (var q in props)
                {
                    float d2 = (q.X - c.X) * (q.X - c.X) + (q.Z - c.Z) * (q.Z - c.Z), rr = r + q.W;
                    if (d2 < rr * rr) sc += 50;
                }
                sc += (hi - lo) * 40;
                if (sc < best) { best = sc; bc = c; }
            }
        bc.Y = m.HeightAt(bc.X, bc.Z);
        return bc;
    }
}

/// <summary>
/// Lane I combined scene: procedural battlefield (lane T, temperate fixture inputs) + the two fixture formations
/// (lane U battle trace, interpolated 2 s steps) + shadows, height fog, sun model, tactical-camera shots.
/// Reads the grid and the trace; never writes them (renderer-invariance test hashes both).
/// </summary>
[DemoScene("battlefield", "Lane I: procedural battlefield + fixture formations + shadows/fog/sun, tactical camera")]
public sealed class BattlefieldScene : DemoScene
{
    public BattleTerrainGrid Grid { get; private set; } = null!;
    public TerrainScenery Scenery { get; private set; } = null!;
    public BattleTrace Trace { get; private set; } = null!;
    public FormationPlayback Playback { get; private set; } = null!;
    public SoldierCrowd Crowd { get; private set; } = null!;
    public Vector3 SiteCentre { get; private set; }
    public float SiteYaw { get; private set; }
    SoldierFrame[] _frames = Array.Empty<SoldierFrame>();
    /// <summary>This frame's soldiers in world space (after <see cref="Update"/>).</summary>
    public ReadOnlySpan<SoldierFrame> Frames => _frames;
    Matrix _siteRot = Matrix.Identity;
    CaptureShot[] _shots = Array.Empty<CaptureShot>();
    int _version, _prepared = -1; Vector3 _preparedCam;

    protected override void OnLoad()
    {
        Grid = BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs);
        Scenery = new TerrainScenery(Device, Grid);
        Trace = BattleTrace.Load(BattleTrace.FindFixture(BattleTrace.DefaultFixture));
        Playback = new FormationPlayback(Trace, BattleSite.FixtureCentreMm);
        _frames = new SoldierFrame[Playback.EntityCount];
        (SiteCentre, SiteYaw) = BattleSite.FindStrip(Scenery.Mesh, 260, 16);
        _siteRot = Matrix.CreateRotationY(SiteYaw);
        Crowd = new SoldierCrowd(Device);
        BuildShots();
        Update(0);
    }

    public override void Configure(RenderSettings settings)
    {
        base.Configure(settings);
        settings.FogParams = Scenery.Weather.FogParams;
    }

    public override float ShadowRadius => 200f;
    public override Vector3 Focus => SiteCentre;

    public override void Update(float t)
    {
        Playback.Evaluate(t, _frames);
        for (int i = 0; i < _frames.Length; i++)
        {
            ref var f = ref _frames[i];
            var p = Vector3.Transform(f.Position, _siteRot) + new Vector3(SiteCentre.X, 0, SiteCentre.Z);
            p.Y = Scenery.HeightAt(p.X, p.Z);
            f.Position = p; f.Yaw += SiteYaw;
        }
        Scenery.TimeSeconds = t;
        _version++;
    }

    Vector3 Centroid(float t, Func<int, bool> pick)
    {
        Update(t); var c = Vector3.Zero; int n = 0;
        for (int e = 0; e < _frames.Length; e++)
            if (_frames[e].Visible && _frames[e].Clip != ClipId.Death && pick(e)) { c += _frames[e].Position; n++; }
        return n > 0 ? c / n : SiteCentre;
    }

    static CameraPose Tac(Vector3 focus, float yaw, float distance)
    {
        var cam = new TacticalCamera(focus, yaw, distance) { Smoothing = 0 };
        cam.Update(0);
        return cam.Pose;
    }

    void BuildShots()
    {
        float t0 = Trace.StepTimeMs(0) / 1000f, firstRanged = float.MaxValue, firstMelee = float.MaxValue;
        foreach (var e in Trace.Events)
            if (e.Kind == TraceEventKind.Fire)
            {
                float s = e.TimeMs / 1000f - t0;
                if (e.A == 2) firstRanged = MathF.Min(firstRanged, s); else firstMelee = MathF.Min(firstMelee, s);
            }
        float tv = firstRanged == float.MaxValue ? 10 : firstRanged + 0.5f;
        float tm = firstMelee == float.MaxValue ? Playback.DurationSeconds / 2 : firstMelee + 2.7f;
        float te = Playback.DurationSeconds;
        float tf = MathF.Min(tv, 14f);
        // local +x (side 0's advance) in world; camera yaw places the camera along +(sin yaw, cos yaw)
        var adv = Vector3.Transform(Vector3.UnitX, _siteRot);
        float behind0 = MathF.Atan2(-adv.X, -adv.Z), flank = behind0 + MathF.PI / 2;
        long inf0 = Trace.Formations.First(f => f.Side == 0 && !f.Ranged).Id;
        var all = Centroid(tv, _ => true);
        var melee = Centroid(tm, e => _frames[e].Clip == ClipId.AttackMelee);
        var follow = Centroid(tf, e => Trace.Entities[e].Formation == inf0);
        var end = Centroid(te, _ => true);
        // dusk: camera on the far side from the low sun, looking into it (back-lit aftermath)
        var toSun = SunModel.At(DuskHour).DirectionToLight;
        float duskYaw = MathF.Atan2(-toSun.X, -toSun.Z) + 0.5f;
        _shots = new[]
        {
            new CaptureShot("wide", Tac(all, flank - 0.45f, 230), 10.5f, tv),
            new CaptureShot("mid", Tac(melee, flank + 0.35f, 70), 14f, tm),
            new CaptureShot("follow-low", Tac(follow, behind0 + 0.55f, 13), 9.5f, tf),
            new CaptureShot("dusk", Tac(end, duskYaw, 40), DuskHour, te),
        };
        Update(0);
    }

    const float DuskHour = 17.7f;
    public override IReadOnlyList<CaptureShot> Shots => _shots;

    void Prepare(RenderContext rc)
    {
        if (_prepared == _version && _preparedCam == rc.Camera.Position) return;
        Crowd.Prepare(_frames, rc.Camera.Position, rc.Frustum);
        _prepared = _version; _preparedCam = rc.Camera.Position;
    }

    public override void DrawShadowCasters(RenderContext rc) { Scenery.DrawShadowCasters(rc); Prepare(rc); Crowd.Draw(rc); }
    public override void DrawOpaque(RenderContext rc) { Prepare(rc); Scenery.DrawOpaque(rc); Crowd.Draw(rc); }
    public override void Dispose() { Scenery?.Dispose(); Crowd?.Dispose(); }
}

/// <summary>
/// Lane I: the lane A surrender cinematic staged on the generated battlefield (the flat set ground is replaced by
/// the battle terrain; the stage is the flattest open, prop-free spot near the battle site). Fixture outcome.
/// </summary>
[DemoScene("battlefield-cine", "Lane I: surrender cinematic (lane A) staged on the procedural battlefield")]
public sealed class BattlefieldCinematicScene() : CinematicSceneBase(WarOutcomeFixtures.Surrender, 1)
{
    public BattleTerrainGrid Grid { get; private set; } = null!;
    public TerrainScenery Scenery { get; private set; } = null!;
    Vector3 _origin;

    protected override Vector3 Origin => _origin;
    protected override bool SetGround => false;

    protected override void OnLoad()
    {
        Grid = BattleTerrainGenerator.Generate(TerrainTemperateScene.FixtureInputs);
        Scenery = new TerrainScenery(Device, Grid);
        var (site, _) = BattleSite.FindStrip(Scenery.Mesh, 260, 16);
        _origin = BattleSite.FindStage(Scenery, site, 90, 14);
        base.OnLoad();
    }

    public override void Update(float t) { if (Scenery != null) Scenery.TimeSeconds = t; base.Update(t); }
    protected override void DrawBackground(RenderContext rc)
    {
        if (rc.IsShadowPass) Scenery.DrawShadowCasters(rc); else Scenery.DrawOpaque(rc);
    }
    public override void Dispose() { base.Dispose(); Scenery?.Dispose(); }
}
