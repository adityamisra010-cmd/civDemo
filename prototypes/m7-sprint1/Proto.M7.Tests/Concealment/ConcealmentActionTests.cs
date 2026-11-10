using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Time;
using Proto.M7.War;
using Xunit.Abstractions;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Concealment;

/// <summary>
/// [AP] Lane AP: hide, camouflage and entrench as T3 action kinds (D-082 DA8, DA8-R: the action points are T3's Action Capacity),
/// the war-pulse detection rule they act through, the AMBUSH flag of an attack from concealment, and field works accrual
/// (docs/m7-plan/concealment-actions-proposal.md §2-§4). Worlds are T2 WarTheaterWorlds with a T3 action layer; windows come from
/// Probe events. Every number is a TEST INPUT (profiles, positions) or the lane's PLACEHOLDER (ConcealmentRules.Proposal).
/// Line terrain: cells 0-9 Open, 10-19 Forest (concealment 1/2), 20-29 Jungle (1/4), 30-34 Marsh, 35-39 Water, 40-59 Open.
/// </summary>
public class ConcealmentActionTests
{
    private readonly ITestOutputHelper _out;
    public ConcealmentActionTests(ITestOutputHelper output) => _out = output;

    internal const long Money = 1_000_000;
    internal static readonly long PulseMs = WarPulseT2Tests.ProposedPulseDays[0] * Day;   // band 0: 21 days
    internal const int AllKinds = 0b111_1111;
    internal const long Hours = 3_600_000;

    /// <summary>0 Raider (every kind, sight 6, range 1); 1 Watcher (Move/Attack/Retreat, sight 4); 2 Scout (Move/Retreat only,
    /// sight 3); 3 Spotter (Move/Attack, sight 3, a long-move budget). Costs: Hide 2, Camouflage 3, Entrench 4, Move 1 per cell.</summary>
    internal static readonly AgentProfile[] Profiles =
    {
        new(0, AllKinds, Capacity: 10, MoveCostPerCell: 1, AttackCost: 2, RetreatCost: 1, RecruitCost: 1, AttackRange: 1, RetreatMaxCells: 3)
        { HideCost = 2, CamouflageCost = 3, EntrenchCost = 4, SightCells = 6 },
        new(1, ActionKinds.Bit(ActionKind.Move) | ActionKinds.Bit(ActionKind.Attack) | ActionKinds.Bit(ActionKind.Retreat), 50, 1, 2, 1, 1, 1, 3)
        { SightCells = 4 },
        new(2, ActionKinds.Bit(ActionKind.Move) | ActionKinds.Bit(ActionKind.Retreat), 10, 1, 2, 1, 1, 1, 5) { SightCells = 3 },
        new(3, ActionKinds.Bit(ActionKind.Move) | ActionKinds.Bit(ActionKind.Attack), 50, 1, 2, 1, 1, 1, 3) { SightCells = 3 },
    };

    internal static TerrainClass[] Line()
    {
        var t = new TerrainClass[60];
        for (int x = 0; x < t.Length; x++)
            t[x] = x < 10 ? TerrainClass.Open : x < 20 ? TerrainClass.Forest : x < 30 ? TerrainClass.Jungle
                 : x < 35 ? TerrainClass.Marsh : x < 40 ? TerrainClass.Water : TerrainClass.Open;
        return t;
    }

    internal sealed record Rig(CampaignCalendar Cal, EventScheduler S, WarTheaterWorld W, Runner R)
    {
        public ActionWindowBook B => W.Actions!;
    }

    internal static long S(int turn) => ShippedCalendar().TurnStart(turn).Ms;
    internal static long W0 => S(1) - 10 * Day;

    /// <summary>The test world: by default one war (id 0, polities 0 and 1, from W0 for 40 pulses); [AP round 2] <paramref name="wars"/>
    /// replaces that list (the enemy relation of AP-R2-04).</summary>
    internal static Rig Build((long at, int polity)[] probes, WarAgentSpec[] agents, int polities = 3, ConcealmentRules? rules = null,
                              bool stops = true, SubTurnOrderLog? replayLog = null, AgentProfile[]? profiles = null, WarSpec[]? wars = null)
    {
        var cal = ShippedCalendar();
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = WarPulseT2Tests.ProposedPulseDays[i] * Day;
        var sched = new WarPulseSchedule(cal, table);
        var s = new EventScheduler(W0 - 30 * Day);
        var sk = new SignificantKinds(WarKinds.Probe);
        wars ??= new[] { War0() };
        var layer = new ActionLayerSpec(profiles ?? Profiles, agents, 0xA11CEUL) { LineTerrain = Line(), ConcealmentRules = rules };
        var w = new WarTheaterWorld(cal, sched, polities, wars, WarFog.None, sk, new long[polities], Money, Money, layer);
        w.Begin(s);
        foreach (var (at, p) in probes) WarTheaterWorld.ScheduleProbe(s, at, p);
        var r = replayLog is null ? new Runner(s, w, sk, stops) : Runner.CreateReplay(s, w, sk, replayLog);
        return new Rig(cal, s, w, r);
    }

    /// <summary>The default war: id 0 between polities 0 and 1, from W0 for 40 pulses.</summary>
    internal static WarSpec War0() => new(0, W0, W0 + 40 * PulseMs, new[] { 0, 1 }, 0, 0, new long[] { 0, 0 });

    internal static void To(Rig rig, long at)
    {
        var r = rig.R.RunUntil(at);
        Assert.Equal((RunStatus.Interrupted, at), (r.Status, r.AtMs));
    }

    internal static int Act(Rig rig, int polity, int kind, int agent, long arg) => rig.R.IssueOrder(kind, WarKinds.Pack(polity, agent), arg).Seq;

    internal static int Outcome(Rig rig, int log)
    {
        foreach (var r in rig.W.Refusals) if (r.LogIndex == log) return r.Reason;
        foreach (var a in rig.B.Actions) if (a.LogIndex == log) return 0;
        throw new Xunit.Sdk.XunitException($"order {log} did not execute");
    }

    internal static void Finish(Rig rig, long limit)
    {
        while (rig.R.RunUntil(limit).Status == RunStatus.Interrupted) { }
    }

    /// <summary>The action layer without log indices: positions, capacity, slots, states, changes, engagements.</summary>
    internal static string View(Rig rig)
    {
        var b = rig.B;
        var parts = new List<string>();
        for (int i = 0; i < b.AgentCount; i++) parts.Add($"a{i}:{b.PositionOf(i)}/{b.RemainingOf(i)}/{b.StatesOf(i)}/{b.EntrenchSince(i)}");
        for (int p = 0; p < 3; p++) parts.Add($"p{p}:{b.SlotsUsed(p, -1)}/{b.SlotsUsed(p, 0)}");
        foreach (var a in b.Actions) parts.Add($"{a.Polity}.{a.AgentId}.{a.Kind}@{a.AtMs}:{a.Arg}c{a.Cost}");
        foreach (var c in b.ConcealmentChanges) parts.Add($"C{c.AgentId}{c.Kind}{(c.Begin ? "+" : "-")}{c.Cause}@{c.AtMs}");
        foreach (var e in b.Engagements) parts.Add($"E{e.Attacker}>{e.Target}@{e.AtMs}:{e.Ambush}/{e.AttackerStates}/{e.TargetWorksPpm}");
        return string.Join("|", parts);
    }

    internal static WarAgentSpec A(int id, int polity, int profile, long x, int war = 0) => new(id, polity, profile, war, x);

    // ---------------------------------------------------------------- 1. the detection rule: hide shortens the distance

    /// <summary>
    /// MECHANISM (proposal §2.1): a hidden agent is detected only within SightCells × the concealment of its cell. a0 hides in a
    /// Forest (1/2) or Jungle (1/4) cell; the Watcher (sight 4) then closes to distance d. Detected iff d ≤ 4 × factor, exactly:
    /// Forest 2 (d = 2 spotted, 3 not), Jungle 1 (d = 1 spotted, 2 not). Spotting ENDS the hide at the move's instant, with the
    /// move's log index as the cause. Control (teeth): without the hide the same distances are all detected (d ≤ 4).
    /// </summary>
    [Theory]
    [InlineData(12L, 2L, true)]
    [InlineData(12L, 3L, false)]
    [InlineData(22L, 1L, true)]
    [InlineData(22L, 2L, false)]
    public void Hide_ShortensTheDetectionDistanceByTheCellsConcealment_Exactly(long cell, long d, bool spotted)
    {
        long t0 = W0 + Day, t1 = W0 + 2 * Day;
        foreach (bool hide in new[] { true, false })
        {
            var rig = Build(new[] { (t0, 0), (t1, 1) }, new[] { A(0, 0, 0, cell), A(1, 1, 1, cell + 10) });
            To(rig, t0);
            int h = hide ? Act(rig, 0, WarKinds.ActHide, 0, 0) : -1;
            To(rig, t1);
            if (hide) Assert.Equal(0, Outcome(rig, h));
            Assert.Equal(hide, rig.B.IsHidden(0));
            Assert.False(rig.B.Detects(1, 0));                                   // 10 cells away: beyond even the open distance
            int m = Act(rig, 1, WarKinds.ActMove, 1, cell + d);
            Finish(rig, t1 + Day);
            Assert.Equal(0, Outcome(rig, m));
            if (!hide) { Assert.True(rig.B.Detects(1, 0)); continue; }       // control: d <= 4 is always detected in the open
            Assert.Equal(!spotted, rig.B.IsHidden(0));
            Assert.Equal(spotted, rig.B.Detects(1, 0));
            var last = rig.B.ConcealmentChanges[^1];
            if (spotted)
                Assert.Equal((0, ConcealKind.Hidden, false, ConcealEnd.Spotted, m, t1), (last.AgentId, last.Kind, last.Begin, last.Cause, last.LogIndex, last.AtMs));
            else
                Assert.Equal((0, ConcealKind.Hidden, true, h, t0), (last.AgentId, last.Kind, last.Begin, last.LogIndex, last.AtMs));
        }
    }

