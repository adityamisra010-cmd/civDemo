using Microsoft.Xna.Framework;

namespace Gfx.Gear.Geometry;

/// <summary>An atlas rectangle: where a part's (u, v) in [0,1]^2 lands in the item's texture atlas.</summary>
public readonly record struct UvRect(float U0, float V0, float U1, float V1)
{
    public static readonly UvRect Full = new(0, 0, 1, 1);
    public Vector2 Map(double u, double v) => new((float)(U0 + (U1 - U0) * u), (float)(V0 + (V1 - V0) * v));
    public float Width => U1 - U0;
    public float Height => V1 - V0;
}

/// <summary>
/// Shape generators (deterministic, double precision, clean quad topology): lathe, sweep along a path with
/// rotation-minimising frames, loft through rings, parametric grids, clipping by a smooth field (marching triangles,
/// so cut edges follow the iso-line exactly) and solidify with rounded or rolled edges. Every generator writes UVs in
/// the [0,1]^2 of a given atlas rectangle and a mask colour for the map painters.
/// </summary>
public static class Shapes
{
    // ------------------------------------------------------------------ profiles

    /// <summary>A circular arc from angle a0 to a1 (radians, 0 = +x) around (cx, cy), n segments (n + 1 points).</summary>
    public static List<Vector2d> Arc(double cx, double cy, double r, double a0, double a1, int n)
    {
        var l = new List<Vector2d>(n + 1);
        for (int i = 0; i <= n; i++) { double a = a0 + (a1 - a0) * i / n; l.Add(new Vector2d(cx + r * Math.Cos(a), cy + r * Math.Sin(a))); }
        return l;
    }

