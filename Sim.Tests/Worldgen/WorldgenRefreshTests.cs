using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Xunit;

namespace Sim.Tests.Worldgen;

/// <summary>
/// T3.1 — WORLDGEN REFRESH (m3-spec §4), the packet's acceptance criteria as
/// pins: (a) water access credits river adjacency, so river-adjacent INLAND
/// sites occur; (b) the edge taper guarantees an ocean margin — no land at
/// the world edge, ever; (c) founding variation — endowments differ
/// measurably seed-to-seed and the T2.4 lockstep signature (all settlements
/// crossing the artisan threshold within one decade) provably breaks.
/// MEASURED PRE-CHANGE (the defect being closed, seed 42 canonical): 11 of
/// 12 settlements' Artisans emerged in the SAME year (40); every settlement
/// founded as an identical 400-person / 6000-food copy across every seed;
/// 185 land cells sat on the eastern boundary column.
/// </summary>
public class WorldgenRefreshTests
{
    private static WorldgenConfig Wg()
    {
        using var s = Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(s);
    }

    private static SimConfig SimCfg()
    {
        using var s = Sim.Data.DataFiles.OpenSim();
        using var n = Sim.Data.DataFiles.OpenNeeds();
        using var g = Sim.Data.DataFiles.OpenGoods();
        return SimConfigLoader.Load(s, n, g);
    }

    /// <summary>The 256² dev preset (D-015) — 10-seed sweeps stay affordable.</summary>
    private static WorldgenConfig Dev() => Wg() with
    {
        SizePx = 256,
        ContinentalMask = Wg().ContinentalMask with { EdgeTaperPx = 12 },
        Siting = Wg().Siting with { SettlementCount = 4, MinSpacingKm = 240.0 },
    };

