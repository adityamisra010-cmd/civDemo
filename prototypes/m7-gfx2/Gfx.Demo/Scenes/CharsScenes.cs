using Gfx.Chars.Animation;
using Gfx.Chars.Rendering;
using Gfx.Engine.Cameras;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;

namespace Gfx.Demo.Scenes;

/// <summary>
/// Stage chars scenes: baked MakeHuman-based characters (tools/charbake) standing on the render1 photographic meadow
/// of the temperate fixture map (G1's placeholder props are switched off, so every visible object is either the
/// photographic ground and far field or a stage-chars character). Characters are always dressed. PRESENTATION ONLY:
/// the grid is generated from labelled fixture inputs and only read; the characters read nothing from any
/// simulation (DR39, DR42).
/// </summary>
public abstract class CharsSceneBase : Render1SceneBase
{
    protected CharStage Stage { get; private set; } = null!;
    protected readonly List<AnimDriver> Drivers = new();
    /// <summary>The stage site (flattest open meadow near the map centre) and the yaw the line-ups face.</summary>
    protected Vector3 Site { get; private set; }
    protected float Facing { get; private set; }
    /// <summary>Hour of day of the default shots; the cast faces about 35 degrees off the sun's azimuth (three-quarter key light).</summary>
    protected virtual float Hour => 10.5f;
    protected virtual float StageRadius => 10f;

    protected override void OnLoad()
    {
        base.OnLoad();
        Scenery.DrawProps = false;
        Site = BattleSite.FindStage(Scenery, Vector3.Zero, 220, StageRadius);
        var sun = SunModel.At(Hour).DirectionToLight;
        Facing = MathF.Atan2(sun.X, sun.Z) + MathHelper.ToRadians(35f);
        Stage = new CharStage(Device);
        Build();
        Update(0);
    }

    protected abstract void Build();
    protected abstract void Animate(float t);

    /// <summary>A point in the stage frame (x right, y up, z towards the audience) → world, on the ground.</summary>
    protected Vector3 At(float x, float z, float y = 0f)
    {
        var p = Site + Vector3.Transform(new Vector3(x, 0, z), Matrix.CreateRotationY(Facing));
        return new Vector3(p.X, Scenery.HeightAt(p.X, p.Z) + y, p.Z);
    }

    /// <summary>A point in the stage frame at an absolute height above the site's ground level (cameras).</summary>
    protected Vector3 Cam(float x, float y, float z)
    {
        var p = Site + Vector3.Transform(new Vector3(x, 0, z), Matrix.CreateRotationY(Facing));
        return new Vector3(p.X, Site.Y + y, p.Z);
    }

    /// <summary>World transform of a character standing at stage (x, z), turned by <paramref name="yaw"/> from the audience.</summary>
    protected Matrix Place(float x, float z, float yaw = 0f) => Matrix.CreateRotationY(Facing + yaw) * Matrix.CreateTranslation(At(x, z));

    public override Vector3 Focus => Site;
    public override float ShadowRadius => 12f;
    public override void Configure(RenderSettings settings)
    {
        base.Configure(settings);
        // close shots: cascade 0 then resolves a few millimetres on a face; the far terrain keeps cascades 2 and 3
        settings.Features.ShadowDistance = 240f;
    }

    public override void Update(float t)
    {
        base.Update(t);
        if (Stage == null) return;
        Animate(t);
        Stage.Invalidate();
    }

    public override void DrawShadowCasters(RenderContext rc) { base.DrawShadowCasters(rc); Stage.DrawShadows(rc); }
    public override void DrawOpaque(RenderContext rc) { base.DrawOpaque(rc); Stage.DrawOpaque(rc); }
    public override void Dispose() { Stage?.Dispose(); base.Dispose(); }

    /// <summary>Eye midpoint (world) of an instance after the last update.</summary>
    protected static Vector3 Eyes(CharInstance inst)
    {
        inst.SolvePose();
        var s = inst.Asset.Skeleton;
        return Vector3.Transform(0.5f * (inst.Heads[s["eye_l"]] + inst.Heads[s["eye_r"]]), inst.World);
    }
}

