using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 rigs #2, #3, #4) — THE GRANARY EFFECTS (D-053 §3), THE CONSTRUCTION PURSE
/// (D-053 §1) and THE AI FOUNDING POLICY (S1c), attributed turn-exactly on the SHIPPED CYLINDER: the 256-px dev AI world
/// (the D-025 dev preset, seed 42, four settlements, worldgen aiEmpires 1, AiOrders appended before every step — the
/// `sim run --founded --seed 42 --size 256 --settlements 4 --ai-empires 1` recipe), founded and stepped by every arm on
/// worldgen.json as shipped (topology.wrapX). ON = the shipped config (one shared arm, a class fixture); OFF = the
/// single-layer twin of each layer (<see cref="TestConfigs.PreGranaryEffects"/>,
/// <see cref="TestConfigs.PreEmpireConstructionStockpile"/> — both keep reservations and every other layer on — and the
/// NON-chained <see cref="SingleSwitchTwins.PreAiFoundingOnly"/>). Every first action is read off the ON run's state or the
/// order logs, never a hash; the AI's order logs agree before it; the write set at the divergence world is MEASURED and
/// holds no topology table. The flat legs (GranaryAttributionTests, EmpireConstructionStockpileTests' and
/// AiFoundingAttributionTests' per-layer proofs on the 1024 flat AI world) are unchanged.
/// SEAM EXPOSURE (MEASURED, recorded in D-055): the dev world's settlements have 0 seam-crossing pairs and 0 wrapped donor
/// orders at founding, so the purse's donor order and the founding policy's site distances are not exercised across the
/// seam by these rigs; a topology-blind (raw dx) mutant of either passes them (T2 step 2, MEASURED) — their seam halves are
/// CylinderAttributionSeamTests.ConstructionPurse_… (seed 16) and CylinderAttributionSeamTests.AiFounding_… (SeamWorld).
/// THE GRANARY'S TWO HALVES (T2 step 3): the farm half is read off the harvest at the step the factor first acts (and at a
/// wild-food granary, where it must not); the famine half never acts on the shared arm (no settlement of a granary-holding
/// civilization is short there), so its own rig — a player granary completing while its settlement is already in famine —
/// pins that the famine factor reads the civilization's granaries off PREV: the step that builds the granary is unchanged,
/// the next one is not.
/// </summary>
public class CylinderAttributionDevAiTests : IClassFixture<CylinderAttributionDevAiTests.Arms>
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 1) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/probe-measure.txt; 450-turn runs):
    //   ON: the first granary stands in world 8 (at a wild-food settlement: inert, C1) and first ACTS in world 355 (96
    //   worlds in 450); the first EnqueueConstruction is stamped 7, the first FoundSettlement 71; no SetTaxRate in 450 turns.
    //   granary effects off: first different world 356 (= acts + 1); write set {GoodStocks, LedgerFlows}; the logs agree to
    //     order k = 113 (turn 402/403, after the divergence).
    //   purse off: the first EnqueueConstruction is stamped 9 (ON: 7); first different world 8 (= 7 + 1); write set
    //     {ConstructionLabor, GoodStocks, LedgerFlows, Structures}; the logs agree to k = 1.
    //   AI founding off: no FoundSettlement; first different world 72 (= 71 + 1); write set {Buckets, ClassStates,
    //     Controls, Deposits, GoodStocks, Grievance, LedgerFlows, SettlementVitals, Settlements, SmoothedAttractiveness};
    //     the logs agree to k = 4 (ON's FoundSettlement at 71).
    //   seam exposure at world 0: 0 seam-crossing pairs, 0 wrapped donor orders.
    // These equal the batch-3 recon's dev-AI values (f7-flat-only §5 P5f: 8/355/356, 7/9/8, 71/72): unchanged by T1a (the
    // AI's heads finish in their delivery step, so no hold binds on this world).
    // ---------------------------------------------------------------------------------------------------------------
    private const int GranaryStands = 8;
    private const int GranaryActs = 355;
    private const int GranaryHorizon = GranaryActs + 1 + 2;
    private const long FirstEnqueueOn = 7;
    private const long FirstEnqueueOff = 9;
    private const int PurseDiverges = (int)FirstEnqueueOn + 1;
    private const int PurseHorizon = PurseDiverges + 2;
    private const long FirstFounding = 71;
    private const int FoundingDiverges = (int)FirstFounding + 1;
    private const int FoundingHorizon = FoundingDiverges + 2;
    private static readonly string[] GranaryWrites = ["GoodStocks", "LedgerFlows"];

    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 3) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe logs t2-out/s3/probes/probe3-run3.txt P3h
    // and probe3.txt P3l) — the SEMANTIC readings the step-3 mutant campaign showed the granary rig needed:
    //   the farm half: in world 355 the AI's eight settlements have just begun to cultivate (wild food no longer) and each
    //     has a granary of its own, so its farm factor is 1.0316…; in the step 355 → 356 each of the eight harvests MORE
    //     grain than the granary-off twin (e.g. 8,602 against 8,339) and the player's two (wild food, no granary) the same.
    //     The first granary (world 8) stands at a wild-food settlement, whose world-9 harvest is the same in both arms.
    //   the famine half (THE PREV PIN; a new rig): the dev AI cylinder, the player's settlement 0 ordered to zero food at turn
    //     1 and a granary ordered there at turn 3. The granary stands first in world 4, in both arms; settlement 0 is in
    //     Famine on worlds 3 and 4 (effective deficit 0.1699 and 0.1403); the famine factor (0.9016) first acts on world 4,
    //     so the worlds first differ in world 5 — write set {Buckets, LedgerFlows, SettlementVitals}, settlement 0's people
    //     185 against 180, every other settlement equal — and NOT in world 4 (the step that built the granary reads PREV,
    //     which had none); the logs are the same seven orders in both arms.
    // ---------------------------------------------------------------------------------------------------------------
    private const int FarmFactorSettlements = 8;
    private const int FamineGranaryOrdered = 3;
    private const int FamineGranaryStands = 4;
    private const int FamineDiverges = FamineGranaryStands + 1;
    private const int FamineHorizon = FamineDiverges + 2;
    private const long FaminePeopleOn = 185, FaminePeopleOff = 180;
    private static readonly string[] FamineWrites = ["Buckets", "LedgerFlows", "SettlementVitals"];
    private static readonly string[] PurseWrites = ["ConstructionLabor", "GoodStocks", "LedgerFlows", "Structures"];
    private static readonly string[] FoundingWrites =
    [
        "Buckets", "ClassStates", "Controls", "Deposits", "GoodStocks", "Grievance", "LedgerFlows", "SettlementVitals",
        "Settlements", "SmoothedAttractiveness",
    ];

    /// <summary>The shared ON arm (the shipped config, to the granary rig's horizon, with the granary probes) and a second,
    /// independent ON run of the same length (determinism).</summary>
    public sealed class Arms
    {
        // ADR-045 (D-062, 2026-10-22): the readings above were measured on the pre-tranche research content; by the R4 rule
        // these older-layer controls run on it (TestConfigs.PreTreeTranche). ResearchTrancheAttributionTests owns the tranche.
        public SimConfig On { get; } = TestConfigs.PreTreeTrancheSim();
        public R.Arm Live { get; }
        public R.Arm Again { get; }

        public Arms()
        {
            Live = R.Run(R.DevAi(), "on", On, GranaryHorizon,
                keep: [0, (int)FirstEnqueueOn, GranaryStands, GranaryStands + 1, (int)FirstFounding, FoundingDiverges, GranaryActs, GranaryActs + 1],
                probes: [R.GranaryStands, R.GranaryActs]);
            Again = R.Run(R.DevAi(), "on-again", TestConfigs.PreTreeTrancheSim(), GranaryHorizon);
        }
    }

    private readonly Arms _arms;

    public CylinderAttributionDevAiTests(Arms arms) => _arms = arms;

    [Fact]
    public void TheSharedOnArm_IsTheShippedCylinder_Deterministic_AndSeamFree()
    {
        R.Arm live = _arms.Live;
        Assert.True(live.At(0).Terrain!.WrapX);
        Assert.Equal(2, live.At(0).Polities.Count);
        R.AssertSameSeries(live, _arms.Again);
        Assert.Equal((0, 0), (R.SeamCrossingPairs(live.At(0)), R.WrappedDonorOrders(live.At(0))));
    }

    /// <summary>#2 — the granary effects. A granary stands long before it can act (the AI's first granaries stand at
    /// wild-food settlements, where both factors are the literal 1.0); the worlds are identical until the first world on
    /// which a factor differs from 1, and differ from the next step on — the first step that reads it from PREV.</summary>
    [Fact]
    public void GranaryEffects_OnTheDevAiCylinder_DivergeTheStepAfterAGranaryFirstActs()
    {
        SimConfig on = _arms.On, off = TestConfigs.PreGranaryEffects(on);
        Assert.True(on.Granaries!.Enabled);
        Assert.False(off.Granaries!.Enabled);
        R.Arm live = _arms.Live;
        R.Arm twin = R.Run(R.DevAi(), "granary-off", off, GranaryHorizon, keep: [GranaryStands + 1, GranaryActs + 1], probes: [R.GranaryStands]);

        Assert.Equal(GranaryStands, live.FirstHeld(R.GranaryStands.Name));
        Assert.Equal(GranaryActs, live.FirstHeld(R.GranaryActs.Name));                  // vacuity: it acts in the horizon
        Assert.Equal(GranaryStands, twin.FirstHeld(R.GranaryStands.Name));               // the same granary stands OFF

        // What the farm half does, read off the harvest (T2 step 3): a granary at a WILD-FOOD settlement saves nothing
        // (the first one, world 8: its world-9 harvest is the twin's), and in the step that reads world 355 every settlement
        // whose farm factor differs from 1 there harvests more grain ON, every other settlement the same.
        int grain = on.Goods!.GrainId, project = GranaryEffects.ProjectId(on), wildWithGranary = 0;
        WorldState first = live.At(GranaryStands);
        for (int i = 0; i < first.Settlements.Count; i++)
        {
            SettlementId s = first.Settlements[i].Id;
            if (GranaryEffects.LocalGranaries(first, on, s) == 0) continue;
            Assert.True(LabourActivities.HarvestsWildFood(first, on, s), $"the first granary (settlement {s.Value}) is not at a wild-food settlement");
            wildWithGranary++;
            Assert.Equal(Harvested(twin.At(GranaryStands + 1), s, grain), Harvested(live.At(GranaryStands + 1), s, grain));
        }
        Assert.True(wildWithGranary > 0 && project >= 0);
        WorldState acts = live.At(GranaryActs), on356 = live.At(GranaryActs + 1), off356 = twin.At(GranaryActs + 1);
        int raised = 0;
        for (int i = 0; i < acts.Settlements.Count; i++)
        {
            SettlementId s = acts.Settlements[i].Id;
            long a = Harvested(on356, s, grain), b = Harvested(off356, s, grain);
            if (GranaryEffects.FarmYieldFactor(acts, on, s) != 1.0)
            {
                Assert.True(a > b, $"settlement {s.Value}: its granary's farm factor did not raise its harvest ({a} against {b})");
                raised++;
            }
            else Assert.Equal(b, a);
        }
        Assert.Equal(FarmFactorSettlements, raised);

        R.AssertWorld0Identical(live, twin);
        Assert.Equal(GranaryActs + 1, R.FirstDifferent(live, twin));
        R.AssertLogsEqualBefore(live.Log, twin.Log, GranaryActs + 1);                     // the AI agrees through the acts turn
        string[] written = R.WriteSet(live.At(GranaryActs + 1), twin.At(GranaryActs + 1));
        Assert.Equal(GranaryWrites, written);
        R.AssertNoTopologyTable(written);
    }

    /// <summary>The famine rig (T2 step 3): the dev AI cylinder; the player orders settlement 0 to zero food at turn 1 (the
    /// ExplainRigs STARVED batch) and a granary there at turn 3, while it is already short.</summary>
    private static R.RigWorld DevAiFamine() => new("dev-ai-famine", TestConfigs.DevWorldgen() with { AiEmpires = 1 }, TestConfigs.DevWorldgen(), 42,
        Ai: true, Script: _ =>
        {
            OrderLog starve = Sim.Tests.Observability.ExplainRigs.ZeroFoodOrders(0);
            var log = new OrderLog();
            for (int i = 0; i < starve.Count; i++) if (starve[i].Turn <= FamineGranaryOrdered) log.Append(starve[i]);
            log.Append(new OrderRecord(FamineGranaryOrdered, 1, OrderKind.EnqueueConstruction, 0, 1.0));
            for (int i = 0; i < starve.Count; i++) if (starve[i].Turn > FamineGranaryOrdered) log.Append(starve[i]);
            return log;
        });

    /// <summary>#2 (the FAMINE half; T2 step 3) — THE PREV PIN of the granary's famine-intensity factor: a granary that
    /// completes in a step does not blunt that step's shortfall (DemographicsSystem reads the civilization's granaries off
    /// PREV), only the next one's. The granary stands first in world 4 while settlement 0 is already in famine; the worlds are
    /// the same through world 4 and first differ in world 5 — the first step that reads a granary-standing world with a
    /// shortfall — where settlement 0 keeps more of its people ON. A famine factor read off NEXT would move world 4.</summary>
    [Fact]
    public void GranaryFamineFactor_OnTheDevAiCylinder_ActsTheStepAfterTheGranaryCompletes_NeverInTheStepThatBuildsIt()
    {
        SimConfig on = TestConfigs.Sim(), off = TestConfigs.PreGranaryEffects(on);
        int[] keep = [0, FamineGranaryOrdered, FamineGranaryStands, FamineDiverges];
        R.Arm live = R.Run(DevAiFamine(), "on", on, FamineHorizon, keep: keep, probes: [R.GranaryActs]);
        R.Arm twin = R.Run(DevAiFamine(), "granary-off", off, FamineHorizon, keep: keep);
        var s0 = new SettlementId(0);

        // The premise, read off the worlds: no granary on world 3, one at settlement 0 on world 4 in BOTH arms (construction
        // is not the layer), and settlement 0 short on both worlds (a positive effective deficit) — the famine factor in
        // force on world 4 and not on world 3.
        WorldState w3 = live.At(FamineGranaryOrdered), w4 = live.At(FamineGranaryStands);
        Assert.Equal((0L, 1L, 1L), (GranaryEffects.LocalGranaries(w3, on, s0), GranaryEffects.LocalGranaries(w4, on, s0),
            GranaryEffects.LocalGranaries(twin.At(FamineGranaryStands), off, s0)));
        Assert.True(EffectiveDeficit(w3, s0, on) > 0.0 && EffectiveDeficit(w4, s0, on) > 0.0, "settlement 0 must be short on worlds 3 and 4");
        Assert.Equal(1.0, GranaryEffects.FamineIntensityFactor(w3, on, s0));
        Assert.True(GranaryEffects.FamineIntensityFactor(w4, on, s0) < 1.0);
        Assert.Equal(FamineGranaryStands, live.FirstHeld(R.GranaryActs.Name));       // the first action, read off the state

        // What it does: nothing in the step that built the granary (world 4: settlement 0's people are the twin's), and in
        // the next step settlement 0 keeps more of its people ON; no other settlement moves.
        Assert.Equal(People(twin.At(FamineGranaryStands), s0), People(w4, s0));
        WorldState a = live.At(FamineDiverges), b = twin.At(FamineDiverges);
        Assert.Equal((FaminePeopleOn, FaminePeopleOff), (People(a, s0), People(b, s0)));
        for (int i = 1; i < a.Settlements.Count; i++)
            Assert.Equal(People(b, a.Settlements[i].Id), People(a, a.Settlements[i].Id));

        // The attribution template.
        R.AssertWorld0Identical(live, twin);
        Assert.Equal(FamineDiverges, R.FirstDifferent(live, twin));
        string[] written = R.WriteSet(a, b);
        Assert.Equal(FamineWrites, written);
        R.AssertNoTopologyTable(written);
        R.AssertLogsEqualBefore(live.Log, twin.Log, FamineHorizon + 1);
        R.AssertSameSeries(live, R.Run(DevAiFamine(), "on-again", TestConfigs.Sim(), FamineHorizon));
        Assert.Equal((0, 0), (R.SeamCrossingPairs(live.At(0)), R.WrappedDonorOrders(live.At(0))));
    }

    private static OrderRecord FirstOf(OrderLog log, OrderKind kind)
    {
        for (int i = 0; i < log.Count; i++) if (log[i].Kind == kind) return log[i];
        throw new InvalidOperationException($"no {kind} order in the log");
    }

    private static double EffectiveDeficit(IReadOnlyWorldState w, SettlementId s, SimConfig cfg) =>
        FoodState.EffectiveDeficit(FoodState.DeficitRatio(w, s), FoodState.Of(w, s, cfg, out _), cfg);

    private static long People(IReadOnlyWorldState w, SettlementId s)
    {
        long n = 0;
        for (int i = 0; i < w.Buckets.Count; i++) if (w.Buckets[i].Settlement == s) n += w.Buckets[i].Count.Value;
        return n;
    }

    private static long Harvested(IReadOnlyWorldState w, SettlementId s, int grain)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(grain));
        Assert.True(i >= 0, $"settlement {s.Value} has no grain row");
        return w.GoodStocks[i].LastProducedUnits;
    }

    /// <summary>#3 — the construction purse. The AI's first construction order is ordered against its Empire's purse ON and
    /// against one settlement's own stock OFF, so the two arms' first EnqueueConstruction stamps differ; the logs agree before
    /// the earlier one and the worlds differ exactly from its delivery.</summary>
    [Fact]
    public void ConstructionPurse_OnTheDevAiCylinder_DivergesExactlyAtTheFirstDifferentConstructionOrdersDelivery()
    {
        SimConfig on = _arms.On, off = TestConfigs.PreEmpireConstructionStockpile(on);
        Assert.True(on.Construction!.EmpireStockpile);
        Assert.False(off.Construction!.EmpireStockpile);
        Assert.True(off.Construction.Reservations);                  // single layer: holds stay on (keyed per settlement)
        R.Arm live = _arms.Live;
        R.Arm twin = R.Run(R.DevAi(), "purse-off", off, PurseHorizon, keep: [(int)FirstEnqueueOn, PurseDiverges]);

        R.AssertWorld0Identical(live, twin);
        long onStamp = R.FirstStamp(live.Log, OrderKind.EnqueueConstruction);
        long offStamp = R.FirstStamp(twin.Log, OrderKind.EnqueueConstruction);
        Assert.Equal((FirstEnqueueOn, FirstEnqueueOff), (onStamp, offStamp));               // vacuity: the purse moves it

        // What the purse lets happen, read off the structures (T2 step 3): the AI's first construction order, stamped 7 and
        // paid from the Empire's purse, is built in its delivery step (world 8 holds one more of its project at its
        // settlement than world 7), while the purse-off world 8 holds none more (its first order is stamped 9).
        OrderRecord first = FirstOf(live.Log, OrderKind.EnqueueConstruction);
        var site = new SettlementId(first.TargetId);
        int project = (int)first.Amount;
        Assert.Equal(ConstructionQuery.Built(live.At((int)FirstEnqueueOn), site, project) + 1, ConstructionQuery.Built(live.At(PurseDiverges), site, project));
        Assert.Equal(ConstructionQuery.Built(twin.At((int)FirstEnqueueOn), site, project), ConstructionQuery.Built(twin.At(PurseDiverges), site, project));
        Assert.Equal(Math.Min(onStamp, offStamp) + 1, R.FirstDifferent(live, twin));
        R.AssertLogsEqualBefore(live.Log, twin.Log, Math.Min(onStamp, offStamp));
        string[] written = R.WriteSet(live.At(PurseDiverges), twin.At(PurseDiverges));
        Assert.Equal(PurseWrites, written);
        R.AssertNoTopologyTable(written);
    }

    /// <summary>#4 — the AI founding policy. The ON log's first FoundSettlement is stamped T; the OFF log never carries one;
    /// every other order through T is the same order; the worlds are identical to T and differ from T + 1, the world the
    /// colony is founded in (one more settlement ON).</summary>
    [Fact]
    public void AiFounding_OnTheDevAiCylinder_DivergesExactlyAtTheFirstFoundingsDelivery()
    {
        SimConfig on = _arms.On, off = SingleSwitchTwins.PreAiFoundingOnly(on);
        Assert.True(on.Colonization!.AiFounding!.Enabled);
        Assert.False(off.Colonization!.AiFounding!.Enabled);
        R.Arm live = _arms.Live;
        R.Arm twin = R.Run(R.DevAi(), "ai-founding-off", off, FoundingHorizon, keep: [FoundingDiverges]);

        R.AssertWorld0Identical(live, twin);
        long stamp = R.FirstStamp(live.Log, OrderKind.FoundSettlement);
        Assert.Equal(FirstFounding, stamp);                                                   // vacuity: it founds in the horizon
        Assert.Equal(-1L, R.FirstStamp(twin.Log, OrderKind.FoundSettlement));
        // The colony, read off the settlements (T2 step 3: before the hash-derived turn): founded in the order's delivery
        // step — one more settlement ON in world 72 than in world 71, none more without the policy.
        Assert.Equal(live.At((int)FirstFounding).Settlements.Count + 1, live.At(FoundingDiverges).Settlements.Count);
        Assert.Equal(live.At((int)FirstFounding).Settlements.Count, twin.At(FoundingDiverges).Settlements.Count);
        Assert.Equal(FoundingDiverges, R.FirstDifferent(live, twin));
        R.AssertLogsEqualBefore(live.Log, twin.Log, stamp);
        R.AssertLogsEqualAtExcept(live.Log, twin.Log, stamp, OrderKind.FoundSettlement);      // added, never altered
        string[] written = R.WriteSet(live.At(FoundingDiverges), twin.At(FoundingDiverges));
        Assert.Equal(FoundingWrites, written);
        R.AssertNoTopologyTable(written);
    }
}
