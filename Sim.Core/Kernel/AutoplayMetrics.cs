using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// Per-seed autoplay metrics (T2.8): the calibration battery's input contract.
/// An OBSERVATIONAL reader — it only reads chronicle tables and conserved
/// stocks after each turn, never writes state, and is deterministic (plain
/// loops, arrays in row order). The JSON shape `sim autoplay` emits from this
/// is documented in README.md §Autoplay metrics; the battery consumes the
/// same object in-process. Schema id: "autoplay-metrics/v1".
/// </summary>
public sealed class AutoplayMetrics
{
    // Parallel per-turn series, one entry per completed turn.
    public List<double> Year { get; } = [];
    public List<double> DtYears { get; } = [];
    public List<long> Population { get; } = [];
    public List<long> Births { get; } = [];
    public List<long> Deaths { get; } = [];          // base + starvation (vitals contract)
    public List<long> StarvationDeaths { get; } = []; // per-turn delta of the ledger sink
    public List<long> MigrationGross { get; } = [];   // sum of outflows over settlements

    // Final-state summary, filled by Finish().
    public ulong Seed { get; set; }
    public string WorldHash { get; set; } = "";
    public long FinalPopulation { get; set; }
    public double FinalYear { get; set; }
    public long[] FinalCohortTotals { get; set; } = new long[Cohorts.Count];
    /// <summary>Fertility-weighted arable area of all catchments, km²
    /// (the HONEST arable definition: Σ EffectiveFarmland × block-km²;
    /// EffectiveFarmland is mean-fertility-weighted lattice blocks).</summary>
    public double ArableKm2 { get; set; }
    public int SettlementCount { get; set; }
}

/// <summary>Accumulates an <see cref="AutoplayMetrics"/> over a run: call
/// <see cref="Observe"/> after every executor step, <see cref="Finish"/> once.</summary>
public sealed class AutoplayCollector(ulong seed)
{
    private readonly AutoplayMetrics _m = new() { Seed = seed };
    private long _prevStarved;

    public void Observe(WorldState world)
    {
        _m.Year.Add(world.Clock.WorldDateYears);
        _m.DtYears.Add(world.Clock.DtYears);

        long pop = 0;
        for (int i = 0; i < world.Buckets.Count; i++) pop += world.Buckets[i].Count.Value;
        _m.Population.Add(pop);

        long births = 0, deaths = 0;
        for (int i = 0; i < world.SettlementVitals.Count; i++)
        {
            births += world.SettlementVitals[i].Births;
            deaths += world.SettlementVitals[i].Deaths;
        }
        _m.Births.Add(births);
        _m.Deaths.Add(deaths);

        long starvedCum = 0;
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == ConservedQuantityIds.Population && row.Reason == ReasonIds.Starvation)
                starvedCum = row.TotalSunk;
        }
        _m.StarvationDeaths.Add(starvedCum - _prevStarved);
        _prevStarved = starvedCum;

        long gross = 0;
        for (int i = 0; i < world.MigrationFlows.Count; i++) gross += world.MigrationFlows[i].Outflow;
        _m.MigrationGross.Add(gross);
    }

    public AutoplayMetrics Finish(WorldState world)
    {
        _m.WorldHash = WorldHash.ComputeHex(world);
        _m.FinalYear = world.Clock.WorldDateYears;
        _m.SettlementCount = world.Settlements.Count;

        long pop = 0;
        for (int i = 0; i < world.Buckets.Count; i++)
        {
            BucketRow b = world.Buckets[i];
            pop += b.Count.Value;
            _m.FinalCohortTotals[b.CohortIdx] += b.Count.Value;
        }
        _m.FinalPopulation = pop;

        if (world.Terrain is { } terrain)
        {
            var lattice = Pathing.TraversalLattice.Build(terrain);
            int stride = terrain.Size / lattice.Size;
            double blockKm2 = stride * terrain.KmPerPx * (stride * terrain.KmPerPx);
            double farmland = 0.0;
            for (int i = 0; i < world.CatchmentSummaries.Count; i++)
                farmland += world.CatchmentSummaries[i].EffectiveFarmland;
            _m.ArableKm2 = farmland * blockKm2;
        }
        return _m;
    }
}

