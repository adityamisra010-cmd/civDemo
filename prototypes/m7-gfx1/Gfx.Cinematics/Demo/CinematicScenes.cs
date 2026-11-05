using System.Globalization;
using Gfx.Avatar;
using Gfx.Engine.Cameras;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Cinematics.Demo;

/// <summary>Waving heraldic banner on a pole (CPU cloth-ish grid, vertex-coloured pattern).</summary>
public sealed class BannerMesh : IDisposable
{
    const int Nx = 24, Ny = 16; const float W = 1.5f, H = 0.95f, Pole = 3.8f;
    readonly DynamicMesh _mesh; readonly Color _p, _s; readonly HeraldryPattern _pat;
    public BannerMesh(GraphicsDevice gd, Heraldry h)
    {
        _p = Colour.Parse(h.Primary); _s = Colour.Parse(h.Secondary); _pat = h.Pattern;
        var ix = new List<int>();
        for (int y = 0; y < Ny; y++) for (int x = 0; x < Nx; x++) { int a = y * (Nx + 1) + x, b = a + 1, c = a + Nx + 1, d = c + 1; ix.AddRange(new[] { a, c, b, b, c, d }); }
        int pole = (Nx + 1) * (Ny + 1);
        var tv = new List<VertexPNC>(); var ti = new List<int>();
        Primitives.AddBox(tv, ti, new Vector3(-0.04f, Pole / 2, 0), new Vector3(0.035f, Pole / 2, 0.035f), new Color(90, 70, 50));
        Primitives.AddBox(tv, ti, new Vector3(-0.04f, Pole + 0.06f, 0), new Vector3(0.06f), new Color(210, 170, 70));
        foreach (var t in ti) ix.Add(pole + t);
        _mesh = new DynamicMesh(gd, pole + tv.Count, ix.ToArray());
        for (int i = 0; i < tv.Count; i++) _mesh.Vertices[pole + i] = tv[i];
    }

    public Color PatternAt(float u, float v) => _pat switch
    {
        HeraldryPattern.PerFess => v < 0.5f ? _s : _p,
        HeraldryPattern.PerPale => u < 0.5f ? _p : _s,
        HeraldryPattern.Cross => MathF.Abs(u - 0.4f) < 0.09f || MathF.Abs(v - 0.5f) < 0.12f ? _s : _p,
        HeraldryPattern.Chevron => MathF.Abs(v - (1 - 1.6f * MathF.Abs(u - 0.5f)) + 0.15f) < 0.16f ? _s : _p,
        _ => MathF.Abs(u - v) < 0.12f || MathF.Abs(u - (1 - v)) < 0.12f ? _s : _p,
    };

    /// <summary>raise 0 = folded at the pole foot (hidden), 1 = at the masthead.</summary>
    public void Update(float t, float raise, Vector3 position, float yaw)
    {
        float top = 1.0f + (Pole - 1.0f) * raise;
        var v = _mesh.Vertices;
        for (int y = 0; y <= Ny; y++)
            for (int x = 0; x <= Nx; x++)
            {
                float u = x / (float)Nx, w = y / (float)Ny;
                float amp = 0.11f * u;
                float wave = amp * MathF.Sin(u * 7f - t * 4.2f + w * 1.3f);
                v[y * (Nx + 1) + x] = new VertexPNC(new Vector3(u * W, top - w * H - 0.05f * u * u, wave), Vector3.Backward, PatternAt(u, w));
            }
        for (int y = 0; y <= Ny; y++)
            for (int x = 0; x <= Nx; x++)
            {
                int i = y * (Nx + 1) + x;
                var dx = v[y * (Nx + 1) + Math.Min(x + 1, Nx)].Position - v[y * (Nx + 1) + Math.Max(x - 1, 0)].Position;
                var dy = v[Math.Min(y + 1, Ny) * (Nx + 1) + x].Position - v[Math.Max(y - 1, 0) * (Nx + 1) + x].Position;
                var n = Vector3.Normalize(Vector3.Cross(dy, dx));
                v[i] = new VertexPNC(v[i].Position, n, v[i].Color);
            }
        _mesh.Upload();
        _mesh.SetWorld(Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(position), Vector4.One);
    }

