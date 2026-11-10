using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.War;
using Xunit.Abstractions;
using static Proto.M7.Tests.Concealment.ConcealmentActionTests;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Concealment;

/// <summary>
/// [AP round 2] Lane AP, verification round 2 (docs/m7-plan/concealment-actions-proposal.md §3.6, §3.7):
///   AP-R2-01 — what an order reveals: no order's outcome (accepted, or its refusal code) depends on an agent the issuer's polity does
///              not detect; the one documented consequence is PAID (an accepted Hide or Camouflage that an unseen enemy detects is
///              spotted at once, its cost spent). Metamorphic leak gate with a positive control (ADR-037 §4's precedent).
///   AP-R2-04 — who is an enemy: only a polity that shares a war with the agent's polity spots, watches or is in contact with it.
///   AP-R2-02 — a hide uses the concealment of its OWN cell (and NoCover / CannotDig read the agent's own cell) at every band edge.
///   AP-R2-03 — EnemyInContact is the hider's own attack range: refused at the range, accepted one cell beyond it.
/// Line terrain as ConcealmentActionTests: 0-9 Open, 10-19 Forest (1/2), 20-29 Jungle (1/4), 30-34 Marsh, 35-39 Water, 40-59 Open.
/// </summary>
public class ConcealmentKnowledgeTests
{
    private readonly ITestOutputHelper _out;
    public ConcealmentKnowledgeTests(ITestOutputHelper output) => _out = output;

    /// <summary>Profiles 0-3 as ConcealmentActionTests, plus (TEST INPUTS): 4 Prober (every kind, capacity 60, sight 3, range 1, the
    /// Raider's costs: a Jungle hide one cell away stays unseen, 1 &gt; 3 × 1/4); 5 Dim Watcher (Move/Attack/Retreat, sight 1);
    /// 6 Lookout (Move/Attack/Retreat, capacity 60, sight 16).</summary>
    internal static readonly AgentProfile[] Profs = Profiles.Concat(new[]
    {
        Profiles[0] with { Id = 4, Capacity = 60, SightCells = 3 },
        Profiles[1] with { Id = 5, SightCells = 1 },
        Profiles[1] with { Id = 6, Capacity = 60, SightCells = 16 },
    }).ToArray();

    private static AgentProfile[] WithProberSight(long sight) => Profs.Select(p => p.Id == 4 ? p with { SightCells = sight } : p).ToArray();

    private static readonly long T0 = W0 + Day, T1 = W0 + PulseMs + Day, T2 = W0 + 2 * PulseMs + Day;

    // ---------------------------------------------------------------- AP-R2-01: the knowledge-leak gate

    /// <summary>The gate's world: polity 1's Raider U (agent 0, sight 6) at <paramref name="uCell"/>, hiding there at T0 when
    /// <paramref name="uHides"/>; polity 0's Prober (agent 1) at 45 marches to <paramref name="proberCell"/> at T1 (a fresh window, so
    /// the probe order at T2 has both slots and full capacity); optionally polity 1's Watcher D (agent 2) at 24, which the Prober sees.</summary>
    private static Rig Gate(long uCell, bool uHides, long proberCell, long proberSight, bool withD)
    {
        var agents = withD ? new[] { A(0, 1, 0, uCell), A(1, 0, 4, 45), A(2, 1, 1, 24) } : new[] { A(0, 1, 0, uCell), A(1, 0, 4, 45) };
        var rig = Build(new[] { (T0, 1), (T1, 0), (T2, 0) }, agents, profiles: WithProberSight(proberSight));
        To(rig, T0);
        int h = uHides ? Act(rig, 1, WarKinds.ActHide, 0, 0) : -1;
        To(rig, T1);
        int m = Act(rig, 0, WarKinds.ActMove, 1, proberCell);
        To(rig, T2);                                                              // orders execute as the run proceeds
        if (uHides) Assert.Equal(0, Outcome(rig, h));
        Assert.Equal((0, proberCell), (Outcome(rig, m), rig.B.PositionOf(1)));
        return rig;
    }

