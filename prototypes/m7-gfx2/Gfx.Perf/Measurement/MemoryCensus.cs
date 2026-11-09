using System.Reflection;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Perf.Measurement;

/// <summary>
/// App-side census of the GPU memory this process holds through MonoGame: every live GraphicsResource the device
/// tracks (MonoGame keeps weak references to all of them), sized from its dimensions and format. It is an
/// ESTIMATE of the app's allocations (driver padding, alignment and internal copies are not visible), the
/// complement of the device-wide VRAM figure from GL_NVX_gpu_memory_info.
/// </summary>
public sealed record CensusResult(
    bool Available, string? Failure,
    int Textures, long TextureBytes, int RenderTargets, long RenderTargetBytes,
    int VertexBuffers, long VertexBytes, int IndexBuffers, long IndexBytes,
    int Effects, int OtherResources, int UnknownFormats, long BackBufferBytes)
{
    public long TotalBytes => TextureBytes + RenderTargetBytes + VertexBytes + IndexBytes + BackBufferBytes;
    public static CensusResult Unavailable(string why) => new(false, why, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public static class MemoryCensus
{
    static readonly FieldInfo? ResourcesField = typeof(GraphicsDevice).GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo? LockField = typeof(GraphicsDevice).GetField("_resourcesLock", BindingFlags.Instance | BindingFlags.NonPublic);

    public static CensusResult Take(GraphicsDevice gd)
    {
        if (ResourcesField?.GetValue(gd) is not System.Collections.IEnumerable set)
            return CensusResult.Unavailable("MonoGame GraphicsDevice._resources not found (MonoGame version changed?)");
        var gate = LockField?.GetValue(gd) ?? set;
        var alive = new List<object>();
        lock (gate)
            foreach (var o in set)
                if (o is WeakReference w && w.Target is { } t) alive.Add(t);

        int tex = 0, rts = 0, vbs = 0, ibs = 0, fx = 0, other = 0, unknown = 0;
        long texB = 0, rtB = 0, vbB = 0, ibB = 0;
        foreach (var r in alive)
        {
            switch (r)
            {
                case RenderTarget2D rt:
                {
                    rts++;
                    long color = TextureBytes(rt.Width, rt.Height, rt.LevelCount, rt.Format, ref unknown);
                    long depth = (long)rt.Width * rt.Height * DepthBytes(rt.DepthStencilFormat);
                    int samples = Math.Max(1, rt.MultiSampleCount);
                    rtB += color + depth * samples + (samples > 1 ? (long)rt.Width * rt.Height * BytesPerPixel(rt.Format, ref unknown) * samples : 0);
                    break;
                }
                case RenderTargetCube rc:
                {
                    rts++;
                    rtB += 6 * (TextureBytes(rc.Size, rc.Size, rc.LevelCount, rc.Format, ref unknown) + (long)rc.Size * rc.Size * DepthBytes(rc.DepthStencilFormat));
                    break;
                }
                case Texture2D t: tex++; texB += TextureBytes(t.Width, t.Height, t.LevelCount, t.Format, ref unknown); break;
                case TextureCube c: tex++; texB += 6 * TextureBytes(c.Size, c.Size, c.LevelCount, c.Format, ref unknown); break;
                case Texture3D t3: tex++; texB += TextureBytes(t3.Width, t3.Height, t3.LevelCount, t3.Format, ref unknown) * t3.Depth; break;
                case VertexBuffer vb: vbs++; vbB += (long)vb.VertexCount * vb.VertexDeclaration.VertexStride; break;
                case IndexBuffer ib: ibs++; ibB += (long)ib.IndexCount * (ib.IndexElementSize == IndexElementSize.SixteenBits ? 2 : 4); break;
                case Effect: fx++; break;
                default: other++; break;
            }
        }
        var pp = gd.PresentationParameters;
        long back = (long)pp.BackBufferWidth * pp.BackBufferHeight * (4L * 2 + DepthBytes(pp.DepthStencilFormat));
        return new CensusResult(true, null, tex, texB, rts, rtB, vbs, vbB, ibs, ibB, fx, other, unknown, back);
    }

    static long TextureBytes(int w, int h, int levels, SurfaceFormat f, ref int unknown)
    {
        long sum = 0;
        for (int l = 0; l < Math.Max(1, levels); l++)
        {
            int lw = Math.Max(1, w >> l), lh = Math.Max(1, h >> l);
            sum += IsBlock8(f) ? 8L * ((lw + 3) / 4) * ((lh + 3) / 4)
                 : IsBlock16(f) ? 16L * ((lw + 3) / 4) * ((lh + 3) / 4)
                 : (long)lw * lh * BytesPerPixel(f, ref unknown);
        }
        return sum;
    }

    static bool IsBlock8(SurfaceFormat f) => f is SurfaceFormat.Dxt1 or SurfaceFormat.Dxt1SRgb or SurfaceFormat.Dxt1a or SurfaceFormat.RgbEtc1;
    static bool IsBlock16(SurfaceFormat f) => f is SurfaceFormat.Dxt3 or SurfaceFormat.Dxt3SRgb or SurfaceFormat.Dxt5 or SurfaceFormat.Dxt5SRgb;

    /// <summary>Bytes per pixel for uncompressed formats (unknown formats count 4 and are reported).</summary>
    public static int BytesPerPixel(SurfaceFormat f, ref int unknown)
    {
        switch (f)
        {
            case SurfaceFormat.Alpha8: return 1;
            case SurfaceFormat.Bgr565: case SurfaceFormat.Bgra5551: case SurfaceFormat.Bgra4444:
            case SurfaceFormat.NormalizedByte2: case SurfaceFormat.HalfSingle: return 2;
            case SurfaceFormat.Color: case SurfaceFormat.ColorSRgb: case SurfaceFormat.NormalizedByte4: case SurfaceFormat.Rgba1010102:
            case SurfaceFormat.Rg32: case SurfaceFormat.Single: case SurfaceFormat.HalfVector2: case SurfaceFormat.Bgr32: case SurfaceFormat.Bgra32:
            case SurfaceFormat.Bgr32SRgb: case SurfaceFormat.Bgra32SRgb: return 4;
            case SurfaceFormat.Rgba64: case SurfaceFormat.Vector2: case SurfaceFormat.HalfVector4: case SurfaceFormat.HdrBlendable: return 8;
            case SurfaceFormat.Vector4: return 16;
            default: unknown++; return 4;
        }
    }

    public static int DepthBytes(DepthFormat d) => d switch
    {
        DepthFormat.None => 0,
        DepthFormat.Depth16 => 2,
        _ => 4, // Depth24 is stored in 32 bits; Depth24Stencil8 packs into 32 bits
    };
}
