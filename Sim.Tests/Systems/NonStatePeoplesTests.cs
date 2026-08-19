using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.5 — NON-STATE PEOPLES (D-037 B3). The decision's own sentence is the
/// acceptance criterion: "RAIDING EMERGES FROM THEIR SUBSISTENCE FAILING — no
/// raid timer, no aggression stat", and "the same bad year that starves villages
/// sends herders after grain".
///
/// The chain under test, every link an existing mechanism:
///   drought (HarvestWeatherRow) -> herding output falls (ProductionSystem)
///   -> deficit (ConsumptionDeficitRow) -> appropriation (Ledger.Transfer).
/// </summary>
public class NonStatePeoplesTests
{
    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static readonly SettlementId S0 = new(0);
    private static readonly SettlementId S1 = new(1);

    /// <summary>Two settlements, one grain stock row each, no control rows.</summary>
    private static WorldState TwoSettlements(SimConfig cfg, long grain0, long grain1)
    {
        var world = new WorldState(7);
        world.Settlements.Add(new SettlementRow(S0, SiteCell: 0, FoundedTurn: 0));
        world.Settlements.Add(new SettlementRow(S1, SiteCell: 1, FoundedTurn: 0));
        // S0 is a HERDING people (D-037 B3's subject); S1 is a farming village.
        world.SectorAllocations.Add(new SectorAllocationRow(S0, 0.0, 1.0, 0.0, 0.0, 0.0));
        world.SectorAllocations.Add(new SectorAllocationRow(S1, 1.0, 0.0, 0.0, 0.0, 0.0));
        var ledger = new Ledger(world.LedgerFlows);
        var grain = new GoodId(cfg.Goods!.GrainId);
        foreach ((SettlementId s, long amount) in new[] { (S0, grain0), (S1, grain1) })
        {
            int row = world.GoodStocks.Add(new GoodStockRow(s, grain, Conserved.Zero, 0.0, 0.0));
            if (amount > 0)
                ledger.Flow(ref world.GoodStocks.Ref(row).Amount,
                    ConservedQuantityIds.OfGood(grain), ReasonIds.InitialEndowment,
                    amount, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return world;
    }

    private static long Grain(WorldState w, SimConfig cfg, SettlementId s)
    {
        int id = cfg.Goods!.GrainId;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement == s && w.GoodStocks[i].Good.Value == id)
                return w.GoodStocks[i].Amount.Value;
        return 0;
    }

    private static long WorldGrain(WorldState w, SimConfig cfg)
    {
        long total = 0;
        for (int i = 0; i < w.Settlements.Count; i++) total += Grain(w, cfg, w.Settlements[i].Id);
        return total;
    }

    // --- A. STATELESS CARRIER -------------------------------------------------

    [Fact]
    public void StatelessSettlement_NeedsNoNewCarrier_AndKeepsItsPopulation()
    {
        // The carrier IS the absence of a ControlRow: ControlRow's contract
        // already admits "exactly one state control row, OR none". A world with
        // no control rows is a valid world, and population stays where it is —
        // nothing about statelessness moves people or needs a second table.
        var counts = new long[Cohorts.Count];
        counts[5] = 400;
        WorldState world = PopulationExactnessTests.BucketWorld(counts);

        Assert.Equal(0, world.Controls.Count);
        long pop = 0;
        for (int b = 0; b < world.Buckets.Count; b++)
            if (world.Buckets[b].Settlement == S0) pop += world.Buckets[b].Count.Value;
        Assert.Equal(400, pop);
    }

    // --- C. DROUGHT COUPLES TO HERDING ---------------------------------------

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(0.25)]
    public void Herding_ScalesExactlyWithHarvestWeather(double multiplier)
    {
        // Herding runs through the EXISTING FromDeposits pathway, and T4.5 gives
        // it the SAME weather multiplier farming already had. Livestock output
        // must scale linearly with the multiplier — proving the coupling exists
        // and that it is the existing multiplier, not a new drought driver.
        SimConfig cfg = TestConfigs.Sim();
        var counts = new long[Cohorts.Count];
        counts[5] = 1000;

        long Produced(double weather)
        {
            WorldState w = PopulationExactnessTests.BucketWorld(counts);
            w.SectorAllocations.Add(new SectorAllocationRow(S0, 0.0, 1.0, 0.0, 0.0, 0.0));
            w.Deposits.Add(new DepositRow(S0, new GoodId(cfg.Goods!.IdOf("livestock")), 1.0));
            foreach (GoodEntry g in cfg.Goods!.Goods)
                w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            w.HarvestWeather.Add(new HarvestWeatherRow(S0, LogDeviation: 0.0, Multiplier: weather));
            var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
            WorldState next = exec.Step(w);
            int id = cfg.Goods!.IdOf("livestock");
            for (int i = 0; i < next.GoodStocks.Count; i++)
                if (next.GoodStocks[i].Good.Value == id) return next.GoodStocks[i].Amount.Value;
            return 0;
        }

        long baseline = Produced(1.0);
        Assert.True(baseline > 0, "no herding output at all — this test would be vacuous");
        long scaled = Produced(multiplier);

        // Exact to the floor: the rate is multiplied before dt integration, so
        // the only slack is the single whole-unit floor.
        long expected = (long)Math.Floor(baseline * multiplier);
        Assert.InRange(scaled, expected - 1, expected + 1);
        if (multiplier < 1.0)
            Assert.True(scaled < baseline, $"drought did not reduce herding: {scaled} vs {baseline}");
    }

