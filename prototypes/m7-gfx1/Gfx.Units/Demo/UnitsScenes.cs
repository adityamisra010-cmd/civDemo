using Gfx.Engine.Cameras;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;

namespace Gfx.Units.Demo;

/// <summary>Shared plumbing: flat ground (terrain lane integrates later) + one <see cref="SoldierCrowd"/>.</summary>
public abstract class UnitsSceneBase : DemoScene
{
    GpuMesh _ground = null!; InstanceBuffer<InstanceTransform> _groundInst = null!;
    protected SoldierCrowd Crowd { get; private set; } = null!;
    protected SoldierFrame[] Frames = Array.Empty<SoldierFrame>();
    int _version, _prepared = -1; Vector3 _preparedCam;
    protected virtual float GroundSize => 800f;

    protected sealed override void OnLoad()
    {
        Build();
        _ground = GpuMesh.Create(Device, Primitives.Plane(GroundSize, 48, new Color(104, 118, 74)));
        _groundInst = new InstanceBuffer<InstanceTransform>(Device, 1);
        _groundInst.Set(new[] { InstanceTransform.From(Matrix.Identity) });
        Crowd = new SoldierCrowd(Device);
        Update(0);
    }

    protected abstract void Build();
    protected abstract void Pose(float t);
    public sealed override void Update(float t) { Pose(t); _version++; }

    void Prepare(RenderContext rc)
    {
        if (_prepared == _version && _preparedCam == rc.Camera.Position) return;
        Crowd.Prepare(Frames, rc.Camera.Position, rc.Frustum);
        _prepared = _version; _preparedCam = rc.Camera.Position;
    }

    public override void DrawShadowCasters(RenderContext rc) { Prepare(rc); Crowd.Draw(rc); }

    public override void DrawOpaque(RenderContext rc)
    {
        Prepare(rc);
        _ground.DrawInstanced(rc, rc.Effects.Get(EffectNames.InstancedLit), _groundInst);
        Crowd.Draw(rc);
    }

    public override void Dispose() { _ground?.Dispose(); _groundInst?.Dispose(); Crowd?.Dispose(); }

    protected static float Hash01(long id, int salt) => (SoldierLook.Mix(unchecked((ulong)id * 31UL + (ulong)salt)) & 0xFFFF) / 65535f;
}

/// <summary>Close-up: every kit × every clip, plus a same-kit block showing per-entity variation.</summary>
[DemoScene("units-closeup", "Lane U: soldier kits x clip library + per-entity variation block (flat ground)")]
public sealed class UnitsCloseupScene : UnitsSceneBase
{
    static readonly ClipId[] Columns = { ClipId.Idle, ClipId.Walk, ClipId.Run, ClipId.AttackMelee, ClipId.FireRanged, ClipId.HitReact, ClipId.Death, ClipId.RoutRun };
    const int BlockCols = 8, BlockRows = 4;

    protected override void Build() => Frames = new SoldierFrame[4 * Columns.Length + BlockCols * BlockRows];

    protected override void Pose(float t)
    {
        int i = 0;
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < Columns.Length; c++)
            {
                long id = 1000 + r * 100 + c; var clip = Columns[c]; var def = ClipLibrary.Clips[(int)clip];
                var look = SoldierLook.For(id, (Kit)r, r < 2 ? 120 : 80);
                float ct = def.Loop ? t + look.AnimOffsetMs / 1000f : (t % (def.Duration + 1.2f));
                Frames[i++] = new SoldierFrame
                {
                    Position = new Vector3((c - 3.5f) * 1.7f, 0, -r * 2.4f), Yaw = 0, Clip = clip, ClipTime = ct,
                    Visible = true, Side = r < 2 ? 0 : 1, Look = look,
                };
            }
        for (int r = 0; r < BlockRows; r++)
            for (int c = 0; c < BlockCols; c++)
            {
                long id = 5000 + r * BlockCols + c; var look = SoldierLook.For(id, Kit.Spearman, 140);
                Frames[i++] = new SoldierFrame
                {
                    Position = new Vector3(14f + c * 1.3f, 0, -r * 1.5f), Yaw = -0.35f, Clip = (r + c) % 3 == 0 ? ClipId.Walk : ClipId.Idle,
                    ClipTime = t + look.AnimOffsetMs / 1000f, Visible = true, Side = 2, Look = look,
                };
            }
    }

    public override IReadOnlyList<CaptureShot> Shots => new[]
    {
        new CaptureShot("kits-clips", new CameraPose(new Vector3(0, 2.6f, 8.5f), new Vector3(0, 1.0f, -3.0f), MathHelper.ToRadians(50)), 10.5f, 0.45f),
        new CaptureShot("variation", new CameraPose(new Vector3(12.5f, 2.4f, 6.5f), new Vector3(18.5f, 1.1f, -2.4f), MathHelper.ToRadians(50)), 14.5f, 1.3f),
        new CaptureShot("dusk", new CameraPose(new Vector3(-9, 2.2f, 7), new Vector3(1, 1.0f, -3), MathHelper.ToRadians(50)), 18.3f, 0.9f),
    };
    public override Vector3 Focus => new(4, 0, -3);
    public override float ShadowRadius => 22f;
    protected override float GroundSize => 200f;
}

