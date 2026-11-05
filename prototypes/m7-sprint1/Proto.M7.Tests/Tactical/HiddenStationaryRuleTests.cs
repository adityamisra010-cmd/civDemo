using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// [TR] DIR5 §2.6 hidden-stationary rule (D-079 DR20, R17, delegated D6) — the exact predicate is on HiddenStationaryRule:
///   ROUT_p(f) := c_p(f) &gt;= 5 AND NOT ((a) concealed with an Active enemy in engagement reach OR (b) explicit Hold in force /
///   on own objective OR (c) ambush posture still waiting), c counting consecutive pulses HIDDEN (no member tracked by the
///   enemy) AND without progress. Consequence: ROUTED (Slice D rout mechanics). Mutual guard and whole-field STANDOFF end.
/// Scenario family: open field, 20 m cells; A = side-0 HI formation hidden in a jungle block; B = side-1 HI formation far
/// beyond sight holding on an EXPLICIT Hold order (exempt (b)), so A's fate is decided by the rule alone.
/// Pulse 60 s; a battle ending at the end of pulse p reports Pulses = p + 1 and EndMs = (p + 1) * 60,000.
/// </summary>
public class HiddenStationaryRuleTests
{
    private static long PulseMs => new TacticalRules().PulseMs;

    /// <summary>Tight harness guard for this class: every battle here is expected to end within about 15 pulses, so 100
    /// pulses without an end is a FAILURE (non-termination), detected fast — never a battle rule.</summary>
    private const long Guard = 100;

    private static BattleResult Run(TacticalBattle b) => TT.RunGuarded(b, Guard);

    /// <summary>Runs a possibly already started battle to its end under the harness guard (non-termination = FAILURE).</summary>
    private static BattleResult Finish(TacticalBattle b)
    {
        while (b.Advance())
            if (b.CurrentPulse > Guard) Assert.Fail("HARNESS GUARD: non-termination is a FAILURE, not a rule");
        return b.Result!;
    }

