using System.Globalization;
using Gfx.Chars.Crowd;
using Gfx.Chars.Rendering;
using Gfx.Engine.Cameras;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Proc.Authoritative;
using Gfx.Proc.Environment;
using Gfx.Proc.Environment.Structures;
using Gfx.Proc.Environment.Trees;
using Gfx.Proc.Presentation;
using Gfx.Proc.Scenes;
using Microsoft.Xna.Framework;

namespace Gfx.Demo.Scenes;

/// <summary>What a formation does in a scripted crowd scene (presentation only; no simulation drives these scenes).</summary>
public enum FormationAct { Idle, Engage, March }

/// <summary>A block of soldiers: centre on the ground plane (x, z), facing yaw (forward = (sin yaw, cos yaw)), files × ranks.</summary>
public sealed record FormationSpec(Vector2 Centre, float Yaw, int Files, int Ranks, int Force, FormationAct Act, uint Seed,
    float FileSpacing = 1.0f, float RankSpacing = 1.15f, bool Swords = false, float MarchStop = 0f, int Fallen = 0)
{
    public Vector2 Forward => new(MathF.Sin(Yaw), MathF.Cos(Yaw));
    public Vector2 Side => new(MathF.Cos(Yaw), -MathF.Sin(Yaw));
    public int Count => Files * Ranks + Fallen;
}

/// <summary>
/// A scripted crowd for the demo scenes (stage render2; PRESENTATION ONLY): formations of stage-chars soldiers drawn by
/// <see cref="CrowdRenderer"/>. Everything here is a deterministic function of scene time and the formation seeds:
/// idle soldiers blend two idle clips by a per-soldier weight, the front ranks of engaged formations cross-fade between
/// a thrust and a block, marching formations walk at the clip's speed and then blend into the idle, fallen soldiers lie
/// at the last frame of a death clip. Two forces of four identities each (stage chars); per-soldier height, width, dye,
/// skin tone, mud and tarnish within bounds (<see cref="CrowdLook"/>). The CMU clips are PROTOTYPE-ONLY.
/// </summary>
public sealed class CrowdCast : IDisposable
{
    public static readonly (string identity, string outfit)[] Variants =
    {
        ("a1", "kit_a"), ("a2", "kit_a"), ("a3", "kit_a"), ("a4", "kit_a"),
        ("b1", "kit_b"), ("b2", "kit_b"), ("b3", "kit_b"), ("b4", "kit_b"),
    };
    // spear kits per force (gambeson, mail, levy), sword kit (surcoat) mixed in for sword blocks
    static readonly int[][] Spears = { new[] { 0, 1, 3, 0, 3 }, new[] { 4, 5, 7, 4, 7 } };
    static readonly int[][] Swords = { new[] { 2, 2, 1 }, new[] { 6, 6, 5 } };

    readonly CharStage _loader;
    public CrowdRenderer Crowd { get; }
    public List<FormationSpec> Formations { get; } = new();
    readonly Func<float, float, float> _height;
    CrowdAgent[] _agents = Array.Empty<CrowdAgent>();
    readonly int _idle, _idle2, _walk, _thrust, _block, _death, _death2;

    public CrowdCast(Microsoft.Xna.Framework.Graphics.GraphicsDevice gd, Func<float, float, float> height)
    {
        _height = height;
        _loader = new CharStage(gd);
        Crowd = new CrowdRenderer(gd, _loader, Variants);
        _idle = CrowdRenderer.Clip("idle"); _idle2 = CrowdRenderer.Clip("idle_proud"); _walk = CrowdRenderer.Clip("walk");
        _thrust = CrowdRenderer.Clip("thrust"); _block = CrowdRenderer.Clip("block");
        _death = CrowdRenderer.Clip("death_fwd"); _death2 = CrowdRenderer.Clip("death_back");
    }

    public int Soldiers => Formations.Sum(f => f.Count);

    static float U(uint h) => (h >> 8) * (1f / 16777216f);
    static float Smooth(float a, float b, float x) { float t = Math.Clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }

