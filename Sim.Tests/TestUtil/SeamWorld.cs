using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Tests.TestUtil;

/// <summary>
/// Batch 3 WS3 — THE SEAM WORLD FIXTURE, shared by every seam test: a square map whose ONLY land is one strip
/// straddling the east-west seam — columns [0, W) and [Size − W, Size) of rows [Size/4, 3·Size/4), where
/// W = 3·Size/16 — and nothing else but ocean. On the cylinder (<c>wrapX</c> true) the strip is ONE landmass crossing
/// the seam; on the flat raster (the same layers, <c>wrapX</c> false) it is TWO islands separated by the whole ocean
/// between them, so every travel quantity between them is +∞ there. Worldgen cannot be asked for such a world (its
/// continental mask centres the land), so it is built with the WS3 test/tool factories
/// <see cref="TerrainSet.FromLayers"/> and <see cref="WorldFounding.FoundOn"/>.
///
/// Layers (uniform, so every difference a test sees is topology): land elevation 0.4 over a sea level of 0.2, movement
/// cost 1.0 (ideal ground; water 25), fertility 0.6, moisture 0.8, temperature 16 °C (the canonical fertility optimum),
/// no rivers. Sites are explicit cells (<see cref="Cell"/>), founded in the given order (settlement i = site i).
/// </summary>
public static class SeamWorld
{
    /// <summary>The canonical seam-world size: 256 px (64 lattice nodes) at 4 km/px — 1,024 km round, so a pair 216 px
    /// apart the flat way (864 km, past ADR-032's 600-km maxRouteKm) is 40 px (160 km) apart the short way.</summary>
    public const int DefaultSize = 256;

    /// <summary>Width of each half of the strip, in px (3·Size/16: 48 px at 256).</summary>
    public static int HalfWidth(int size) => 3 * size / 16;

    /// <summary>The row-major cell of (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static int Cell(int x, int y, int size = DefaultSize) => y * size + x;

    /// <summary>Whether (<paramref name="x"/>, <paramref name="y"/>) is land in the seam world of <paramref name="size"/>.</summary>
    public static bool IsLand(int x, int y, int size)
    {
        int w = HalfWidth(size);
        return y >= size / 4 && y < 3 * size / 4 && (x < w || x >= size - w);
    }

    /// <summary>The seam world's terrain at <paramref name="size"/> px (a multiple of the lattice stride 4).
    /// <paramref name="westFertility"/> / <paramref name="eastFertility"/> set the land fertility of the strip's two
    /// pieces (x &lt; HalfWidth, the map's west edge; x ≥ Size − HalfWidth, its east edge) — 0.6 both by default.</summary>
    public static TerrainSet Terrain(bool wrapX, int size = DefaultSize, double westFertility = 0.6, double eastFertility = 0.6)
    {
        int cells = size * size;
        var elevation = new double[cells];
        var water = new double[cells];
        var temperature = new double[cells];
        var moisture = new double[cells];
        var fertility = new double[cells];
        var movement = new double[cells];
        var rivers = new double[cells];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                bool land = IsLand(x, y, size);
                elevation[i] = land ? 0.4 : 0.0;
                water[i] = land ? 0.0 : 1.0;
                temperature[i] = 16.0;
                moisture[i] = land ? 0.8 : 1.0;
                fertility[i] = !land ? 0.0 : x < size / 2 ? westFertility : eastFertility;
                movement[i] = land ? 1.0 : 25.0;
            }
        return TerrainSet.FromLayers(size, 4.0, 0.2, elevation, water, temperature, moisture, fertility, movement,
            rivers, [], wrapX);
    }

    /// <summary>
    /// A world founded on the seam world with settlement i at <paramref name="sites"/>[i] (cells), the production
    /// sim config unless one is given, and <paramref name="aiEmpires"/> AI Empires (settlements are dealt to the
    /// polities round-robin by WorldFounding: settlement i to polity 1 + i mod (aiEmpires + 1)).
    /// </summary>
    public static WorldState Found(bool wrapX, int[] sites, int aiEmpires = 0, SimConfig? sim = null,
        int size = DefaultSize, ulong seed = 42, double westFertility = 0.6, double eastFertility = 0.6)
    {
        WorldgenConfig wg = Worldgen(size, aiEmpires);
        return WorldFounding.FoundOn(Terrain(wrapX, size, westFertility, eastFertility), sites, wg, sim ?? TestConfigs.Sim(), seed);
    }

    /// <summary>The worldgen config the seam world's founding path holds: the canonical one at the seam world's size
    /// (its siting, hinterland radius and topology flag are what ColonizationSystem and FoundingQuery read; terrain is
    /// never regenerated from it).</summary>
    public static WorldgenConfig Worldgen(int size = DefaultSize, int aiEmpires = 0) =>
        TestConfigs.Worldgen() with { SizePx = size, AiEmpires = aiEmpires };

    /// <summary>The two canonical seam sites: WEST at (20, Size/2) and EAST at (Size − 20, Size/2) — 40 px apart the short
    /// way (across the seam), Size − 40 px the flat way.</summary>
    public static int[] TwoSides(int size = DefaultSize) =>
        [Cell(20, size / 2, size), Cell(size - 20, size / 2, size)];
}
