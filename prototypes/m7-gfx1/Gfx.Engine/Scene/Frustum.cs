using Microsoft.Xna.Framework;

namespace Gfx.Engine.Scene;

/// <summary>
/// View frustum as 6 inward-facing normalised planes, extracted from an XNA (row-vector, D3D z∈[0,1]) ViewProj
/// (Gribb–Hartmann). Conservative: may keep objects slightly outside near corners, never culls a visible one.
/// </summary>
public readonly struct Frustum
{
    readonly Vector4 _l, _r, _b, _t, _n, _f;

    public Frustum(in Matrix vp)
    {
        var c1 = new Vector4(vp.M11, vp.M21, vp.M31, vp.M41);
        var c2 = new Vector4(vp.M12, vp.M22, vp.M32, vp.M42);
        var c3 = new Vector4(vp.M13, vp.M23, vp.M33, vp.M43);
        var c4 = new Vector4(vp.M14, vp.M24, vp.M34, vp.M44);
        _l = N(c4 + c1); _r = N(c4 - c1); _b = N(c4 + c2); _t = N(c4 - c2); _n = N(c3); _f = N(c4 - c3);
    }

    static Vector4 N(Vector4 p) { float len = new Vector3(p.X, p.Y, p.Z).Length(); return p / len; }
    static float D(in Vector4 p, in Vector3 v) => p.X * v.X + p.Y * v.Y + p.Z * v.Z + p.W;

    public bool IntersectsSphere(Vector3 c, float r) =>
        D(_l, c) >= -r && D(_r, c) >= -r && D(_b, c) >= -r && D(_t, c) >= -r && D(_n, c) >= -r && D(_f, c) >= -r;

    public bool IntersectsBox(Vector3 min, Vector3 max) =>
        Box(_l, min, max) && Box(_r, min, max) && Box(_b, min, max) && Box(_t, min, max) && Box(_n, min, max) && Box(_f, min, max);

    static bool Box(in Vector4 p, in Vector3 min, in Vector3 max)
    {   // positive vertex test
        var v = new Vector3(p.X >= 0 ? max.X : min.X, p.Y >= 0 ? max.Y : min.Y, p.Z >= 0 ? max.Z : min.Z);
        return D(p, v) >= 0;
    }

    /// <summary>Writes indices of visible spheres (xyz centre, w radius) in input order; returns the count.</summary>
    public int CullSpheres(ReadOnlySpan<Vector4> spheres, Span<int> visible)
    {
        int n = 0;
        for (int i = 0; i < spheres.Length; i++)
        {
            var s = spheres[i];
            if (IntersectsSphere(new Vector3(s.X, s.Y, s.Z), s.W)) visible[n++] = i;
        }
        return n;
    }
}

/// <summary>
/// Distance-based LOD for terrain chunks / instance groups. <c>Distances[i]</c> is the far edge of LOD i;
/// beyond the last entry the object is culled (LOD = -1). Hysteresis (fraction) avoids popping at boundaries.
/// </summary>
public sealed class ChunkLod
{
    public float[] Distances { get; }
    public float Hysteresis { get; }
    public ChunkLod(float[] distances, float hysteresis = 0.1f)
    {
        for (int i = 1; i < distances.Length; i++) if (distances[i] <= distances[i - 1]) throw new ArgumentException("distances must be ascending");
        Distances = distances; Hysteresis = hysteresis;
    }

    public int Select(float distance)
    {
        for (int i = 0; i < Distances.Length; i++) if (distance <= Distances[i]) return i;
        return -1;
    }

    /// <summary>Like <see cref="Select(float)"/>, but only moves to a COARSER level once past the edge by the hysteresis band.</summary>
    public int Select(float distance, int previous)
    {
        int lod = Select(distance);
        if (previous < 0 || previous >= Distances.Length) return lod;
        if (lod == previous) return lod;
        bool coarser = lod < 0 || lod > previous;
        if (coarser && distance <= Distances[previous] * (1 + Hysteresis)) return previous;
        return lod;
    }

    /// <summary>Visible chunks (frustum-tested AABBs) with their LOD, in chunk-index order.</summary>
    public int Visible(in Frustum f, Vector3 camera, ReadOnlySpan<BoundingBox> chunks, Span<(int chunk, int lod)> output)
    {
        int n = 0;
        for (int i = 0; i < chunks.Length; i++)
        {
            var b = chunks[i];
            if (!f.IntersectsBox(b.Min, b.Max)) continue;
            var closest = Vector3.Clamp(camera, b.Min, b.Max);
            int lod = Select(Vector3.Distance(camera, closest));
            if (lod >= 0) output[n++] = (i, lod);
        }
        return n;
    }
}