/// <summary>
/// The player avatar (G1 PlayerFixture identity) in its medieval kit: an eight-step turntable framed like G1's
/// avatar-turntable shots (for before/after pairs), face close-ups bare-headed, and the five expressions.
/// </summary>
[DemoScene("chars-hero", "Stage chars: player avatar (G1 PlayerFixture identity) turntable, face close-ups, expressions")]
public sealed class CharsHeroScene : CharsSceneBase
{
    CharInstance _a = null!;
    AnimDriver _d = null!;
    Vector3 _eyes;
    static readonly string[] Expressions = { "battlecry", "pain", "grim", "calm", "blink" };

    protected override void Build()
    {
        _a = Stage.Spawn("avatar", "era_medieval");
        _d = new AnimDriver(_a, t => new AnimInput { Speed = 0, Stance = "rest", LookAt = Cam(0, 1.62f, 3.0f), Grip = "relaxed" }, t => Place(0, 0));
        _d.Advance(2.0f);
        _eyes = Eyes(_a);
    }

    // scene time: integer part = turntable step (45 degrees), fraction selects the face mode
    protected override void Animate(float t)
    {
        int step = (int)MathF.Floor(t + 1e-4f);
        float frac = t - step;
        _d.Advance(2.0f);
        _a.World = Matrix.CreateRotationY(MathHelper.ToRadians(45f * step)) * Place(0, 0);
        _a.SetHeadgear(frac < 0.05f || frac > 0.97f);
        _a.Expression.Clear();
        for (int k = 0; k < Expressions.Length; k++)
            if (MathF.Abs(frac - (0.2f + 0.15f * k)) < 0.05f) _a.Expression[Expressions[k]] = 1f;
    }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var l = new List<CaptureShot>();
            // G1 avatar-turntable framing: camera (0, 1.25, 4.2) -> (0, 0.98, 0), fov 0.55 rad, 10:30
            for (int i = 0; i < 8; i++)
                l.Add(new CaptureShot($"yaw{i * 45:000}", new CameraPose(Cam(0, 1.25f, 4.2f), Cam(0, 0.98f, 0), 0.55f, 0.1f, 4000f), Hour, i));
            var e = _eyes; var fwd = Vector3.Normalize(Cam(0, 0, 1) - Cam(0, 0, 0)); var right = Vector3.Cross(fwd, Vector3.Up);
            CameraPose Face(float side, float dist, float lift, float fov = 0.42f) =>
                new(e + fwd * dist + right * side + new Vector3(0, lift, 0), e + new Vector3(0, -0.035f, 0), fov, 0.05f, 4000f);
            l.Add(new CaptureShot("face-front", Face(0, 0.62f, 0.01f), Hour, 0.1f));
            l.Add(new CaptureShot("face-34", Face(0.38f, 0.48f, 0.02f), Hour, 0.1f));
            l.Add(new CaptureShot("face-side", Face(0.60f, 0.04f, 0.0f), Hour, 0.1f));
            l.Add(new CaptureShot("helmet-34", Face(0.45f, 0.62f, 0.05f, 0.5f), Hour, 0f));
            l.Add(new CaptureShot("expr-neutral", Face(0.16f, 0.52f, 0f), Hour, 0.1f));
            for (int k = 0; k < Expressions.Length; k++)
                l.Add(new CaptureShot("expr-" + Expressions[k], Face(0.16f, 0.52f, 0f), Hour, 0.2f + 0.15f * k));
            l.Add(new CaptureShot("face-dusk", Face(0.38f, 0.48f, 0.02f), 17.6f, 0.1f));
            // G1 avatar-turntable "face-front" framing: camera (0, 1.74, 0.95) -> (0, 1.70, 0), fov 0.42 rad (bare-headed here)
            l.Add(new CaptureShot("face-g1cam", new CameraPose(Cam(0, 1.74f, 0.95f), Cam(0, 1.70f, 0), 0.42f, 0.05f, 4000f), Hour, 0.1f));
            return l;
        }
    }
}

/// <summary>
/// The avatar's one identity in three eras (ancient, medieval, early modern): one shared identity asset (face, body,
/// skin, eyes, hair), three outfit files. The era is a fixture input of the scene, never stored in the profile.
/// </summary>
[DemoScene("chars-eras", "Stage chars: the avatar's identity across three eras (one shared body, three outfits)")]
public sealed class CharsErasScene : CharsSceneBase
{
    public static readonly string[] Eras = { "ancient", "medieval", "early_modern" };
    readonly List<CharInstance> _inst = new();
    readonly List<Vector3> _eyes = new();

