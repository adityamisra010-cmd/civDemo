namespace Sim.Core.State;

/// <summary>
/// M5 integration (R2b, the weather-distance correction) — PURE GEOGRAPHY between two settlements:
/// the straight-line (flat-raster Euclidean) distance between their site cells. No road, route
/// class, modernization, cost factor or Pathfinder result enters it, so no change to the transport
/// network can move it. This is the distance a CLIMATIC quantity reads: weather does not travel
/// along roads (docs/m5-governing-loop-port.md §5 B4, escalated; resolved here).
///
/// It is the same formula as <see cref="RoadDevelopmentQuery.GeographicKm"/> (ADR-032's road
/// length invariant), applied to raw site cells rather than called, because that function lives
/// in the frozen transport surface (997824b) and takes a toy fallback this reader does not want.
/// Both now go through <see cref="Pathing.MapTopology.Km"/>.
///
/// BATCH 3 WS3 (ADR-032 FR3 amended by the Director's 2026-10-14 instruction): on a wrapped
/// (cylindrical) world the straight line is the one ON THE CYLINDER — x the short way round the
/// seam, <see cref="Pathing.MapTopology.Dx"/>. Weather between two settlements either side of the
/// seam correlates by their true separation; a flat world is unchanged bit for bit.
///
/// <see cref="IdealGroundCostUnits"/> expresses the length in the IDEAL-GROUND travel-cost units
/// the weather kernel's range is denominated in (<c>harvestVariance.spatialRangeCostUnits</c>):
/// km / <see cref="TransportQuery.KmPerCostUnit"/>. On ideal ground with no road the two measures
/// coincide; elsewhere the geographic one is what proximity means. A terrain-less world (a
/// hand-built toy, where no Pathfinder runs and the distance rows are written by hand) has no
/// positions, and the caller falls back to its hand-written table.
/// </summary>
public static class GeographicDistance
{
    /// <summary>Straight-line km between two row-major cells of the world's raster (on the cylinder when it wraps).</summary>
    public static double Km(Worldgen.TerrainSet terrain, int cellA, int cellB)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        return Pathing.MapTopology.Km(cellA, cellB, terrain.Size, terrain.KmPerPx, terrain.WrapX);
    }

    /// <summary>
    /// The straight-line distance in ideal-ground travel-cost units, or false when the world has no
    /// terrain (no positions exist; the caller's fallback applies).
    /// </summary>
    public static bool TryIdealGroundCostUnits(IReadOnlyWorldState world, int cellA, int cellB, double kmPerCostUnit, out double costUnits)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Terrain is not { } terrain || !(kmPerCostUnit > 0.0)) { costUnits = double.NaN; return false; }
        costUnits = Km(terrain, cellA, cellB) / kmPerCostUnit;
        return true;
    }
}
