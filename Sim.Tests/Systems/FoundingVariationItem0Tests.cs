using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.6b ITEM 0 — MEASURE BEFORE YOU DESIGN (directed packet, packet one;
/// nothing else starts until this reports). On the CANONICAL founded world
/// (1024², N = 12), across 5 seeds:
///   (a) terrain spread across the twelve SITES vs across the CONTINENT —
///       if the sites are far more alike than the land they were chosen
///       from, top-score siting is the cause, measured not guessed;
///   (b) production bundle spread under the identical shipped allocation;
///   (c) price spread per good and distance-to-deadband for every pair —
///       the number that says whether divergence needs 10% or 10×;
///   (d) endowment spread at founding (endowmentJitter = 0.25 already
///       ships — the T2.13 lockstep baseline predates it, so the baseline
///       is REMEASURED here, never recalled);
///   plus the P1 baseline: first-Artisans-emergence turn per settlement.
/// Writes the full table to /tmp/t36b-item0.txt; the review record quotes
/// it. Runtime is ~5 min/seed (worldgen 1024² + 150 canonical turns), so
/// this rig is Skip-gated after the measurement — reproducible on demand,
/// not a CI cost. The recorded numbers stand as the packet's evidence.
/// </summary>
public class FoundingVariationItem0Tests
{
    private static readonly ulong[] Seeds = [42, 7, 101, 202, 303];
    private const int Horizon = 150; // 1,500 years at the Neolithic dt

    [Fact(Skip = "T3.6b Item 0 measurement rig (~30 min: 5 canonical worlds x 150 turns) — run manually to reproduce docs/t3.6b-review-record.md; recorded numbers are the packet's evidence")]
    public void Item0_MeasureTheWorldBeforeDesigningAnything()
    {
        SimConfig cfg = TestConfigs.Sim();
        var report = new System.Text.StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        foreach (ulong seed in Seeds)
        {
            WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, seed);
            TerrainSet terrain = world.Terrain!;
            int n = world.Settlements.Count;

            // ---- (a) terrain spread: sites vs continent ------------------
            var siteFert = new double[n];
            var siteMoist = new double[n];
            var siteElev = new double[n];
            int size = (int)Math.Sqrt(terrain.Fertility.Length);
            for (int s = 0; s < n; s++)
            {
                int cell = world.Settlements[s].SiteCell;
                siteFert[s] = terrain.Fertility[cell];
                siteMoist[s] = terrain.Moisture[cell];
                siteElev[s] = terrain.Elevation[cell];
            }
            (double fLandMean, double fLandCv) = LandStats(terrain, terrain.Fertility);
            (double mLandMean, double mLandCv) = LandStats(terrain, terrain.Moisture);
            report.Append(inv, $"seed {seed} (a) site fertility {Stats(siteFert)} | land fertility mean={fLandMean:F4} cv={fLandCv:F3}\n");
            report.Append(inv, $"seed {seed} (a) site moisture  {Stats(siteMoist)} | land moisture  mean={mLandMean:F4} cv={mLandCv:F3}\n");
            report.Append(inv, $"seed {seed} (a) site elevation {Stats(siteElev)}\n");

            // deposits per settlement, per deposit good (rolled at founding)
            foreach (GoodEntry g in cfg.Goods!.Goods)
            {
                if (g.DepositChannel is null) continue;
                var ab = new double[n];
                for (int i = 0; i < world.Deposits.Count; i++)
                    if (world.Deposits[i].Good.Value == g.Id)
                        ab[world.Deposits[i].Settlement.Value] = world.Deposits[i].Abundance;
                report.Append(inv, $"seed {seed} (a) deposit {g.Name,-10} {Stats(ab)}\n");
            }

            // ---- (d) endowment spread at founding ------------------------
            var pop = new double[n];
            var food = new double[n];
            for (int i = 0; i < world.Buckets.Count; i++)
                pop[world.Buckets[i].Settlement.Value] += world.Buckets[i].Count.Value;
            for (int i = 0; i < world.GoodStocks.Count; i++)
                if (world.GoodStocks[i].Good.Value == cfg.Goods.GrainId)
                    food[world.GoodStocks[i].Settlement.Value] += world.GoodStocks[i].Amount.Value;
            report.Append(inv, $"seed {seed} (d) founding pop   {Stats(pop)}\n");
            report.Append(inv, $"seed {seed} (d) founding grain {Stats(food)}\n");

            // ---- run the canonical pipeline, tracking P1 + (b) + (c) -----
            using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
            using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
            var executor = new TurnExecutor(
                EraTableLoader.Load(eraStream),
                PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)));

            var emergence = new long[n];
            Array.Fill(emergence, -1L);
            for (int t = 1; t <= Horizon; t++)
            {
                world = executor.Step(world);
                for (int i = 0; i < world.ClassStates.Count; i++)
                {
                    ClassStateRow row = world.ClassStates[i];
                    if (row.Class.Value == 2 && row.Active != 0
                        && emergence[row.Settlement.Value] < 0)
                        emergence[row.Settlement.Value] = t;
                }

                if (t == 50) // year 500: production bundles + artisan counts
                {
                    foreach (GoodEntry g in cfg.Goods.Goods)
                    {
                        var prod = new double[n];
                        for (int i = 0; i < world.GoodStocks.Count; i++)
                            if (world.GoodStocks[i].Good.Value == g.Id)
                                prod[world.GoodStocks[i].Settlement.Value] = world.GoodStocks[i].LastProducedUnits;
                        if (Mean(prod) > 0.0)
                            report.Append(inv, $"seed {seed} (b) t50 produced {g.Name,-10} {Stats(prod)}\n");
                    }
                    var artisans = new double[n];
                    for (int i = 0; i < world.Buckets.Count; i++)
                        if (world.Buckets[i].Class.Value == 2)
                            artisans[world.Buckets[i].Settlement.Value] += world.Buckets[i].Count.Value;
                    report.Append(inv, $"seed {seed} (P1) t50 artisan counts {Stats(artisans)}\n");
                }
            }

            report.Append(inv, $"seed {seed} (P1) artisan emergence turns: [{string.Join(", ", emergence)}]\n");

            // ---- (c) price spread + deadband distance at the horizon -----
            foreach (GoodEntry g in cfg.Goods.Goods)
            {
                if (g.Id == cfg.Goods.GrainId) continue;
                var price = new double[n];
                for (int i = 0; i < world.Prices.Count; i++)
                    if (world.Prices[i].Good.Value == g.Id)
                        price[world.Prices[i].Settlement.Value] = world.Prices[i].Price;
                double bestRatio = 0.0;
                double bestGap = 0.0, bestThr = 0.0;
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        double cost = double.PositiveInfinity;
                        for (int d = 0; d < world.SettlementDistances.Count; d++)
                            if (world.SettlementDistances[d].From.Value == i
                                && world.SettlementDistances[d].To.Value == j)
                            { cost = world.SettlementDistances[d].TravelCost; break; }
                        if (double.IsInfinity(cost)) continue;
                        double gap = Math.Abs(price[i] - price[j]);
                        double thr = g.BulkPerUnit * cost * cfg.Trade.CostPerBulkCostUnit;
                        double ratio = thr > 0 ? gap / thr : 0.0;
                        if (ratio > bestRatio) { bestRatio = ratio; bestGap = gap; bestThr = thr; }
                    }
                report.Append(inv, $"seed {seed} (c) t{Horizon} {g.Name,-10} price {Stats(price)} bestPair gap={bestGap:F4} thr={bestThr:F4} gap/thr={bestRatio:F3}\n");
            }
            report.Append('\n');
        }

        System.IO.File.WriteAllText("/tmp/t36b-item0.txt", report.ToString());
        Assert.True(report.Length > 0);
    }

    private static double Mean(double[] v)
    {
        double sum = 0; foreach (double x in v) sum += x; return v.Length > 0 ? sum / v.Length : 0;
    }

    private static string Stats(double[] v)
    {
        double mean = Mean(v);
        double min = double.MaxValue, max = double.MinValue, sq = 0;
        foreach (double x in v) { if (x < min) min = x; if (x > max) max = x; sq += (x - mean) * (x - mean); }
        double sd = v.Length > 1 ? Math.Sqrt(sq / (v.Length - 1)) : 0;
        double cv = mean != 0 ? sd / Math.Abs(mean) : 0;
        return string.Create(CultureInfo.InvariantCulture,
            $"min={min:F4} max={max:F4} mean={mean:F4} cv={cv:F3}");
    }

    private static (double Mean, double Cv) LandStats(TerrainSet terrain, ReadOnlySpan<double> field)
    {
        double sum = 0; long count = 0;
        for (int i = 0; i < field.Length; i++)
        {
            if (terrain.Water[i] >= 0.5) continue;
            sum += field[i]; count++;
        }
        double mean = sum / count;
        double sq = 0;
        for (int i = 0; i < field.Length; i++)
        {
            if (terrain.Water[i] >= 0.5) continue;
            sq += (field[i] - mean) * (field[i] - mean);
        }
        double sd = Math.Sqrt(sq / (count - 1));
        return (mean, mean != 0 ? sd / mean : 0);
    }
}