    protected override void Build()
    {
        for (int i = 0; i < Eras.Length; i++)
        {
            var inst = Stage.Spawn("avatar", "era_" + Eras[i]);
            _inst.Add(inst);
            float x = (i - 1) * 1.25f;
            // at rest: the weapon upright in the right hand, the shield arm relaxed, so the three outfits stay visible
            string stance = "rest";
            int k = i;
            Drivers.Add(new AnimDriver(inst, t => new AnimInput { Speed = 0, LookAt = Cam(0, 1.55f, 5.0f), Stance = stance, Grip = "relaxed" },
                t => Place((k - 1) * 1.25f, 0, -0.25f)));
        }
        foreach (var d in Drivers) d.Advance(1.5f);
        foreach (var inst in _inst) _eyes.Add(Eyes(inst));
    }

    protected override void Animate(float t) { foreach (var d in Drivers) d.Advance(1.5f + t); }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            // G1 avatar-eras framing: camera (0, 1.35, 6.6) -> (0, 0.95, 0), fov 0.55 rad, 10:30
            var l = new List<CaptureShot>
            {
                new("lineup", new CameraPose(Cam(0, 1.35f, 6.6f), Cam(0, 0.95f, 0), 0.55f, 0.1f, 4000f), Hour, 0),
                new("lineup-34", new CameraPose(Cam(2.6f, 1.45f, 4.4f), Cam(0, 0.95f, 0), 0.55f, 0.1f, 4000f), Hour, 0),
            };
            var fwd = Vector3.Normalize(Cam(0, 0, 1) - Cam(0, 0, 0)); var right = Vector3.Cross(fwd, Vector3.Up);
            for (int i = 0; i < Eras.Length; i++)
            {
                var e = _eyes[i];
                l.Add(new CaptureShot("face-" + Eras[i], new CameraPose(e + fwd * 0.75f + right * 0.18f + new Vector3(0, 0.02f, 0), e + new Vector3(0, -0.07f, 0), 0.5f, 0.05f, 4000f), Hour, 0));
            }
            return l;
        }
    }
}

/// <summary>
/// Two opposing forces in line: force A (four soldiers and the commander, woad blue and ochre) faces force B (four
/// soldiers, madder red and bone) across six metres of meadow. Every soldier is a distinct baked identity (face,
/// body, skin, hair, beard) in its force's kit; idles start at different phases so nobody moves in unison.
/// </summary>
[DemoScene("chars-forces", "Stage chars: two opposing forces in line (4 + commander vs 4), distinct identities")]
public sealed class CharsForcesScene : CharsSceneBase
{
    public static readonly string[] ForceA = { "a1", "a2", "a3", "a4" }, ForceB = { "b1", "b2", "b3", "b4" };
    protected override float StageRadius => 12f;
    readonly List<CharInstance> _a = new(), _b = new();
    CharInstance _cmd = null!;
    Vector3 _cmdEyes;

    static string Stance(string id) => id switch { "a3" or "b3" => "sword", _ => "spear" };

    protected override void Build()
    {
        for (int i = 0; i < 4; i++)
        {
            float z = (i - 1.5f) * 1.15f;
            var a = Stage.Spawn(ForceA[i], "kit_a"); _a.Add(a);
            var b = Stage.Spawn(ForceB[i], "kit_b"); _b.Add(b);
            string sa = Stance(ForceA[i]), sb = Stance(ForceB[i]);
            float oa = 0.37f * i, ob = 0.53f * i + 0.2f;
            Drivers.Add(new AnimDriver(a, t => new AnimInput { Speed = 0, Stance = sa, Idle = i % 2 == 0 ? "idle" : "idle_proud", LookAt = At(3.0f, z, 1.6f) },
                t => Place(-3.0f, z, MathHelper.PiOver2), oa));
            Drivers.Add(new AnimDriver(b, t => new AnimInput { Speed = 0, Stance = sb, Idle = i % 2 == 1 ? "idle" : "idle_proud", LookAt = At(-3.0f, z, 1.6f) },
                t => Place(3.0f, z, -MathHelper.PiOver2), ob));
        }
        _cmd = Stage.Spawn("commander_a", "kit_a");
        Drivers.Add(new AnimDriver(_cmd, t => new AnimInput { Speed = 0, Stance = "sword", Idle = "idle_proud", LookAt = At(3.0f, 0.3f, 1.7f) },
            t => Place(-1.9f, 0.3f, MathHelper.PiOver2 - 0.4f), 0.9f));
        foreach (var d in Drivers) d.Advance(2.0f);
        _cmdEyes = Eyes(_cmd);
    }

