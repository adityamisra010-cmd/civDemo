using Gfx.Avatar;
using Gfx.Engine.Cameras;
using Gfx.Units;
using Microsoft.Xna.Framework;

namespace Gfx.Cinematics;

/// <summary>One character at an instant (presentation only).</summary>
public readonly record struct CharacterFrame(bool Visible, string Clip, Pose Pose, Vector3 Position, float Yaw)
{
    public Matrix World => Matrix.CreateRotationY(Yaw) * Matrix.CreateTranslation(Position);
}

public sealed record CineFrame(int ShotIndex, Beat Beat, float ShotU, CameraPose Camera, CharacterFrame Avatar, CharacterFrame Opponent,
    float PlayerBannerRaise, float OpponentBannerRaise, bool CardVisible);

/// <summary>
/// Evaluates a <see cref="CinematicPlan"/> at absolute time t (seconds): which shot, both characters' clip/pose/
/// placement (staging = procedural positioning around two marks) and the choreographed camera. CPU-only and
/// deterministic; reads the plan/outcome/profiles, never writes them. GPU drawing lives in the demo scene.
/// </summary>
public sealed class CinematicPlayer
{
    public CinematicPlan Plan { get; }
    public float Duration => Plan.TotalMs / 1000f;
    public Vector3 AvatarMark { get; }
    public Vector3 OpponentMark { get; }
    public Vector3 Mid => (AvatarMark + OpponentMark) * 0.5f;
    public Vector3 PlayerBannerPos => AvatarMark + new Vector3(-1.4f, 0, -1.8f);
    public Vector3 OpponentBannerPos => OpponentMark + new Vector3(1.4f, 0, -1.8f);
    const float FaceOpp = MathF.PI / 2, FaceAv = -MathF.PI / 2;

    public CinematicPlayer(CinematicPlan plan)
    {
        Plan = plan;
        float d = plan.OutcomeBeat switch
        {
            OutcomeBeatKind.Treaty => 0.82f, OutcomeBeatKind.Execution => 1.05f, OutcomeBeatKind.Submission => 1.5f,
            OutcomeBeatKind.Standoff => 2.6f, _ => 2.0f,
        };
        AvatarMark = new Vector3(-d / 2, 0, 0); OpponentMark = new Vector3(d / 2, 0, 0);
    }

    /// <summary>Start time (s) of shot <paramref name="i"/>.</summary>
    public float ShotStart(int i) { int ms = 0; for (int k = 0; k < i; k++) ms += Plan.Shots[k].DurationMs; return ms / 1000f; }

    public CineFrame Evaluate(float t)
    {
        int i = 0; float start = 0;
        while (i < Plan.Shots.Count - 1 && t >= start + Plan.Shots[i].DurationMs / 1000f) { start += Plan.Shots[i].DurationMs / 1000f; i++; }
        var shot = Plan.Shots[i]; float dur = shot.DurationMs / 1000f;
        float local = Math.Clamp(t - start, 0, dur), u = local / dur;

        var av = Character(shot.AvatarClip, local, dur, AvatarMark, FaceOpp);
        var op = Character(shot.OpponentClip, local, dur, OpponentMark, FaceAv);
        if (shot.Beat == Beat.AvatarIntro && av.Visible)
        {
            float k = Math.Clamp(u / 0.85f, 0, 1);
            var from = AvatarMark + new Vector3(-5.0f, 0, -1.4f);
            var pos = Vector3.Lerp(from, AvatarMark, k);
            var dir = AvatarMark - from;
            av = av with { Position = pos, Yaw = MathF.Atan2(dir.X, dir.Z), Pose = k >= 1 ? CineClips.Sample("stand-proud", local, dur) : av.Pose };
        }
        if (shot.Beat == Beat.OutcomeBeat && Plan.OutcomeBeat == OutcomeBeatKind.Capture)
        {
            float turn = Math.Clamp(local / 0.8f, 0, 1);
            var opPos = OpponentMark + new Vector3(0, 0, 0.75f * MathF.Max(0, local - 0.6f));
            op = op with { Position = opPos, Yaw = MathHelper.Lerp(FaceAv, 0, turn) };
            var behind = opPos + new Vector3(-0.45f, 0, -1.3f);
            av = av with { Position = Vector3.Lerp(AvatarMark, behind, Math.Clamp(local / 1.2f, 0, 1)), Yaw = MathHelper.Lerp(FaceOpp, 0.15f, turn) };
        }

        float pb = 0, ob = 0;
        int bannerIdx = IndexOf(Beat.Banner);
        if (bannerIdx >= 0 && i >= bannerIdx)
        {
            float raise = i == bannerIdx ? Math.Clamp(u / 0.45f, 0, 1) : 1;
            if (Plan.Banner is BannerSide.Player or BannerSide.Both) pb = raise;
            if (Plan.Banner is BannerSide.Opponent or BannerSide.Both) ob = raise;
        }
        var cam = CameraFor(shot, u, av.Position, op.Position);
        return new CineFrame(i, shot.Beat, u, cam, av, op, pb, ob, shot.Beat == Beat.WarRecordCard);
    }

