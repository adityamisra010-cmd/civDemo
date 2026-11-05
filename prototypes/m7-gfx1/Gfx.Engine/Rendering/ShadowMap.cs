using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Rendering;

/// <summary>
/// Single directional shadow map (default 2048², SurfaceFormat.Single light-space depth, 3×3 PCF in
/// <c>GlslLib.shadowPcf</c>). Fit an orthographic light frustum around a focus sphere; the light-space origin is
/// snapped to whole texels so the map does not shimmer when the camera moves. (Recon: probe ShadowShot.)
/// </summary>
public sealed class ShadowMap : IDisposable
{
    public RenderTarget2D Target { get; }
    public int Size { get; }
    public Matrix LightViewProj { get; private set; } = Matrix.Identity;
    /// <summary>Depth bias in light-space depth units (depends on the fitted depth range; see <see cref="Fit"/>).</summary>
    public float Bias { get; set; }
    public float Strength { get; set; } = 1f;
    public float BiasTexels { get; set; } = 1.0f;
    /// <summary>Normal offset of the lookup position, in shadow texels (converted to metres by <see cref="Fit"/>).</summary>
    public float NormalOffsetTexels { get; set; } = 1.5f;
    public float NormalOffsetWorld { get; private set; }

    public ShadowMap(GraphicsDevice gd, int size = 2048)
    {
        Size = size;
        Target = new RenderTarget2D(gd, size, size, false, SurfaceFormat.Single, DepthFormat.Depth24);
    }

    public Vector4 Params(bool enabled) => new(1f / Size, Bias, enabled ? 1 : 0, Strength);

    public void Fit(Vector3 directionToLight, Vector3 focus, float radius)
    {
        var dir = Vector3.Normalize(directionToLight);
        var up = MathF.Abs(dir.Y) > 0.99f ? Vector3.Forward : Vector3.Up;
        float depth = radius * 4f;
        var view = Matrix.CreateLookAt(focus + dir * radius * 2f, focus, up);
        // snap the focus to the texel grid in light space
        float texel = 2f * radius / Size;
        var f = Vector3.Transform(focus, view);
        var snap = new Vector3(MathF.Round(f.X / texel) * texel - f.X, MathF.Round(f.Y / texel) * texel - f.Y, 0);
        view *= Matrix.CreateTranslation(snap);
        LightViewProj = view * Matrix.CreateOrthographic(2f * radius, 2f * radius, 0.1f, depth);
        Bias = BiasTexels * texel / depth;
        NormalOffsetWorld = NormalOffsetTexels * texel;
    }

    public void Dispose() => Target.Dispose();
}