    /// <summary>MECHANISM: camouflage multiplies the distance by CamouflageFactor (1/2) in any terrain, the open included; with a
    /// hide in a Forest the two multiply (1/2 × 1/2 = 1/4). The factor and the exact detection boundaries are pinned; a spotting
    /// ends both states.</summary>
    [Fact]
    public void Camouflage_WorksInTheOpen_AndCombinesWithAHideMultiplicatively()
    {
        long t0 = W0 + Day, t1 = W0 + PulseMs + Day, t2 = W0 + PulseMs + 2 * Day;
        Rig Case(long cell, bool hide, long d)
        {
            var rig = Build(new[] { (t0, 0), (t1, 0), (t2, 1) }, new[] { A(0, 0, 0, cell), A(1, 1, 1, cell + 10) });
            To(rig, t0);
            int c = Act(rig, 0, WarKinds.ActCamouflage, 0, 0);
            To(rig, t1);
            int h = hide ? Act(rig, 0, WarKinds.ActHide, 0, 0) : -1;
            To(rig, t2);
            Assert.Equal(0, Outcome(rig, c));
            if (hide) Assert.Equal(0, Outcome(rig, h));
            Act(rig, 1, WarKinds.ActMove, 1, cell + d);
            Finish(rig, t2 + Day);
            return rig;
        }
        Assert.Equal(Q32.FromRatio(1, 2), Case(5, false, 9).B.ConcealmentFactor(0));
        var open2 = Case(5, false, 2); var open3 = Case(5, false, 3);
        Assert.True(open2.B.Detects(1, 0)); Assert.False(open2.B.IsCamouflaged(0));        // 2 <= 4 x 1/2: spotted, camouflage ends
        Assert.False(open3.B.Detects(1, 0)); Assert.True(open3.B.IsCamouflaged(0));
        Assert.Equal(Q32.FromRatio(1, 4), Case(12, true, 9).B.ConcealmentFactor(0));
        var forest1 = Case(12, true, 1); var forest2 = Case(12, true, 2);
        Assert.True(forest1.B.Detects(1, 0)); Assert.False(forest1.B.IsHidden(0)); Assert.False(forest1.B.IsCamouflaged(0));   // both end
        Assert.False(forest2.B.Detects(1, 0)); Assert.True(forest2.B.IsHidden(0)); Assert.True(forest2.B.IsCamouflaged(0));
        Assert.Equal(new[] { ConcealEnd.Spotted, ConcealEnd.Spotted }, forest1.B.ConcealmentChanges.Where(c => !c.Begin).Select(c => c.Cause).ToArray());
    }

    // ---------------------------------------------------------------- 2. refusals: recorded, inert

    /// <summary>
    /// Every precondition refuses with its own code and changes nothing (no slot, no capacity, no state, no change row): NoCover
    /// (Hide in the open), EnemyInContact (an enemy within the attack range), Observed (an enemy polity detects the agent) — [AP
    /// round 2] both judged on enemy agents the issuer's polity detects; every enemy in these cases is seen (ConcealmentKnowledgeTests),
    /// AlreadyInState (the state held from an earlier window), CannotDig (Entrench in a marsh or water), BadValue (B ≠ 0),
    /// NotInActionSet (the Scout has no Hide), CapacityExhausted (Entrench costs 4 with 3 left), TargetUndetected (an Attack on a
    /// hidden enemy in reach). Each run is compared with the same run without the refused order: the layers are identical.
    /// </summary>
    [Fact]
    public void Refusals_AreRecordedWithTheirCode_AndChangeNothing()
    {
        long t0 = W0 + Day, t1 = W0 + PulseMs + Day;
        (int Reason, string View, string Clean) Run(WarAgentSpec[] agents, Func<Rig, int> refused, Action<Rig>? first = null, Action<Rig>? same = null, int polity = 0)
        {
            Rig Go(bool withRefused, out int log)
            {
                var rig = Build(new[] { (t0, 0), (t0, 1), (t1, polity) }, agents);
                To(rig, t0);
                first?.Invoke(rig);
                To(rig, t1);
                same?.Invoke(rig);
                log = withRefused ? refused(rig) : -1;
                Finish(rig, t1 + Day);
                return rig;
            }
            var a = Go(true, out int l);
            var b = Go(false, out _);
            return (Outcome(a, l), View(a), View(b));
        }
        void Check(int expected, (int Reason, string View, string Clean) r)
        {
            Assert.Equal(expected, r.Reason);
            Assert.Equal(r.Clean, r.View);
        }
        var lone = new[] { A(0, 0, 0, 12), A(1, 1, 1, 50) };
        Check(WarRefusal.NoCover, Run(new[] { A(0, 0, 0, 5), A(1, 1, 1, 50) }, r => Act(r, 0, WarKinds.ActHide, 0, 0)));
        Check(WarRefusal.EnemyInContact, Run(new[] { A(0, 0, 0, 12), A(1, 1, 2, 13) }, r => Act(r, 0, WarKinds.ActHide, 0, 0)));
        Check(WarRefusal.EnemyInContact, Run(new[] { A(0, 0, 0, 5), A(1, 1, 2, 6) }, r => Act(r, 0, WarKinds.ActCamouflage, 0, 0)));
        Check(WarRefusal.Observed, Run(new[] { A(0, 0, 0, 12), A(1, 1, 1, 16) }, r => Act(r, 0, WarKinds.ActHide, 0, 0)));        // 4 <= 4
        Check(WarRefusal.Observed, Run(new[] { A(0, 0, 0, 5), A(1, 1, 1, 9) }, r => Act(r, 0, WarKinds.ActCamouflage, 0, 0)));
        Check(WarRefusal.AlreadyInState, Run(lone, r => Act(r, 0, WarKinds.ActHide, 0, 0), first: r => Act(r, 0, WarKinds.ActHide, 0, 0)));
        Check(WarRefusal.AlreadyInState, Run(lone, r => Act(r, 0, WarKinds.ActEntrench, 0, 0), first: r => Act(r, 0, WarKinds.ActEntrench, 0, 0)));
        Check(WarRefusal.AlreadyInState, Run(lone, r => Act(r, 0, WarKinds.ActCamouflage, 0, 0), first: r => Act(r, 0, WarKinds.ActCamouflage, 0, 0)));
        Check(WarRefusal.CannotDig, Run(new[] { A(0, 0, 0, 31), A(1, 1, 1, 50) }, r => Act(r, 0, WarKinds.ActEntrench, 0, 0)));
        Check(WarRefusal.CannotDig, Run(new[] { A(0, 0, 0, 36), A(1, 1, 1, 50) }, r => Act(r, 0, WarKinds.ActEntrench, 0, 0)));
        Check(WarRefusal.BadValue, Run(lone, r => Act(r, 0, WarKinds.ActHide, 0, 5)));
        Check(WarRefusal.NotInActionSet, Run(new[] { A(0, 0, 2, 12), A(1, 1, 1, 50) }, r => Act(r, 0, WarKinds.ActHide, 0, 0)));
        // capacity 10 - (Move 19 -> 12: 7) = 3 < Entrench 4; the Move is in the same window in both runs
        Check(WarRefusal.CapacityExhausted, Run(new[] { A(0, 0, 0, 19), A(1, 1, 1, 50) }, r => Act(r, 0, WarKinds.ActEntrench, 0, 0),
                                                same: r => Act(r, 0, WarKinds.ActMove, 0, 12)));
        // the hidden Raider stands 1 cell from the Spotter (sight 3 x 1/4 < 1): in reach, not detected, cannot be targeted
        Check(WarRefusal.TargetUndetected, Run(new[] { A(0, 0, 0, 22), A(1, 1, 3, 40) }, r => Act(r, 1, WarKinds.ActAttack, 1, 0),
                                               first: r => { Act(r, 0, WarKinds.ActHide, 0, 0); Act(r, 1, WarKinds.ActMove, 1, 23); }, polity: 1));
        // [AP round 1, AP-R1-06] the detection check comes BEFORE the reach (proposal §3.3): the hidden Raider 3 cells from the
        // Spotter (out of reach, range 1; undetected, 3 > 3 x 1/4) is refused TargetUndetected, not OutOfReach, so the refusal says
        // nothing about an unseen agent's distance; the same target unconcealed is detected (3 <= 3) and refused OutOfReach (control)
        Check(WarRefusal.TargetUndetected, Run(new[] { A(0, 0, 0, 22), A(1, 1, 3, 40) }, r => Act(r, 1, WarKinds.ActAttack, 1, 0),
                                               first: r => { Act(r, 0, WarKinds.ActHide, 0, 0); Act(r, 1, WarKinds.ActMove, 1, 25); }, polity: 1));
        Check(WarRefusal.OutOfReach, Run(new[] { A(0, 0, 0, 22), A(1, 1, 3, 40) }, r => Act(r, 1, WarKinds.ActAttack, 1, 0),
                                         first: r => Act(r, 1, WarKinds.ActMove, 1, 25), polity: 1));
        // [AP round 1, AP-R1-07] a CAMOUFLAGED agent still needs cover to hide: camouflage in the open (cell 5), then a hide there
        Check(WarRefusal.NoCover, Run(new[] { A(0, 0, 0, 5), A(1, 1, 1, 50) }, r => Act(r, 0, WarKinds.ActHide, 0, 0),
                                      first: r => Act(r, 0, WarKinds.ActCamouflage, 0, 0)));
    }