    protected override void Animate(float t) { foreach (var d in Drivers) d.Advance(2.0f + t); }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var fwd = Vector3.Normalize(Cam(0, 0, 1) - Cam(0, 0, 0)); var right = Vector3.Cross(fwd, Vector3.Up);
            var e = _cmdEyes;
            return new[]
            {
                new CaptureShot("lines-34", new CameraPose(Cam(0.6f, 2.3f, 7.4f), Cam(0f, 0.95f, 0f), 0.62f, 0.1f, 4000f), Hour, 0),
                new CaptureShot("lines-low", new CameraPose(Cam(-0.4f, 1.15f, 4.6f), Cam(0f, 1.15f, 0f), 0.75f, 0.1f, 4000f), Hour, 0),
                new CaptureShot("force-a", new CameraPose(Cam(0.6f, 1.55f, 1.4f), Cam(-3.0f, 1.1f, 0f), 0.62f, 0.1f, 4000f), Hour, 0),
                new CaptureShot("force-b", new CameraPose(Cam(-0.6f, 1.55f, 1.4f), Cam(3.0f, 1.1f, 0f), 0.62f, 0.1f, 4000f), Hour, 0),
                new CaptureShot("commander", new CameraPose(e + fwd * 0.75f - right * 1.05f + new Vector3(0, -0.05f, 0), e + new Vector3(0, -0.22f, 0), 0.55f, 0.05f, 4000f), Hour, 0),
                new CaptureShot("lines-dusk", new CameraPose(Cam(0.6f, 2.3f, 7.4f), Cam(0f, 0.95f, 0f), 0.62f, 0.1f, 4000f), 17.6f, 0),
            };
        }
    }
}

/// <summary>
/// The LOD strip: one soldier at LOD0 (every part), LOD1 (about 3,000 triangles) and LOD2 (about 1,500), side by side
/// at the same distance, and the same three at the distances they are meant for.
/// </summary>
[DemoScene("chars-lod", "Stage chars: LOD strip (LOD0 full detail, LOD1 ~3,000 tris, LOD2 ~1,500 tris)")]
public sealed class CharsLodScene : CharsSceneBase
{
    public string Identity { get; init; } = "a2";
    public string Outfit { get; init; } = "kit_a";
    readonly CharInstance[] _l = new CharInstance[3];

    protected override void Build()
    {
        for (int k = 0; k < 3; k++)
        {
            var inst = Stage.Spawn(Identity, Outfit);
            inst.Lod = k;
            _l[k] = inst;
            float x = (k - 1) * 1.1f;
            Drivers.Add(new AnimDriver(inst, t => new AnimInput { Speed = 0, Stance = "spear" }, t => Place(x, 0, -0.35f)));
        }
        foreach (var d in Drivers) d.Advance(2.0f);
    }

    // scene time 0: side by side; 1: at their distances (LOD0 at 6 m, LOD1 at 18 m, LOD2 at 40 m from the camera)
    protected override void Animate(float t)
    {
        foreach (var d in Drivers) d.Advance(2.0f);
        if (t > 0.5f)
        {
            _l[0].World = Place(-1.1f, 0.0f, -0.35f);
            _l[1].World = Place(1.4f, -12.0f, -0.35f);
            _l[2].World = Place(4.5f, -34.0f, -0.35f);
        }
    }

    public int Triangles(int lod) => _l[lod].AllParts().Sum(p => p.TriangleCount);

