using Gfx.Chars.Animation;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Anim;

/// <summary>Body dimensions of one skeleton used to scale authored keys (rest pose, metres).</summary>
public sealed record Dims(float Ankle, float Hip, float Shoulder, float ShoulderX, float Pelvis, float ArmReach, Vector3 FootL, Vector3 FootR)
{
    public static Dims Of(Skeleton s)
    {
        var fl = s.Head[s["foot_l"]]; var fr = s.Head[s["foot_r"]];
        var u = s.Head[s["upperarm_l"]]; var l = s.Head[s["lowerarm_l"]]; var h = s.Head[s["hand_l"]];
        return new Dims(fl.Y, s.Head[s["thigh_l"]].Y, u.Y, u.X, s.Head[s["pelvis"]].Y, (l - u).Length() + (h - l).Length() + 0.07f, fl, fr);
    }
}

/// <summary>
/// The combat clips of stage kit, authored in code as keyed body poses (no weapon motion capture exists for this
/// project): guards for the spear-and-aspis hoplite, the sword-and-shield fighter and the archer; the overhand spear
/// thrust, the overhead and the forehand side cuts, the shield block, the bow's draw, hold and loose, a heavy hit
/// reaction, two deaths (falling back, collapsing forward), surrender (weapons dropped, kneeling, hands raised) and
/// victory (weapon raised, shouting). Every clip is scaled to the skeleton it is baked for (<see cref="Dims"/>), so the
/// same authored motion fits every identity. Right-handed; the shield on the left arm. Clip names start "kit.".
/// </summary>
public static class CombatClips
{
    static Vector3 N(float x, float y, float z) => Vector3.Normalize(new Vector3(x, y, z));

    // ---------------------------------------------------------------- shared poses

    /// <summary>The hoplite's guard: left shoulder forward, aspis covering chin to knee, spear overhand above the shoulder.</summary>
    static Key HopliteGuard(Dims d, bool central = false, float t = 0)
    {
        return new Key
        {
            T = t,
            Pelvis = new Vector3(0.02f, -0.06f, 0.03f), PelvisYaw = -22, SpineYaw = -8, SpinePitch = 7, HeadYaw = 26, HeadPitch = 2,
            FL = new FootKey(new Vector3(0.12f, d.Ankle, 0.30f), -12), FR = new FootKey(new Vector3(-0.17f, d.Ankle, -0.20f), 32),
            L = central ? CentralLeft(d) : new HandKey(new Vector3(0.00f, d.Shoulder - 0.24f, 0.36f), Vector3.UnitY, -Vector3.UnitZ, 1f, N(1f, -0.4f, 0.1f)),
            R = new HandKey(new Vector3(-0.21f, d.Shoulder + 0.11f, 0.04f), N(0f, 0.14f, -1f), N(0.3f, -1f, 0f), 1f, N(-1f, 0.1f, -0.7f)),
        };
    }

    /// <summary>
    /// A central-grip shield held out in front: the fist on the horizontal bar, palm down, knuckles into the boss (the
    /// board then turns about the bar to face forward, see KitHold).
    /// </summary>
    static HandKey CentralLeft(Dims d) => new(new Vector3(0.10f, d.Hip + 0.26f, 0.36f), -Vector3.UnitX, -Vector3.UnitY, 1f, N(1f, -0.6f, -0.3f));

    /// <summary>The sword-and-shield guard: sword forward at waist height, point up and forward; shield forward.</summary>
    static Key SwordGuard(Dims d, bool centralGrip, float t = 0)
    {
        var k = new Key
        {
            T = t,
            Pelvis = new Vector3(0.0f, -0.07f, 0.02f), PelvisYaw = -15, SpineYaw = -6, SpinePitch = 8, HeadYaw = 18,
            FL = new FootKey(new Vector3(0.13f, d.Ankle, 0.27f), -10), FR = new FootKey(new Vector3(-0.17f, d.Ankle, -0.18f), 30),
            R = new HandKey(new Vector3(-0.17f, d.Hip + 0.10f, 0.30f), N(0.10f, 0.75f, 0.65f), N(0.9f, 0f, -0.2f), 1f, N(-1f, -0.4f, -0.3f)),
        };
        k.L = centralGrip ? CentralLeft(d)
            : new HandKey(new Vector3(0.02f, d.Shoulder - 0.26f, 0.35f), Vector3.UnitY, -Vector3.UnitZ, 1f, N(1f, -0.4f, 0.1f));
        return k;
    }

