using System.Security.Cryptography;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Geometry;

/// <summary>
/// A generated mesh in item space (metres; +Y up, +Z forward, right-handed): positions, normals, tangents (w =
/// handedness, the bitangent cross(N, T) * w points towards decreasing v, the image's "up", as in glTF), UVs, an
/// RGBA "mask" colour per vertex (generator data for the map painters: part id, edge, cavity), and a triangle list.
/// Built in double precision by the shape generators and stored as floats. Deterministic: no randomness here, every
/// operation is ordered by vertex and triangle index. PRESENTATION ONLY.
/// </summary>
public sealed class KitMesh
{
    public string Name = "";
    public readonly List<Vector3> Positions = new();
    public readonly List<Vector3> Normals = new();
    public readonly List<Vector2> Uvs = new();
    public readonly List<Vector4> Tangents = new();
    public readonly List<Color> Masks = new();
    public readonly List<int> Indices = new();

    public int VertexCount => Positions.Count;
    public int TriangleCount => Indices.Count / 3;

    public KitMesh() { }
    public KitMesh(string name) { Name = name; }

    public int Add(Vector3 p, Vector2 uv, Color mask)
    {
        Positions.Add(p); Uvs.Add(uv); Masks.Add(mask); Normals.Add(Vector3.Zero); Tangents.Add(Vector4.Zero);
        return Positions.Count - 1;
    }

    public int Add(Vector3 p, Vector3 n, Vector2 uv, Color mask)
    {
        Positions.Add(p); Uvs.Add(uv); Masks.Add(mask); Normals.Add(n); Tangents.Add(Vector4.Zero);
        return Positions.Count - 1;
    }

    public void Tri(int a, int b, int c) { Indices.Add(a); Indices.Add(b); Indices.Add(c); }

    /// <summary>Quad a-b-c-d (a perimeter in order) as two triangles split along the shorter diagonal.</summary>
    public void Quad(int a, int b, int c, int d)
    {
        float ac = (Positions[a] - Positions[c]).LengthSquared(), bd = (Positions[b] - Positions[d]).LengthSquared();
        if (ac <= bd) { Tri(a, b, c); Tri(a, c, d); }
        else { Tri(a, b, d); Tri(b, c, d); }
    }

    /// <summary>Appends another mesh (optionally transformed: positions by the matrix, normals and tangents by its rotation part).</summary>
    public void Append(KitMesh o, Matrix? m = null)
    {
        int b = Positions.Count;
        for (int i = 0; i < o.VertexCount; i++)
        {
            var p = o.Positions[i]; var n = o.Normals[i]; var t = o.Tangents[i];
            if (m is { } mm)
            {
                p = Vector3.Transform(p, mm);
                n = SafeNormalize(Vector3.TransformNormal(n, mm));
                var t3 = SafeNormalize(Vector3.TransformNormal(new Vector3(t.X, t.Y, t.Z), mm));
                t = new Vector4(t3, t.W * (mm.Determinant() < 0 ? -1f : 1f));
            }
            Positions.Add(p); Normals.Add(n); Tangents.Add(t); Uvs.Add(o.Uvs[i]); Masks.Add(o.Masks[i]);
        }
        bool flip = m is { } fm && fm.Determinant() < 0;
        for (int k = 0; k < o.Indices.Count; k += 3)
        {
            Indices.Add(b + o.Indices[k]);
            Indices.Add(b + o.Indices[k + (flip ? 2 : 1)]);
            Indices.Add(b + o.Indices[k + (flip ? 1 : 2)]);
        }
    }

    public void Transform(Matrix m)
    {
        float hand = m.Determinant() < 0 ? -1f : 1f;
        for (int i = 0; i < VertexCount; i++)
        {
            Positions[i] = Vector3.Transform(Positions[i], m);
            Normals[i] = SafeNormalize(Vector3.TransformNormal(Normals[i], m));
            var t = Tangents[i];
            var t3 = SafeNormalize(Vector3.TransformNormal(new Vector3(t.X, t.Y, t.Z), m));
            Tangents[i] = new Vector4(t3, t.W * hand);
        }
        if (hand < 0) FlipWinding();
    }

