using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Gfx.Gear.Items;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Runtime;

/// <summary>
/// A drawable bow prop: the bow's mesh skinned to five bones (grip, upper and lower limb, upper and lower string half).
/// Each frame the string's nock is put where the drawing hand is; the limbs bend until each string half (fixed length)
/// reaches the nock from its tip, and the string halves are turned to meet there; after the loose the string returns
/// to brace. PRESENTATION ONLY.
/// </summary>
public sealed class BowRig
{
    public const int Root = 0, LimbU = 1, LimbL = 2, StrU = 3, StrL = 4;
    public static readonly Vector3 TipU = new(0, 0.885f, 0.065f), TipL = new(0, -0.885f, 0.065f), Brace = new(0, 0, -0.165f);
    public CharInstance Instance { get; }
    public KitItem Item { get; }

    BowRig(CharInstance inst, KitItem item) { Instance = inst; Item = item; }

    /// <summary>Builds the skinned bow asset and adds an instance to the world.</summary>
    public static BowRig Create(KitWorld world, KitItem bow)
    {
        var f = new CharacterFile();
        f.Info["id"] = "prop:" + bow.Id + ":rig";
        f.Bones.Add(new BoneDef("root", -1, Vector3.Zero, Vector3.UnitY));
        f.Bones.Add(new BoneDef("limb_u", 0, Vector3.Zero, TipU));
        f.Bones.Add(new BoneDef("limb_l", 0, Vector3.Zero, TipL));
        f.Bones.Add(new BoneDef("str_u", 1, TipU, Brace));
        f.Bones.Add(new BoneDef("str_l", 2, TipL, Brace));
        var mesh = bow.Parts[0].Mesh;
        // string vertices: the thin tube behind the stave (z < -0.02 or near the tips' string loops) by distance to the string lines
        var def = Runtime.KitGpu.ToMeshDef(mesh, "bow", "kit", 0, skin: i =>
        {
            var p = mesh.Positions[i];
            float ds = Math.Min(DistToSegment(p, TipU, Brace), DistToSegment(p, TipL, Brace));
            bool str = ds < 0.003f && (p.Z < 0.055f || Math.Abs(p.Y) < 0.86f);
            if (str) return new[] { (p.Y >= 0 ? StrU : StrL, 1f) };
            float u = Math.Clamp(Math.Abs(p.Y) / 0.18f, 0f, 1f);
            int limb = p.Y >= 0 ? LimbU : LimbL;
            return new[] { (limb, u), (Root, 1 - u) };
        });
        f.Meshes.Add(def);
        f.Info["materials"] = new System.Text.Json.Nodes.JsonObject { ["bow"] = new System.Text.Json.Nodes.JsonObject { ["kind"] = "kit", ["maps"] = "w", ["normal"] = 1.0 } };
        var inst = world.AddAsset(f, bow, 1.1f);
        return new BowRig(inst, bow);
    }

    /// <summary>String half length (brace to tip), m.</summary>
    public static float HalfString => (Brace - TipU).Length();

    /// <summary>
    /// The limb bend (radians, about the bow's X axis, the tips moving towards the archer) at which the upper string half,
    /// of fixed length, reaches the nock d (bow space) from the upper tip.
    /// </summary>
    public static float SolveBend(Vector3 d)
    {
        float lo = 0f, hi = 0.6f, half = HalfString;
        for (int it = 0; it < 30; it++)
        {
            float m = 0.5f * (lo + hi);
            var tip = Vector3.Transform(TipU, Matrix.CreateRotationX(-m));
            if ((tip - d).Length() > half) lo = m; else hi = m;
        }
        return hi;
    }

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a; float t = Math.Clamp(Vector3.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
        return (p - (a + ab * t)).Length();
    }

    /// <summary>
    /// Poses the bow for a nock position given in world space (null = at brace). The bow's world transform must be set
    /// first (Instance.World). Returns the limb bend (radians).
    /// </summary>
    public float Draw(Vector3? nockWorld)
    {
        var p = Instance.Pose;
        p.Reset();
        if (nockWorld is not { } nw) return 0f;
        var d = Vector3.Transform(nw, Matrix.Invert(Instance.World));
        if (d.Z > Brace.Z - 0.005f) return 0f;   // the hand is not behind the brace: the string is at rest
        float bend = SolveBend(d);
        p.Local[LimbU] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -bend);
        p.Local[LimbL] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, bend);
        // the string halves aim at the nock
        foreach (var (bone, limb, tipRest, sign) in new[] { (StrU, LimbU, TipU, -1f), (StrL, LimbL, TipL, 1f) })
        {
            var limbRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, sign * bend);
            var tip = Vector3.Transform(tipRest, limbRot);
            var restDir = Vector3.Normalize(Brace - tipRest);
            var want = Vector3.Normalize(d - tip);
            var global = Q.Between(restDir, want);
            p.Local[bone] = Q.Mul(Q.Inverse(limbRot), global);
        }
        return bend;
    }
}
