using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T2.6 acceptance (m2 spec §4, D-018/D-021): famine raises grievance in the
// starvation window; plenty decays it slowly (measured, documented half-life
// at canonical TUNE values); cohort replacement decays it strictly faster
// (generational decay); satisfaction clamps at both ends; an UNBOUND need
// contributes exactly nothing whatever its weight; the grievance integration
// is dt-correct (first-order); and the read-isolation gate has teeth
// (scripts/check-read-isolation.sh — injection demonstrated in the packet
// evidence, allowlist reviewed in the script itself).
public class NeedsGrievanceTests
{
    /// <summary>The canonical pipeline MINUS harvest weather (T3.4b). This file's
    /// rigs are CONTROLLED EXPERIMENTS on famine mechanics — they compare a
    /// deliberately starved run against a fed baseline, and the contrast IS the
    /// measurement. Background weather noise is a confound on both arms: at the
    /// derived sigma the famine arm measured LOWER per-capita mortality than its
    /// own baseline (0.609 vs 0.707) simply because the baseline had bad years
    /// too. A controlled experiment must isolate its variable. Weather's own
    /// behaviour is covered by the quarantine families and the migration
    /// profile; excluding it here restores the contrast these rigs exist to
    /// measure. Legitimate because an absent weather row means multiplier 1.0
    /// BY DESIGN (ProductionSystem) — nothing about farming changes.</summary>
    private static SystemRegistration[] WithoutWeather(SystemRegistration[] all)
    {
        var kept = new List<SystemRegistration>();
        foreach (SystemRegistration r in all)
            if (r.Name != Sim.Core.Systems.Harvest.HarvestWeatherSystem.Name) kept.Add(r);
        return [.. kept];
    }

    private const ulong Seed = 42;

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    private static TurnExecutor ProductionExecutor(SimConfig cfg)
    {
        using var stream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(CanonicalEra(), WithoutWeather(PipelineLoader.Load(stream, SystemCatalog.All(cfg))));
    }

    private static TurnExecutor GrievanceOnly(SimConfig cfg, double dt = 10.0) =>
        new(FlatEra(dt), [SystemCatalog.NeedsGrievance(cfg)]);