    public void FlipWinding()
    {
        for (int i = 0; i < Indices.Count; i += 3) (Indices[i + 1], Indices[i + 2]) = (Indices[i + 2], Indices[i + 1]);
    }

    /// <summary>Remaps UVs into a rectangle of the atlas: uv' = offset + uv * scale.</summary>
    public void MapUv(Vector2 offset, Vector2 scale)
    {
        for (int i = 0; i < Uvs.Count; i++) Uvs[i] = offset + Uvs[i] * scale;
    }

    public void SetMask(Color c) { for (int i = 0; i < Masks.Count; i++) Masks[i] = c; }

    public static Vector3 SafeNormalize(Vector3 v)
    {
        float l = v.Length();
        return l > 1e-12f ? v / l : Vector3.UnitY;
    }

    public BoundingBox Bounds()
    {
        if (VertexCount == 0) return new BoundingBox();
        var lo = Positions[0]; var hi = Positions[0];
        foreach (var p in Positions) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
        return new BoundingBox(lo, hi);
    }

    /// <summary>
    /// Auto-smooth normals: area-and-angle weighted face normals, smoothed across an edge only when the two faces'
    /// normals differ by less than <paramref name="angleDeg"/>; vertices shared by faces of different smoothing groups
    /// are split. Existing positions, UVs and masks are kept; tangents are recomputed by <see cref="ComputeTangents"/>.
    /// </summary>
    public void ComputeNormals(float angleDeg = 50f)
    {
        int nt = TriangleCount;
        var fn = new Vector3[nt];
        var fa = new double[nt];
        for (int f = 0; f < nt; f++)
        {
            var a = Positions[Indices[3 * f]]; var b = Positions[Indices[3 * f + 1]]; var c = Positions[Indices[3 * f + 2]];
            var cr = Vector3.Cross(b - a, c - a);
            double len = cr.Length();
            fa[f] = len * 0.5;
            fn[f] = len > 1e-20 ? cr / (float)len : Vector3.Zero;
        }
        float cosLim = MathF.Cos(MathHelper.ToRadians(angleDeg));
        // vertex -> incident faces (ordered by face index)
        int nv = VertexCount;
        var start = new int[nv + 1];
        foreach (var i in Indices) start[i + 1]++;
        for (int i = 0; i < nv; i++) start[i + 1] += start[i];
        var fill = (int[])start.Clone();
        var inc = new int[Indices.Count];
        for (int f = 0; f < nt; f++) for (int k = 0; k < 3; k++) inc[fill[Indices[3 * f + k]]++] = f;
        // positions are welded by exact equality for smoothing (seams split by UV still smooth across)
        var weld = new Dictionary<(float, float, float), int>();
        var wid = new int[nv];
        for (int i = 0; i < nv; i++)
        {
            var p = Positions[i];
            var key = (p.X, p.Y, p.Z);
            if (!weld.TryGetValue(key, out int w)) { w = weld.Count; weld[key] = w; }
            wid[i] = w;
        }
        var wstart = new int[weld.Count + 1];
        for (int i = 0; i < nv; i++) wstart[wid[i] + 1] += start[i + 1] - start[i];
        for (int i = 0; i < weld.Count; i++) wstart[i + 1] += wstart[i];
        var wfill = (int[])wstart.Clone();
        var winc = new int[Indices.Count];
        for (int i = 0; i < nv; i++) for (int k = start[i]; k < start[i + 1]; k++) winc[wfill[wid[i]]++] = inc[k];
        // for each corner (face f, vertex v): sum the incident faces (at v's welded position) whose normal is close to f's
        var cornerNormal = new Vector3[Indices.Count];
        for (int f = 0; f < nt; f++)
            for (int k = 0; k < 3; k++)
            {
                int v = Indices[3 * f + k];
                int w = wid[v];
                var sum = Vector3.Zero;
                for (int q = wstart[w]; q < wstart[w + 1]; q++)
                {
                    int g = winc[q];
                    if (g != f && Vector3.Dot(fn[f], fn[g]) < cosLim) continue;
                    sum += fn[g] * (float)(fa[g] * CornerAngle(g, Positions[v]));
                }
                cornerNormal[3 * f + k] = SafeNormalize(sum.LengthSquared() > 0 ? sum : fn[f]);
            }
        // split vertices whose corners disagree
        var assigned = new Dictionary<(int v, int nx, int ny, int nz), int>();
        var remap = new int[Indices.Count];
        var oldCount = nv;
        var firstUse = new bool[oldCount];
        for (int c = 0; c < Indices.Count; c++)
        {
            int v = Indices[c];
            var n = cornerNormal[c];
            var key = (v, (int)MathF.Round(n.X * 4096), (int)MathF.Round(n.Y * 4096), (int)MathF.Round(n.Z * 4096));
            if (assigned.TryGetValue(key, out int nvx)) { remap[c] = nvx; continue; }
            if (!firstUse[v]) { firstUse[v] = true; Normals[v] = n; assigned[key] = v; remap[c] = v; continue; }
            int copy = Positions.Count;
            Positions.Add(Positions[v]); Uvs.Add(Uvs[v]); Masks.Add(Masks[v]); Normals.Add(n); Tangents.Add(Vector4.Zero);
            assigned[key] = copy; remap[c] = copy;
        }
        for (int c = 0; c < Indices.Count; c++) Indices[c] = remap[c];
        // vertices no triangle uses (a lathe's seam column at an apex) get a unit normal, so every attribute is valid
        for (int i = 0; i < Normals.Count; i++) if (Normals[i].LengthSquared() < 1e-12f) Normals[i] = Vector3.UnitY;
    }