    [Fact]
    public void Extraction_DoesNotSeeHarvestWeather_OreIsNotACrop()
    {
        // The other half of the weather fence: FromDeposits is shared by herding
        // and EXTRACTION, and a drought must not shrink the quarry. Extraction
        // passes weather 1.0 at its call site, so output is identical under any
        // weather row.
        SimConfig cfg = TestConfigs.Sim();
        var counts = new long[Cohorts.Count];
        counts[5] = 1000;

        long Extracted(double weather)
        {
            WorldState w = PopulationExactnessTests.BucketWorld(counts);
            w.SectorAllocations.Add(new SectorAllocationRow(S0, 0.0, 0.0, 1.0, 0.0, 0.0));
            w.Deposits.Add(new DepositRow(S0, new GoodId(cfg.Goods!.IdOf("clay")), 1.0));
            foreach (GoodEntry g in cfg.Goods!.Goods)
                w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            w.HarvestWeather.Add(new HarvestWeatherRow(S0, LogDeviation: 0.0, Multiplier: weather));
            var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
            WorldState next = exec.Step(w);
            int id = cfg.Goods!.IdOf("clay");
            for (int i = 0; i < next.GoodStocks.Count; i++)
                if (next.GoodStocks[i].Good.Value == id) return next.GoodStocks[i].Amount.Value;
            return 0;
        }

        long dry = Extracted(0.25);
        long normal = Extracted(1.0);
        Assert.True(normal > 0, "no extraction output — vacuous");
        Assert.Equal(normal, dry);
    }

    [Fact]
    public void Farming_UnderWeather_IsUnchangedByT45()
    {
        // REGRESSION GUARD for the refactor: Farm's inline weather lookup became
        // the shared HarvestWeatherFor helper. Farming's response to weather must
        // be exactly linear in the multiplier, as it was before T4.5 — the
        // extraction moved code, never behaviour.
        SimConfig cfg = TestConfigs.Sim();
        var counts = new long[Cohorts.Count];
        counts[5] = 200;

        long Harvested(double weather)
        {
            WorldState w = PopulationExactnessTests.BucketWorld(counts);
            w.CatchmentSummaries.Add(new CatchmentSummaryRow(
                S0, NodeCount: 1, EffectiveArableKm2: 5000.0, NetworkRevision: 0, LastRecomputeTurn: 0));
            w.SectorAllocations.Add(new SectorAllocationRow(S0, 1.0, 0.0, 0.0, 0.0, 0.0));
            foreach (GoodEntry g in cfg.Goods!.Goods)
                w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            w.HarvestWeather.Add(new HarvestWeatherRow(S0, LogDeviation: 0.0, Multiplier: weather));
            var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
            return Grain(exec.Step(w), cfg, S0);
        }

        long full = Harvested(1.0);
        long half = Harvested(0.5);
        Assert.True(full > 0, "no harvest — vacuous");
        Assert.InRange(half, (long)Math.Floor(full * 0.5) - 1, (long)Math.Floor(full * 0.5) + 1);
    }

