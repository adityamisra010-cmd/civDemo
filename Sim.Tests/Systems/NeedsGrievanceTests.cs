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
        return new TurnExecutor(CanonicalEra(), PipelineLoader.Load(stream, SystemCatalog.All(cfg)));
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

    // --- the canonical registry ---------------------------------------------

    [Fact]
    public void CanonicalRegistry_TheEightNeeds_SustenanceOnlyBoundAtM2()
    {
        // The D-018 §3 ladder is FROZEN design: exactly these eight, in this
        // order; M2 binds Sustenance alone (m2 spec scope fence — Shelter
        // binds at M3 housing, the rest later).
        NeedsConfig needs = TestConfigs.Sim().Needs!;
        string[] ladder = ["Sustenance", "Shelter", "Safety", "Health",
                           "Belonging/Faith", "Comfort", "Dignity/Liberty", "Prospects"];
        Assert.Equal(ladder.Length, needs.Needs.Length);
        for (int i = 0; i < ladder.Length; i++)
        {
            Assert.Equal(ladder[i], needs.Needs[i].Name);
            Assert.Equal(i == 0, needs.Needs[i].Bound);
        }
    }

    // --- famine raises grievance --------------------------------------------

    [Fact]
    public void Famine_RaisesGrievance_InTheStarvationWindow()
    {
        // The T2.7 shallow-famine rig (measured there: deficit ≈ 0.2 through
        // turns 9–11, starvation firing in the same window), N = 1 production
        // autoplay. Grievance is EXACTLY zero through the fed prelude (no
        // deficit → no accrual; the stock founds at zero) and strictly rises
        // across the starvation window.
        SimConfig fed = TestConfigs.Sim();
        fed = fed with
        {
            Farming = fed.Farming with { YieldPerFarmlandPerYear = 1000.0, OutputPerFarmerPerYear = 1.45 },
            Founding = fed.Founding with { FoodStore = 4000 },
        };
        SimConfig starving = fed with
        {
            Farming = fed.Farming with { YieldPerFarmlandPerYear = 1000.0, OutputPerFarmerPerYear = 1.05 },
        };
        TurnExecutor fedExec = ProductionExecutor(fed);
        TurnExecutor famineExec = ProductionExecutor(starving);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), fed, Seed, 1);

        for (int t = 1; t <= 6; t++) world = fedExec.Step(world);
        Assert.Equal(0.0, Grievance(world, 0)); // fed prelude: nobody aggrieved

        double preFamine = Grievance(world, 0);
        long starvedBefore = StarvedTotal(world);
        for (int t = 7; t <= 11; t++) world = famineExec.Step(world);
        world = fedExec.Step(world); // the lagged last starvation turn (Prev deficit)
        double postFamine = Grievance(world, 0);
        long starvedAfter = StarvedTotal(world);

        Assert.True(starvedAfter > starvedBefore, "rig vacuous: nobody starved in the window");
        Assert.True(postFamine > preFamine + 1.0,
            $"grievance did not rise across the starvation window: {preFamine:F2} -> {postFamine:F2}");
        Console.WriteLine($"famine grievance: {preFamine:F2} -> {postFamine:F2} across the starvation window");
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
        // A fed N = 1 world carrying a hand-seeded grievance of 10 (grievance
        // is a non-conserved double stock — test-side seeding is legal rigging,
        // no Ledger involved). At canonical TUNEs the decay rate is
        // baseDecay (0.005/yr) + (1 − inherit 0.85) × turnover, with turnover
        // ≈ CBR + CDR ≈ 0.074/yr in the fed regime — decayRate ≈ 0.016/yr,
        // analytic half-life ≈ 43 years ≈ 4 Neolithic turns (explicit Euler at
        // dt = 10 lands near 4 turns). ASSERTED BAND: half-life within
        // [2, 8] turns (20–80 years) — slow (grudges outlive their causes:
        // multiple decades) but not immortal (D-021 §8).
        SimConfig cfg = TestConfigs.Sim();
        cfg = cfg with
        {
            Farming = cfg.Farming with { YieldPerFarmlandPerYear = 100_000.0, OutputPerFarmerPerYear = 500.0 },
        };
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed, 1);
        for (int i = 0; i < world.Grievances.Count; i++)
            world.Grievances[i] = world.Grievances[i] with { Value = 10.0 };

        int halfTurn = -1;
        for (int t = 1; t <= 20; t++)
        {
            world = exec.Step(world);
            Assert.Equal(0.0, MaxDeficit(world)); // the rig IS fed — decay only
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
        SimConfig cfg = TestConfigs.Sim();
        GrievanceTuning g = cfg.Needs!.Grievance;
        const double dt = 10.0;
        WorldState world = GrievanceWorld(2);
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
        // Deficits outside [0,1] (impossible from Consumption, possible from a
        // rigged row — and the clamp is the documented guard): 1.7 must clamp
        // satisfaction to exactly 0.0, −0.4 to exactly 1.0; accrual follows
        // the clamped values (max rate at s = 0, EXACTLY none at s = 1).
        SimConfig cfg = TestConfigs.Sim();
        const double dt = 10.0;
        WorldState world = GrievanceWorld(2);
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), 1.7, 1));
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(1), -0.4, 1));

        WorldState next = GrievanceOnly(cfg, dt).Step(world);

        Assert.Equal(0.0, SustenanceValue(next, 0)); // clamped low
        Assert.Equal(1.0, SustenanceValue(next, 1)); // clamped high
        // Accrual: weight 1.0 × (1 − 0)⁺ × dt at s = 0; exactly zero at s = 1.
        double w = cfg.Needs!.Needs[0].Weight;
        Assert.Equal(Math.Max(0.0, w * 1.0 * dt - cfg.Needs!.Grievance.BaseDecayPerYear * 0.0 * dt),
            Grievance(next, 0));
        Assert.Equal(0.0, Grievance(next, 1));
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
            Farming = cfg.Farming with { YieldPerFarmlandPerYear = 1000.0, OutputPerFarmerPerYear = 1.05 },
            Founding = cfg.Founding with { FoodStore = 2000 },
        };
        var riggedNeeds = (NeedEntry[])cfg.Needs!.Needs.Clone();
        riggedNeeds[1] = riggedNeeds[1] with { Weight = 1e9 }; // Shelter, unbound
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
        // Law-3 pin: constant deficit 0.5 (hand row, no consumption in the
        // pipeline to overwrite it), same 40 sim-years at dt 10 / 5 / 2.5 —
        // successive refinements of the explicit-Euler accrual−decay step
        // should roughly halve the terminal-value deviation.
        SimConfig cfg = TestConfigs.Sim();
        const int horizonYears = 40;
        var finals = new double[3];
        double[] dts = [10.0, 5.0, 2.5];
        for (int i = 0; i < dts.Length; i++)
        {
            WorldState world = GrievanceWorld(1);
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), 0.5, 1));
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
}
