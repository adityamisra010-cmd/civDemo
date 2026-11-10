using Microsoft.Xna.Framework;

namespace Gfx.Chars.Animation;

/// <summary>
/// Pose edits in character space on a solved pose (global rotations + heads). After an edit the local rotations are
/// recomputed from the globals so later FK passes see the same result. PRESENTATION ONLY.
/// </summary>
public static class Ik
{
    /// <summary>Rotates bone b (and its subtree, by FK) by a character-space rotation r about its head.</summary>
    public static void RotateGlobal(Skeleton s, Pose p, Quaternion[] global, Vector3[] head, int b, Quaternion r)
    {
        int par = s.Parent[b];
        var g = Q.Mul(r, global[b]);
        p.Local[b] = par < 0 ? g : Q.Mul(Q.Inverse(global[par]), g);
        Refresh(s, p, global, head, b);
    }

    /// <summary>Re-solves globals and heads of bone b's subtree from the local rotations.</summary>
    public static void Refresh(Skeleton s, Pose p, Quaternion[] global, Vector3[] head, int b)
    {
        for (int i = b; i < s.Count; i++)
        {
            if (i != b && !IsDescendant(s, i, b)) continue;
            int par = s.Parent[i];
            if (par < 0) { global[i] = p.Local[i]; continue; }
            global[i] = Q.Mul(global[par], p.Local[i]);
            if (i != b) head[i] = head[par] + Q.Rotate(global[par], s.Head[i] - s.Head[par]);
        }
    }

    public static bool IsDescendant(Skeleton s, int i, int ancestor)
    {
        for (int k = s.Parent[i]; k >= 0; k = s.Parent[k]) if (k == ancestor) return true;
        return false;
    }

    /// <summary>
    /// Two-bone IK (upper a, lower b, end c): moves joint c's head to <paramref name="target"/> keeping the bend in
    /// the plane of the current chain (pole = current mid-joint side). End bone keeps its character-space rotation
    /// when <paramref name="keepEndRotation"/> is set. Returns the reach error (m) after solving.
    /// </summary>
    public static float TwoBone(Skeleton s, Pose p, Quaternion[] global, Vector3[] head, int a, int b, int c, Vector3 target,
        float weight = 1f, bool keepEndRotation = true, Vector3? pole = null)
    {
        if (weight <= 0) return 0;
        var endRot = global[c];
        Vector3 A = head[a], B = head[b], C = head[c];
        float l1 = (B - A).Length(), l2 = (C - B).Length();
        var T = Vector3.Lerp(C, target, weight);
        var at = T - A;
        float d = Math.Clamp(at.Length(), MathF.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
        var dir = at.LengthSquared() > 1e-12f ? Vector3.Normalize(at) : Vector3.UnitY;
        // bend plane: keep the current knee/elbow side (or the given pole)
        var poleDir = pole ?? (B - (A + Vector3.Dot(B - A, dir) * dir));
        if (poleDir.LengthSquared() < 1e-10f) poleDir = Vector3.Cross(dir, Vector3.UnitX);
        poleDir = Vector3.Normalize(poleDir - Vector3.Dot(poleDir, dir) * dir);
        float cosA = Math.Clamp((l1 * l1 + d * d - l2 * l2) / (2 * l1 * d), -1f, 1f);
        float sinA = MathF.Sqrt(1 - cosA * cosA);
        var Bn = A + dir * (cosA * l1) + poleDir * (sinA * l1);
        // rotate the upper bone so its child joint lands on Bn
        RotateGlobal(s, p, global, head, a, Q.Between(B - A, Bn - A));
        // rotate the lower bone so the end lands on T
        var Cn = A + dir * d;
        RotateGlobal(s, p, global, head, b, Q.Between(head[c] - head[b], Cn - head[b]));
        if (keepEndRotation) RotateGlobal(s, p, global, head, c, Q.Mul(endRot, Q.Inverse(global[c])));
        return (head[c] - T).Length();
    }

    /// <summary>
    /// Turns a chain of bones (weights summing to 1) so that the last bone's forward points at a target. The total turn
    /// (yaw about the vertical, pitch about the chain's side axis) is measured once from the current forward and clamped
    /// to the limits, then shared out along the chain by weight, so the head never turns further than the limits allow
    /// however far behind the target is.
    /// </summary>
    public static void LookAt(Skeleton s, Pose p, Quaternion[] global, Vector3[] head, int[] chain, float[] weights, int eyeRef,
        Vector3 forwardRest, Vector3 target, float maxYawRad, float maxPitchRad, float weight)
    {
        if (weight <= 0) return;
        var fwd = Vector3.Normalize(Q.Rotate(global[chain[^1]], forwardRest));
        var want = target - head[eyeRef];
        if (want.LengthSquared() < 1e-6f) return;
        want.Normalize();
        float yaw = MathF.IEEERemainder(MathF.Atan2(want.X, want.Z) - MathF.Atan2(fwd.X, fwd.Z), MathF.Tau);
        yaw = Math.Clamp(yaw, -maxYawRad, maxYawRad) * weight;
        float pitch = Math.Clamp(MathF.Asin(Math.Clamp(want.Y, -1f, 1f)) - MathF.Asin(Math.Clamp(fwd.Y, -1f, 1f)), -maxPitchRad, maxPitchRad) * weight;
        float total = 0; foreach (var w in weights) total += w;
        for (int k = 0; k < chain.Length; k++)
        {
            float share = weights[k] / Math.Max(total, 1e-4f);
            var f = Vector3.Normalize(Q.Rotate(global[chain[^1]], forwardRest));
            var side = Vector3.Cross(Vector3.UnitY, f);
            if (side.LengthSquared() < 1e-8f) side = Vector3.UnitX;
            side.Normalize();
            var r = Q.Mul(Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * share), Quaternion.CreateFromAxisAngle(side, -pitch * share));
            RotateGlobal(s, p, global, head, chain[k], r);
        }
    }

    static float Remaining(float[] w, int k) { float r = 0; for (int i = k; i < w.Length; i++) r += w[i]; return r; }
}
