using Proto.M7.Events;
using Proto.M7.Time;
using Proto.M7.War;
using Xunit.Abstractions;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests;

/// <summary>
/// Phase C lane T3 (DIR5 §2.8; C23): at most two actions per decision window, integrated with the ratified, agent-specific
/// Action Capacity (ledger §17.2/§18/§19; D-043 B2/B8). Combination matrix and anti-exploit rules are PROPOSALS (ActionRules).
/// Worlds are T2 WarTheaterWorlds with an action layer; windows come from Probe / PulseStart / TurnStart events.
/// </summary>
public class ActionWindowsT3Tests
{
    private readonly ITestOutputHelper _out;
    public ActionWindowsT3Tests(ITestOutputHelper output) => _out = output;

    private const long Money = 1_000_000;
    private static readonly long PulseMs = WarPulseT2Tests.ProposedPulseDays[0] * Day;   // band 0 (Neolithic): 21 days
    private const int AllKinds = 0b1111;

    // Profiles: 0 Army (range 1), 1 Scout (Move/Retreat only), 2 Archer (range 3).
    private static readonly AgentProfile[] Profiles =
    {
        new(0, AllKinds, Capacity: 6, MoveCostPerCell: 1, AttackCost: 2, RetreatCost: 1, RecruitCost: 1, AttackRange: 1, RetreatMaxCells: 3),
        new(1, ActionKinds.Bit(ActionKind.Move) | ActionKinds.Bit(ActionKind.Retreat), 10, 1, 2, 1, 1, 1, 5),
        new(2, ActionKinds.Bit(ActionKind.Move) | ActionKinds.Bit(ActionKind.Attack) | ActionKinds.Bit(ActionKind.Retreat), 6, 1, 2, 1, 1, 3, 3),
    };

    /// <summary>Default agents. War 0 = polities {0, 1}. a3 (archer) is in contact with a0; a8 with a1.</summary>
    private static WarAgentSpec[] DefaultAgents() => new WarAgentSpec[]
    {
        new(0, 0, 0, 0, 10),
        new(1, 0, 0, 0, 20),
        new(2, 0, 0, 0, 30),
        new(3, 1, 2, 0, 11),
        new(4, 0, 0, -1, 100),
        new(5, 0, 1, -1, 200),
        new(6, 2, 0, -1, 300),
        new(7, 3, 0, -1, 400),
        new(8, 1, 0, 0, 21),
    };

    private sealed record Rig(CampaignCalendar Cal, EventScheduler S, WarTheaterWorld W, Runner R, WarSession Session)
    {
        public ActionWindowBook B => W.Actions!;
    }

    private static Rig Build(long warStart, (long at, int polity)[] probes, SignificantKinds? sig = null, WarAgentSpec[]? agents = null,
                             WarFog? fog = null, int polities = 4, long startMs = -1, bool stops = true, SubTurnOrderLog? replayLog = null,
                             WarSpec[]? wars = null)
    {
        var cal = ShippedCalendar();
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = WarPulseT2Tests.ProposedPulseDays[i] * Day;
        var sched = new WarPulseSchedule(cal, table);
        var s = new EventScheduler(startMs >= 0 ? startMs : Math.Max(0, warStart - 30 * Day));
        var sk = sig ?? new SignificantKinds(WarKinds.Probe);
        wars ??= new[] { new WarSpec(0, warStart, warStart + 40 * PulseMs, new[] { 0, 1 }, 0, 0, new long[] { 0, 0 }) };
        var layer = new ActionLayerSpec(Profiles, agents ?? DefaultAgents(), 0xC0FFEEUL);
        var w = new WarTheaterWorld(cal, sched, polities, wars, fog ?? WarFog.None, sk, new long[polities], Money, Money, layer);
        w.Begin(s);
        foreach (var (at, p) in probes) WarTheaterWorld.ScheduleProbe(s, at, p);
        var r = replayLog is null ? new Runner(s, w, sk, stops) : Runner.CreateReplay(s, w, sk, replayLog);
        return new Rig(cal, s, w, r, new WarSession(r, w));
    }

    /// <summary>Run to the next window, which must be at `at`.</summary>
    private static void To(Rig rig, long at)
    {
        var r = rig.R.RunUntil(at);
        Assert.Equal((RunStatus.Interrupted, at), (r.Status, r.AtMs));
    }

    private static int Act(Rig rig, int polity, int kind, int agent, long arg) => rig.R.IssueOrder(kind, WarKinds.Pack(polity, agent), arg).Seq;

    /// <summary>0 = accepted, else the recorded refusal reason. The order must have executed.</summary>
    private static int Outcome(Rig rig, int log)
    {
        foreach (var r in rig.W.Refusals) if (r.LogIndex == log) return r.Reason;
        foreach (var a in rig.B.Actions) if (a.LogIndex == log) return 0;
        throw new Xunit.Sdk.XunitException($"order {log} did not execute");
    }