    // ---------------------------------------------------------------- 2a. the action points: each new kind spends its cost

    /// <summary>
    /// DA8 ("Units can spend action points to hide/entrench/camouflage etc."; DA8-R: T3's Action Capacity): an accepted Hide,
    /// Camouflage or Entrench SPENDS its profile cost from the agent's capacity at its instant, and its action record carries that cost.
    /// The test costs are distinct (Hide 2, Camouflage 3, Entrench 4 of capacity 10; TEST INPUTS), so a cost read from another field is
    /// seen. AT THE BOUNDARY: a march first leaves exactly the cost (accepted, nothing left) or one unit less (CapacityExhausted: no
    /// state, no change row, the capacity unchanged, only the march's slot used). [AP round 1, AP-R1-01: before this test the Hide and
    /// Camouflage costs were pinned only by the golden hash.]
    /// </summary>
    [Theory]
    [InlineData(WarKinds.ActHide, 2L)]
    [InlineData(WarKinds.ActCamouflage, 3L)]
    [InlineData(WarKinds.ActEntrench, 4L)]
    public void EveryNewKind_SpendsItsProfileCost_FromActionCapacity(int kind, long cost)
    {
        long t0 = W0 + Day;
        var fresh = Build(new[] { (t0, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50) });
        To(fresh, t0);
        int o = Act(fresh, 0, kind, 0, 0);
        Finish(fresh, t0 + Day);
        Assert.Equal(0, Outcome(fresh, o));
        Assert.Equal(cost, fresh.B.Actions.Single(a => a.LogIndex == o).Cost);
        Assert.Equal(10 - cost, fresh.B.RemainingOf(0));
        foreach (long left in new[] { cost, cost - 1 })
        {
            // march east from the Forest at 12 (1 per cell): 10 - left cells, ending in Forest or Jungle (concealing and diggable),
            // 29 or more cells from the Watcher at 50 (sight 4): every precondition holds except, in the second run, the capacity
            long to = 12 + (10 - left);
            var rig = Build(new[] { (t0, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50) });
            To(rig, t0);
            int m = Act(rig, 0, WarKinds.ActMove, 0, to);
            int k = Act(rig, 0, kind, 0, 0);
            Finish(rig, t0 + Day);
            Assert.Equal(0, Outcome(rig, m));
            Assert.True(to < 30 && rig.B.PositionOf(0) == to);
            if (left == cost)
            {
                Assert.Equal(0, Outcome(rig, k));
                Assert.Equal((cost, 0L), (rig.B.Actions.Single(a => a.LogIndex == k).Cost, rig.B.RemainingOf(0)));
                Assert.NotEqual(ConcealKind.None, rig.B.StatesOf(0));
            }
            else
            {
                Assert.Equal(WarRefusal.CapacityExhausted, Outcome(rig, k));
                Assert.Equal((left, ConcealKind.None, 1), (rig.B.RemainingOf(0), rig.B.StatesOf(0), rig.B.SlotsUsed(0, 0)));
                Assert.Empty(rig.B.ConcealmentChanges);
            }
        }
    }

    // ---------------------------------------------------------------- 3. what ends a state

    /// <summary>A hide and a camouflage end when the agent moves or retreats (cause Moved; its works are lost too) or attacks (cause
    /// Attacked; its works stay). Each end row carries the order's log index and instant.</summary>
    [Fact]
    public void States_EndOnMoveRetreatAndAttack_WithTheirCause_AndWorksAreLostOnlyByLeaving()
    {
        long t0 = W0 + Day, t1 = W0 + PulseMs + Day, t2 = W0 + 2 * PulseMs + Day;
        // (a) move: the hidden, camouflaged, entrenched Raider at 12 moves to 11
        var rig = Build(new[] { (t0, 0), (t1, 0), (t2, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50) });
        To(rig, t0);
        Act(rig, 0, WarKinds.ActHide, 0, 0); Act(rig, 0, WarKinds.ActEntrench, 0, 0);
        To(rig, t1);
        Act(rig, 0, WarKinds.ActCamouflage, 0, 0);
        To(rig, t2);
        Assert.Equal(ConcealKind.Hidden | ConcealKind.Camouflaged | ConcealKind.Entrenched, rig.B.StatesOf(0));
        Assert.Equal(ConcealmentRules.WorksFullPpm, rig.B.WorksOf(0, t2));
        int mv = Act(rig, 0, WarKinds.ActMove, 0, 11);
        Finish(rig, t2 + Day);
        Assert.Equal(ConcealKind.None, rig.B.StatesOf(0));
        Assert.Equal(0, rig.B.WorksOf(0, t2 + Day));
        Assert.Equal(new[] { (ConcealKind.Hidden, ConcealEnd.Moved), (ConcealKind.Camouflaged, ConcealEnd.Moved), (ConcealKind.Entrenched, ConcealEnd.Moved) },
                     rig.B.ConcealmentChanges.Where(c => !c.Begin).Select(c => (c.Kind, c.Cause)).ToArray());
        Assert.All(rig.B.ConcealmentChanges.Where(c => !c.Begin), c => Assert.Equal((mv, t2), (c.LogIndex, c.AtMs)));

        // (b) retreat: the Raider hides and digs in, the Spotter closes undetected (3 x 1/4 < 1), the Raider retreats from the contact
        var r2 = Build(new[] { (t0, 0), (t0 + Hours, 1), (t1, 0) }, new[] { A(0, 0, 0, 22), A(1, 1, 3, 40) });
        To(r2, t0);
        Act(r2, 0, WarKinds.ActHide, 0, 0); Act(r2, 0, WarKinds.ActEntrench, 0, 0);
        To(r2, t0 + Hours);
        Act(r2, 1, WarKinds.ActMove, 1, 23);
        To(r2, t1);
        int rt = Act(r2, 0, WarKinds.ActRetreat, 0, 20);
        Finish(r2, t1 + Day);
        Assert.Equal(0, Outcome(r2, rt));
        Assert.Equal(new[] { (ConcealKind.Hidden, ConcealEnd.Moved), (ConcealKind.Entrenched, ConcealEnd.Moved) },
                     r2.B.ConcealmentChanges.Where(c => !c.Begin).Select(c => (c.Kind, c.Cause)).ToArray());

        // (c) attack: the hidden, entrenched Raider strikes the Spotter: the hide ends (Attacked), the works stay
        var r3 = Build(new[] { (t0, 0), (t0 + Hours, 1), (t1, 0) }, new[] { A(0, 0, 0, 22), A(1, 1, 3, 40) });
        To(r3, t0);
        Act(r3, 0, WarKinds.ActHide, 0, 0); Act(r3, 0, WarKinds.ActEntrench, 0, 0);
        To(r3, t0 + Hours);
        Act(r3, 1, WarKinds.ActMove, 1, 23);
        To(r3, t1);
        int at = Act(r3, 0, WarKinds.ActAttack, 0, 1);
        Finish(r3, t1 + Day);
        Assert.Equal(0, Outcome(r3, at));
        Assert.Equal(ConcealKind.Entrenched, r3.B.StatesOf(0));
        Assert.Equal(ConcealmentRules.WorksFullPpm, r3.B.WorksOf(0, t1));
        var end = r3.B.ConcealmentChanges.Single(c => !c.Begin);
        Assert.Equal((ConcealKind.Hidden, ConcealEnd.Attacked, at), (end.Kind, end.Cause, end.LogIndex));
    }

    // ---------------------------------------------------------------- 4. the ambush flag

