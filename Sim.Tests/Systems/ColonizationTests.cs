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

    /// <summary>Run ONE catchment turn so `SettlementDistances` exists in PREV.
    /// Migration reads Prev, and a world straight out of founding has an empty
    /// distance table — which is missing data, not isolation. Every rig that asks
    /// migration to judge reachability must be warmed first.</summary>
    private static WorldState Warm(WorldState w) =>
        new TurnExecutor(FlatEra(), [SystemCatalog.Catchment(TestConfigs.Sim())]).Step(w);

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

    /// <summary>Migration + colonization ONLY: the two systems whose handoff this
    /// packet is about, with nothing else able to move a person or a grain.</summary>
    /// <summary>Catchment is included because it is what WRITES SettlementDistances,
    /// and migration cannot judge a destination unreachable from a table that has
    /// never been computed. It moves no person and no grain.</summary>
    private static TurnExecutor Handoff() => new(FlatEra(), [
        SystemCatalog.Catchment(TestConfigs.Sim()),
        SystemCatalog.Migration(TestConfigs.Sim()),
        SystemCatalog.Colonization(TestConfigs.Sim(), TestConfigs.Worldgen())]);

    /// <summary>Make a settlement NON-VIABLE under ADR-012's absolute food gate:
    /// no store AND no last harvest. This is the gate's own condition, not a new one.</summary>
    private static void Starve(WorldState w, SettlementId s)
    {
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement == s)
                w.GoodStocks[i] = w.GoodStocks[i] with
                { Amount = Conserved.Zero, LastProducedUnits = 0 };
    }

    /// <summary>Every settlement EXCEPT <paramref name="keep"/> loses its food, so
    /// <paramref name="keep"/> has no viable destination anywhere.</summary>
    private static void StarveAllBut(WorldState w, SettlementId keep)
    {
        for (int s = 0; s < w.Settlements.Count; s++)
            if (w.Settlements[s].Id != keep) Starve(w, w.Settlements[s].Id);
    }

    /// <summary>Seed the unplaced-departure demand DIRECTLY, so a test can exercise
    /// colonization's own behaviour without also re-deriving migration's. This is the
    /// new precondition: the party comes from demand, not from a deficit row.</summary>
    private static void SeedDemand(WorldState w, SettlementId s, double perBucket)
    {
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == s) w.Buckets.Ref(i).UnplacedDeparture = perBucket;
    }

    private static void SetDeficit(WorldState w, SettlementId s, double ratio)
        => w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, ratio, 1000));

    private static double UnplacedOf(WorldState w, SettlementId s)
    {
        double d = 0.0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == s) d += w.Buckets[i].UnplacedDeparture;
        return d;
    }

    /// <summary>A world where ONE settlement is starving and every other settlement
    /// is a food-less ruin — i.e. exactly ADR-012's "no viable destination".</summary>
    private static (WorldState World, SettlementId Source) StrandedSource(ulong seed = 1UL)
    {
        WorldState w = Warm(Founded(seed));
        SettlementId src = w.Settlements[0].Id;
        StarveAllBut(w, src);
        SetDeficit(w, src, 0.40);
        return (w, src);
    }

    // === A. THE TRIGGER — D-037 B1's condition, not a consumption deficit =====

    [Fact]
    public void ADeficitALONE_DoesNOTCauseColonization_WhenAViableDestinationExists()
    {
        // THE HEADLINE REGRESSION FOR THIS PACKET. The first T4.4 implementation
        // founded on `DeficitRatio > 0` and nothing else. Here the source is
        // severely short of food AND every other settlement is fed, so migration
        // can place its people: D-037 B1's condition ("no viable destination") is
        // NOT met and nothing may be founded, however hungry the source is.
        WorldState w = Warm(Founded());
        SettlementId src = w.Settlements[0].Id;
        SetDeficit(w, src, 0.90);
        int before = w.Settlements.Count;

        w = Handoff().Step(w);

        Assert.Equal(before, w.Settlements.Count);
        Assert.Equal(0.0, UnplacedOf(w, src));
    }

    [Fact]
    public void MigrationDemandWithAViableDestination_TransfersInsteadOfFounding()
    {
        WorldState w = Warm(Founded());
        SettlementId src = w.Settlements[0].Id;
        SetDeficit(w, src, 0.60);
        int before = w.Settlements.Count;
        long srcBefore = PopulationOf(w, src);

        w = Handoff().Step(w);

        Assert.Equal(before, w.Settlements.Count);          // no founding
        Assert.True(PopulationOf(w, src) < srcBefore,       // but people DID move
            "famine flight moved nobody, so this test proves nothing about the branch taken");
    }

    [Fact]
    public void NoViableDestination_FoundsASettlement_WhenUnclaimedLandExists()
    {
        (WorldState w, SettlementId src) = StrandedSource();
        int before = w.Settlements.Count;

        w = Handoff().Step(w);

        Assert.Equal(before + 1, w.Settlements.Count);
    }

    [Fact]
    public void NoDeficit_NoDemand_NoFounding_EvenWithNowhereViableToGo()
    {
        // The demand is the flight desire, and flight is `FamineFlightFactor x
        // deficit_source`. A FED settlement generates none, so isolation alone
        // founds nothing — colonization is refugee-driven, exactly as B1 says.
        WorldState w = Warm(Founded());
        SettlementId src = w.Settlements[0].Id;
        StarveAllBut(w, src);                    // nowhere viable to go...
        Assert.Equal(0, w.ConsumptionDeficits.Count);   // ...but nobody wants to leave
        int before = w.Settlements.Count;

        w = Handoff().Step(w);

        Assert.Equal(before, w.Settlements.Count);
    }

    [Fact]
    public void TheFoundingPopulationComesFromTheMigrationDemand_NotFromAnyDeficitRatio()
    {
        // The party equals the floored unplaced demand — NOT `count x deficit`,
        // which is what the defective implementation used. The two are different
        // numbers, and this test pins that the demand is the one that governs.
        (WorldState w, SettlementId src) = StrandedSource();

        WorldState afterMigration = new TurnExecutor(
            FlatEra(), [SystemCatalog.Migration(TestConfigs.Sim())]).Step(w);
        double demand = UnplacedOf(afterMigration, src);
        Assert.True(demand > 0.0, "migration wrote no unplaced demand — the rig is wrong");

        long expected = 0;
        for (int i = 0; i < afterMigration.Buckets.Count; i++)
            if (afterMigration.Buckets[i].Settlement == src)
                expected += (long)Math.Floor(afterMigration.Buckets[i].UnplacedDeparture);

        WorldState founded = Handoff().Step(w);
        SettlementId daughter = founded.Settlements[^1].Id;
        Assert.Equal(expected, PopulationOf(founded, daughter));

        // And it is NOT the old deficit-derived number.
        long oldFormula = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == src)
                oldFormula += (long)Math.Floor(w.Buckets[i].Count.Value * 0.40);
        Assert.NotEqual(oldFormula, expected);
    }

    [Fact]
    public void SourceLosesEXACTLYWhatTheDaughterGains()
    {
        // The source is the world's only fed settlement, so it also RECEIVES gap-driven
        // migrants — its net change is not the party. Colonization's own effect is
        // therefore isolated by measuring across the colonization step alone.
        (WorldState w, SettlementId src) = StrandedSource();
        WorldState afterMigration = new TurnExecutor(FlatEra(), [
            SystemCatalog.Catchment(TestConfigs.Sim()),
            SystemCatalog.Migration(TestConfigs.Sim())]).Step(w);
        long srcBefore = PopulationOf(afterMigration, src);
        long worldBefore = Population(afterMigration);

        WorldState after = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(afterMigration);

        SettlementId daughter = after.Settlements[^1].Id;
        long moved = PopulationOf(after, daughter);
        Assert.True(moved > 0, "no party moved");
        Assert.Equal(srcBefore - moved, PopulationOf(after, src));   // left exactly once
        Assert.Equal(worldBefore, Population(after));                // arrived exactly once
    }

    [Fact]
    public void OneTurnsDemandCannotFoundTwoSettlements()
    {
        (WorldState w, SettlementId src) = StrandedSource();
        int before = w.Settlements.Count;

        // Run colonization TWICE against the same migration output. The demand is
        // consumed as it is drawn, so the second pass has nothing to spend.
        WorldState afterMigration = new TurnExecutor(
            FlatEra(), [SystemCatalog.Migration(TestConfigs.Sim())]).Step(w);
        var colonize = new TurnExecutor(FlatEra(),
            [SystemCatalog.Colonization(TestConfigs.Sim(), TestConfigs.Worldgen())]);
        WorldState once = colonize.Step(afterMigration);
        WorldState twice = colonize.Step(once);

        Assert.Equal(before + 1, once.Settlements.Count);
        Assert.Equal(before + 1, twice.Settlements.Count);
        Assert.Equal(Population(once), Population(twice));
    }

    [Fact]
    public void ANewlyFoundedSettlementIsAVIABLEDestinationUnderADR012_TheCascadeBRAKE()
    {
        // The mechanism that replaces the old cooldown/immunity heuristics: the
        // daughter carries provisions, so `store > 0` and ADR-012's absolute food
        // gate admits it as a destination. This is asserted through the GATE'S OWN
        // CONDITION (store > 0 OR lastHarvest > 0), not through a private helper.
        (WorldState w, SettlementId src) = StrandedSource();

        w = Handoff().Step(w);
        SettlementId daughter = w.Settlements[^1].Id;

        long store = 0, lastHarvest = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement == daughter
                && w.GoodStocks[i].Good.Value == TestConfigs.Sim().Goods!.GrainId)
            { store = w.GoodStocks[i].Amount.Value; lastHarvest = w.GoodStocks[i].LastProducedUnits; }

        Assert.True(store > 0 || lastHarvest > 0,
            "the daughter is not a viable destination, so the cascade brake does not exist");
    }

    [Fact]
    public void TwoStarvingSettlementsThatCanSEEEachOther_DoNotFoundAtAll()
    {
        // TEST 10, and the precise inverse of the old failure. Both settlements are
        // in deficit; the old trigger founded twice on that fact alone. Here each is
        // a viable destination for the other (both still hold food), so neither has
        // unplaced demand and neither founds.
        WorldState w = Warm(Founded());
        SettlementId a = w.Settlements[0].Id, b = w.Settlements[1].Id;
        SetDeficit(w, a, 0.50);
        SetDeficit(w, b, 0.50);
        int before = w.Settlements.Count;

        w = Handoff().Step(w);

        Assert.Equal(before, w.Settlements.Count);
        Assert.Equal(0.0, UnplacedOf(w, a));
        Assert.Equal(0.0, UnplacedOf(w, b));
    }

    [Fact]
    public void TheOldRunawayLoopDoesNotREPRODUCE_TinyDaughterDoesNotFoundAgain()
    {
        // THE ADVERSARIAL PIN for the measured pathology: found a settlement, then
        // keep stepping the handoff and prove the founding does not repeat every
        // turn. The old mechanism produced one founding per deficit settlement per
        // turn, unbounded; the daughter itself became a deficit settlement and
        // founded in turn (measured 12 -> 178 by turn 77).
        (WorldState w, SettlementId src) = StrandedSource();
        long popBefore = Population(w);
        TurnExecutor exec = Handoff();

        w = exec.Step(w);
        int afterFirst = w.Settlements.Count;

        // Ten more turns with the deficit STILL asserted on the source every turn —
        // the old trigger would have founded on every one of them.
        for (int t = 0; t < 10; t++)
        {
            w.ConsumptionDeficits.Clear();
            SetDeficit(w, src, 0.40);
            w = exec.Step(w);
        }

        Assert.True(w.Settlements.Count < afterFirst + 10,
            $"the runaway reproduced: {afterFirst} -> {w.Settlements.Count} in ten turns");
        Assert.Equal(popBefore, Population(w));
    }

    [Fact]
    public void SubPersonDemandIsBANKED_NotFlooredToZeroForever()
    {
        // The T4.13 F3 stall, pre-empted: a demand under one person must accumulate
        // in the D-004 remainder rather than vanishing every turn.
        (WorldState w, SettlementId src) = StrandedSource();
        WorldState after = new TurnExecutor(
            FlatEra(), [SystemCatalog.Migration(TestConfigs.Sim())]).Step(w);
        after = new TurnExecutor(FlatEra(),
            [SystemCatalog.Colonization(TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(after);

        double banked = 0.0;
        for (int i = 0; i < after.Buckets.Count; i++)
            if (after.Buckets[i].Settlement == src) banked += after.Buckets[i].UnplacedRemainder;
        Assert.True(banked > 0.0 && banked < after.Buckets.Count,
            $"remainder not banked as a sub-person fraction: {banked}");
    }

    [Fact]
    public void ZeroProvisionFoundingIsIMPOSSIBLE_AnEmptyGranaryOutfitsNobody()
    {
        // The clearing cost is BINDING. A source with an empty granary has nothing
        // to outfit an expedition with, and — the reason this matters mechanically —
        // a daughter founded with no provisions fails ADR-012's absolute food gate,
        // is therefore not a viable destination, and brakes nothing. Measured on
        // 4d11c02: the FirstReign source granary was empty from turn 5 and the world
        // founded 16 settlements in 14 consecutive turns.
        WorldState w = Warm(Founded());
        SettlementId src = w.Settlements[0].Id;
        StarveAllBut(w, src);
        Starve(w, src);                 // ...and the source's own granary is empty too
        SetDeficit(w, src, 0.40);
        int before = w.Settlements.Count;

        w = Handoff().Step(w);

        Assert.Equal(before, w.Settlements.Count);
    }

    [Fact]
    public void EveryFoundedSettlementCarriesRealProvisions_NoEmptyDaughters()
    {
        (WorldState w, SettlementId _s) = StrandedSource();
        w = Handoff().Step(w);

        int grain = TestConfigs.Sim().Goods!.GrainId;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            if (w.Settlements[i].FoundedTurn == 0) continue;      // turn-zero settlement
            SettlementId id = w.Settlements[i].Id;
            long store = 0;
            for (int k = 0; k < w.GoodStocks.Count; k++)
                if (w.GoodStocks[k].Settlement == id && w.GoodStocks[k].Good.Value == grain)
                    store = w.GoodStocks[k].Amount.Value;
            Assert.True(store > 0, $"settlement {id.Value} was founded with no provisions");
        }
    }

    [Fact]
    public void TheRemainderBanksOnlyASubPersonFraction_NeverWholePeople()
    {
        // The bank is taken BEFORE the availability clamp. Banking after it would
        // carry whole people forward: a bucket whose desire far exceeds its
        // population would accumulate person-units of unmet desire and discharge
        // them as one huge party later. Measured on 4d11c02: desire 500 against 25
        // people banked 475.
        (WorldState w, SettlementId src) = StrandedSource();

        // Drive desire far above the population so the clamp certainly binds.
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == src) w.Buckets.Ref(i).UnplacedDeparture = 1e6;

        w = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(w);

        for (int i = 0; i < w.Buckets.Count; i++)
            Assert.True(w.Buckets[i].UnplacedRemainder < 1.0,
                $"bucket {i} banked {w.Buckets[i].UnplacedRemainder} — whole people carried forward");
    }

    [Fact]
    public void MigrationWritesTheDemandAndColonizationCONSUMESIt()
    {
        // The handoff contract, asserted directly: non-zero after migration, zero
        // after colonization has drawn it.
        (WorldState w, SettlementId src) = StrandedSource();
        WorldState afterMigration = new TurnExecutor(
            FlatEra(), [SystemCatalog.Migration(TestConfigs.Sim())]).Step(w);
        Assert.True(UnplacedOf(afterMigration, src) > 0.0);

        WorldState afterColonization = new TurnExecutor(FlatEra(),
            [SystemCatalog.Colonization(TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(afterMigration);
        Assert.Equal(0.0, UnplacedOf(afterColonization, src));
    }

    // --- B. CONSERVATION (law 1) --------------------------------------------

    [Fact]
    public void FoundingConservesPopulationEXACTLY_NothingIsMinted()
    {
        // Two arms. ARM 1 uses the stranded rig (which hand-zeroes grain stocks to
        // make destinations non-viable, so the GRAIN ledger is deliberately
        // perturbed by the rig): people are audited exactly, with no epsilon.
        (WorldState w, SettlementId _src) = StrandedSource();
        long before = Population(w);

        w = Handoff().Step(w);

        Assert.True(w.Settlements.Count > 12, "nothing was founded — the test is vacuous");
        Assert.Equal(before, Population(w));          // EXACT. Not one person minted or lost.
        for (int i = 0; i < w.Buckets.Count; i++)
            Assert.True(w.Buckets[i].Count.Value >= 0, $"bucket {i} went negative");

        // ARM 2 touches no stock by hand, so the WHOLE-WORLD ledger audit applies
        // — including the grain the colonists carry as provisions.
        WorldState w2 = Founded();
        SeedDemand(w2, w2.Settlements[0].Id, 12.0);
        long before2 = Population(w2);
        w2 = new TurnExecutor(FlatEra(), [SystemCatalog.Colonization(
            TestConfigs.Sim(), TestConfigs.Worldgen())]).Step(w2);
        Assert.True(w2.Settlements.Count > 12, "arm 2 founded nothing");
        Assert.Equal(before2, Population(w2));
        Assert.True(ConservationAuditor.IsConserved(w2, out string report), report);
    }

    [Fact]
    public void TheFoundingPartyIsTAKENFromTheSource_NotCreated()
    {
        WorldState w = Founded();
        SettlementId src = w.Settlements[0].Id;
        SeedDemand(w, src, 12.0);
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
        WorldState w = Warm(Founded());
        for (int i = 0; i < w.Settlements.Count; i++)
            SetDeficit(w, w.Settlements[i].Id, 0.40);

        long popBefore = Population(w);
        w = Handoff().Step(w);

        Assert.Equal(popBefore, Population(w));
        for (int i = 0; i < w.Buckets.Count; i++)
            Assert.True(w.Buckets[i].Count.Value >= 0, $"bucket {i} went negative");
    }

    // --- C. THE NEW SETTLEMENT'S STATE --------------------------------------

    [Fact]
    public void NewSettlementHasTheCompleteClassAState()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = Founded();
        SeedDemand(w, w.Settlements[0].Id, 12.0);
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
        SeedDemand(w, w.Settlements[0].Id, 12.0);

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
        SeedDemand(w, w.Settlements[0].Id, 12.0);
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
        SeedDemand(w, w.Settlements[0].Id, 12.0);

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
            SeedDemand(w, w.Settlements[i].Id, 12.0);
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
            SeedDemand(w, w.Settlements[i].Id, 12.0);

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
                SeedDemand(w, w.Settlements[i].Id, 12.0);
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
            SeedDemand(w, w.Settlements[i].Id, 12.0);
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
            SeedDemand(w, w.Settlements[i].Id, 12.0);
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
        SeedDemand(w, src, 12.0);

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
