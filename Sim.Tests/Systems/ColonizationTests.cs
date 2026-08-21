using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.4 — COLONIZATION FROM BELOW (D-037 B1). Population with nowhere viable to go
/// departs into unclaimed land and founds a new settlement.
///
/// THE TWO REJECTION RULES ARE TESTED SEPARATELY THROUGHOUT, because the director
/// ruled them distinct: minimum TRAVEL-COST SPACING (ADR-018's floor, a physical
/// settlement-distinctness rule) and exact SITE-CELL OCCUPANCY. Spacing is evaluated
/// on the candidate's ORIGIN LATTICE NODE (stride 4, ~16 km) while sites are ~4 km
/// cells, so the spacing test alone cannot be relied on to keep site cells distinct —
/// turn-zero siting gets that only incidentally. Neither is a catchment rule.
/// </summary>
public class ColonizationTests
{
    private static TurnExecutor FoundedExecutor()
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream,
                SystemCatalog.All(TestConfigs.Sim(), TestConfigs.Worldgen())));
    }

    private static WorldState Founded(ulong seed = 1UL) =>
        WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), seed);

    private static long Population(WorldState w)
    {
        long p = 0;
        for (int i = 0; i < w.Buckets.Count; i++) p += w.Buckets[i].Count.Value;
        return p;
    }

    private static long PopulationOf(WorldState w, SettlementId s)
    {
        long p = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == s) p += w.Buckets[i].Count.Value;
        return p;
    }

    // --- A. THE TRIGGER ------------------------------------------------------

    [Fact]
    public void NoEligiblePopulation_NoFounding()
    {
        // The trigger is PREV ConsumptionDeficitRow.DeficitRatio > 0 — the same row
        // and the same read T4.5's appropriation uses. A world where nobody is short
        // of food colonises nothing, however much empty land there is.
        WorldState w = Founded();
        Assert.Equal(0, w.ConsumptionDeficits.Count);  // founding writes no deficits
        int before = w.Settlements.Count;

        w = FoundedExecutor().Step(w);

        Assert.Equal(before, w.Settlements.Count);
    }

    [Fact]
    public void ADeficitSettlement_FoundsExactlyOneDaughter_PerTurn()
    {
        // One founding per eligible source per turn is the structural bound: the
        // party is drawn once per source. Not a magic per-turn cap.
        WorldState w = Founded();
        SettlementId src = w.Settlements[0].Id;
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(src, 0.20, 1000));
        int before = w.Settlements.Count;

        w = FoundedExecutor().Step(w);

        Assert.Equal(before + 1, w.Settlements.Count);
    }

    // --- B. CONSERVATION (law 1) --------------------------------------------

    [Fact]
    public void FoundingConservesPopulationEXACTLY_NothingIsMinted()
    {
        WorldState w = Founded();
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[0].Id, 0.25, 1000));
        long before = Population(w);

        w = FoundedExecutor().Step(w);

        // The whole-world audit, exact and with no epsilon.
        Assert.True(ConservationAuditor.IsConserved(w, out string report), report);
        // And the population only moved — demographics may also have run, so the
        // check that matters is that the ledger reconciles, above.
        Assert.True(Population(w) > 0, $"world emptied: {before}");
    }

    [Fact]
    public void TheFoundingPartyIsTAKENFromTheSource_NotCreated()
    {
        WorldState w = Founded();
        SettlementId src = w.Settlements[0].Id;
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(src, 0.30, 1000));
        long srcBefore = PopulationOf(w, src);

        // Colonization ALONE, so demographics cannot confound the arithmetic.
        var exec = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]);
        w = exec.Step(w);

        var daughter = new SettlementId(w.Settlements[^1].Id.Value);
        long moved = PopulationOf(w, daughter);
        Assert.True(moved > 0, "no party moved");
        Assert.Equal(srcBefore - moved, PopulationOf(w, src));
    }

    [Fact]
    public void MigrationAndFoundingCannotDoubleSpendTheSamePeople()
    {
        // Colonization reads LIVE post-migration buckets. Migration's overdraw
        // scaler caps outflow at the bucket's PREV count, so a later system
        // reading PREV would offer people migration had already moved. Running
        // the two together must still conserve exactly and must not drive any
        // bucket negative.
        WorldState w = Founded();
        for (int i = 0; i < w.Settlements.Count; i++)
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[i].Id, 0.40, 1000));

        w = FoundedExecutor().Step(w);

        Assert.True(ConservationAuditor.IsConserved(w, out string report), report);
        for (int i = 0; i < w.Buckets.Count; i++)
            Assert.True(w.Buckets[i].Count.Value >= 0, $"bucket {i} went negative");
    }

    // --- C. THE NEW SETTLEMENT'S STATE --------------------------------------

    [Fact]
    public void NewSettlementHasTheCompleteClassAState()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Founded();
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[0].Id, 0.25, 1000));
        int before = w.Settlements.Count;

        w = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(cfg, TestConfigs.Worldgen())]).Step(w);
        Assert.Equal(before + 1, w.Settlements.Count);
        SettlementId id = w.Settlements[^1].Id;

        // Buckets: the FULL cross-product, key for key. Without matching keys
        // MigrationSystem refunds the move and nobody could ever arrive.
        int srcBuckets = 0, dstBuckets = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
        {
            if (w.Buckets[i].Settlement == w.Settlements[0].Id) srcBuckets++;
            if (w.Buckets[i].Settlement == id) dstBuckets++;
        }
        Assert.Equal(srcBuckets, dstBuckets);

        // A GoodStockRow for EVERY good — a missing grain row means the settlement
        // silently never farms, forever.
        foreach (GoodEntry g in cfg.Goods!.Goods)
            Assert.True(GoodStockIndex.IndexOf(w.GoodStocks, id, new GoodId(g.Id)) >= 0,
                $"no stock row for good {g.Id}");

        // Deposits — without them herding and extraction are permanently dead and
        // UNRECOVERABLE, because Deposits has no per-turn owner.
        int deposits = 0;
        for (int i = 0; i < w.Deposits.Count; i++) if (w.Deposits[i].Settlement == id) deposits++;
        Assert.True(deposits > 0, "no deposit rows — production would be dead forever");

        int classStates = 0, grievances = 0;
        for (int i = 0; i < w.ClassStates.Count; i++) if (w.ClassStates[i].Settlement == id) classStates++;
        for (int i = 0; i < w.Grievances.Count; i++) if (w.Grievances[i].Settlement == id) grievances++;
        Assert.Equal(cfg.Registries!.Classes.Length, classStates);
        Assert.Equal(cfg.Registries.Classes.Length, grievances);
    }

    [Fact]
    public void NewSettlementGetsNoFreeHousing()
    {
        WorldState w = Founded();
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[0].Id, 0.25, 1000));

        w = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(w);
        SettlementId id = w.Settlements[^1].Id;

        // HousingSystem materialises a missing row at ZERO when population > 0.
        // Colonization must not pre-empt it with free dwellings.
        for (int i = 0; i < w.Housing.Count; i++)
            Assert.True(w.Housing[i].Settlement != id,
                "colonization created a housing row — colonists must start homeless and build");
    }

    [Fact]
    public void NewSettlementRecordsItsRealFoundedTurn()
    {
        WorldState w = Founded();
        var exec = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]);
        w = exec.Step(w);                                  // turn 1, no deficit: nothing
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[0].Id, 0.25, 1000));
        w = exec.Step(w);                                  // turn 2: founds

        SettlementRow daughter = w.Settlements[^1];
        Assert.Equal(2, daughter.FoundedTurn);
        Assert.Equal(0, w.Settlements[0].FoundedTurn);     // turn-zero settlements unchanged
    }

    [Fact]
    public void CatchmentGoesStaleThroughTheEXISTINGCountMechanism_NotANewPath()
    {
        // CatchmentSystem.IsStale opens with a settlement-count mismatch check, so
        // appending a settlement forces a recompute by itself. Colonization must
        // write NO catchment state — asserting that here is what keeps a future
        // change from quietly manufacturing same-turn catchment.
        WorldState w = Founded();
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[0].Id, 0.25, 1000));

        w = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(w);
        SettlementId id = w.Settlements[^1].Id;

        for (int i = 0; i < w.CatchmentSummaries.Count; i++)
            Assert.True(w.CatchmentSummaries[i].Settlement != id, "colonization wrote catchment state");
        Assert.NotEqual(w.CatchmentSummaries.Count, w.Settlements.Count);   // the staleness signal
    }

    // --- D. SITE LEGALITY: SPACING vs OCCUPANCY, kept distinct ---------------

    [Fact]
    public void ChosenSiteRespectsTheMinimumTRAVELCOSTSpacing_FromEveryExistingSettlement()
    {
        WorldgenConfig wg = TestConfigs.Worldgen();
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Founded();
        for (int i = 0; i < w.Settlements.Count; i++)
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[i].Id, 0.35, 1000));
        int before = w.Settlements.Count;

        w = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(cfg, wg)]).Step(w);
        Assert.True(w.Settlements.Count > before, "nothing was founded — test is vacuous");

        // Re-derive the spacing field from EVERY site and assert every pair clears
        // the floor. This is the ADR-018 rule, in travel cost, not kilometres.
        var f = SettlementSiting.PrepareFrontier(w.Terrain!, wg.Siting, cfg.Transport.RiverCostFactor);
        for (int a = 0; a < w.Settlements.Count; a++)
        {
            var others = new List<int>();
            for (int b = 0; b < w.Settlements.Count; b++)
                if (b != a) others.Add(w.Settlements[b].SiteCell);
            double[] spacing = SettlementSiting.SeedSpacing(f, others.ToArray());
            int node = Sim.Core.Pathing.LatticeMap.OriginLatticeNode(
                f.Lattice, w.Terrain!.Size, w.Settlements[a].SiteCell);
            Assert.True(spacing[node] >= f.MinSpacingCostUnits,
                $"settlement {a} sits inside another's spacing exclusion ({spacing[node]:F3} < {f.MinSpacingCostUnits:F3})");
        }
    }

    [Fact]
    public void SiteCellsAreEXACTLYDistinct_NotMerelyIncidentallySo()
    {
        // The director ruled this an explicit requirement rather than something
        // inherited from the spacing calculation: spacing is evaluated at ~16 km
        // lattice-node granularity while sites are ~4 km cells, and nothing in the
        // tree ever asserted SiteCell uniqueness.
        WorldState w = Founded();
        for (int i = 0; i < w.Settlements.Count; i++)
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[i].Id, 0.35, 1000));

        w = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(w);

        for (int a = 0; a < w.Settlements.Count; a++)
            for (int b = a + 1; b < w.Settlements.Count; b++)
                Assert.True(w.Settlements[a].SiteCell != w.Settlements[b].SiteCell,
                    $"settlements {a} and {b} share SiteCell {w.Settlements[a].SiteCell}");
    }

    [Fact]
    public void AnOccupiedSiteCellIsRejectedEVENWhenSpacingWouldAllowIt()
    {
        // SPACING AND OCCUPANCY ARE DIFFERENT RULES — proven by driving them apart.
        // With minSpacingKm at zero the spacing test permits everything (its check
        // is a strict `<`), so occupancy is the ONLY thing that can reject the
        // incumbent's own cell. If occupancy were merely a by-product of spacing,
        // this returns the occupied cell.
        WorldgenConfig wg = TestConfigs.Worldgen();
        var zeroSpacing = wg with { Siting = wg.Siting with { MinSpacingKm = 0.0 } };
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Founded();

        var f = SettlementSiting.PrepareFrontier(w.Terrain!, zeroSpacing.Siting, cfg.Transport.RiverCostFactor);
        double[] spacing = SettlementSiting.SeedSpacing(f, System.Array.Empty<int>());

        // The unconstrained argmax — the best cell in the world.
        int best = SettlementSiting.ChooseFrontierSite(
            f, w.Terrain!, spacing, System.Array.Empty<int>(), zeroSpacing.Siting.ScoreJitter, w.Seed);
        Assert.True(best >= 0);

        // Declare that exact cell occupied; spacing still permits it, occupancy must not.
        int next = SettlementSiting.ChooseFrontierSite(
            f, w.Terrain!, spacing, new[] { best }, zeroSpacing.Siting.ScoreJitter, w.Seed);
        Assert.NotEqual(best, next);
    }

    [Fact]
    public void TwoSitesInOneCatchmentRegion_OnlyTheSpacingLegalOneIsChosen()
    {
        // CATCHMENT AND SPACING ARE NOT THE SAME RULE (director ruling). Take the
        // best cell, then ask for another with only that one seeding the spacing
        // field: the winner must lie OUTSIDE the spacing exclusion even though
        // both candidates sit in the same broad hinterland region.
        WorldgenConfig wg = TestConfigs.Worldgen();
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Founded();
        var f = SettlementSiting.PrepareFrontier(w.Terrain!, wg.Siting, cfg.Transport.RiverCostFactor);

        double[] empty = SettlementSiting.SeedSpacing(f, System.Array.Empty<int>());
        int first = SettlementSiting.ChooseFrontierSite(
            f, w.Terrain!, empty, System.Array.Empty<int>(), wg.Siting.ScoreJitter, w.Seed);
        Assert.True(first >= 0);

        double[] seeded = SettlementSiting.SeedSpacing(f, new[] { first });
        int second = SettlementSiting.ChooseFrontierSite(
            f, w.Terrain!, seeded, new[] { first }, wg.Siting.ScoreJitter, w.Seed);
        Assert.True(second >= 0);

        int node = Sim.Core.Pathing.LatticeMap.OriginLatticeNode(f.Lattice, w.Terrain!.Size, second);
        Assert.True(seeded[node] >= f.MinSpacingCostUnits,
            "the second site sits inside the first's spacing exclusion");
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ExistingSettlementsSeedTheSpacingField()
    {
        // The single behavioural difference from turn-zero siting: at turn zero the
        // field is +infinity everywhere because nothing stands yet.
        WorldgenConfig wg = TestConfigs.Worldgen();
        WorldState w = Founded();
        var f = SettlementSiting.PrepareFrontier(
            w.Terrain!, wg.Siting, TestConfigs.Sim().Transport.RiverCostFactor);

        double[] none = SettlementSiting.SeedSpacing(f, System.Array.Empty<int>());
        foreach (double d in none) Assert.True(double.IsPositiveInfinity(d));

        var cells = new int[w.Settlements.Count];
        for (int i = 0; i < cells.Length; i++) cells[i] = w.Settlements[i].SiteCell;
        double[] seeded = SettlementSiting.SeedSpacing(f, cells);

        int finite = 0;
        foreach (double d in seeded) if (!double.IsPositiveInfinity(d)) finite++;
        Assert.True(finite > 0, "seeding produced no exclusion at all");
    }

    [Fact]
    public void RiverAwareTraversalReachesSpacingThroughTheLattice()
    {
        // T4.7 made riverCostFactor authoritative. Frontier spacing must go through
        // the SAME lattice, so changing that factor must change the exclusion field —
        // if it did not, a second graph would have crept in.
        WorldgenConfig wg = TestConfigs.Worldgen();
        WorldState w = Founded();
        var cells = new int[w.Settlements.Count];
        for (int i = 0; i < cells.Length; i++) cells[i] = w.Settlements[i].SiteCell;

        var cheapRivers = SettlementSiting.PrepareFrontier(w.Terrain!, wg.Siting, 0.05);
        var dearRivers = SettlementSiting.PrepareFrontier(w.Terrain!, wg.Siting, 0.95);
        double[] a = SettlementSiting.SeedSpacing(cheapRivers, cells);
        double[] b = SettlementSiting.SeedSpacing(dearRivers, cells);

        bool differs = false;
        for (int i = 0; i < a.Length && !differs; i++)
            if (a[i] != b[i] && !(double.IsPositiveInfinity(a[i]) && double.IsPositiveInfinity(b[i])))
                differs = true;
        Assert.True(differs, "riverCostFactor did not reach the spacing field — the lattice is bypassed");
    }

    [Fact]
    public void NoLegalFrontierSite_ReturnsMinusOne_AndNeverThrows()
    {
        // ChooseSites THROWS when it cannot place its count; a per-turn system must
        // not — "the frontier is full" is an ordinary outcome, not a config error.
        // Driven at the siting level with a spacing field I control: every node at
        // cost 0 is inside every exclusion, so nothing is legal.
        WorldgenConfig wg = TestConfigs.Worldgen();
        WorldState w = Founded();
        var f = SettlementSiting.PrepareFrontier(
            w.Terrain!, wg.Siting, TestConfigs.Sim().Transport.RiverCostFactor);

        var blocked = new double[f.Lattice.NodeCount];   // 0 everywhere < the floor
        int site = SettlementSiting.ChooseFrontierSite(
            f, w.Terrain!, blocked, System.Array.Empty<int>(), wg.Siting.ScoreJitter, w.Seed);

        Assert.Equal(-1, site);
    }

    [Fact]
    public void UnreachableLandSatisfiesSpacingTrivially_DocumentingTheSEMANTICS()
    {
        // RECORDED BEHAVIOUR, found while writing the test above and kept because
        // it is load-bearing and non-obvious. The spacing prefilter is a CAPPED
        // Dijkstra, and its own contract says "any node the capped expansion never
        // reaches is trivially far enough". So a node no existing settlement can
        // reach keeps +infinity and PASSES the spacing test at ANY floor — even one
        // larger than the world.
        //
        // Under travel-cost semantics that is coherent (unreachable IS infinitely
        // far), but it means SPACING ALONE DOES NOT IMPLY REACHABILITY: nothing here
        // requires a founding party to be able to walk to its site. Adding such a
        // requirement needs a travel budget, i.e. a new constant, so it is NOT done
        // in this packet — it is reported in docs/t4.4-review-record.md as an
        // adversarial finding for the director.
        WorldgenConfig wg = TestConfigs.Worldgen();
        var impossible = wg with { Siting = wg.Siting with { MinSpacingKm = 1_000_000.0 } };
        WorldState w = Founded();
        var f = SettlementSiting.PrepareFrontier(
            w.Terrain!, impossible.Siting, TestConfigs.Sim().Transport.RiverCostFactor);

        var cells = new int[w.Settlements.Count];
        for (int i = 0; i < cells.Length; i++) cells[i] = w.Settlements[i].SiteCell;
        double[] spacing = SettlementSiting.SeedSpacing(f, cells);

        int site = SettlementSiting.ChooseFrontierSite(
            f, w.Terrain!, spacing, cells, impossible.Siting.ScoreJitter, w.Seed);

        Assert.True(site >= 0, "expected unreachable land to remain eligible at an impossible floor");
        int node = Sim.Core.Pathing.LatticeMap.OriginLatticeNode(f.Lattice, w.Terrain!.Size, site);
        Assert.True(double.IsPositiveInfinity(spacing[node]),
            "the chosen site was REACHED by the relaxation, so this is not the unreachable case");
    }

    // --- E. DETERMINISM ------------------------------------------------------

    [Fact]
    public void FoundingIsBitIdenticalAcrossIdenticalRuns()
    {
        WorldState Run()
        {
            WorldState w = Founded();
            for (int i = 0; i < w.Settlements.Count; i++)
                w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[i].Id, 0.30, 1000));
            var exec = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
                TestConfigs.Sim(), TestConfigs.Worldgen())]);
            for (int t = 0; t < 3; t++) w = exec.Step(w);
            return w;
        }
        Assert.Equal(WorldHash.ComputeHex(Run()), WorldHash.ComputeHex(Run()));
    }

    [Fact]
    public void SimultaneousFoundersResolveInAscendingSettlementOrder()
    {
        // Every settlement eligible at once. Resolution is settlement-major
        // ascending, and each acceptance immediately grows the exclusion field so a
        // later founder cannot crowd an earlier one — which is also why the result
        // is reproducible rather than order-of-discovery dependent.
        WorldState w = Founded();
        for (int i = 0; i < w.Settlements.Count; i++)
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[i].Id, 0.30, 1000));
        int before = w.Settlements.Count;

        w = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(w);

        // Ids are dense and ascending — MigrationSystem indexes new int[maxId + 1].
        for (int i = 0; i < w.Settlements.Count; i++)
            Assert.Equal(i, w.Settlements[i].Id.Value);
        Assert.True(w.Settlements.Count > before);
    }

    [Fact]
    public void FoundingIsBoundedPerTurn_AtMostOneDaughterPerEligibleSource()
    {
        // The bound is structural, not a magic counter: the party is drawn once per
        // source per turn, so foundings per turn can never exceed the number of
        // settlements that were eligible at the start of the turn.
        WorldState w = Founded();
        for (int i = 0; i < w.Settlements.Count; i++)
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[i].Id, 0.60, 1000));
        int eligible = w.Settlements.Count;

        w = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(w);

        Assert.True(w.Settlements.Count - eligible <= eligible,
            $"founded {w.Settlements.Count - eligible} from {eligible} eligible sources in one turn");
    }

    // --- F. THE ADR-012 HAZARD ----------------------------------------------

    [Fact]
    public void NewSettlementsAttractivenessIsSeededFromItsSource_NotFromItsOwnEmptiness()
    {
        // A new settlement is a tiny population against a full catchment — the exact
        // magnet profile ADR-012's resurrection cycle was made of — and migration's
        // EMA does NOT damp it ("a settlement's first sighting initializes S = A").
        // So the row is seeded from the SOURCE's smoothed value before migration
        // ever sees the daughter.
        WorldState w = Founded();
        SettlementId src = w.Settlements[0].Id;
        w.SmoothedAttractiveness.Add(new SmoothedAttractivenessRow(src, 3.5));
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(src, 0.25, 1000));

        w = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(w);
        SettlementId daughter = w.Settlements[^1].Id;

        double seeded = double.NaN;
        for (int i = 0; i < w.SmoothedAttractiveness.Count; i++)
            if (w.SmoothedAttractiveness[i].Settlement == daughter)
                seeded = w.SmoothedAttractiveness[i].Value;

        Assert.False(double.IsNaN(seeded), "the daughter has no smoothed-attractiveness row");
        Assert.Equal(3.5, seeded, 10);
    }

    private static EraTable FlatEra() => EraTableLoader.Load(
        """{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": 10 } ] }""");
}
