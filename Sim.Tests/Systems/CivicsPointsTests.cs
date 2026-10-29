using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-043 (D-062, the Director's 2026-10-22 ruling) — THE TWO LANES AND CIVICS POINTS. Research Points (per turn,
/// ADR-030) fund ONLY the Technology target; Civics Points (per sim-year × dtYears, law 3) fund ONLY the Civics target,
/// are banked in integer milli-CP without cap or loss, and are audited by the identity Balance == Generated − Spent.
///
/// THESE SUPERSEDE, FOR THE SHIPPED (two-lane) MODEL, the one-pool pins of ResearchEngineTests
/// (ResearchPointPool_IsTotalPopulationOfControlledSettlements_OnePoolPerPolity, NoTarget_RpReachesNoNode_AndNothingIsBanked,
/// Completion_AtEffectiveCost_IsImmediate_ClearsTheTarget_AndLosesTheOverflow, Civics_ResearchesWithProgressCompletionAndBranching,
/// SharedPool_OneTargetAcrossBothTrees_TotalInvestedEqualsTotalRp) — dated 2026-10-22. Those tests are KEPT: their rig
/// content carries no tuning.civicsPoints, so they now pin the ONE-POOL MODEL (the switch off, the attribution twin),
/// which stays a supported configuration.
///
/// The rig (ResearchRigs.Standard with CivicsPoints on): one player settlement of 10 000 people — 1 000 RP a turn
/// (10 × P^0.5) and, at the rig CP calibration 1.0 × P^0.5 per sim-year, 100 CP a sim-year = 1 000 000 milli-CP a dt-10
/// turn. Every expected value is exact (law 1's no-epsilon discipline applied to the CP bank).
///
/// ADR-054 (2026-10-27, D-065 R6) RE-RIG: the rig now carries the SHIPPED form, tuning.civicsPoints.perTurn — CP per turn from
/// the RP base (10 × P^0.5), never × dtYears — so 10 000 people accrue 1 000 CP (1 000 000 milli-CP) a turn at EVERY dt. At
/// the dt-10 turns these tests step, that is the number the per-year rig gave, so every expectation and property is kept;
/// the dt theory now pins the per-turn accrual, and its per-sim-year twin (ADR-043 §3, perTurn absent) is pinned beside it.
/// </summary>
public class CivicsPointsTests
{
    private static readonly PolityId P1 = new(Player);

    private static ResearchContent TwoLaneRig(bool enabled = true, bool perTurn = true)
    {
        Spec spec = Standard();
        if (enabled && perTurn) spec.CivicsPerTurn = true;   // ADR-054: the shipped form
        else spec.CivicsPoints = (enabled, 1.0, 0.5);       // ADR-043 §3 (per sim-year), or the switch off
        return spec.Load();
    }

    private static readonly Lazy<ResearchContent> Rig = new(() => TwoLaneRig());

    private static CivicsPointsRow Account(IReadOnlyWorldState w, int polity = Player) =>
        ResearchQuery.CivicsPointsAccount(w, new PolityId(polity));

    private static void AssertIdentity(IReadOnlyWorldState w)
    {
        Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(w.CivicsPoints));
        for (int i = 0; i < w.CivicsPoints.Count; i++)
            Assert.Equal(w.CivicsPoints[i].GeneratedMilli - w.CivicsPoints[i].SpentMilli, w.CivicsPoints[i].BalanceMilli);
    }

    private static bool Lane(IReadOnlyWorldState w, ResearchContent c, ResearchTree lane, out int key)
    {
        bool held = ResearchQuery.TryGetTarget(w, c, P1, lane, out ResearchNodeId node);
        key = node.Value;
        return held;
    }

    // ------------------------------------------------------------------ the two lanes

    [Fact]
    public void TwoLanes_RpFundsOnlyTheTechTarget_CpFundsOnlyTheCivicTarget_InTheSameStep()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 2));      // b — Technology
        orders.Append(Target(0, 1001));   // law — Civics (a civic order no longer displaces the tech target)
        WorldState w = Executor(Rig.Value, orders).Step(WithCompleted(PlayerWorld(), 1));

        Assert.Equal(1000.0, Progress(w, 2));              // one turn of RP, all of it on b
        Assert.Equal(1000.0, Progress(w, 1001));           // one turn of CP, all of it on law
        Assert.True(Lane(w, Rig.Value, ResearchTree.Technology, out int tech) && tech == 2);
        Assert.True(Lane(w, Rig.Value, ResearchTree.Civics, out int civic) && civic == 1001);
        Assert.Equal(2, w.ResearchTargets.Count);
        Assert.Equal(new CivicsPointsRow(P1, 0, 1_000_000, 1_000_000), Account(w));
        AssertIdentity(w);
    }

    [Fact]
    public void ACivicOrder_DoesNotDisplaceTheTechTarget_AndAppliesTurnExact_WhileCpBankedBeforeItIsSpent()
    {
        // Stamped turn 1 → the civics lane is funded from the step 1 → 2 (the SetResearchTarget precedent), never earlier;
        // the CP of step 0 → 1, which reached no civic, was BANKED and is spent with step 1 → 2's.
        var orders = new OrderLog();
        orders.Append(Target(0, 2));
        orders.Append(Target(1, 1001));
        TurnExecutor ex = Executor(Rig.Value, orders);
        WorldState w1 = ex.Step(WithCompleted(PlayerWorld(), 1));
        Assert.Equal(0.0, Progress(w1, 1001));
        Assert.False(Lane(w1, Rig.Value, ResearchTree.Civics, out _));
        Assert.Equal(new CivicsPointsRow(P1, 1_000_000, 1_000_000, 0), Account(w1));

        WorldState w2 = ex.Step(w1);
        Assert.Equal(2000.0, Progress(w2, 2));                   // the tech lane was never interrupted
        Assert.Equal(2000.0, Progress(w2, 1001));                // 1 000 banked + 1 000 accrued, both spent
        Assert.True(Lane(w2, Rig.Value, ResearchTree.Technology, out int tech) && tech == 2);
        Assert.Equal(new CivicsPointsRow(P1, 0, 2_000_000, 2_000_000), Account(w2));
        AssertIdentity(w2);
    }

    [Fact]
    public void ClearOrders_MinusOneClearsOnlyTheTechLane_MinusTwoOnlyTheCivicsLane_TurnExact()
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 2));
        orders.Append(Target(0, 1001));
        orders.Append(Target(1, ResearchQuery.ClearCivicsTargetId));
        orders.Append(Target(2, ResearchQuery.ClearTechnologyTargetId));
        TurnExecutor ex = Executor(Rig.Value, orders);
        WorldState w1 = ex.Step(WithCompleted(PlayerWorld(), 1));
        WorldState w2 = ex.Step(w1);
        // −2 stamped 1: the civics lane is idle in step 1 → 2 (its CP is banked), the tech lane untouched.
        Assert.False(Lane(w2, Rig.Value, ResearchTree.Civics, out _));
        Assert.True(Lane(w2, Rig.Value, ResearchTree.Technology, out int tech) && tech == 2);
        Assert.Equal(1000.0, Progress(w2, 1001));                // progress kept (D-044 R9)
        Assert.Equal(2000.0, Progress(w2, 2));
        Assert.Equal(new CivicsPointsRow(P1, 1_000_000, 2_000_000, 1_000_000), Account(w2));
        // −1 stamped 2: the tech lane is idle in step 2 → 3 (RP lost, D-044 R20-D: no RP bank), CP keeps banking.
        WorldState w3 = ex.Step(w2);
        Assert.False(Lane(w3, Rig.Value, ResearchTree.Technology, out _));
        Assert.Equal(2000.0, Progress(w3, 2));
        Assert.Equal(0, w3.ResearchTargets.Count);
        Assert.Equal(new CivicsPointsRow(P1, 2_000_000, 3_000_000, 1_000_000), Account(w3));
        AssertIdentity(w3);
    }

    [Fact]
    public void UnspentCp_IsBanked_WithoutLossOrCap_WhileUnspentRpIsLost()
    {
        TurnExecutor ex = Executor(Rig.Value);
        WorldState w = WithCompleted(PlayerWorld(), 1);
        for (int t = 1; t <= 7; t++)
        {
            w = ex.Step(w);
            Assert.Equal(new CivicsPointsRow(P1, t * 1_000_000L, t * 1_000_000L, 0), Account(w));
            AssertIdentity(w);
        }
        Assert.Equal(0, w.ResearchProgress.Count);   // no target on either lane: RP reached nothing and was not stored
    }

    [Fact]
    public void CivicCompletion_SpendsExactlyTheRemainder_KeepsTheRestBanked_AndClearsOnlyTheCivicsLane()
    {
        // Three idle turns bank 3 000 CP; law (2 500) is then targeted with a tech target beside it.
        var orders = new OrderLog();
        orders.Append(Target(3, 1001));
        orders.Append(Target(3, 2));
        TurnExecutor ex = Executor(Rig.Value, orders);
        WorldState w = Run(ex, WithCompleted(PlayerWorld(), 1), 4);
        Assert.True(Done(w, 1001));
        Assert.Equal(0.0, Progress(w, 1001));                                        // completion is the fact; row removed
        Assert.False(Lane(w, Rig.Value, ResearchTree.Civics, out _));                // the civics lane cleared on completion
        Assert.True(Lane(w, Rig.Value, ResearchTree.Technology, out int tech) && tech == 2);
        Assert.Equal(new CivicsPointsRow(P1, 1_500_000, 4_000_000, 2_500_000), Account(w));   // 4 000 − 2 500 banked
        AssertIdentity(w);
    }

    [Fact]
    public void ACivicNeverReceivesRp_AndATechnologyNeverReceivesCp()
    {
        // Only a civic target: the RP of the turn reaches nothing. Only a tech target: the CP is banked, not spent.
        var civicOnly = new OrderLog();
        civicOnly.Append(Target(0, 1001));
        WorldState a = Executor(Rig.Value, civicOnly).Step(WithCompleted(PlayerWorld(), 1));
        Assert.Equal(1000.0, Progress(a, 1001));     // CP only (1 000 CP), not CP + RP
        Assert.Equal(1, a.ResearchProgress.Count);

        var techOnly = new OrderLog();
        techOnly.Append(Target(0, 2));
        WorldState b = Executor(Rig.Value, techOnly).Step(WithCompleted(PlayerWorld(), 1));
        Assert.Equal(1000.0, Progress(b, 2));        // RP only
        Assert.Equal(new CivicsPointsRow(P1, 1_000_000, 1_000_000, 0), Account(b));
    }

    // ------------------------------------------------------------------ dimension (law 3) and exactness

    [Theory]
    [InlineData(10.0)]
    [InlineData(5.0)]
    [InlineData(1.0)]
    [InlineData(0.5)]
    public void CpAccrual_IsPerTurn_IdenticalAtEveryDt_LikeRp(double dtYears)
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 2));
        WorldState w = Executor(Rig.Value, orders, dtYears).Step(WithCompleted(PlayerWorld(), 1));
        Assert.Equal(1_000_000L, Account(w).GeneratedMilli);        // ADR-054: CP per turn, the RP base
        Assert.Equal(1000.0, Progress(w, 2));                       // ADR-030: RP per turn, identical at every dt
    }

    /// <summary>The ADR-043 §3 twin (perTurn absent): CP per sim-year, integrated over dtYears.</summary>
    [Theory]
    [InlineData(10.0, 1_000_000L)]
    [InlineData(5.0, 500_000L)]
    [InlineData(1.0, 100_000L)]
    [InlineData(0.5, 50_000L)]
    public void CpAccrual_TheTwin_IsPerSimYear_IntegratedOverDtYears_UnlikeRp(double dtYears, long expectedMilli)
    {
        var orders = new OrderLog();
        orders.Append(Target(0, 2));
        WorldState w = Executor(TwoLaneRig(perTurn: false), orders, dtYears).Step(WithCompleted(PlayerWorld(), 1));
        Assert.Equal(expectedMilli, Account(w).GeneratedMilli);   // law 3: 100 CP a sim-year × dt
        Assert.Equal(1000.0, Progress(w, 2));
    }

    [Fact]
    public void CpAccrual_IsTheFloorOfTheFormula_InIntegerMilliCp()
    {
        // 12 345 people: 1000 × 1.0 × √12345 × 10 = 1 111 080.555… → floor 1 111 080 milli-CP (never rounded up).
        WorldState w = World([Player], [Player], 12_345);
        Assert.Equal(1_111_080L, ResearchQuery.CivicsPointsMilli(w, Rig.Value, P1, 10.0));
        WorldState next = Executor(Rig.Value).Step(w);
        Assert.Equal(1_111_080L, Account(next).GeneratedMilli);
        Assert.Equal(0L, ResearchQuery.CivicsPointsMilli(w, Rig.Value, new PolityId(9), 10.0));   // no population, no row
        Assert.Equal(0L, ResearchQuery.CivicsPointsMilli(w, Rig.Value, P1, 0.0));
    }

    [Fact]
    public void CpAccounts_AreOnePerHolder_InAscendingPolityOrder_WhateverTheRosterOrder()
    {
        // Roster order 3, 1, 2: the CP table is written ascending (1, 2, 3) — the invariant Snapshot.Load enforces.
        WorldState w = World([3, 1, 2], [3, 1, 2], 10_000);
        WorldState next = Run(Executor(Rig.Value), w, 2);
        Assert.Equal(3, next.CivicsPoints.Count);
        for (int i = 0; i < 3; i++) Assert.Equal(i + 1, next.CivicsPoints[i].Polity.Value);
        AssertIdentity(next);
    }

    // ------------------------------------------------------------------ the switch (attribution twin)

    [Fact]
    public void SwitchOff_IsTheOnePoolModel_ByteForByte_NoCpRow_AndACivicDisplacesTheTechTarget()
    {
        ResearchContent off = TwoLaneRig(enabled: false);
        ResearchContent absent = Standard().Load();
        Assert.False(off.Tuning.TwoLanes);
        var orders = new OrderLog();
        orders.Append(Target(0, 2));
        orders.Append(Target(1, 1001));
        orders.Append(Target(3, ResearchQuery.ClearCivicsTargetId));   // one-pool: −2 clears the one target, being a civic
        WorldState a = WithCompleted(PlayerWorld(), 1), b = a.Clone();
        TurnExecutor exOff = Executor(off, orders), exAbsent = Executor(absent, orders);
        for (int t = 0; t < 5; t++)
        {
            a = exOff.Step(a);
            b = exAbsent.Step(b);
            Assert.Equal(WorldHash.ComputeHex(b), WorldHash.ComputeHex(a));
            Assert.Equal(0, a.CivicsPoints.Count);
            Assert.True(a.ResearchTargets.Count <= 1);
        }
        Assert.Equal(1000.0, Progress(a, 2));     // b got one turn, then law displaced it (one pool, one target)
        Assert.Equal(2000.0, Progress(a, 1001));  // law got RP for steps 1→2 and 2→3, then −2 cleared it
        Assert.Equal(0, a.ResearchTargets.Count);
    }

    // ------------------------------------------------------------------ the AI's two lanes (composite keys, tie-dense)

    [Fact]
    public void AiLanes_EachTheArgminOfItsOwnTree_TieDense_LowerKeyWins_AndALiveLaneIsKept()
    {
        // Every available node costs 2 500 (bit-equal): technology b(2), d(4), w(11); civics law(1001), x(1003), y(1004).
        Spec spec = Standard();
        spec.CivicsPerTurn = true;   // ADR-054: the shipped per-turn form (the AI choice does not read the accrual)
        spec.Civics.AddRange([new Node(1003, "x", "a"), new Node(1004, "y", "a")]);
        ResearchContent content = spec.Load();
        SimConfig cfg = TestConfigs.Sim() with
        {
            Research = content, Ages = null, Roads = null, Governance = null, Institutions = null,
        };
        var ai = new PolityId(2);
        WorldState w = World([2], [2], 10_000);
        w.ResearchCompleted.Add(new ResearchCompletedRow(ai, new ResearchNodeId(1)));
        bool[] completed = ResearchQuery.CompletedMask(w, content, ai);
        bool[] available = ResearchQuery.AvailableMask(content, completed);
        double tie = ResearchQuery.EffectiveCost(w, content, ai, content.IndexOf(new ResearchNodeId(2)));
        foreach (int k in (int[])[4, 11, 1001, 1003, 1004])
            Assert.Equal(BitConverter.DoubleToInt64Bits(tie), BitConverter.DoubleToInt64Bits(ResearchQuery.EffectiveCost(w, content, ai, content.IndexOf(new ResearchNodeId(k)))));

        Assert.Equal(content.IndexOf(new ResearchNodeId(2)), AiResearchPolicy.Choose(w, cfg, ai, completed, available, ResearchTree.Technology));
        Assert.Equal(content.IndexOf(new ResearchNodeId(1001)), AiResearchPolicy.Choose(w, cfg, ai, completed, available, ResearchTree.Civics));
        OrderRecord[] both = AiResearchPolicy.DecideAll(w, cfg, ai);
        Assert.Equal(new[] { 2, 1001 }, new[] { both[0].TargetId, both[1].TargetId });

        // A held, live lane is kept: with law targeted only the tech lane orders, and the reverse.
        w.ResearchTargets.Add(new ResearchTargetRow(ai, new ResearchNodeId(1001)));
        OrderRecord[] techOnly = AiResearchPolicy.DecideAll(w, cfg, ai);
        Assert.Single(techOnly);
        Assert.Equal(2, techOnly[0].TargetId);
        w.ResearchTargets.Add(new ResearchTargetRow(ai, new ResearchNodeId(4)));
        Assert.Empty(AiResearchPolicy.DecideAll(w, cfg, ai));

        // Tie-dense continuation: completing the lower keys walks each lane up its keys in order.
        w.ResearchTargets.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(ai, new ResearchNodeId(2)));
        w.ResearchCompleted.Add(new ResearchCompletedRow(ai, new ResearchNodeId(1001)));
        OrderRecord[] next = AiResearchPolicy.DecideAll(w, cfg, ai);
        Assert.Equal(2, next.Length);
        Assert.Equal(1003, next[1].TargetId);
        Assert.True(content.Nodes[content.IndexOf(new ResearchNodeId(next[0].TargetId))].Tree == ResearchTree.Technology);
    }

    [Fact]
    public void AiLanes_WithTheSwitchOff_ReturnTheOnePoolOrderAlone()
    {
        Spec spec = Standard();
        ResearchContent content = spec.Load();
        SimConfig cfg = TestConfigs.Sim() with { Research = content, Ages = null, Roads = null, Governance = null, Institutions = null };
        var ai = new PolityId(2);
        WorldState w = World([2], [2], 10_000);
        w.ResearchCompleted.Add(new ResearchCompletedRow(ai, new ResearchNodeId(1)));
        OrderRecord[] one = AiResearchPolicy.DecideAll(w, cfg, ai);
        Assert.Single(one);
        Assert.Equal(AiResearchPolicy.Decide(w, cfg, ai)!.Value, one[0]);
    }

    // ------------------------------------------------------------------ the shipped content, four seeds

    [Theory]
    [Trait("suite", "determinism")]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(42UL)]
    [InlineData(7UL)]
    public void ShippedContent_AccountingIdentityHoldsEveryStep_AndEveryAccrualIsTheFormula(ulong seed)
    {
        SimConfig cfg = TestConfigs.Sim();
        ResearchContent content = cfg.Research!;
        Assert.True(content.Tuning.TwoLanes);
        WorldgenConfig wg = TestConfigs.DevWorldgen() with { AiEmpires = 1 };
        var orders = new OrderLog();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var ex = new TurnExecutor(EraTableLoader.Load(eraStream), PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, wg)), orders);
        WorldState w = WorldFounding.Found(wg, cfg, seed);
        // Every Empire knows the closure of a script, so a civic (law_code) is open from turn 1 and the AI's civics lane
        // spends from the start (on the shipped pace the first civic opens hundreds of turns in) — knowledge rows only.
        var closure = new bool[content.Nodes.Count];
        var stack = new Stack<int>();
        stack.Push(content.IndexOfId("cuneiform"));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (closure[i]) continue;
            closure[i] = true;
            foreach (int q in content.Nodes[i].PrerequisiteNodes) stack.Push(q);
        }
        for (int p = 0; p < w.Polities.Count; p++)
            for (int i = 0; i < closure.Length; i++)
                if (closure[i]) w.ResearchCompleted.Add(new ResearchCompletedRow(w.Polities[p].Id, content.Nodes[i].Key));
        long spentTotal = 0;
        for (int t = 0; t < 40; t++)
        {
            AiOrders.Append(orders, w, cfg, wg);
            WorldState next = ex.Step(w);
            AssertIdentity(next);
            double dt = next.Clock.DtYears;
            for (int i = 0; i < next.CivicsPoints.Count; i++)
            {
                CivicsPointsRow now = next.CivicsPoints[i];
                CivicsPointsRow before = ResearchQuery.CivicsPointsAccount(w, now.Polity);
                Assert.Equal(ResearchQuery.CivicsPointsMilli(w, content, now.Polity, dt), now.GeneratedMilli - before.GeneratedMilli);
                Assert.True(now.SpentMilli >= before.SpentMilli);
            }
            w = next;
        }
        Assert.True(w.CivicsPoints.Count > 0);
        for (int i = 0; i < w.CivicsPoints.Count; i++) spentTotal += w.CivicsPoints[i].SpentMilli;
        Assert.True(spentTotal > 0, "the AI's civics lane spent Civics Points within 40 turns");
    }
}
