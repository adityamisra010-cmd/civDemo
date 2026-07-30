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
        var exec = new TurnExecutor(CanonicalEra(), [SystemCatalog.Catchment(TestConfigs.Sim())]);
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
            // terrain — matches the no-network founding state). T3.8: budgets
            // are PER SETTLEMENT via the system's own pure tier functions on
            // the founded housing rows (Prev for the catchment step) — the
            // witness's independence is the Dijkstra + composite-nearest
            // computation, not the budget derivation, exactly as the base
            // budget already came from TravelBudgetCostUnits.
            var sim = TestConfigs.Sim();
            var budgets = new double[count];
            var tiers = new int[count];
            var fields = new double[count][];
            for (int s = 0; s < count; s++)
            {
                int tier = 0;
                for (int h = 0; h < world.Housing.Count; h++)
                    if (world.Housing[h].Settlement == world.Settlements[s].Id)
                    {
                        tier = CatchmentSystem.SizeTier(
                            world.Housing[h].Dwellings.Value, sim.Catchment.SizeDwellingsRef);
                        break;
                    }
                tiers[s] = tier;
                budgets[s] = CatchmentSystem.TierBudget(sim, lattice, tier);
                fields[s] = new double[n];
                Array.Fill(fields[s], double.PositiveInfinity);
                int origin = LatticeMap.OriginLatticeNode(
                    lattice, world.Terrain!.Size, world.Settlements[s].SiteCell);
                Pathfinder.RelaxCappedFrom(lattice, origin, budgets[s], fields[s]);
            }
            // The T3.8 record's discriminating diagnostic: state whether this
            // world actually exercises UNEQUAL budgets (all-equal tiers make
            // the sweep blind to per-budget semantics; the constructed pin in
            // PartitionUnequalBudgetTests covers that case by construction).
            Console.WriteLine($"seed {seed}: founding tiers [{string.Join(",", tiers)}]");

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

            // T3.8: FULL-lattice sweep. The 500-draw LCG existed to bound the
            // comparison cost, but the expensive part is the fields — already
            // computed above — and the comparison is O(n·count), so every node
            // is checked. Strictly stronger than sampling; the T3.2b ≥50
            // claimed-comparison bar is kept as the non-vacuousness floor.
            int claimedTotal = 0;
            for (int node = 0; node < n; node++)
            {
                int bestOwner = -1;
                double bestCost = double.PositiveInfinity;
                for (int s = 0; s < count; s++)
                {
                    double c = fields[s][node];
                    if (c > budgets[s]) continue;
                    // Composite (cost, settlement id): strictly better, or
                    // equal cost and lower id (ascending s makes id implicit).
                    if (c < bestCost) { bestCost = c; bestOwner = s; }
                }
                Assert.Equal(bestOwner, claimed[node]);
                if (bestOwner >= 0) claimedTotal++;
            }
            // Non-vacuousness, recalibrated for the sweep: the T3.2b ≥50 bar
            // counted claimed DRAWS with duplicates over a ~40-node claimed
            // set (measured: 44 distinct at seed 42); the sweep covers every
            // claimed node exactly once, so the completeness identity replaces
            // the draw count — the witness's claimed total must equal the
            // system's row count exactly (bidirectional: no phantom claims,
            // no unwitnessed rows) — plus a floor that each settlement claims
            // on average at least a couple of nodes.
            Assert.Equal(world.CatchmentNodes.Count, claimedTotal);
            Assert.True(claimedTotal >= 2 * count,
                $"seed {seed}: only {claimedTotal} claimed nodes for {count} settlements — witness vacuous");
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
                LatticeMap.BlockArableKm2(world.Terrain!, lattice, row.LatticeNode);
        }
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(perSettlement[s]),
                BitConverter.DoubleToInt64Bits(world.CatchmentSummaries[s].EffectiveArableKm2));
            Assert.True(world.CatchmentSummaries[s].NodeCount > 0,
                $"settlement {s} owns no land — partition degenerate");
        }
    }

    [Fact]
    public void Siting_TenSeeds_SpacingRespected_OnLandNearWater_TopDecileByScore()
    {
        // Spacing + quality across 10 dev seeds (N = 4). QUALITY CRITERION,
        // AMENDED AT T3.1 exactly as this header anticipated ("if terrain
        // ever forces a lower-ranked pick the criterion is what must be
        // renegotiated"): the argmax objective is now fertility × water-or-
        // RIVER access × the seeded score JITTER (founding variation), so
        // top-decile is asserted over THAT distribution — the quantity the
        // argmax actually maximizes — with the same seed the chooser used.
        WorldgenConfig cfg = TestConfigs.DevWorldgen();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            TerrainSet terrain = Sim.Core.Worldgen.Worldgen.Generate(cfg, seed);
            int[] sites = SettlementSiting.ChooseSites(terrain, cfg.Siting, 4, seed);
            var lattice = TraversalLattice.Build(terrain);

            // Pairwise travel-time spacing ≥ the configured minimum.
            for (int i = 0; i < sites.Length; i++)
            {
                var field = new double[lattice.NodeCount];
                Array.Fill(field, double.PositiveInfinity);
                double minSpacing = LatticeGeometry.CostUnitsForIdealGroundKm(
                    lattice, cfg.Siting.MinSpacingKm);
                Pathfinder.RelaxCappedFrom(lattice,
                    LatticeMap.OriginLatticeNode(lattice, terrain.Size, sites[i]),
                    minSpacing, field);
                for (int j = i + 1; j < sites.Length; j++)
                {
                    int nodeJ = LatticeMap.OriginLatticeNode(lattice, terrain.Size, sites[j]);
                    Assert.True(field[nodeJ] >= minSpacing
                        || double.IsPositiveInfinity(field[nodeJ]),
                        $"seed {seed}: sites {i},{j} at travel {field[nodeJ]} < {minSpacing}");
                }
            }

            // Land, near water, and the T3.1 STRUCTURAL quality guarantee:
            // every site's UNJITTERED score sits at or above the configured
            // floor percentile of all positive-score candidates (the jitter
            // varies WHICH good site wins, never admits a bad one), and the
            // FIRST pick is the exact argmax of the jittered objective (later
            // picks are spacing-constrained). CandidateScores is the
            // chooser's own raster — single source of truth.
            double[] unjittered = SettlementSiting.CandidateScores(terrain, cfg.Siting);
            var positive = new List<double>();
            for (int i = 0; i < unjittered.Length; i++)
                if (unjittered[i] > 0.0) positive.Add(unjittered[i]);
            positive.Sort();
            double floor = positive[Math.Min(positive.Count - 1,
                (int)(positive.Count * cfg.Siting.ScoreFloorPercentile))];
            double bestJittered = 0.0;
            for (int i = 0; i < unjittered.Length; i++)
                if (unjittered[i] >= floor)
                    bestJittered = Math.Max(bestJittered,
                        unjittered[i] * SettlementSiting.Jitter(seed, i, cfg.Siting.ScoreJitter));
            foreach (int site in sites)
            {
                Assert.True(terrain.Water[site] < 0.5, $"seed {seed}: site on water");
                Assert.True(unjittered[site] > 0.0, $"seed {seed}: site {site} not near water (access 0)");
                Assert.True(unjittered[site] >= floor,
                    $"seed {seed}: site score {unjittered[site]} below the floor {floor}");
            }
            double firstScore = unjittered[sites[0]]
                                * SettlementSiting.Jitter(seed, sites[0], cfg.Siting.ScoreJitter);
            Assert.Equal(bestJittered, firstScore);
        }
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
