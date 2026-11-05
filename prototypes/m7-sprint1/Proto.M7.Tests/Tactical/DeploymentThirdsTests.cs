using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// [TR] DIR5 §2.14 deployment thirds: each army in its own third along the deployment axis, the middle third undeployable
/// before battle, edits allowed during the deployment phase, the whole field accessible once battle starts, and realistic
/// asymmetry PRESERVED (the 20 m exit difference of the pre-F1 Meeting geometry is kept, not normalized).
/// Field 48 x 32 cells of 20,000 mm: L = 960,000 along x; thirds [0, 320,000) | [320,000, 640,000) | [640,000, 960,000).
/// Entity radius 300 mm.
/// </summary>
public class DeploymentThirdsTests
{
    private const long L = FieldW * CellMm;   // 960,000
    private const long R = 300;

    private static FormationSpec One(long fid, int side, long eid, Vec2L pos)
        => new(fid, side, UnitProfiles.HeavyInfantry, 1, pos) { Entities = new[] { new EntitySpec(eid, pos) } };

    private static BattleSetup Pair(Vec2L p0, Vec2L p1, SideSpec[]? sides = null, TerrainGrid? g = null)
        => TT.Setup(g ?? OpenField(), sides ?? DefaultSides(), 1, null, One(1, 0, 10, p0), One(2, 1, 20, p1));

    // ------------------------------------------------------------------ exact boundaries

    /// <summary>Half-open thirds by exact integer comparison (3u vs L, 2L): 319,999 | 320,000 and 639,999 | 640,000; and an L
    /// not divisible by 3 (47 cells: L = 940,000, L/3 = 313,333.3...): 313,333 is in the first third, 313,334 in the middle.</summary>
    [Fact]
    public void ThirdsAreHalfOpenAndExact()
    {
        Assert.Equal(0, DeploymentZones.ThirdOf(0, L));
        Assert.Equal(0, DeploymentZones.ThirdOf(319_999, L));
        Assert.Equal(1, DeploymentZones.ThirdOf(320_000, L));
        Assert.Equal(1, DeploymentZones.ThirdOf(639_999, L));
        Assert.Equal(2, DeploymentZones.ThirdOf(640_000, L));
        Assert.Equal(2, DeploymentZones.ThirdOf(959_999, L));
        Assert.Equal(0, DeploymentZones.ThirdOf(313_333, 940_000));
        Assert.Equal(1, DeploymentZones.ThirdOf(313_334, 940_000));
        Assert.Equal(1, DeploymentZones.ThirdOf(626_666, 940_000));
        Assert.Equal(2, DeploymentZones.ThirdOf(626_667, 940_000));
    }

    /// <summary>The WHOLE disc must be inside the own third. West side: 3(x + r) &lt; L, so x = 319,699 is accepted and x =
    /// 319,700 (disc edge touching 320,000) is refused as MiddleThird. East side: 3(x - r) &gt;= 2L: 640,300 accepted, 640,299
    /// refused. A disc reaching the enemy's third is EnemyThird. North/South use y and the same rule.</summary>
    [Fact]
    public void WholeDiscMustBeInsideTheOwnThird()
    {
        var g = OpenField();
        Assert.Equal(DeploymentRefusal.None, DeploymentZones.ZoneOf(g, ExitEdge.West, new Vec2L(319_699, 1), R));
        Assert.Equal(DeploymentRefusal.MiddleThird, DeploymentZones.ZoneOf(g, ExitEdge.West, new Vec2L(319_700, 1), R));
        Assert.Equal(DeploymentRefusal.None, DeploymentZones.ZoneOf(g, ExitEdge.East, new Vec2L(640_300, 1), R));
        Assert.Equal(DeploymentRefusal.MiddleThird, DeploymentZones.ZoneOf(g, ExitEdge.East, new Vec2L(640_299, 1), R));
        Assert.Equal(DeploymentRefusal.EnemyThird, DeploymentZones.ZoneOf(g, ExitEdge.West, new Vec2L(700_000, 1), R));
        Assert.Equal(DeploymentRefusal.EnemyThird, DeploymentZones.ZoneOf(g, ExitEdge.East, new Vec2L(100_000, 1), R));
        Assert.Equal(DeploymentRefusal.EnemyThird, DeploymentZones.ZoneOf(g, ExitEdge.West, new Vec2L(639_800, 1), R));   // straddles into the enemy third
        long H = FieldH * CellMm;                                                                                         // 640,000: thirds at 213,333.3 / 426,666.6
        Assert.Equal(DeploymentRefusal.None, DeploymentZones.ZoneOf(g, ExitEdge.North, new Vec2L(1, 213_033), R));   // 3 * 213,333 < 640,000
        Assert.Equal(DeploymentRefusal.MiddleThird, DeploymentZones.ZoneOf(g, ExitEdge.North, new Vec2L(1, 213_034), R));
        Assert.Equal(DeploymentRefusal.None, DeploymentZones.ZoneOf(g, ExitEdge.South, new Vec2L(1, 426_967), R));   // 3 * 426,667 >= 2H
        Assert.Equal(DeploymentRefusal.MiddleThird, DeploymentZones.ZoneOf(g, ExitEdge.South, new Vec2L(1, 426_966), R));
        Assert.Equal(640_000, H);
    }