    /// <summary>
    /// Replaces each interior corner of a polyline (x = radius or across, y = height or along) with a circular fillet of
    /// the given radius (clamped to 45 % of the shorter neighbouring segment), <paramref name="steps"/> segments each.
    /// radii[i] &lt;= 0 keeps corner i sharp. Endpoints are kept.
    /// </summary>
    public static List<Vector2d> Fillet(IReadOnlyList<Vector2d> pts, IReadOnlyList<double> radii, int steps = 4)
    {
        var o = new List<Vector2d> { pts[0] };
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var p = pts[i]; var a = pts[i - 1]; var b = pts[i + 1];
            double r = radii[i];
            var da = (a - p); double la = da.Length; var db = (b - p); double lb = db.Length;
            if (r <= 0 || la < 1e-9 || lb < 1e-9) { o.Add(p); continue; }
            da /= la; db /= lb;
            double cos = Math.Clamp(da.Dot(db), -1, 1);
            double half = Math.Acos(cos) * 0.5;               // half the interior angle
            if (half > Math.PI * 0.5 - 1e-4) { o.Add(p); continue; } // straight: nothing to round
            double tlen = r / Math.Tan(half);
            double maxT = 0.45 * Math.Min(la, lb);
            if (tlen > maxT) { tlen = maxT; r = tlen * Math.Tan(half); }
            var t1 = p + da * tlen; var t2 = p + db * tlen;
            var bis = (da + db).Normalized();
            var c = p + bis * (r / Math.Sin(half));
            double a1 = Math.Atan2(t1.Y - c.Y, t1.X - c.X), a2 = Math.Atan2(t2.Y - c.Y, t2.X - c.X);
            double d = a2 - a1;
            while (d > Math.PI) d -= 2 * Math.PI;
            while (d < -Math.PI) d += 2 * Math.PI;
            for (int k = 0; k <= steps; k++) { double ang = a1 + d * k / steps; o.Add(new Vector2d(c.X + r * Math.Cos(ang), c.Y + r * Math.Sin(ang))); }
        }
        o.Add(pts[^1]);
        return o;
    }

    /// <summary>Cumulative arc length of a polyline (first value 0).</summary>
    public static double[] ArcLengths(IReadOnlyList<Vector2d> pts)
    {
        var s = new double[pts.Count];
        for (int i = 1; i < pts.Count; i++) s[i] = s[i - 1] + (pts[i] - pts[i - 1]).Length;
        return s;
    }

    /// <summary>Resamples a polyline to n points evenly spaced along its length.</summary>
    public static List<Vector2d> Resample(IReadOnlyList<Vector2d> pts, int n)
    {
        var s = ArcLengths(pts); double total = s[^1];
        var o = new List<Vector2d>(n);
        int j = 0;
        for (int i = 0; i < n; i++)
        {
            double t = total * i / (n - 1);
            while (j < pts.Count - 2 && s[j + 1] < t) j++;
            double seg = s[j + 1] - s[j];
            double f = seg > 1e-12 ? (t - s[j]) / seg : 0;
            o.Add(pts[j] + (pts[j + 1] - pts[j]) * f);
        }
        return o;
    }

    // ------------------------------------------------------------------ lathe

    /// <summary>
    /// Revolves a profile (x = radius, y = height) about +Y. <paramref name="radiusAt"/>(azimuth, profile index) can
    /// scale the radius per azimuth (ovals); <paramref name="uv"/> maps (azimuth fraction, profile arc fraction) by
    /// default, or the caller's function of the point. Azimuth 0 is +Z (the front), increasing towards +X.
    /// </summary>
    public static KitMesh Lathe(IReadOnlyList<Vector2d> profile, int segments, UvRect rect, Color mask,
        Func<double, int, double>? radiusAt = null, Func<Vector3d, double, double, Vector2>? uv = null, double a0 = 0, double a1 = 2 * Math.PI)
    {
        var m = new KitMesh();
        var s = ArcLengths(profile); double total = Math.Max(s[^1], 1e-9);
        bool full = Math.Abs(a1 - a0 - 2 * Math.PI) < 1e-9;
        int cols = segments + 1;
        for (int j = 0; j < profile.Count; j++)
            for (int i = 0; i < cols; i++)
            {
                double f = (double)i / segments;
                double a = a0 + (a1 - a0) * f;
                double r = profile[j].X * (radiusAt?.Invoke(a, j) ?? 1.0);
                var p = new Vector3d(r * Math.Sin(a), profile[j].Y, r * Math.Cos(a));
                var t = uv != null ? uv(p, f, s[j] / total) : rect.Map(f, s[j] / total);
                m.Add(p.ToVector3(), t, mask);
            }
        for (int j = 0; j < profile.Count - 1; j++)
            for (int i = 0; i < segments; i++)
            {
                int a = j * cols + i, b = a + 1, c = a + cols + 1, d = a + cols;
                // skip degenerate quads at the axis (radius 0 rows)
                if (profile[j].X < 1e-9 && profile[j + 1].X < 1e-9) continue;
                // winding: a profile traced clockwise in the (radius, height) half-plane (outward along the top, down
                // the outside, inward along the bottom) gets outward-facing normals
                if (profile[j].X < 1e-9) { m.Tri(a, d, c); continue; }
                if (profile[j + 1].X < 1e-9) { m.Tri(a, c, b); continue; }
                m.Quad(a, d, c, b);
            }
        _ = full;
        return m;
    }

    // ------------------------------------------------------------------ sweep

    /// <summary>
    /// Sweeps a 2D section (x along the frame normal, y along the binormal) along a 3D path with rotation-minimising
    /// frames (double reflection, Wang et al. 2008). <paramref name="scale"/>(i) scales the section at path point i.
    /// u runs along the section (its arc length, normalised), v along the path (normalised); <paramref name="vRepeat"/>
    /// repeats v (metres of path per texture repeat when <paramref name="vMetric"/> is set).
    /// </summary>
    public static KitMesh Sweep(IReadOnlyList<Vector3d> path, IReadOnlyList<Vector2d> section, bool closedSection, UvRect rect, Color mask,
        Vector3d? up = null, Func<int, double>? scale = null, bool closedPath = false, double vRepeat = 1.0, bool vMetric = false,
        Func<int, double>? twist = null, bool caps = false)
    {
        var frames = Frames(path, up ?? new Vector3d(0, 1, 0), closedPath);
        var m = new KitMesh();
        int ns = section.Count;
        var sec = new List<Vector2d>(section);
        if (closedSection) sec.Add(section[0]);
        var su = ArcLengths(sec); double stotal = Math.Max(su[^1], 1e-12);
        var pl = new double[path.Count + (closedPath ? 1 : 0)];
        for (int i = 1; i < pl.Length; i++) pl[i] = pl[i - 1] + (path[i % path.Count] - path[i - 1]).Length;
        double ptotal = Math.Max(pl[^1], 1e-12);
        int rows = path.Count + (closedPath ? 1 : 0);
        int cols = sec.Count;
        for (int j = 0; j < rows; j++)
        {
            int pj = j % path.Count;
            var (o, n, b) = frames[pj];
            if (closedPath && j == path.Count) (o, n, b) = frames[0];
            double sc = scale?.Invoke(pj) ?? 1.0;
            double tw = twist?.Invoke(pj) ?? 0.0;
            double ct = Math.Cos(tw), st = Math.Sin(tw);
            var n2 = n * ct + b * st; var b2 = b * ct - n * st;
            double v = vMetric ? pl[j] / vRepeat : pl[j] / ptotal * vRepeat;
            for (int i = 0; i < cols; i++)
            {
                var q = sec[i];
                var p = path[pj] + n2 * (q.X * sc) + b2 * (q.Y * sc);
                m.Add(p.ToVector3(), rect.Map(su[i] / stotal, v), mask);
            }
        }
        for (int j = 0; j < rows - 1; j++)
            for (int i = 0; i < cols - 1; i++)
            {
                int a = j * cols + i, b = a + 1, c = a + cols + 1, d = a + cols;
                // a counter-clockwise section (in the frame's normal/binormal plane) gets outward-facing normals
                m.Quad(a, b, c, d);
            }
        if (caps && !closedPath)
        {
            foreach (int j in new[] { 0, rows - 1 })
            {
                var centre = Vector3d.Zero;
                for (int i = 0; i < ns; i++) centre += new Vector3d(m.Positions[j * cols + i]);
                centre /= ns;
                var (o, n, b) = frames[j % path.Count];
                int c0 = m.Add(centre.ToVector3(), rect.Map(0.5, j == 0 ? 0 : 1), mask);
                for (int i = 0; i < ns; i++)
                {
                    int a = j * cols + i, b2 = j * cols + (i + 1) % ns;
                    int a2 = m.Add(m.Positions[a], m.Uvs[a], mask), b3 = m.Add(m.Positions[b2], m.Uvs[b2], mask);
                    if (j == 0) m.Tri(c0, a2, b3); else m.Tri(c0, b3, a2);
                }
                _ = o; _ = n; _ = b;
            }
        }
        return m;
    }

    /// <summary>Rotation-minimising frames (origin, normal, binormal) along a path; the first normal is the up vector made perpendicular.</summary>
    public static List<(Vector3d o, Vector3d n, Vector3d b)> Frames(IReadOnlyList<Vector3d> path, Vector3d up, bool closed)
    {
        int n = path.Count;
        var tangents = new Vector3d[n];
        for (int i = 0; i < n; i++)
        {
            Vector3d d;
            if (closed) d = path[(i + 1) % n] - path[(i - 1 + n) % n];
            else if (i == 0) d = path[1] - path[0];
            else if (i == n - 1) d = path[n - 1] - path[n - 2];
            else d = path[i + 1] - path[i - 1];
            tangents[i] = d.Normalized();
        }
        var r0 = up - tangents[0] * up.Dot(tangents[0]);
        if (r0.Length < 1e-9) r0 = Vector3d.Cross(tangents[0], new Vector3d(1, 0, 0));
        if (r0.Length < 1e-9) r0 = Vector3d.Cross(tangents[0], new Vector3d(0, 0, 1));
        r0 = r0.Normalized();
        var res = new List<(Vector3d, Vector3d, Vector3d)>(n);
        var r = r0;
        res.Add((path[0], r, Vector3d.Cross(tangents[0], r).Normalized()));
        for (int i = 0; i < n - 1; i++)
        {
            var v1 = path[i + 1] - path[i]; double c1 = v1.Dot(v1);
            if (c1 < 1e-24) { res.Add((path[i + 1], r, Vector3d.Cross(tangents[i + 1], r).Normalized())); continue; }
            var rL = r - v1 * (2.0 / c1 * v1.Dot(r));
            var tL = tangents[i] - v1 * (2.0 / c1 * v1.Dot(tangents[i]));
            var v2 = tangents[i + 1] - tL; double c2 = v2.Dot(v2);
            r = c2 < 1e-24 ? rL : rL - v2 * (2.0 / c2 * v2.Dot(rL));
            r = (r - tangents[i + 1] * r.Dot(tangents[i + 1])).Normalized();
            res.Add((path[i + 1], r, Vector3d.Cross(tangents[i + 1], r).Normalized()));
        }
        if (closed && n > 2)
        {
            // distribute the closing twist so the frame meets itself
            var (_, nEnd, bEnd) = res[n - 1];
            var (_, n0, _) = res[0];
            // propagate one more step to point 0
            var v1 = path[0] - path[n - 1]; double c1 = Math.Max(v1.Dot(v1), 1e-24);
            var rL = nEnd - v1 * (2.0 / c1 * v1.Dot(nEnd));
            var tL = tangents[n - 1] - v1 * (2.0 / c1 * v1.Dot(tangents[n - 1]));
            var v2 = tangents[0] - tL; double c2 = v2.Dot(v2);
            var rr = c2 < 1e-24 ? rL : rL - v2 * (2.0 / c2 * v2.Dot(rL));
            double ang = Math.Atan2(Vector3d.Cross(rr, n0).Dot(tangents[0]), rr.Dot(n0));
            for (int i = 0; i < n; i++)
            {
                double a = ang * i / n;
                var (o, nn, bb) = res[i];
                double c = Math.Cos(a), s = Math.Sin(a);
                res[i] = (o, nn * c + bb * s, bb * c - nn * s);
            }
            _ = bEnd;
        }
        return res;
    }

    /// <summary>A circle section of n points (closed), radius r, optionally an ellipse (rx, ry).</summary>
    public static List<Vector2d> Circle(double rx, double ry, int n, double phase = 0)
    {
        var l = new List<Vector2d>(n);
        for (int i = 0; i < n; i++) { double a = phase + 2 * Math.PI * i / n; l.Add(new Vector2d(rx * Math.Cos(a), ry * Math.Sin(a))); }
        return l;
    }

    public static KitMesh Tube(IReadOnlyList<Vector3d> path, double radius, int sides, UvRect rect, Color mask, Vector3d? up = null,
        Func<int, double>? scale = null, bool closedPath = false, double vRepeat = 1.0, bool vMetric = false, bool caps = false)
        => Sweep(path, Circle(radius, radius, sides), true, rect, mask, up, scale, closedPath, vRepeat, vMetric, null, caps);

    // ------------------------------------------------------------------ loft and grids

    /// <summary>Rings (equal point counts) joined by quads; closed rings repeat the first column for the UV seam.</summary>
    public static KitMesh Loft(IReadOnlyList<Vector3d[]> rings, bool closedRings, UvRect rect, Color mask, Func<int, int, Vector2>? uv = null)
    {
        var m = new KitMesh();
        int n = rings[0].Length;
        int cols = closedRings ? n + 1 : n;
        var vlen = new double[rings.Count];
        for (int j = 1; j < rings.Count; j++)
        {
            double d = 0;
            for (int i = 0; i < n; i++) d += (rings[j][i] - rings[j - 1][i]).Length;
            vlen[j] = vlen[j - 1] + d / n;
        }
        double vt = Math.Max(vlen[^1], 1e-12);
        for (int j = 0; j < rings.Count; j++)
        {
            // u by arc length around the ring
            var ring = rings[j];
            var ul = new double[cols];
            for (int i = 1; i < cols; i++) ul[i] = ul[i - 1] + (ring[i % n] - ring[i - 1]).Length;
            double ut = Math.Max(ul[^1], 1e-12);
            for (int i = 0; i < cols; i++)
                m.Add(ring[i % n].ToVector3(), uv?.Invoke(j, i) ?? rect.Map(ul[i] / ut, vlen[j] / vt), mask);
        }
        for (int j = 0; j < rings.Count - 1; j++)
            for (int i = 0; i < cols - 1; i++)
            {
                int a = j * cols + i, b = a + 1, c = a + cols + 1, d = a + cols;
                m.Quad(a, b, c, d);
            }
        return m;
    }

    /// <summary>A parametric (nu x nv) grid: f(u, v) for u, v in [0,1]; UVs are (u, v) in the rect.</summary>
    public static KitMesh Grid(int nu, int nv, Func<double, double, Vector3d> f, UvRect rect, Color mask)
    {
        var m = new KitMesh();
        for (int j = 0; j <= nv; j++)
            for (int i = 0; i <= nu; i++)
            {
                double u = (double)i / nu, v = (double)j / nv;
                m.Add(f(u, v).ToVector3(), rect.Map(u, v), mask);
            }
        int cols = nu + 1;
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                int a = j * cols + i, b = a + 1, c = a + cols + 1, d = a + cols;
                m.Quad(a, b, c, d);
            }
        return m;
    }

    // ------------------------------------------------------------------ clipping by a field

    /// <summary>
    /// Keeps the part of a mesh where <paramref name="field"/>(vertex) &lt;= 0, cutting triangles along the zero
    /// iso-line (marching triangles: positions, UVs and masks interpolated linearly on each crossed edge, shared between
    /// neighbours, so the cut is one clean, watertight curve). Islands smaller than <paramref name="minIsland"/>
    /// triangles are dropped.
    /// </summary>
    public static KitMesh Clip(KitMesh src, Func<int, Vector3, double> field, int minIsland = 0)
    {
        var f = new double[src.VertexCount];
        for (int i = 0; i < f.Length; i++) { f[i] = field(i, src.Positions[i]); if (Math.Abs(f[i]) < 1e-12) f[i] = 1e-12; }
        var m = new KitMesh(src.Name);
        var keep = new int[src.VertexCount];
        Array.Fill(keep, -1);
        int Keep(int v)
        {
            if (keep[v] < 0) keep[v] = m.Add(src.Positions[v], src.Uvs[v], src.Masks[v]);
            return keep[v];
        }
        var cut = new Dictionary<long, int>();
        int Cut(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cut.TryGetValue(key, out int v)) return v;
            double t = f[a] / (f[a] - f[b]);
            var p = Vector3.Lerp(src.Positions[a], src.Positions[b], (float)t);
            var uv = Vector2.Lerp(src.Uvs[a], src.Uvs[b], (float)t);
            var ma = src.Masks[a].ToVector4(); var mb = src.Masks[b].ToVector4();
            v = m.Add(p, uv, new Color(Vector4.Lerp(ma, mb, (float)t)));
            cut[key] = v;
            return v;
        }
        for (int t = 0; t < src.TriangleCount; t++)
        {
            int[] v = { src.Indices[3 * t], src.Indices[3 * t + 1], src.Indices[3 * t + 2] };
            bool[] inside = { f[v[0]] <= 0, f[v[1]] <= 0, f[v[2]] <= 0 };
            int cnt = (inside[0] ? 1 : 0) + (inside[1] ? 1 : 0) + (inside[2] ? 1 : 0);
            if (cnt == 0) continue;
            if (cnt == 3) { m.Tri(Keep(v[0]), Keep(v[1]), Keep(v[2])); continue; }
            // rotate so the odd vertex is first
            int k = 0;
            for (int q = 0; q < 3; q++) if ((cnt == 1) == inside[q]) { k = q; break; }
            int a = v[k], b = v[(k + 1) % 3], c = v[(k + 2) % 3];
            if (cnt == 1) m.Tri(Keep(a), Cut(a, b), Cut(a, c));
            else { int ab = Cut(a, b), ac = Cut(a, c); m.Tri(ab, Keep(b), Keep(c)); m.Tri(ab, Keep(c), ac); }
        }
        RemoveSlivers(m, 1e-11f);
        if (minIsland > 0) DropIslands(m, minIsland);
        return Compact(m);
    }

    static void RemoveSlivers(KitMesh m, float minArea)
    {
        var keep = new List<int>(m.Indices.Count);
        for (int t = 0; t < m.TriangleCount; t++)
        {
            int a = m.Indices[3 * t], b = m.Indices[3 * t + 1], c = m.Indices[3 * t + 2];
            if (Vector3.Cross(m.Positions[b] - m.Positions[a], m.Positions[c] - m.Positions[a]).Length() * 0.5f < minArea) continue;
            keep.Add(a); keep.Add(b); keep.Add(c);
        }
        m.Indices.Clear(); m.Indices.AddRange(keep);
    }

    /// <summary>Drops connected components (by shared vertices) with fewer than n triangles.</summary>
    public static void DropIslands(KitMesh m, int n)
    {
        int nv = m.VertexCount;
        var parent = new int[nv];
        for (int i = 0; i < nv; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        // weld by position for connectivity (cut vertices are shared, seams may not be)
        var weld = new Dictionary<(float, float, float), int>();
        for (int i = 0; i < nv; i++)
        {
            var p = m.Positions[i]; var key = (p.X, p.Y, p.Z);
            if (weld.TryGetValue(key, out int w)) parent[Find(i)] = Find(w); else weld[key] = i;
        }
        for (int t = 0; t < m.TriangleCount; t++)
        {
            int a = Find(m.Indices[3 * t]), b = Find(m.Indices[3 * t + 1]), c = Find(m.Indices[3 * t + 2]);
            parent[b] = a; parent[Find(c)] = Find(a);
        }
        var count = new Dictionary<int, int>();
        for (int t = 0; t < m.TriangleCount; t++) { int r = Find(m.Indices[3 * t]); count[r] = count.GetValueOrDefault(r) + 1; }
        var keep = new List<int>();
        for (int t = 0; t < m.TriangleCount; t++)
            if (count[Find(m.Indices[3 * t])] >= n) { keep.Add(m.Indices[3 * t]); keep.Add(m.Indices[3 * t + 1]); keep.Add(m.Indices[3 * t + 2]); }
        m.Indices.Clear(); m.Indices.AddRange(keep);
    }

    /// <summary>Removes unreferenced vertices (keeping the order of first use by vertex index).</summary>
    public static KitMesh Compact(KitMesh m)
    {
        var used = new bool[m.VertexCount];
        foreach (var i in m.Indices) used[i] = true;
        var map = new int[m.VertexCount];
        var o = new KitMesh(m.Name);
        for (int i = 0; i < m.VertexCount; i++)
        {
            if (!used[i]) { map[i] = -1; continue; }
            map[i] = o.Add(m.Positions[i], m.Normals[i], m.Uvs[i], m.Masks[i]);
            o.Tangents[map[i]] = m.Tangents[i];
        }
        foreach (var i in m.Indices) o.Indices.Add(map[i]);
        return o;
    }

    // ------------------------------------------------------------------ boundaries and solidify

    /// <summary>
    /// Boundary loops (edges used by one triangle), following the triangles' winding, vertices welded by position.
    /// Each loop is a list of vertex indices of the mesh (one representative per welded position).
    /// </summary>
    public static List<List<int>> BoundaryLoops(KitMesh m)
    {
        var weld = new Dictionary<(float, float, float), int>();
        var wid = new int[m.VertexCount];
        var rep = new List<int>();
        for (int i = 0; i < m.VertexCount; i++)
        {
            var p = m.Positions[i]; var key = (p.X, p.Y, p.Z);
            if (!weld.TryGetValue(key, out int w)) { w = rep.Count; weld[key] = w; rep.Add(i); }
            wid[i] = w;
        }
        var edgeCount = new Dictionary<(int, int), int>();
        for (int t = 0; t < m.TriangleCount; t++)
            for (int k = 0; k < 3; k++)
            {
                int a = wid[m.Indices[3 * t + k]], b = wid[m.Indices[3 * t + (k + 1) % 3]];
                var key = a < b ? (a, b) : (b, a);
                edgeCount[key] = edgeCount.GetValueOrDefault(key) + 1;
            }
        var next = new Dictionary<int, int>();
        var order = new List<int>();
        for (int t = 0; t < m.TriangleCount; t++)
            for (int k = 0; k < 3; k++)
            {
                int a = wid[m.Indices[3 * t + k]], b = wid[m.Indices[3 * t + (k + 1) % 3]];
                var key = a < b ? (a, b) : (b, a);
                if (edgeCount[key] != 1) continue;
                if (!next.ContainsKey(a)) { next[a] = b; order.Add(a); }
            }
        var loops = new List<List<int>>();
        var done = new HashSet<int>();
        foreach (int s in order)
        {
            if (done.Contains(s)) continue;
            var loop = new List<int>();
            int c = s;
            while (!done.Contains(c) && next.ContainsKey(c))
            {
                done.Add(c); loop.Add(rep[c]); c = next[c];
                if (loop.Count > 1_000_000) break;
            }
            if (loop.Count >= 3) loops.Add(loop);
        }
        return loops;
    }

    /// <summary>
    /// Gives a surface thickness: the surface (outer) plus an inner copy offset by -normal * thickness (winding flipped,
    /// UVs remapped into <paramref name="innerRect"/> when given) and, along every boundary loop, an edge strip with
    /// <paramref name="edgeSteps"/> segments: a half-round of radius thickness/2 that also bulges by
    /// <paramref name="bead"/> (m) above the outer surface (a rolled edge) when bead &gt; 0. Edge strip UVs: u along the
    /// loop (metres / <paramref name="edgeMetresPerU"/>), v across the edge, in <paramref name="edgeRect"/>. Edge
    /// vertices get mask B = 255. The input must have smooth normals (call ComputeNormals first).
    /// </summary>
    public static KitMesh Solidify(KitMesh outer, double thickness, UvRect? innerRect, UvRect edgeRect, int edgeSteps = 6, double bead = 0,
        double edgeMetresPerU = 1.0, Func<Vector2, Vector2>? innerUv = null, Color? innerMask = null)
    {
        var res = new KitMesh(outer.Name);
        res.Append(outer);
        // inner surface
        var inner = new KitMesh();
        for (int i = 0; i < outer.VertexCount; i++)
        {
            var uv = outer.Uvs[i];
            if (innerUv != null) uv = innerUv(uv);
            inner.Add(outer.Positions[i] - outer.Normals[i] * (float)thickness, -outer.Normals[i], uv, innerMask ?? outer.Masks[i]);
        }
        inner.Indices.AddRange(outer.Indices);
        inner.FlipWinding();
        _ = innerRect;
        int innerBase = res.VertexCount;
        res.Append(inner);
        // edge strips
        foreach (var loop in BoundaryLoops(outer))
        {
            int n = loop.Count;
            var len = new double[n + 1];
            for (int i = 1; i <= n; i++) len[i] = len[i - 1] + (outer.Positions[loop[i % n]] - outer.Positions[loop[i - 1]]).Length();
            int rowBase = res.VertexCount;
            for (int i = 0; i <= n; i++)
            {
                int v = loop[i % n];
                var p = new Vector3d(outer.Positions[v]);
                var nrm = new Vector3d(outer.Normals[v]).Normalized();
                var prev = new Vector3d(outer.Positions[loop[(i - 1 + n) % n]]);
                var next = new Vector3d(outer.Positions[loop[(i + 1) % n]]);
                var d = (next - prev).Normalized();
                var b = Vector3d.Cross(d, nrm).Normalized();   // outward, in the surface plane (boundary follows the winding)
                var mid = p - nrm * (thickness * 0.5);
                for (int k = 0; k <= edgeSteps; k++)
                {
                    double a = Math.PI * k / edgeSteps;
                    double hn = thickness * 0.5 * Math.Cos(a) + bead * Math.Sin(a) * Math.Sin(a);
                    double hb = (thickness * 0.5 + bead * 0.6) * Math.Sin(a);
                    var q = mid + nrm * hn + b * hb;
                    var mk = outer.Masks[v]; mk.B = 255;
                    res.Add(q.ToVector3(), edgeRect.Map(len[i] / edgeMetresPerU, (double)k / edgeSteps), mk);
                }
            }
            int cols = edgeSteps + 1;
            for (int i = 0; i < n; i++)
                for (int k = 0; k < edgeSteps; k++)
                {
                    int a = rowBase + i * cols + k, b2 = a + 1, c = a + cols + 1, d2 = a + cols;
                    res.Quad(a, d2, c, b2);
                }
        }
        _ = innerBase;
        return res;
    }

    /// <summary>Flips the winding when most of the area-weighted face normals point towards <paramref name="centre"/>.</summary>
    public static void OrientOutward(KitMesh m, Vector3 centre)
    {
        double s = 0;
        for (int f = 0; f < m.TriangleCount; f++)
        {
            var a = m.Positions[m.Indices[3 * f]]; var b = m.Positions[m.Indices[3 * f + 1]]; var c = m.Positions[m.Indices[3 * f + 2]];
            var n = Vector3.Cross(b - a, c - a);
            s += Vector3.Dot(n, (a + b + c) / 3f - centre);
        }
        if (s < 0) m.FlipWinding();
    }

    /// <summary>Signed distance from p to a closed polygon (negative inside), by the even-odd rule.</summary>
    public static double PolygonSdf(IReadOnlyList<Vector2d> poly, Vector2d p)
    {
        double best = double.MaxValue; bool inside = false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var a = poly[j]; var b = poly[i];
            var ab = b - a; double l2 = ab.Dot(ab);
            double t = l2 > 0 ? Math.Clamp((p - a).Dot(ab) / l2, 0, 1) : 0;
            double d = (p - (a + ab * t)).Length;
            if (d < best) best = d;
            if (((a.Y > p.Y) != (b.Y > p.Y)) && (p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)) inside = !inside;
        }
        return inside ? -best : best;
    }
}