    private static void Finish(Rig rig, long limit)
    {
        while (rig.R.RunUntil(limit).Status == RunStatus.Interrupted) { }
    }

    private static long S(int turn) => ShippedCalendar().TurnStart(turn).Ms;

    // ---------------------------------------------------------------- 1. the count: two per window; refusal is inert

    /// <summary>The action layer without log indices (so an extra refused order does not shift the comparison).</summary>
    private static string View(Rig rig)
    {
        var b = rig.B;
        var parts = new List<string>();
        for (int i = 0; i < b.AgentCount; i++) parts.Add($"a{i}:{b.PositionOf(i)}/{b.RemainingOf(i)}/{b.WindowSerialOf(i)}/{b.LeviesOf(i)}");
        for (int p = 0; p < 4; p++) parts.Add($"p{p}:{b.SlotsUsed(p, -1)}/{b.SlotsUsed(p, 0)}");
        foreach (var a in b.Actions) parts.Add($"{a.Polity}.{a.AgentId}.{a.Kind}@{a.AtMs}w{a.WarId}.{a.WindowIndex}:{a.Arg}c{a.Cost}r{a.Roll}");
        foreach (var r in b.Refreshes) parts.Add($"R{r.AgentId}{r.Cause}@{r.AtMs}:{r.WindowIndex}");
        foreach (var d in b.Disengagements) parts.Add($"D{d.AgentId}-{d.EnemyId}@{d.Serial}{d.Lifted}");
        return string.Join("|", parts);
    }

    [Fact]
    public void ThirdAction_InOneWindow_IsRefused_AndARefusalChangesNothing()
    {
        long w0 = S(1) - 10 * Day, t = w0 + 3 * Day;
        Rig Run(bool third)
        {
            var rig = Build(w0, new[] { (t, 0), (w0 + PulseMs + 1, 0) });
            To(rig, t);
            int bad = Act(rig, 0, WarKinds.ActAttack, 0, 8);           // a8 at 21 is out of reach: refused, consumes no slot
            int m1 = Act(rig, 0, WarKinds.ActMove, 1, 22);
            int m2 = Act(rig, 0, WarKinds.ActMove, 2, 31);
            int m3 = third ? Act(rig, 0, WarKinds.ActMove, 0, 9) : -1; // otherwise legal: a0 has capacity and no action yet
            To(rig, w0 + PulseMs + 1);
            Assert.Equal(WarRefusal.OutOfReach, Outcome(rig, bad));
            Assert.Equal((0, 0), (Outcome(rig, m1), Outcome(rig, m2)));
            if (third) Assert.Equal(WarRefusal.SlotsExhausted, Outcome(rig, m3));
            Assert.Equal(2, rig.B.Actions.Count(a => a.WindowIndex == 0));
            Assert.Equal((10L, 6L), (rig.B.PositionOf(0), rig.B.RemainingOf(0)));
            Act(rig, 0, WarKinds.ActAttack, 0, 3);                      // next pulse: an identity-keyed engagement roll
            Finish(rig, w0 + 3 * PulseMs);
            return rig;
        }
        var with = Run(true);
        var without = Run(false);
        // The refused third action left no trace in the action layer (slots, capacity, positions, rolls, refreshes) or ledger.
        Assert.Equal(View(without), View(with));
        Assert.Equal(without.B.Actions[^1].Roll, with.B.Actions[^1].Roll);
        Assert.True(with.B.Actions[^1].Roll >= 0);
        Assert.Equal(without.W.Ledger.Snapshot(), with.W.Ledger.Snapshot());
        Assert.Equal(without.W.Refusals.Count + 1, with.W.Refusals.Count);
    }

    // ---------------------------------------------------------------- 2. combination matrix (PROPOSAL), exhaustive

    private static readonly int[] KindOrder = { WarKinds.ActMove, WarKinds.ActAttack, WarKinds.ActRetreat, WarKinds.ActRecruit };

    /// <summary>The argument of `kind` for an agent at x in contact with an enemy at ex (each individually legal).</summary>
    private static long ArgFor(int kind, long x, long ex, int enemyAgent) => kind switch
    {
        WarKinds.ActMove => ex + 1,                                    // the far side of the enemy: contact kept
        WarKinds.ActAttack => enemyAgent,
        WarKinds.ActRetreat => x <= ex ? x - 2 : x + 2,                // away from the enemy
        _ => 1000,
    };

