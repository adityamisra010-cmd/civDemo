using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// Fixes and semantic pins from the verify-D-tactical round (findings F1-F7; mutants Md, Mf, Mg, Mh, Mi, Mj, Ma).
/// Each test states the rule it pins and the mutant it is meant to kill. Expected values are derived in the comments,
/// not captured from a run. Every battle runs under the harness guard only (the engine has no pulse cap).
/// </summary>
public class VerificationFixDTests
{
    private static FormationSpec One(long fid, int side, UnitProfile p, long eid, Vec2L pos, long? morale = null)
        => new(fid, side, p, 1, pos) { Entities = new[] { new EntitySpec(eid, pos) }, Morale = morale };

    private static EntityView Ent(TacticalBattle b, long id) => b.Entities().Single(e => e.Id == id);

    // ---------------- F1: the Meeting scenario must be a mirror image about the field centre ----------------

    [Theory]
    [InlineData(50, 50)]
    [InlineData(60, 60)]
    [InlineData(10, 10)]
    public void MeetingAnchorsAreMirrorImagesAboutTheFieldCentre(int n0, int n1)
    {
        var s = Meeting(UnitProfiles.HeavyInfantry, n0, UnitProfiles.HeavyInfantry, n1, 1);
        var f0 = s.Formations.Single(f => f.Side == 0);
        var f1 = s.Formations.Single(f => f.Side == 1);
        Assert.Equal(ExitEdge.West, s.Sides[0].Exit);
        Assert.Equal(ExitEdge.East, s.Sides[1].Exit);
        long fieldW = FieldW * CellMm;                       // 960,000 mm
        // Geometric mirror: side 0's distance to the West line (x = 0) equals side 1's distance to the East line (x = 960,000).
        Assert.Equal(f0.Anchor.X - 0, fieldW - f1.Anchor.X);
        Assert.Equal(290_000, f0.Anchor.X);                   // Cell(14): 14*20,000 + 10,000
        Assert.Equal(670_000, f1.Anchor.X);                   // Cell(33): 960,000 - 290,000
        Assert.Equal(f0.Anchor.Y, f1.Anchor.Y);
        Assert.Equal(fieldW, f0.Anchor.X + f1.Anchor.X);      // anchor midpoint = field centre 480,000
        // Each side's objective is the other's start, so the objectives mirror too.
        Assert.Equal(new[] { f1.Anchor }, s.Sides[0].Objectives);
        Assert.Equal(new[] { f0.Anchor }, s.Sides[1].Objectives);
        // The engine's own exit-path metric agrees: equal integer path distance to each side's exit edge.
        var w = new EdgeField(s.Grid, s.Costs, ExitEdge.West);
        var e = new EdgeField(s.Grid, s.Costs, ExitEdge.East);
        var c0 = s.Grid.CellOf(f0.Anchor); var c1 = s.Grid.CellOf(f1.Anchor);
        Assert.Equal(w.DistanceAt(s.Grid.Index(c0.X, c0.Y)), e.DistanceAt(s.Grid.Index(c1.X, c1.Y)));
        // Distance from the anchor to the opponent stays beyond sight (12 cells): 19 cells.
        Assert.Equal(19 * CellMm, f1.Anchor.X - f0.Anchor.X);
        Assert.Equal(33, MirrorColumn(14));
    }

    // ---------------- F2: HitMin = 0 makes a contact battle non-terminating; Validate must refuse it ----------------