    /// <summary>Recomputes every agent for scene time <paramref name="t"/> and hands them to the renderer.</summary>
    public void Update(float t)
    {
        int n = Soldiers;
        if (_agents.Length != n) _agents = new CrowdAgent[n];
        int k = 0;
        foreach (var f in Formations)
        {
            var fwd = f.Forward; var side = f.Side;
            var kits = f.Swords ? Swords[f.Force] : Spears[f.Force];
            float march = f.Act == FormationAct.March ? MathF.Min(t, f.MarchStop) : 0f;
            for (int r = 0; r < f.Ranks; r++)
                for (int c = 0; c < f.Files; c++)
                {
                    uint key = CrowdLook.Hash(f.Seed * 7919u + (uint)(r * 977 + c));
                    var look = CrowdLook.FromKey(key);
                    float jx = (U(CrowdLook.Hash(key ^ 0xA5A5A5A5u)) - 0.5f) * 0.24f, jz = (U(CrowdLook.Hash(key ^ 0x5A5A5A5Au)) - 0.5f) * 0.24f;
                    var p2 = f.Centre + side * ((c - (f.Files - 1) * 0.5f) * f.FileSpacing + jx) - fwd * (r * f.RankSpacing + jz);
                    ref var a = ref _agents[k++];
                    a = default;
                    a.Variant = kits[(int)(CrowdLook.Hash(key ^ 0x1234567u) % (uint)kits.Length)];
                    a.Look = look;
                    a.Yaw = f.Yaw + (U(CrowdLook.Hash(key ^ 0x7F4A7C15u)) - 0.5f) * 0.22f;
                    float off = U(CrowdLook.Hash(key ^ 0x2545F491u));
                    switch (f.Act)
                    {
                        case FormationAct.Engage when r < 2:
                        {
                            // thrust, cross-fade into a block and back (period 3.2 s, per-soldier offset)
                            const float P = 3.2f;
                            float tau = ((t + off * P) % P + P) % P;
                            a.ClipA = _thrust; a.TimeA = tau;
                            a.ClipB = _block; a.TimeB = MathF.Max(0, tau - 1.5f);
                            a.Blend = Smooth(1.35f, 1.85f, tau) * (1 - Smooth(2.75f, 3.2f, tau));
                            break;
                        }
                        case FormationAct.March:
                        {
                            float stopBlend = Smooth(f.MarchStop, f.MarchStop + 0.6f, t);
                            float speed = Crowd.Bank.Clip(Crowd.Variants[a.Variant].BankIdentity, _walk).Speed * look.Height;
                            p2 += fwd * (speed * march);
                            a.ClipA = _walk; a.TimeA = MathF.Min(t, f.MarchStop + 0.6f) * look.Height + off;
                            a.ClipB = _idle; a.TimeB = t + off * 7f;
                            a.Blend = stopBlend;
                            break;
                        }
                        default:
                            a.ClipA = _idle; a.TimeA = t + off * 7.8f;
                            a.ClipB = _idle2; a.TimeB = t + off * 3.1f + 1.7f;
                            a.Blend = U(CrowdLook.Hash(key ^ 0x6C8E9CF5u));
                            break;
                    }
                    a.Position = new Vector3(p2.X, _height(p2.X, p2.Y), p2.Y);
                }
            // the fallen lie in front of the formation at the last frame of a death clip
            for (int i = 0; i < f.Fallen; i++)
            {
                uint key = CrowdLook.Hash(f.Seed * 104729u + (uint)i + 50000u);
                var p2 = f.Centre + fwd * (1.4f + 1.6f * U(key)) + side * ((U(CrowdLook.Hash(key ^ 3u)) - 0.5f) * f.Files * f.FileSpacing);
                ref var a = ref _agents[k++];
                a = default;
                a.Variant = (f.Force == 0 ? 4 : 0) + (int)(CrowdLook.Hash(key ^ 9u) % 4u); // the other force's dead
                a.Look = CrowdLook.FromKey(key);
                a.Yaw = f.Yaw + MathF.PI + (U(CrowdLook.Hash(key ^ 5u)) - 0.5f) * 1.2f;
                bool back = (key & 1u) != 0;
                a.ClipA = back ? _death2 : _death; a.TimeA = 1000f;
                a.ClipB = a.ClipA; a.TimeB = 1000f; a.Blend = 0;
                a.Position = new Vector3(p2.X, _height(p2.X, p2.Y), p2.Y);
            }
        }
        Crowd.SetAgents(_agents.AsSpan(0, k));
    }