    private static (int first, int second, Rig rig) Pair(int k1, int k2, bool sameAgent)
    {
        long w0 = S(1) - 10 * Day, t = w0 + 3 * Day;
        var rig = Build(w0, new[] { (t, 0) });
        To(rig, t);
        int f = Act(rig, 0, k1, 0, ArgFor(k1, 10, 11, 3));
        int agent2 = sameAgent ? 0 : 1;
        long x2 = sameAgent ? (k1 == WarKinds.ActMove ? 12 : k1 == WarKinds.ActRetreat ? 8 : 10) : 20;
        long ex2 = sameAgent ? 11 : 21;
        int s = Act(rig, 0, k2, agent2, ArgFor(k2, x2, ex2, sameAgent ? 3 : 8));
        Finish(rig, t + Day);
        return (Outcome(rig, f), Outcome(rig, s), rig);
    }

    [Fact]
    public void CombinationMatrix_SameAgent_EveryOrderedPair()
    {
        // Expected outcome of the SECOND action (row = first kind, column = second): 0 accepted, else the refusal.
        const int K = WarRefusal.KindRepeated, C = WarRefusal.CombinationForbidden;
        int[,] expected =
        {
            //   Move  Attack Retreat Recruit
            {    K,    0,     C,      0 },    // Move
            {    C,    K,     C,      C },    // Attack
            {    C,    C,     K,      C },    // Retreat
            {    0,    C,     0,      K },    // Recruit
        };
        for (int i = 0; i < 4; i++)
        for (int j = 0; j < 4; j++)
        {
            var (first, second, rig) = Pair(KindOrder[i], KindOrder[j], sameAgent: true);
            _out.WriteLine($"{(ActionKind)i}->{(ActionKind)j}: {second}");
            Assert.Equal(0, first);
            Assert.True(expected[i, j] == second, $"{(ActionKind)i}->{(ActionKind)j}: expected {expected[i, j]}, got {second}");
            int accepted = expected[i, j] == 0 ? 2 : 1;
            Assert.Equal(accepted, rig.B.Actions.Count);
            Assert.Equal(accepted, rig.B.SlotsUsed(0, 0));
            if (accepted == 2)
            {
                // Both applied: the effects of both kinds are visible.
                var kinds = rig.B.Actions.Select(a => a.Kind).ToArray();
                Assert.Equal(new[] { (ActionKind)i, (ActionKind)j }, kinds);
                bool moved = kinds.Contains(ActionKind.Move), retreated = kinds.Contains(ActionKind.Retreat);
                if (kinds.Contains(ActionKind.Recruit)) Assert.Equal(1000, rig.B.LeviesOf(0));
                if (moved) Assert.Equal(12, rig.B.PositionOf(0));
                if (retreated) Assert.Equal(8, rig.B.PositionOf(0));
                Assert.Equal(6 - rig.B.Actions.Sum(a => a.Cost), rig.B.RemainingOf(0));
            }
        }
    }

    [Fact]
    public void CombinationMatrix_DifferentAgents_EveryOrderedPairApplies()
    {
        for (int i = 0; i < 4; i++)
        for (int j = 0; j < 4; j++)
        {
            var (first, second, rig) = Pair(KindOrder[i], KindOrder[j], sameAgent: false);
            Assert.True(first == 0 && second == 0, $"{(ActionKind)i}+{(ActionKind)j}: {first}/{second}");
            Assert.Equal(2, rig.B.SlotsUsed(0, 0));
            Assert.Equal(new[] { 0, 1 }, rig.B.Actions.Select(a => a.AgentId).ToArray());
        }
    }

    [Fact]
    public void AgentSpecificActionSet_Ownership_AndRecruitConservation()
    {
        long w0 = S(1) - 10 * Day, t = w0 + 3 * Day;
        var rig = Build(w0, new[] { (t, 0) });
        To(rig, t);
        int scoutAttack = Act(rig, 0, WarKinds.ActAttack, 5, 6);    // Scout has no Attack
        int foreign = Act(rig, 0, WarKinds.ActMove, 3, 12);         // a3 belongs to polity 1
        int broke = Act(rig, 0, WarKinds.ActRecruit, 4, Money + 1); // more than the treasury holds
        int ok = Act(rig, 0, WarKinds.ActRecruit, 4, 250_000);
        Finish(rig, t + Day);
        Assert.Equal(new[] { WarRefusal.NotInActionSet, WarRefusal.NotOwnAgent, WarRefusal.BadValue, 0 },
                     new[] { Outcome(rig, scoutAttack), Outcome(rig, foreign), Outcome(rig, broke), Outcome(rig, ok) });
        Assert.Equal(Money - 250_000, rig.W.Ledger.Balance(rig.W.TreasuryOf(0)));
        Assert.Equal(250_000, rig.W.Ledger.Balance(rig.W.RecruitSink));
        Assert.Equal((Int128)Money * 4, rig.W.Ledger.Total(Commodity.Money));   // exact conservation
        Assert.Equal(1, rig.B.SlotsUsed(0, -1));                                 // only the accepted one used a slot
    }