    // ---------------------------------------------------------------- clips

    public static Authored HopliteGuardLoop(Dims d, bool central = false)
    {
        var a = new Authored { Name = "kit.hoplite.guard" + (central ? ".c" : ""), Loop = true, Note = "spear guard: shield up, spear overhand, breathing" };
        var g = HopliteGuard(d, central);
        a.Add(g);
        a.Then(1.0f, Ease.InOut, k => { k.Pelvis.Y -= 0.008f; k.SpinePitch += 1.5f; k.R = k.R! with { Pos = k.R.Pos + new Vector3(0, -0.012f, 0) }; });
        a.Then(2.0f, Ease.InOut, k => { k.Pelvis = g.Pelvis; k.SpinePitch = g.SpinePitch; k.R = g.R; });
        return a;
    }

    public static Authored HopliteThrust(Dims d, bool central = false)
    {
        var a = new Authored { Name = "kit.hoplite.thrust" + (central ? ".c" : ""), Note = "overhand spear thrust over the shield rim, stepping in" };
        var g = HopliteGuard(d, central);
        a.Add(g);
        // wind up: the spear drawn back, hips turned away
        a.Then(0.35f, Ease.InOut, k =>
        {
            k.PelvisYaw = -32; k.SpineYaw = -14; k.Pelvis = new Vector3(0.0f, -0.07f, -0.02f);
            k.R = k.R! with { Pos = new Vector3(-0.24f, d.Shoulder + 0.15f, -0.12f), Dir = N(0f, 0.12f, -1f) };
        });
        // strike: hips and shoulders drive round, the arm extends, the front foot steps in
        a.Then(0.56f, Ease.Snap, k =>
        {
            k.PelvisYaw = -6; k.SpineYaw = 6; k.SpinePitch = 13; k.HeadPitch = 6; k.HeadYaw = 10;
            k.Pelvis = new Vector3(0.03f, -0.10f, 0.12f);
            k.FL = new FootKey(new Vector3(0.12f, d.Ankle, 0.44f), -10);
            k.R = k.R! with { Pos = new Vector3(-0.10f, d.Shoulder - 0.02f, 0.10f + d.ArmReach * 0.9f), Dir = N(0f, 0.20f, -1f) };
            k.L = k.L! with { Pos = k.L.Pos + new Vector3(0.03f, -0.02f, 0.06f) };
            k.Expression = "battlecry";
        });
        a.Then(0.74f, Ease.Out, k => { k.R = k.R! with { Pos = k.R.Pos + new Vector3(0, 0.01f, -0.04f) }; });
        a.Then(1.30f, Ease.InOut, k => { var h = HopliteGuard(d, central); Copy(h, k); });
        a.Events.Add((0.56f, "impact"));
        return a;
    }

    public static Authored SwordGuardLoop(Dims d, bool central)
    {
        var a = new Authored { Name = central ? "kit.sword.guard.c" : "kit.sword.guard", Loop = true, Note = "sword-and-shield guard, breathing" };
        var g = SwordGuard(d, central);
        a.Add(g);
        a.Then(1.1f, Ease.InOut, k => { k.Pelvis.Y -= 0.008f; k.SpinePitch += 1.5f; });
        a.Then(2.2f, Ease.InOut, k => { k.Pelvis = g.Pelvis; k.SpinePitch = g.SpinePitch; });
        return a;
    }