    /// <summary>Applies the crowd switches of the frame (<see cref="RenderFeatures"/>: crowdlod, crowdlevel, crowdfade,
    /// crowdlooks, crowdblend, frustum); the scenes call it before the shadow pass and before the opaque pass.</summary>
    public void ApplySwitches(RenderFeatures f)
    {
        if (Crowd.Lod.Bias != f.CrowdLodBias) Crowd.Lod = Crowd.Lod with { Bias = f.CrowdLodBias };
        Crowd.ForceLevel = f.CrowdLevel; Crowd.LodFade = f.CrowdLodFade; Crowd.FrustumCulling = f.FrustumCulling;
        Crowd.Looks = f.CrowdLooks; Crowd.ClipBlend = f.CrowdClipBlend;
    }

    public string Report(OcclusionBuffer? occ, bool occOn)
    {
        var s = Crowd.Stats;
        return string.Format(CultureInfo.InvariantCulture,
            "[crowd] agents={0} drawn={1} lod1={2} lod2={3} lod3={4} infade={5} culled-frustum={6} culled-occlusion={7} draws={8} tris={9} shadow-instances={10} shadow-draws={11} shadow-tris={12} shadow-culled-cascade={15} lod3-bake={13} cpu: set-agents={16:F2} ms cull+level={17:F2} ms shadow-select={18:F2} ms{14}",
            s.Agents, s.Soldiers, s.PerLevel[0], s.PerLevel[1], s.PerLevel[2], s.InFade, s.CulledFrustum, s.CulledOcclusion, s.Draws, s.Triangles,
            s.ShadowInstances, s.ShadowDraws, s.ShadowTriangles, Crowd.HasLod3 ? "yes" : "NO (lod2 used)",
            occ == null ? "" : !occOn ? " occlusion=off" : string.Format(CultureInfo.InvariantCulture, " occlusion=on occluder-tris={0} occlusion-build={1:F2} ms tests={2} hidden={3}",
                occ.TrianglesRasterized, occ.BuildMs, occ.Tests, occ.Occluded), s.ShadowCulledCascade,
            s.SetAgentsMs, s.CullMs, s.ShadowSelectMs);
    }

    public void Dispose() { Crowd.Dispose(); _loader.Dispose(); }
}

/// <summary>
/// Conservative occluders of a battle map (stage render2): the terrain as a coarse grid whose every vertex lies at or
/// below the lowest true height within ±<see cref="Reach"/> m (so each coarse triangle stays under the real surface,
/// including the renderer's coarser terrain LODs), and optional thin boxes inside walls (a palisade ring).
/// </summary>
public sealed class BattleOccluders
{
    public const float Cell = 8f, Reach = 16f;
    readonly Vector3[] _v;
    readonly int[] _tiles;           // per tile: start index into _idx, count
    readonly int[] _idx;
    readonly BoundingBox[] _tileBox;
    readonly List<Matrix> _boxes = new();
    public int TerrainTriangles => _idx.Length / 3;
    public int Boxes => _boxes.Count;

