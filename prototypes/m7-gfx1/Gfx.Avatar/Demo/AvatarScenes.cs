using Gfx.Engine.Cameras;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Units;
using Microsoft.Xna.Framework;

namespace Gfx.Avatar.Demo;

/// <summary>Ground plane + a set of CPU-posed avatars standing in rest pose.</summary>
public abstract class AvatarSceneBase : DemoScene
{
    GpuMesh _ground = null!; InstanceBuffer<InstanceTransform> _groundInst = null!;
    protected readonly List<(PosedAvatar avatar, Func<float, Matrix> world)> Figures = new();

    protected override void OnLoad()
    {
        _ground = GpuMesh.Create(Device, Primitives.Plane(80, 16, new Color(112, 116, 96)));
        _groundInst = new InstanceBuffer<InstanceTransform>(Device, 1);
        _groundInst.Set(new[] { InstanceTransform.From(Matrix.Identity) });
        Build();
        Update(0);
    }

    protected abstract void Build();
    protected void Add(AvatarModel m, Func<float, Matrix> world) => Figures.Add((new PosedAvatar(Device, m), world));

    public override void Update(float t)
    {
        var rest = Pose.Rest();
        foreach (var (a, w) in Figures) a.SetPose(rest, w(t));
    }

    public override float ShadowRadius => 12f;
    public override void DrawShadowCasters(RenderContext rc) { foreach (var (a, _) in Figures) a.DrawShadow(rc); }
    public override void DrawOpaque(RenderContext rc)
    {
        _ground.DrawInstanced(rc, rc.Effects.Get(EffectNames.InstancedLit), _groundInst);
        foreach (var (a, _) in Figures) a.Draw(rc);
    }
    public override void Dispose() { _ground?.Dispose(); _groundInst?.Dispose(); foreach (var (a, _) in Figures) a.Dispose(); }
}

/// <summary>Player avatar (fixture profile) turning on the spot; sceneTime = turntable step (60° each).</summary>
[DemoScene("avatar-turntable", "Lane A: persistent player avatar turntable (medieval royal outfit) + face close-ups")]
public sealed class AvatarTurntableScene : AvatarSceneBase
{
    protected override void Build()
    {
        var p = AvatarProfile.PlayerFixture();
        Add(AvatarMesh.Build(p, EraDress.Select(p, capabilityTier: 4, AvatarRole.Royal)), t => Matrix.CreateRotationY(t * MathHelper.ToRadians(60)));
    }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var l = new List<CaptureShot>();
            for (int i = 0; i < 6; i++)
                l.Add(new CaptureShot($"yaw{i * 60:000}", new CameraPose(new Vector3(0, 1.25f, 4.2f), new Vector3(0, 0.98f, 0), 0.55f), 10.5f, i));
            l.Add(new CaptureShot("face-front", new CameraPose(new Vector3(0, 1.74f, 0.95f), new Vector3(0, 1.70f, 0), 0.42f), 10.5f, 0));
            l.Add(new CaptureShot("face-34", new CameraPose(new Vector3(0, 1.74f, 0.95f), new Vector3(0, 1.70f, 0), 0.42f), 10.5f, 0.6f));
            return l;
        }
    }
}

/// <summary>
/// Same identity in four eras (capability tiers 1, 3, 5, 7 → ancient, medieval, early-modern, modern), three rows
/// (military, civil, royal). Era is computed from the tier input, never stored in the profile.
/// </summary>
[DemoScene("avatar-eras", "Lane A: one identity across 4 eras x 3 roles (computed era dress)")]
public sealed class AvatarErasScene : AvatarSceneBase
{
    public static readonly int[] Tiers = { 1, 3, 5, 7 };
    static readonly AvatarRole[] Roles = { AvatarRole.Military, AvatarRole.Civil, AvatarRole.Royal };
    readonly AvatarProfile _profile;

    public AvatarErasScene() : this(AvatarProfile.PlayerFixture()) { }
    /// <summary>Renders <paramref name="profile"/> (read only — the scene never writes it).</summary>
    public AvatarErasScene(AvatarProfile profile) => _profile = profile;

    protected override void Build()
    {
        var p = _profile;
        for (int r = 0; r < Roles.Length; r++)
            for (int e = 0; e < Tiers.Length; e++)
            {
                var pos = new Vector3(r * 14f + (e - 1.5f) * 1.25f, 0, 0);
                Add(AvatarMesh.Build(p, EraDress.Select(p, Tiers[e], Roles[r])), _ => Matrix.CreateRotationY(-0.25f) * Matrix.CreateTranslation(pos));
            }
    }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var l = new List<CaptureShot>();
            for (int r = 0; r < Roles.Length; r++)
                l.Add(new CaptureShot("lineup-" + Roles[r].ToString().ToLowerInvariant(),
                    new CameraPose(new Vector3(r * 14f, 1.35f, 6.6f), new Vector3(r * 14f, 0.95f, 0), 0.55f), 10.5f, 0));
            return l;
        }
    }
}

/// <summary>Face-variation sheet: 12 deterministic sample profiles (heads only), 3/4 and front views.</summary>
[DemoScene("avatar-faces", "Lane A: face-variation sheet (12 sample profiles, 14 morph channels)")]
public sealed class AvatarFacesScene : AvatarSceneBase
{
    protected override void Build()
    {
        for (int i = 0; i < 12; i++)
        {
            var p = AvatarProfile.Sample(1000UL + (ulong)i, "Sample " + i);
            var m = AvatarMesh.Build(p, null, new AvatarBuildOptions(HeadOnly: true));
            var g = new Vector3((i % 4 - 1.5f) * 0.30f, 1.70f - (i / 4 - 1f) * 0.32f, 0);
            float s = m.Scale;
            Add(m, t => Matrix.CreateTranslation(-AvatarMesh.HeadCentre * s) * Matrix.CreateRotationY(t > 0.5f ? 0f : 0.45f) * Matrix.CreateTranslation(g));
        }
    }

    public override IReadOnlyList<CaptureShot> Shots => new[]
    {
        new CaptureShot("sheet-34", new CameraPose(new Vector3(0, 1.70f, 2.05f), new Vector3(0, 1.70f, 0), 0.55f), 10.5f, 0),
        new CaptureShot("sheet-front", new CameraPose(new Vector3(0, 1.70f, 2.05f), new Vector3(0, 1.70f, 0), 0.55f), 10.5f, 1),
    };
}