    [Fact]
    public void HitMinZeroIsRejectedByValidateAndByTheBattle()
    {
        Assert.Throws<ArgumentException>(() => new TacticalRules { HitMin = Q32.Zero, HitMax = Q32.Zero }.Validate());
        Assert.Throws<ArgumentException>(() => new TacticalRules { HitMin = Q32.Zero }.Validate());
        // The verifier's reproduction setup (two 5-entity HI formations in contact, both Attack) can no longer be built.
        var rules = new TacticalRules { HitMin = Q32.Zero, HitMax = Q32.Zero };
        var setup = TT.Setup(OpenField(), DefaultSides(), 3, rules,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 5, Cell(20, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 5, Cell(22, 16)));
        Assert.Throws<ArgumentException>(() => new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance));
        // Boundary: the smallest positive hit chance (raw 1 = 2^-32) is accepted; the defaults are accepted.
        new TacticalRules { HitMin = new Q32(1) }.Validate();
        new TacticalRules().Validate();
    }

    // ---------------- F3a / Md: surrender needs "wanted to move AND could not", not mere contact ----------------

    /// <summary>
    /// E (side 0) routs at the first Resolve (morale 299 &lt; 300) while an Active enemy W stands 1,000 mm EAST of it, i.e. in
    /// contact (2r + reach = 1,200). E had not tried to move at that step (it was Active and on its slot), and its way WEST
    /// to its exit is open, so it must flee and escape — not surrender. Mutant Md (surrender on contact alone) surrenders it
    /// at t = 0. A second side-0 formation far away keeps the battle open, so E's fate is decided by the rout rules only.
    /// </summary>
    [Fact]
    public void RoutingEntityInContactWithAnOpenRetreatDoesNotSurrender()
    {
        var rules = new TacticalRules();
        Vec2L c = Cell(20, 16);
        var setup = TT.Setup(OpenField(), DefaultSides(), 31, rules,
            One(1, 0, UnitProfiles.HeavyInfantry, 10, c, rules.RoutThreshold - 1),
            One(3, 0, UnitProfiles.HeavyInfantry, 30, Cell(5, 28)),
            One(2, 1, UnitProfiles.HeavyInfantry, 20, c + new Vec2L(1_000, 0)));
        var b = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        var r = TT.RunGuarded(b);
        var e = Ent(b, 10);
        Assert.Equal(EntityState.Escaped, e.State);
        Assert.True(e.RoutedEscape);
        var f1 = r.Formations.Single(f => f.FormationId == 1);
        Assert.Equal(0, f1.Surrendered);
        Assert.Equal(1, f1.RoutedEscaped);
        TT.AssertConserved(r);
    }

    /// <summary>
    /// Positive control for the same clause: W stands 900 mm WEST of E, on E's descent line to the West exit. E routs at
    /// t = 0, moves 300 mm at t = 2,000 (to touching), and at t = 4,000 wants to move, cannot, and W is in contact: E
    /// surrenders (W deals at most 3 x 10 HP by then, E has 50).
    /// </summary>
    [Fact]
    public void RoutingEntityBlockedByAnEnemyInContactSurrenders()
    {
        var rules = new TacticalRules();
        Vec2L c = Cell(20, 16);
        var setup = TT.Setup(OpenField(), DefaultSides(), 31, rules,
            One(1, 0, UnitProfiles.HeavyInfantry, 10, c, rules.RoutThreshold - 1),
            One(3, 0, UnitProfiles.HeavyInfantry, 30, Cell(5, 28)),
            One(2, 1, UnitProfiles.HeavyInfantry, 20, c - new Vec2L(900, 0)));
        var b = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        var r = TT.RunGuarded(b);
        Assert.Equal(EntityState.Surrendered, Ent(b, 10).State);
        Assert.Equal(1, r.Formations.Single(f => f.FormationId == 1).Surrendered);
        TT.AssertConserved(r);
    }

    // ---------------- F3b / Mf: elimination counts ACTIVE entities only ----------------

    /// <summary>
    /// Spec §4 termination (a): a side with no Active entity left — here all ten of its entities are Routing — ends the
    /// battle at that Resolve. Side 0 starts at morale 299 &lt; 300, routs at the first Resolve (t = 0), so the battle ends
    /// at t = 0 with all ten counted as routed-escaped survivors. Mutant Mf (count Active + Routing) keeps the battle running
    /// until the routers walk off the West edge (EndMs &gt; 0).
    /// </summary>
    [Fact]
    public void ASideLeftWithOnlyRoutingEntitiesIsEliminatedAtThatResolve()
    {
        var rules = new TacticalRules();
        var setup = TT.Setup(OpenField(), DefaultSides(), 32, rules,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)) { Morale = rules.RoutThreshold - 1 },
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(40, 16)));
        var r = TT.RunGuarded(new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance));
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(0, r.Side);
        Assert.Equal(1, r.HoldsField);
        Assert.Equal(0, r.EndMs);
        Assert.Equal(1, r.Pulses);
        var f1 = r.Formations[0];
        Assert.Equal(10, f1.RoutedEscaped);
        Assert.Equal(10, f1.Survivors);
        Assert.Equal(0, f1.Killed + f1.Surrendered);
        TT.AssertConserved(r);
    }

    // ---------------- F3c / Mg: damage is clamped to the HP that remains (no overkill in the books) ----------------

    /// <summary>
    /// Two attackers (Melee 50 => 10 damage per hit, hits certain by TEST INPUT) strike one 1-HP target at t = 0. The first
    /// Damage takes the target's only HP, the second finds 0 HP left: DamageTaken = DamageDealt = 1 exactly, and the dead
    /// target's HP is 0, not -19. Mutant Mg (dmg = ev.A) books 20 and leaves HP at -19.
    /// </summary>
    [Fact]
    public void OverkillIsClampedSoDamageBooksEqualHpLost()
    {
        var rules = new TacticalRules { HitMin = Q32.One, HitMax = Q32.One };          // TEST INPUT: certain hits
        var target = UnitProfiles.HeavyInfantry with { Integrity = 1, Melee = 0 };        // TEST INPUT: 1 HP, does not strike back
        Vec2L c = Cell(20, 16);
        var setup = TT.Setup(OpenField(), DefaultSides(), 33, rules,
            One(1, 0, UnitProfiles.HeavyInfantry, 10, c + new Vec2L(0, -1_000)),
            One(3, 0, UnitProfiles.HeavyInfantry, 30, c + new Vec2L(0, 1_000)),
            One(2, 1, target, 20, c));
        var b = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        var r = TT.RunGuarded(b);
        Assert.Equal(0, r.EndMs);
        var tf = r.Formations.Single(f => f.FormationId == 2);
        Assert.Equal(1, tf.Killed);
        Assert.Equal(1, tf.DamageTaken);
        long dealt = r.Formations.Where(f => f.Side == 0).Sum(f => f.DamageDealt);
        Assert.Equal(1, dealt);
        Assert.Equal(2, r.Formations.Where(f => f.Side == 0).Sum(f => f.AttacksMade));   // both attacked
        Assert.Equal(0, Ent(b, 20).Hp);
    }

    // ---------------- F6 / Mh: the rout threshold is strict (Morale < RoutThreshold) ----------------

    [Theory]
    [InlineData(0L, false)]    // morale == threshold: holds
    [InlineData(-1L, true)]    // morale == threshold - 1: routs at the first Resolve
    public void RoutThresholdIsStrict(long offset, bool routs)
    {
        var rules = new TacticalRules();
        var setup = TT.Setup(OpenField(), DefaultSides(), 34, rules,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)) { Morale = rules.RoutThreshold + offset },
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(40, 16)));
        var b = new TacticalBattle(setup, PassiveController.Instance, PassiveController.Instance);
        b.RunUntilPulse(1);
        if (routs)
        {
            Assert.True(b.IsOver);
            Assert.All(b.Entities().Where(e => e.Side == 0), e => Assert.True(e.State == EntityState.Escaped && e.RoutedEscape));
        }
        else
        {
            Assert.False(b.IsOver);
            Assert.Equal(1, b.CurrentPulse);
            Assert.All(b.Entities().Where(e => e.Side == 0), e => Assert.Equal(EntityState.Active, e.State));
        }
    }

    // ---------------- F4 / Mi: only the FIRST detection of an enemy entity is primary activity ----------------

    /// <summary>
    /// Termination bound "first detections, at most n per side". O (side 0) stands in the open at Cell(10,16); T (side 1)
    /// shuttles every pulse between Cell(21,16) (11 cells, inside both sights of 12 cells) and Cell(24,16) (14 cells,
    /// outside), so each side loses the other and RE-acquires it every second pulse, for 40 pulses. Re-acquisition is not new
    /// information: after the first detections nothing is primary (the shuttle sets no new minimum and nobody is concealed),
    /// so both sides disengage five idle pulses after the first approach. Mutant Mi (credit every re-acquisition) resets both
    /// counters every second pulse and the battle lives as long as the shuttle (40+ pulses).
    /// </summary>
    [Fact]
    public void ReacquiringALostEnemyIsNotNewActivity()
    {
        var shuttle = new ScriptedController();
        for (int p = 0; p < 40; p++)
            shuttle.Add(p, new OrderRequest(2, OrderKind.MoveTo, 0, -1, p % 2 == 0 ? Cell(21, 16) : Cell(24, 16)));
        var setup = TT.Setup(OpenField(), DefaultSides(), 35, null,
            One(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
            One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(24, 16)));
        var b = new TacticalBattle(setup, PassiveController.Instance, shuttle);
        b.Start();
        bool was0 = false, was1 = false;
        int acq0 = 0, acq1 = 0;
        while (b.Advance())
        {
            bool t0 = b.IsTracked(0, 20), t1 = b.IsTracked(1, 10);
            if (t0 && !was0) acq0++;
            if (t1 && !was1) acq1++;
            was0 = t0; was1 = t1;
            if (b.CurrentPulse > TT.HarnessMaxPulses) Assert.Fail("HARNESS GUARD");
        }
        var r = b.Result!;
        // The scenario really flickers: first acquisition plus at least two re-acquisitions per side before the end.
        Assert.True(acq0 >= 3, $"side 0 acquisitions {acq0}");
        Assert.True(acq1 >= 3, $"side 1 acquisitions {acq1}");
        Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
        Assert.InRange(r.Pulses, 5, 8);
        Assert.All(r.Formations, f => { Assert.Equal(0, f.Killed); Assert.Equal(1, f.DisengagedEscaped); });
    }

    // ---------------- F5 / Mj: EdgeField descent tie-break (composite key (dist + step, cell index)) ----------------

    /// <summary>
    /// Hand case: West exit, a single Wall at (9,16). From (10,16) the diagonals are corner-cut by the wall, so the only
    /// descent steps are N (10,15) and S (10,17), each with key 10*o + o (o = orth step on Open): an exact tie. The lower
    /// cell index, N, must win. Mutant Mj (higher index wins) picks S.
    /// </summary>
    [Fact]
    public void EdgeFieldDescentTieBreaksOnTheLowerCellIndex()
    {
        var g = OpenField();
        g.Set(9, 16, TerrainClass.Wall);
        var costs = TerrainCostTable.PlaceholderTestTable();
        var f = new EdgeField(g, costs, ExitEdge.West);
        int c = g.Index(10, 16), n = g.Index(10, 15), s = g.Index(10, 17);
        Assert.Equal(f.DistanceAt(n), f.DistanceAt(s));                 // the tie is real
        Assert.True(f.DistanceAt(n) < f.DistanceAt(c));
        Assert.Equal(n, f.NextCell(c));
        Assert.True(n < s);
    }

    /// <summary>
    /// Tie-dense oracle: on a field with a lattice of single-cell walls (every wall creates equal-cost detours), for all four
    /// exit edges, NextCell of EVERY reachable non-edge cell equals the brute-force argmin over legal descent steps of the
    /// composite key (dist[n] + step(c, n), n). The test also counts the cells where two or more candidates share the minimal
    /// key, and requires that ties are common (so the tie-break, not the cost, decides a large share of the steps).
    /// </summary>
    [Theory]
    [InlineData(ExitEdge.West)]
    [InlineData(ExitEdge.East)]
    [InlineData(ExitEdge.North)]
    [InlineData(ExitEdge.South)]
    public void EdgeFieldDescentMatchesTheCompositeKeyOracleOnATieDenseField(ExitEdge edge)
    {
        var g = new TerrainGrid(24, 16, CellMm, TerrainClass.Open);
        for (int y = 2; y < 16; y += 3) for (int x = 2; x < 24; x += 3) g.Set(x, y, TerrainClass.Wall);
        var costs = TerrainCostTable.PlaceholderTestTable();
        var pf = new GridPathfinder(g, costs);
        var f = new EdgeField(g, costs, edge);
        int W = g.Width, H = g.Height;
        long Cost(int x, int y) => costs.Cost(g.ClassAtIndex(y * W + x));
        int[] dx = { -1, 1, 0, 0, -1, 1, -1, 1 }, dy = { 0, 0, -1, 1, -1, -1, 1, 1 };
        int checkedCells = 0, tieCells = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int c = y * W + x;
                long dc = f.DistanceAt(c);
                if (Cost(x, y) == 0 || dc == EdgeField.Unreachable || dc == 0) { Assert.Equal(-1, f.NextCell(c)); continue; }
                long bestKey = long.MaxValue; int best = -1, atBest = 0;
                for (int k = 0; k < 8; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H || Cost(nx, ny) == 0) continue;
                    int ni = ny * W + nx;
                    if (f.DistanceAt(ni) >= dc) continue;
                    bool diag = dx[k] != 0 && dy[k] != 0;
                    if (diag && (Cost(nx, y) == 0 || Cost(x, ny) == 0)) continue;
                    long key = f.DistanceAt(ni) + (diag ? pf.DiagStep(Cost(x, y), Cost(nx, ny)) : pf.OrthStep(Cost(x, y), Cost(nx, ny)));
                    if (key < bestKey) { bestKey = key; best = ni; atBest = 1; }
                    else if (key == bestKey) { atBest++; if (ni < best) best = ni; }
                }
                Assert.Equal(best, f.NextCell(c));
                checkedCells++;
                if (atBest >= 2) tieCells++;
            }
        Assert.True(checkedCells > 250, $"checked {checkedCells}");
        Assert.True(tieCells >= 20, $"tie cells {tieCells}");
    }
}