    public static Authored OverheadCut(Dims d, bool central)
    {
        var a = new Authored { Name = central ? "kit.sword.cut_over.c" : "kit.sword.cut_over", Note = "overhead cut: the sword raised behind the head, driven down and through" };
        var g = SwordGuard(d, central);
        a.Add(g);
        a.Then(0.45f, Ease.InOut, k =>
        {
            k.PelvisYaw = -28; k.SpineYaw = -10; k.SpinePitch = -4; k.HeadPitch = -4;
            k.Pelvis = new Vector3(0, -0.05f, -0.02f);
            k.R = new HandKey(new Vector3(-0.12f, d.Shoulder + 0.40f, -0.04f), N(0.1f, 0.35f, -1f), N(0.9f, 0f, 0.3f), 1f, N(-1f, 0.6f, -0.2f));
        });
        a.Then(0.74f, Ease.Snap, k =>
        {
            k.PelvisYaw = 2; k.SpineYaw = 8; k.SpinePitch = 22; k.HeadPitch = 10;
            k.Pelvis = new Vector3(0.02f, -0.11f, 0.10f);
            k.FL = new FootKey(new Vector3(0.13f, d.Ankle, 0.40f), -8);
            k.R = new HandKey(new Vector3(-0.04f, d.Hip + 0.08f, 0.46f), N(0.05f, -0.45f, 1f), N(0.9f, 0.1f, 0.2f), 1f, N(-1f, -0.2f, 0.2f));
            k.Expression = "battlecry";
        });
        a.Then(0.90f, Ease.Out, k => { k.R = k.R! with { Pos = new Vector3(0.10f, d.Hip - 0.04f, 0.34f), Dir = N(0.4f, -0.7f, 0.6f) }; k.SpinePitch = 25; });
        a.Then(1.45f, Ease.InOut, k => { var h = SwordGuard(d, central); Copy(h, k); });
        a.Events.Add((0.74f, "impact"));
        return a;
    }

    public static Authored SideCut(Dims d, bool central)
    {
        var a = new Authored { Name = central ? "kit.sword.cut_side.c" : "kit.sword.cut_side", Note = "forehand cut: wound back at the right shoulder, swept across" };
        var g = SwordGuard(d, central);
        a.Add(g);
        a.Then(0.40f, Ease.InOut, k =>
        {
            k.PelvisYaw = -38; k.SpineYaw = -16; k.SpinePitch = 6;
            k.R = new HandKey(new Vector3(-0.30f, d.Shoulder + 0.02f, -0.06f), N(0.5f, 0.35f, -0.8f), N(0.2f, -0.9f, 0.2f), 1f, N(-1f, -0.3f, -0.5f));
        });
        a.Then(0.64f, Ease.Snap, k =>
        {
            k.PelvisYaw = 12; k.SpineYaw = 16; k.SpinePitch = 12; k.HeadYaw = 6;
            k.Pelvis = new Vector3(0.03f, -0.10f, 0.07f);
            k.R = new HandKey(new Vector3(0.22f, d.Shoulder - 0.22f, 0.40f), N(1f, 0.10f, 0.25f), N(0.1f, -1f, 0.1f), 1f, N(-0.6f, -0.6f, 0.2f));
            k.Expression = "battlecry";
        });
        a.Then(0.80f, Ease.Out, k => { k.R = k.R! with { Pos = new Vector3(0.30f, d.Shoulder - 0.26f, 0.28f), Dir = N(1f, 0.0f, -0.2f) }; });
        a.Then(1.35f, Ease.InOut, k => { var h = SwordGuard(d, central); Copy(h, k); });
        a.Events.Add((0.64f, "impact"));
        return a;
    }

    public static Authored ShieldBlock(Dims d, string weapon, bool central)
    {
        var a = new Authored { Name = $"kit.{weapon}.block" + (central ? ".c" : ""), Note = "shield block: the shield punched up over the head, braced, recovering" };
        var g = weapon == "hoplite" ? HopliteGuard(d, central) : SwordGuard(d, central);
        a.Add(g);
        a.Then(0.22f, Ease.Snap, k =>
        {
            k.Pelvis = g.Pelvis + new Vector3(0, -0.07f, -0.05f); k.SpinePitch = g.SpinePitch + 10; k.HeadPitch = 14;
            k.FR = new FootKey(g.FR!.Pos + new Vector3(0, 0, -0.08f), g.FR.Yaw);
            k.L = k.L! with { Pos = central ? new Vector3(0.06f, d.Shoulder + 0.04f, 0.40f) : new Vector3(0.0f, d.Shoulder + 0.02f, 0.38f) };
            k.Expression = "grim";
        });
        a.Then(0.62f, Ease.InOut, k => { });
        a.Then(0.68f, Ease.Out, k => { k.Pelvis += new Vector3(0, -0.01f, -0.04f); k.L = k.L! with { Pos = k.L.Pos + new Vector3(0, 0, -0.04f) }; });
        a.Then(1.20f, Ease.InOut, k => { Copy(g, k); k.Expression = null; });
        a.Events.Add((0.66f, "impact"));
        return a;
    }