    /// <summary>
    /// KNOWLEDGE-LEAK GATE (metamorphic on hidden state; ADR-037 §4's precedent; proposal §3.6). The Prober issues one probe order at
    /// T2; the worlds differ ONLY in where polity 1's U stands and whether it hides — four placements, in every one of which polity 0
    /// does not detect U at T2 (asserted): hidden in the Jungle next to the Prober (in its attack range, and it watches the Prober:
    /// 1 ≤ 6), hidden in the Forest far away, unconcealed 4 cells away (it watches the Prober, 4 ≤ 6; unseen, 4 &gt; 3), unconcealed
    /// far away. INVARIANT: the outcome code, the capacity and slots spent, the Prober's cell and its retreat records are identical in
    /// all four. Refusals cost nothing, so before round 2 each difference was a FREE probe: a Hide or Camouflage refused
    /// EnemyInContact or Observed only because of U, a Retreat accepted only because of U (and refused toward it), MEASURED on 16efc0a
    /// (H-AP §12). The ONE documented difference is PAID: an accepted Hide or Camouflage that U detects with its new concealment
    /// factor (1 ≤ 6 × 1/4; 1 ≤ 6 × 1/2) is spotted at once — begun and ended (Spotted) by the order's own log index at T2, its cost
    /// spent — and is held in every other placement (4 &gt; 6 × 1/4, 4 &gt; 6 × 1/2). POSITIVE CONTROL (teeth): with the Prober's
    /// sight unlimited, U is detected (its own hide is spotted at once at T0) and the same two placements (next / far) give the stated
    /// different outcomes where the order depends on U, so the probes are sensitive.
    /// </summary>
    [Theory]
    [InlineData("Hide", WarKinds.ActHide, 0L, 23L, false, WarRefusal.EnemyInContact, 0)]
    [InlineData("Camouflage", WarKinds.ActCamouflage, 0L, 23L, false, WarRefusal.EnemyInContact, 0)]
    [InlineData("Hide watched", WarKinds.ActHide, 0L, 26L, false, WarRefusal.Observed, 0)]
    [InlineData("Camouflage watched", WarKinds.ActCamouflage, 0L, 26L, false, WarRefusal.Observed, 0)]
    [InlineData("Entrench", WarKinds.ActEntrench, 0L, 23L, false, 0, 0)]
    [InlineData("Retreat away", WarKinds.ActRetreat, 25L, 23L, false, 0, WarRefusal.OutOfReach)]
    [InlineData("Retreat toward, seen D", WarKinds.ActRetreat, 21L, 23L, true, WarRefusal.OutOfReach, 0)]
    [InlineData("Attack U", WarKinds.ActAttack, 0L, 23L, false, 0, WarRefusal.OutOfReach)]
    [InlineData("Move", WarKinds.ActMove, 24L, 23L, false, 0, 0)]
    [InlineData("Recruit", WarKinds.ActRecruit, 100L, 23L, false, 0, 0)]
    public void KnowledgeLeak_NoOrderOutcomeDependsOnAnEnemyTheIssuerDoesNotDetect(string name, int kind, long arg, long proberCell, bool withD,
                                                                                  int controlNext, int controlFar)
    {
        var placements = new (string Name, long Cell, bool Hides, bool SpotsTheNewConcealment)[]
        {
            ("hidden, next", 22, true, proberCell == 23), ("hidden, far", 12, true, false),
            ("open, 4 cells", proberCell + 4, false, false), ("open, far", 50, false, false),
        };
        var seen = new List<string>();
        foreach (var pl in placements)
        {
            var rig = Gate(pl.Cell, pl.Hides, proberCell, 3, withD);
            Assert.False(rig.B.Detects(0, 0));                                   // the gate's premise: U is unseen at T2
            Assert.Equal(pl.Hides, rig.B.IsHidden(0));
            int o = Act(rig, 0, kind, 1, arg);
            Finish(rig, T2 + Day);
            int code = Outcome(rig, o);
            var disengaged = string.Join(",", rig.B.Disengagements.Select(d => $"{d.AgentId}>{d.EnemyId}"));
            seen.Add($"{code}|{rig.B.RemainingOf(1)}|{rig.B.SlotsUsed(0, 0)}|{rig.B.PositionOf(1)}|{disengaged}");
            var ends = rig.B.ConcealmentChanges.Where(c => c.AgentId == 1 && !c.Begin).ToArray();
            if (kind is WarKinds.ActHide or WarKinds.ActCamouflage)
            {
                Assert.Equal(0, code);
                if (pl.SpotsTheNewConcealment)
                {
                    var e = Assert.Single(ends);                                  // the documented PAID channel
                    Assert.Equal((ConcealEnd.Spotted, o, T2), (e.Cause, e.LogIndex, e.AtMs));
                    Assert.Equal(ConcealKind.None, rig.B.StatesOf(1));
                }
                else
                {
                    Assert.Empty(ends);
                    Assert.NotEqual(ConcealKind.None, rig.B.StatesOf(1));
                }
            }
            else Assert.Empty(ends);
            _out.WriteLine($"{name} [{pl.Name}]: outcome {code}, capacity {rig.B.RemainingOf(1)}, states {rig.B.StatesOf(1)}");
        }
        Assert.Single(seen.Distinct());                                          // THE INVARIANT: identical in every placement

        // positive control: the same order where polity 0 sees U (the Prober's sight unlimited), U next to it / far away
        var next = Gate(22, true, proberCell, long.MaxValue, withD);
        var far = Gate(12, true, proberCell, long.MaxValue, withD);
        Assert.True(next.B.Detects(0, 0) && far.B.Detects(0, 0));
        Assert.False(next.B.IsHidden(0));                                        // U's own hide was spotted at once at T0
        int on = Act(next, 0, kind, 1, arg), of = Act(far, 0, kind, 1, arg);
        Finish(next, T2 + Day); Finish(far, T2 + Day);
        Assert.Equal((controlNext, controlFar), (Outcome(next, on), Outcome(far, of)));
    }

