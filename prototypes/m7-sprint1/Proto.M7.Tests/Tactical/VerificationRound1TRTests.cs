using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// [TR] Verification round 1 (verify-TR-r1): one semantic test per reproduced finding (V1, V2, V4-V9, V11, V13; V3's tests are
/// in HiddenStationaryRuleTests and TerrainDomainInterplayTests, V10 is pinned by DeploymentThirdsTests, V12 is a documented
/// order). Every expected value below was MEASURED on the fixed tree and is DERIVED in the comment where a derivation exists.
/// Field: 48 x 32 cells of 20,000 mm, pulse 60 s, step 2 s; HI open-ground pace 1.3 m/s = 2,600 mm per step = 78 m per pulse.
/// </summary>
public class VerificationRound1TRTests
{
    private const long Guard = 100;

    private static FormationSpec One(long fid, int side, UnitProfile p, long eid, Vec2L pos)
        => new(fid, side, p, 1, pos) { Entities = new[] { new EntitySpec(eid, pos) } };

    private static ScriptedController Hold(params long[] formations)
    {
        var c = new ScriptedController();
        foreach (var f in formations) c.Add(0, new OrderRequest(f, OrderKind.Hold));
        return c;
    }

    // ------------------------------------------------------------------ V1: a hidden formation on a purposeful march is not stationary

