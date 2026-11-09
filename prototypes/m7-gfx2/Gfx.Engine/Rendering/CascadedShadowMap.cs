using Gfx.Engine.Cameras;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Rendering;

/// <summary>
/// Cascaded shadow maps in one 2×2 atlas (stage render1; PRESENTATION ONLY). All cascades share one light view, so
/// cascade k's clip position is an affine function of cascade 0's (<see cref="Scale"/>, <see cref="Offset"/>): the
/// vertex stage computes one light-space position and the pixel stage picks the finest cascade that contains it.
/// Each cascade is a bounding sphere of its view-frustum slice (stable under camera rotation), snapped to whole
/// texels (no shimmer), and its light-space depth range is stretched toward the sun to hold every caster in
/// <see cref="RenderSettings.ShadowCasterBounds"/>: G1's single map clipped casters more than 2 radii up-sun
/// (≈400 m), which is why low-sun scenes lost their long shadows.
/// </summary>
public sealed class CascadedShadowMap : IDisposable
{
    public const int MaxCascades = 4;
    public RenderTarget2D Atlas { get; }
    public int TileSize { get; }
    public int Count { get; private set; }
    public Matrix LightView { get; private set; }
    public Matrix[] ViewProj { get; } = new Matrix[MaxCascades];
    readonly Matrix[] _proj = new Matrix[MaxCascades];
    /// <summary>Far edge of each cascade's view-depth slice (m).</summary>
    public float[] SplitFar { get; } = new float[MaxCascades];
    /// <summary>Bounding-sphere radius of each cascade (m).</summary>
    public float[] Radius { get; } = new float[MaxCascades];
    /// <summary>Per cascade: xyz = clip scale relative to cascade 0, w = depth bias (cascade depth units).</summary>
    public Vector4[] Scale { get; } = new Vector4[MaxCascades];
    /// <summary>Per cascade: xyz = clip offset relative to cascade 0.</summary>
    public Vector4[] Offset { get; } = new Vector4[MaxCascades];
    public float NormalOffsetWorld { get; private set; }
    public float BiasTexels { get; set; } = 1.6f;
    /// <summary>Blend between logarithmic (1) and uniform (0) split placement.</summary>
    public float SplitLambda { get; set; } = 0.86f;

    public CascadedShadowMap(GraphicsDevice gd, int tileSize)
    {
        TileSize = tileSize;
        Atlas = new RenderTarget2D(gd, tileSize * 2, tileSize * 2, false, SurfaceFormat.Single, DepthFormat.Depth24);
    }

    /// <summary>Practical split scheme (Zhang et al. 2006): far edge of slice i of n over [near, far].</summary>
    public static float[] Splits(float near, float far, int n, float lambda)
    {
        var s = new float[n];
        for (int i = 1; i <= n; i++)
        {
            float f = i / (float)n;
            float log = near * MathF.Pow(far / near, f), uni = near + (far - near) * f;
            s[i - 1] = lambda * log + (1 - lambda) * uni;
        }
        return s;
    }

    /// <summary>Fits <paramref name="count"/> cascades to the camera's view (unjittered) up to <paramref name="distance"/>.</summary>
    public void Fit(in CameraPose cam, float aspect, Vector3 dirToLight, int count, float distance, BoundingBox? casterBounds)
    {
        Count = Math.Clamp(count, 1, MaxCascades);
        var L = Vector3.Normalize(dirToLight);
        var up = MathF.Abs(L.Y) > 0.99f ? Vector3.Forward : Vector3.Up;
        // one light view for all cascades, looking from the sun's side through the world origin
        LightView = Matrix.CreateLookAt(Vector3.Zero, -L, up);
        float near = MathF.Max(cam.Near, 0.3f);
        float far = MathF.Min(distance, cam.Far);
        var splits = Splits(near, far, Count, SplitLambda);
        // caster depth extent in light view space (z grows toward the sun)
        float casterZMax = float.MinValue;
        if (casterBounds is { } cb)
            foreach (var c in cb.GetCorners()) casterZMax = MathF.Max(casterZMax, Vector3.Transform(c, LightView).Z);

        var fwd = cam.Forward;
        var right = Vector3.Normalize(Vector3.Cross(fwd, cam.Up));
        var upv = Vector3.Cross(right, fwd);
        float ty = MathF.Tan(cam.FovYRadians / 2), tx = ty * aspect;
        float prev = near;
        for (int k = 0; k < Count; k++)
        {
            float n0 = prev, f0 = splits[k]; prev = f0; SplitFar[k] = f0;
            // bounding sphere of the slice: centre on the view axis where it minimises the radius
            float t2 = tx * tx + ty * ty;
            float zc = MathF.Min(f0, 0.5f * (n0 + f0) * (1 + t2));
            float rFar = MathF.Sqrt((f0 - zc) * (f0 - zc) + f0 * f0 * t2), rNear = MathF.Sqrt((zc - n0) * (zc - n0) + n0 * n0 * t2);
            float r = MathF.Max(rFar, rNear);
            r = MathF.Ceiling(r * 16f) / 16f; // quantised: the size never jitters
            Radius[k] = r;
            var centreW = cam.Position + fwd * zc;
            var c = Vector3.Transform(centreW, LightView);
            float texel = 2 * r / TileSize;
            float cx = MathF.Round(c.X / texel) * texel, cy = MathF.Round(c.Y / texel) * texel;
            float zMax = MathF.Max(c.Z + r, casterZMax == float.MinValue ? c.Z + r + 1500f : casterZMax) + 10f;
            float zMin = c.Z - r - 10f;
            // view looks down −Z: CreateOrthographicOffCenter maps z = −zNear → 0 and z = −zFar → 1
            var proj = Matrix.CreateOrthographicOffCenter(cx - r, cx + r, cy - r, cy + r, -zMax, -zMin);
            _proj[k] = proj;
            ViewProj[k] = LightView * proj;
            float depthRange = zMax - zMin;
            Scale[k] = new Vector4(0, 0, 0, BiasTexels * texel / depthRange);
            if (k == 0) NormalOffsetWorld = 1.5f * texel;
        }
        var p0 = _proj[0];
        for (int k = 0; k < Count; k++)
        {
            // clip_k = clip_0 · s + o per axis (orthographic projections sharing one view)
            var pk = _proj[k];
            var sc = new Vector3(pk.M11 / p0.M11, pk.M22 / p0.M22, pk.M33 / p0.M33);
            var o = new Vector3(pk.M41 - p0.M41 * sc.X, pk.M42 - p0.M42 * sc.Y, pk.M43 - p0.M43 * sc.Z);
            Scale[k] = new Vector4(sc, Scale[k].W);
            Offset[k] = new Vector4(o, 0);
        }
        for (int k = Count; k < MaxCascades; k++) { Scale[k] = new Vector4(1, 1, 1, 0); Offset[k] = new Vector4(0, 0, 10, 0); }
    }

    /// <summary>Atlas viewport of cascade k (2×2 tiles: k = 0 top-left, 1 top-right, 2 bottom-left, 3 bottom-right).</summary>
    public Viewport TileViewport(int k) => new((k % 2) * TileSize, (k / 2) * TileSize, TileSize, TileSize);

    /// <summary>x = cascade count, y = atlas size (texels), z/w unused.</summary>
    public Vector4 AtlasParams => new(Count, TileSize * 2, 0, 0);

    public void Dispose() => Atlas.Dispose();
}