    /// <summary>
    /// KNOWLEDGE-LEAK GATE, the hit-and-run record (AP-R2-01; found by this round's gate, MEASURED on 16efc0a: refused HitAndRun 15 or
    /// TargetUndetected 18 depending only on an unseen enemy's movement). R (polity 0, sight 3) retreats from E (a Watcher) at T1; E
    /// marches out of R's sight (10 cells); at T2 E either CLOSES to 5 cells — still unseen, but T3 lifts R's hit-and-run record when
    /// the enemy closes on it — or WITHDRAWS to 15 cells. R then orders an Attack on E within the cooldown. The detection check now
    /// comes first, so both worlds refuse TargetUndetected: the order says nothing of the unseen agent's movement. Control (R's sight
    /// unlimited, E seen): OutOfReach 17 after the approach (the record lifted, 5 &gt; 1) and HitAndRun 15 after the withdrawal.
    /// </summary>
    [Fact]
    public void KnowledgeLeak_AnAttackOnAnUnseenAgent_SaysNothingOfItsMovements()
    {
        long t1b = T1 + Hours, t2b = T2 + Hours;
        (int Attack, bool Lifted, bool Seen) Run(bool closes, long sight)
        {
            var rig = Build(new[] { (T1, 0), (t1b, 1), (T2, 1), (t2b, 0) }, new[] { A(0, 0, 4, 12), A(1, 1, 1, 13) }, profiles: WithProberSight(sight));
            To(rig, T1);
            int r = Act(rig, 0, WarKinds.ActRetreat, 0, 10);
            To(rig, t1b);
            int m1 = Act(rig, 1, WarKinds.ActMove, 1, 20);
            To(rig, T2);
            int m2 = Act(rig, 1, WarKinds.ActMove, 1, closes ? 15 : 25);
            To(rig, t2b);
            bool seen = rig.B.Detects(0, 1);
            int a = Act(rig, 0, WarKinds.ActAttack, 0, 1);
            Finish(rig, t2b + Day);
            Assert.Equal((0, 0, 0, closes ? 15L : 25L), (Outcome(rig, r), Outcome(rig, m1), Outcome(rig, m2), rig.B.PositionOf(1)));
            return (Outcome(rig, a), rig.B.Disengagements.Single().Lifted, seen);
        }
        Assert.Equal((WarRefusal.TargetUndetected, true, false), Run(closes: true, sight: 3));
        Assert.Equal((WarRefusal.TargetUndetected, false, false), Run(closes: false, sight: 3));    // the same code: no leak
        Assert.Equal((WarRefusal.OutOfReach, true, true), Run(closes: true, sight: long.MaxValue));
        Assert.Equal((WarRefusal.HitAndRun, false, true), Run(closes: false, sight: long.MaxValue)); // control: seen, so it may differ
    }