    private static ScriptedController HoldB() => new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Hold));

    private static TerrainGrid JungleAt(int x0 = 3, int y0 = 3, int x1 = 9, int y1 = 9)
    {
        var g = OpenField();
        TT.Fill(g, x0, y0, x1, y1, TerrainClass.Jungle);
        return g;
    }

    private static BattleSetup Base(TerrainGrid? g = null, SideSpec[]? sides = null, FormationSpec? a = null, TacticalRules? rules = null)
        => TT.Setup(g ?? JungleAt(), sides ?? DefaultSides(), 11, rules ?? TT.HideRulesDir5(),
            a ?? new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(6, 6)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(40, 28)));

    // ------------------------------------------------------------------ the pure predicate

    [Fact]
    public void PredicateCounterAndThreshold()
    {
        Assert.Equal(1, HiddenStationaryRule.NextCounter(0, hidden: true, progress: false, 5));
        Assert.Equal(5, HiddenStationaryRule.NextCounter(4, true, false, 5));
        Assert.Equal(5, HiddenStationaryRule.NextCounter(5, true, false, 5));          // saturates: no overflow, no implicit ceiling
        Assert.Equal(0, HiddenStationaryRule.NextCounter(4, false, false, 5));         // seen by an enemy: not hidden
        Assert.Equal(0, HiddenStationaryRule.NextCounter(4, true, true, 5));           // progress
        Assert.False(HiddenStationaryRule.Routs(4, 5, InactivityExemption.None));       // 4 pulses: not yet
        Assert.True(HiddenStationaryRule.Routs(5, 5, InactivityExemption.None));        // 5 pulses: routs
        foreach (var ex in new[] { InactivityExemption.ConcealedEnemyInReach, InactivityExemption.DefensiveHold, InactivityExemption.AmbushWait,
                                   InactivityExemption.DefensiveHold | InactivityExemption.AmbushWait })
            Assert.False(HiddenStationaryRule.Routs(5, 5, ex));
    }

    // ------------------------------------------------------------------ genuine inactivity: routed at exactly five pulses

    /// <summary>A sits hidden and idle on its default (not ordered) Hold: c = 5 at the end of pulse 4 -&gt; ROUTED (not escaped,
    /// not deleted): side 0 has no Active entity left -&gt; SideEliminated at 300,000 ms; the routing entities are counted as
    /// routed escapes at the end (conserved, 0 killed). B is judged at the same instant but exempt (explicit Hold).</summary>
    [Fact]
    public void GenuinelyInactiveHiddenFormationRoutsAfterExactlyFivePulses()
    {
        var b = new TacticalBattle(Base(), PassiveController.Instance, HoldB());
        var r = Run(b);
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(0, r.Side);
        Assert.Equal(5, r.Pulses);
        Assert.Equal(5 * PulseMs, r.EndMs);
        Assert.Equal(1, r.HoldsField);
        var a = r.Formations[0];
        Assert.Equal(10, a.RoutedEscaped);
        Assert.Equal(0, a.DisengagedEscaped);                     // R17: "escaped" is not the consequence
        Assert.Equal(0, a.Killed + a.Surrendered);
        Assert.Equal(10, r.Formations[1].ActiveAtEnd);
        Assert.Equal(new[]
        {
            new InactivityVerdict(4, 1, 0, 5, InactivityExemption.None, true),
            new InactivityVerdict(4, 2, 1, 5, InactivityExemption.DefensiveHold, false),
        }, b.InactivityVerdicts);
        TT.AssertConserved(r);
    }

    /// <summary>
    /// The 4-vs-5 boundary as an order-delivery pin with +-1 neighbours: A is ordered to march to its own objective (14 cells
    /// south) at pulse k. The march is progress (new running minimum to the objective within that pulse). k = 3 or 4 (the 4th
    /// or 5th pulse) resets the counter before it reaches 5 -&gt; A is not routed (it reaches the objective and the battle ends
    /// later as a Standoff with everybody exempt); k = 5 or 6 is too late: after pulses 0-4 idle, A is routed at the end of
    /// pulse 4 and the order is never delivered.
    /// </summary>
    [Theory]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void ProgressInTheFifthPulseSavesItAndTheSixthIsTooLate(int orderPulse, bool routed)
    {
        var obj = Cell(6, 20);
        var s = Base(sides: DefaultSides(new[] { obj }, null));
        var ctl = new ScriptedController().Add(orderPulse, new OrderRequest(1, OrderKind.MoveTo, 0, -1, obj));
        var b = new TacticalBattle(s, ctl, HoldB());
        b.RunUntilPulse(Math.Min(orderPulse, 4));
        Assert.Equal(Math.Min(orderPulse, 4), b.IdleCounter(1));       // c after pulses 0..k-1, all idle
        var r = Finish(b);
        if (routed)
        {
            Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
            Assert.Equal(5, r.Pulses);
            Assert.Equal(10, r.Formations[0].RoutedEscaped);
            Assert.DoesNotContain(b.Orders, o => o.Side == 0);         // the order pulse never came
        }
        else
        {
            Assert.Equal(BattleEndReason.Standoff, r.Reason);
            Assert.True(r.Pulses > 5);
            Assert.Equal(0, r.Formations[0].RoutedEscaped);
            Assert.Equal(10, r.Formations[0].ActiveAtEnd);
            Assert.DoesNotContain(b.InactivityVerdicts, v => v.Routed);
            Assert.Contains(b.Orders, o => o.Side == 0 && o.Pulse == orderPulse && o.Accepted);
        }
        TT.AssertConserved(r);
    }

    // ------------------------------------------------------------------ exemption (a): concealed with an enemy in reach

    /// <summary>
    /// One A entity in a single jungle cell (Cell(10,16)); one B entity in the open d mm east, holding (explicit). A sees B at
    /// t = 0 (first detection = progress in pulse 0) and stays hidden itself; pulses 1-5 idle -&gt; c = 5 at the end of pulse 5.
    /// Engagement reach of A = contact 1,200 + one pulse at 1,300 mm/s (78,000) = 79,200 mm.
    /// d = 79,200: exempt (a) -&gt; not routed; the whole field is idle too -&gt; Standoff, 6 pulses.
    /// d = 79,201: not exempt -&gt; routed -&gt; SideEliminated, 6 pulses. Clause off (teeth): routed at d = 79,200.
    /// </summary>
    [Theory]
    [InlineData(79_200, true, false)]
    [InlineData(79_201, true, true)]
    [InlineData(79_200, false, true)]
    public void ConcealedWithAnEnemyInReachIsNotInactivity(long d, bool clause, bool routed)
    {
        var g = OpenField();
        g.SetClass(10, 16, TerrainClass.Jungle);
        Vec2L pa = Cell(10, 16), pb = pa + new Vec2L(d, 0);
        var s = TT.Setup(g, DefaultSides(), 5, TT.HideRulesDir5(clause),
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 1, pa) { Entities = new[] { new EntitySpec(10, pa) } },
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 1, pb) { Entities = new[] { new EntitySpec(20, pb) } });
        var b = new TacticalBattle(s, PassiveController.Instance, HoldB());
        var r = Run(b);
        Assert.True(b.EverDetected(0, 20));                            // A saw B
        Assert.False(b.EverDetected(1, 10));                           // B never found A: hidden throughout
        Assert.Equal(6, r.Pulses);
        var v = Assert.Single(b.InactivityVerdicts, x => x.FormationId == 1);
        Assert.Equal(5, v.Pulse);
        Assert.Equal(routed, v.Routed);
        Assert.Equal(clause && !routed ? InactivityExemption.ConcealedEnemyInReach : InactivityExemption.None, v.Exemptions);
        Assert.Equal(routed ? BattleEndReason.SideEliminated : BattleEndReason.Standoff, r.Reason);
        Assert.Equal(routed ? 1 : 0, r.Formations[0].RoutedEscaped);
        TT.AssertConserved(r);
    }

    // ------------------------------------------------------------------ exemption (b): defensive hold / position

    /// <summary>An EXPLICIT Hold order (accepted) exempts A: no rout; with B also exempt the whole field is idle -&gt; Standoff at
    /// the end of pulse 4 with both formations intact. A Hold followed by a Rally order is no longer a Hold in force: routed.
    /// The default standing Hold (no order) is genuine inactivity (base test).</summary>
    [Fact]
    public void ExplicitHoldIsDefenceNotInactivity()
    {
        var hold = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.Hold));
        var b = new TacticalBattle(Base(), hold, HoldB());
        var r = Run(b);
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.Equal(5, r.Pulses);
        Assert.All(r.Formations, f => { Assert.Equal(10, f.ActiveAtEnd); Assert.Equal(0, f.Escaped); });
        Assert.All(b.InactivityVerdicts, v => Assert.Equal(InactivityExemption.DefensiveHold, v.Exemptions));
        Assert.True(b.HoldExplicit(1));

        var thenRally = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.Hold)).Add(1, new OrderRequest(1, OrderKind.Rally));
        var b2 = new TacticalBattle(Base(), thenRally, HoldB());
        var r2 = Run(b2);
        Assert.Equal(BattleEndReason.SideEliminated, r2.Reason);
        Assert.Equal(5, r2.Pulses);
        Assert.False(b2.HoldExplicit(1));
        Assert.Equal(10, r2.Formations[0].RoutedEscaped);
    }

    /// <summary>
    /// Order-delivery pin for exemption (b): the explicit Hold is judged as in force at the pulse end that judges A. Delivered
    /// in pulse 3 or 4 — in pulse 4 even at its LAST step (offset 29, t = 298,000 ms, before the 300,000 ms pulse end) — it
    /// saves A at the end of pulse 4 (Standoff, B exempt too); delivered at pulse 5 it is too late (routed at the end of 4).
    /// A Hold is not progress: the counter still reaches 5 (the verdict records the exemption).
    /// </summary>
    [Theory]
    [InlineData(3, 0, false)]
    [InlineData(4, 0, false)]
    [InlineData(4, 29, false)]
    [InlineData(5, 0, true)]
    public void ExplicitHoldCountsFromTheInstantItIsDelivered(int pulse, int stepOffset, bool routed)
    {
        var hold = new ScriptedController().Add(pulse, new OrderRequest(1, OrderKind.Hold, stepOffset));
        var b = new TacticalBattle(Base(), hold, HoldB());
        var r = Run(b);
        Assert.Equal(5, r.Pulses);
        Assert.Equal(routed ? BattleEndReason.SideEliminated : BattleEndReason.Standoff, r.Reason);
        var v = b.InactivityVerdicts.Single(x => x.FormationId == 1);
        Assert.Equal(4, v.Pulse);
        Assert.Equal(5, v.IdlePulses);
        Assert.Equal(routed ? InactivityExemption.None : InactivityExemption.DefensiveHold, v.Exemptions);
        if (!routed) Assert.Equal((long)pulse * 60_000 + stepOffset * 2_000, b.Orders.Single(o => o.Side == 0).AtMs);
    }

    /// <summary>Standing on an own objective is holding a position: A's anchor exactly one cell (20,000 mm) from its side's
    /// objective is exempt (Standoff, nobody routed); 20,001 mm is not (routed at the end of pulse 4).</summary>
    [Theory]
    [InlineData(20_000, false)]
    [InlineData(20_001, true)]
    public void HoldingAnObjectiveIsDefence(long offset, bool routed)
    {
        var obj = Cell(6, 6) + new Vec2L(offset, 0);
        var b = new TacticalBattle(Base(sides: DefaultSides(new[] { obj }, null)), PassiveController.Instance, HoldB());
        var r = Run(b);
        Assert.Equal(5, r.Pulses);
        Assert.Equal(routed ? BattleEndReason.SideEliminated : BattleEndReason.Standoff, r.Reason);
        Assert.Equal(routed ? InactivityExemption.None : InactivityExemption.DefensiveHold, b.InactivityVerdicts.Single(v => v.FormationId == 1).Exemptions);
    }

    // ------------------------------------------------------------------ exemption (c): deliberate waiting (ambush)

    /// <summary>A deployed under an Ambush (wait) order on its standing Hold is waiting, not inactive: not routed (Standoff with
    /// B also exempt). Once it is ordered to move (directive no longer the waiting Hold) and then idles hidden, it is genuinely
    /// inactive again: routed at the end of pulse 4.</summary>
    [Fact]
    public void AmbushWaitIsDeliberateWaiting()
    {
        var amb = new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(6, 6)) { Posture = Posture.Ambush };
        var b = new TacticalBattle(Base(a: amb), PassiveController.Instance, HoldB());
        var r = Run(b);
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.Equal(5, r.Pulses);
        Assert.Equal(InactivityExemption.AmbushWait, b.InactivityVerdicts.Single(v => v.FormationId == 1).Exemptions);
        Assert.Equal(10, r.Formations[0].ActiveAtEnd);

        var move = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(7, 6)));
        var b2 = new TacticalBattle(Base(a: amb), move, HoldB());
        var r2 = Run(b2);
        Assert.Equal(BattleEndReason.SideEliminated, r2.Reason);
        Assert.Equal(5, r2.Pulses);
        Assert.Equal(10, r2.Formations[0].RoutedEscaped);
    }

    // ------------------------------------------------------------------ mutual guard + standoffs

    /// <summary>
    /// [verify-TR-r1 V3] Both sides hidden and genuinely idle (no orders, no objectives, beyond sight). DEFAULT (D-079 S304:
    /// "routed or lost, per unit; not a draw or intact escape by default"): BOTH formations are routed at the end of pulse 4
    /// and the battle ends BothEliminated — every entity a routed escape, nobody killed, nobody left Active. With the
    /// mutual guard switched ON (TacticalRules.MutualStandoffGuard, lane TR's PROPOSAL P-TR1) the same battle is a Standoff
    /// with both formations intact (the pre-round-1 default).
    /// </summary>
    [Fact]
    public void MutualHiddenIdlenessRoutsBothSidesUnlessTheGuardIsOn()
    {
        var d = new TacticalBattle(Base(), PassiveController.Instance, PassiveController.Instance);
        var rd = Run(d);
        Assert.Equal(BattleEndReason.BothEliminated, rd.Reason);
        Assert.Equal(-1, rd.Side);
        Assert.Equal(5, rd.Pulses);
        Assert.Equal(5 * PulseMs, rd.EndMs);
        Assert.All(rd.Formations, f => { Assert.Equal(0, f.ActiveAtEnd); Assert.Equal(10, f.RoutedEscaped); Assert.Equal(0, f.Killed + f.Surrendered); });
        Assert.Equal(2, d.InactivityVerdicts.Count);
        Assert.All(d.InactivityVerdicts, v => { Assert.True(v.Routed); Assert.Equal(4, v.Pulse); Assert.Equal(InactivityExemption.None, v.Exemptions); });
        TT.AssertConserved(rd);

        var b = new TacticalBattle(Base(rules: TT.HideRulesDir5() with { MutualStandoffGuard = true }), PassiveController.Instance, PassiveController.Instance);
        var r = Run(b);
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.Equal(-1, r.Side);
        Assert.Equal(-1, r.HoldsField);
        Assert.Equal(5, r.Pulses);
        Assert.All(r.Formations, f => { Assert.Equal(10, f.ActiveAtEnd); Assert.Equal(0, f.Escaped + f.Killed + f.Surrendered); });
        Assert.Equal(2, b.InactivityVerdicts.Count);
        Assert.All(b.InactivityVerdicts, v => { Assert.False(v.Routed); Assert.Equal(InactivityExemption.None, v.Exemptions); });
    }

    /// <summary>Visible but idle: two formations in the open 8 cells apart see each other at t = 0 (progress) and then stand.
    /// They are never hidden, so the rule never judges them; the whole field is idle for pulses 1-5 -&gt; Standoff, 6 pulses.</summary>
    [Fact]
    public void VisibleIdleFormationsEndInAStandoff()
    {
        var s = TT.Setup(OpenField(), DefaultSides(), 7, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(20, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(28, 16)));
        var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
        var r = Run(b);
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.Equal(6, r.Pulses);
        Assert.Empty(b.InactivityVerdicts);
        Assert.Equal(0, b.IdleCounter(1));
        Assert.Equal(5, b.FieldIdleCounter);
    }

    /// <summary>"Stationary" means going nowhere: A marches back and forth inside its jungle every pulse (it really moves).
    /// [verify-TR-r1 V1] Marching on a MoveTo is progress only while it sets a new minimum of the distance to that order's goal
    /// cell (minima per goal cell, never reset): pulse 0 reaches Cell(5,5) and pulse 1 gets 22 m closer to Cell(8,8) (both
    /// progress, counter 0); from pulse 2 on it only re-covers ground toward goals it already came as close to, so the counter
    /// runs 1, 2, 3, 4, 5 at the ends of pulses 2-6 -&gt; stationary in effect -&gt; routed at the end of pulse 6 (7 pulses).</summary>
    [Fact]
    public void OscillatingHiddenFormationIsStationaryInEffect()
    {
        var osc = new ScriptedController();
        for (int p = 0; p < 10; p++) osc.Add(p, new OrderRequest(1, OrderKind.MoveTo, 0, -1, p % 2 == 0 ? Cell(5, 5) : Cell(8, 8)));
        var b = new TacticalBattle(Base(), osc, HoldB());
        var anchors = new List<Vec2L>();
        var idleAtPulseStart = new List<long>();
        b.Start();
        long seen = -1;
        while (b.Advance())
        {
            if (b.CurrentPulse != seen) { seen = b.CurrentPulse; anchors.Add(b.Anchor(1)); idleAtPulseStart.Add(b.IdleCounter(1)); }
            if (b.CurrentPulse > Guard) Assert.Fail("HARNESS GUARD");
        }
        var r = b.Result!;
        // It really kept moving: the anchor travelled at least one cell (20 m) in every pulse (measured: 28-31 m), back and forth.
        Assert.Equal(7, anchors.Count);
        for (int i = 1; i < anchors.Count; i++)
            Assert.True(Vec2L.Distance(anchors[i - 1], anchors[i]) >= CellMm, $"pulse {i}: moved {Vec2L.Distance(anchors[i - 1], anchors[i])} mm");
        Assert.Equal(new long[] { 0, 0, 1, 2, 3, 4 }, idleAtPulseStart.Skip(1).ToArray());
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(7, r.Pulses);
        Assert.Equal(10, r.Formations[0].RoutedEscaped);
        var v = Assert.Single(b.InactivityVerdicts, x => x.FormationId == 1);
        Assert.Equal(6, v.Pulse);
        Assert.True(v.Routed);
    }

    // ------------------------------------------------------------------ the consequence uses the rout mechanics

    /// <summary>
    /// When the side still has another formation on the field (C, holding explicitly, exempt), the battle goes on after A is
    /// routed at the end of pulse 4: A's entities flee West down the exit field through the jungle and ESCAPE at the edge as
    /// routed escapes (none killed or captured); the field then idles and ends in a Standoff with C intact.
    /// </summary>
    [Fact]
    public void RoutedFormationLeavesByTheRoutMechanics()
    {
        var holdC = new ScriptedController().Add(0, new OrderRequest(3, OrderKind.Hold));
        var s = TT.Setup(JungleAt(), DefaultSides(), 11, TT.HideRulesDir5(),
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(6, 6)),
            new FormationSpec(3, 0, UnitProfiles.HeavyInfantry, 10, Cell(6, 24)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(40, 28)));
        var b = new TacticalBattle(s, holdC, HoldB());
        b.RunUntilPulse(5);
        Assert.All(b.Entities().Where(e => e.FormationId == 1), e => Assert.Equal(EntityState.Routing, e.State));
        var r = Finish(b);
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.True(r.Pulses > 6);
        var a = r.Formations.Single(f => f.FormationId == 1);
        Assert.Equal(10, a.RoutedEscaped);
        Assert.Equal(0, a.Killed + a.Surrendered);
        Assert.Equal(10, r.Formations.Single(f => f.FormationId == 3).ActiveAtEnd);
        Assert.All(b.Entities().Where(e => e.FormationId == 1), e => Assert.True(e.RoutedEscape && !e.Disengaged));
        TT.AssertConserved(r);
    }

    // ------------------------------------------------------------------ replay + determinism of the new rule

    [Fact]
    public void ReplayReproducesARoutVerdictBitForBit()
    {
        var obj = Cell(6, 20);
        var s = Base(sides: DefaultSides(new[] { obj }, null));
        var live = new TacticalBattle(s, new ScriptedController().Add(2, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(6, 8))), HoldB());
        var r1 = Run(live);
        var replay = new TacticalBattle(s, ScriptedController.FromLog(live.Orders, 0), ScriptedController.FromLog(live.Orders, 1));
        var r2 = Run(replay);
        Assert.Equal(r1.TraceHash, r2.TraceHash);
        Assert.Equal(r1.StateHash, r2.StateHash);
        Assert.Equal(r1.ResultHash(), r2.ResultHash());
        Assert.Equal(live.InactivityVerdicts, replay.InactivityVerdicts);
        Assert.Contains(live.InactivityVerdicts, v => v.Routed);
    }
}