/// <summary>
/// Derived corridor metrics over an <see cref="AutoplayMetrics"/> series —
/// shared between the battery's teeth and any offline analysis so the CI
/// assertion and the nightly report can never diverge in definition.
/// </summary>
public static class CalibrationAnalysis
{
    /// <summary>Exponential growth rate per year over [fromYear, toYear],
    /// from the population at the last turn at-or-before each endpoint.
    /// Returns NaN when either endpoint population is nonpositive.</summary>
    public static double WindowGrowthPerYear(AutoplayMetrics m, double fromYear, double toYear)
    {
        int i0 = IndexAtOrBefore(m, fromYear), i1 = IndexAtOrBefore(m, toYear);
        if (i0 < 0 || i1 <= i0) return double.NaN;
        long p0 = m.Population[i0], p1 = m.Population[i1];
        double years = m.Year[i1] - m.Year[i0];
        if (p0 <= 0 || p1 <= 0 || years <= 0.0) return double.NaN;
        return Math.Log(p1 / (double)p0) / years;
    }

    /// <summary>Crude rate per person-year over [fromYear, toYear] for a
    /// per-turn count series (births, deaths, starvation).</summary>
    public static double CrudeRatePerPersonYear(
        AutoplayMetrics m, List<long> series, double fromYear, double toYear)
    {
        double events = 0.0, personYears = 0.0;
        for (int i = 0; i < m.Year.Count; i++)
        {
            if (m.Year[i] < fromYear || m.Year[i] > toYear) continue;
            events += series[i];
            personYears += m.Population[i] * m.DtYears[i];
        }
        return personYears <= 0.0 ? double.NaN : events / personYears;
    }

    /// <summary>Gross migration as a fraction of population per decade over
    /// the whole run (the T2.5 corridor's unit).</summary>
    public static double MigrationGrossPerDecade(AutoplayMetrics m)
    {
        double moved = 0.0, personYears = 0.0;
        for (int i = 0; i < m.Year.Count; i++)
        {
            moved += m.MigrationGross[i];
            personYears += m.Population[i] * m.DtYears[i];
        }
        return personYears <= 0.0 ? double.NaN : moved / personYears * 10.0;
    }

    /// <summary>Peak-to-trough drawdowns of at least <paramref name="minDepth"/>
    /// (fraction of the running peak), in series order. Each crash reports the
    /// running-peak turn/value and the trough turn/value that ended it.</summary>
    public static List<(int PeakIndex, long Peak, int TroughIndex, long Trough)> Crashes(
        AutoplayMetrics m, double minDepth)
    {
        var crashes = new List<(int, long, int, long)>();
        int peakIdx = 0, troughIdx = 0;
        long peak = long.MinValue, trough = long.MaxValue;
        bool inDrawdown = false;
        for (int i = 0; i < m.Population.Count; i++)
        {
            long p = m.Population[i];
            if (p >= peak)
            {
                if (inDrawdown && trough <= (long)(peak * (1.0 - minDepth)))
                    crashes.Add((peakIdx, peak, troughIdx, trough));
                peak = p; peakIdx = i; trough = p; troughIdx = i; inDrawdown = false;
            }
            else
            {
                inDrawdown = true;
                if (p < trough) { trough = p; troughIdx = i; }
            }
        }
        if (inDrawdown && trough <= (long)(peak * (1.0 - minDepth)))
            crashes.Add((peakIdx, peak, troughIdx, trough));
        return crashes;
    }

    /// <summary>Final child/adult/elder shares of the final population.</summary>
    public static (double Child, double Adult, double Elder) PyramidShares(AutoplayMetrics m)
    {
        long child = 0, adult = 0, elder = 0;
        for (int c = 0; c < Cohorts.Count; c++)
        {
            long n = m.FinalCohortTotals[c];
            if (BandViews.IsChild(c)) child += n;
            else if (BandViews.IsElder(c)) elder += n;
            else adult += n;
        }
        double total = child + adult + elder;
        return total <= 0.0 ? (double.NaN, double.NaN, double.NaN)
            : (child / total, adult / total, elder / total);
    }

    /// <summary>People per arable km² at the end of the run.</summary>
    public static double DensityPerArableKm2(AutoplayMetrics m) =>
        m.ArableKm2 <= 0.0 ? double.NaN : m.FinalPopulation / m.ArableKm2;

    private static int IndexAtOrBefore(AutoplayMetrics m, double year)
    {
        int found = -1;
        for (int i = 0; i < m.Year.Count; i++) if (m.Year[i] <= year) found = i;
        return found;
    }
}