    /// <summary>
    /// RETREAT CONTACTS ARE THE ONES ITS POLITY SEES (AP-R2-01; T3's rule "away from EVERY contact" read on the detected contacts): the
    /// Prober at 23 has a seen Watcher D at 24 and an unseen hidden Raider U at 22. It retreats to 21 — away from D, toward U: accepted
    /// (before round 2: refused, MEASURED on 16efc0a), and the hit-and-run record names D only, so a later attack on U is never
    /// refused HitAndRun for a retreat from an enemy it could not see. Ending next to U does not spot it (1 &gt; 3 × 1/4).
    /// </summary>
    [Fact]
    public void Retreat_ContactsAndDisengagementRecords_AreTheEnemiesItsPolitySees()
    {
        var rig = Gate(22, true, 23, 3, withD: true);
        Assert.Equal((false, true), (rig.B.Detects(0, 0), rig.B.Detects(0, 2)));
        int r = Act(rig, 0, WarKinds.ActRetreat, 1, 21);
        Finish(rig, T2 + Day);
        Assert.Equal(0, Outcome(rig, r));
        Assert.Equal(21, rig.B.PositionOf(1));
        var d = Assert.Single(rig.B.Disengagements);
        Assert.Equal((1, 2), (d.AgentId, d.EnemyId));
        Assert.True(rig.B.IsHidden(0));
    }