    double CornerAngle(int f, Vector3 at)
    {
        int i0 = Indices[3 * f], i1 = Indices[3 * f + 1], i2 = Indices[3 * f + 2];
        Vector3 a = Positions[i0], b = Positions[i1], c = Positions[i2];
        Vector3 p, q, r;
        if (a == at) { p = a; q = b; r = c; } else if (b == at) { p = b; q = c; r = a; } else { p = c; q = a; r = b; }
        var u = q - p; var w = r - p;
        double lu = u.Length(), lw = w.Length();
        if (lu < 1e-12 || lw < 1e-12) return 0;
        double cs = Math.Clamp(Vector3.Dot(u, w) / (lu * lw), -1.0, 1.0);
        return Math.Acos(cs);
    }

    /// <summary>
    /// Per-vertex tangents from the UVs (Lengyel's accumulation): T along increasing u, orthogonalised against the
    /// normal; w chosen so cross(N, T) * w points towards decreasing v (the image's "up": OpenGL / glTF normal maps).
    /// </summary>
    public void ComputeTangents()
    {
        int nv = VertexCount;
        var tan = new Vector3[nv]; var bit = new Vector3[nv];
        for (int f = 0; f < TriangleCount; f++)
        {
            int i0 = Indices[3 * f], i1 = Indices[3 * f + 1], i2 = Indices[3 * f + 2];
            var p0 = Positions[i0]; var e1 = Positions[i1] - p0; var e2 = Positions[i2] - p0;
            var uv0 = Uvs[i0]; var d1 = Uvs[i1] - uv0; var d2 = Uvs[i2] - uv0;
            float det = d1.X * d2.Y - d2.X * d1.Y;
            if (MathF.Abs(det) < 1e-14f) continue;
            float r = 1f / det;
            var t = (e1 * d2.Y - e2 * d1.Y) * r;
            var b = (e2 * d1.X - e1 * d2.X) * r;
            float area = Vector3.Cross(e1, e2).Length();
            // weight by area so slivers do not dominate
            t = SafeNormalize(t) * area; b = SafeNormalize(b) * area;
            tan[i0] += t; tan[i1] += t; tan[i2] += t;
            bit[i0] += b; bit[i1] += b; bit[i2] += b;
        }
        for (int i = 0; i < nv; i++)
        {
            var n = Normals[i];
            var t = tan[i] - n * Vector3.Dot(n, tan[i]);
            if (t.LengthSquared() < 1e-20f)
            {
                // no UV gradient: any vector perpendicular to the normal
                t = Vector3.Cross(MathF.Abs(n.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX, n);
            }
            t = SafeNormalize(t);
            // bitangent along +v from the UVs; the stored frame's bitangent points along -v
            float w = Vector3.Dot(Vector3.Cross(n, t), bit[i]) < 0f ? 1f : -1f;
            Tangents[i] = new Vector4(t, w);
        }
    }

    /// <summary>Normals and tangents in one call (auto-smooth angle in degrees).</summary>
    public KitMesh Finish(float smoothAngleDeg = 50f)
    {
        ComputeNormals(smoothAngleDeg);
        ComputeTangents();
        return this;
    }

    /// <summary>SHA-256 over every attribute (little-endian floats) and the indices: the generator determinism fingerprint.</summary>
    public string Hash()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(VertexCount); w.Write(Indices.Count);
            for (int i = 0; i < VertexCount; i++)
            {
                var p = Positions[i]; var n = Normals[i]; var t = Tangents[i]; var uv = Uvs[i]; var m = Masks[i];
                w.Write(p.X); w.Write(p.Y); w.Write(p.Z); w.Write(n.X); w.Write(n.Y); w.Write(n.Z);
                w.Write(t.X); w.Write(t.Y); w.Write(t.Z); w.Write(t.W); w.Write(uv.X); w.Write(uv.Y); w.Write(m.PackedValue);
            }
            foreach (var i in Indices) w.Write(i);
        }
        return Convert.ToHexString(SHA256.HashData(ms.ToArray()));
    }

    /// <summary>Validation used by tests: indices in range, no degenerate triangles, unit normals, unit tangents, finite values.</summary>
    public IReadOnlyList<string> Validate(float minArea = 1e-12f)
    {
        var errs = new List<string>();
        foreach (var i in Indices) if (i < 0 || i >= VertexCount) { errs.Add($"{Name}: index {i} out of range"); break; }
        if (Indices.Count % 3 != 0) errs.Add($"{Name}: index count not a multiple of 3");
        int degenerate = 0;
        for (int f = 0; f < TriangleCount && errs.Count == 0; f++)
        {
            var a = Positions[Indices[3 * f]]; var b = Positions[Indices[3 * f + 1]]; var c = Positions[Indices[3 * f + 2]];
            if (Vector3.Cross(b - a, c - a).Length() * 0.5f < minArea) degenerate++;
        }
        if (degenerate > 0) errs.Add($"{Name}: {degenerate} degenerate triangles");
        for (int i = 0; i < VertexCount; i++)
        {
            var p = Positions[i]; var n = Normals[i]; var t = Tangents[i];
            if (!float.IsFinite(p.X + p.Y + p.Z)) { errs.Add($"{Name}: non-finite position {i}"); break; }
            if (MathF.Abs(n.Length() - 1f) > 1e-3f) { errs.Add($"{Name}: normal {i} not unit ({n.Length()})"); break; }
            if (MathF.Abs(new Vector3(t.X, t.Y, t.Z).Length() - 1f) > 1e-3f || MathF.Abs(MathF.Abs(t.W) - 1f) > 1e-6f) { errs.Add($"{Name}: tangent {i} invalid"); break; }
        }
        return errs;
    }
}