    public static Authored BowShoot(Dims d)
    {
        var a = new Authored { Name = "kit.bow.shoot", Note = "archer: bow raised, string drawn to the jaw, held, loosed, lowered" };
        // side-on stance: left shoulder to the target (+z), feet across the line
        var rest = new Key
        {
            T = 0, Pelvis = new Vector3(0, -0.03f, 0), PelvisYaw = -75, SpineYaw = -5, HeadYaw = 70,
            FL = new FootKey(new Vector3(0.06f, d.Ankle, 0.22f), -70), FR = new FootKey(new Vector3(-0.04f, d.Ankle, -0.20f), -80),
            L = new HandKey(new Vector3(0.05f, d.Hip + 0.02f, 0.30f), N(0, 1, 0.15f), N(-0.3f, 0, 1f), 1f, N(1f, -0.6f, -0.2f)),
            R = new HandKey(new Vector3(0.02f, d.Hip + 0.06f, 0.22f), N(0, 1, 0), N(1f, 0, 0), 1f, N(-1f, -0.6f, -0.3f)),
        };
        a.Add(rest);
        // raise: bow arm towards the target, the right hand at the string by the bow
        a.Then(0.55f, Ease.InOut, k =>
        {
            k.L = new HandKey(new Vector3(0.03f, d.Shoulder - 0.03f, 0.08f + d.ArmReach * 0.95f), N(0, 1, 0.08f), N(-0.3f, 0, 1f), 1f, N(1f, -0.3f, -0.4f));
            k.R = new HandKey(new Vector3(0.00f, d.Shoulder - 0.04f, 0.50f), N(0, 1, 0), N(0.2f, 0, 1f), 1f, N(-1f, -0.2f, -0.4f));
            k.SpineRoll = -4;
        });
        // draw to the anchor under the jaw
        a.Then(1.25f, Ease.InOut, k =>
        {
            k.R = new HandKey(new Vector3(-0.02f, d.Shoulder + 0.08f, 0.06f), N(0, 1, 0.1f), N(0.3f, 0, 1f), 1f, N(-1f, 0.2f, -0.8f));
            k.SpinePitch = -3; k.Expression = "grim";
        });
        a.Then(1.62f, Ease.Linear, k => { k.R = k.R! with { Pos = k.R.Pos + new Vector3(0, 0, -0.01f) }; });
        // loose: the drawing hand flies back a little, the bow hand drops forward
        a.Then(1.72f, Ease.Snap, k =>
        {
            k.R = k.R! with { Pos = new Vector3(-0.06f, d.Shoulder + 0.10f, -0.06f), Palm = N(0.5f, 0, 1f) };
            k.L = k.L! with { Pos = k.L.Pos + new Vector3(0, -0.03f, 0.03f) };
            k.Expression = null;
        });
        a.Then(2.5f, Ease.InOut, k => { k.L = rest.L; k.R = rest.R; k.SpineRoll = 0; k.SpinePitch = 0; });
        a.Events.Add((1.66f, "loose"));
        return a;
    }