    /// <summary>
    /// SAME-INSTANT PIN of the round-2 delivery rule (proposal §3.6): a Hide or Camouflage takes effect AT its instant T, in log order,
    /// and is judged against the world as it stands then. The Prober (sight 3) at 26 conceals itself at T; at the same T polity 1's
    /// Lookout (sight 16) marches from 50 to <paramref name="dest"/>, logged BEFORE or AFTER the order.
    ///   • dest 29 (3 cells: the Prober SEES it): Lookout first → refused Observed (a seen watcher; nothing spent); order first → held,
    ///     then the Lookout's move spots it (the MOVE's log index).
    ///   • the boundary cell (4 cells: hidden 16 × 1/4 = 4; camouflaged 16 × 1/2 = 8 cells): Lookout first → accepted, the cost spent,
    ///     and SPOTTED AT ONCE by the order's OWN log index at T (an unseen watcher); order first → held, then the move spots it.
    ///   • one cell beyond the boundary: held in both orders (nothing detects it).
    /// Every end row carries T. Replay equality alone could not tell these apart; it is asserted too (the run replayed from its order
    /// log without stops gives the same book, world state, trace and change rows).
    /// </summary>
    [Theory]
    [InlineData(WarKinds.ActHide, 29L, true, WarRefusal.Observed, "none")]
    [InlineData(WarKinds.ActHide, 29L, false, 0, "move")]
    [InlineData(WarKinds.ActHide, 30L, true, 0, "order")]
    [InlineData(WarKinds.ActHide, 30L, false, 0, "move")]
    [InlineData(WarKinds.ActHide, 31L, true, 0, "none")]
    [InlineData(WarKinds.ActHide, 31L, false, 0, "none")]
    [InlineData(WarKinds.ActCamouflage, 29L, true, WarRefusal.Observed, "none")]
    [InlineData(WarKinds.ActCamouflage, 29L, false, 0, "move")]
    [InlineData(WarKinds.ActCamouflage, 34L, true, 0, "order")]
    [InlineData(WarKinds.ActCamouflage, 34L, false, 0, "move")]
    [InlineData(WarKinds.ActCamouflage, 35L, true, 0, "none")]
    [InlineData(WarKinds.ActCamouflage, 35L, false, 0, "none")]
    public void SpottedAtOnce_AConcealmentIsJudgedAtItsOwnInstant_InLogOrder(int kind, long dest, bool lookoutFirst, int expected, string endedBy)
    {
        long t = T0;
        var rig = Build(new[] { (t, 0), (t, 1) }, new[] { A(0, 0, 4, 26), A(1, 1, 6, 50) }, profiles: Profs);
        To(rig, t);
        int o, m;
        if (lookoutFirst) { m = Act(rig, 1, WarKinds.ActMove, 1, dest); o = Act(rig, 0, kind, 0, 0); }
        else { o = Act(rig, 0, kind, 0, 0); m = Act(rig, 1, WarKinds.ActMove, 1, dest); }
        Finish(rig, t + Day);
        Assert.Equal(0, Outcome(rig, m));
        Assert.Equal(expected, Outcome(rig, o));
        long cost = kind == WarKinds.ActHide ? 2 : 3;
        Assert.Equal(expected == 0 ? 60 - cost : 60, rig.B.RemainingOf(0));
        var ends = rig.B.ConcealmentChanges.Where(c => !c.Begin).ToArray();
        switch (endedBy)
        {
            case "none":
                Assert.Empty(ends);
                Assert.Equal(expected == 0, rig.B.StatesOf(0) != ConcealKind.None);
                break;
            default:
                var e = Assert.Single(ends);
                Assert.Equal((0, ConcealEnd.Spotted, endedBy == "order" ? o : m, t), (e.AgentId, e.Cause, e.LogIndex, e.AtMs));
                Assert.Equal(ConcealKind.None, rig.B.StatesOf(0));
                break;
        }
        Assert.Equal(dest <= (kind == WarKinds.ActHide ? 30 : 34) || expected != 0, rig.B.Detects(1, 0));

        // replay from the order log, without stops: the same book, world state, trace and change rows (the at-once spotting is a pure
        // function of the state at T in log order)
        var replay = Build(new[] { (t, 0), (t, 1) }, new[] { A(0, 0, 4, 26), A(1, 1, 6, 50) }, profiles: Profs, stops: false, replayLog: rig.R.Orders);
        replay.R.RunUntil(t + Day);
        Assert.Equal((rig.B.HashHex(), rig.W.StateHashHex(), rig.S.Trace.HashHex()), (replay.B.HashHex(), replay.W.StateHashHex(), replay.S.Trace.HashHex()));
        Assert.Equal(rig.B.ConcealmentChanges, replay.B.ConcealmentChanges);
    }

    // ---------------------------------------------------------------- AP-R2-04: the enemy relation