    // ------------------------------------------------------------------ full check: deterministic refusal

    [Fact]
    public void CheckRefusesMiddleThirdImpassableOverlapAndUnopposedSides()
    {
        Assert.True(DeploymentZones.Check(Pair(Cell(5, 16), Cell(40, 16))).Accepted);
        var mid = DeploymentZones.Check(Pair(Cell(20, 16), Cell(40, 16)));
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.MiddleThird, 10), mid);
        var g = OpenField(); g.SetClass(5, 16, TerrainClass.Water);
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.NotPassable, 10), DeploymentZones.Check(Pair(Cell(5, 16), Cell(40, 16), g: g)));
        var ov = TT.Setup(OpenField(), DefaultSides(), 1, null, One(1, 0, 10, Cell(5, 16)), One(3, 0, 30, Cell(5, 16) + new Vec2L(500, 0)), One(2, 1, 20, Cell(40, 16)));
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.Overlap, 10), DeploymentZones.Check(ov));
        var same = new[] { new SideSpec(ExitEdge.West), new SideSpec(ExitEdge.West) };
        Assert.Equal(DeploymentRefusal.SidesNotOpposed, DeploymentZones.Check(Pair(Cell(5, 16), Cell(6, 20), same)).Refusal);
        var perp = new[] { new SideSpec(ExitEdge.West), new SideSpec(ExitEdge.North) };
        Assert.Equal(DeploymentRefusal.SidesNotOpposed, DeploymentZones.Check(Pair(Cell(5, 16), Cell(6, 2), perp)).Refusal);
    }

    /// <summary>The refusal is a pure function of the deployment: the FIRST failing entity by ascending id, whatever the
    /// order of formations and explicit entities in the input (tie-dense: three entities all in the middle third).</summary>
    [Fact]
    public void RefusalIsIndependentOfInputOrder()
    {
        FormationSpec F(long fid, long[] ids) => new(fid, 0, UnitProfiles.HeavyInfantry, ids.Length, Cell(20, 10))
        {
            Entities = Array.ConvertAll(ids, id => new EntitySpec(id, Cell(20, 4 + id % 20)))
        };
        var enemy = One(9, 1, 900, Cell(40, 16));
        var a = TT.Setup(OpenField(), DefaultSides(), 1, null, F(1, new long[] { 17, 5 }), F(2, new long[] { 11 }), enemy);
        var b = TT.Setup(OpenField(), DefaultSides(), 1, null, enemy, F(2, new long[] { 11 }), F(1, new long[] { 5, 17 }));
        var va = DeploymentZones.Check(a); var vb = DeploymentZones.Check(b);
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.MiddleThird, 5), va);
        Assert.Equal(va, vb);
    }

    // ------------------------------------------------------------------ the deployment phase

    [Fact]
    public void PhaseAcceptsEditsInsideTheOwnThirdAndRefusesTheMiddleThirdWithoutChangingAnything()
    {
        var phase = new DeploymentPhase(Meeting(UnitProfiles.HeavyInfantry, 10, UnitProfiles.HeavyInfantry, 10, 1));
        Assert.True(phase.Current().Accepted);
        var before = phase.Formations[0].Anchor;
        var v = phase.Propose(0, 1, Cell(20, 16));                                 // into the middle third
        Assert.Equal(DeploymentRefusal.MiddleThird, v.Refusal);
        Assert.Equal(1000, v.EntityId);                                            // generated id 1*1000 + slot 0 (the lowest id)
        Assert.Equal(before, phase.Formations[0].Anchor);                          // refused edit changed nothing
        Assert.Equal(DeploymentRefusal.NotOwnFormation, phase.Propose(1, 1, Cell(5, 16)).Refusal);
        Assert.Equal(DeploymentRefusal.UnknownFormation, phase.Propose(0, 77, Cell(5, 16)).Refusal);
        Assert.True(phase.Propose(0, 1, Cell(6, 10)).Accepted);
        Assert.Equal(Cell(6, 10), phase.Formations[0].Anchor);
        Assert.Equal(4, phase.Log.Count);                                          // every edit logged, accepted or refused
        Assert.Equal(new[] { DeploymentRefusal.MiddleThird, DeploymentRefusal.NotOwnFormation, DeploymentRefusal.UnknownFormation, DeploymentRefusal.None },
                     new[] { phase.Log[0].Verdict.Refusal, phase.Log[1].Verdict.Refusal, phase.Log[2].Verdict.Refusal, phase.Log[3].Verdict.Refusal });
        Assert.Equal(new long[] { 0, 1, 2, 3 }, new[] { phase.Log[0].Index, phase.Log[1].Index, phase.Log[2].Index, phase.Log[3].Index });
    }

    /// <summary>Close is refused while the proposal is invalid (the phase stays open), succeeds after a fix, yields explicit
    /// positions EXACTLY as deployed with Deployment = Thirds, and refuses every later edit (battle has started).</summary>
    [Fact]
    public void CloseRequiresAValidDeploymentAndThenLocksIt()
    {
        var s = Pair(Cell(20, 16), Cell(40, 16));                                  // side 0 starts in the middle third
        var phase = new DeploymentPhase(s);
        Assert.Equal(DeploymentRefusal.MiddleThird, phase.Close(out var none).Refusal);
        Assert.Null(none);
        Assert.False(phase.IsClosed);
        Assert.True(phase.Propose(0, 1, Cell(10, 16), new[] { new EntitySpec(10, Cell(10, 16)) }).Accepted);
        Assert.True(phase.Close(out var battle).Accepted);
        Assert.NotNull(battle);
        Assert.Equal(DeploymentRule.Thirds, battle!.Deployment);
        Assert.Equal(Cell(10, 16), battle.Formations[0].Entities![0].Pos);
        Assert.Equal(Cell(40, 16), battle.Formations[1].Entities![0].Pos);
        Assert.Equal(DeploymentRefusal.PhaseClosed, phase.Propose(0, 1, Cell(5, 16)).Refusal);
        Assert.Equal(DeploymentRefusal.PhaseClosed, phase.Close(out _).Refusal);
    }

    /// <summary>The battle re-checks a Thirds setup (defence in depth): a hand-built setup claiming Deployment = Thirds with an
    /// entity in the middle third is refused at construction with the same verdict.</summary>
    [Fact]
    public void BattleRefusesAThirdsSetupWithAMiddleThirdEntity()
    {
        var bad = Pair(Cell(24, 16), Cell(40, 16)) with { Deployment = DeploymentRule.Thirds };
        var e = Assert.Throws<ArgumentException>(() => new TacticalBattle(bad, PassiveController.Instance, PassiveController.Instance));
        Assert.Contains("MiddleThird", e.Message);
        Assert.Contains("entity 10", e.Message);
        // Unrestricted (unit-level probes) keeps accepting it.
        _ = new TacticalBattle(Pair(Cell(24, 16), Cell(40, 16)), PassiveController.Instance, PassiveController.Instance);
    }

    /// <summary>Materialising generated slots changes nothing: the battle closed from a phase without edits replays the
    /// generated-slot battle event for event (same trace hash, same result hash).</summary>
    [Fact]
    public void ClosedDeploymentReproducesTheGeneratedBattleExactly()
    {
        var s = Meeting(UnitProfiles.HeavyInfantry, 20, UnitProfiles.HeavyInfantry, 20, 4);
        Assert.True(new DeploymentPhase(s).Close(out var closed).Accepted);
        var a = TT.RunGuarded(new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()));
        var b = TT.RunGuarded(new TacticalBattle(closed!, new KnowledgeAi(), new KnowledgeAi()));
        Assert.Equal(a.TraceHash, b.TraceHash);
        Assert.Equal(a.ResultHash(), b.ResultHash());
    }

    // ------------------------------------------------------------------ after start: the whole field

    /// <summary>"Once deployment ends and battle starts, the entire battlefield becomes accessible": a deployed formation is
    /// ordered into the middle third and on into the enemy third; both orders are accepted and the anchor arrives.</summary>
    [Fact]
    public void AfterStartTheWholeFieldIsAccessible()
    {
        Assert.True(new DeploymentPhase(Pair(Cell(10, 16), Cell(40, 2))).Close(out var battle).Accepted);
        battle = battle! with { Sides = DefaultSides(new[] { Cell(36, 16) }, null) };
        var mv = new ScriptedController()
            .Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(24, 16)))
            .Add(5, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(36, 16)));   // before 5 idle pulses accrue after arrival (pulse 3)
        var hold = new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Hold));
        var b = new TacticalBattle(battle, mv, hold);
        b.RunUntilPulse(5);
        Assert.Equal(Cell(24, 16), b.Anchor(1));
        Assert.Equal(1, DeploymentZones.ThirdOf(b.Anchor(1).X, L));                 // in the middle third
        b.RunUntilPulse(12);
        Assert.Equal(Cell(36, 16), b.Anchor(1));
        Assert.Equal(2, DeploymentZones.ThirdOf(b.Anchor(1).X, L));                 // in the enemy's third
        Assert.All(b.Orders, o => Assert.True(o.Accepted));
    }

    // ------------------------------------------------------------------ asymmetry preserved (DIR5 §2.14, D-079 DR32)

    /// <summary>
    /// The pre-F1 Meeting geometry (anchors Cell(14,16) and Cell(34,16)) is a legal deployment and is kept EXACTLY: side 0 is
    /// 290,000 mm from its exit line, side 1 270,000 mm — the 20,000 mm (20 m) difference survives the deployment phase, the
    /// closed battle setup, and the battle's start positions; the engine's own exit-path metric differs by exactly one open
    /// cell (20,000 mm x cost 1000). Nothing is normalized. The mirrored Meeting is the control (difference 0).
    /// </summary>
    [Fact]
    public void TwentyMetreExitDifferenceIsPreserved()
    {
        var asym = MeetingAsymmetric(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, 1);
        var phase = new DeploymentPhase(asym);
        Assert.True(phase.Current().Accepted);
        Assert.True(phase.Close(out var closed).Accepted);
        var w = new EdgeField(closed!.Grid, closed.Costs, ExitEdge.West);
        var e = new EdgeField(closed.Grid, closed.Costs, ExitEdge.East);
        long ExitGap(BattleSetup s) => w.LineDistanceMm(s.Formations[0].Anchor) - e.LineDistanceMm(s.Formations[1].Anchor);
        Assert.Equal(290_000, w.LineDistanceMm(closed.Formations[0].Anchor));
        Assert.Equal(270_000 - 1, e.LineDistanceMm(closed.Formations[1].Anchor));   // East line convention: L - 1 - x
        Assert.Equal(20_000 + 1, ExitGap(closed));
        Assert.Equal(ExitGap(asym), ExitGap(closed));
        var c0 = closed.Grid.CellOf(closed.Formations[0].Anchor); var c1 = closed.Grid.CellOf(closed.Formations[1].Anchor);
        Assert.Equal(20_000L * 1000, w.DistanceAt(closed.Grid.Index(c0.X, c0.Y)) - e.DistanceAt(closed.Grid.Index(c1.X, c1.Y)));
        // The battle starts from exactly the deployed positions.
        var b = new TacticalBattle(closed, new KnowledgeAi(), new KnowledgeAi());
        var start = b.Entities();
        foreach (var f in closed.Formations)
            foreach (var es in f.Entities!)
                Assert.Equal(es.Pos, start.Single(x => x.Id == es.Id).Pos);
        // Control: the mirrored scenario has no gap.
        var mir = Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, 1);
        Assert.Equal(1, ExitGap(mir));                                             // only the half-open line convention's 1 mm
        // An edit that keeps the asymmetry (moving side 1 one more cell toward its exit) is accepted as given.
        var p2 = new DeploymentPhase(asym);
        Assert.True(p2.Propose(1, 2, Cell(35, 16)).Accepted);
        Assert.Equal(Cell(35, 16), p2.Formations[1].Anchor);
    }
}