    /// <summary>Hand-built K-settlement world for grievance-only stepping: one
    /// 1000-person bucket per settlement (class 1, cohort 5), a zeroed
    /// grievance row each, deficits/vitals seeded by the caller.</summary>
    private static WorldState GrievanceWorld(int settlements)
    {
        var world = new WorldState(11);
        var ledger = new Ledger(world.LedgerFlows);
        for (int s = 0; s < settlements; s++)
        {
            var id = new SettlementId(s);
            world.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            int row = world.Buckets.Add(new BucketRow(
                id, new CultureId(1), new ReligionId(1), new ClassId(1),
                cohortIdx: 5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, 1000, FlowDirection.Source, OverdrawPolicy.Throw);
            world.Grievances.Add(new GrievanceRow(id, new ClassId(1), 0.0));
        }
        return world;
    }

    private static double Grievance(WorldState world, int settlement)
    {
        for (int i = 0; i < world.Grievances.Count; i++)
            if (world.Grievances[i].Settlement.Value == settlement) return world.Grievances[i].Value;
        Assert.Fail($"no grievance row for settlement {settlement}");
        return 0.0;
    }


    /// <summary>T3.5: rig one settlement's published fill ratio for a good —
    /// the pair (pre-clamp demand, post-clamp obtained) NeedsGrievanceSystem
    /// reads from Prev. 1000 is an arbitrary round denominator; only the RATIO
    /// is read. A good with no row at all reads fill 1.0 by design, so a rig
    /// that wants a shortfall must state it here.</summary>
    private static void Fill(WorldState world, int settlement, string good, double ratio)
    {
        GoodsConfig goods = TestConfigs.Sim().Goods!;
        int id = goods.IdOf(good);
        Assert.True(id > 0, $"unknown good '{good}'");
        const long Demand = 1000;
        var row = new GoodStockRow(
            new SettlementId(settlement), new GoodId(id), Conserved.Zero, 0.0, 0.0,
            lastConsumptionDemandUnits: Demand,
            lastConsumptionEatenUnits: (long)Math.Round(Demand * ratio));
        world.GoodStocks.Add(row);
    }

    /// <summary>Every good any basket wants, filled at <paramref name="ratio"/>.</summary>
    private static void FillAll(WorldState world, int settlement, double ratio)
    {
        foreach (string good in AllBasketGoods()) Fill(world, settlement, good, ratio);
    }

    private static string[] AllBasketGoods()
    {
        NeedsConfig needs = TestConfigs.Sim().Needs!;
        var names = new List<string>();
        foreach (BasketEntry e in needs.Baskets.Entries) if (!names.Contains(e.Good)) names.Add(e.Good);
        names.Sort(StringComparer.Ordinal);
        return [.. names];
    }

    /// <summary>The canonical config with SUSTENANCE ALONE bound — the M2
    /// binding, restored deliberately for the rigs whose claim is about food.
    /// T3.5 binds Shelter and Comfort, and an all-farming settlement produces
    /// neither timber nor cloth, so in a production autoplay those two sit at
    /// zero and dominate the aggregate: a famine's contribution is a rounding
    /// error against a background already near its limit, and the arm that
    /// starves decays FASTER (generational turnover) than the arm that does
    /// not. The confounded-rig doctrine applies directly — a controlled
    /// experiment must isolate its variable, and here the variable is
    /// Sustenance. Rigging the Bound flags is data rigging, which the
    /// constitution permits; the Shelter binding is measured by its own tests.
    /// </summary>
    private static SimConfig SustenanceOnlyConfig(SimConfig cfg)
    {
        var needs = (NeedEntry[])cfg.Needs!.Needs.Clone();
        var kept = new List<BasketEntry>();
        for (int i = 0; i < needs.Length; i++)
            needs[i] = needs[i] with { Bound = needs[i].Id == 1 };
        foreach (BasketEntry e in cfg.Needs.Baskets.Entries) if (e.Need == 1) kept.Add(e);
        return cfg with
        {
            Needs = cfg.Needs with { Needs = needs, Baskets = new BasketsConfig([.. kept]) },
        };
    }

    /// <summary>The canonical config with every bound need's varietyWeight
    /// zeroed. Used ONLY by the rigs that isolate the grievance INTEGRATION
    /// (decay, generational turnover, dt-correctness): with variety live, a
    /// fully-supplied staple-heavy basket still scores below 1.0 by design
    /// (D-035-A), so "fed" would never mean zero accrual and a decay-only
    /// measurement would be measuring accrual too. Zeroing the coefficient is
    /// TUNE rigging, which the constitution permits; the variety mechanism
    /// itself is measured by its own tests, not by these.</summary>
    private static SimConfig VarietyOffConfig()
    {
        SimConfig cfg = TestConfigs.Sim();
        var needs = (NeedEntry[])cfg.Needs!.Needs.Clone();
        for (int i = 0; i < needs.Length; i++) needs[i] = needs[i] with { VarietyWeight = 0.0 };
        return cfg with { Needs = cfg.Needs with { Needs = needs } };
    }

    // --- the canonical registry ---------------------------------------------

    [Fact]
    public void CanonicalRegistry_TheEightNeeds_SustenanceShelterComfortBoundAtM3()
    {
        // The D-018 §3 ladder is FROZEN design: exactly these eight, in this
        // order. WHICH are bound grows by milestone and is the scope fence:
        // M2 bound Sustenance alone; T3.5 binds Shelter and Comfort against
        // real goods baskets. The other five stay unbound and inert.
        NeedsConfig needs = TestConfigs.Sim().Needs!;
        string[] ladder = ["Sustenance", "Shelter", "Safety", "Health",
                           "Belonging/Faith", "Comfort", "Dignity/Liberty", "Prospects"];
        string[] boundAtM3 = ["Sustenance", "Shelter", "Comfort"];
        Assert.Equal(ladder.Length, needs.Needs.Length);
        for (int i = 0; i < ladder.Length; i++)
        {
            Assert.Equal(ladder[i], needs.Needs[i].Name);
            Assert.Equal(Array.IndexOf(boundAtM3, ladder[i]) >= 0, needs.Needs[i].Bound);
        }
        // Every bound need is served by a basket, and no unbound need is —
        // a basket for an unbound need would be dead data that silently comes
        // alive the day someone flips a flag.
        foreach (NeedEntry need in needs.Needs)
        {
            bool served = false;
            foreach (BasketEntry e in needs.Baskets.Entries) if (e.Need == need.Id) { served = true; break; }
            Assert.Equal(need.Bound, served);
        }
    }

    // --- famine raises grievance --------------------------------------------

    [Fact]
    public void Famine_RaisesGrievance_InTheStarvationWindow()
    {
        // The T2.7 shallow-famine rig, re-rigged at T3.5 as a CONTROLLED
        // TWO-ARM CONTRAST. The old form asserted the fed prelude was EXACTLY
        // zero grievance; that is no longer true and the change is real rather
        // than incidental — T3.5 binds Shelter and Comfort, an all-farming
        // settlement produces neither timber nor cloth, and so it carries
        // standing grievance before any famine arrives. Asserting an absolute
        // level would now be asserting the value of a saturated background.
        //
        // What the packet actually claims is a CONTRAST: starvation raises
        // grievance ABOVE what the same world would have carried without it. So
        // both arms are stepped from one shared prefix over the same turns and
        // only the harvest differs. That is the claim, isolated.
        SimConfig fed = SustenanceOnlyConfig(TestConfigs.Sim());
        fed = fed with
        {
            Farming = fed.Farming with { YieldPerArableKm2PerYear = 1000.0, OutputPerFarmerPerYear = 1.45 },
            Founding = fed.Founding with { FoodStore = 4000 },
        };
        SimConfig starving = fed with
        {
            Farming = fed.Farming with { YieldPerArableKm2PerYear = 1000.0, OutputPerFarmerPerYear = 1.05 },
        };
        TurnExecutor fedExec = ProductionExecutor(fed);
        TurnExecutor famineExec = ProductionExecutor(starving);

        WorldState prefix = WorldFounding.Found(TestConfigs.DevWorldgen(), fed, Seed, 1);
        for (int t = 1; t <= 6; t++) prefix = fedExec.Step(prefix);

        WorldState control = prefix.Clone(), famine = prefix.Clone();
        long starvedBefore = StarvedTotal(prefix);
        for (int t = 7; t <= 11; t++)
        {
            control = fedExec.Step(control);
            famine = famineExec.Step(famine);
        }
        control = fedExec.Step(control);   // the lagged last turn (Prev deficit)
        famine = fedExec.Step(famine);

        Assert.Equal(starvedBefore, StarvedTotal(control));   // the control arm never starves
        Assert.True(StarvedTotal(famine) > starvedBefore, "rig vacuous: nobody starved in the window");
        Assert.True(Grievance(famine, 0) > Grievance(control, 0) + 1.0,
            $"starvation did not raise grievance above the fed control: "
            + $"famine {Grievance(famine, 0):F2} vs control {Grievance(control, 0):F2}");
        Console.WriteLine(
            $"famine grievance: control {Grievance(control, 0):F2} -> famine {Grievance(famine, 0):F2} "
            + "across the starvation window");
    }

    private static long StarvedTotal(WorldState world)
    {
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == ConservedQuantityIds.Population && row.Reason == ReasonIds.Starvation)
                return row.TotalSunk;
        }
        return 0;
    }

    // --- plenty decays it (measured half-life) ------------------------------

    [Fact]
    public void Plenty_DecaysGrievance_MeasuredHalfLifeDocumented()
    {
        // A world carrying a hand-seeded grievance of 10 (grievance is a
        // non-conserved double stock — test-side seeding is legal rigging, no
        // Ledger involved) with EVERY basket fully supplied, so accrual is
        // exactly zero and what is measured is decay alone.
        //
        // T3.5 re-rig: this used to be a fed PRODUCTION autoplay. An
        // all-farming settlement now produces no timber, stone, pottery or
        // cloth, so "fed" no longer means "supplied" — Shelter and Comfort sit
        // at zero and grievance climbs instead of decaying. The rig moves to a
        // hand world where full supply can actually be stated, and the turnover
        // term is supplied explicitly rather than emerging from demography.
        //
        // At canonical TUNEs decayRate = baseDecay (0.005/yr) + (1 − inherit
        // 0.85) × turnover, with turnover ≈ CBR + CDR ≈ 0.074/yr in a fed
        // regime → ≈ 0.016/yr, analytic half-life ≈ 43 years ≈ 4 turns at
        // dt = 10. ASSERTED BAND: [2, 8] turns (20–80 years) — slow (grudges
        // outlive their causes) but not immortal (D-021 §8).
        SimConfig cfg = VarietyOffConfig();
        const double dt = 10.0, turnover = 0.074;
        WorldState world = GrievanceWorld(1);
        FillAll(world, 0, 1.0);
        world.SettlementVitals.Add(new SettlementVitalsRow(
            new SettlementId(0), Births: (long)(1000 * turnover * dt / 2), Deaths: (long)(1000 * turnover * dt / 2), DtYears: dt));
        world.Grievances[0] = world.Grievances[0] with { Value = 10.0 };

        TurnExecutor exec = GrievanceOnly(cfg, dt);
        int halfTurn = -1;
        for (int t = 1; t <= 20; t++)
        {
            world = exec.Step(world);
            Assert.Equal(1.0, SustenanceValue(world, 0));   // the rig IS supplied — decay only
            if (Grievance(world, 0) <= 5.0) { halfTurn = t; break; }
        }
        Assert.True(halfTurn is >= 2 and <= 8,
            $"grievance half-life {halfTurn} turns outside [2, 8] (20–80 sim-years)");
        Console.WriteLine(
            $"grievance half-life under plenty: {halfTurn} turns ({halfTurn * 10} sim-years) "
            + $"at baseDecay {cfg.Needs!.Grievance.BaseDecayPerYear}, inherit {cfg.Needs!.Grievance.InheritFraction}");
    }

    private static double MaxDeficit(WorldState world)
    {
        double max = 0.0;
        for (int i = 0; i < world.ConsumptionDeficits.Count; i++)
            max = Math.Max(max, world.ConsumptionDeficits[i].DeficitRatio);
        return max;
    }

    // --- generational decay: strictly faster on turnover --------------------

    [Fact]
    public void GenerationalDecay_EqualGrief_HigherTurnoverDecaysStrictlyFaster()
    {
        // The rigged two-settlement comparison the packet demands: equal
        // grievance (10.0), equal everything except the PREV vitals row —
        // settlement 0 turned over nobody, settlement 1 turned over 750 of its
        // 1000 people this decade. One grievance-only step: the high-turnover
        // settlement's stock must sit STRICTLY below the quiet one's, and both
        // must match the hand recurrence bit-exactly.
        SimConfig cfg = VarietyOffConfig();
        GrievanceTuning g = cfg.Needs!.Grievance;
        const double dt = 10.0;
        WorldState world = GrievanceWorld(2);
        // Both settlements fully supplied: accrual is exactly zero on both arms,
        // so the only difference between them is the turnover term (T3.5 —
        // an unsupplied basket would add an identical accrual to both and blunt
        // the contrast this rig exists to make sharp).
        FillAll(world, 0, 1.0);
        FillAll(world, 1, 1.0);
        for (int i = 0; i < world.Grievances.Count; i++)
            world.Grievances[i] = world.Grievances[i] with { Value = 10.0 };
        world.SettlementVitals.Add(new SettlementVitalsRow(new SettlementId(0), 0, 0, dt));
        world.SettlementVitals.Add(new SettlementVitalsRow(new SettlementId(1), 400, 350, dt));

        WorldState next = GrievanceOnly(cfg, dt).Step(world);

        double quietDecay = g.BaseDecayPerYear;
        double turnover = (400 + 350) / 1000.0 / dt;
        double churnDecay = g.BaseDecayPerYear + (1.0 - g.InheritFraction) * turnover;
        double expectedQuiet = Math.Max(0.0, 10.0 + 0.0 * dt - quietDecay * 10.0 * dt);
        double expectedChurn = Math.Max(0.0, 10.0 + 0.0 * dt - churnDecay * 10.0 * dt);
        Assert.Equal(expectedQuiet, Grievance(next, 0));
        Assert.Equal(expectedChurn, Grievance(next, 1));
        Assert.True(Grievance(next, 1) < Grievance(next, 0),
            $"generational decay not faster: churn {Grievance(next, 1):F4} !< quiet {Grievance(next, 0):F4}");
    }

    // --- satisfaction clamps at both ends -----------------------------------

    [Fact]
    public void Satisfaction_ClampsAtBothEnds()
    {
        // T3.5 re-rigged: satisfaction is now driven by published FILL RATIOS,
        // not by a deficit row. Settlement 0 obtains NOTHING of any basket good
        // (fill 0) and must read EXACTLY 0.0; settlement 1 obtains everything
        // (fill 1) and must read EXACTLY 1.0 — the variety coefficient is
        // zeroed here so the quantity clamp is measured alone, and 0/1 are the
        // clamp's own endpoints rather than values approached from inside.
        // Accrual follows the clamped values: maximal at s = 0, EXACTLY none at
        // s = 1 (grievance is a stock founding at zero, so "none" is testable
        // as bit-exact zero rather than as a small number).
        SimConfig cfg = VarietyOffConfig();
        const double dt = 10.0;
        WorldState world = GrievanceWorld(2);
        FillAll(world, 0, 0.0);
        FillAll(world, 1, 1.0);

        WorldState next = GrievanceOnly(cfg, dt).Step(world);

        Assert.Equal(0.0, SustenanceValue(next, 0)); // clamped low
        Assert.Equal(1.0, SustenanceValue(next, 1)); // clamped high
        Assert.Equal(0.0, Grievance(next, 1));       // nothing unmet -> nothing accrued
        Assert.True(Grievance(next, 0) > 0.0,
            "starved settlement accrued no grievance — the rig is vacuous");
    }

    private static double SustenanceValue(WorldState world, int settlement)
    {
        for (int i = 0; i < world.NeedSatisfactions.Count; i++)
        {
            NeedSatisfactionRow row = world.NeedSatisfactions[i];
            if (row.Settlement.Value == settlement && row.NeedId == 1) return row.Value;
        }
        Assert.Fail($"no Sustenance satisfaction row for settlement {settlement}");
        return 0.0;
    }

    // --- unbound needs contribute EXACTLY nothing ---------------------------

    [Fact]
    public void UnboundNeed_HugeWeight_ZeroEffect_BitExact()
    {
        // The packet's rig: crank an UNBOUND need's weight to 1e9 and run the
        // same famine world — the entire world state must be BIT-IDENTICAL
        // (hash equality), because unbound needs are skipped entirely, not
        // merely small. A "multiply by weight but forget the bound gate"
        // regression detonates instantly against a 1e9 multiplier.
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with
        {
            Farming = cfg.Farming with { YieldPerArableKm2PerYear = 1000.0, OutputPerFarmerPerYear = 1.05 },
            Founding = cfg.Founding with { FoodStore = 2000 },
        };
        var riggedNeeds = (NeedEntry[])cfg.Needs!.Needs.Clone();
        // Index 2 is SAFETY. It was Shelter at T2.6; T3.5 BOUND Shelter, so
        // rigging index 1 would now be rigging a live need and the test would
        // be asserting that a bound need does nothing. The gate is asserted
        // below rather than assumed, so this cannot rot silently again.
        Assert.False(riggedNeeds[2].Bound, "rig targets a BOUND need — the inertness claim is void");
        riggedNeeds[2] = riggedNeeds[2] with { Weight = 1e9 }; // Safety, unbound
        SimConfig rigged = cfg with { Needs = cfg.Needs with { Needs = riggedNeeds } };

        WorldState a = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, 1);
        WorldState b = WorldFounding.Found(TestConfigs.DevWorldgen(), rigged, Seed, 1);
        TurnExecutor execA = ProductionExecutor(cfg);
        TurnExecutor execB = ProductionExecutor(rigged);
        for (int t = 1; t <= 12; t++) { a = execA.Step(a); b = execB.Step(b); }

        Assert.True(Grievance(a, 0) > 0.0, "rig vacuous: famine never accrued grievance");
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }

    // --- dt-correctness of the integration ----------------------------------

    [Fact]
    public void GrievanceIntegration_DtHalving_FirstOrderConvergence()
    {
        // Law-3 pin: constant basket fill 0.5 (hand rows, no consumption in the
        // pipeline to overwrite them), same 40 sim-years at dt 10 / 5 / 2.5 —
        // successive refinements of the explicit-Euler accrual−decay step
        // should roughly halve the terminal-value deviation.
        SimConfig cfg = VarietyOffConfig();
        const int horizonYears = 40;
        var finals = new double[3];
        double[] dts = [10.0, 5.0, 2.5];
        for (int i = 0; i < dts.Length; i++)
        {
            WorldState world = GrievanceWorld(1);
            FillAll(world, 0, 0.5);   // constant half-supplied baskets (no consumption in the pipeline to overwrite them)
            var exec = GrievanceOnly(cfg, dts[i]);
            world = exec.Run(world, (int)(horizonYears / dts[i]));
            finals[i] = Grievance(world, 0);
        }

        double l1Coarse = Math.Abs(finals[0] - finals[1]);
        double l1Fine = Math.Abs(finals[1] - finals[2]);
        Assert.True(l1Coarse > 0 && l1Fine > 0, "dt-halving produced no deviation — vacuous");
        double ratio = l1Coarse / l1Fine;
        Assert.True(ratio is >= 1.4 and <= 3.5,
            $"grievance convergence ratio {ratio:F2} outside [1.4, 3.5] "
            + $"(G: dt10 {finals[0]:F4}, dt5 {finals[1]:F4}, dt2.5 {finals[2]:F4})");
    }

    // --- the vitals chronicle feeding turnover ------------------------------

    [Fact]
    public void VitalsChronicle_MatchesLedgerDeltas_PerTurn()
    {
        // SettlementVitals is the D-021 turnover input — its counts must equal
        // the Ledger's per-turn birth/death(+starvation) deltas exactly on an
        // N = 1 world (where per-settlement and global aggregates coincide).
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, 1);
        (long pb, long pd, long ps) = (0, 0, 0);
        for (int t = 1; t <= 10; t++)
        {
            world = exec.Step(world);
            (long b, long d, long s) = Vitals(world);
            Assert.Equal(1, world.SettlementVitals.Count);
            SettlementVitalsRow row = world.SettlementVitals[0];
            Assert.Equal(b - pb, row.Births);
            Assert.Equal((d - pd) + (s - ps), row.Deaths);
            Assert.Equal(10.0, row.DtYears);
            (pb, pd, ps) = (b, d, s);
        }
    }

    private static (long B, long D, long S) Vitals(WorldState world)
    {
        long b = 0, d = 0, s = 0;
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity != ConservedQuantityIds.Population) continue;
            if (row.Reason == ReasonIds.Births) b = row.TotalSourced;
            else if (row.Reason == ReasonIds.Deaths) d = row.TotalSunk;
            else if (row.Reason == ReasonIds.Starvation) s = row.TotalSunk;
        }
        return (b, d, s);
    }

    [Fact]
    public void ExtinctSettlement_GrievanceZeroed_NoSatisfactionRows_NoGhostStock()
    {
        // T2.13 (director exit-session finding, ghost grievance): grievance
        // is held by people. An extinct settlement carrying a nonzero stock
        // must read EXACTLY ZERO after the next step - not linger and decay
        // for centuries in an empty ruin - and publishes no satisfaction rows
        // (nobody to be satisfied). A living neighbor is untouched.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = GrievanceWorld(2);
        // Settlement 0: carried grievance, then extinguished.
        world.Grievances[0] = world.Grievances[0] with { Value = 7.5 };
        new Ledger(world.LedgerFlows).Flow(ref world.Buckets.Ref(0).Count,
            ConservedQuantityIds.Population, ReasonIds.Starvation, 1000,
            FlowDirection.Sink, OverdrawPolicy.Throw);
        // Settlement 1: alive, carrying its own stock.
        world.Grievances[1] = world.Grievances[1] with { Value = 2.0 };

        WorldState next = GrievanceOnly(cfg).Step(world);

        Assert.Equal(0.0, Grievance(next, 0)); // EXACT zero, not decayed-toward
        Assert.True(Grievance(next, 1) > 0.0, "living neighbor's stock vanished too - overreach");
        for (int i = 0; i < next.NeedSatisfactions.Count; i++)
            Assert.NotEqual(0, next.NeedSatisfactions[i].Settlement.Value);
    }
}
