using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Disaster;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.21-1 (CR-015 §3.1/§3.2, test plan §6.1) — the derived four-state food
/// classification and the dead-zone effective deficit. FAMINE is exceptional:
/// it requires a positive deficit AND an explicit cause (an APPLIED famine-class
/// disaster, or deliberate abandonment of food labour); every other shortfall is
/// STRESS (absorbed) or SEVERE (adaptation exhausted). Every case the mandate
/// names is a test here, and the three mutants the spec lists are killed by the
/// tests that name them.
/// </summary>
public class FoodStateTests
{
    private static readonly SettlementId S0 = new(0);

    private static SimConfig Cfg() => TestConfigs.Sim();

    /// <summary>A hand world holding exactly the rows FoodState reads.</summary>
    private static WorldState Rig(
        double deficit, SectorAllocationRow? sectors = null, DisasterRow? disaster = null,
        double? weather = null, bool deficitRow = true)
    {
        var w = new WorldState(7);
        w.Settlements.Add(new SettlementRow(S0, SiteCell: 0, FoundedTurn: 0));
        if (deficitRow) w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(S0, deficit, 10_000));
        if (sectors is SectorAllocationRow row) w.SectorAllocations.Add(row);
        if (disaster is DisasterRow d) w.Disasters.Add(d);
        if (weather is double m) w.HarvestWeather.Add(new HarvestWeatherRow(S0, Math.Log(m), m));
        return w;
    }

    private static (FoodStateKind Kind, FamineReason Reason, double DEff) Classify(WorldState w, SimConfig cfg)
    {
        FoodStateKind kind = FoodState.Of(w, S0, cfg, out FamineReason reason);
        double dEff = FoodState.EffectiveDeficit(FoodState.DeficitRatio(w, S0), kind, cfg);
        return (kind, reason, dEff);
    }

    // ======================================================================
    // §6.1 — CLASSIFICATION
    // ======================================================================

    [Fact]
    public void F_Normal_ZeroDeficit_IsNormal()
    {
        SimConfig cfg = Cfg();
        // d = 0 is NORMAL whatever the sectors or disaster rows say: an
        // abandonment the store still covers and a disaster a granary absorbs
        // are not famines — "decided by the food balance".
        var abandoned = new SectorAllocationRow(S0, 0.0, 0.0, 0.1, 0.1, 0.1);
        var struck = new DisasterRow(S0, 1, 0.75, 0.0, 1.0, 0.625);
        foreach (WorldState w in new[]
        {
            Rig(0.0),
            Rig(0.0, sectors: abandoned),
            Rig(0.0, disaster: struck),
            Rig(0.0, sectors: abandoned, disaster: struck),
            Rig(0.0, weather: 0.55),
            Rig(0.0, deficitRow: false),   // absent row reads 0, as every system reads it
        })
        {
            (FoodStateKind kind, FamineReason reason, double dEff) = Classify(w, cfg);
            Assert.Equal(FoodStateKind.Normal, kind);
            Assert.Equal(FamineReason.None, reason);
            Assert.Equal(0.0, dEff);
        }
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.15)]
    [InlineData(0.20)]
    public void F_Stress_OrdinaryWeather_NeverFamine(double d)
    {
        // A moderate bad harvest: a 0.55 weather draw on prev, no disaster row,
        // the Default sector mix. This is exactly the shortfall the old chronicle
        // latched as "famine" at d >= 0.15, and exactly what the mandate says is
        // NOT famine. STRESS, reason None, and adaptation absorbs it whole.
        // KILLS M-FS-WEATHER (Famine whenever d > 0, or struck := weather < 0.7).
        SimConfig cfg = Cfg();
        WorldState w = Rig(d, weather: 0.55);
        (FoodStateKind kind, FamineReason reason, double dEff) = Classify(w, cfg);
        Assert.Equal(FoodStateKind.Stress, kind);
        Assert.Equal(FamineReason.None, reason);
        Assert.Equal(0.0, dEff);
    }

    [Fact]
    public void F_Severe_WhereAdaptationEnds()
    {
        SimConfig cfg = Cfg();
        double a = cfg.FoodState.AdaptationAbsorbableShortfall;
        Assert.Equal(0.20, a);
        Assert.Equal(a, FoodState.SevereThreshold(cfg));

        // d = a is the last STRESS value: the cut is exactly absorbed.
        (FoodStateKind atA, _, double dEffAtA) = Classify(Rig(a), cfg);
        Assert.Equal(FoodStateKind.Stress, atA);
        Assert.Equal(0.0, dEffAtA);

        // d = a + ε is SEVERE and the unabsorbed remainder starves.
        double aPlus = Math.BitIncrement(a);
        (FoodStateKind aboveA, _, double dEffAbove) = Classify(Rig(aPlus), cfg);
        Assert.Equal(FoodStateKind.Severe, aboveA);
        Assert.True(dEffAbove > 0.0);

        // ONE boundary: the threshold IS the config value and moves with it.
        SimConfig moved = cfg with { FoodState = cfg.FoodState with { AdaptationAbsorbableShortfall = 0.30 } };
        Assert.Equal(0.30, FoodState.SevereThreshold(moved));
        Assert.Equal(FoodStateKind.Stress, FoodState.Of(Rig(0.25), S0, moved, out _));
        Assert.Equal(FoodStateKind.Severe, FoodState.Of(Rig(0.31), S0, moved, out _));
        Assert.Equal(0.0, FoodState.EffectiveDeficit(0.25, FoodStateKind.Stress, moved));

        // The kernel's 1/3 birth full-stop is a KERNEL FACT inside SEVERE, not a
        // state boundary: at d = 1/3 (SEVERE, since 1/3 > a) suppression is 0.
        double third = 1.0 / 3.0;
        Assert.Equal(FoodStateKind.Severe, FoodState.Of(Rig(third), S0, cfg, out _));
        double suppression = Math.Max(0.0, 1.0 - cfg.Demographics.FamineFertilitySuppressionSlope * third);
        Assert.Equal(0.0, suppression);
    }

    [Fact]
    public void F_Famine_DisasterApplied()
    {
        // prev holds the row that says a famine-class multiplier 0.625 was
        // APPLIED to the harvest that produced this deficit. d = 0.15 — a value
        // that is STRESS under weather — is FAMINE by disaster, unadapted.
        // KILLS M-FS-DISASTER (struck ignored → Stress).
        SimConfig cfg = Cfg();
        var applied = new DisasterRow(S0, 0, 0.0, 0.0, 1.0, 0.625);
        (FoodStateKind kind, FamineReason reason, double dEff) = Classify(Rig(0.15, disaster: applied), cfg);
        Assert.Equal(FoodStateKind.Famine, kind);
        Assert.Equal(FamineReason.Disaster, reason);
        Assert.Equal(0.15, dEff);

        // An ABSORBED disaster (d = 0) is not a famine.
        (FoodStateKind absorbed, FamineReason none, _) = Classify(Rig(0.0, disaster: applied), cfg);
        Assert.Equal(FoodStateKind.Normal, absorbed);
        Assert.Equal(FamineReason.None, none);

        // The row must say APPLIED, not merely "a strike is pending": a row whose
        // Multiplier < 1 but AppliedMultiplier == 1 is next turn's shock, not the
        // cause of this deficit.
        var pending = new DisasterRow(S0, 1, 0.75, 0.0, 0.625, 1.0);
        Assert.Equal(FoodStateKind.Stress, FoodState.Of(Rig(0.15, disaster: pending), S0, cfg, out FamineReason r2));
        Assert.Equal(FamineReason.None, r2);
        Assert.False(FoodState.IsStruck(Rig(0.15, disaster: pending), S0));
        Assert.True(FoodState.IsStruck(Rig(0.15, disaster: applied), S0));
    }

    [Fact]
    public void F_NotAbandoned_HerdingAlive()
    {
        // farming = 0, herding > 0: NOT abandonment (director's rule) — a pure
        // pastoralist at d = 0.5 is SEVERE, never famine. What the food model
        // then does to it is CR-015 G6 (S_Pastoralist_Pipeline, T4.21-4).
        SimConfig cfg = Cfg();
        var pastoral = new SectorAllocationRow(S0, 0.0, 0.15, 0.10, 0.12, 0.08);
        Assert.False(FoodState.IsAbandoned(Rig(0.5, sectors: pastoral), S0));
        (FoodStateKind kind, FamineReason reason, _) = Classify(Rig(0.5, sectors: pastoral), cfg);
        Assert.Equal(FoodStateKind.Severe, kind);
        Assert.Equal(FamineReason.None, reason);
    }

    [Fact]
    public void F_NotAbandoned_FarmingAlive()
    {
        SimConfig cfg = Cfg();
        var farmerOnly = new SectorAllocationRow(S0, 0.55, 0.0, 0.10, 0.12, 0.08);
        Assert.False(FoodState.IsAbandoned(Rig(0.5, sectors: farmerOnly), S0));
        Assert.Equal(FoodStateKind.Severe, FoodState.Of(Rig(0.5, sectors: farmerOnly), S0, cfg, out FamineReason reason));
        Assert.Equal(FamineReason.None, reason);
    }

    [Fact]
    public void F_Famine_Abandonment()
    {
        // KILLS M-FS-ABANDON (predicate reads Share, or Farming only).
        SimConfig cfg = Cfg();

        // Both food sectors at 0 with other labour alive: abandonment.
        var abandoned = new SectorAllocationRow(S0, 0.0, 0.0, 0.1, 0.1, 0.1);
        Assert.True(FoodState.IsAbandoned(Rig(0.2, sectors: abandoned), S0));
        (FoodStateKind kind, FamineReason reason, double dEff) = Classify(Rig(0.2, sectors: abandoned), cfg);
        Assert.Equal(FoodStateKind.Famine, kind);
        Assert.Equal(FamineReason.Abandonment, reason);
        Assert.Equal(0.2, dEff);   // no adaptation possible

        // The ALL-ZERO row (reachable only from a hand-written log): nothing is
        // farmed, so it is abandoned. Share would divide 0/0 here; Raw does not.
        var allZero = new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 0.0, 0.0);
        Assert.True(FoodState.IsAbandoned(Rig(0.2, sectors: allZero), S0));
        Assert.Equal(FoodStateKind.Famine, FoodState.Of(Rig(0.2, sectors: allZero), S0, cfg, out _));

        // The legacy LaborAllocation pct = 0 row (Farming 0 / Herding 0 /
        // Construction 1.0, PathBuildSystem's mapping): abandonment — the
        // FirstReign fixture and CollapseStabilityTests are FAMINE rigs without
        // re-rigging.
        var legacyPctZero = new SectorAllocationRow(S0, 0.0, 0.0, 0.0, 0.0, 1.0);
        Assert.True(FoodState.IsAbandoned(Rig(0.2, sectors: legacyPctZero), S0));
        Assert.Equal(FoodStateKind.Famine, FoodState.Of(Rig(0.2, sectors: legacyPctZero), S0, cfg, out _));

        // Absent row → Sectors.Default → never abandoned.
        Assert.False(FoodState.IsAbandoned(Rig(0.2), S0));
        Assert.Equal(FoodStateKind.Stress, FoodState.Of(Rig(0.2), S0, cfg, out _));

        // Sub-unit but non-zero food labour is NOT abandonment: the predicate
        // is exact zero on the raw weight, never a threshold.
        var trace = new SectorAllocationRow(S0, 1e-9, 0.0, 0.1, 0.1, 0.1);
        Assert.False(FoodState.IsAbandoned(Rig(0.2, sectors: trace), S0));
    }

    [Fact]
    public void F_Famine_Both()
    {
        SimConfig cfg = Cfg();
        var abandoned = new SectorAllocationRow(S0, 0.0, 0.0, 0.1, 0.1, 0.1);
        var applied = new DisasterRow(S0, 0, 0.0, 0.0, 1.0, 0.5);
        (FoodStateKind kind, FamineReason reason, _) = Classify(Rig(0.4, sectors: abandoned, disaster: applied), cfg);
        Assert.Equal(FoodStateKind.Famine, kind);
        Assert.Equal(FamineReason.Both, reason);
    }

    // ======================================================================
    // §3.2 — THE EFFECTIVE DEFICIT
    // ======================================================================

    [Theory]
    [InlineData(0.10, 0.0)]
    [InlineData(0.20, 0.0)]
    [InlineData(0.25, 0.0625)]
    [InlineData(0.30, 0.125)]
    [InlineData(0.40, 0.25)]
    [InlineData(0.50, 0.375)]
    [InlineData(1.00, 1.0)]
    public void EffectiveDeficit_DeadZone_WorkedValues(double d, double expected)
    {
        // The spec's worked values at a = 0.2 (§3.2). Exactly 1 at d = 1.
        SimConfig cfg = Cfg();
        FoodStateKind state = d > cfg.FoodState.AdaptationAbsorbableShortfall
            ? FoodStateKind.Severe : d > 0.0 ? FoodStateKind.Stress : FoodStateKind.Normal;
        Assert.Equal(expected, FoodState.EffectiveDeficit(d, state, cfg), 15);
    }

    [Theory]
    [InlineData(0.10)]
    [InlineData(0.15)]
    [InlineData(0.35)]
    [InlineData(1.0)]
    public void EffectiveDeficit_Famine_IsTheNominalDeficit(double d)
    {
        // No adaptation possible: the whole deficit starves.
        Assert.Equal(d, FoodState.EffectiveDeficit(d, FoodStateKind.Famine, Cfg()));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1234567)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void EffectiveDeficit_NullArm_IsBitExactlyLinear(double d)
    {
        // a = 0 reproduces today's response BIT FOR BIT: (d − 0)/1 = d.
        SimConfig cfg = Cfg();
        SimConfig nullArm = cfg with { FoodState = cfg.FoodState with { AdaptationAbsorbableShortfall = 0.0 } };
        foreach (FoodStateKind k in new[] { FoodStateKind.Stress, FoodStateKind.Severe, FoodStateKind.Famine })
            Assert.Equal(BitConverter.DoubleToInt64Bits(d),
                BitConverter.DoubleToInt64Bits(FoodState.EffectiveDeficit(d, k, nullArm)));
    }

    // ======================================================================
    // FULL-PIPELINE TIMING PINS (the food-balance pipeline: disaster →
    // production → consumption → pathbuild on a hand rig, weather off; N = 1)
    // ======================================================================

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>
    /// One land-bound settlement of 1000 adults (10,000 person-years of demand per
    /// dt-10 turn), Default sector mix, arable sized so the land side yields
    /// ρ × demand per year, a grain endowment of <paramref name="storeYears"/> years
    /// of demand, no deposits (the non-staple basket lines substitute to grain).
    /// </summary>
    private static WorldState BalanceRig(SimConfig cfg, double rho, double storeYears)
    {
        var counts = new long[Cohorts.Count];
        counts[5] = 1000;
        WorldState w = PopulationExactnessTests.BucketWorld(counts);
        // Default mix: farming 0.55 × 1000 adults × 5.0/yr = 2750/yr labour side;
        // the land side is rho × 1000 per year and binds.
        double arable = rho * 1000.0 / cfg.Farming.YieldPerArableKm2PerYear;
        w.CatchmentSummaries.Add(new CatchmentSummaryRow(
            S0, NodeCount: 1, EffectiveArableKm2: arable, NetworkRevision: 0, LastRecomputeTurn: 0));
        var ledger = new Ledger(w.LedgerFlows);
        var grain = new GoodId(cfg.Goods!.GrainId);
        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            int row = w.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            if (g.Id == grain.Value)
            {
                long endowment = (long)Math.Round(storeYears * 1000.0);
                if (endowment > 0)
                    ledger.Flow(ref w.GoodStocks.Ref(row).Amount, ConservedQuantityIds.OfGood(grain),
                        ReasonIds.InitialEndowment, endowment, FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }
        return w;
    }

    private static TurnExecutor BalanceExecutor(SimConfig cfg, OrderLog? orders = null) =>
        new(FlatEra(10.0),
            [SystemCatalog.Disaster(cfg), SystemCatalog.Production(cfg),
             SystemCatalog.Consumption(cfg), SystemCatalog.PathBuild(cfg)], orders);

    private static SimConfig Forced(SimConfig cfg) =>
        cfg with { Disaster = cfg.Disaster with { HazardPerYear = 1e6 } };   // P = 1 − e^{−1e7} = 1

    [Fact]
    public void F_Stockpile_DecidesFamine()
    {
        // "Decided by the food balance": the same forced strike on the same land
        // is FAMINE against a thin store and NOTHING against a granary that
        // covers it. Strike drawn at turn 1 → applied at step 2 → d(2) → the
        // state after turn 2 is what step 3 classifies.
        SimConfig cfg = Forced(Cfg());

        // Thin store, ρ = 1.3: the decade multiplier 1 − s·5/10 ∈ [0.5, 0.625]
        // leaves 0.65–0.81 of demand; 0.1 y of store cannot close that.
        WorldState thin = BalanceRig(cfg, rho: 1.3, storeYears: 0.1);
        TurnExecutor exec = BalanceExecutor(cfg);
        thin = exec.Step(thin);
        Assert.Equal(FoodStateKind.Normal, FoodState.Of(thin, S0, cfg, out _));   // d(1) = 0: no strike applied yet
        Assert.True(thin.Disasters.Count == 1 && thin.Disasters[0].Multiplier < 1.0, "no strike drawn — rig vacuous");
        thin = exec.Step(thin);
        Assert.True(FoodState.DeficitRatio(thin, S0) > 0.0, "thin store absorbed the strike — rig vacuous");
        Assert.Equal(FoodStateKind.Famine, FoodState.Of(thin, S0, cfg, out FamineReason reason));
        Assert.Equal(FamineReason.Disaster, reason);

        // Full granary, ρ = 2: the struck harvest alone is ≥ demand
        // (2 × [0.5, 0.625] ≥ 1) and 1.5 y of store stands behind it: d = 0.
        WorldState fat = BalanceRig(cfg, rho: 2.0, storeYears: 1.5);
        TurnExecutor exec2 = BalanceExecutor(cfg);
        fat = exec2.Step(fat);
        fat = exec2.Step(fat);
        Assert.True(fat.Disasters.Count == 1 && fat.Disasters[0].AppliedMultiplier < 1.0, "no strike applied — rig vacuous");
        Assert.Equal(0.0, FoodState.DeficitRatio(fat, S0));
        Assert.Equal(FoodStateKind.Normal, FoodState.Of(fat, S0, cfg, out FamineReason none));
        Assert.Equal(FamineReason.None, none);
    }

    [Fact]
    public void F_AbandonmentTiming_TurnExact()
    {
        // Order at turn t → row in state t+1 (PathBuild is last) → zero harvest
        // at step t+1→t+2 → d(t+2) > 0 once the store is short → FAMINE first at
        // step t+2→t+3, i.e. on the state after turn t+2. Restore at u → row u+1
        // restored → the state after u+1 reads a non-abandoned row with d > 0
        // (SEVERE/STRESS, adapted, one turn) → first restored harvest at step
        // u+1→u+2 → NORMAL on the state after u+2.
        SimConfig cfg = Cfg();
        const int t = 2, u = 6;
        var log = new OrderLog();
        log.Append(new OrderRecord(t, ActorId: 1, OrderKind.SectorAllocation, TargetId: 0 * 8 + Sectors.Farming, 0.0));
        log.Append(new OrderRecord(t, ActorId: 1, OrderKind.SectorAllocation, TargetId: 0 * 8 + Sectors.Herding, 0.0));
        log.Append(new OrderRecord(u, ActorId: 1, OrderKind.SectorAllocation, TargetId: 0 * 8 + Sectors.Farming, 55.0));
        log.Append(new OrderRecord(u, ActorId: 1, OrderKind.SectorAllocation, TargetId: 0 * 8 + Sectors.Herding, 15.0));

        WorldState w = BalanceRig(cfg, rho: 1.3, storeYears: 1.5);
        TurnExecutor exec = BalanceExecutor(cfg, log);
        var kinds = new List<FoodStateKind>();
        var reasons = new List<FamineReason>();
        var deficits = new List<double>();
        for (int turn = 1; turn <= u + 3; turn++)
        {
            w = exec.Step(w);
            kinds.Add(FoodState.Of(w, S0, cfg, out FamineReason r));
            reasons.Add(r);
            deficits.Add(FoodState.DeficitRatio(w, S0));
        }
        FoodStateKind After(int turn) => kinds[turn - 1];

        // Before the order lands: fed.
        Assert.Equal(FoodStateKind.Normal, After(t));
        // Row in force from state t+1, store still feeding everyone: NOT famine.
        Assert.Equal(FoodStateKind.Normal, After(t + 1));
        Assert.Equal(0.0, deficits[t + 1 - 1]);
        // First hungry state: FAMINE by abandonment, the same turn d appears.
        Assert.True(deficits[t + 2 - 1] > 0.0, "the store covered a whole abandoned decade — rig vacuous");
        Assert.Equal(FoodStateKind.Famine, After(t + 2));
        Assert.Equal(FamineReason.Abandonment, reasons[t + 2 - 1]);
        for (int turn = t + 2; turn <= u; turn++) Assert.Equal(FoodStateKind.Famine, After(turn));
        // Restore at u: state u+1 carries the restored row and the last hungry d.
        Assert.True(deficits[u + 1 - 1] > 0.0);
        Assert.True(After(u + 1) is FoodStateKind.Severe or FoodStateKind.Stress,
            $"state after turn {u + 1} is {After(u + 1)} — the restored row must leave FAMINE before the harvest returns");
        Assert.Equal(FamineReason.None, reasons[u + 1 - 1]);
        // First restored harvest: NORMAL.
        Assert.Equal(0.0, deficits[u + 2 - 1]);
        Assert.Equal(FoodStateKind.Normal, After(u + 2));
        Assert.Equal(FoodStateKind.Normal, After(u + 3));
    }

    [Fact]
    public void F_DisasterTiming_TurnExact()
    {
        // λ forced to 1 − ε for ONE turn (config twin), then 0: the strike drawn
        // at turn 1 is applied at step 2, d(2) > 0, and the state after turn 2
        // classifies FAMINE — the same turn the deficit first appears. An
        // implementation that read the row's MULTIPLIER (the next shock) instead
        // of the APPLIED multiplier would see 1.0 there and say SEVERE.
        SimConfig cfg = Cfg();
        SimConfig forced = Forced(cfg);
        WorldState w = BalanceRig(cfg, rho: 1.3, storeYears: 0.1);

        w = BalanceExecutor(forced).Step(w);              // turn 1: the draw
        Assert.Equal(1, w.Disasters.Count);
        Assert.True(w.Disasters[0].Multiplier < 1.0 && w.Disasters[0].AppliedMultiplier == 1.0);
        Assert.Equal(FoodStateKind.Normal, FoodState.Of(w, S0, cfg, out _));   // d(1) = 0

        TurnExecutor quiet = BalanceExecutor(cfg);         // λ = 0 from here
        w = quiet.Step(w);                                  // turn 2: applied
        Assert.True(FoodState.DeficitRatio(w, S0) > 0.0, "strike absorbed — rig vacuous");
        Assert.Equal(1, w.Disasters.Count);
        Assert.Equal(1.0, w.Disasters[0].Multiplier);
        Assert.True(w.Disasters[0].AppliedMultiplier < 1.0);
        Assert.Equal(FoodStateKind.Famine, FoodState.Of(w, S0, cfg, out FamineReason reason));
        Assert.Equal(FamineReason.Disaster, reason);

        w = quiet.Step(w);                                  // turn 3: the harvest is back
        Assert.Equal(0, w.Disasters.Count);
        Assert.Equal(FoodStateKind.Normal, FoodState.Of(w, S0, cfg, out _));
    }
}