    public BattleOccluders(TerrainMeshData m)
    {
        int nx = (int)(2 * m.HalfExtentX / Cell) + 1, nz = (int)(2 * m.HalfExtentZ / Cell) + 1;
        _v = new Vector3[nx * nz];
        float step = m.CellM;
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = -m.HalfExtentX + i * Cell, z = -m.HalfExtentZ + j * Cell;
                float lo = float.MaxValue;
                for (float dz = -Reach; dz <= Reach + 1e-3f; dz += step)
                    for (float dx = -Reach; dx <= Reach + 1e-3f; dx += step)
                        lo = MathF.Min(lo, m.HeightAt(x + dx, z + dz));
                _v[j * nx + i] = new Vector3(x, lo - 0.05f, z);
            }
        // tiles of 8 x 8 cells for a cheap frustum pre-test
        const int T = 8;
        int tx = (nx - 1 + T - 1) / T, tz = (nz - 1 + T - 1) / T;
        var idx = new List<int>(); var tiles = new List<int>(); var boxes = new List<BoundingBox>();
        for (int b = 0; b < tz; b++)
            for (int a = 0; a < tx; a++)
            {
                int start = idx.Count;
                var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
                for (int j = b * T; j < Math.Min((b + 1) * T, nz - 1); j++)
                    for (int i = a * T; i < Math.Min((a + 1) * T, nx - 1); i++)
                    {
                        int v0 = j * nx + i, v1 = v0 + 1, v2 = v0 + nx, v3 = v2 + 1;
                        idx.Add(v0); idx.Add(v2); idx.Add(v1); idx.Add(v1); idx.Add(v2); idx.Add(v3);
                        foreach (int q in new[] { v0, v1, v2, v3 }) { min = Vector3.Min(min, _v[q]); max = Vector3.Max(max, _v[q]); }
                    }
                tiles.Add(start); tiles.Add(idx.Count - start); boxes.Add(new BoundingBox(min, max));
            }
        _idx = idx.ToArray(); _tiles = tiles.ToArray(); _tileBox = boxes.ToArray();
    }

    /// <summary>Height of the terrain occluder at world (x, z): the grid's triangle under the point (for tests and debug).</summary>
    public float OccluderHeightAt(float x, float z, float halfExtentX, float halfExtentZ)
    {
        int nx = (int)(2 * halfExtentX / Cell) + 1;
        float fx = (x + halfExtentX) / Cell, fz = (z + halfExtentZ) / Cell;
        int i = Math.Clamp((int)MathF.Floor(fx), 0, nx - 2), j = Math.Clamp((int)MathF.Floor(fz), 0, _v.Length / nx - 2);
        float tx = fx - i, tz = fz - j;
        float h0 = _v[j * nx + i].Y, h1 = _v[j * nx + i + 1].Y, h2 = _v[(j + 1) * nx + i].Y, h3 = _v[(j + 1) * nx + i + 1].Y;
        // the cell is split along v1-v2 into (v0, v2, v1) and (v1, v2, v3)
        return tx + tz <= 1f ? h0 + (h1 - h0) * tx + (h2 - h0) * tz : h3 + (h2 - h3) * (1 - tx) + (h1 - h3) * (1 - tz);
    }

    /// <summary>
    /// Adds a wall ring (closed polyline on the ground) as thin boxes inside the wall, 10 cm thick, standing on the lowest
    /// ground along each segment and 85 % of the wall's nominal height tall: below the real wall top everywhere along the
    /// segment (the palisade's logs are at least the nominal height − 0.3 m above the ground they stand on), so the box
    /// never covers a pixel the wall leaves open above it. Where the ground rises along the segment, the box's lower part
    /// lies inside the ground, which hides what is behind it anyway. Segments next to the first post (the gate) are left
    /// open. The slits of a few centimetres between logs are treated as closed (see reports/render2.md).
    /// </summary>
    public void AddWallRing(IReadOnlyList<Vector2> ring, float height, Func<float, float, float> ground, int skipFirst = 1)
        => _boxes.AddRange(WallBoxes(ring, height, ground, skipFirst));

    /// <summary>The occluder boxes <see cref="AddWallRing"/> adds (unit box [−1, 1]³ → world), for tests and tools.</summary>
    public static List<Matrix> WallBoxes(IReadOnlyList<Vector2> ring, float height, Func<float, float, float> ground, int skipFirst = 1)
    {
        var boxes = new List<Matrix>();
        for (int i = skipFirst; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            if (i == ring.Count - 1 && skipFirst > 0) continue; // the gate gap between the last and the first post
            var mid = (a + b) * 0.5f; var d = b - a; float len = d.Length();
            if (len < 0.5f) continue;
            float g = float.MaxValue;
            int steps = (int)MathF.Ceiling(len / 0.25f);
            for (int k = 0; k <= steps; k++) { var p = a + d * (k / (float)steps); g = MathF.Min(g, ground(p.X, p.Y)); }
            float h = height * 0.85f;
            float yaw = MathF.Atan2(d.X, d.Y);
            boxes.Add(Matrix.CreateScale(0.05f, h * 0.5f, len * 0.5f * 0.95f) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(mid.X, g + h * 0.5f, mid.Y));
        }
        return boxes;
    }

    /// <summary>Rasterises the occluders in the camera's frustum into <paramref name="ob"/> (Begin … End).</summary>
    public void Build(OcclusionBuffer ob, RenderContext rc)
    {
        ob.Begin(rc.View, rc.Projection, rc.Camera.Near);
        for (int t = 0; t < _tileBox.Length; t++)
        {
            if (!rc.Frustum.IntersectsBox(_tileBox[t].Min, _tileBox[t].Max)) continue;
            ob.AddTriangles(_v, _idx.AsSpan(_tiles[2 * t], _tiles[2 * t + 1]));
        }
        foreach (var b in _boxes) ob.AddBox(b);
        ob.End();
    }
}

