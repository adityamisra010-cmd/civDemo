using Microsoft.Xna.Framework;

namespace Gfx.Units;

/// <summary>
/// Canonical humanoid skeleton (20 joints). Units: metres, Y up, the soldier FACES +Z (its left side is +X).
/// Bind pose = arms hanging, legs straight. Joint transforms use the XNA row-vector convention:
/// <c>Model[j] = LocalRot[j] · T(bind[j] − bind[parent]) · Model[parent]</c>; root: <c>LocalRot[0] · T(rootOffset)</c>
/// (the root sits on the ground between the feet, so root rotations pivot at the feet, e.g. falling).
/// Skin matrix = <c>T(−bind[j]) · Model[j]</c> (inverse bind is a pure translation because the bind pose has no rotation).
/// </summary>
public static class Skeleton
{
    public const int Root = 0, Pelvis = 1, Spine = 2, Chest = 3, Neck = 4, Head = 5,
        LClav = 6, LUpperArm = 7, LForearm = 8, LHand = 9,
        RClav = 10, RUpperArm = 11, RForearm = 12, RHand = 13,
        LThigh = 14, LShin = 15, LFoot = 16,
        RThigh = 17, RShin = 18, RFoot = 19;
    public const int JointCount = 20;

    public static readonly string[] Names =
    {
        "Root", "Pelvis", "Spine", "Chest", "Neck", "Head",
        "L_Clavicle", "L_UpperArm", "L_Forearm", "L_Hand",
        "R_Clavicle", "R_UpperArm", "R_Forearm", "R_Hand",
        "L_Thigh", "L_Shin", "L_Foot", "R_Thigh", "R_Shin", "R_Foot",
    };

    /// <summary>Parent index (−1 for the root). Parents always precede children.</summary>
    public static readonly int[] Parent = { -1, 0, 1, 2, 3, 4, 3, 6, 7, 8, 3, 10, 11, 12, 1, 14, 15, 1, 17, 18 };

    /// <summary>Bind-pose joint positions, model space (m).</summary>
    public static readonly Vector3[] Bind =
    {
        new(0, 0, 0), new(0, 0.95f, 0), new(0, 1.10f, 0), new(0, 1.30f, 0), new(0, 1.50f, 0), new(0, 1.60f, 0),
        new(0.05f, 1.45f, 0), new(0.20f, 1.45f, 0), new(0.20f, 1.17f, 0), new(0.20f, 0.92f, 0),
        new(-0.05f, 1.45f, 0), new(-0.20f, 1.45f, 0), new(-0.20f, 1.17f, 0), new(-0.20f, 0.92f, 0),
        new(0.10f, 0.93f, 0), new(0.10f, 0.50f, 0), new(0.10f, 0.08f, 0),
        new(-0.10f, 0.93f, 0), new(-0.10f, 0.50f, 0), new(-0.10f, 0.08f, 0),
    };

    /// <summary>Model-space joint transforms for a pose.</summary>
    public static void ModelTransforms(in Pose pose, Span<Matrix> model)
    {
        model[Root] = pose.Local[Root] * Matrix.CreateTranslation(pose.RootOffset);
        for (int j = 1; j < JointCount; j++)
        {
            int p = Parent[j];
            model[j] = pose.Local[j] * Matrix.CreateTranslation(Bind[j] - Bind[p]) * model[p];
        }
    }

    /// <summary>Skin matrices (inverse bind × model) for a pose — what the bone texture stores per row.</summary>
    public static Matrix[] SkinMatrices(in Pose pose)
    {
        Span<Matrix> model = stackalloc Matrix[JointCount];
        ModelTransforms(pose, model);
        var skin = new Matrix[JointCount];
        for (int j = 0; j < JointCount; j++) skin[j] = Matrix.CreateTranslation(-Bind[j]) * model[j];
        return skin;
    }
}

/// <summary>A skeleton pose: local rotation per joint plus root translation.</summary>
public readonly struct Pose
{
    public readonly Matrix[] Local;
    public readonly Vector3 RootOffset;
    public Pose(Matrix[] local, Vector3 rootOffset) { Local = local; RootOffset = rootOffset; }
    public static Pose Rest()
    {
        var l = new Matrix[Skeleton.JointCount];
        for (int i = 0; i < l.Length; i++) l[i] = Matrix.Identity;
        return new Pose(l, Vector3.Zero);
    }
}