/// <summary>Formation clash driven by the exported battle-trace fixture (interpolated 2 s steps).</summary>
[DemoScene("units-clash", "Lane U: fixture-driven formation clash (battle-trace fixture, 220 soldiers)")]
public sealed class UnitsClashScene : UnitsSceneBase
{
    public BattleTrace Trace { get; private set; } = null!;
    public FormationPlayback Playback { get; private set; } = null!;
    CaptureShot[] _shots = Array.Empty<CaptureShot>();

    protected override void Build()
    {
        Trace = BattleTrace.Load(BattleTrace.FindFixture(BattleTrace.DefaultFixture));
        Playback = new FormationPlayback(Trace, new Vector2(480_000, 330_000));
        Frames = new SoldierFrame[Playback.EntityCount];
        float t0 = Trace.StepTimeMs(0) / 1000f;
        float firstRanged = float.MaxValue, firstMelee = float.MaxValue;
        foreach (var e in Trace.Events)
            if (e.Kind == TraceEventKind.Fire)
            {
                float s = e.TimeMs / 1000f - t0;
                if (e.A == 2) firstRanged = MathF.Min(firstRanged, s); else firstMelee = MathF.Min(firstMelee, s);
            }
        Vector3 Centroid(float t, ClipId? clip, int side = -1)
        {
            Playback.Evaluate(t, Frames); var c = Vector3.Zero; int n = 0;
            foreach (var f in Frames) if (f.Visible && (clip == null || f.Clip == clip) && (side < 0 || f.Side == side)) { c += f.Position; n++; }
            return n > 0 ? c / n : Vector3.Zero;
        }
        float tv = firstRanged == float.MaxValue ? 10 : firstRanged + 0.5f;
        float tm = firstMelee == float.MaxValue ? Playback.DurationSeconds / 2 : firstMelee + 2.7f;
        float te = Playback.DurationSeconds;
        var c0 = Centroid(4, null, 0); var cMelee = Centroid(tm, ClipId.AttackMelee); var cEnd = Centroid(te, ClipId.Death);
        var cFire = Centroid(tv, ClipId.FireRanged, 0);
        _shots = new[]
        {
            new CaptureShot("approach", new CameraPose(c0 + new Vector3(4, 6, 24), c0 + new Vector3(20, 1, 0), MathHelper.ToRadians(55)), 9.5f, 4),
            new CaptureShot("volley", new CameraPose(cFire + new Vector3(-9, 3.5f, 11), cFire + new Vector3(8, 1.2f, 0), MathHelper.ToRadians(55)), 11f, tv),
            new CaptureShot("melee", new CameraPose(cMelee + new Vector3(-9, 4.5f, 12), cMelee + new Vector3(0, 0.8f, 0), MathHelper.ToRadians(50)), 15.5f, tm),
            new CaptureShot("aftermath", new CameraPose(cEnd + new Vector3(14, 9, 20), cEnd), 17.8f, te),
        };
    }

    protected override void Pose(float t) => Playback.Evaluate(t, Frames);
    public override IReadOnlyList<CaptureShot> Shots => _shots;
    public override float ShadowRadius => 60f;
}