    public void Draw(RenderContext rc, Effect e) => _mesh.Draw(rc, e);
    public void Dispose() => _mesh.Dispose();
}

/// <summary>
/// Outcome-driven cinematic scene: plan (pure) → player (CPU) → two avatars, set, banners, war-record card.
/// Captures one key frame per shot (three for the outcome beat). Inputs are frozen: the scene reads the outcome
/// and both profiles and never writes them (tests hash them before/after a render).
/// </summary>
public abstract class CinematicSceneBase : DemoScene
{
    public WarOutcome Outcome { get; }
    public AvatarProfile Player { get; }
    public AvatarProfile OpponentProfile { get; }
    public CinematicPlan Plan { get; }
    public CinematicPlayer Cine { get; }
    public CineFrame? Frame { get; private set; }

    GpuMesh _set = null!; InstanceBuffer<InstanceTransform> _setInst = null!;
    PosedAvatar _avatar = null!, _opponent = null!; BannerMesh _pBanner = null!, _oBanner = null!;
    DynamicMesh _card = null!; float _t;

    /// <summary>World offset of the whole staged set (integration: a flat spot on the battle terrain). Default origin.</summary>
    protected virtual Vector3 Origin => Vector3.Zero;
    /// <summary>false omits the field set's flat ground (a background such as the battle terrain supplies it).</summary>
    protected virtual bool SetGround => true;
    /// <summary>Optional background drawn before the staged set (both passes; <c>rc.IsShadowPass</c> tells which).</summary>
    protected virtual void DrawBackground(RenderContext rc) { }
    CameraPose Shift(CameraPose c) => c with { Position = c.Position + Origin, Target = c.Target + Origin };

    protected CinematicSceneBase(WarOutcome outcome, ulong seed, AvatarProfile? player = null, AvatarProfile? opponent = null, CinematicSettings? settings = null)
    {
        Outcome = outcome; Player = player ?? AvatarProfile.PlayerFixture();
        OpponentProfile = opponent ?? AvatarProfile.Sample(0xC0FFEEUL + (ulong)outcome.OpponentCultureId, "Opposing leader");
        Plan = CinematicPlanner.Plan(outcome, seed, settings);
        Cine = new CinematicPlayer(Plan);
    }

    protected override void OnLoad()
    {
        var tier = Plan.EraCapabilityTier;
        _avatar = new PosedAvatar(Device, AvatarMesh.Build(Player, EraDress.Select(Player, tier, Plan.AvatarRole), new AvatarBuildOptions(Sword: Plan.AvatarArmed)));
        _opponent = new PosedAvatar(Device, AvatarMesh.Build(OpponentProfile, EraDress.Select(OpponentProfile, tier, Plan.OpponentRole)));
        _set = GpuMesh.Create(Device, CineSets.Build(Plan.Set, Outcome.WeatherTimeSeed, Colour.Parse(Player.Heraldry.Primary), Colour.Parse(OpponentProfile.Heraldry.Primary), SetGround));
        _setInst = new InstanceBuffer<InstanceTransform>(Device, 1); _setInst.Set(new[] { InstanceTransform.From(Matrix.CreateTranslation(Origin)) });
        _pBanner = new BannerMesh(Device, Player.Heraldry); _oBanner = new BannerMesh(Device, OpponentProfile.Heraldry);
        BuildCard();
        Update(0);
    }