    /// <summary>
    /// V1. Ten HI at Cell(3,1), hidden throughout (the only enemy holds at Cell(44,16), far beyond sight), ordered at pulse 0 to
    /// MoveTo Cell(3,30) — NOT an objective. The anchor advances 78 m per pulse (30,000 -&gt; 108,000 -&gt; ... -&gt; 576,000 mm at the
    /// pulse starts) and reaches 610,000 mm during pulse 7; every marching pulse sets a new goal minimum, so the idle counter
    /// stays 0 through pulse 7. Only after it stands still at its destination does the counter run 1..5 (ends of pulses 8-12):
    /// routed at the end of pulse 12 (13 pulses). Before the fix it was routed at the end of pulse 4 while still marching.
    /// </summary>
    [Fact]
    public void AHiddenFormationMarchingOnAMoveToOrderIsNotStationary()
    {
        var mv = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(3, 30)));
        var s = TT.Setup(OpenField(), DefaultSides(), 9, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(3, 1)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(44, 16)));
        var b = new TacticalBattle(s, mv, Hold(2));
        var y = new List<long>();
        var idle = new List<long>();
        b.Start();
        long seen = -1;
        while (b.Advance())
        {
            if (b.CurrentPulse != seen) { seen = b.CurrentPulse; y.Add(b.Anchor(1).Y); idle.Add(b.IdleCounter(1)); }
            if (b.CurrentPulse > Guard) Assert.Fail("HARNESS GUARD: non-termination is a FAILURE, not a rule");
        }
        var r = b.Result!;
        Assert.Equal(new long[] { 30_000, 108_000, 186_000, 264_000, 342_000, 420_000, 498_000, 576_000, 610_000 }, y.Take(9).ToArray());
        Assert.Equal(new long[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4 }, idle.ToArray());
        Assert.False(b.EverDetected(1, 1000));                                  // hidden throughout
        var v = Assert.Single(b.InactivityVerdicts, x => x.FormationId == 1);
        Assert.Equal(12, v.Pulse);
        Assert.True(v.Routed);
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(13, r.Pulses);
        Assert.Equal(10, r.Formations[0].RoutedEscaped);
        TT.AssertConserved(r);
    }

    /// <summary>V1, legacy model untouched: under LegacySideDisengage a MoveTo march to a non-objective point is still not
    /// activity (the march-progress minima are tracked under the DIR5 model only), so the hidden marcher's side disengages at
    /// the end of pulse 4 exactly as before round 1.</summary>
    [Fact]
    public void MarchProgressIsTrackedUnderTheDir5ModelOnly()
    {
        var mv = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(3, 30)));
        var s = TT.Setup(OpenField(), DefaultSides(), 9, TT.Legacy,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(3, 1)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(44, 16)));
        var r = TT.RunGuarded(new TacticalBattle(s, mv, Hold(2)), Guard);
        Assert.Equal(5, r.Pulses);
        Assert.True(r.Reason is BattleEndReason.SideDisengaged or BattleEndReason.BothDisengaged, r.Reason.ToString());
    }

    // ------------------------------------------------------------------ V2: the deployment phase judges the edited formation

    private static BattleSetup Three(Vec2L f1, Vec2L f2, Vec2L f3)
        => TT.Setup(OpenField(), DefaultSides(), 1, null,
            One(1, 0, UnitProfiles.HeavyInfantry, 10, f1), One(2, 1, UnitProfiles.HeavyInfantry, 20, f2), One(3, 0, UnitProfiles.HeavyInfantry, 30, f3));

    /// <summary>
    /// V2. Two side-0 formations start in the middle third (x = 400,000 and 500,000 mm; middle = [320,000, 640,000)). Each edit
    /// is judged on the EDITED formation against the others' current positions: moving formation 1 into the West third is
    /// accepted although formation 3 is still invalid; Close is refused naming formation 3's entity; the second edit repairs
    /// it and Close succeeds. Before the fix every single edit was refused (MiddleThird of the OTHER formation) — a deadlock.
    /// </summary>
    [Fact]
    public void TwoInvalidFormationsAreRepairedOneEditAtATime()
    {
        var ph = new DeploymentPhase(Three(Cell(20, 10), Cell(40, 16), Cell(25, 20)));
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.MiddleThird, 10), ph.Current());
        Assert.True(ph.Propose(0, 1, Cell(5, 10), new[] { new EntitySpec(10, Cell(5, 10)) }).Accepted);
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.MiddleThird, 30), ph.Close(out var none));
        Assert.Null(none);
        Assert.True(ph.Propose(0, 3, Cell(5, 20), new[] { new EntitySpec(30, Cell(5, 20)) }).Accepted);
        Assert.True(ph.Close(out var battle).Accepted);
        Assert.Equal(Cell(5, 10), battle!.Formations.Single(f => f.Id == 1).Entities![0].Pos);
        Assert.Equal(Cell(5, 20), battle.Formations.Single(f => f.Id == 3).Entities![0].Pos);
    }

    /// <summary>
    /// V2. One player's invalid placement does not block the other player's edits: side 0's formation 1 stands in the ENEMY
    /// (East) third, yet side 1 re-proposes its legal formation 2 and is accepted. An edit is still refused for its OWN
    /// failures, reported by its own entity id: into the middle third (MiddleThird, its id), or onto another entity — even an
    /// invalidly placed one (Overlap, its id). A refused edit changes nothing.
    /// </summary>
    [Fact]
    public void AnEditIsJudgedOnItsOwnPlacementOnly()
    {
        var ph = new DeploymentPhase(Three(Cell(40, 10), Cell(40, 16), Cell(5, 20)));
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.EnemyThird, 10), ph.Current());
        Assert.True(ph.Propose(1, 2, Cell(41, 16), new[] { new EntitySpec(20, Cell(41, 16)) }).Accepted);
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.MiddleThird, 30), ph.Propose(0, 3, Cell(18, 20), new[] { new EntitySpec(30, Cell(18, 20)) }));
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.Overlap, 20), ph.Propose(1, 2, Cell(40, 10), new[] { new EntitySpec(20, Cell(40, 10)) }));
        Assert.Equal(Cell(41, 16), ph.Formations.Single(f => f.Id == 2).Entities![0].Pos);
        Assert.Equal(Cell(5, 20), ph.Formations.Single(f => f.Id == 3).Entities![0].Pos);
    }

    // ------------------------------------------------------------------ V4: concealed by terrain = own cell OR terrain on the enemy's sight line

    /// <summary>
    /// V4. A lone HI (A) stands in the OPEN at Cell(10,16) behind a full-height obstacle column at x = 11 — a building (opaque
    /// layer) or a 10 m ridge (ground above the 1.7 m eye line) — with an enemy HI 40 m away at Cell(12,16), inside A's
    /// 79,200 mm engagement reach, holding. Neither sees the other. A is hidden by TERRAIN with an enemy in reach: exempt (a),
    /// not routed. Both formations are idle and exempt, so the battle ends by the whole-field Standoff at the end of pulse 4.
    /// (Before the fix A was routed: only its own cell's concealment counted.)
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HiddenBehindABuildingOrACrestWithAnEnemyInReachIsConcealedByTerrain(bool building)
    {
        var g = OpenField();
        for (int y = 0; y < FieldH; y++)
            if (building) g.SetClass(11, y, TerrainClass.Building);
            else g.Set(11, y, TerrainClass.Hills, 10_000);
        var s = TT.Setup(g, DefaultSides(), 5, null,
            One(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
            One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(12, 16)));
        var b = new TacticalBattle(s, PassiveController.Instance, Hold(2));
        var r = TT.RunGuarded(b, Guard);
        Assert.False(b.EverDetected(1, 10));
        Assert.False(b.EverDetected(0, 20));
        var v = Assert.Single(b.InactivityVerdicts, x => x.FormationId == 1);
        Assert.Equal(4, v.Pulse);
        Assert.Equal(InactivityExemption.ConcealedEnemyInReach, v.Exemptions);
        Assert.False(v.Routed);
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.Equal(5, r.Pulses);
        Assert.Equal(1, r.Formations[0].ActiveAtEnd);
    }

    /// <summary>
    /// V4 teeth (the concealment requirement of exemption (a)). A stands in the OPEN with a CLEAR line to an enemy 60 m away —
    /// inside A's reach — that does not see it only because its sight is 2 cells (40 m). A is hidden, but not by terrain: no
    /// exemption, routed. A detected the enemy at t = 0 (progress in pulse 0), so its counter reaches 5 at the end of pulse 5.
    /// (Mutant "drop the concealment condition": exempt, not routed.)
    /// </summary>
    [Fact]
    public void HiddenInTheOpenFromAShortSightedEnemyIsNotConcealment()
    {
        var s = TT.Setup(OpenField(), DefaultSides(), 5, null,
            One(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
            One(2, 1, TT.WithSight(UnitProfiles.HeavyInfantry, 2), 20, Cell(10, 16) + new Vec2L(60_000, 0)));
        var b = new TacticalBattle(s, PassiveController.Instance, Hold(2));
        var r = TT.RunGuarded(b, Guard);
        Assert.True(b.EverDetected(0, 20));
        Assert.False(b.EverDetected(1, 10));
        var v = Assert.Single(b.InactivityVerdicts, x => x.FormationId == 1);
        Assert.Equal(5, v.Pulse);
        Assert.Equal(InactivityExemption.None, v.Exemptions);
        Assert.True(v.Routed);
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(6, r.Pulses);
        Assert.Equal(1, r.Formations[0].RoutedEscaped);
    }

    // ------------------------------------------------------------------ V5: a shot blocked by a building is not activity

    /// <summary>
    /// V5. An archer (formation 1, explicit Hold) at Cell(10,16) and its target, an enemy HI at Cell(12,16) — exactly 40,000 mm,
    /// the archer's range — are separated by a building column at x = 11. A friendly HI observer at Cell(16,16) TRACKS the
    /// target from pulse 1 on, so the archer keeps firing (Fire events executed every step) but every shot is blocked by the
    /// building: no attack is counted, the archer's idle counter runs 1..5 at the ends of pulses 0-4, and with everybody
    /// exempt (explicit Holds) the field idles to a Standoff at the end of pulse 5. (Mutant "a blocked shot counts as an
    /// attack": the archer makes progress every pulse and the battle never ends — harness guard.)
    /// </summary>
    [Fact]
    public void ShotsBlockedByABuildingAreNotActivity()
    {
        var g = OpenField();
        TT.Fill(g, 11, 0, 11, FieldH - 1, TerrainClass.Building);
        var s = TT.Setup(g, DefaultSides(), 3, null,
            One(1, 0, UnitProfiles.Archers, 10, Cell(10, 16)),
            One(3, 0, UnitProfiles.HeavyInfantry, 30, Cell(16, 16)),
            One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(12, 16)));
        var b = new TacticalBattle(s, Hold(1, 3), Hold(2));
        var idle = new List<long>();
        b.Start();
        long seen = -1;
        while (b.Advance())
        {
            if (b.CurrentPulse != seen) { seen = b.CurrentPulse; idle.Add(b.IdleCounter(1)); }
            if (b.CurrentPulse > Guard) Assert.Fail("HARNESS GUARD: non-termination is a FAILURE, not a rule");
        }
        var r = b.Result!;
        Assert.True(b.IsTracked(0, 20));
        Assert.Equal(new long[] { 0, 1, 2, 3, 4, 5 }, idle.ToArray());
        Assert.True(Executed(b, TacticalKinds.Fire).Count(e => e.Source.Id == 10) > 0);    // it did try to shoot
        Assert.Equal(0, r.Formations.Single(f => f.FormationId == 1).AttacksMade);
        Assert.Equal(0, r.Formations.Single(f => f.FormationId == 2).DamageTaken);
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.Equal(6, r.Pulses);
    }

    // ------------------------------------------------------------------ V6: descent is free in the engine's kinematics

    /// <summary>V6 (mirror of AnchorClimbsSlowerExactly). Ground 5 m high for x &lt;= 10, 0 beyond: an HI anchor marching EAST
    /// from Cell(10,16) DOWN the step moves exactly 2,600 + 2,600 mm in its first two steps — the open-ground pace, Naismith's
    /// descent carries no cost. (Mutant "descent charged like ascent": 3,443 mm.)</summary>
    [Fact]
    public void AnAnchorDescendsAtTheFlatPaceExactly()
    {
        var g = OpenField();
        for (int y = 0; y < FieldH; y++) for (int x = 0; x <= 10; x++) g.Set(x, y, TerrainClass.Open, 5_000);
        var mv = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(13, 16)));
        var b = new TacticalBattle(TT.Setup(g, DefaultSides(), 1, null,
            One(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
            One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(40, 30))), mv, PassiveController.Instance);
        b.Start();
        while (b.Scheduler.TryPeekKey(out var k) && k.TimeMs <= 2_000 && !b.IsOver) b.Advance();
        Assert.Equal(2_600 + 2_600, b.Anchor(1).X - Cell(10, 16).X);
        Assert.Equal(Cell(10, 16).Y, b.Anchor(1).Y);
    }

    // ------------------------------------------------------------------ V7: cohesion follows the members as they move

    /// <summary>V7. Ten HI march from open ground into a forest (x = 13..30). Cohesion = mean OrderCap of the Active members'
    /// cells: One at construction (all in the open), exactly 1/2 after three pulses (all ten in forest cells, OrderCap 1/2).
    /// (Mutant "cohesion only at construction": stays One.)</summary>
    [Fact]
    public void CohesionIsRecomputedAsTheFormationMovesIntoAForest()
    {
        var g = OpenField();
        TT.Fill(g, 13, 0, 30, FieldH - 1, TerrainClass.Forest);
        var mv = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(20, 16)));
        var b = new TacticalBattle(TT.Setup(g, DefaultSides(), 1, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
            One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(46, 1))), mv, PassiveController.Instance);
        Assert.Equal(Q32.One, b.Cohesion(1));
        b.RunUntilPulse(3);
        Assert.All(b.Entities().Where(e => e.FormationId == 1), e => Assert.Equal(TerrainClass.Forest, g.ClassAt(g.CellOf(e.Pos).X, g.CellOf(e.Pos).Y)));
        Assert.Equal(Q32.FromRatio(1, 2), b.Cohesion(1));
    }

    // ------------------------------------------------------------------ V8: the shipped scenarios against the thirds rule

    /// <summary>
    /// V8 (a pin of the reported fact, not a rule change). Of the shipped battle setups, two violate the DIR5 §2.14 thirds:
    /// the parity Ambush (both orientations: side 1's AMBUSHERS stand in the middle third, entity 2000) and lane N's
    /// Amphibious(42) (the four defending shore formations 20-23 stand in the middle third, first entity 20000). Every other
    /// parity scenario, Meeting, MeetingAsymmetric and CombinedArms is a legal thirds deployment. They run because
    /// BattleSetup.Deployment defaults to Unrestricted. With the round-1 phase the amphibious defence is repaired by four edits
    /// (one per formation, into the East third) and the phase closes; the Ambush's side-0 player can edit its own legal
    /// formation although side 1's ambushers are illegal.
    /// </summary>
    [Fact]
    public void ShippedScenariosAgainstTheThirdsRule()
    {
        foreach (ParityScenarioId id in Enum.GetValues<ParityScenarioId>())
            foreach (var mirrored in new[] { false, true })
            {
                var v = DeploymentZones.Check(ParityScenarios.Build(id, 1, mirrored));
                if (id == ParityScenarioId.Ambush) Assert.Equal(new DeploymentVerdict(DeploymentRefusal.MiddleThird, 2000), v);
                else Assert.True(v.Accepted, $"{id} mirrored={mirrored}: {v}");
            }
        Assert.True(DeploymentZones.Check(Meeting(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, 1)).Accepted);
        Assert.True(DeploymentZones.Check(MeetingAsymmetric(UnitProfiles.HeavyInfantry, 50, UnitProfiles.HeavyInfantry, 50, 1)).Accepted);
        Assert.True(DeploymentZones.Check(CombinedArms(42)).Accepted);

        var amph = MultiDomainScenarios.Amphibious(42);
        Assert.Equal(DeploymentRule.Unrestricted, amph.Deployment);
        Assert.Equal(new DeploymentVerdict(DeploymentRefusal.MiddleThird, 20000), DeploymentZones.Check(amph));
        var ph = new DeploymentPhase(amph);
        long S = amph.Grid.CellSizeMm;
        Vec2L P(long cx, long cy) => new(cx * S + S / 2, cy * S + S / 2);
        Assert.True(ph.Propose(1, 20, P(52, 20)).Accepted);
        Assert.True(ph.Propose(1, 21, P(52, 6)).Accepted);
        Assert.True(ph.Propose(1, 22, P(52, 30)).Accepted);
        Assert.True(ph.Propose(1, 23, P(52, 36)).Accepted);
        Assert.True(ph.Close(out var closed).Accepted);
        Assert.Equal(DeploymentRule.Thirds, closed!.Deployment);

        var amb = ParityScenarios.Build(ParityScenarioId.Ambush, 1, false);
        Assert.Equal(Posture.Ambush, amb.Formations.Single(f => f.Id == 2).Posture);
        Assert.True(new DeploymentPhase(amb).Propose(0, 1, amb.Formations.Single(f => f.Id == 1).Anchor).Accepted);
    }

    // ------------------------------------------------------------------ V9: rivers and marsh do not touch missile effectiveness (limitation pin)

    /// <summary>
    /// V9 (pins a REPORTED LIMITATION so it cannot change silently). The missile equation p = clamp(A·E/(A·E+D))·T has no
    /// footing or cohesion term, and Marsh and Ford have missile transmission 1: an archer's hit chance on a target 20 m away
    /// is 75/120 whether the target or the shooter stands in Open, Marsh or Ford; a Forest target cell (transmission 3/4) is
    /// the control. Rivers and marsh act on MELEE only (footing 1/2 both ways, OrderCap 1/2 via cohesion).
    /// </summary>
    [Fact]
    public void MarshAndFordDoNotChangeTheMissileHitChance()
    {
        Q32 P(Action<TerrainGrid> terrain)
        {
            var g = OpenField();
            terrain(g);
            var b = new TacticalBattle(TT.Setup(g, DefaultSides(), 1, null,
                One(1, 0, UnitProfiles.Archers, 10, Cell(10, 16)),
                One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(11, 16))), PassiveController.Instance, PassiveController.Instance);
            return b.MissileHitChanceOf(10, 20, out _);
        }
        Q32 open = P(_ => { });
        Assert.Equal(Q32.FromRatio(75, 120), open);
        foreach (var c in new[] { TerrainClass.Marsh, TerrainClass.Ford })
        {
            Assert.Equal(open, P(g => g.SetClass(11, 16, c)));                // target in the marsh / ford
            Assert.Equal(open, P(g => g.SetClass(10, 16, c)));                // shooter in the marsh / ford
        }
        Assert.Equal(open.Mul(Q32.FromRatio(3, 4)), P(g => g.SetClass(11, 16, TerrainClass.Forest)));
    }

    // ------------------------------------------------------------------ V11 + V13: lane N's domain order and landing paths

    private const long CellD = 50_000;
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, CellD);
    private static readonly VesselClass T30 = DomainProfiles.SteamTransport with { Capacity = 30 };

    /// <summary>
    /// V11. Twenty riflemen (formation 2) on a coast, given an EXPLICIT Hold at pulse 0 (or no Hold — the control), then Embark
    /// (pulse 1, a lane-N domain order) and Disembark (pulse 3) to a point walled off from the beach: the landing finds no path
    /// and lane N sets an IMPLICIT Hold. Side 1 marches to its objective far away (progress, so the field never idles). The
    /// landed formation is hidden and idle and is judged at the end of pulse 10: with or without the earlier explicit Hold it
    /// has NO exemption and is routed — the accepted Embark cleared the explicit flag. (Before the fix the stale flag gave
    /// the formation that had held explicitly DefensiveHold: not routed.)
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnAcceptedDomainOrderClearsTheExplicitHold(bool holdFirst)
    {
        var g = Coast(CellD, 20);
        for (int y = 18; y <= 22; y++) for (int x = 38; x <= 42; x++) if (x == 38 || x == 42 || y == 18 || y == 22) g.Set(x, y, TerrainClass.Wall);
        var s = OnGrid(g, 1, null, null,
            Ship(1, 0, T30, C(19, 20)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, C(20, 20)),
            new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2))) with { Sides = MultiDomainScenarios.Sides(null, new[] { C(62, 38) }) };
        var c0 = new ScriptedController();
        if (holdFirst) c0.Add(0, new OrderRequest(2, OrderKind.Hold));
        c0.Add(1, new OrderRequest(2, OrderKind.Embark, 0, 1));
        c0.Add(3, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(40, 20)));
        var c1 = new ScriptedController().Add(0, new OrderRequest(9, OrderKind.MoveTo, 0, -1, C(62, 38)));
        var b = new TacticalBattle(s, c0, c1);
        b.RunUntilPulse(1);                                                         // pulse 1 started; its Embark not yet delivered
        Assert.Equal(holdFirst, b.HoldExplicit(2));                                 // explicit Hold in force before the Embark
        b.RunUntilPulse(5);
        Assert.False(b.HoldExplicit(2));                                            // cleared by the accepted Embark
        Assert.Equal(OrderKind.Hold, b.Directive(2));                               // lane N's implicit Hold (landing without a path)
        Assert.All(b.Orders.Where(o => o.Side == 0), o => Assert.Equal(OrderInvalid.None, o.Invalid));
        while (b.Advance()) if (b.CurrentPulse > 60) Assert.Fail("HARNESS GUARD: non-termination is a FAILURE, not a rule");
        var r = b.Result!;
        Assert.Equal(r.Personnel!.OpeningTotal, r.Personnel.Total);
        Assert.All(r.Formations, f => Assert.True(f.Conserved));
        var v = Assert.Single(b.InactivityVerdicts, x => x.FormationId == 2);
        Assert.Equal(10, v.Pulse);
        Assert.Equal(InactivityExemption.None, v.Exemptions);
        Assert.True(v.Routed);
        Assert.Equal(0, r.Formations.Single(f => f.FormationId == 2).ActiveAtEnd);
    }

    /// <summary>
    /// V13. Twenty riflemen march from Cell(24,20) to a transport, board it while still walking (12 of them board in a step
    /// in which they moved), and land again at pulse 3. A landing places an entity at its landing spot — it is not a run-up:
    /// every landed rifleman's LastStepMm is 0 at the instant it lands. (Before the fix 12 of them carried their last walking
    /// step, 900-3,900 mm, into the landing step's melee impetus term.)
    /// </summary>
    [Fact]
    public void ALandedEntityHasNoRunUp()
    {
        var s = OnGrid(Coast(CellD, 20), 1, null, null,
            Ship(1, 0, T30, C(19, 20)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, C(24, 20)),
            new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2)));
        var c0 = new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Embark, 0, 1)).Add(3, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(25, 20)));
        var b = new TacticalBattle(s, c0, Hold(9));
        b.Start();
        var ids = b.Entities().Where(e => e.FormationId == 2).Select(e => e.Id).ToArray();
        var prev = ids.Select(id => EntityState.Active).ToArray();
        int boardedMoving = 0, landed = 0;
        while (b.Advance() && b.CurrentPulse < 8)
        {
            var now = b.Entities();
            for (int i = 0; i < ids.Length; i++)
            {
                var st = now.Single(e => e.Id == ids[i]).State;
                if (prev[i] == EntityState.Active && st == EntityState.Embarked && b.LastStepMm(ids[i]) > 0) boardedMoving++;
                if (prev[i] == EntityState.Embarked && st == EntityState.Active) { landed++; Assert.Equal(0, b.LastStepMm(ids[i])); }
                prev[i] = st;
            }
        }
        Assert.Equal(12, boardedMoving);
        Assert.Equal(20, landed);
    }
}