/// <summary>Synthetic large battle line (LABELLED SYNTHETIC — not fixture data) for wide shots and the 1k/5k/15k bench.</summary>
public abstract class UnitsCrowdScene : UnitsSceneBase
{
    protected abstract int Count { get; }
    float _front;
    const int BlockCols = 25, BlockRows = 10, PerBlock = BlockCols * BlockRows;
    const float Spacing = 1.5f;
    readonly List<(Vector3 pos, float yaw, ClipId clip, SoldierLook look, int side, float clipT)> _base = new();

    protected override void Build()
    {
        int blocksPerArmy = Math.Max(1, Count / 2 / PerBlock);
        int across = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(blocksPerArmy * 1.6f)));
        float blockW = BlockCols * Spacing + 6, blockD = BlockRows * Spacing + 10;
        _front = across * blockW;
        for (int side = 0; side < 2; side++)
            for (int b = 0; b < blocksPerArmy; b++)
            {
                int line = b / across, slot = b % across;
                Kit kit = line == 0 ? (slot % 2 == 0 ? Kit.Spearman : Kit.Swordsman) : (line % 2 == 1 ? Kit.Archer : Kit.Musketeer);
                float sx = side == 0 ? -1 : 1;
                for (int r = 0; r < BlockRows; r++)
                    for (int c = 0; c < BlockCols; c++)
                    {
                        long id = side * 1_000_000 + b * 1000 + r * BlockCols + c;
                        var look = SoldierLook.For(id, kit, side == 0 ? 120 : 85);
                        float x = sx * (1.2f + line * blockD + r * Spacing) + (Hash01(id, 1) - 0.5f) * 0.4f;
                        float z = -_front / 2 + slot * blockW + c * Spacing + (Hash01(id, 2) - 0.5f) * 0.4f;
                        ClipId clip = line == 0 && r < 2 ? ClipId.AttackMelee
                            : kit is Kit.Archer or Kit.Musketeer ? ClipId.FireRanged
                            : (Hash01(id, 3) < 0.15f ? ClipId.Walk : ClipId.Idle);
                        float clipT = 0;
                        if (line == 0 && r < 2 && Hash01(id, 4) < 0.12f) { clip = ClipId.Death; clipT = 10; x += sx * -0.5f; }
                        else if (line == 0 && r < 3 && Hash01(id, 5) < 0.06f) clip = ClipId.HitReact;
                        _base.Add((new Vector3(x, 0, z), side == 0 ? MathF.PI / 2 : -MathF.PI / 2, clip, look, side, clipT));
                    }
            }
        Frames = new SoldierFrame[_base.Count];
    }

    protected override void Pose(float t)
    {
        for (int i = 0; i < _base.Count; i++)
        {
            var b = _base[i];
            float ct = b.clip switch
            {
                ClipId.Death => b.clipT,
                ClipId.HitReact => (t + b.look.AnimOffsetMs / 1000f) % 1.4f,
                _ => t + b.look.AnimOffsetMs / 1000f,
            };
            Frames[i] = new SoldierFrame { Position = b.pos, Yaw = b.yaw, Clip = b.clip, ClipTime = ct, Visible = true, Side = b.side, Look = b.look };
        }
    }

    public int SoldierCount => _base.Count;
    public override IReadOnlyList<CaptureShot> Shots => new[]
    {
        new CaptureShot("wide", new CameraPose(new Vector3(-0.42f * _front - 20, 0.2f * _front + 12, 0.42f * _front + 30), new Vector3(0, 0, 0.1f * _front), MathHelper.ToRadians(55), 0.5f, 4000f), 10f, 0.5f),
        new CaptureShot("frontline", new CameraPose(new Vector3(-16, 7, 26), new Vector3(0, 1, 6), MathHelper.ToRadians(55)), 16.5f, 0.8f),
    };
    public override float ShadowRadius => 90f;
    protected override float GroundSize => MathF.Max(1500, _front * 6f);
}

[DemoScene("units-wide5k", "Lane U: 5k soldiers, synthetic battle line (wide + front-line shots); bench 5k")]
public sealed class UnitsWide5kScene : UnitsCrowdScene { protected override int Count => 5000; }

[DemoScene("units-bench-1k", "Lane U bench: 1k soldiers (synthetic battle line)")]
public sealed class UnitsBench1kScene : UnitsCrowdScene { protected override int Count => 1000; }

[DemoScene("units-bench-15k", "Lane U bench: 15k soldiers (synthetic battle line)")]
public sealed class UnitsBench15kScene : UnitsCrowdScene { protected override int Count => 15000; }