/// <summary>
/// Stage render2: two formations of 110 stage-chars soldiers (220) facing each other on the render1 meadow, the front
/// ranks fighting (thrust ⇄ block cross-fades), the rest idle (two idle clips blended per soldier). Instanced, three LODs
/// with dithered cross-fades, GPU skinning from the bank texture. Scripted presentation, not a simulation.
/// </summary>
[DemoScene("crowd-formation", "render2: 220 instanced stage-chars soldiers in two engaged formations (3 LODs, GPU skinning, 2-clip blends)")]
public sealed class CrowdFormationScene : Render1SceneBase, IFrameReport
{
    CrowdCast _cast = null!;
    Vector3 _site; float _yaw;

    protected override void OnLoad()
    {
        base.OnLoad();
        Scenery.DrawProps = false;
        _site = BattleSite.FindStage(Scenery, Vector3.Zero, 220, 26);
        // the line faces about 35 degrees off the morning sun's azimuth (three-quarter light on the faces)
        var sun = SunModel.At(10.5f).DirectionToLight;
        _yaw = MathF.Atan2(sun.X, sun.Z) + MathHelper.ToRadians(35f);
        _cast = new CrowdCast(Device, Scenery.HeightAt);
        var f = new Vector2(MathF.Sin(_yaw), MathF.Cos(_yaw));
        var c = new Vector2(_site.X, _site.Z);
        const int files = 11, ranks = 10;
        // front ranks 1.1 m from the centre line
        _cast.Formations.Add(new FormationSpec(c - f * 1.1f, _yaw, files, ranks, 0, FormationAct.Engage, 11));
        _cast.Formations.Add(new FormationSpec(c + f * 1.1f, _yaw + MathF.PI, files, ranks, 1, FormationAct.Engage, 23));
        Update(0);
    }

    public override void Configure(RenderSettings settings)
    {
        base.Configure(settings);
        settings.Features.ShadowDistance = 400f;
    }

    public override Vector3 Focus => _site;
    public override void Update(float t) { base.Update(t); _cast?.Update(t); }

    Vector3 At(float side, float fwd, float up)
    {
        var f = new Vector3(MathF.Sin(_yaw), 0, MathF.Cos(_yaw)); var s = new Vector3(MathF.Cos(_yaw), 0, -MathF.Sin(_yaw));
        var p = _site + s * side + f * fwd;
        return new Vector3(p.X, Scenery.HeightAt(p.X, p.Z) + up, p.Z);
    }

    public override IReadOnlyList<CaptureShot> Shots => new[]
    {
        new CaptureShot("wide", new CameraPose(At(-26f, -14f, 13f), At(0, -1f, 0.5f), 0.8f, 0.3f, 4000f), 10.5f, 2.1f),
        new CaptureShot("front-low", new CameraPose(At(-9.5f, -3.2f, 1.65f), At(2f, 0.6f, 1.2f), 0.85f, 0.1f, 4000f), 10.5f, 2.1f),
        new CaptureShot("close", new CameraPose(At(-9.2f, -1.4f, 1.75f), At(-4.0f, 0.2f, 1.3f), 0.7f, 0.1f, 4000f), 10.5f, 2.1f),
        new CaptureShot("rear", new CameraPose(At(3f, -17f, 3.2f), At(0f, 6f, 1.2f), 0.8f, 0.1f, 4000f), 10.5f, 2.1f),
        new CaptureShot("high", new CameraPose(At(-6f, -36f, 30f), At(0, 0, 0), 0.75f, 0.3f, 4000f), 10.5f, 2.1f),
        new CaptureShot("dusk", new CameraPose(At(-26f, -14f, 13f), At(0, -1f, 0.5f), 0.8f, 0.3f, 4000f), 17.6f, 2.1f),
    };