    /// <summary>
    /// AMBUSH (proposal §5.2): an Attack by an agent holding a hide or a camouflage that the target's polity does not detect at the
    /// instant. The Raider hides in the Jungle (22); the Spotter (sight 3) closes to 23 without spotting it (3 × 1/4 &lt; 1); in a
    /// later window the Raider attacks: the engagement is an ambush with the attacker's state Hidden, and the hide then ends
    /// (Attacked). The visible attack one window later is not an ambush. Camouflage alone: an observer of sight 1 (TEST INPUT) does
    /// not detect a camouflaged agent at distance 1 (1 × 1/2 &lt; 1), so its attack is an ambush with the state Camouflaged.
    /// </summary>
    [Fact]
    public void Ambush_AnAttackFromConcealment_IsFlagged_AndAVisibleAttackIsNot()
    {
        long t0 = W0 + Day, tm = W0 + Day + Hours, t1 = W0 + PulseMs + Day, t2 = W0 + 2 * PulseMs + Day;
        var rig = Build(new[] { (t0, 0), (tm, 1), (t1, 0), (t2, 0) }, new[] { A(0, 0, 0, 22), A(1, 1, 3, 40) });
        To(rig, t0);
        Act(rig, 0, WarKinds.ActHide, 0, 0);
        To(rig, tm);
        Act(rig, 1, WarKinds.ActMove, 1, 23);
        To(rig, t1);
        Assert.True(rig.B.IsHidden(0));
        Assert.False(rig.B.Detects(1, 0));
        Assert.True(rig.B.Detects(0, 1));
        int amb = Act(rig, 0, WarKinds.ActAttack, 0, 1);
        To(rig, t2);
        int vis = Act(rig, 0, WarKinds.ActAttack, 0, 1);
        Finish(rig, t2 + Day);
        Assert.Equal((0, 0), (Outcome(rig, amb), Outcome(rig, vis)));
        var e = rig.B.Engagements;
        Assert.Equal(2, e.Count);
        Assert.Equal((amb, t1, 0, 1, 0, 1, true, ConcealKind.Hidden),
                     (e[0].LogIndex, e[0].AtMs, e[0].Attacker, e[0].Target, e[0].AttackerPolity, e[0].TargetPolity, e[0].Ambush, e[0].AttackerStates));
        Assert.Equal((vis, false, ConcealKind.None), (e[1].LogIndex, e[1].Ambush, e[1].AttackerStates));
        Assert.Equal(ConcealEnd.Attacked, rig.B.ConcealmentChanges.Single(c => !c.Begin).Cause);

        var prof = Profiles.Select(p => p.Id == 3 ? p with { SightCells = 1 } : p).ToArray();
        var rc = Build(new[] { (t0, 0), (tm, 1), (t1, 0) }, new[] { A(0, 0, 0, 5), A(1, 1, 3, 40) }, profiles: prof);
        To(rc, t0);
        Act(rc, 0, WarKinds.ActCamouflage, 0, 0);
        To(rc, tm);
        Act(rc, 1, WarKinds.ActMove, 1, 6);
        To(rc, t1);
        int cam = Act(rc, 0, WarKinds.ActAttack, 0, 1);
        Finish(rc, t1 + Day);
        Assert.Equal(0, Outcome(rc, cam));
        Assert.Equal((true, ConcealKind.Camouflaged), (rc.B.Engagements[0].Ambush, rc.B.Engagements[0].AttackerStates));
    }

    /// <summary>
    /// TURN-EXACT PIN (the ambush window rule): Hide → Attack of the same agent in ONE window is CombinationForbidden; the ambush
    /// needs the hide in an earlier window. The hide is ordered 2 days before the pulse start X; the attack at X − 1 ms is in the
    /// same pulse (refused, and the agent stays hidden), at X and X + 1 ms in the next pulse (accepted, an ambush).
    /// </summary>
    [Theory]
    [InlineData(-1L, WarRefusal.CombinationForbidden)]
    [InlineData(0L, 0)]
    [InlineData(1L, 0)]
    public void Ambush_NeedsTheHideInAnEarlierWindow_TurnExactAtThePulseStart(long delta, int expected)
    {
        long x1 = W0 + PulseMs, th = x1 - 2 * Day, tm = x1 - Day, ta = x1 + delta;
        var rig = Build(new[] { (th, 0), (tm, 1), (ta, 0) }, new[] { A(0, 0, 0, 22), A(1, 1, 3, 40) });
        To(rig, th);
        Act(rig, 0, WarKinds.ActHide, 0, 0);
        To(rig, tm);
        Act(rig, 1, WarKinds.ActMove, 1, 23);
        To(rig, ta);
        int a = Act(rig, 0, WarKinds.ActAttack, 0, 1);
        Finish(rig, ta + Day);
        Assert.Equal(expected, Outcome(rig, a));
        if (expected == 0)
        {
            var e = rig.B.Engagements.Single();
            Assert.Equal((ta, true), (e.AtMs, e.Ambush));
            Assert.Equal(1L, rig.B.Actions.Single(x => x.LogIndex == a).WindowIndex);   // pulse 1's window
            Assert.False(rig.B.IsHidden(0));
        }
        else { Assert.Empty(rig.B.Engagements); Assert.True(rig.B.IsHidden(0)); }
    }

    // ---------------------------------------------------------------- 5. same-instant delivery: log order decides

