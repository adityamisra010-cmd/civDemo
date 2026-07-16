using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Weather;

/// <summary>Writable handles to WeatherSystem's own tables (built by SystemCatalog only).</summary>
public readonly record struct WeatherTables(Table<RainfallRow> Rainfall);

/// <summary>
/// M0 toy system: draws each region's rainfall for this turn from the system's
/// (SystemId × RegionId) RNG stream. Rainfall is environmental state sampled per
/// turn, not an integrated stock, so dt does not scale it (law 3 applies to rates
/// feeding stocks — GrowthSystem's side). STATELESS: no instance fields.
/// </summary>
public sealed class WeatherSystem : ISimSystem<WeatherTables>
{
    public static readonly SystemId WellKnownId = new(1);
    public const string Name = "weather";

    /// <summary>TUNE: uniform rainfall ceiling, mm/year.</summary>
    public const double MaxRainfallMmPerYear = 2000.0;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<WeatherTables> ctx)
    {
        IReadOnlyTable<RegionRow> regions = ctx.Prev.Regions;
        Table<RainfallRow> rainfall = ctx.Owned.Rainfall;

        for (int i = 0; i < regions.Count; i++)
        {
            RegionId region = regions[i].Id;
            double mm = ctx.Rng(region).NextDouble() * MaxRainfallMmPerYear;

            // §3.9 order consumption — M0's only order: SetRainBias adds a flat
            // mm/year bias to this region's draw, floored at zero. Orders apply in
            // log order; iteration is over the batch array (deterministic).
            for (int o = 0; o < ctx.Orders.Count; o++)
            {
                OrderRecord order = ctx.Orders[o];
                if (order.Kind == OrderKind.SetRainBias && order.TargetId == region.Value)
                    mm += order.Amount;
            }
            if (mm < 0.0) mm = 0.0;

            var row = new RainfallRow(region, mm);
            if (i < rainfall.Count) rainfall[i] = row;
            else rainfall.Add(row);
        }
    }
}