    // ---------------------------------------------------------------- 3. Action Capacity integration and refresh pins

    [Fact]
    public void Refresh_WarAgentsAtPulseStartNotTurnStart_CampaignAgentsAtTurnStart_ResetNotAdd()
    {
        long s1 = S(1), w0 = s1 - 10 * Day, x1 = w0 + PulseMs;   // pulse 0 [w0, x1) straddles S1
        var rig = Build(w0, new[] { (s1 - 5 * Day, 0), (s1 + Day, 0), (x1 + 1, 0) });
        To(rig, s1 - 5 * Day);
        int spend = Act(rig, 0, WarKinds.ActMove, 0, 5);          // a0 (war): cost 5 of 6
        int camp = Act(rig, 0, WarKinds.ActMove, 4, 104);         // a4 (campaign): cost 4 of 6
        To(rig, s1 + Day);
        Assert.Equal((0, 0), (Outcome(rig, spend), Outcome(rig, camp)));
        Assert.Equal(1, rig.B.RemainingOf(0));                    // NOT refreshed at the turn boundary S1 (ADR-061 §3.11)
        Assert.Equal(6, rig.B.RemainingOf(4));                    // refreshed (reset, not 2 + 6) at S1 (D-043 B2)
        To(rig, x1 + 1);
        Assert.Equal(6, rig.B.RemainingOf(0));                    // refreshed (reset) at pulse start x1 (ledger §19)

        var a0 = rig.B.Refreshes.Where(r => r.AgentId == 0).ToArray();
        Assert.Equal(new[] { RefreshCause.Begin, RefreshCause.PulseStart, RefreshCause.PulseStart }, a0.Select(r => r.Cause).ToArray());
        Assert.Equal(new[] { w0, x1 }, a0.Skip(1).Select(r => r.AtMs).ToArray());
        Assert.Equal(new[] { 0L, 1L }, a0.Skip(1).Select(r => r.WindowIndex).ToArray());
        var a4 = rig.B.Refreshes.Where(r => r.AgentId == 4).ToArray();
        Assert.Equal(new[] { RefreshCause.Begin, RefreshCause.TurnStart }, a4.Select(r => r.Cause).ToArray());
        Assert.Equal((s1, 1L), (a4[1].AtMs, a4[1].WindowIndex));
    }

    [Fact]
    public void CapacityExhaustion_RefusesAnOtherwiseLegalActionWithAFreeSlot()
    {
        long s1 = S(1), w0 = s1 - 10 * Day;
        var agents = DefaultAgents().Select(a => a.Id == 3 ? a with { X = 6 } : a).ToArray();
        var rig = Build(w0, new[] { (s1 - 5 * Day, 0), (s1 + Day, 0) }, agents: agents);
        To(rig, s1 - 5 * Day);
        Act(rig, 0, WarKinds.ActMove, 0, 5);                       // 10 -> 5 (now in contact with a3 at 6); a0 has 1 AC left
        To(rig, s1 + Day);                                         // same pulse, after the turn boundary
        Assert.Equal((1, 1L), (rig.B.SlotsUsed(0, 0), rig.B.RemainingOf(0)));   // a slot is free
        int attack = Act(rig, 0, WarKinds.ActAttack, 0, 3);       // in the set, legal target, Move->Attack allowed; cost 2 > 1
        int recruit = Act(rig, 0, WarKinds.ActRecruit, 0, 10);    // cost 1: fits, takes the free slot
        Finish(rig, s1 + 2 * Day);
        Assert.Equal((WarRefusal.CapacityExhausted, 0), (Outcome(rig, attack), Outcome(rig, recruit)));
        Assert.Equal((2, 0L), (rig.B.SlotsUsed(0, 0), rig.B.RemainingOf(0)));
    }

    // ---------------------------------------------------------------- 4. order-delivery pins (turn-exact, ±1 ms)