    /// <summary>
    /// TURN-EXACT PIN (when an action takes effect relative to when it was ordered): an action applies AT its instant T, in log
    /// order. At one T, polity 1 moves the Watcher to distance 3 of the Raider (inside the open distance 4, outside the hidden one
    /// 2) and polity 0 hides the Raider. Hide first: accepted, and the later move does not spot it. Move first: the Raider is
    /// Observed when its hide executes, and the hide is refused. Replay equality alone could not tell these apart.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameInstant_TheLogOrderDecidesWhetherTheHideHolds(bool hideFirst)
    {
        long t = W0 + Day;
        var rig = Build(new[] { (t, 0), (t, 1) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 20) });
        To(rig, t);
        int h, m;
        if (hideFirst) { h = Act(rig, 0, WarKinds.ActHide, 0, 0); m = Act(rig, 1, WarKinds.ActMove, 1, 15); }
        else { m = Act(rig, 1, WarKinds.ActMove, 1, 15); h = Act(rig, 0, WarKinds.ActHide, 0, 0); }
        Finish(rig, t + Day);
        Assert.Equal(0, Outcome(rig, m));
        Assert.Equal(15, rig.B.PositionOf(1));
        Assert.Equal(hideFirst ? 0 : WarRefusal.Observed, Outcome(rig, h));
        Assert.Equal(hideFirst, rig.B.IsHidden(0));
        Assert.Equal(!hideFirst, rig.B.Detects(1, 0));
    }

    /// <summary>TURN-EXACT PIN (the enclosing window, T3's rule, for every new kind): two slots used in pulse 0; the new action at
    /// X − 1 ms is refused SlotsExhausted; at X and X + 1 ms it spends pulse 1's fresh window, and its state begins at its instant.</summary>
    [Theory]
    [InlineData(WarKinds.ActHide, -1L, WarRefusal.SlotsExhausted)]
    [InlineData(WarKinds.ActHide, 0L, 0)]
    [InlineData(WarKinds.ActHide, 1L, 0)]
    [InlineData(WarKinds.ActCamouflage, -1L, WarRefusal.SlotsExhausted)]
    [InlineData(WarKinds.ActCamouflage, 0L, 0)]
    [InlineData(WarKinds.ActCamouflage, 1L, 0)]
    [InlineData(WarKinds.ActEntrench, -1L, WarRefusal.SlotsExhausted)]
    [InlineData(WarKinds.ActEntrench, 0L, 0)]
    [InlineData(WarKinds.ActEntrench, 1L, 0)]
    public void EveryNewKind_UsesTheWindowContainingItsInstant(int kind, long delta, int expected)
    {
        long x1 = W0 + PulseMs, t = x1 + delta;
        var rig = Build(new[] { (x1 - 3 * Day, 0), (t, 0) }, new[] { A(0, 0, 0, 12), A(1, 0, 0, 14), A(2, 0, 0, 16), A(3, 1, 1, 55) });
        To(rig, x1 - 3 * Day);
        Act(rig, 0, WarKinds.ActMove, 1, 13);
        Act(rig, 0, WarKinds.ActMove, 2, 17);
        To(rig, t);
        int o = Act(rig, 0, kind, 0, 0);
        Finish(rig, x1 + Day);
        Assert.Equal(expected, Outcome(rig, o));
        var row = rig.B.StateRows()[0];
        if (expected == 0)
        {
            var rec = rig.B.Actions.Single(a => a.LogIndex == o);
            Assert.Equal((0, 1L, t), (rec.WarId, rec.WindowIndex, rec.AtMs));
            long since = kind == WarKinds.ActHide ? row.HiddenSinceMs : kind == WarKinds.ActCamouflage ? row.CamouflagedSinceMs : row.EntrenchSinceMs;
            Assert.Equal(t, since);
        }
        else Assert.Equal(ConcealKind.None, rig.B.StatesOf(0));
    }

    /// <summary>TURN-EXACT PIN in the CAMPAIGN window (T3's rule: an agent outside an active war, WarId −1, acts in the current
    /// strategic turn's window), for every new kind: two slots used 3 days before turn 2 starts; the new action at the start − 1 ms is
    /// refused SlotsExhausted; at the start and + 1 ms it spends turn 2's fresh campaign window (WarId −1, window 2) and the capacity
    /// refreshed at the turn start, and its state begins at its instant. [AP round 1, AP-R1-13: the new kinds' window pins covered
    /// only operational (War Pulse) windows.]</summary>
    [Theory]
    [InlineData(WarKinds.ActHide, -1L, WarRefusal.SlotsExhausted)]
    [InlineData(WarKinds.ActHide, 0L, 0)]
    [InlineData(WarKinds.ActHide, 1L, 0)]
    [InlineData(WarKinds.ActCamouflage, -1L, WarRefusal.SlotsExhausted)]
    [InlineData(WarKinds.ActCamouflage, 0L, 0)]
    [InlineData(WarKinds.ActCamouflage, 1L, 0)]
    [InlineData(WarKinds.ActEntrench, -1L, WarRefusal.SlotsExhausted)]
    [InlineData(WarKinds.ActEntrench, 0L, 0)]
    [InlineData(WarKinds.ActEntrench, 1L, 0)]
    public void EveryNewKind_InTheCampaignWindow_UsesTheTurnContainingItsInstant(int kind, long delta, int expected)
    {
        long s2 = S(2), t = s2 + delta;
        var rig = Build(new[] { (s2 - 3 * Day, 0), (t, 0) },
                        new[] { A(0, 0, 0, 12, war: -1), A(1, 0, 0, 14, war: -1), A(2, 0, 0, 16, war: -1), A(3, 1, 1, 55, war: -1) });
        To(rig, s2 - 3 * Day);
        Act(rig, 0, WarKinds.ActMove, 1, 13);
        Act(rig, 0, WarKinds.ActMove, 2, 17);
        To(rig, t);
        int o = Act(rig, 0, kind, 0, 0);
        Finish(rig, s2 + Day);
        Assert.Equal(expected, Outcome(rig, o));
        Assert.Equal(2, rig.B.Actions.Count(a => a.WarId == -1 && a.WindowIndex == 1));
        long cost = kind == WarKinds.ActHide ? 2 : kind == WarKinds.ActCamouflage ? 3 : 4;
        var row = rig.B.StateRows()[0];
        if (expected == 0)
        {
            var rec = rig.B.Actions.Single(a => a.LogIndex == o);
            Assert.Equal((-1, 2L, t), (rec.WarId, rec.WindowIndex, rec.AtMs));       // turn 2's fresh campaign window
            Assert.Equal((1, 10 - cost), (rig.B.SlotsUsed(0, -1), rig.B.RemainingOf(0)));
            long since = kind == WarKinds.ActHide ? row.HiddenSinceMs : kind == WarKinds.ActCamouflage ? row.CamouflagedSinceMs : row.EntrenchSinceMs;
            Assert.Equal(t, since);
        }
        else Assert.Equal((ConcealKind.None, 0, 10L), (rig.B.StatesOf(0), rig.B.SlotsUsed(0, -1), rig.B.RemainingOf(0)));
    }

    /// <summary>
    /// THE SAME-AGENT COMBINATION MATRIX for every ordered pair that involves a lane-AP kind (proposal §3.4; T3's 4 × 4 block is
    /// pinned by ActionWindowsT3Tests): the outcome of the SECOND action of one agent in one window — accepted (0),
    /// CombinationForbidden or KindRepeated — is exactly the proposal's table; an accepted pair applies both actions (every applied
    /// lane-AP kind holds its state at the end), a refused second action applies nothing. The matrix is checked before any target or
    /// state precondition, so a forbidden second action is refused as such even where it would be illegal anyway. Pairs whose first
    /// action needs an enemy in contact (Attack, Retreat), and Entrench → Attack, run with the Watcher adjacent (13); every other pair
    /// with it far away (50), where a hide or camouflage is legal (no enemy in contact, not observed).
    /// </summary>
    [Fact]
    public void CombinationMatrix_EveryOrderedPairWithANewKind_IsTheProposalsTable()
    {
        const int K = WarRefusal.KindRepeated, C = WarRefusal.CombinationForbidden;
        int[,] expected =
        {
            //  Move Attack Retreat Recruit Hide Camo Entrench   (second)
            {   K,   0,     C,      0,      0,   0,   0 },      // Move
            {   C,   K,     C,      C,      C,   C,   C },      // Attack
            {   C,   C,     K,      C,      C,   C,   C },      // Retreat
            {   0,   C,     0,      K,      0,   0,   0 },      // Recruit
            {   C,   C,     C,      0,      K,   0,   0 },      // Hide
            {   C,   C,     C,      0,      0,   K,   0 },      // Camouflage
            {   C,   0,     C,      0,      0,   0,   K },      // Entrench
        };
        long t = W0 + Day;
        int pairs = 0;
        for (int i = 0; i < ActionKinds.Count; i++)
            for (int j = 0; j < ActionKinds.Count; j++)
            {
                if (i < 4 && j < 4) continue;                                    // T3's block: ActionWindowsT3Tests
                var first = (ActionKind)i; var second = (ActionKind)j;
                bool contact = first is ActionKind.Attack or ActionKind.Retreat || (first == ActionKind.Entrench && second == ActionKind.Attack);
                var rig = Build(new[] { (t, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, contact ? 13 : 50) });
                To(rig, t);
                int a = Act(rig, 0, ActionKinds.OrderBase + i, 0, ArgOf(first));
                int b = Act(rig, 0, ActionKinds.OrderBase + j, 0, ArgOf(second));
                Finish(rig, t + Day);
                Assert.True(Outcome(rig, a) == 0, $"{first} alone must be legal here, got {Outcome(rig, a)}");
                Assert.True(expected[i, j] == Outcome(rig, b), $"{first} -> {second}: expected {expected[i, j]}, got {Outcome(rig, b)}");
                var applied = rig.B.Actions.Select(x => x.Kind).ToArray();
                Assert.Equal(expected[i, j] == 0 ? new[] { first, second } : new[] { first }, applied);
                foreach (var k in applied)
                    if (StateOf(k) != ConcealKind.None) Assert.True((rig.B.StatesOf(0) & StateOf(k)) != 0, $"{first} -> {second}: {k} not held");
                pairs++;
            }
        Assert.Equal(7 * 7 - 4 * 4, pairs);

        static long ArgOf(ActionKind k) => k switch
        {
            ActionKind.Move => 13, ActionKind.Attack => 1, ActionKind.Retreat => 11, ActionKind.Recruit => 1000, _ => 0,
        };
        static ConcealKind StateOf(ActionKind k) => k switch
        {
            ActionKind.Hide => ConcealKind.Hidden, ActionKind.Camouflage => ConcealKind.Camouflaged, ActionKind.Entrench => ConcealKind.Entrenched,
            _ => ConcealKind.None,
        };
    }

    // ---------------------------------------------------------------- 6. field works: exact, dt-correct, lost on leaving

    /// <summary>
    /// TURN-EXACT PIN of the works (proposal §4.2): Entrench at T; an enemy attack at T + d records the target's works
    /// floor(rate × d ÷ year) = floor(5d / 432) ppm at the proposal's rate (complete in one day): 12 h − 1 ms → 499,999 (below the
    /// cover threshold), 12 h → 500,000 (cover), 24 h − 1 ms → 999,999 (below the obstacle threshold), 24 h → 1,000,000 (complete),
    /// 30 h → 1,000,000 (capped).
    /// </summary>
    [Theory]
    [InlineData(43_199_999L, 499_999L)]
    [InlineData(43_200_000L, 500_000L)]
    [InlineData(86_399_999L, 999_999L)]
    [InlineData(86_400_000L, 1_000_000L)]
    [InlineData(108_000_000L, 1_000_000L)]
    public void Entrench_WorksAtAnEngagementAreExact_AndCrossTheThresholdsAtExactInstants(long d, long works)
    {
        long t0 = W0 + Day, ta = t0 + d;
        var rig = Build(new[] { (t0, 0), (ta, 1) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 13) });
        To(rig, t0);
        Act(rig, 0, WarKinds.ActEntrench, 0, 0);
        To(rig, ta);
        int a = Act(rig, 1, WarKinds.ActAttack, 1, 0);
        Finish(rig, ta + Day);
        Assert.Equal(0, Outcome(rig, a));
        var e = rig.B.Engagements.Single();
        Assert.Equal((works, 12L, false), (e.TargetWorksPpm, e.TargetCell, e.Ambush));
        Assert.Equal(works, rig.B.WorksOf(0, ta));
        Assert.Equal(Math.Min(1_000_000L, (long)((Int128)5 * d / 432)), works);                    // the closed form, by hand
    }

    /// <summary>
    /// dt-CORRECTNESS (Law 3; metamorphic): the works are the exact integral of a per-sim-year rate, so any partition of the elapsed
    /// time into sub-steps (T1's IntegrateExact with its exact carry, as the war world settles across turn boundaries and era bands)
    /// gives the same value as one integration, and a translation of the interval (inside a 3,600-day turn, straddling the band edge
    /// into 1,800-day turns, or in a later band) changes nothing: the value depends on the elapsed time only.
    /// </summary>
    [Fact]
    public void Entrench_WorksArePartitionAndTranslationInvariant()
    {
        long rate = ConcealmentRules.Proposal().WorksRatePpmPerYear;
        var cal = ShippedCalendar();
        long edge = cal.Bands[1].StartMs;                                     // the first band edge (a turn start)
        for (ulong seed = 1; seed <= 40; seed++)
        {
            long dt = 1 + (long)Rng.CounterRng.NextBelow(seed, 7, 0, 0, 0, 0, (ulong)(30 * Hours));
            long one = FieldWorks.WorksAt(1_000, 1_000 + dt, rate);
            int k = 1 + (int)Rng.CounterRng.NextBelow(seed, 8, 0, 0, 0, 0, 9);
            var cuts = new long[k + 1]; cuts[k] = dt;
            for (int i = 1; i < k; i++) cuts[i] = (long)Rng.CounterRng.NextBelow(seed, 9, i, 0, 0, 0, (ulong)dt);
            Array.Sort(cuts);
            var carry = ExactFraction.Zero; long sum = 0;
            for (int i = 0; i < k; i++) sum += RateIntegrator.IntegrateExact(rate, cuts[i + 1] - cuts[i], SimInstant.MsPerYear, ref carry);
            Assert.Equal(Math.Min(ConcealmentRules.WorksFullPpm, sum), one);
            Assert.Equal(one, FieldWorks.WorksAt(edge - dt / 2, edge - dt / 2 + dt, rate));   // straddles the band edge
            Assert.Equal(one, FieldWorks.WorksAt(S(500), S(500) + dt, rate));               // a later band
        }
        Assert.Equal(0, FieldWorks.WorksAt(-1, 1_000_000, rate));             // not entrenching
        Assert.Equal(0, FieldWorks.WorksAt(5, 5, rate));                     // no time elapsed
        Assert.Equal(0, FieldWorks.WorksAt(0, 1_000_000_000, 0));            // no labour rate
        Assert.Equal(ConcealmentRules.WorksFullPpm, FieldWorks.WorksAt(0, long.MaxValue / 2, rate));   // capped, no overflow
    }

    /// <summary>TURN-EXACT PIN: the works are lost AT the move's instant, in log order. (a) An enemy attack ordered before the move at
    /// the same instant records the complete works, and the later move loses them. (b) The move first (12 → 11, next to the enemy
    /// at 10), then the attack at the same instant: 0 works at cell 11.</summary>
    [Fact]
    public void Entrench_WorksAreLostAtTheMovesInstant_InLogOrder()
    {
        long t0 = W0 + Day, t1 = W0 + PulseMs + Day;
        var ra = Build(new[] { (t0, 0), (t1, 0), (t1, 1) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 13) });
        To(ra, t0);
        Act(ra, 0, WarKinds.ActEntrench, 0, 0);
        To(ra, t1);
        int atkA = Act(ra, 1, WarKinds.ActAttack, 1, 0);
        int mvA = Act(ra, 0, WarKinds.ActMove, 0, 11);
        Finish(ra, t1 + Day);
        Assert.Equal((0, 0), (Outcome(ra, atkA), Outcome(ra, mvA)));
        Assert.Equal((ConcealmentRules.WorksFullPpm, 12L), (ra.B.Engagements.Single().TargetWorksPpm, ra.B.Engagements.Single().TargetCell));
        Assert.Equal(0, ra.B.WorksOf(0, t1));

        var rb = Build(new[] { (t0, 0), (t1, 0), (t1, 1) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 10) });
        To(rb, t0);
        Act(rb, 0, WarKinds.ActEntrench, 0, 0);
        To(rb, t1);
        int mvB = Act(rb, 0, WarKinds.ActMove, 0, 11);
        int atkB = Act(rb, 1, WarKinds.ActAttack, 1, 0);
        Finish(rb, t1 + Day);
        Assert.Equal((0, 0), (Outcome(rb, mvB), Outcome(rb, atkB)));
        Assert.Equal((0L, 11L), (rb.B.Engagements.Single().TargetWorksPpm, rb.B.Engagements.Single().TargetCell));
    }

    // ---------------------------------------------------------------- 7. tie-dense: one move spots many

    /// <summary>TIE-DENSE: one enemy move brings FOUR hidden agents of polity 0 (two on each side, at equal distances) inside its
    /// detection distance at once (Forest: 4 × 1/2 = 2). All four are spotted at that instant with the move's log index, and the end
    /// rows come in ascending agent id, consecutive, whatever the agents' positions (two layouts, mirrored). A fifth hidden agent at
    /// distance 3 stays hidden.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Spotting_ManyAgentsAtOneMove_EndInAscendingAgentIdOrder(bool mirrored)
    {
        long t0 = W0 + Day, t1 = W0 + PulseMs + Day, t2 = W0 + 2 * PulseMs + Day, t3 = W0 + 2 * PulseMs + 2 * Day;
        long[] xs = mirrored ? new long[] { 17, 13, 16, 14, 18 } : new long[] { 13, 17, 14, 16, 18 };
        var agents = new WarAgentSpec[6];
        for (int i = 0; i < 5; i++) agents[i] = A(i, 0, 0, xs[i]);
        agents[5] = A(5, 1, 1, 45);
        var rig = Build(new[] { (t0, 0), (t1, 0), (t2, 0), (t3, 1) }, agents);
        To(rig, t0);
        Act(rig, 0, WarKinds.ActHide, 0, 0); Act(rig, 0, WarKinds.ActHide, 1, 0);
        To(rig, t1);
        Act(rig, 0, WarKinds.ActHide, 2, 0); Act(rig, 0, WarKinds.ActHide, 3, 0);
        To(rig, t2);
        Act(rig, 0, WarKinds.ActHide, 4, 0);
        To(rig, t3);
        int m = Act(rig, 1, WarKinds.ActMove, 5, 15);
        Finish(rig, t3 + Day);
        Assert.Equal(0, Outcome(rig, m));
        var ends = rig.B.ConcealmentChanges.Where(c => !c.Begin).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, ends.Select(c => c.AgentId).ToArray());
        Assert.All(ends, c => Assert.Equal((ConcealEnd.Spotted, m, t3), (c.Cause, c.LogIndex, c.AtMs)));
        Assert.Equal(Enumerable.Range(ends[0].Seq, ends.Length).ToArray(), ends.Select(c => c.Seq).ToArray());
        Assert.True(rig.B.IsHidden(4));
    }

    /// <summary>
    /// THE SCOPE OF SPOTTING (proposal §3.2): only ANOTHER polity's accepted Move or Retreat spots a concealed agent. (a) A polity-mate
    /// that moves onto the hidden agent's own cell does not end its hide (a polity always knows its own agents; ending it would make
    /// concealment unusable for any polity with two agents). (b) An enemy RETREAT that ends within the shortened distance spots exactly
    /// like a move: the Scout (sight 3) in contact with a visible agent retreats to 1 cell from a hide in the Forest (3 × 1/2 = 1.5
    /// cells): Spotted at the retreat's instant with its log index; the same retreat ending 2 cells away does not spot.
    /// [AP round 1, AP-R1-03: neither case was tested.]
    /// </summary>
    [Fact]
    public void Spotting_OnlyAnotherPolitysMoveOrRetreatSpots()
    {
        long t0 = W0 + Day, t1 = W0 + PulseMs + Day;
        var own = Build(new[] { (t0, 0), (t1, 0) }, new[] { A(0, 0, 0, 12), A(1, 0, 0, 14), A(2, 1, 1, 50) });
        To(own, t0);
        int h = Act(own, 0, WarKinds.ActHide, 0, 0);
        To(own, t1);
        int m = Act(own, 0, WarKinds.ActMove, 1, 12);
        Finish(own, t1 + Day);
        Assert.Equal((0, 0, 12L), (Outcome(own, h), Outcome(own, m), own.B.PositionOf(1)));
        Assert.True(own.B.IsHidden(0));
        var only = own.B.ConcealmentChanges.Single();                       // the hide's own begin row; no end row
        Assert.Equal((0, ConcealKind.Hidden, true, h), (only.AgentId, only.Kind, only.Begin, only.LogIndex));

        foreach (long to in new long[] { 13, 14 })
        {
            var rig = Build(new[] { (t0, 0), (t1, 1) }, new[] { A(0, 0, 0, 12), A(1, 0, 0, 19), A(2, 1, 2, 18) });
            To(rig, t0);
            int hide = Act(rig, 0, WarKinds.ActHide, 0, 0);
            To(rig, t1);
            int rt = Act(rig, 1, WarKinds.ActRetreat, 2, to);
            Finish(rig, t1 + Day);
            Assert.Equal((0, 0, to), (Outcome(rig, hide), Outcome(rig, rt), rig.B.PositionOf(2)));
            bool spotted = to == 13;
            Assert.Equal(!spotted, rig.B.IsHidden(0));
            Assert.Equal(spotted, rig.B.Detects(1, 0));
            var last = rig.B.ConcealmentChanges[^1];
            if (spotted)
                Assert.Equal((0, ConcealKind.Hidden, false, ConcealEnd.Spotted, rt, t1), (last.AgentId, last.Kind, last.Begin, last.Cause, last.LogIndex, last.AtMs));
            else
                Assert.Equal((0, ConcealKind.Hidden, true, hide), (last.AgentId, last.Kind, last.Begin, last.LogIndex));
        }
    }

    /// <summary>
    /// SPOTTING IS JUDGED FROM THE MOVE'S END POSITION (proposal §3.2: every concealed agent that the mover's polity "now detects"):
    /// T3's Move is instantaneous, so the mover has no position between its old cell and its new one. The Raider hides in the Jungle
    /// at 22 (Watcher sight 4 × 1/4 = 1 cell); the Watcher moves from 40 to d: ending at 21, 22 or 23 spots it, ending at 24 does not,
    /// and a march straight THROUGH the hidden agent's cell to 15 or 10 does not. This pins the documented rule together with its
    /// limitation (proposal §9; sweeping the cells passed is a PROPOSAL alternative, not implemented). [AP round 1, AP-R1-04]
    /// </summary>
    [Theory]
    [InlineData(21L, true)]
    [InlineData(22L, true)]
    [InlineData(23L, true)]
    [InlineData(24L, false)]
    [InlineData(15L, false)]
    [InlineData(10L, false)]
    public void Spotting_IsJudgedFromTheMovesEndPosition_APassThroughDoesNotSpot(long to, bool spotted)
    {
        long t0 = W0 + Day, t1 = W0 + 2 * Day;
        var rig = Build(new[] { (t0, 0), (t1, 1) }, new[] { A(0, 0, 0, 22), A(1, 1, 1, 40) });
        To(rig, t0);
        int h = Act(rig, 0, WarKinds.ActHide, 0, 0);
        To(rig, t1);
        int m = Act(rig, 1, WarKinds.ActMove, 1, to);
        Finish(rig, t1 + Day);
        Assert.Equal((0, 0, to), (Outcome(rig, h), Outcome(rig, m), rig.B.PositionOf(1)));
        Assert.Equal(!spotted, rig.B.IsHidden(0));
        Assert.Equal(spotted, rig.B.Detects(1, 0));
        Assert.Equal(spotted ? 2 : 1, rig.B.ConcealmentChanges.Count);
    }

    // ---------------------------------------------------------------- 8. no stock moves; replay; golden

    /// <summary>CONSERVATION: hide, camouflage and entrench move no stock (labour is the agent's own time, already in its upkeep; no
    /// material is drawn). Every ledger account is unchanged by them, exactly; a Recruit in the same run moves exactly its amount
    /// (the control).</summary>
    [Fact]
    public void ConcealmentActions_MoveNoStock_ExactLedgerEquality()
    {
        long t0 = W0 + Day, t1 = W0 + PulseMs + Day;
        var rig = Build(new[] { (t0, 0), (t1, 0) }, new[] { A(0, 0, 0, 12), A(1, 0, 0, 14), A(2, 1, 1, 50) });
        var before = rig.W.Ledger.Snapshot();
        To(rig, t0);
        Act(rig, 0, WarKinds.ActHide, 0, 0); Act(rig, 0, WarKinds.ActEntrench, 1, 0);
        To(rig, t1);
        Act(rig, 0, WarKinds.ActCamouflage, 0, 0); Act(rig, 0, WarKinds.ActRecruit, 1, 1_234);
        Finish(rig, t1 + Day);
        var after = rig.W.Ledger.Snapshot();
        Assert.Equal(4, rig.B.Actions.Count);
        for (int i = 0; i < before.Length; i++)
            if (i != rig.W.TreasuryOf(0) && i != rig.W.RecruitSink) Assert.Equal(before[i], after[i]);
        Assert.Equal(before[rig.W.TreasuryOf(0)] - 1_234, after[rig.W.TreasuryOf(0)]);
        Assert.Equal(before[rig.W.RecruitSink] + 1_234, after[rig.W.RecruitSink]);
        Assert.Equal((Int128)Money * 3, rig.W.Ledger.Total(Commodity.Money));
        Assert.Equal((Int128)Money * 3, rig.W.Ledger.Total(Commodity.Goods));
    }

    private static Rig Scripted(bool stops, SubTurnOrderLog? replay)
    {
        long t0 = W0 + Day, tm = W0 + Day + Hours, t1 = W0 + PulseMs + Day, t1b = t1 + Hours, t2 = W0 + 2 * PulseMs + Day;
        var rig = Build(new[] { (t0, 0), (tm, 1), (t1, 0), (t1b, 1), (t2, 0) },
                        new[] { A(0, 0, 0, 22), A(1, 0, 0, 12), A(2, 1, 3, 40), A(3, 1, 1, 50) }, stops: stops, replayLog: replay);
        long limit = t2 + Day;
        if (replay is not null || !stops) { rig.R.RunUntil(limit); return rig; }
        int n = 0;
        while (rig.R.RunUntil(limit).Status == RunStatus.Interrupted)
        {
            n++;
            if (n == 1) { Act(rig, 0, WarKinds.ActHide, 0, 0); Act(rig, 0, WarKinds.ActEntrench, 1, 0); }
            if (n == 2) { Act(rig, 1, WarKinds.ActMove, 2, 23); Act(rig, 1, WarKinds.ActMove, 3, 18); }   // 2 is not spotted (3 x 1/4 < 1)
            if (n == 3) { Act(rig, 0, WarKinds.ActAttack, 0, 2); Act(rig, 0, WarKinds.ActCamouflage, 1, 0); }   // the ambush; 18 is 6 > 4 away
            if (n == 4) { Act(rig, 1, WarKinds.ActMove, 3, 13); Act(rig, 1, WarKinds.ActAttack, 2, 0); }    // spots 1 (1 <= 4 x 1/2); a visible attack
            if (n == 5) { Act(rig, 0, WarKinds.ActMove, 1, 11); Act(rig, 0, WarKinds.ActHide, 0, 0); }      // abandons the works; re-hide in contact
        }
        return rig;
    }

    /// <summary>REPLAY AND GOLDEN: a scripted run (hide, entrench, an undetected approach, an ambush, a camouflage, a spotting move, a
    /// visible attack, a move that abandons works, a refused re-hide in contact) is reproduced by replay (book, world state, trace), and
    /// its book hash is pinned (first pin, lane AP) with the semantic content asserted: the engagements and every change.</summary>
    [Fact]
    public void Replay_ReproducesTheConcealmentLayer_AndTheGoldenRun()
    {
        var live = Scripted(stops: true, replay: null);
        var replay = Scripted(stops: false, replay: live.R.Orders);
        Assert.Equal(live.B.HashHex(), replay.B.HashHex());
        Assert.Equal(live.W.StateHashHex(), replay.W.StateHashHex());
        Assert.Equal(live.S.Trace.HashHex(), replay.S.Trace.HashHex());
        Assert.Equal(View(live), View(replay));
        var e = live.B.Engagements;
        Assert.Equal(new[] { (0, 2, true, ConcealKind.Hidden, 0L), (2, 0, false, ConcealKind.None, 0L) },
                     e.Select(x => (x.Attacker, x.Target, x.Ambush, x.AttackerStates, x.TargetWorksPpm)).ToArray());
        var ch = live.B.ConcealmentChanges.Select(c => $"{c.AgentId}{c.Kind}{(c.Begin ? "+" : "-")}{c.Cause}").ToArray();
        _out.WriteLine(string.Join(" ", ch));
        Assert.Equal(new[]
        {
            "0Hidden+None", "1Entrenched+None",            // window 1
            "0Hidden-Attacked", "1Camouflaged+None",       // window 3: the ambush ends the hide
            "1Camouflaged-Spotted",                        // window 4: the Watcher closes to 13 (1 <= 4 x 1/2)
            "1Entrenched-Moved",                           // window 5: the move abandons the works
        }, ch);
        Assert.Equal(WarRefusal.EnemyInContact, live.W.Refusals.Single().Reason);   // window 5: the re-hide in contact
        _out.WriteLine($"AP golden: book {live.B.HashHex()} state {live.W.StateHashHex()}");
        Assert.Equal(BookGolden, live.B.HashHex());
        Assert.Equal(StateGolden, live.W.StateHashHex());
    }

    /// <summary>First pins (lane AP, measured on Linux x64 .NET 10; no earlier value): the book and world state hashes of
    /// <see cref="Replay_ReproducesTheConcealmentLayer_AndTheGoldenRun"/>. The semantic assertions above carry the meaning; the digests
    /// pin reproducibility only. Cross-platform equality is UNTESTED (CR-013).</summary>
    internal const string BookGolden = "165388CCFF1202910BE62C92811A0B1ACF27DDBDA9B66BC6C860F04FCF1117F5";
    internal const string StateGolden = "38DCB06F4FC0DD95B68D4E318CC53C08F98330548E982E8A4651CADC467C5B17";

    /// <summary>IDENTITY: a profile set that enables none of the lane-AP kinds appends nothing to the book hash, so T3's own worlds hash
    /// as before (T3's golden passes unchanged); enabling a kind, with no action ever taken, changes only the hash input (teeth), not the
    /// layer's state.</summary>
    [Fact]
    public void WithoutTheNewKinds_TheBookHashesAsT3_AndTheLayerStateIsTheSame()
    {
        long t0 = W0 + Day;
        var t3 = Profiles.Select(p => p with { ActionMask = p.ActionMask & 0b1111 }).ToArray();
        var a = Build(new[] { (t0, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50) }, profiles: t3);
        var b = Build(new[] { (t0, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50) }, profiles: Profiles);
        Finish(a, t0 + Day); Finish(b, t0 + Day);
        Assert.NotEqual(a.B.HashHex(), b.B.HashHex());
        Assert.Equal(View(a), View(b));
    }

    // ---------------------------------------------------------------- 9. configuration and validation

    /// <summary>
    /// CONFIGURED RULES (proposal §2.1; ActionLayerSpec.ConcealmentRules): the book uses the rules it is given. With a camouflage factor
    /// of 1/4 (TEST INPUT) the camouflaged agent in the open has the factor 1/4, so the Watcher (sight 4) closing to 2 cells does not
    /// find it (2 > 4 × 1/4); with the proposal's 1/2 the same move spots it (2 ≤ 4 × 1/2). ConcealmentRules.Validate refuses a
    /// camouflage factor outside (0, 1], a negative works rate and thresholds outside 1 ≤ cover ≤ obstacle ≤ complete works (the
    /// bounds themselves are accepted), and the book validates the rules it is given. A profile with a negative Hide, Camouflage or
    /// Entrench cost (it would ADD capacity: remaining −= cost) or a negative sight is refused when the book is built; 0 is accepted.
    /// [AP round 1, AP-R1-08: no test exercised these paths.]
    /// </summary>
    [Fact]
    public void ConfiguredRules_AreTheBooks_AndInvalidRulesOrProfilesAreRefused()
    {
        long t0 = W0 + Day, t1 = W0 + PulseMs + Day;
        foreach (bool quarter in new[] { true, false })
        {
            var rules = quarter ? new ConcealmentRules { CamouflageFactor = Q32.FromRatio(1, 4) } : null;
            var rig = Build(new[] { (t0, 0), (t1, 1) }, new[] { A(0, 0, 0, 5), A(1, 1, 1, 20) }, rules: rules);
            To(rig, t0);
            int c = Act(rig, 0, WarKinds.ActCamouflage, 0, 0);
            To(rig, t1);
            Assert.Equal(quarter ? Q32.FromRatio(1, 4) : Q32.FromRatio(1, 2), rig.B.ConcealmentFactor(0));
            Assert.Equal(quarter ? Q32.FromRatio(1, 4) : ConcealmentRules.Proposal().CamouflageFactor, rig.B.ConcealmentRules.CamouflageFactor);
            int m = Act(rig, 1, WarKinds.ActMove, 1, 7);
            Finish(rig, t1 + Day);
            Assert.Equal((0, 0), (Outcome(rig, c), Outcome(rig, m)));
            Assert.Equal(quarter, rig.B.IsCamouflaged(0));
            Assert.Equal(!quarter, rig.B.Detects(1, 0));
        }

        static void Bad(ConcealmentRules r) => Assert.Throws<ArgumentOutOfRangeException>(() => r.Validate());
        Bad(new ConcealmentRules { CamouflageFactor = Q32.Zero });
        Bad(new ConcealmentRules { CamouflageFactor = new Q32(-1) });
        Bad(new ConcealmentRules { CamouflageFactor = new Q32(Q32.OneRaw + 1) });
        Bad(new ConcealmentRules { WorksRatePpmPerYear = -1 });
        Bad(new ConcealmentRules { CoverThresholdPpm = 0 });
        Bad(new ConcealmentRules { CoverThresholdPpm = 600_000, ObstacleThresholdPpm = 599_999 });
        Bad(new ConcealmentRules { CoverThresholdPpm = ConcealmentRules.WorksFullPpm + 1, ObstacleThresholdPpm = ConcealmentRules.WorksFullPpm + 1 });
        Bad(new ConcealmentRules { ObstacleThresholdPpm = ConcealmentRules.WorksFullPpm + 1 });
        new ConcealmentRules { CamouflageFactor = new Q32(1), WorksRatePpmPerYear = 0, CoverThresholdPpm = 1, ObstacleThresholdPpm = 1 }.Validate();
        new ConcealmentRules { CamouflageFactor = Q32.One, CoverThresholdPpm = ConcealmentRules.WorksFullPpm }.Validate();
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(new[] { (t0, 0) }, new[] { A(0, 0, 0, 5), A(1, 1, 1, 50) },
                                                               rules: new ConcealmentRules { CamouflageFactor = Q32.Zero }));

        var agents = new[] { A(0, 0, 0, 5), A(1, 1, 1, 50) };
        foreach (var bad in new Func<AgentProfile, AgentProfile>[]
                 { p => p with { HideCost = -1 }, p => p with { CamouflageCost = -1 }, p => p with { EntrenchCost = -1 }, p => p with { SightCells = -1 } })
            Assert.Throws<ArgumentException>(() => Build(new[] { (t0, 0) }, agents, profiles: Profiles.Select(p => p.Id == 0 ? bad(p) : p).ToArray()));
        var zero = Build(new[] { (t0, 0) }, agents,
                         profiles: Profiles.Select(p => p.Id == 0 ? p with { HideCost = 0, CamouflageCost = 0, EntrenchCost = 0, SightCells = 0 } : p).ToArray());
        Finish(zero, t0 + Day);
        Assert.Equal(10L, zero.B.RemainingOf(0));
    }

    /// <summary>
    /// THE BOOK HASH COVERS THE ENGAGEMENT LOG, AND THE CONFIGURED WORKS RATE REACHES IT: two worlds that differ only in
    /// ConcealmentRules.WorksRatePpmPerYear (the proposal's 360,000,000 ppm per year = complete in one day, and 36,000,000 = ten days,
    /// TEST INPUT) run the same orders, an Entrench and an enemy attack one day later. Every action, refresh, state and change row is the
    /// same; the engagement records differ only in the target's works (1,000,000 against floor(36,000,000 × 1 day ÷ 360 days) = 100,000
    /// ppm), and so do the book hashes: the hash is the action layer's replay-divergence check and must see every engagement field.
    /// [AP round 1, AP-R1-08 and AP-R1-13: the configured works rate and the engagement log in the hash were pinned only by the golden.]
    /// </summary>
    [Fact]
    public void BookHash_CoversTheEngagementLog_AndTheConfiguredWorksRateReachesIt()
    {
        long t0 = W0 + Day, ta = t0 + Day;
        Rig Run(ConcealmentRules? rules)
        {
            var rig = Build(new[] { (t0, 0), (ta, 1) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 13) }, rules: rules);
            To(rig, t0);
            Act(rig, 0, WarKinds.ActEntrench, 0, 0);
            To(rig, ta);
            Act(rig, 1, WarKinds.ActAttack, 1, 0);
            Finish(rig, ta + Day);
            return rig;
        }
        var day = Run(null);
        var tenDays = Run(new ConcealmentRules { WorksRatePpmPerYear = 36_000_000 });
        Assert.Equal((ConcealmentRules.WorksFullPpm, 100_000L), (day.B.Engagements.Single().TargetWorksPpm, tenDays.B.Engagements.Single().TargetWorksPpm));
        Assert.Equal((ConcealmentRules.WorksFullPpm, 100_000L), (day.B.WorksOf(0, ta), tenDays.B.WorksOf(0, ta)));
        Assert.Equal(day.B.Engagements.Single() with { TargetWorksPpm = 0 }, tenDays.B.Engagements.Single() with { TargetWorksPpm = 0 });
        Assert.Equal(day.B.Actions, tenDays.B.Actions);
        Assert.Equal(day.B.Refreshes, tenDays.B.Refreshes);
        Assert.Equal(day.B.ConcealmentChanges, tenDays.B.ConcealmentChanges);
        Assert.Equal(day.B.StateRows(), tenDays.B.StateRows());
        Assert.NotEqual(day.B.HashHex(), tenDays.B.HashHex());
    }
}
