using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Catchment;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Worldgen;

// T2.3 acceptance: twin-identical plural worldgen; the partition equals a
// brute-force nearest-by-(travelTime, id) witness on random sampled nodes
// across seeds; zero double-claims by table assertion; siting respects spacing
// across 10 seeds with every site on land, near water, and top-decile by
// score; farmland conservation (per-settlement sums re-aggregate exactly);
// the --settlements plumbing end to end.
public class PluralWorldTests
{
    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    private static WorldState FoundedStepped(ulong seed, out TraversalLattice lattice)
    {
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), seed);
        lattice = TraversalLattice.Build(world.Terrain!);
        var exec = new TurnExecutor(CanonicalEra(), [SystemCatalog.Catchment()]);
        return exec.Step(world);
    }

    [Fact]
    public void PluralWorldgen_TwinIdentical()
    {
        WorldState a = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        WorldState b = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        Assert.Equal(4, a.Settlements.Count);
        Assert.True(WorldStates.StateEquals(a, b));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }

    [Fact]
    public void Partition_EqualsBruteForceWitness_On500SampledNodesAcross3Seeds()
    {
        // THE WITNESS (packet-mandated): for sampled nodes, the partition's
        // claim must equal an INDEPENDENT brute-force computation — one
        // single-source capped Dijkstra PER SETTLEMENT (the pre-T2.3
        // primitive), then nearest by the composite (travel cost, settlement
        // id). Any multi-source relax/tie bug diverges from the witness.
        foreach (ulong seed in new ulong[] { 42, 7, 1234 })
        {
            WorldState world = FoundedStepped(seed, out TraversalLattice lattice);
            int n = lattice.NodeCount;
            int count = world.Settlements.Count;

            // Independent per-settlement cost fields (RelaxCappedFrom is raw
            // terrain — matches the no-network founding state).
            var fields = new double[count][];
            for (int s = 0; s < count; s++)
            {
                fields[s] = new double[n];
                Array.Fill(fields[s], double.PositiveInfinity);
                int origin = LatticeMap.OriginLatticeNode(
                    lattice, world.Terrain!.Size, world.Settlements[s].SiteCell);
                Pathfinder.RelaxCappedFrom(lattice, origin, CatchmentSystem.TravelBudget, fields[s]);
            }

            // Claim table → owner per node (also: zero double-claims).
            var claimed = new int[n];
            Array.Fill(claimed, -1);
            for (int i = 0; i < world.CatchmentNodes.Count; i++)
            {
                CatchmentNodeRow row = world.CatchmentNodes[i];
                Assert.True(claimed[row.LatticeNode] < 0,
                    $"seed {seed}: node {row.LatticeNode} claimed twice");
                claimed[row.LatticeNode] = row.Settlement.Value;
            }

            // ≥500 deterministic pseudo-random samples (fixed LCG — no RNG law
            // concerns in tests, but keep it reproducible).
            ulong lcg = seed * 6364136223846793005UL + 1442695040888963407UL;
            int sampled = 0, claimedSamples = 0;
            while (sampled < 500)
            {
                lcg = lcg * 6364136223846793005UL + 1442695040888963407UL;
                int node = (int)(lcg >> 33) % n;
                sampled++;

                int bestOwner = -1;
                double bestCost = double.PositiveInfinity;
                for (int s = 0; s < count; s++)
                {
                    double c = fields[s][node];
                    if (c > CatchmentSystem.TravelBudget) continue;
                    // Composite (cost, settlement id): strictly better, or
                    // equal cost and lower id (ascending s makes id implicit).
                    if (c < bestCost) { bestCost = c; bestOwner = s; }
                }
                Assert.Equal(bestOwner, claimed[node]);
                if (bestOwner >= 0) claimedSamples++;
            }
            Assert.True(claimedSamples >= 50,
                $"seed {seed}: only {claimedSamples}/500 samples were claimed — witness vacuous");
        }
    }

    [Fact]
    public void Partition_FarmlandConservation_PerSettlementSumsReaggregateExactly()
    {
        // "No double-counted land" made arithmetic: recompute each
        // settlement's farmland INDEPENDENTLY from the claim table in the
        // system's own accumulation order (ascending node id) — bit-exact per
        // settlement — and the union of claims covers each node exactly once,
        // so the total is the single flat sum over all claimed nodes.
        WorldState world = FoundedStepped(42, out TraversalLattice lattice);

        var perSettlement = new double[world.Settlements.Count];
        var seen = new HashSet<int>();
        for (int i = 0; i < world.CatchmentNodes.Count; i++)
        {
            CatchmentNodeRow row = world.CatchmentNodes[i];
            Assert.True(seen.Add(row.LatticeNode), "double-claimed node");
            perSettlement[row.Settlement.Value] +=
                LatticeMap.BlockFertility(world.Terrain!, lattice, row.LatticeNode);
        }
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(perSettlement[s]),
                BitConverter.DoubleToInt64Bits(world.CatchmentSummaries[s].EffectiveFarmland));
            Assert.True(world.CatchmentSummaries[s].NodeCount > 0,
                $"settlement {s} owns no land — partition degenerate");
        }
    }

    [Fact]
    public void Siting_TenSeeds_SpacingRespected_OnLandNearWater_TopDecileByScore()
    {
        // Spacing + quality across 10 dev seeds (N = 4). QUALITY CRITERION,
        // stated honestly: each site is in the top decile of the SITING SCORE
        // (fertility × water access — the quantity the argmax actually
        // maximizes) over ALL land candidates. Later picks are spacing-
        // constrained, so raw-fertility top-decile is NOT guaranteed — score
        // top-decile held empirically on every seed tested and is asserted;
        // if terrain ever forces a lower-ranked pick the criterion (not the
        // mechanism) is what must be renegotiated.
        WorldgenConfig cfg = TestConfigs.DevWorldgen();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            TerrainSet terrain = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            int[] sites = SettlementSiting.ChooseSites(terrain, cfg.Siting, 4);
            var lattice = TraversalLattice.Build(terrain);

            // Pairwise travel-time spacing ≥ the configured minimum.
            for (int i = 0; i < sites.Length; i++)
            {
                var field = new double[lattice.NodeCount];
                Array.Fill(field, double.PositiveInfinity);
                Pathfinder.RelaxCappedFrom(lattice,
                    LatticeMap.OriginLatticeNode(lattice, terrain.Size, sites[i]),
                    cfg.Siting.MinSpacingTravel, field);
                for (int j = i + 1; j < sites.Length; j++)
                {
                    int nodeJ = LatticeMap.OriginLatticeNode(lattice, terrain.Size, sites[j]);
                    Assert.True(field[nodeJ] >= cfg.Siting.MinSpacingTravel
                        || double.IsPositiveInfinity(field[nodeJ]),
                        $"seed {seed}: sites {i},{j} at travel {field[nodeJ]} < {cfg.Siting.MinSpacingTravel}");
                }
            }

            // Land, near water, top score decile.
            double[] scores = SiteScores(terrain, cfg.Siting, out int landCandidates);
            var sorted = (double[])scores.Clone();
            Array.Sort(sorted);
            double p90 = sorted[(int)(sorted.Length * 0.9)];
            foreach (int site in sites)
            {
                Assert.True(terrain.Water[site] < 0.5, $"seed {seed}: site on water");
                double access = ScoreOf(terrain, cfg.Siting, site, out bool nearWater);
                Assert.True(nearWater, $"seed {seed}: site {site} not near water (access 0)");
                Assert.True(access >= p90,
                    $"seed {seed}: site score {access} below the top decile ({p90})");
            }
        }
    }

    private static double[] SiteScores(TerrainSet terrain, SitingConfig cfg, out int landCandidates)
    {
        var scores = new List<double>();
        for (int i = 0; i < terrain.Fertility.Length; i++)
        {
            if (terrain.Water[i] >= 0.5) continue;
            scores.Add(ScoreOf(terrain, cfg, i, out _));
        }
        landCandidates = scores.Count;
        return [.. scores];
    }

    private static double ScoreOf(TerrainSet terrain, SitingConfig cfg, int cell, out bool nearWater)
    {
        // Recompute the siting score independently (BFS distance via a local
        // scan is exact for the assertion's purposes: reuse the public single-
        // site chooser contract — access > 0 ⇔ within the cutoff).
        int size = terrain.Size;
        int cx = cell % size, cy = cell / size;
        int best = int.MaxValue;
        int r = cfg.WaterAccessCutoffPx;
        for (int y = Math.Max(0, cy - r); y <= Math.Min(size - 1, cy + r); y++)
        {
            for (int x = Math.Max(0, cx - r); x <= Math.Min(size - 1, cx + r); x++)
            {
                if (terrain.Water[y * size + x] < 0.5) continue;
                int d = Math.Abs(x - cx) + Math.Abs(y - cy); // BFS distance is 4-neighbour
                if (d < best) best = d;
            }
        }
        double access = best == int.MaxValue ? 0.0 : Math.Max(0.0, 1.0 - best / (double)r);
        nearWater = access > 0.0;
        return terrain.Fertility[cell] * access;
    }

    [Fact]
    public void SettlementsFlag_EndToEnd_HeadlessAndCanonicalAgree()
    {
        // D-029 plumbing: N = 2 through the headless recipe equals the
        // canonical recipe with the same override, and founds exactly 2.
        WorldState viaFlag = Sim.Cli.HeadlessFounding.Found(
            42, sizeOverridePx: 256, settlementsOverride: 2);
        WorldgenConfig wg = TestConfigs.Worldgen() with { SizePx = 256 };
        WorldState canonical = WorldFounding.Found(wg, TestConfigs.Sim(), 42, settlementsOverride: 2);
        Assert.Equal(2, viaFlag.Settlements.Count);
        Assert.Equal(WorldHash.ComputeHex(canonical), WorldHash.ComputeHex(viaFlag));
    }
}