    public override IReadOnlyList<CaptureShot> Shots => new[]
    {
        new CaptureShot("strip", new CameraPose(Cam(0, 1.15f, 4.4f), Cam(0, 0.95f, 0), 0.55f, 0.1f, 4000f), Hour, 0),
        new CaptureShot("strip-faces", new CameraPose(Cam(0, 1.62f, 1.9f), Cam(0, 1.52f, 0), 0.55f, 0.05f, 4000f), Hour, 0),
        new CaptureShot("distances", new CameraPose(Cam(0, 1.6f, 6.0f), Cam(0.8f, 1.0f, -12f), 0.62f, 0.1f, 4000f), Hour, 1),
    };
}

/// <summary>
/// Animation programs for contact sheets, one character each: a walk and a run on a moving root (stride-synchronised,
/// foot locking on), an idle-to-thrust-to-block sequence with an additive hit reaction, a death, a surrender (kneel,
/// hands raised) and a head look that follows a moving point. Presentation only: the inputs are a fixed script.
/// </summary>
[DemoScene("chars-anim", "Stage chars: animation programs (walk, run, thrust, block, hit, death, surrender, look)")]
public sealed class CharsAnimScene : CharsSceneBase
{
    protected override float StageRadius => 12f;
    public AnimDriver Walker { get; private set; } = null!;
    public AnimDriver Runner { get; private set; } = null!;
    AnimDriver _fighter = null!, _dier = null!, _kneeler = null!, _looker = null!;
    public const float WalkSpeed = 1.45f, RunSpeed = 3.6f;

    protected override void Build()
    {
        var w = Stage.Spawn("a1", "kit_a");
        Walker = new AnimDriver(w, t => new AnimInput { Speed = WalkSpeed, Stance = "spear", StanceWeight = 0.8f, UpperClip = "idle", UpperClipTime = 1.0f, UpperWeight = 0.7f },
            t => Matrix.CreateRotationY(Facing + MathHelper.PiOver2) * Matrix.CreateTranslation(Along(At(0f, 3.5f), WalkSpeed * t)));
        var r = Stage.Spawn("b1", "kit_b");
        Runner = new AnimDriver(r, t => new AnimInput { Speed = RunSpeed, Stance = "spear", StanceWeight = 0.6f },
            t => Matrix.CreateRotationY(Facing + MathHelper.PiOver2) * Matrix.CreateTranslation(Along(At(-9f, 7.0f), RunSpeed * t)));
        var f = Stage.Spawn("avatar", "era_medieval");
        _fighter = new AnimDriver(f, t => new AnimInput
        {
            Action = t < 2f ? "thrust" : t < 4f ? "block" : null, ActionStart = t < 2f ? 0.5f : 2.0f, HoldLastFrame = false,
            Stance = t >= 4f ? "sword" : "none", HitTime = 4.6f, HitYaw = 0.3f, HitStrength = 1.2f, LookAt = At(0f, 3.0f, 1.6f),
        }, t => Place(0f, 0f, 0f));
        var d = Stage.Spawn("b2", "kit_b");
        _dier = new AnimDriver(d, t => new AnimInput { Action = t >= 0.6f ? "death_fwd" : null, ActionStart = 0.6f }, t => Place(-3.2f, -0.8f, 0.6f));
        var k = Stage.Spawn("b3", "kit_b");
        _kneeler = new AnimDriver(k, t => new AnimInput { Action = t >= 0.5f ? "kneel" : null, ActionStart = 0.5f, Stance = t >= 2.4f ? "hands_up" : "none", StanceWeight = Math.Clamp((t - 2.4f) / 0.4f, 0f, 1f) },
            t => Place(3.2f, -0.6f, -0.5f));
        var l = Stage.Spawn("commander_a", "kit_a");
        _looker = new AnimDriver(l, t => new AnimInput { Speed = 0, Idle = "idle_proud", LookAt = At(-2.2f + 1.5f * t, 3.0f, 1.7f) }, t => Place(0f, -3.4f, 0f));
        Drivers.AddRange(new[] { Walker, Runner, _fighter, _dier, _kneeler, _looker });
        foreach (var dr in Drivers) dr.Advance(0f);
    }

    static readonly string[] Weapons = { "sword", "sword_grip", "spear_shaft", "spear_head", "shield_face", "shield_back", "shield_rim" };