    void BuildCard()
    {
        var v = new List<VertexPNC>(); var ix = new List<int>();
        var bg = Color.Lerp(Colour.Parse(Player.Heraldry.Primary), Color.Black, 0.72f); var edge = Colour.Parse(Player.Heraldry.Secondary);
        var text = new Color(236, 228, 206);
        const float Wd = 1.0f, Ht = 0.62f, cell = 0.0052f;
        GlyphText.Quad(v, ix, -Wd / 2 - 0.012f, Ht / 2 + 0.012f, Wd + 0.024f, Ht + 0.024f, edge, -0.001f);
        GlyphText.Quad(v, ix, -Wd / 2, Ht / 2, Wd, Ht, bg, 0);
        var rec = Outcome.Record; var inv = CultureInfo.InvariantCulture;
        string[] lines =
        {
            "OUTCOME: " + Label(Plan.OutcomeBeat),
            "DURATION: " + rec.DurationDays.ToString(inv) + " DAYS",
            "BATTLES: " + rec.Battles.ToString(inv),
            "LOSSES: " + rec.CasualtiesPlayer.ToString("N0", inv) + " / " + rec.CasualtiesOpponent.ToString("N0", inv),
            "TERRITORY: " + (rec.TerritoryCellsChanged >= 0 ? "+" : "") + rec.TerritoryCellsChanged.ToString(inv) + " CELLS",
        };
        string title = "WAR RECORD";
        GlyphText.Add(v, ix, title, -GlyphText.Width(title, cell * 2f) / 2, Ht / 2 - 0.05f, cell * 2f, edge);
        for (int i = 0; i < lines.Length; i++) GlyphText.Add(v, ix, lines[i], -Wd / 2 + 0.06f, Ht / 2 - 0.16f - i * 0.062f, cell, text);
        string label = "FIXTURE DATA - NOT SIMULATION STATE";
        GlyphText.Add(v, ix, label, -GlyphText.Width(label, cell * 0.7f) / 2, -Ht / 2 + 0.06f, cell * 0.7f, new Color(170, 160, 140));
        _card = new DynamicMesh(Device, v.Count, ix.ToArray());
        v.CopyTo(_card.Vertices); _card.Upload();
    }

    static string Label(OutcomeBeatKind k) => k switch
    {
        OutcomeBeatKind.Victory => "DECISIVE VICTORY", OutcomeBeatKind.Submission => "ENEMY SURRENDER", OutcomeBeatKind.Capture => "LEADER CAPTURED",
        OutcomeBeatKind.Execution => "LEADER EXECUTED", OutcomeBeatKind.Treaty => "NEGOTIATED PEACE", OutcomeBeatKind.Standoff => "INCONCLUSIVE",
        OutcomeBeatKind.DefeatYield => "DEFEAT", OutcomeBeatKind.RepresentativeYields => "VICTORY - LEADER FLED",
        OutcomeBeatKind.AftermathCut => "WAR ENDED", _ => "VICTORY",
    };

    public override void Update(float t)
    {
        _t = t; var f = Cine.Evaluate(t); Frame = f;
        var o = Origin; var shift = Matrix.CreateTranslation(o);
        _avatar.SetPose(f.Avatar.Pose, f.Avatar.World * shift, o.Y);
        _opponent.SetPose(f.Opponent.Pose, f.Opponent.World * shift, o.Y);
        _pBanner.Update(t, f.PlayerBannerRaise, o + (Plan.Banner == BannerSide.Both ? Cine.Mid + new Vector3(-0.3f, 0, -1.8f) : Cine.PlayerBannerPos), Plan.Banner == BannerSide.Both ? MathF.PI : 0);
        _oBanner.Update(t, f.OpponentBannerRaise, o + (Plan.Banner == BannerSide.Both ? Cine.Mid + new Vector3(0.3f, 0, -1.8f) : Cine.OpponentBannerPos), 0);
    }

    public override void Configure(RenderSettings s)
    {
        base.Configure(s);
        var (fog, exposure) = CinematicPlayer.WeatherLook(Plan.Weather);
        s.FogParams = fog; s.Exposure = exposure;
    }