    public static Authored HitFront(Dims d, string weapon, bool central)
    {
        var a = new Authored { Name = $"kit.{weapon}.hit" + (central ? ".c" : ""), Note = "heavy hit from the front: thrown back a step, recovering" };
        var g = weapon == "hoplite" ? HopliteGuard(d, central) : SwordGuard(d, central);
        a.Add(g);
        a.Then(0.12f, Ease.Snap, k =>
        {
            k.Pelvis = g.Pelvis + new Vector3(0, -0.02f, -0.10f); k.PelvisPitch = -6; k.SpinePitch = -14; k.HeadPitch = -22; k.SpineYaw = g.SpineYaw - 10;
            k.R = k.R! with { Pos = k.R.Pos + new Vector3(-0.05f, 0.06f, -0.10f) };
            k.L = k.L! with { Pos = k.L.Pos + new Vector3(0.04f, 0.05f, -0.08f) };
            k.Expression = "pain";
        });
        a.Then(0.34f, Ease.Out, k => { k.FR = new FootKey(g.FR!.Pos + new Vector3(0, 0, -0.16f), g.FR.Yaw); k.Pelvis = g.Pelvis + new Vector3(0, -0.06f, -0.14f); });
        a.Then(1.00f, Ease.InOut, k => { Copy(g, k); k.FR = new FootKey(g.FR!.Pos + new Vector3(0, 0, -0.16f), g.FR.Yaw); k.Expression = "grim"; });
        return a;
    }

    public static Authored DeathBack(Dims d, string weapon, bool central)
    {
        var a = new Authored { Name = $"kit.{weapon}.death_back" + (central ? ".c" : ""), Note = "death: struck, knees give, falls on the back, arms thrown out" };
        var g = weapon == "hoplite" ? HopliteGuard(d, central) : weapon == "bow" ? BowShoot(d).Keys[0] : SwordGuard(d, central);
        a.Add(g);
        a.Then(0.14f, Ease.Snap, k => { k.SpinePitch = -18; k.HeadPitch = -25; k.Pelvis = g.Pelvis + new Vector3(0, -0.02f, -0.06f); k.Expression = "pain"; });
        a.Then(0.55f, Ease.InOut, k =>
        {
            k.Pelvis = g.Pelvis + new Vector3(0, -0.30f, -0.12f); k.PelvisPitch = -12; k.SpinePitch = -10;
            k.R = k.R! with { Weight = 0.3f }; k.L = k.L! with { Weight = 0.3f };
            k.ArmR = 20; k.ArmL = 20;
        });
        a.Then(1.15f, Ease.In, k =>
        {
            k.Pelvis = new Vector3(0.02f, -d.Pelvis + 0.11f, -0.42f); k.PelvisPitch = -86; k.PelvisYaw = g.PelvisYaw * 0.5f; k.SpinePitch = -4; k.HeadPitch = 8; k.HeadYaw = 25;
            k.FL = k.FL! with { Weight = 0f }; k.FR = k.FR! with { Weight = 0f };
            // lying on the back the legs stay nearly straight (a bent knee would push the heel into the ground)
            k.KneeL = 4; k.KneeR = 2; k.ArmR = 70; k.ArmL = 55;
            k.R = k.R! with { Weight = 0f }; k.L = k.L! with { Weight = 0f };
        });
        a.Then(1.35f, Ease.Out, k => { k.Pelvis += new Vector3(0, 0.02f, 0); k.SpinePitch = -2; });
        a.Then(2.2f, Ease.InOut, k => { k.Pelvis += new Vector3(0, -0.02f, 0); k.HeadYaw = 35; k.Expression = "calm"; });
        a.Events.Add((0.30f, "release_r"));
        return a;
    }