    /// <summary>
    /// WHO IS AN ENEMY (AP-R2-04; proposal §3.7, PROPOSAL): only a polity that shares a war with the agent's polity spots, watches or is
    /// in contact with its concealment. Three polities; war 0 is between 0 and 1; in the second world a war 1 between 0 and 2 is added.
    /// (a) Polity 2's Watcher (a campaign agent, war −1) moves next to polity 0's Forest hide: with no war between them the hide
    /// holds (polity 2 detects it, polity 1 does not — the ambush against polity 1 survives); with war 1 the same move spots it.
    /// (b) Polity 2's Watcher in contact (13) or watching (15: 3 ≤ 4) does not refuse polity 0's hide, and a hide that polity 2
    /// detects with the new factor (13: 1 ≤ 4 × 1/2) is not spotted at once; with war 1 they are refused EnemyInContact and Observed.
    /// (c) Control: polity 1's Watcher (war 0) moving next to the hide spots it. Before round 2 every other polity counted (MEASURED on
    /// 16efc0a: the non-belligerent's move spotted the hide; its contact and its watching refused the order).
    /// </summary>
    [Fact]
    public void OnlyAPolityAtWarWithTheAgentsPolity_SpotsWatchesOrContactsIt()
    {
        var oneWar = new[] { War0() };
        var twoWars = new[] { War0(), new WarSpec(1, W0, W0 + 40 * PulseMs, new[] { 0, 2 }, 0, 0, new long[] { 0, 0 }) };
        var probe = Build(new[] { (T0, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50) }, wars: oneWar);
        var probe2 = Build(new[] { (T0, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50) }, wars: twoWars);
        Assert.Equal((true, true, false, false, false, false), (probe.B.AreEnemies(0, 1), probe.B.AreEnemies(1, 0), probe.B.AreEnemies(0, 2),
                                                              probe.B.AreEnemies(2, 0), probe.B.AreEnemies(1, 2), probe.B.AreEnemies(0, 0)));
        Assert.Equal((true, true, false, false), (probe2.B.AreEnemies(0, 2), probe2.B.AreEnemies(2, 0), probe2.B.AreEnemies(1, 2), probe2.B.AreEnemies(2, 2)));

        foreach (bool war1 in new[] { false, true })
        {
            // (a) a third polity's move next to the hide
            var rig = Build(new[] { (T0, 0), (T1, 2) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50), A(2, 2, 1, 40, war: -1) }, wars: war1 ? twoWars : oneWar);
            To(rig, T0);
            int h = Act(rig, 0, WarKinds.ActHide, 0, 0);
            To(rig, T1);
            int m = Act(rig, 2, WarKinds.ActMove, 2, 13);
            Finish(rig, T1 + Day);
            Assert.Equal((0, 0), (Outcome(rig, h), Outcome(rig, m)));
            Assert.Equal((true, false), (rig.B.Detects(2, 0), rig.B.Detects(1, 0)));
            Assert.Equal(!war1, rig.B.IsHidden(0));
            var last = rig.B.ConcealmentChanges[^1];
            if (war1) Assert.Equal((0, false, ConcealEnd.Spotted, m), (last.AgentId, last.Begin, last.Cause, last.LogIndex));
            else Assert.Equal((0, true, h), (last.AgentId, last.Begin, last.LogIndex));

            // (b) a third polity's agent in contact, or watching
            foreach (var (x2, refusal) in new[] { (13L, WarRefusal.EnemyInContact), (15L, WarRefusal.Observed) })
            {
                var rb = Build(new[] { (T0, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50), A(2, 2, 1, x2, war: -1) }, wars: war1 ? twoWars : oneWar);
                To(rb, T0);
                Assert.True(rb.B.Detects(0, 2));                                  // polity 0 sees the third polity's Watcher
                int hb = Act(rb, 0, WarKinds.ActHide, 0, 0);
                Finish(rb, T0 + Day);
                Assert.Equal(war1 ? refusal : 0, Outcome(rb, hb));
                Assert.Equal(!war1, rb.B.IsHidden(0));
                if (!war1) Assert.Equal(x2 == 13, rb.B.Detects(2, 0));            // detected by polity 2 (1 ≤ 2), not an enemy: held
            }
        }