    public override float ShadowRadius => 18f;
    public override Vector3 Focus => Cine.Mid + Origin;

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var l = new List<CaptureShot>(); float hour = Plan.HourTenths / 10f;
            for (int i = 0; i < Plan.Shots.Count; i++)
            {
                var s = Plan.Shots[i]; float st = Cine.ShotStart(i), d = s.DurationMs / 1000f;
                float[] us = s.Beat == Beat.OutcomeBeat ? new[] { 0.3f, 0.62f, 0.92f } : new[] { 0.62f };
                foreach (var u in us)
                {
                    float t = st + u * d;
                    l.Add(new CaptureShot($"{i:00}-{s.Beat.ToString().ToLowerInvariant()}-{(int)(u * 100):00}", Shift(Cine.Evaluate(t).Camera), hour, t));
                }
            }
            return l;
        }
    }

    bool Show(bool avatar) => Frame is { } f && (avatar ? f.Avatar.Visible : f.Opponent.Visible);

    public override void DrawShadowCasters(RenderContext rc)
    {
        DrawBackground(rc);
        if (Show(true)) _avatar.DrawShadow(rc);
        if (Show(false)) _opponent.DrawShadow(rc);
        var depth = rc.Effects.Get(EffectNames.InstancedDepth);
        _set.DrawInstanced(rc, depth, _setInst);
        if (Frame?.PlayerBannerRaise > 0) _pBanner.Draw(rc, depth);
        if (Frame?.OpponentBannerRaise > 0) _oBanner.Draw(rc, depth);
    }

    public override void DrawOpaque(RenderContext rc)
    {
        DrawBackground(rc);
        var lit = rc.Effects.Get(EffectNames.InstancedLit);
        _set.DrawInstanced(rc, lit, _setInst);
        if (Show(true)) _avatar.Draw(rc);
        if (Show(false)) _opponent.Draw(rc);
        if (Frame?.PlayerBannerRaise > 0) _pBanner.Draw(rc, lit);
        if (Frame?.OpponentBannerRaise > 0) _oBanner.Draw(rc, lit);
        if (Frame?.CardVisible == true)
        {
            // camera-facing panel just beyond the near plane, filling ~65 % of the view height
            var cam = rc.Camera; var fwd = cam.Forward; var right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.Up)); var up = Vector3.Cross(right, fwd);
            float dist = 0.62f / (0.65f * 2f * MathF.Tan(cam.FovYRadians / 2));
            var pos = cam.Position + fwd * dist;
            var world = new Matrix(right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0, -fwd.X, -fwd.Y, -fwd.Z, 0, pos.X, pos.Y, pos.Z, 1);
            float fade = Math.Clamp(Frame.ShotU / 0.15f, 0, 1) * 1.35f;
            _card.SetWorld(world, new Vector4(fade, fade, fade, 1));
            rc.Device.DepthStencilState = DepthStencilState.None;
            _card.Draw(rc, rc.Effects.Get(CineEffectNames.Unlit));
            rc.Device.DepthStencilState = DepthStencilState.Default;
        }
    }

    public override void Dispose()
    {
        foreach (var d in new IDisposable?[] { _set, _setInst, _avatar, _opponent, _pBanner, _oBanner, _card }) d?.Dispose();
    }
}

[DemoScene("cine-execution", "Lane A: cinematic — leader executed (fixture; execution content ON)")]
public sealed class CineExecutionScene() : CinematicSceneBase(WarOutcomeFixtures.Execution, 1);
[DemoScene("cine-surrender", "Lane A: cinematic — enemy surrender (leader kneels)")]
public sealed class CineSurrenderScene() : CinematicSceneBase(WarOutcomeFixtures.Surrender, 1);
[DemoScene("cine-capture", "Lane A: cinematic — leader captured (led away bound)")]
public sealed class CineCaptureScene() : CinematicSceneBase(WarOutcomeFixtures.Capture, 1);
[DemoScene("cine-peace", "Lane A: cinematic — negotiated peace (treaty handshake)")]
public sealed class CinePeaceScene() : CinematicSceneBase(WarOutcomeFixtures.Peace, 1);
[DemoScene("cine-inconclusive", "Lane A: cinematic — inconclusive war (no victory beats, no banner)")]
public sealed class CineInconclusiveScene() : CinematicSceneBase(WarOutcomeFixtures.Inconclusive, 1);
[DemoScene("cine-naval", "Lane A: cinematic — naval victory, leader fled (representative)")]
public sealed class CineNavalScene() : CinematicSceneBase(WarOutcomeFixtures.Naval, 1);
[DemoScene("cine-siege", "Lane A: cinematic — siege victory, leader captured (fallen fortress)")]
public sealed class CineSiegeScene() : CinematicSceneBase(WarOutcomeFixtures.Siege, 1);
[DemoScene("cine-defeat", "Lane A: cinematic — defeat (player's avatar on the losing side)")]
public sealed class CineDefeatScene() : CinematicSceneBase(WarOutcomeFixtures.Defeat, 1);

/// <summary>Injectable variant for tests (any outcome/seed/profiles/settings). Not a registered demo scene.</summary>
public sealed class CinematicScene(WarOutcome outcome, ulong seed, AvatarProfile? player = null, AvatarProfile? opponent = null, CinematicSettings? settings = null)
    : CinematicSceneBase(outcome, seed, player, opponent, settings);