    public override void DrawShadowCasters(RenderContext rc) { base.DrawShadowCasters(rc); _cast.ApplySwitches(rc.Features); _cast.Crowd.DrawShadows(rc); }
    public override void DrawOpaque(RenderContext rc)
    {
        base.DrawOpaque(rc);
        _cast.ApplySwitches(rc.Features);
        _cast.Crowd.Occlusion = null;
        _cast.Crowd.DrawOpaque(rc);
    }

    public string FrameReport() => _cast.Report(null, false);
    public override void Dispose() { _cast?.Dispose(); base.Dispose(); }
}

/// <summary>
/// Stage render2 battle scenes on the env stage's temperate battlefield (forest, river, Iron Age village): two armies of
/// stage-chars soldiers in formations of 250 (25 files × 10 ranks) facing each other across the flattest open strip,
/// front lines engaged, the rest idle or marching up. Occlusion culling rasterises the terrain and the village
/// palisade on the CPU (<see cref="OcclusionBuffer"/>). Scripted presentation, not a simulation.
/// </summary>
public abstract class CrowdBattleSceneBase : EnvSceneBase, IFrameReport
{
    public override BattleTerrainInputs Inputs => TerrainTemperateScene.FixtureInputs;
    protected override Func<TerrainMeshData, WaterField, StructureKit?, SettlementPlan>? Settlement =>
        (m, w, kit) => Settlements.IronAgeVillage(m, w, (uint)(Grid.Seed ^ (Grid.Seed >> 32)), kit);

    /// <summary>Number of soldiers (formations of 250).</summary>
    protected abstract int SoldierCount { get; }
    protected CrowdCast Cast = null!;
    BattleOccluders _occ = null!;
    readonly OcclusionBuffer _ob = new(256, 144);
    Vector3 _centre; float _yaw;
    Vector2 _len, _wid;
    bool _occOn;

    protected override (IReadOnlyList<Exclusion>, IReadOnlyList<PlacedTree>?, bool) Plan(TerrainMeshData mesh)
    {
        int formations = SoldierCount / 250;
        float halfLen = MathF.Min(260f, 30f + 14f * (formations / 2));
        // the village is planned (not built) first, the same way the environment will, so the battle can keep clear of it
        var village = Settlements.IronAgeVillage(mesh, new WaterField(mesh), (uint)(Grid.Seed ^ (Grid.Seed >> 32)), null);
        (_centre, _yaw) = FindBattleStrip(mesh, halfLen, 45f, village.Centre, village.Radius + 25f);
        _len = new Vector2(MathF.Cos(_yaw), -MathF.Sin(_yaw)); _wid = new Vector2(MathF.Sin(_yaw), MathF.Cos(_yaw));
        // no trees on the battle strip
        var ex = new List<Exclusion>();
        var c = new Vector2(_centre.X, _centre.Z);
        for (float x = -halfLen - 20; x <= halfLen + 20; x += 25)
            for (float z = -60; z <= 60; z += 30)
                ex.Add(new Exclusion(c + _len * x + _wid * z, 24f));
        return (ex, null, true);
    }

