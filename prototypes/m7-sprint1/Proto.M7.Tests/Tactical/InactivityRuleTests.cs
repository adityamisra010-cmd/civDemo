using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// The Director's five-pulse inactivity rule with the active-concealment clause (spec §4 tests (i)-(iv)).
/// Every scenario runs under the HARNESS guard only; the engine itself has no pulse cap.
/// [TR] These pin the SPRINT 1 (LEGACY) side-disengagement rule, superseded for the tactical resolver by DIR5 §2.6 / D-079
/// R17 (tests of the DIR5 rule: HiddenStationaryRuleTests). Kept, pinned to InactivityModel.LegacySideDisengage, because the
/// AutoResolver still implements the legacy rule and the parity tests compare both resolvers on it.
/// </summary>
public class InactivityRuleTests
{
    private static long PulseMs => new TacticalRules().PulseMs;

    // (ii) A hidden unit with no enemy nearby: its side disengages after EXACTLY five pulses; entities preserved as survivors.
    [Fact]
    public void HiddenUnitWithNoEnemyNearbyDisengagesAfterExactlyFivePulses()
    {
        var g = OpenField();
        TT.Fill(g, 3, 3, 9, 9, TerrainClass.Jungle);
        var setup = TT.Setup(g, DefaultSides(null, new[] { Cell(40, 1) }), 11, TT.HideRules(),
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(6, 6)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(40, 28)));   // advances north, 34+ cells away
        var b = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        var r = TT.RunGuarded(b);
        Assert.Equal(BattleEndReason.SideDisengaged, r.Reason);
        Assert.Equal(0, r.Side);
        Assert.Equal(5, r.Pulses);
        Assert.Equal(5 * PulseMs, r.EndMs);
        Assert.Equal(1, r.HoldsField);
        var f1 = r.Formations[0];
        Assert.Equal(10, f1.DisengagedEscaped);       // preserved — not deleted, not killed
        Assert.Equal(10, f1.Survivors);
        Assert.Equal(0, f1.Killed + f1.Surrendered);
        Assert.All(b.Entities().Where(e => e.Side == 0), e => Assert.True(e.Disengaged && e.State == EntityState.Escaped));
        TT.AssertConserved(r);
    }

    // (iii) Mutual hiding standoff: both concealed, inside each other's envelope, nobody active => ends after five pulses.
    [Fact]
    public void MutualHidingStandoffEndsAfterFivePulses()
    {
        var g = OpenField();
        TT.Fill(g, 15, 13, 20, 19, TerrainClass.Jungle);
        TT.Fill(g, 26, 13, 31, 19, TerrainClass.Jungle);
        var setup = TT.Setup(g, DefaultSides(), 12, TT.HideRules(),
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(18, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(28, 16)));   // 10 cells: inside both sights (12)
        var b = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        var r = TT.RunGuarded(b);
        Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
        Assert.Equal(5, r.Pulses);
        Assert.Equal(5 * PulseMs, r.EndMs);
        Assert.All(r.Formations, f => { Assert.Equal(10, f.DisengagedEscaped); Assert.Equal(0, f.Killed); });
        Assert.False(b.EverDetected(0, 2000));
        Assert.False(b.EverDetected(1, 1000));
    }

    private static BattleSetup Ambush(bool rule)
    {
        var g = OpenField();
        TT.Fill(g, 21, 5, 27, 11, TerrainClass.Jungle);
        var hider = TT.WithSight(UnitProfiles.HeavyInfantry, 6);      // TEST INPUT: short-sighted ambusher
        return TT.Setup(g, DefaultSides(null, new[] { Cell(16, 16) }), 21, TT.HideRules(rule),
            new FormationSpec(1, 0, hider, 10, Cell(24, 8)) { Posture = Posture.Ambush },
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(30, 16)));
    }

    private static ScriptedController Searcher() => new ScriptedController()
        .Add(0, new OrderRequest(2, OrderKind.MoveTo, 0, -1, Cell(16, 16)))     // sweeps past at 8 cells (inside its sight 12)
        .Add(5, new OrderRequest(2, OrderKind.MoveTo, 0, -1, Cell(24, 11)));    // then turns into the ambush

    // (i) A hidden ambusher with an enemy approaching/searching inside its envelope stays (no termination) and springs.
    [Fact]
    public void HiddenAmbusherWithSearchingEnemyStaysAndSprings()
    {
        var b = new TacticalBattle(Ambush(rule: true), new KnowledgeAi(), Searcher());
        b.Start();
        int maxCounter = 0;
        while (b.Advance())
        {
            maxCounter = Math.Max(maxCounter, b.InactivityCounter(1));
            if (b.CurrentPulse > TT.HarnessMaxPulses) Assert.Fail("HARNESS GUARD");
        }
        var r = b.Result!;
        var a = r.Formations[0];
        Assert.True(maxCounter < 5);
        Assert.True(r.Pulses > 5);                 // survived well past the five-pulse mark
        Assert.Equal(0, a.DisengagedEscaped);
        Assert.True(a.DamageDealt > 0);            // the ambush was sprung
        Assert.True(a.AttacksMade > 0);
        TT.AssertConserved(r);
    }

    // (i-control) Teeth: the SAME scenario with the active-concealment clause OFF disengages the ambusher at pulse 5, unsprung.
    [Fact]
    public void WithoutActiveConcealmentTheAmbusherWouldBeDisengaged()
    {
        var r = TT.RunGuarded(new TacticalBattle(Ambush(rule: false), new KnowledgeAi(), Searcher()));
        Assert.Equal(BattleEndReason.SideDisengaged, r.Reason);
        Assert.Equal(0, r.Side);
        Assert.Equal(5, r.Pulses);
        Assert.Equal(0, r.Formations[0].DamageDealt);
        Assert.Equal(10, r.Formations[0].DisengagedEscaped);
    }

    // (iv) An oscillating mover cannot keep a battle alive: moving back and forth creates no new minima.
    [Fact]
    public void OscillatingMoverCannotKeepTheBattleAlive()
    {
        var osc = new ScriptedController();
        for (int p = 0; p < 40; p++)
            osc.Add(p, new OrderRequest(1, OrderKind.MoveTo, 0, -1, p % 2 == 0 ? Cell(10, 10) : Cell(10, 22)));
        var setup = TT.Setup(OpenField(), DefaultSides(), 13, TT.Legacy,   // [TR] legacy rule
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(40, 16)));
        var b = new TacticalBattle(setup, osc, PassiveController.Instance);
        var anchors = new List<Vec2L>();
        b.Start();
        long seen = -1;
        while (b.Advance())
        {
            if (b.CurrentPulse != seen) { seen = b.CurrentPulse; anchors.Add(b.Anchor(1)); }
            if (b.CurrentPulse > TT.HarnessMaxPulses) Assert.Fail("HARNESS GUARD");
        }
        var r = b.Result!;
        Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
        Assert.Equal(5, r.Pulses);
        Assert.True(anchors.Distinct().Count() >= 4);   // it really kept moving
        TT.AssertConserved(r);
    }

    // (iv-b) Oscillating in SIGHT of the enemy: only the first approach/detections count; the battle still ends, bounded.
    [Fact]
    public void OscillatingInSightEndsAfterFiveIdlePulses()
    {
        var osc = new ScriptedController();
        for (int p = 0; p < 40; p++)
            osc.Add(p, new OrderRequest(1, OrderKind.MoveTo, 0, -1, p % 2 == 0 ? Cell(12, 16) : Cell(5, 16)));   // toward / away from the enemy
        var setup = TT.Setup(OpenField(), DefaultSides(), 14, TT.Legacy,   // [TR] legacy rule
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(20, 16)));
        var b = new TacticalBattle(setup, osc, PassiveController.Instance);
        var xs = new List<long>();
        b.Start();
        long seen = -1;
        while (b.Advance())
        {
            if (b.CurrentPulse != seen) { seen = b.CurrentPulse; xs.Add(b.Anchor(1).X); }
            if (b.CurrentPulse > TT.HarnessMaxPulses) Assert.Fail("HARNESS GUARD");
        }
        var r = b.Result!;
        Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
        Assert.InRange(r.Pulses, 5, 8);                      // bounded: only the first approach set new minima
        Assert.True(xs.Max() - xs.Min() >= 3 * CellMm);      // it really went back and forth by several cells
        Assert.All(r.Formations, f => Assert.Equal(0, f.Killed));
    }
}