    [Fact]
    public void NoWeatherRow_MeansMultiplierOne_ForHerdingAsForFarming()
    {
        // The absent-row rule T4.5 inherits: a world with no weather system in
        // its pipeline produces exactly as it did before the coupling existed.
        SimConfig cfg = TestConfigs.Sim();
        var counts = new long[Cohorts.Count];
        counts[5] = 1000;

        long Produced(bool withRow)
        {
            WorldState w = PopulationExactnessTests.BucketWorld(counts);
            w.SectorAllocations.Add(new SectorAllocationRow(S0, 0.0, 1.0, 0.0, 0.0, 0.0));
            w.Deposits.Add(new DepositRow(S0, new GoodId(cfg.Goods!.IdOf("livestock")), 1.0));
            foreach (GoodEntry g in cfg.Goods!.Goods)
                w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            if (withRow) w.HarvestWeather.Add(new HarvestWeatherRow(S0, 0.0, 1.0));
            var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Production(cfg)]);
            WorldState next = exec.Step(w);
            int id = cfg.Goods!.IdOf("livestock");
            for (int i = 0; i < next.GoodStocks.Count; i++)
                if (next.GoodStocks[i].Good.Value == id) return next.GoodStocks[i].Amount.Value;
            return 0;
        }

        Assert.Equal(Produced(withRow: true), Produced(withRow: false));
    }

    // --- E. THE RAID ----------------------------------------------------------

    [Fact]
    public void StatelessDeficit_AppropriatesExactlyTheShortfall_LedgerExact()
    {
        // The authorized model: the amount taken is the raider's OWN measured
        // shortfall, DeficitRatio x DemandUnits. No constant, no raid size.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = TwoSettlements(cfg, grain0: 0, grain1: 1000);
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, DeficitRatio: 0.25, DemandUnits: 400));

        long before = WorldGrain(w, cfg);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Appropriation(cfg)]);
        WorldState next = exec.Step(w);

        Assert.Equal(100, Grain(next, cfg, S0));   // 0.25 x 400, exactly
        Assert.Equal(900, Grain(next, cfg, S1));
        Assert.Equal(before, WorldGrain(next, cfg)); // NOTHING created or destroyed
    }

    [Fact]
    public void FedStatelessSettlement_TakesNothing_TheRaidIsNotPeriodic()
    {
        // The anti-"unconditional periodic raid" pin. Same world, same stateless
        // raider, ten consecutive turns, no deficit row: the take must be zero
        // every single turn. A timer or cooldown disguised as a deficit rule
        // would fire here.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = TwoSettlements(cfg, grain0: 0, grain1: 1000);
        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Appropriation(cfg)]);

        for (int t = 0; t < 10; t++)
        {
            w = exec.Step(w);
            Assert.Equal(0, Grain(w, cfg, S0));
            Assert.Equal(1000, Grain(w, cfg, S1));
        }

        // And a ZERO-ratio row is still "fed": presence of the row is not hunger.
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, DeficitRatio: 0.0, DemandUnits: 400));
        w = exec.Step(w);
        Assert.Equal(0, Grain(w, cfg, S0));
    }

    [Fact]
    public void Appropriation_CannotDriveTheVictimNegative_NorMintGrain()
    {
        // Unopposed does not mean unbounded. The victim holds 30; the shortfall
        // is 1,000. The transfer clamps: the victim bottoms at zero and the
        // raider receives EXACTLY what existed, never the amount it wanted.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = TwoSettlements(cfg, grain0: 0, grain1: 30);
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, DeficitRatio: 1.0, DemandUnits: 1000));

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Appropriation(cfg)]);
        WorldState next = exec.Step(w);

        Assert.Equal(30, Grain(next, cfg, S0));
        Assert.Equal(0, Grain(next, cfg, S1));
        Assert.Equal(30, WorldGrain(next, cfg));
        Assert.True(Grain(next, cfg, S1) >= 0, "a stock went negative");
    }

    [Fact]
    public void Appropriation_IsDeterministic_AndBreaksTiesByLowestSettlementId()
    {
        // Two equally-rich victims: the tie must resolve to the LOWEST id, and
        // must do so identically on every run (composite key (stock desc, id asc)).
        SimConfig cfg = TestConfigs.Sim();

        WorldState Build()
        {
            var w = new WorldState(7);
            w.Settlements.Add(new SettlementRow(new SettlementId(0), 0, 0));
            w.Settlements.Add(new SettlementRow(new SettlementId(1), 1, 0));
            w.Settlements.Add(new SettlementRow(new SettlementId(2), 2, 0));
            w.SectorAllocations.Add(new SectorAllocationRow(new SettlementId(0), 0.0, 1.0, 0.0, 0.0, 0.0));
            var ledger = new Ledger(w.LedgerFlows);
            var grain = new GoodId(cfg.Goods!.GrainId);
            foreach ((int s, long amount) in new[] { (0, 0L), (1, 500L), (2, 500L) })
            {
                int row = w.GoodStocks.Add(new GoodStockRow(
                    new SettlementId(s), grain, Conserved.Zero, 0.0, 0.0));
                if (amount > 0)
                    ledger.Flow(ref w.GoodStocks.Ref(row).Amount,
                        ConservedQuantityIds.OfGood(grain), ReasonIds.InitialEndowment,
                        amount, FlowDirection.Source, OverdrawPolicy.Throw);
            }
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(
                new SettlementId(0), DeficitRatio: 0.2, DemandUnits: 500));
            return w;
        }

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Appropriation(cfg)]);
        WorldState a = exec.Step(Build());
        WorldState b = exec.Step(Build());

        Assert.Equal(100, Grain(a, cfg, new SettlementId(0)));
        Assert.Equal(400, Grain(a, cfg, new SettlementId(1))); // lowest id pays
        Assert.Equal(500, Grain(a, cfg, new SettlementId(2)));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b)); // bit-exact repeat
    }

    // --- F. STATE BOUNDARY ----------------------------------------------------

    [Fact]
    public void ControlledSettlement_InDeficit_DoesNotAppropriate()
    {
        // THE DISCRIMINATOR IS THE CONTROL ROW, not the deficit. Identical world,
        // identical hunger — the only difference is that S0 is controlled by a
        // polity, and the raid must not happen.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = TwoSettlements(cfg, grain0: 0, grain1: 1000);
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, DeficitRatio: 0.25, DemandUnits: 400));
        w.Controls.Add(new ControlRow(new PolityId(1), S0, Strength: 1.0));

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Appropriation(cfg)]);
        WorldState next = exec.Step(w);

        Assert.Equal(0, Grain(next, cfg, S0));
        Assert.Equal(1000, Grain(next, cfg, S1));
    }

    [Fact]
    public void RemovingTheControlRow_RestoresTheBehaviour_ProvingTheDiscriminator()
    {
        // The paired half of the boundary: same world WITHOUT the control row
        // raids. Together these two tests prove the control row is the actual
        // discriminator rather than something incidental to the fixture.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = TwoSettlements(cfg, grain0: 0, grain1: 1000);
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, DeficitRatio: 0.25, DemandUnits: 400));

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Appropriation(cfg)]);
        Assert.Equal(100, Grain(exec.Step(w), cfg, S0));
    }

    [Fact]
    public void FarmingVillage_Stateless_AndHungry_StillDoesNotRaid()
    {
        // THE SECOND HALF OF THE DISCRIMINATOR. D-037 B3's raiders are
        // PASTORALISTS, not merely uncontrolled settlements — and since nothing
        // writes Controls yet, statelessness alone is true of every settlement in
        // every live world. Without this condition an ordinary starving village
        // would take its neighbour's grain, which is famine relief, not steppe
        // raiding: measured, it emptied CollapseStabilityTests' food-less-death
        // precondition and made an ADR-012 resurrection guard vacuous.
        //
        // Same hunger, same statelessness, ALL-FARMING allocation: no raid.
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = TwoSettlements(cfg, grain0: 0, grain1: 1000);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == S0)
                w.SectorAllocations[i] = new SectorAllocationRow(S0, 1.0, 0.0, 0.0, 0.0, 0.0);
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, DeficitRatio: 0.25, DemandUnits: 400));

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Appropriation(cfg)]);
        WorldState next = exec.Step(w);

        Assert.Equal(0, Grain(next, cfg, S0));
        Assert.Equal(1000, Grain(next, cfg, S1));
    }

    [Fact]
    public void EvenSplitBetweenPloughAndHerd_IsNotHerdingDominant()
    {
        // The tie rule, pinned: dominance is STRICT. A settlement split evenly
        // between farming and herding is a mixed village, not a herding people,
        // and must not raid — otherwise "dominant" would quietly mean "present".
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = TwoSettlements(cfg, grain0: 0, grain1: 1000);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == S0)
                w.SectorAllocations[i] = new SectorAllocationRow(S0, 0.5, 0.5, 0.0, 0.0, 0.0);
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, DeficitRatio: 0.25, DemandUnits: 400));

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Appropriation(cfg)]);
        Assert.Equal(0, Grain(exec.Step(w), cfg, S0));
    }

    // --- D + E END TO END: DROUGHT ALL THE WAY TO THE RAID --------------------

    [Fact]
    public void Drought_StarvesTheHerders_AndSendsThemAfterGrain_FullChain()
    {
        // D-037 B3's sentence, executed: "the same bad year that starves villages
        // sends herders after grain." Real systems, one pipeline, NO hand-set
        // deficit — production and consumption generate the hunger themselves.
        //
        // S0 is a MARGINAL MIXED settlement (half farming, half herding, a small
        // catchment) and S1 is a large farming neighbour that always has a good
        // year. Marginal, not pure-pastoralist, and the reason is a real property
        // of the model worth recording: the consumption basket is grain-dominant
        // and a non-staple surplus substitutes INTO the staple but never the
        // reverse, so a settlement with only livestock is short of grain in every
        // weather and its deficit carries no drought signal at all. A settlement
        // that is marginal is the one whose subsistence a bad year can actually
        // break, which is exactly D-037 B3's "marginal terrain".
        SimConfig cfg = TestConfigs.Sim();

        (long Ate, long Grew, double WorstDeficit) Run(double weather)
        {
            var w = new WorldState(7);
            w.Settlements.Add(new SettlementRow(S0, 0, 0)); // marginal herders
            w.Settlements.Add(new SettlementRow(S1, 1, 0)); // the granary next door
            var ledger = new Ledger(w.LedgerFlows);
            for (int c = 0; c < Cohorts.Count; c++)
                foreach (SettlementId s in new[] { S0, S1 })
                {
                    int row = w.Buckets.Add(new BucketRow(
                        s, new CultureId(1), new ReligionId(1), new ClassId(1),
                        c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
                    if (c == 5)
                        ledger.Flow(ref w.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                            ReasonIds.InitialEndowment, 300, FlowDirection.Source, OverdrawPolicy.Throw);
                }
            foreach (SettlementId s in new[] { S0, S1 })
            {
                bool herder = s == S0;
                w.SectorAllocations.Add(herder
                    ? new SectorAllocationRow(s, 0.4, 0.6, 0.0, 0.0, 0.0)
                    : new SectorAllocationRow(s, 1.0, 0.0, 0.0, 0.0, 0.0));
                if (herder) w.Deposits.Add(new DepositRow(s, new GoodId(cfg.Goods!.IdOf("livestock")), 1.0));
                w.CatchmentSummaries.Add(new CatchmentSummaryRow(
                    s, NodeCount: 1, EffectiveArableKm2: herder ? 5_000.0 : 40_000.0,
                    NetworkRevision: 0, LastRecomputeTurn: 0));
                // Only S0's weather varies. S1 keeps a good year throughout, so
                // every difference between the two arms is the bad year at S0.
                w.HarvestWeather.Add(new HarvestWeatherRow(s, 0.0, herder ? weather : 1.0));
                foreach (GoodEntry g in cfg.Goods!.Goods)
                    w.GoodStocks.Add(new GoodStockRow(s, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            }

            var exec = new TurnExecutor(FlatEra(10.0),
                [SystemCatalog.Production(cfg), SystemCatalog.Appropriation(cfg), SystemCatalog.Consumption(cfg)]);
            // TOTALS over the window, never a single turn: the mechanism is
            // self-correcting and therefore PHASE-DEPENDENT — hunger in turn t
            // drives a raid in t+1 which feeds them, so t+2 needs no raid.
            long ate = 0, grew = 0; double worst = 0.0;
            for (int t = 0; t < 6; t++)
            {
                w = exec.Step(w);
                ate += GrainEaten(w, cfg, S0);
                grew += GrainProduced(w, cfg, S0);
                worst = Math.Max(worst, Deficit(w, S0));
            }
            return (ate, grew, worst);
        }

        (long wetAte, long wetGrew, double wetWorst) = Run(1.0);
        (long dryAte, long dryGrew, double dryWorst) = Run(0.05);

        // 1. THE BAD YEAR CREATES THE HUNGER. A good year never does.
        Assert.Equal(0.0, wetWorst);
        Assert.True(dryWorst > 0.0, "the drought produced no deficit at all — the chain is vacuous");

        // 2. THE HUNGRY SETTLEMENT EATS GRAIN IT DID NOT GROW. Its own harvest
        //    collapsed with the weather, so the surplus it ate has exactly one
        //    possible origin: the neighbour's granary. This is the appropriation,
        //    observed end to end without ever setting a deficit by hand.
        Assert.True(dryAte > dryGrew,
            $"the herders ate only what they grew — no appropriation: ate={dryAte} grew={dryGrew}");

        // 3. AND THE FED SETTLEMENT FEEDS ITSELF. In a good year S0 never eats
        //    more grain than it grew, so appropriation is not an unconditional
        //    background flow that happens to be larger under drought.
        Assert.True(wetAte <= wetGrew,
            $"a fed settlement still took grain: ate={wetAte} grew={wetGrew}");

        Assert.True(dryWorst > wetWorst);
    }

    private static double Deficit(WorldState w, SettlementId s)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (w.ConsumptionDeficits[i].Settlement == s) return w.ConsumptionDeficits[i].DeficitRatio;
        return 0.0;
    }

    /// <summary>Grain this settlement GREW on the last completed turn.</summary>
    private static long GrainProduced(WorldState w, SimConfig cfg, SettlementId s)
    {
        int id = cfg.Goods!.GrainId;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement == s && w.GoodStocks[i].Good.Value == id)
                return w.GoodStocks[i].LastProducedUnits;
        return 0;
    }

    /// <summary>Grain this settlement ate on the last completed turn — the
    /// per-settlement observational field ConsumptionSystem already publishes.</summary>
    private static long GrainEaten(WorldState w, SimConfig cfg, SettlementId s)
    {
        int id = cfg.Goods!.GrainId;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement == s && w.GoodStocks[i].Good.Value == id)
                return w.GoodStocks[i].LastConsumptionEatenUnits;
        return 0;
    }

    // --- G. RESISTANCE BOUNDARY ----------------------------------------------

    [Fact]
    public void T45_IntroducesNoCombatOrResistanceMechanism()
    {
        // Scope fence as a test so it cannot rot: T4.8 owns conflict. T4.5 ships
        // unopposed appropriation, so no combat vocabulary may appear in the
        // system it added, and the system may own no state of its own.
        string source = System.IO.File.ReadAllText(RepoPath(
            "Sim.Core/Systems/Appropriation/AppropriationSystem.cs"));
        foreach (string banned in new[]
                 { "Battle", "Combat", "Resist", "Attack", "Defen", "Casualt", "Morale", "Strength =" })
            Assert.DoesNotContain(banned, source, StringComparison.Ordinal);

        // No RNG: a raid is never a random event (D-037 B3 forbids the timer/stat shape).
        Assert.DoesNotContain("ctx.Rng", source, StringComparison.Ordinal);
    }

    private static string RepoPath(string relative)
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return System.IO.Path.Combine(dir!.FullName, relative);
    }
}