        // (c) control: the war enemy's move next to the hide spots it
        var rc = Build(new[] { (T0, 0), (T1, 1) }, new[] { A(0, 0, 0, 12), A(1, 1, 1, 50) }, wars: oneWar);
        To(rc, T0);
        Act(rc, 0, WarKinds.ActHide, 0, 0);
        To(rc, T1);
        int mc = Act(rc, 1, WarKinds.ActMove, 1, 13);
        Finish(rc, T1 + Day);
        Assert.False(rc.B.IsHidden(0));
        Assert.Equal((ConcealEnd.Spotted, mc), (rc.B.ConcealmentChanges[^1].Cause, rc.B.ConcealmentChanges[^1].LogIndex));
    }

    // ---------------------------------------------------------------- AP-R2-02: the agent's own cell, at band edges

    /// <summary>
    /// THE HIDE USES THE CONCEALMENT OF ITS OWN CELL (proposal §2.1: "the class of a's cell"; Law 2), pinned at the EDGES of terrain
    /// bands, where a neighbouring cell's class differs (AP-R2-02: before round 2 every hide stood inside a band, and a mutant reading
    /// the next cell survived the whole suite, MEASURED). The Raider hides at <paramref name="cell"/>; the Watcher (sight 4) marches from
    /// 45 to <paramref name="to"/>. The factor is exactly the own cell's (Jungle 1/4, Forest 1/2); detection follows it exactly:
    /// 29 (Jungle; 30 Marsh, 1) at 2 cells: hidden (a factor read from 30 would give 4 cells); 20 (Jungle; 19 Forest) at 2: hidden
    /// (19's 1/2 would give 2); 10 (Forest; 9 Open) at 3: hidden (9's 1 would give 4); 19 (Forest; 20 Jungle) at 2: spotted (20's
    /// 1/4 would give 1).
    /// </summary>
    [Theory]
    [InlineData(29L, 31L, 1L, 4L, false)]
    [InlineData(20L, 22L, 1L, 4L, false)]
    [InlineData(10L, 13L, 1L, 2L, false)]
    [InlineData(19L, 21L, 1L, 2L, true)]
    public void Hide_UsesTheConcealmentOfItsOwnCell_AtTheEdgesOfTerrainBands(long cell, long to, long num, long den, bool spotted)
    {
        var rig = Build(new[] { (T0, 0), (T0 + Day, 1) }, new[] { A(0, 0, 0, cell), A(1, 1, 1, 45) });
        To(rig, T0);
        int h = Act(rig, 0, WarKinds.ActHide, 0, 0);
        To(rig, T0 + Day);
        Assert.Equal(0, Outcome(rig, h));
        Assert.Equal(Q32.FromRatio(num, den), rig.B.ConcealmentFactor(0));
        Assert.Equal(ConcealmentTable.PlaceholderTestTable().Multiplier(rig.B.TerrainAt(cell)), rig.B.ConcealmentFactor(0));
        Assert.NotEqual(rig.B.TerrainAt(cell - 1) == rig.B.TerrainAt(cell), rig.B.TerrainAt(cell + 1) == rig.B.TerrainAt(cell));  // a band edge
        int m = Act(rig, 1, WarKinds.ActMove, 1, to);
        Finish(rig, T0 + 2 * Day);
        Assert.Equal(0, Outcome(rig, m));
        Assert.Equal((!spotted, spotted), (rig.B.IsHidden(0), rig.B.Detects(1, 0)));
    }

    /// <summary>NOCOVER AND CANNOTDIG READ THE AGENT'S OWN CELL at the band edges (AP-R2-02's property for the two other own-cell reads):
    /// Hide at 9 (Open, next to Forest) → NoCover, at 10 (Forest, next to Open) → accepted, at 29 (Jungle, next to Marsh) → accepted, at
    /// 30 (Marsh, next to Jungle) → NoCover; Entrench at 29 → accepted, at 30 (Marsh) → CannotDig, at 39 (Water, next to Open) →
    /// CannotDig, at 40 (Open, next to Water) → accepted. Accepted orders hold their state; refused ones change nothing.</summary>
    [Fact]
    public void NoCoverAndCannotDig_ReadTheAgentsOwnCell_AtTheEdgesOfTerrainBands()
    {
        foreach (var (kind, cell, expected) in new[]
                 {
                     (WarKinds.ActHide, 9L, WarRefusal.NoCover), (WarKinds.ActHide, 10L, 0), (WarKinds.ActHide, 29L, 0), (WarKinds.ActHide, 30L, WarRefusal.NoCover),
                     (WarKinds.ActEntrench, 29L, 0), (WarKinds.ActEntrench, 30L, WarRefusal.CannotDig), (WarKinds.ActEntrench, 39L, WarRefusal.CannotDig),
                     (WarKinds.ActEntrench, 40L, 0),
                 })
        {
            var rig = Build(new[] { (T0, 0) }, new[] { A(0, 0, 0, cell), A(1, 1, 1, 55) });
            To(rig, T0);
            int o = Act(rig, 0, kind, 0, 0);
            Finish(rig, T0 + Day);
            Assert.True(expected == Outcome(rig, o), $"order {kind} at {cell} ({rig.B.TerrainAt(cell)}): expected {expected}, got {Outcome(rig, o)}");
            Assert.Equal(expected == 0, rig.B.StatesOf(0) != ConcealKind.None);
            Assert.Equal(expected == 0 ? 1 : 0, rig.B.ConcealmentChanges.Count);
        }
    }

    // ---------------------------------------------------------------- AP-R2-03: the contact range, at its edge

    /// <summary>
    /// ENEMYINCONTACT IS THE HIDER'S OWN ATTACK RANGE, EXACTLY (proposal §3.1: "no enemy agent within the agent's attack range"),
    /// pinned from BOTH sides (AP-R2-03: before round 2 only the refusing side was tested, and a mutant refusing one cell beyond the
    /// range survived, MEASURED). The Raider (sight 6; attack range <paramref name="range"/>: 1 as shipped, 2 a TEST INPUT) at 12
    /// (Forest) and a Dim Watcher (sight 1) that the Raider's polity sees and that does NOT watch the Raider beyond 1 cell: at the
    /// range → refused EnemyInContact, nothing changed; one cell beyond → accepted, the state held.
    /// </summary>
    [Theory]
    [InlineData(WarKinds.ActHide, 1L)]
    [InlineData(WarKinds.ActCamouflage, 1L)]
    [InlineData(WarKinds.ActHide, 2L)]
    [InlineData(WarKinds.ActCamouflage, 2L)]
    public void EnemyInContact_IsTheHidersOwnAttackRange_RefusedAtItAndAcceptedOneCellBeyond(int kind, long range)
    {
        var prof = Profs.Select(p => p.Id == 0 ? p with { AttackRange = range } : p).ToArray();
        foreach (long d in new[] { range, range + 1 })
        {
            var rig = Build(new[] { (T0, 0) }, new[] { A(0, 0, 0, 12), A(1, 1, 5, 12 + d) }, profiles: prof);
            To(rig, T0);
            Assert.True(rig.B.Detects(0, 1));                                    // a SEEN enemy: the boundary is the range, not detection
            Assert.Equal(d <= 1, rig.B.Detects(1, 0));
            int o = Act(rig, 0, kind, 0, 0);
            Finish(rig, T0 + Day);
            bool inContact = d == range;
            Assert.Equal(inContact ? WarRefusal.EnemyInContact : 0, Outcome(rig, o));
            Assert.Equal(!inContact, rig.B.StatesOf(0) != ConcealKind.None);
            Assert.Equal(inContact ? 10 : 10 - (kind == WarKinds.ActHide ? 2 : 3), rig.B.RemainingOf(0));
        }
    }
}
