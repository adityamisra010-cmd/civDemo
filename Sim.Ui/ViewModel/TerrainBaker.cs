using Sim.Core.Worldgen;

namespace Sim.Ui.ViewModel;

/// <summary>
/// Bakes the immutable TerrainSet rasters into one RGBA byte buffer (row-major,
/// 4 bytes/texel) — done ONCE at startup; the Game uploads it to a Texture2D
/// and never touches the rasters again (ADR-008: terrain is immutable in M1).
/// PURE function of the TerrainSet: byte-identical for the same terrain (the
/// bake-determinism test pins this). No MonoGame types — testable headless.
/// </summary>
public static class TerrainBaker
{
    public static byte[] Bake(TerrainSet terrain)
    {
        int size = terrain.Size;
        var pixels = new byte[size * size * 4];
        ReadOnlySpan<double> elevation = terrain.Elevation;
        ReadOnlySpan<double> water = terrain.Water;
        ReadOnlySpan<double> rivers = terrain.Rivers;

        // Normalization ranges from the actual raster (min/max are stable
        // properties of the immutable terrain, so the bake stays pure).
        double minElev = double.MaxValue, maxElev = double.MinValue;
        for (int i = 0; i < elevation.Length; i++)
        {
            if (elevation[i] < minElev) minElev = elevation[i];
            if (elevation[i] > maxElev) maxElev = elevation[i];
        }
        double sea = terrain.SeaLevel;
        double landSpan = maxElev > sea ? maxElev - sea : 1.0;
        double depthSpan = sea > minElev ? sea - minElev : 1.0;

        for (int i = 0; i < size * size; i++)
        {
            TerrainPalette.Rgba color;
            if (rivers[i] >= 0.5)
                color = TerrainPalette.RiverColor;          // rivers overdraw everything
            else if (water[i] >= 0.5)
                color = TerrainPalette.Water((sea - elevation[i]) / depthSpan);
            else
                color = TerrainPalette.Land((elevation[i] - sea) / landSpan);

            int o = i * 4;
            pixels[o] = color.R;
            pixels[o + 1] = color.G;
            pixels[o + 2] = color.B;
            pixels[o + 3] = color.A;
        }
        return pixels;
    }
}