    protected override void Animate(float t)
    {
        foreach (var d in Drivers) d.Advance(t);
        // surrender: once the kneeling soldier raises his hands, his weapons are no longer in them (not drawn; the
        // prototype does not lay dropped weapons on the ground)
        foreach (var w in Weapons) { if (t >= 2.3f) _kneeler.Instance.HiddenParts.Add(w); else _kneeler.Instance.HiddenParts.Remove(w); }
    }

    /// <summary>A point moved <paramref name="metres"/> along the stage's +x (the walkers' path), on the ground.</summary>
    Vector3 Along(Vector3 start, float metres)
    {
        var p = start + Vector3.Transform(new Vector3(metres, 0, 0), Matrix.CreateRotationY(Facing));
        return new Vector3(p.X, Scenery.HeightAt(p.X, p.Z), p.Z);
    }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var l = new List<CaptureShot>();
            var fwd = Vector3.Normalize(Cam(0, 0, 1) - Cam(0, 0, 0)); var right = Vector3.Cross(fwd, Vector3.Up);
            // walk: six frames over one cycle (side view, the camera keeps pace with the walker)
            float cycle = Walker.Animator.CycleSeconds(WalkSpeed);
            for (int i = 0; i < 6; i++)
            {
                float t = 3.0f + cycle * i / 6f;
                var p = Along(At(0f, 3.5f), WalkSpeed * t);
                l.Add(new CaptureShot($"walk-{i}", new CameraPose(p + fwd * 3.6f + new Vector3(0, 1.0f, 0), p + new Vector3(0, 0.95f, 0), 0.6f, 0.1f, 4000f), Hour, t));
            }
            float rcycle = Runner.Animator.CycleSeconds(RunSpeed);
            for (int i = 0; i < 4; i++)
            {
                float t = 2.0f + rcycle * i / 4f;
                var p = Along(At(-9f, 7.0f), RunSpeed * t);
                l.Add(new CaptureShot($"run-{i}", new CameraPose(p + fwd * 3.8f + new Vector3(0, 1.0f, 0), p + new Vector3(0, 0.95f, 0), 0.6f, 0.1f, 4000f), Hour, t));
            }
            var fc = At(0f, 0f);
            foreach (var (name, t) in new[] { ("thrust-a", 0.9f), ("thrust-b", 1.25f), ("block", 2.6f), ("hit-0", 4.6f), ("hit-1", 4.72f), ("hit-2", 5.0f) })
                l.Add(new CaptureShot(name, new CameraPose(fc + fwd * 3.0f + right * 1.4f + new Vector3(0, 1.3f, 0), fc + new Vector3(0, 1.05f, 0), 0.6f, 0.1f, 4000f), Hour, t));
            var dc = At(-3.2f, -0.8f);
            // the CMU fall (90_16) stands for about 2.5 s before the actor drops; the frames sample the fall itself
            foreach (var (name, t) in new[] { ("death-0", 2.6f), ("death-1", 3.0f), ("death-2", 3.4f), ("death-3", 4.5f) })
                l.Add(new CaptureShot(name, new CameraPose(dc + fwd * 3.2f + right * 1.2f + new Vector3(0, 1.4f, 0), dc + new Vector3(0, 0.6f, 0), 0.62f, 0.1f, 4000f), Hour, t));
            var kc = At(3.2f, -0.6f);
            foreach (var (name, t) in new[] { ("surrender-0", 1.2f), ("surrender-1", 2.2f), ("surrender-2", 3.4f) })
                l.Add(new CaptureShot(name, new CameraPose(kc + fwd * 3.0f - right * 0.8f + new Vector3(0, 1.2f, 0), kc + new Vector3(0, 0.7f, 0), 0.62f, 0.1f, 4000f), Hour, t));
            var lc = At(0f, -3.4f);
            foreach (var (name, t) in new[] { ("look-0", 0.2f), ("look-1", 1.5f), ("look-2", 2.8f) })
                l.Add(new CaptureShot(name, new CameraPose(lc + fwd * 1.6f + new Vector3(0, 1.65f, 0), lc + new Vector3(0, 1.55f, 0), 0.55f, 0.05f, 4000f), Hour, t));
            return l;
        }
    }
}