    [Fact]
    public void NoLandAtTheWorldEdge_TenSeeds()
    {
        // T3.1(b): the smoothstep edge taper crushes boundary elevation to
        // exactly 0, below any non-degenerate quantile sea level — so the
        // world edge is ocean by construction. Ten seeds, every boundary cell.
        WorldgenConfig cfg = Dev();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            int n = t.Size;
            ReadOnlySpan<double> water = t.Water;
            for (int i = 0; i < n; i++)
            {
                Assert.True(water[i] >= 0.5, $"seed {seed}: LAND at north boundary x={i}");
                Assert.True(water[(n - 1) * n + i] >= 0.5, $"seed {seed}: LAND at south boundary x={i}");
                Assert.True(water[i * n] >= 0.5, $"seed {seed}: LAND at west boundary y={i}");
                Assert.True(water[i * n + n - 1] >= 0.5, $"seed {seed}: LAND at east boundary y={i}");
            }
        }
    }

    [Fact]
    public void RiverAdjacentInlandSites_OccurAcrossTenSeeds()
    {
        // T3.1(a): with the access BFS seeded from riverbanks, fertile
        // interior river valleys can win the siting argmax. ACROSS ten seeds
        // at the canonical 12-settlement count, at least some chosen sites
        // must be river-adjacent AND genuinely inland (farther from the SEA
        // than the access cutoff — a site the old sea-only scoring gave zero
        // access and could never pick).
        WorldgenConfig cfg = Wg();
        int inlandRiverine = 0, total = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            int n = t.Size;
            int[] seaDist = GridDistance(t.Water, default, n);
            int[] riverDist = GridDistance(t.Water, t.Rivers, n, riversOnly: true);
            int[] sites = SettlementSiting.ChooseSites(t, cfg.Siting, cfg.Siting.SettlementCount, seed);
            foreach (int site in sites)
            {
                total++;
                if (riverDist[site] <= cfg.Rivers.AdjacencyRadiusPx
                    && seaDist[site] > cfg.Siting.WaterAccessCutoffPx)
                    inlandRiverine++;
            }
        }
        Assert.True(inlandRiverine > 0,
            $"no river-adjacent inland site among {total} sites across 10 seeds — " +
            "the missing settlement type is still missing");
    }

    [Fact]
    public void FoundingEndowments_DifferMeasurably_SeedToSeed_AndSettlementToSettlement()
    {
        // T3.1(c): the jittered endowment — settlements must no longer found
        // as identical copies. Both axes checked: across settlements within
        // one world, and across seeds for the same settlement id.
        WorldgenConfig wg = Wg();
        SimConfig cfg = SimCfg();
        WorldState w42 = WorldFounding.Found(wg, cfg, 42);
        var pops = new List<long>();
        var foods = new List<long>();
        for (int s = 0; s < w42.Settlements.Count; s++)
        {
            long pop = 0;
            for (int b = 0; b < w42.Buckets.Count; b++)
                if (w42.Buckets[b].Settlement.Value == s) pop += w42.Buckets[b].Count.Value;
            pops.Add(pop);
            foods.Add(w42.GoodStocks[
                GoodStockIndex.IndexOf(w42.GoodStocks, new SettlementId(s), new GoodId(cfg.Goods!.GrainId))
            ].Amount.Value);
        }
        Assert.True(pops.Distinct().Count() >= w42.Settlements.Count / 2,
            $"populations barely vary within one world: {string.Join(",", pops)}");
        Assert.True(foods.Distinct().Count() >= w42.Settlements.Count / 2,
            $"food stores barely vary within one world: {string.Join(",", foods)}");

        WorldState w7 = WorldFounding.Found(wg, cfg, 7);
        long s0Pop42 = pops[0], s0Pop7 = 0;
        for (int b = 0; b < w7.Buckets.Count; b++)
            if (w7.Buckets[b].Settlement.Value == 0) s0Pop7 += w7.Buckets[b].Count.Value;
        Assert.NotEqual(s0Pop42, s0Pop7);
    }

    [Fact]
    public void Founding_IsTwinIdentical()
    {
        // Same seed, twice → byte-identical world (jitter is pure hashing,
        // no RNG stream): the T3.1 accept criterion restated on the new code.
        WorldgenConfig wg = Wg();
        SimConfig cfg = SimCfg();
        WorldState a = WorldFounding.Found(wg, cfg, 123);
        WorldState b = WorldFounding.Found(wg, cfg, 123);
        Assert.Equal(
            WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }

    [Fact]
    public void LockstepSignature_ProvablyBreaks_ArtisanEmergenceSpreads()
    {
        // T3.1(c) THE LOCKSTEP BREAK. MEASURED PRE-CHANGE (identical
        // endowments): 11 of 12 settlements' Artisans emerged in the SAME
        // year (40) — "all settlements crossing a class threshold within one
        // decade", the T2.4 signature. With founding variation the crossing
        // years must SPREAD: no single crossing year may hold more than half
        // the settlements, and at least three distinct crossing years must
        // occur within the horizon.
        SimConfig cfg = SimCfg();
        WorldgenConfig wg = Wg();
        EraTable era;
        using (var s = Sim.Data.DataFiles.OpenEraPacing()) era = EraTableLoader.Load(s);
        TurnExecutor exec;
        using (var s = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(era, PipelineLoader.Load(s, SystemCatalog.All(cfg)));

        WorldState world = WorldFounding.Found(wg, cfg, 42);
        int n = world.Settlements.Count;
        var emerged = new double[n];
        Array.Fill(emerged, double.NaN);
        for (int t = 0; t < 120; t++)
        {
            world = exec.Step(world);
            for (int i = 0; i < world.ClassStates.Count; i++)
            {
                ClassStateRow row = world.ClassStates[i];
                if (row.Class.Value != 2 || row.Active == 0) continue;
                if (double.IsNaN(emerged[row.Settlement.Value]))
                    emerged[row.Settlement.Value] = world.Clock.WorldDateYears;
            }
        }
        double[] years = emerged.Where(v => !double.IsNaN(v)).ToArray();
        Assert.True(years.Length >= n / 2,
            $"only {years.Length}/{n} settlements ever emerged artisans within the horizon");
        int largestCluster = years.GroupBy(y => y).Max(g => g.Count());
        int distinct = years.Distinct().Count();
        Assert.True(largestCluster <= n / 2,
            $"{largestCluster} of {n} settlements crossed in the SAME year — the lockstep survives " +
            $"(years: {string.Join(",", years.Select(y => y.ToString("F0")))})");
        Assert.True(distinct >= 3,
            $"only {distinct} distinct crossing years — the lockstep survives " +
            $"(years: {string.Join(",", years.Select(y => y.ToString("F0")))})");
    }

    [Fact]
    public void AbsurdEndowmentConfig_ThrowsTheNamedOverflow_NeverWraps()
    {
        // M3 OVERFLOW DISCIPLINE (director-raised): a deliberately absurd
        // endowment — cohort counts near long.MaxValue with jitter scaling
        // them PAST it — must produce the loud named FlowOverflowException,
        // never a wrapped negative stock. (The Ledger chokepoint was already
        // checked — T0.6; this pins the INTERMEDIATE arithmetic on the way
        // there.)
        WorldgenConfig wg = Wg() with
        {
            SizePx = 64,
            ContinentalMask = Wg().ContinentalMask with { EdgeTaperPx = 4 },
            Siting = Wg().Siting with { SettlementCount = 1, MinSpacingKm = 0.0 },
        };
        var absurd = new long[Cohorts.Count];
        Array.Fill(absurd, long.MaxValue / 2);
        SimConfig cfg = SimCfg() with
        {
            Founding = new FoundingConfig(absurd, FoodStore: long.MaxValue - 5,
                EndowmentJitter: 0.9),
        };
        var ex = Assert.ThrowsAny<Exception>(() => WorldFounding.Found(wg, cfg, 3));
        Assert.True(ex is FlowOverflowException or LedgerOverflowException,
            $"expected the named overflow exception, got {ex.GetType().Name}: {ex.Message}");
    }

    /// <summary>Multi-source BFS from sea (or river) cells — test-local mirror
    /// of the siting helper, for classifying chosen sites.</summary>
    private static int[] GridDistance(
        ReadOnlySpan<double> water, ReadOnlySpan<double> rivers, int size, bool riversOnly = false)
    {
        int cells = size * size;
        var dist = new int[cells];
        var queue = new int[cells];
        int head = 0, tail = 0;
        for (int i = 0; i < cells; i++)
        {
            bool src = riversOnly
                ? rivers.Length == cells && rivers[i] >= 0.5
                : water[i] >= 0.5;
            if (src) { dist[i] = 0; queue[tail++] = i; }
            else dist[i] = int.MaxValue;
        }
        while (head < tail)
        {
            int i = queue[head++];
            int x = i % size, y = i / size, d = dist[i] + 1;
            if (x > 0 && dist[i - 1] == int.MaxValue) { dist[i - 1] = d; queue[tail++] = i - 1; }
            if (x < size - 1 && dist[i + 1] == int.MaxValue) { dist[i + 1] = d; queue[tail++] = i + 1; }
            if (y > 0 && dist[i - size] == int.MaxValue) { dist[i - size] = d; queue[tail++] = i - size; }
            if (y < size - 1 && dist[i + size] == int.MaxValue) { dist[i + size] = d; queue[tail++] = i + size; }
        }
        return dist;
    }
}
