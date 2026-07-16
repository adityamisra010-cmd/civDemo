using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Growth;

/// <summary>Writable handles to GrowthSystem's own tables (built by SystemCatalog only).</summary>
public readonly record struct GrowthTables(Table<BiomassRow> Biomass);

/// <summary>
/// M0 toy system: integrates each region's `long` biomass stock from LAST turn's
/// rainfall (read from Prev — the one-turn-lag default coupling, §3.2). The growth
/// rate is per-sim-year and integrates via dtYears (law 3); fractional flow
/// converts to integer units through the D-004 remainder accumulator stored in the
/// row. STATELESS: no instance fields.
///
/// NOTE: direct mutation of the Biomass stock is licensed for THIS PACKET ONLY —
/// T0.6 migrates all conserved-stock mutation behind the Ledger API (law 1).
/// </summary>
public sealed class GrowthSystem : ISimSystem<GrowthTables>
{
    public static readonly SystemId WellKnownId = new(2);
    public const string Name = "growth";

    /// <summary>TUNE: biomass base units grown per mm-of-rain per year.</summary>
    public const double GrowthPerMmPerYear = 1.0;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<GrowthTables> ctx)
    {
        IReadOnlyTable<RegionRow> regions = ctx.Prev.Regions;
        IReadOnlyTable<RainfallRow> prevRain = ctx.Prev.Rainfall;
        Table<BiomassRow> biomass = ctx.Owned.Biomass;

        for (int i = 0; i < regions.Count; i++)
        {
            if (i >= biomass.Count)
                biomass.Add(new BiomassRow(regions[i].Id, Biomass: 0, GrowthRemainder: 0.0));

            // Prev rainfall is turn t−1 output; before the first weather turn the
            // table is empty and growth is zero.
            double rainMmPerYear = i < prevRain.Count ? prevRain[i].RainfallMmPerYear : 0.0;
            double ratePerYear = GrowthPerMmPerYear * rainMmPerYear;

            ref BiomassRow row = ref biomass.Ref(i);
            double exact = ratePerYear * ctx.DtYears + row.GrowthRemainder;
            long delta = (long)Math.Floor(exact);
            row = row with
            {
                Biomass = row.Biomass + delta, // licensed direct mutation, this packet only
                GrowthRemainder = exact - delta,
            };
        }
    }
}