    public static Authored DeathForward(Dims d, string weapon, bool central)
    {
        var a = new Authored { Name = $"kit.{weapon}.death_fwd" + (central ? ".c" : ""), Note = "death: doubles over, drops to the knees, falls forward" };
        var g = weapon == "hoplite" ? HopliteGuard(d, central) : SwordGuard(d, central);
        a.Add(g);
        a.Then(0.30f, Ease.Snap, k => { k.SpinePitch = 32; k.HeadPitch = 20; k.Pelvis = g.Pelvis + new Vector3(0, -0.06f, -0.04f); k.Expression = "pain"; k.R = k.R! with { Weight = 0.4f }; });
        a.Then(0.95f, Ease.In, k =>
        {
            // on the knees: the shins on the ground
            k.Pelvis = new Vector3(0.0f, -d.Pelvis + d.Hip * 0.58f, 0.10f); k.PelvisPitch = 12; k.SpinePitch = 28;
            k.FL = k.FL! with { Weight = 0f }; k.FR = k.FR! with { Weight = 0f }; k.KneeL = 95; k.KneeR = 95;
            k.R = k.R! with { Weight = 0f }; k.L = k.L! with { Weight = 0.0f }; k.ArmR = 10; k.ArmL = 10;
        });
        a.Then(1.55f, Ease.In, k => { k.Pelvis = new Vector3(0.0f, -d.Pelvis + 0.14f, 0.55f); k.PelvisPitch = 82; k.SpinePitch = 6; k.PelvisYaw = 0; k.SpineYaw = 0; k.HeadYaw = 50; k.HeadPitch = 0; k.KneeL = 20; k.KneeR = 35; k.ArmR = -10; k.ArmL = -10;
            // the arms along the body, the backs of the hands on the ground beside the thighs (palms up, thumbs in)
            k.L = new HandKey(new Vector3(0.27f, 0.045f, 0.42f), N(-1f, 0, 0), N(0, 1f, 0), 1f);
            k.R = new HandKey(new Vector3(-0.27f, 0.045f, 0.42f), N(1f, 0, 0), N(0, 1f, 0), 1f); });
        a.Then(2.2f, Ease.Out, k => { k.Expression = "calm"; });
        a.Events.Add((0.40f, "release_r"));
        return a;
    }

    public static Authored Surrender(Dims d, string weapon, bool central)
    {
        var a = new Authored { Name = $"kit.{weapon}.surrender" + (central ? ".c" : ""), Note = "surrender: weapons dropped, kneels, raises open hands" };
        var g = weapon == "hoplite" ? HopliteGuard(d, central) : weapon == "bow" ? BowShoot(d).Keys[0] : SwordGuard(d, central);
        a.Add(g);
        a.Then(0.45f, Ease.InOut, k =>
        {
            k.PelvisYaw = 0; k.SpineYaw = 0; k.HeadYaw = 0; k.SpinePitch = 4;
            k.R = new HandKey(new Vector3(-0.22f, d.Hip - 0.02f, 0.12f), N(0, 0.2f, 1f), N(1f, 0, 0), 0.6f);
            k.L = new HandKey(new Vector3(0.22f, d.Hip - 0.02f, 0.12f), N(0, 0.2f, 1f), N(-1f, 0, 0), 0.6f);
        });
        // down on the right knee, the left foot forward
        a.Then(1.35f, Ease.InOut, k =>
        {
            k.Pelvis = new Vector3(0, -0.42f, -0.05f); k.SpinePitch = 8;
            k.FL = new FootKey(new Vector3(0.13f, d.Ankle, 0.30f), -5);
            k.FR = new FootKey(new Vector3(-0.12f, d.Ankle + 0.03f, -0.38f), 10, 55);
            k.R = k.R! with { Weight = 0f }; k.L = k.L! with { Weight = 0f }; k.ArmR = 12; k.ArmL = 12;
        });
        // hands up, palms forward
        a.Then(2.1f, Ease.InOut, k =>
        {
            k.R = new HandKey(new Vector3(-0.27f, d.Shoulder - 0.36f + 0.30f, 0.16f), N(-0.2f, 1f, 0), N(0, 0, 1f), 1f, N(-1f, -0.6f, 0f));
            k.L = new HandKey(new Vector3(0.27f, d.Shoulder - 0.36f + 0.30f, 0.16f), N(0.2f, 1f, 0), N(0, 0, 1f), 1f, N(1f, -0.6f, 0f));
            k.HeadPitch = 12; k.Expression = "pain";
        });
        a.Then(3.0f, Ease.InOut, k => { k.HeadPitch = 16; });
        a.Events.Add((0.30f, "release_r"));
        a.Events.Add((0.40f, "release_l"));
        return a;
    }

