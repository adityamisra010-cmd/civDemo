using Microsoft.Xna.Framework;

namespace Gfx.Engine.Scene;

/// <summary>
/// CPU occlusion culling (stage render2; PRESENTATION ONLY): a small software depth buffer of the frame's big occluders
/// (terrain, walls, buildings), rasterised on the CPU at the camera's view, eroded by one pixel and reduced into a
/// max-depth pyramid (hi-Z). An object is culled only when its whole screen footprint lies behind the farthest occluder
/// depth there, so the test is conservative: it may keep a hidden object, it never drops a visible one, as long as the
/// occluders are no larger than the real geometry (terrain occluders are built below the true surface, wall boxes inside
/// the walls).
/// <para>Depth is linear view depth (m). Rasterisation: edge functions at pixel centres, 1/w interpolated across the
/// triangle (exact perspective depth), triangles clipped against the near plane. Erosion: each pixel takes the farthest
/// depth of its 3×3 neighbourhood (an uncovered neighbour is infinitely far), so a pixel only half covered by an occluder
/// never occludes. Pyramid level k+1 holds the farthest depth of each 2×2 block of level k.</para>
/// </summary>
public sealed class OcclusionBuffer
{
    public int Width { get; }
    public int Height { get; }
    readonly float[] _raw;
    readonly List<float[]> _levels = new();
    readonly List<(int w, int h)> _sizes = new();
    Matrix _viewProj;
    float _near;
    public int TrianglesRasterized { get; private set; }
    public int Tests { get; private set; }
    public int Occluded { get; private set; }
    /// <summary>CPU time of the last Begin…End (ms).</summary>
    public double BuildMs { get; private set; }
    readonly System.Diagnostics.Stopwatch _sw = new();

    public OcclusionBuffer(int width = 256, int height = 144)
    {
        Width = width; Height = height;
        _raw = new float[width * height];
        int w = width, h = height;
        while (true)
        {
            _levels.Add(new float[w * h]); _sizes.Add((w, h));
            if (w == 1 && h == 1) break;
            w = Math.Max(1, (w + 1) / 2); h = Math.Max(1, (h + 1) / 2);
        }
    }

    /// <summary>Starts a frame: clears to "no occluder" (+∞) for the camera's view and projection.</summary>
    public void Begin(in Matrix view, in Matrix projection, float near)
    {
        _sw.Restart();
        _viewProj = view * projection;
        _near = MathF.Max(near, 1e-3f);
        Array.Fill(_raw, float.PositiveInfinity);
        TrianglesRasterized = 0; Tests = 0; Occluded = 0;
    }

    /// <summary>The farthest occluder depth (m) at pyramid level 0 after <see cref="End"/> (+∞ = none); for tests and debug views.</summary>
    public float DepthAt(int x, int y) => _levels[0][y * Width + x];
    public float RawDepthAt(int x, int y) => _raw[y * Width + x];
    public int Levels => _levels.Count;

    /// <summary>Rasterises world-space triangles (index triples into <paramref name="v"/>).</summary>
    public void AddTriangles(ReadOnlySpan<Vector3> v, ReadOnlySpan<int> indices)
    {
        Span<Vector4> c = stackalloc Vector4[3];
        for (int t = 0; t + 2 < indices.Length; t += 3)
        {
            c[0] = Vector4.Transform(new Vector4(v[indices[t]], 1), _viewProj);
            c[1] = Vector4.Transform(new Vector4(v[indices[t + 1]], 1), _viewProj);
            c[2] = Vector4.Transform(new Vector4(v[indices[t + 2]], 1), _viewProj);
            ClipAndRaster(c);
        }
    }

    /// <summary>Rasterises an oriented box (the 12 triangles of its faces): world = local unit box [−1,1]³ × <paramref name="box"/>.</summary>
    public void AddBox(in Matrix box)
    {
        Span<Vector3> p = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++) p[i] = Vector3.Transform(new Vector3((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1), box);
        AddTriangles(p, BoxIndices);
    }

    static readonly int[] BoxIndices =
    {
        0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3,
    };

    void ClipAndRaster(Span<Vector4> tri)
    {
        // clip against w = near (in front of the camera); XNA perspective: w = view depth
        Span<Vector4> poly = stackalloc Vector4[4];
        int n = 0;
        for (int i = 0; i < 3; i++)
        {
            var a = tri[i]; var b = tri[(i + 1) % 3];
            bool ina = a.W >= _near, inb = b.W >= _near;
            if (ina) poly[n++] = a;
            if (ina != inb)
            {
                float t = (_near - a.W) / (b.W - a.W);
                poly[n++] = Vector4.Lerp(a, b, t);
            }
        }
        if (n < 3) return;
        Raster(poly[0], poly[1], poly[2]);
        if (n == 4) Raster(poly[0], poly[2], poly[3]);
    }