    [Theory]
    [InlineData(-1, WarRefusal.SlotsExhausted)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    public void OperationalWindow_ActionAtPulseStartNeighbours_UsesTheWindowContainingT(long delta, int expected)
    {
        long w0 = S(1) - 10 * Day, x1 = w0 + PulseMs, t = x1 + delta;
        var rig = Build(w0, new[] { (x1 - 3 * Day, 0), (t, 0) });
        To(rig, x1 - 3 * Day);
        Act(rig, 0, WarKinds.ActMove, 0, 9);
        Act(rig, 0, WarKinds.ActMove, 1, 22);
        To(rig, t);
        int o = Act(rig, 0, WarKinds.ActMove, 2, 31);
        Finish(rig, x1 + Day);
        Assert.Equal(expected, Outcome(rig, o));
        Assert.Equal(2, rig.B.Actions.Count(a => a.WarId == 0 && a.WindowIndex == 0));
        if (expected == 0)
        {
            var rec = rig.B.Actions.Single(a => a.LogIndex == o);
            Assert.Equal((0, 1L, t), (rec.WarId, rec.WindowIndex, rec.AtMs));   // pulse 1's fresh window
            Assert.Equal(1, rig.B.SlotsUsed(0, 0));
        }
        else Assert.Equal((0, 30L), (rig.B.SlotsUsed(0, 0), rig.B.PositionOf(2)));   // pulse 1 untouched; a2 did not move
    }

    [Theory]
    [InlineData(-1, WarRefusal.SlotsExhausted)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    public void CampaignWindow_ActionAtTurnStartNeighbours_UsesTheWindowContainingT(long delta, int expected)
    {
        long s2 = S(2), t = s2 + delta;
        var rig = Build(S(1) - 10 * Day, new[] { (s2 - 3 * Day, 0), (t, 0) });
        To(rig, s2 - 3 * Day);
        Act(rig, 0, WarKinds.ActMove, 4, 101);
        Act(rig, 0, WarKinds.ActMove, 5, 201);
        To(rig, t);
        int o = Act(rig, 0, WarKinds.ActMove, 4, 99);              // a4 moves again (cost 2)
        Finish(rig, s2 + Day);
        Assert.Equal(expected, Outcome(rig, o));
        Assert.Equal(2, rig.B.Actions.Count(a => a.WarId == -1 && a.WindowIndex == 1));
        if (expected == 0)
        {
            var rec = rig.B.Actions.Single(a => a.LogIndex == o);
            Assert.Equal((-1, 2L, t), (rec.WarId, rec.WindowIndex, rec.AtMs));  // turn 2's fresh window
            Assert.Equal((1, 6L - 2), (rig.B.SlotsUsed(0, -1), rig.B.RemainingOf(4)));
        }
        else Assert.Equal((0, 6L, 101L), (rig.B.SlotsUsed(0, -1), rig.B.RemainingOf(4), rig.B.PositionOf(4)));
    }

    // ---------------------------------------------------------------- 5. anti-exploit rules (PROPOSAL)

    [Fact]
    public void NoWindowFarming_KInterruptsInOnePulse_StillAllowTwoActions_AndRefreshNothing()
    {
        long s1 = S(1), w0 = s1 - 10 * Day;
        foreach (int k in new[] { 1, 3, 6 })
        {
            // k probe windows plus the TurnStart window at S1, all inside pulse 0 [w0, w0 + 21 d).
            var probes = Enumerable.Range(0, k).Select(i => (w0 + Day + i * 3 * Day + 7, 0)).ToArray();
            var rig = Build(w0, probes, sig: new SignificantKinds(WarKinds.TurnStart, WarKinds.Probe));
            var war = new List<int>();
            int camp = -1, n = 0;
            while (rig.R.RunUntil(w0 + PulseMs - 1).Status == RunStatus.Interrupted)
            {
                for (int j = 0; j < 2; j++)
                {
                    int agent = (n + j) % 3;
                    war.Add(Act(rig, 0, WarKinds.ActMove, agent, 10 * agent + 11 + n));
                }
                if (rig.R.StoppedAtMs == s1) camp = Act(rig, 0, WarKinds.ActMove, 4, 101);
                n++;
            }
            Assert.Equal(k + 1, n);
            var outcomes = war.Select(l => Outcome(rig, l)).ToArray();
            _out.WriteLine($"k={k}: {string.Join(",", outcomes)}");
            Assert.Equal(new[] { 0, 0 }, outcomes.Take(2).ToArray());
            Assert.All(outcomes.Skip(2), o => Assert.Equal(WarRefusal.SlotsExhausted, o));
            Assert.Equal(0, rig.B.Refreshes.Count(r => r.AgentId < 3 && r.AtMs > w0));   // no refresh at any interrupt
            Assert.Equal(0, Outcome(rig, camp));                     // S1 opened the campaign turn window (a real refresh)
        }
    }

    [Fact]
    public void NoCarryOver_UnusedSlotsAndCapacity_DoNotAccumulate()
    {
        long w0 = S(1) - 10 * Day, t0 = w0 + Day, t1 = w0 + PulseMs + Day;
        var rig = Build(w0, new[] { (t0, 0), (t1, 0) });
        To(rig, t0);                                               // pulse 0: the window is shown, nothing is done
        To(rig, t1);                                               // pulse 1
        Assert.Equal(6, rig.B.RemainingOf(0));                    // reset to capacity, not 6 + 6
        int big = Act(rig, 0, WarKinds.ActMove, 0, 3);             // cost 7: possible only with carried capacity
        int a = Act(rig, 0, WarKinds.ActMove, 1, 22);
        int b = Act(rig, 0, WarKinds.ActMove, 2, 31);
        int c = Act(rig, 0, WarKinds.ActMove, 0, 9);               // 3rd accepted-shape action: possible only with carried slots
        Finish(rig, t1 + Day);
        Assert.Equal(new[] { WarRefusal.CapacityExhausted, 0, 0, WarRefusal.SlotsExhausted },
                     new[] { Outcome(rig, big), Outcome(rig, a), Outcome(rig, b), Outcome(rig, c) });
    }

    [Fact]
    public void NoSplitting_OneMoveCannotBecomeTwoInAWindow_AndASplitAcrossWindowsIsNeverCheaper()
    {
        long w0 = S(1) - 10 * Day, t0 = w0 + Day, t1 = w0 + PulseMs + Day;
        var rig = Build(w0, new[] { (t0, 0), (t1, 0) });
        To(rig, t0);
        int h1 = Act(rig, 0, WarKinds.ActMove, 1, 23);
        int h2 = Act(rig, 0, WarKinds.ActMove, 1, 26);             // the second half, same window: refused
        int r1 = Act(rig, 0, WarKinds.ActRecruit, 2, 10);
        To(rig, t1);
        int r2 = Act(rig, 0, WarKinds.ActRecruit, 2, 10);          // a split recruit: a new window, a new slot
        int h3 = Act(rig, 0, WarKinds.ActMove, 1, 26);             // the second half in the next window
        Finish(rig, t1 + Day);
        Assert.Equal(new[] { 0, WarRefusal.KindRepeated, 0, 0, 0 },
                     new[] { Outcome(rig, h1), Outcome(rig, h2), Outcome(rig, r1), Outcome(rig, r2), Outcome(rig, h3) });
        long splitCost = rig.B.Actions.Where(a => a.AgentId == 1).Sum(a => a.Cost);
        long wholeCost = Math.Abs(26 - 20) * Profiles[0].MoveCostPerCell;
        Assert.Equal(26, rig.B.PositionOf(1));
        Assert.True(splitCost >= wholeCost);                      // never cheaper (here equal: linear route cost)
        Assert.Equal(2, rig.B.Actions.Count(a => a.AgentId == 1)); // and it took two windows' slots, not one
        Assert.Equal(2, rig.B.Actions.Count(a => a.AgentId == 2));
        Assert.Equal(20, rig.B.LeviesOf(2));
    }

    [Fact]
    public void HitAndRun_RetreatThenReattack_IsBarredForOneWindow_UnlessTheEnemyAttackedOrClosed()
    {
        long w0 = S(1) - 10 * Day;
        long t0 = w0 + Day, t1 = w0 + PulseMs + Day, t2 = w0 + 2 * PulseMs + Day;
        int[] Run(int lift)
        {
            var rig = Build(w0, new[] { (t0, 0), (t1, 0), (t1, 1), (t2, 0) });
            To(rig, t0);
            int ret = Act(rig, 0, WarKinds.ActRetreat, 0, 9);       // a0 10 -> 9, away from archer a3 at 11
            To(rig, t1);
            int liftLog = -1, close = -1;
            if (lift == 1) liftLog = Act(rig, 1, WarKinds.ActAttack, 3, 0);  // a3 (range 3) attacks a0 at distance 2
            if (lift == 2) liftLog = Act(rig, 1, WarKinds.ActMove, 3, 10);   // a3 closes on a0 (11 -> 10)
            if (lift != 2) close = Act(rig, 0, WarKinds.ActMove, 0, 10);      // a0 back into contact (9 -> 10)
            int atk1 = Act(rig, 0, WarKinds.ActAttack, 0, 3);
            To(rig, t2);
            int atk2 = Act(rig, 0, WarKinds.ActAttack, 0, 3);
            Finish(rig, t2 + Day);
            if (close >= 0) Assert.Equal(0, Outcome(rig, close));
            Assert.Single(rig.B.Disengagements);
            Assert.Equal(lift != 0, rig.B.Disengagements[0].Lifted);
            return new[] { Outcome(rig, ret), liftLog < 0 ? 0 : Outcome(rig, liftLog), Outcome(rig, atk1), Outcome(rig, atk2) };
        }
        Assert.Equal(new[] { 0, 0, WarRefusal.HitAndRun, 0 }, Run(0));   // barred in the next window, free in the one after
        Assert.Equal(new[] { 0, 0, 0, 0 }, Run(1));                      // the enemy attacked: no bar
        Assert.Equal(new[] { 0, 0, 0, 0 }, Run(2));                      // the enemy closed: no bar
    }

    // ---------------------------------------------------------------- 6. fog: only polities that see the trigger may act

    [Fact]
    public void Fog_AWindowGrantsActionsOnlyToPolitiesThatSeeItsTrigger_AndAttemptsByOthersConsumeNothing()
    {
        long w0 = S(1) - 100 * Day;
        long seeFrom = w0 + 2 * PulseMs, seeUntil = w0 + 4 * PulseMs;            // polity 2 observes from pulse 2 to pulse 3
        var fog = new WarFog(new[] { new WarObserver(2, 10, 0, 50, seeFrom, seeUntil) });
        long later = w0 + 2 * PulseMs + 5 * Day;                                 // polity 3's own window, same campaign turn
        var rig = Build(w0, new[] { (later, 3) }, sig: new SignificantKinds(WarKinds.PulseStart, WarKinds.Probe), fog: fog, startMs: w0 - Day);

        To(rig, w0);
        To(rig, w0 + PulseMs);                                      // pulse 1: polity 2 does not see it
        Assert.Equal(0b0011UL, rig.W.LatestWindowRecipients);
        int p2Blind = Act(rig, 2, WarKinds.ActMove, 6, 301);
        To(rig, seeFrom);                                           // pulse 2: polity 2 sees it
        Assert.Equal(0b0111UL, rig.W.LatestWindowRecipients);
        int p3 = Act(rig, 3, WarKinds.ActMove, 7, 401);             // polity 3 never sees war 0
        int p2a = Act(rig, 2, WarKinds.ActMove, 6, 302);
        int p2b = Act(rig, 2, WarKinds.ActRecruit, 6, 5);
        int p2c = Act(rig, 2, WarKinds.ActRetreat, 6, 299);         // third: refused on the count (checked before reach)
        int p0 = Act(rig, 0, WarKinds.ActMove, 0, 9);               // a participant acts at the same window
        To(rig, later);
        Assert.Equal(WarRefusal.NotARecipient, Outcome(rig, p2Blind));
        Assert.Equal(new[] { WarRefusal.NotARecipient, 0, 0, WarRefusal.SlotsExhausted, 0 },
                     new[] { Outcome(rig, p3), Outcome(rig, p2a), Outcome(rig, p2b), Outcome(rig, p2c), Outcome(rig, p0) });
        Assert.Equal((0, 400L, 6L), (rig.B.SlotsUsed(3, -1), rig.B.PositionOf(7), rig.B.RemainingOf(7)));   // untouched
        Assert.Equal(0b1000UL, rig.W.LatestWindowRecipients);
        int q1 = Act(rig, 3, WarKinds.ActMove, 7, 402);             // the earlier refused attempt consumed nothing
        int q2 = Act(rig, 3, WarKinds.ActRecruit, 7, 1);
        int q3 = Act(rig, 2, WarKinds.ActMove, 6, 303);             // polity 2 does not receive polity 3's probe
        Finish(rig, later + Day);
        Assert.Equal(new[] { 0, 0, WarRefusal.NotARecipient }, new[] { Outcome(rig, q1), Outcome(rig, q2), Outcome(rig, q3) });
        Assert.Equal(0, rig.B.Refreshes.Count(r => r.AgentId == 7 && r.Cause == RefreshCause.TurnStart));   // same window
    }

    // ---------------------------------------------------------------- 7. shown / skipped / replayed: identical budget

    private static Rig Scenario(bool stops, SubTurnOrderLog? replay, bool orders)
    {
        long w0 = S(1) - 10 * Day;
        var probes = new[] { (w0 + Day, 0), (w0 + 2 * Day, 1), (S(1), 0), (w0 + PulseMs + 3, 0), (w0 + 2 * PulseMs + 5, 1) };
        var rig = Build(w0, probes, sig: new SignificantKinds(WarKinds.PulseStart, WarKinds.TurnStart, WarKinds.Probe), stops: stops, replayLog: replay);
        long limit = w0 + 4 * PulseMs;
        if (replay is not null || !stops) { rig.R.RunUntil(limit); return rig; }
        int n = 0;
        while (rig.Session.RunUntilWindowFor(0, limit).Status == RunStatus.Interrupted)
        {
            n++;
            if (!orders) continue;
            if (n == 1) { Act(rig, 0, WarKinds.ActMove, 0, 12); Act(rig, 0, WarKinds.ActAttack, 0, 3); Act(rig, 0, WarKinds.ActMove, 1, 22); }
            if (n == 3) { Act(rig, 0, WarKinds.ActRecruit, 4, 777); Act(rig, 0, WarKinds.ActMove, 5, 204); Act(rig, 0, WarKinds.ActMove, 4, 1); }
            if (n == 5) { Act(rig, 0, WarKinds.ActRetreat, 0, 14); Act(rig, 0, WarKinds.ActAttack, 0, 3); Act(rig, 1, WarKinds.ActMove, 8, 20); }
        }
        return rig;
    }

    [Fact]
    public void Budget_IsIdentical_WhetherWindowsAreShownSkippedOrReplayed()
    {
        // Shown with no orders vs auto (no stops): identical refreshes, windows, capacity, slots.
        var shown = Scenario(stops: true, replay: null, orders: false);
        var auto = Scenario(stops: false, replay: null, orders: false);
        Assert.True(shown.R.Interrupts.Count > 0);
        Assert.Empty(auto.R.Interrupts);
        Assert.Equal(shown.B.HashHex(), auto.B.HashHex());
        Assert.Equal(shown.W.StateHashHex(), auto.W.StateHashHex());
        Assert.Equal(shown.R.WindowsHashHex(), auto.R.WindowsHashHex());

        // Shown with orders vs its replay (stops off, orders pre-scheduled).
        var live = Scenario(stops: true, replay: null, orders: true);
        var replay = Scenario(stops: false, replay: live.R.Orders, orders: false);
        Assert.Equal(9, live.R.Orders.Count);
        Assert.Equal((5, 4), (live.B.Actions.Count, live.W.Refusals.Count));
        Assert.Equal(live.B.HashHex(), replay.B.HashHex());
        Assert.Equal(live.W.StateHashHex(), replay.W.StateHashHex());
        Assert.Equal(live.S.Trace.HashHex(), replay.S.Trace.HashHex());
        Assert.Equal(shown.B.Refreshes.ToArray(), live.B.Refreshes.ToArray());   // orders never change refreshes
        Assert.NotEqual(shown.B.HashHex(), live.B.HashHex());                    // teeth: the orders matter
        _out.WriteLine($"T3 golden: state {live.W.StateHashHex()} book {live.B.HashHex()} actions {live.B.Actions.Count} refusals {live.W.Refusals.Count}");
        // T3 golden (first pinned in lane T3; no earlier value).
        Assert.Equal("2E4CB273DCDCBFC6619AC7ECAECF5C5E2A04DCE6A83D3B0FFE62AC49E24F6198", live.W.StateHashHex());
        Assert.Equal("1BBAA5CFA2C2FBBF82AEFCDE24F8EAF1804E25D5669BED642594B0ADCC27E70F", live.B.HashHex());
        // Semantic content of the golden run: which actions were accepted / refused, with reasons.
        Assert.Equal(new[] { ActionKind.Move, ActionKind.Attack, ActionKind.Recruit, ActionKind.Move, ActionKind.Retreat },
                     live.B.Actions.Select(a => a.Kind).ToArray());
        Assert.Equal(new[] { WarRefusal.SlotsExhausted, WarRefusal.SlotsExhausted, WarRefusal.CombinationForbidden, WarRefusal.NotARecipient },
                     live.W.Refusals.Select(r => r.Reason).ToArray());
    }

    // ---------------------------------------------------------------- 8. tie-dense simultaneous actions

    [Fact]
    public void TieDense_SimultaneousEqualCostActions_OutcomesArePolityLocalAndIndependentOfInterleaving()
    {
        // 4 polities x 3 campaign agents, all at one window (TurnStart, every polity a recipient), every action Move cost 1.
        var agents = new List<WarAgentSpec>();
        for (int p = 0; p < 4; p++) for (int j = 0; j < 3; j++) agents.Add(new WarAgentSpec(agents.Count, p, 0, -1, 1000 * p + 10 * j));
        long s1 = S(1);
        string? reference = null;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var rig = Build(s1 + 5 * PulseMs, Array.Empty<(long, int)>(), sig: new SignificantKinds(WarKinds.TurnStart), agents: agents.ToArray(), startMs: s1 - Day);
            To(rig, s1);
            var labels = new[] { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3 };
            var perm = Permutation(12, seed);
            var next = new int[4];
            var issued = new List<(int polity, int agent, int log)>();
            foreach (int i in perm)
            {
                int p = labels[i], agent = 3 * p + next[p]++;
                issued.Add((p, agent, Act(rig, p, WarKinds.ActMove, agent, 1000 * p + 10 * (agent % 3) + 1)));
            }
            Finish(rig, s1 + Day);
            var view = string.Join(";", Enumerable.Range(0, 4).Select(p =>
                string.Join(",", issued.Where(x => x.polity == p).Select(x => $"{x.agent}:{Outcome(rig, x.log)}"))));
            Assert.Equal("0:0,1:0,2:10;3:0,4:0,5:10;6:0,7:0,8:10;9:0,10:0,11:10", view);
            reference ??= view;
            Assert.Equal(reference, view);
        }
    }
}
