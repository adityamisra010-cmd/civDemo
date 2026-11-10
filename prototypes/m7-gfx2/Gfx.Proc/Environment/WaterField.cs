using Gfx.Proc.Authoritative;
using Gfx.Proc.Presentation;

namespace Gfx.Proc.Environment;

/// <summary>
/// The water surface near every grid sample (PRESENTATION; derived from the grid's heights and water depths, read only):
/// at wet samples their own surface, at dry samples within <see cref="Reach"/> cells the highest surface of the wet
/// samples around them, elsewhere none. Ground cover uses it to keep out of the water and its splash zone (no ferns in
/// the shallows), sedges to grow just above the waterline, and the water and terrain shaders for foam and wet banks.
/// </summary>
public sealed class WaterField
{
    public const int Reach = 4;
    readonly float[] _surface;   // NaN where no water is near
    readonly TerrainMeshData _m;

    public WaterField(TerrainMeshData m)
    {
        _m = m;
        var g = m.Grid;
        int W = g.Width, H = g.Height;
        _surface = new float[W * H];
        var own = new float[W * H];
        for (int i = 0; i < own.Length; i++) own[i] = g.WaterDepthMm[i] > 0 ? (g.HeightMm[i] + g.WaterDepthMm[i]) / 1000f : float.NaN;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (!float.IsNaN(own[i])) { _surface[i] = own[i]; continue; }
                float best = float.NaN;
                for (int dy = -Reach; dy <= Reach; dy++)
                    for (int dx = -Reach; dx <= Reach; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H || dx * dx + dy * dy > Reach * Reach) continue;
                        float s = own[yy * W + xx];
                        if (!float.IsNaN(s) && (float.IsNaN(best) || s > best)) best = s;
                    }
                _surface[i] = best;
            }
    }

    /// <summary>Per-sample surface heights (m; NaN = no water near), row-major like the grid.</summary>
    public ReadOnlySpan<float> Samples => _surface;

    /// <summary>The nearby water surface at world (x, z), or NaN (nearest sample).</summary>
    public float SurfaceAt(float x, float z)
    {
        var g = _m.Grid;
        int ix = Math.Clamp((int)MathF.Round(x / _m.CellM + g.Width / 2), 0, g.Width - 1);
        int iz = Math.Clamp((int)MathF.Round(z / _m.CellM + g.Height / 2), 0, g.Height - 1);
        return _surface[iz * g.Width + ix];
    }

    /// <summary>Height of the ground above the nearby water surface at (x, z) (m); +∞ when no water is near.</summary>
    public float Freeboard(float x, float z)
    {
        float s = SurfaceAt(x, z);
        return float.IsNaN(s) ? float.PositiveInfinity : _m.HeightAt(x, z) - s;
    }

    public bool AnyWater
    {
        get { foreach (var v in _surface) if (!float.IsNaN(v)) return true; return false; }
    }
}