    int IndexOf(Beat b) { for (int k = 0; k < Plan.Shots.Count; k++) if (Plan.Shots[k].Beat == b) return k; return -1; }

    static CharacterFrame Character(string clip, float local, float dur, Vector3 mark, float yaw) =>
        clip == "" ? new CharacterFrame(false, "", Pose.Rest(), mark, yaw) : new CharacterFrame(true, clip, CineClips.Sample(clip, local, dur), mark, yaw);

    static readonly float[] FramingScale = { 1.0f, 0.82f, 1.25f };

    /// <summary>Camera choreography per (beat, move): splines/orbits/dollies around the staged subjects.</summary>
    CameraPose CameraFor(PlannedShot s, float u, Vector3 A, Vector3 O)
    {
        float k = FramingScale[Math.Clamp(s.Framing, 0, 2)]; float z = s.CameraSide;
        var M = Mid; var head = new Vector3(0, 1.55f, 0);
        float e = Ease.InOutSine(u);
        Vector3 L(Vector3 a, Vector3 b) => Vector3.Lerp(a, b, e);
        CameraPose Orbit(Vector3 c, float r, float h, float a0, float a1, Vector3 look, float fov = 0.7f)
        {
            float a = MathHelper.Lerp(a0, a1, e);
            return new CameraPose(c + new Vector3(MathF.Sin(a) * r, h, MathF.Cos(a) * r * z), look, fov);
        }
        switch (s.Beat)
        {
            case Beat.Aftermath:
                return s.Move == CameraMove.Crane
                    ? new CameraPose(L(M + new Vector3(-12, 9, 14 * z), M + new Vector3(3, 3.2f, 9 * z) * k), L(M + new Vector3(-4, 0, -4), M + new Vector3(4, 0.5f, -2)), 0.8f)
                    : new CameraPose(L(M + new Vector3(-16, 2.2f, 8 * z), M + new Vector3(-3, 2.0f, 7 * z)), L(M + new Vector3(-8, 0.4f, -3), M + new Vector3(4, 0.4f, -4)), 0.75f);
            case Beat.AvatarIntro:
                return s.Move switch
                {
                    CameraMove.Orbit => Orbit(A, 2.8f * k, 1.55f, 0.6f, 1.3f, A + head, 0.6f),
                    CameraMove.LowAngle => new CameraPose(A + new Vector3(2.1f * k, 0.55f, 1.7f * z * k), A + head + new Vector3(0, 0.1f, 0), 0.65f),
                    _ => new CameraPose(A + L(new Vector3(3.4f, 1.6f, 2.4f * z), new Vector3(2.0f, 1.6f, 1.4f * z)) * new Vector3(k, 1, k), A + head, 0.6f),
                };
            case Beat.OpponentIntro:
                return s.Move switch
                {
                    CameraMove.Orbit => Orbit(O, 2.6f * k, 1.55f, -0.6f, -1.2f, O + head, 0.6f),
                    CameraMove.OverShoulder => new CameraPose(A + new Vector3(-1.2f, 1.85f, 1.0f * z), O + head - new Vector3(0, 0.1f, 0), 0.5f),
                    _ => new CameraPose(O + L(new Vector3(-3.2f, 1.6f, 2.3f * z), new Vector3(-1.9f, 1.6f, 1.4f * z)) * new Vector3(k, 1, k), O + head, 0.6f),
                };
            case Beat.Confrontation:
                return s.Move switch
                {
                    CameraMove.Orbit => Orbit(M, 4.6f * k, 1.6f, -0.35f, 0.35f, M + new Vector3(0, 1.25f, 0)),
                    CameraMove.Static => new CameraPose(M + new Vector3(-1.0f, 1.6f, 4.8f * z * k), M + new Vector3(0, 1.2f, 0), 0.7f),
                    _ => new CameraPose(M + L(new Vector3(0, 1.7f, 5.6f * z), new Vector3(0, 1.6f, 4.2f * z)) * new Vector3(1, 1, k), M + new Vector3(0, 1.25f, 0), 0.7f),
                };
            case Beat.OutcomeBeat:
            {
                if (Plan.OutcomeBeat == OutcomeBeatKind.AftermathCut)
                    return new CameraPose(L(M + new Vector3(-8, 7, 12 * z), M + new Vector3(-4, 4, 9 * z)), M + new Vector3(6, 0, -5), 0.8f);
                var focus = Vector3.Lerp(A, O, 0.5f) + new Vector3(0, 1.1f, 0);
                if (Plan.OutcomeBeat == OutcomeBeatKind.Capture) focus = Vector3.Lerp(A, O, 0.5f) + new Vector3(0, 1.1f, 0);
                return s.Move switch
                {
                    CameraMove.LowAngle => new CameraPose(focus + new Vector3(0.6f, -0.6f, 3.4f * z * k), focus + new Vector3(0, 0.3f, 0), 0.75f),
                    CameraMove.OverShoulder => new CameraPose(A + new Vector3(-1.6f, 1.95f, 1.35f * z), O + new Vector3(0, Plan.OutcomeBeat is OutcomeBeatKind.Submission or OutcomeBeatKind.Execution ? 0.8f : 1.2f, 0), 0.6f),
                    CameraMove.Orbit => Orbit(focus - new Vector3(0, 1.1f, 0), 3.9f * k, 1.5f, 0.25f, -0.25f, focus),
                    _ => new CameraPose(focus + L(new Vector3(0.3f, 0.5f, 4.4f * z), new Vector3(0.1f, 0.3f, 3.2f * z)) * new Vector3(1, 1, k), focus, 0.7f),
                };
            }
            case Beat.Banner:
            {
                var bp = Plan.Banner == BannerSide.Opponent ? OpponentBannerPos : Plan.Banner == BannerSide.Both ? Mid + new Vector3(0, 0, -1.8f) : PlayerBannerPos;
                var look = bp + new Vector3(0.6f, 2.9f, 0);
                return s.Move == CameraMove.Orbit
                    ? Orbit(bp, 5.0f * k, 1.4f, -0.5f, 0.5f, look, 0.7f)
                    : new CameraPose(bp + L(new Vector3(2.6f, 0.4f, 3.2f * z), new Vector3(2.0f, 0.5f, 2.6f * z)) * new Vector3(k, 1, k), look, 0.75f);
            }
            default: // WarRecordCard
                return s.Move == CameraMove.Crane
                    ? new CameraPose(L(M + new Vector3(0, 2.5f, 8 * z), M + new Vector3(0, 5, 10 * z)), M + new Vector3(0, 1, 0), 0.7f)
                    : new CameraPose(M + new Vector3(-1, 3.2f, 9 * z), M + new Vector3(0, 1, 0), 0.7f);
        }
    }

    /// <summary>Fog (density, falloff, base, max) + exposure for the plan's presentation weather.</summary>
    public static (Vector4 fog, float exposure) WeatherLook(Weather w) => w switch
    {
        Weather.Haze => (new Vector4(0.006f, 0.05f, 0, 0.8f), 0.85f),
        Weather.Overcast => (new Vector4(0.004f, 0.05f, 0, 0.85f), 0.68f),
        Weather.Mist => (new Vector4(0.03f, 0.25f, 0, 0.92f), 0.8f),
        _ => (new Vector4(0.0015f, 0.05f, 0, 0.6f), 0.85f),
    };
}