    void Raster(Vector4 a, Vector4 b, Vector4 c)
    {
        TrianglesRasterized++;
        // screen positions (pixel units, y down) and 1/w
        float ax = (a.X / a.W * 0.5f + 0.5f) * Width, ay = (0.5f - a.Y / a.W * 0.5f) * Height, aw = 1f / a.W;
        float bx = (b.X / b.W * 0.5f + 0.5f) * Width, by = (0.5f - b.Y / b.W * 0.5f) * Height, bw = 1f / b.W;
        float cx = (c.X / c.W * 0.5f + 0.5f) * Width, cy = (0.5f - c.Y / c.W * 0.5f) * Height, cw = 1f / c.W;
        float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        if (MathF.Abs(area) < 1e-12f) return;
        int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx)))), x1 = Math.Min(Width - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx))));
        int y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy)))), y1 = Math.Min(Height - 1, (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy))));
        if (x0 > x1 || y0 > y1) return;
        float inv = 1f / area;
        for (int y = y0; y <= y1; y++)
        {
            float py = y + 0.5f;
            int row = y * Width;
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f;
                float w0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) * inv;
                float w1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) * inv;
                float w2 = 1f - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                float iw = w0 * aw + w1 * bw + w2 * cw;
                if (iw <= 0) continue;
                float d = 1f / iw;
                if (d < _raw[row + x]) _raw[row + x] = d;
            }
        }
    }

    /// <summary>Ends a frame: one-pixel erosion (farthest depth of the 3×3 neighbourhood) and the max-depth pyramid.</summary>
    public void End()
    {
        var l0 = _levels[0];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float m = 0;
                for (int j = -1; j <= 1; j++)
                {
                    int yy = y + j;
                    if (yy < 0 || yy >= Height) { m = float.PositiveInfinity; break; }
                    for (int i = -1; i <= 1; i++)
                    {
                        int xx = x + i;
                        float d = xx < 0 || xx >= Width ? float.PositiveInfinity : _raw[yy * Width + xx];
                        if (d > m) m = d;
                    }
                }
                l0[y * Width + x] = m;
            }
        for (int k = 1; k < _levels.Count; k++)
        {
            var (pw, ph) = _sizes[k - 1]; var (w, h) = _sizes[k];
            var src = _levels[k - 1]; var dst = _levels[k];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float m = 0;
                    for (int j = 0; j < 2; j++)
                        for (int i = 0; i < 2; i++)
                        {
                            int sx = Math.Min(pw - 1, 2 * x + i), sy = Math.Min(ph - 1, 2 * y + j);
                            m = MathF.Max(m, src[sy * pw + sx]);
                        }
                    dst[y * w + x] = m;
                }
        }
        BuildMs = _sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// True when a world-space sphere is certainly hidden: its screen rectangle (from the 8 corners of its bounding box)
    /// lies wholly behind the farthest occluder depth covering it. Any corner behind the near plane: not occluded.
    /// </summary>
    public bool IsSphereOccluded(Vector3 centre, float radius)
    {
        Tests++;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, minW = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            var p = centre + new Vector3((i & 1) != 0 ? radius : -radius, (i & 2) != 0 ? radius : -radius, (i & 4) != 0 ? radius : -radius);
            var c = Vector4.Transform(new Vector4(p, 1), _viewProj);
            if (c.W < _near) return false;
            float sx = (c.X / c.W * 0.5f + 0.5f) * Width, sy = (0.5f - c.Y / c.W * 0.5f) * Height;
            minX = MathF.Min(minX, sx); maxX = MathF.Max(maxX, sx); minY = MathF.Min(minY, sy); maxY = MathF.Max(maxY, sy);
            minW = MathF.Min(minW, c.W);
        }
        if (maxX < 0 || maxY < 0 || minX >= Width || minY >= Height) return false; // off screen: the frustum test owns it
        int x0 = Math.Max(0, (int)MathF.Floor(minX)), x1 = Math.Min(Width - 1, (int)MathF.Floor(maxX));
        int y0 = Math.Max(0, (int)MathF.Floor(minY)), y1 = Math.Min(Height - 1, (int)MathF.Floor(maxY));
        // pyramid level where the rectangle spans at most 2 texels per axis
        int level = 0;
        while (level + 1 < _levels.Count && ((x1 >> level) - (x0 >> level) > 1 || (y1 >> level) - (y0 >> level) > 1)) level++;
        var (w, _) = _sizes[level]; var buf = _levels[level];
        float far = 0;
        for (int y = y0 >> level; y <= (y1 >> level); y++)
            for (int x = x0 >> level; x <= (x1 >> level); x++)
                far = MathF.Max(far, buf[y * w + x]);
        bool hidden = far < minW;
        if (hidden) Occluded++;
        return hidden;
    }
}