    /// <summary>
    /// The flattest dry open strip (centre, yaw; length along (cos yaw, −sin yaw)) whose rectangle keeps
    /// <paramref name="keepOut"/> m from <paramref name="avoid"/> (read-only search of the generated map).
    /// </summary>
    static (Vector3, float) FindBattleStrip(TerrainMeshData m, float halfLen, float halfWid, Vector2 avoid, float keepOut)
    {
        float best = float.MaxValue; var bc = Vector3.Zero; float by = 0;
        for (int a = 0; a < 12; a++)
        {
            float yaw = a * MathF.PI / 12;
            var len = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw)); var wid = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            for (float cx = -320; cx <= 320; cx += 20)
                for (float cz = -320; cz <= 320; cz += 20)
                {
                    float sc = 0, lo = float.MaxValue, hi = float.MinValue;
                    for (float x = -halfLen; x <= halfLen && sc < best; x += 12)
                        for (float z = -halfWid; z <= halfWid; z += halfWid / 2)
                        {
                            var p = new Vector2(cx, cz) + len * x + wid * z;
                            if (MathF.Abs(p.X) > m.HalfExtentX - 40 || MathF.Abs(p.Y) > m.HalfExtentZ - 40 || Vector2.Distance(p, avoid) < keepOut) { sc = float.MaxValue; break; }
                            if (m.IsWet(p.X, p.Y)) sc += 1000;
                            sc += m.ClassAt(p.X, p.Y) switch
                            {
                                TerrainClass.Water or TerrainClass.Beach => 1000, TerrainClass.Mountain => 200, TerrainClass.Forest or TerrainClass.Jungle => 40,
                                TerrainClass.Hills => 3, _ => 0,
                            } + (1 - m.NormalAt(p.X, p.Y).Y) * 300;
                            float h = m.HeightAt(p.X, p.Y); lo = MathF.Min(lo, h); hi = MathF.Max(hi, h);
                        }
                    if (sc == float.MaxValue) continue;
                    sc += (hi - lo) * 2 + new Vector2(cx, cz).Length() * 0.05f;
                    if (sc < best) { best = sc; bc = new Vector3(cx, 0, cz); by = yaw; }
                }
        }
        bc.Y = m.HeightAt(bc.X, bc.Z);
        return (bc, by);
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        var m = Env.Terrain.Mesh;
        Cast = new CrowdCast(Device, m.HeightAt);
        Layout(m);
        _occ = new BattleOccluders(m);
        if (Env.Settlement is { } s) _occ.AddWallRing(s.Outline, 3.4f, m.HeightAt);
        BattleShots();
        Update(4f);
    }

    /// <summary>Formation slots along the strip: army A on one side, B on the other, lines 3 m apart at the front,
    /// further lines 28 m behind; slots on water, steep or wooded ground or in the village are skipped.</summary>
    void Layout(TerrainMeshData m)
    {
        int want = SoldierCount / 250;
        var c = new Vector2(_centre.X, _centre.Z);
        var s = Env.Settlement;
        bool Ok(Vector2 p, Vector2 fwd)
        {
            for (float a = -13f; a <= 13f; a += 6.5f)
                for (float b = -1f; b <= 11f; b += 6f)
                {
                    var q = p + _len * a - fwd * b;
                    if (m.IsWet(q.X, q.Y) || m.NormalAt(q.X, q.Y).Y < 0.94f) return false;
                    var cl = m.ClassAt(q.X, q.Y);
                    if (cl is TerrainClass.Forest or TerrainClass.Jungle or TerrainClass.Water or TerrainClass.Mountain) return false;
                    if (s != null && Vector2.Distance(q, s.Centre) < s.Radius + 12f) return false;
                    if (MathF.Abs(q.X) > m.HalfExtentX - 30 || MathF.Abs(q.Y) > m.HalfExtentZ - 30) return false;
                }
            return true;
        }
        int[] placed = new int[2];
        for (int line = 0; line < 8 && placed[0] + placed[1] < want; line++)
            for (int slot = 0; slot < 24 && placed[0] + placed[1] < want; slot++)
            {
                int kx = (slot + 1) / 2;
                float x = (slot % 2 == 1 ? 1 : -1) * kx * 29f;
                for (int force = 0; force < 2 && placed[0] + placed[1] < want; force++)
                {
                    if (placed[force] >= (want + 1 - force) / 2) continue;
                    float back = 1.6f + line * 28f;
                    // army A faces +wid (stands on the -wid side), army B faces -wid
                    var fwd = force == 0 ? _wid : -_wid;
                    var p = c + _len * x - fwd * back;
                    if (!Ok(p, fwd)) continue;
                    float yaw = MathF.Atan2(fwd.X, fwd.Y);
                    var act = line == 0 ? FormationAct.Engage : FormationAct.Idle;
                    bool swords = (slot % 3) == 2;
                    Cast.Formations.Add(new FormationSpec(p, yaw, 25, 10, force, act, (uint)(1000 + line * 100 + slot * 2 + force), Swords: swords,
                        Fallen: act == FormationAct.Engage ? 4 : 0));
                    placed[force]++;
                }
            }
    }

    public override void Configure(RenderSettings settings)
    {
        base.Configure(settings);
        settings.Features.ShadowDistance = 900f;
    }

    public override void Update(float t) { base.Update(t); Cast?.Update(t); }

    public override void DrawShadowCasters(RenderContext rc) { base.DrawShadowCasters(rc); Cast.ApplySwitches(rc.Features); Cast.Crowd.DrawShadows(rc); }
    public override void DrawOpaque(RenderContext rc)
    {
        base.DrawOpaque(rc);
        Cast.ApplySwitches(rc.Features);
        _occOn = rc.Features.OcclusionCulling && !rc.IsReflectionPass;
        if (_occOn) { _occ.Build(_ob, rc); Cast.Crowd.Occlusion = _ob; }
        else Cast.Crowd.Occlusion = null;
        Cast.Crowd.DrawOpaque(rc);
    }

    public string FrameReport() => Cast.Report(_ob, _occOn) + string.Format(CultureInfo.InvariantCulture,
        " formations={0} occluder-terrain-tris={1} occluder-boxes={2}", Cast.Formations.Count, _occ.TerrainTriangles, _occ.Boxes);

    protected Vector3 StripPoint(float along, float across, float up)
    {
        var p = new Vector2(_centre.X, _centre.Z) + _len * along + _wid * across;
        return new Vector3(p.X, Env.Terrain.HeightAt(p.X, p.Y) + up, p.Y);
    }

    protected override void MakeShots() { } // the shots need the formations: BattleShots() after the layout

    Vector3 Ground(Vector2 p, float up) => new(p.X, Env.Terrain.HeightAt(p.X, p.Y) + up, p.Y);

    void BattleShots()
    {
        var front = Cast.Formations.Where(f => f.Act == FormationAct.Engage && f.Force == 0).ToList();
        if (front.Count == 0) front = Cast.Formations.Where(f => f.Force == 0).ToList();
        var all = Cast.Formations.Select(f => f.Centre).ToList();
        var mid = all.Aggregate(Vector2.Zero, (a, b) => a + b) / Math.Max(1, all.Count);
        // the engaged formation of army A nearest the middle, and the far end of the front
        var f0 = front.OrderBy(f => Vector2.Distance(f.Centre, mid)).First();
        float along = front.Max(f => MathF.Abs(Vector2.Dot(f.Centre - mid, _len))) + 20f;
        var lineEnd = mid + _len * along;
        var shots = new List<CaptureShot>
        {
            new("vista", new CameraPose(Ground(mid - _wid * 150f - _len * 60f, 75f), Ground(mid, 0f), 0.85f, 0.5f, 40000f), 10.5f, 4f),
            new("line", new CameraPose(Ground(lineEnd - _wid * 22f, 12f), Ground(mid, 1f), 0.8f, 0.3f, 40000f), 10.5f, 4f),
            new("front-low", new CameraPose(Ground(f0.Centre + _len * 15.5f - f0.Forward * 2.0f, 1.7f), Ground(f0.Centre + f0.Forward * 1.6f - _len * 2f, 1.3f), 0.8f, 0.1f, 40000f), 10.5f, 4f),
            new("rear", new CameraPose(Ground(f0.Centre - f0.Forward * 48f + _len * 6f, 4f), Ground(f0.Centre + f0.Forward * 4f, 1.2f), 0.8f, 0.3f, 40000f), 10.5f, 4f),
        };
        if (Env.Settlement is { } s)
        {
            // from beyond the village, looking past it at the armies: the palisade and the ground hide part of them
            var toBattle = Vector2.Normalize(mid - s.Centre);
            var eye2 = s.Centre - toBattle * (s.Radius + 18f);
            shots.Add(new CaptureShot("village", new CameraPose(Ground(eye2, 2.0f), Ground(mid, 1.5f), 0.9f, 0.3f, 40000f), 10.5f, 4f));
        }
        ShotList = shots.ToArray();
    }

    public override void Dispose() { Cast?.Dispose(); base.Dispose(); }
}

[DemoScene("crowd-battle-2k", "render2: 2,000 instanced soldiers (8 formations) on the env temperate battlefield, CPU occlusion culling")]
public sealed class CrowdBattle2kScene : CrowdBattleSceneBase { protected override int SoldierCount => 2000; }

[DemoScene("crowd-battle-10k", "render2: 10,000 instanced soldiers (40 formations) on the env temperate battlefield, CPU occlusion culling")]
public sealed class CrowdBattle10kScene : CrowdBattleSceneBase { protected override int SoldierCount => 10000; }