    public static Authored Victory(Dims d, string weapon, bool central)
    {
        var a = new Authored { Name = $"kit.{weapon}.victory" + (central ? ".c" : ""), Note = "victory: weapon raised high, shouting, shaken twice" };
        var g = weapon == "hoplite" ? HopliteGuard(d, central) : SwordGuard(d, central);
        a.Add(g);
        var up = weapon == "hoplite" ? N(0f, -1f, 0.15f) : N(0f, 1f, 0.15f);   // the spear is held overhand: its head goes up from the little finger
        a.Then(0.55f, Ease.InOut, k =>
        {
            k.PelvisYaw = -5; k.SpineYaw = 0; k.SpinePitch = -6; k.HeadPitch = -14; k.HeadYaw = 0;
            k.R = new HandKey(new Vector3(-0.17f, d.Shoulder + d.ArmReach * 0.82f, 0.08f), up, N(1f, 0, 0.3f), 1f, N(-1f, 0.2f, 0f));
            k.Expression = "battlecry";
        });
        a.Then(0.85f, Ease.InOut, k => { k.R = k.R! with { Pos = k.R.Pos + new Vector3(0, -0.10f, 0.02f) }; });
        a.Then(1.10f, Ease.Snap, k => { k.R = k.R! with { Pos = k.R.Pos + new Vector3(0, 0.10f, -0.02f) }; });
        a.Then(1.40f, Ease.InOut, k => { k.R = k.R! with { Pos = k.R.Pos + new Vector3(0, -0.10f, 0.02f) }; });
        a.Then(1.65f, Ease.Snap, k => { k.R = k.R! with { Pos = k.R.Pos + new Vector3(0, 0.10f, -0.02f) }; });
        a.Then(2.5f, Ease.InOut, k => { k.Expression = "calm"; });
        return a;
    }

    static void Copy(Key from, Key to)
    {
        to.Pelvis = from.Pelvis; to.PelvisYaw = from.PelvisYaw; to.PelvisPitch = from.PelvisPitch; to.PelvisRoll = from.PelvisRoll;
        to.SpineYaw = from.SpineYaw; to.SpinePitch = from.SpinePitch; to.SpineRoll = from.SpineRoll; to.HeadYaw = from.HeadYaw; to.HeadPitch = from.HeadPitch;
        to.FL = from.FL; to.FR = from.FR; to.L = from.L; to.R = from.R; to.ArmL = from.ArmL; to.ArmR = from.ArmR; to.KneeL = from.KneeL; to.KneeR = from.KneeR;
        to.Expression = null;
    }

    /// <summary>Every authored clip for one skeleton (hoplite set, sword set for strap and central-grip shields, archer).</summary>
    public static List<Authored> All(Dims d)
    {
        var l = new List<Authored> { BowShoot(d), DeathBack(d, "bow", false), Surrender(d, "bow", false) };
        foreach (bool c in new[] { false, true })
        {
            l.AddRange(new[] { HopliteGuardLoop(d, c), HopliteThrust(d, c), ShieldBlock(d, "hoplite", c), HitFront(d, "hoplite", c),
                DeathBack(d, "hoplite", c), DeathForward(d, "hoplite", c), Surrender(d, "hoplite", c), Victory(d, "hoplite", c) });
            l.AddRange(new[] { SwordGuardLoop(d, c), OverheadCut(d, c), SideCut(d, c), ShieldBlock(d, "sword", c), HitFront(d, "sword", c),
                DeathBack(d, "sword", c), DeathForward(d, "sword", c), Surrender(d, "sword", c), Victory(d, "sword", c) });
        }
        return l;
    }

    /// <summary>Bakes every authored clip for an identity and adds them to its clip file (once).</summary>
    public static IReadOnlyList<Authored> Install(Gfx.Chars.Rendering.CharAsset identity)
    {
        var s = identity.Skeleton;
        var d = Dims.Of(s);
        var sockets = Rig.KitSockets.For(identity);
        var all = All(d);
        foreach (var a in all)
            if (identity.ClipFile.Clip(a.Name) == null)
                identity.ClipFile.Clips.Add(ClipBaker.Bake(a, s, sockets));
        return all;
    }
}
