using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.Tests.Tactical;
using Proto.M7.War;
using Xunit.Abstractions;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Concealment;

/// <summary>
/// [AP] Field works on lane TR's battle grid (docs/m7-plan/concealment-actions-proposal.md §4.3) and the war-pulse engagement →
/// battle hand-off (§5): an entrenched defender's works become Earthwork cells (cover: missile transmission 1/4 for an entity in it)
/// under its formations and Ditch cells (footing and order 1/2) in front, at the works thresholds; lane TR's equations then act on
/// them unchanged (Law 2: the works are terrain, not a bonus). Every value is the PLACEHOLDER table or a TEST INPUT.
/// </summary>
public class FieldWorksBattleTests
{
    private readonly ITestOutputHelper _out;
    public FieldWorksBattleTests(ITestOutputHelper output) => _out = output;

    private static readonly ConcealmentRules R = ConcealmentRules.Proposal();

    private static BattleSetup Line(Vec2L defenderAnchor, int count = 10, TerrainClass under = TerrainClass.Open)
    {
        var g = OpenField();
        var c = g.CellOf(defenderAnchor);
        g.Set(c.X, c.Y, under);
        return new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(), new[]
        {
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, count, defenderAnchor),
        }, 1);
    }

    private static string Cells(TerrainGrid g)
    {
        var l = new List<string>();
        for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                if (g.ClassAt(x, y) is TerrainClass.Earthwork or TerrainClass.Ditch) l.Add($"{x},{y}:{g.ClassAt(x, y)}:{g.HeightAt(x, y)}");
        return string.Join(" ", l);
    }

    /// <summary>THRESHOLDS: below the cover threshold no cell changes; at it the cells under the defender's formation become
    /// Earthwork; at the obstacle threshold the cell in front of each (toward the enemy: west for the east-exiting side 1) becomes
    /// Ditch. A 10-entity block in one cell: 1 + 1 cells; a 50-entity block straddling a cell corner: 4 cover cells, 2 ditches (the
    /// cells in front of the rear cover cells are cover cells themselves). Heights are kept. The source grid is not modified.</summary>
    [Fact]
    public void Realize_PlacesCoverAndDitchesAtTheThresholds_UnderAndInFrontOfTheDefender()
    {
        var s = Line(Cell(33, 16));
        s.Grid.Set(33, 16, TerrainClass.Open, 5_000);
        foreach (var (works, cover, ditch, text) in new[]
        {
            (499_999L, 0, 0, ""), (500_000L, 1, 0, "33,16:Earthwork:5000"), (999_999L, 1, 0, "33,16:Earthwork:5000"),
            (1_000_000L, 1, 1, "32,16:Ditch:0 33,16:Earthwork:5000"),
        })
        {
            var g = FieldWorks.Realize(s, 1, works, R, out var placed);
            Assert.Equal((cover, ditch), (placed.CoverCells, placed.ObstacleCells));
            Assert.Equal(text, Cells(g));
            Assert.Equal("", Cells(s.Grid));                                         // a copy: the source grid is untouched
        }
        var big = Line(new Vec2L(679_000, 339_000), count: 50);
        var gb = FieldWorks.Realize(big, 1, ConcealmentRules.WorksFullPpm, R, out var pb);
        Assert.Equal((4, 2), (pb.CoverCells, pb.ObstacleCells));
        Assert.Equal("32,16:Ditch:0 33,16:Earthwork:0 34,16:Earthwork:0 32,17:Ditch:0 33,17:Earthwork:0 34,17:Earthwork:0", Cells(gb));
        var west = FieldWorks.Realize(Line(Cell(33, 16)), 0, ConcealmentRules.WorksFullPpm, R, out var pw);   // side 0 exits West: ditch to the east
        Assert.Equal("10,16:Earthwork:0 11,16:Ditch:0", Cells(west));
    }

    /// <summary>TIE-DENSE: two defender formations share a cell and each also covers its own; listing them in either order (and
    /// with their ids swapped) gives the same grid and the same counts — the works are a set of cells.</summary>
    [Fact]
    public void Realize_IsIndependentOfTheFormationsOrder()
    {
        // a (5 rows, cells 33 and 34) and b (cell 34 only) share cell 34
        var a = new FormationSpec(5, 1, UnitProfiles.HeavyInfantry, 50, new Vec2L(675_000, 330_000));
        var b = new FormationSpec(6, 1, UnitProfiles.HeavyInfantry, 50, new Vec2L(683_000, 330_000));
        var att = new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16));
        BattleSetup S(params FormationSpec[] f) => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(), f, 1);
        string? first = null;
        foreach (var setup in new[] { S(att, a, b), S(b, att, a), S(att, b with { Id = 5 }, a with { Id = 6 }) })
        {
            var g = FieldWorks.Realize(setup, 1, ConcealmentRules.WorksFullPpm, R, out var p);
            string text = $"{Cells(g)} / {p.CoverCells} {p.ObstacleCells}";
            first ??= text;
            Assert.Equal(first, text);
        }
        _out.WriteLine(first);
    }

    /// <summary>Only diggable ground takes works (FieldWorks.Diggable: Road, Open, Forest, Jungle, Hills): a defender in a forest
    /// cell gets an Earthwork (the canopy is cleared for the trench line); in a marsh nothing; a ditch is not dug into water.</summary>
    [Fact]
    public void Realize_DigsOnlyDiggableGround()
    {
        var forest = FieldWorks.Realize(Line(Cell(33, 16), under: TerrainClass.Forest), 1, ConcealmentRules.WorksFullPpm, R, out var pf);
        Assert.Equal((1, 1), (pf.CoverCells, pf.ObstacleCells));
        var marsh = FieldWorks.Realize(Line(Cell(33, 16), under: TerrainClass.Marsh), 1, ConcealmentRules.WorksFullPpm, R, out var pm);
        Assert.Equal((0, 0), (pm.CoverCells, pm.ObstacleCells));
        var s = Line(Cell(33, 16));
        s.Grid.Set(32, 16, TerrainClass.Water);
        var water = FieldWorks.Realize(s, 1, ConcealmentRules.WorksFullPpm, R, out var pw);
        Assert.Equal((1, 0), (pw.CoverCells, pw.ObstacleCells));
        Assert.Equal(TerrainClass.Water, water.ClassAt(32, 16));
        Assert.True(FieldWorks.Diggable(TerrainClass.Open) && FieldWorks.Diggable(TerrainClass.Hills) && FieldWorks.Diggable(TerrainClass.Road));
        Assert.False(FieldWorks.Diggable(TerrainClass.Earthwork) || FieldWorks.Diggable(TerrainClass.Ditch) || FieldWorks.Diggable(TerrainClass.Building)
                     || FieldWorks.Diggable(TerrainClass.Mountain) || FieldWorks.Diggable(TerrainClass.Wall) || FieldWorks.Diggable(TerrainClass.Ford));
    }

    /// <summary>Works are dug only under the defender's LAND formations that stand on the field: a defender air formation over open
    /// ground and a defender formation that starts embarked on a transport get none (aircraft do not dig; an embarked formation is not
    /// on the ground), nor does the attacker's formation; the land formation's cell and the ditch in front of it are the only works.
    /// [AP round 1, AP-R1-13: a mutant digging under every defender formation survived.]</summary>
    [Fact]
    public void Realize_DigsOnlyUnderTheDefendersLandFormationsOnTheGround()
    {
        var setup = new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(), new[]
        {
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 1, Cell(10, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 1, Cell(33, 16)),
            new FormationSpec(3, 1, UnitProfiles.HeavyInfantry, 1, Cell(36, 10)) { Domain = Domain.Air },
            new FormationSpec(4, 1, UnitProfiles.HeavyInfantry, 1, Cell(36, 20)) { EmbarkedOn = 9 },
        }, 1);
        var g = FieldWorks.Realize(setup, 1, ConcealmentRules.WorksFullPpm, R, out var p);
        Assert.Equal((1, 1), (p.CoverCells, p.ObstacleCells));
        Assert.Equal("32,16:Ditch:0 33,16:Earthwork:0", Cells(g));
    }

    // ---------------------------------------------------------------- the works act through lane TR's equations

    /// <summary>MECHANISM (cover): an archer's direct-fire hit chance on a heavy infantryman standing in an Earthwork cell is exactly
    /// the open-ground chance × the Earthwork's missile transmission 1/4 (the struck entity's own cell, lane TR's FirePath rule); the
    /// earthwork's concealment 1/2 is the cell multiplier the detection rule reads.</summary>
    [Fact]
    public void Earthwork_IsCoverInTheMissileEquation_Exactly()
    {
        Q32 Shot(TerrainClass targetCell)
        {
            var g = OpenField();
            g.Set(22, 16, targetCell);
            var s = new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(), new[]
            {
                new FormationSpec(1, 0, UnitProfiles.Archers, 1, Cell(20, 16)) { Entities = new[] { new EntitySpec(100, Cell(20, 16)) } },
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 1, Cell(22, 16)) { Entities = new[] { new EntitySpec(200, Cell(22, 16)) } },
            }, 1);
            var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
            var p = b.MissileHitChanceOf(100, 200, out long struck);
            Assert.Equal(200, struck);
            return p;
        }
        var open = Shot(TerrainClass.Open);
        var works = Shot(TerrainClass.Earthwork);
        Assert.True(open > Q32.Zero);
        Assert.Equal(open.Mul(Q32.FromRatio(1, 4)), works);
        Assert.Equal(Q32.FromRatio(1, 4), TerrainEffects.Placeholder.MissileTransmission(TerrainClass.Earthwork));
        Assert.Equal(Q32.FromRatio(1, 2), ConcealmentTable.PlaceholderTestTable().Multiplier(TerrainClass.Earthwork));
    }

    /// <summary>MECHANISM (obstacle): a heavy infantryman attacking from a Ditch cell strikes with footing 1/2 and his formation's
    /// cohesion c0 + (1 − c0) × 1/2 = 3/4 (attack factor 3/8, lane TR's ford value), and the defender in its Earthwork keeps footing
    /// and order 1: the engine's melee hit chance equals TerrainCombat.HitChance(50, 3/8, 45, 1) exactly; from open ground it is the
    /// factor-1 chance.</summary>
    [Fact]
    public void Ditch_IsAnObstacleInTheMeleeEquation_Exactly()
    {
        Q32 Blow(TerrainClass attackerCell)
        {
            var g = OpenField();
            g.Set(21, 16, attackerCell);
            g.Set(22, 16, TerrainClass.Earthwork);
            var s = new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), DefaultSides(), new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 1, new Vec2L(439_500, 330_000)) { Entities = new[] { new EntitySpec(100, new Vec2L(439_500, 330_000)) } },
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 1, new Vec2L(440_500, 330_000)) { Entities = new[] { new EntitySpec(200, new Vec2L(440_500, 330_000)) } },
            }, 1);
            return new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance).MeleeHitChanceOf(100, 200);
        }
        var fx = TerrainEffects.Placeholder;
        var r = new TacticalRules();
        Assert.Equal(TerrainCombat.HitChance(50, Q32.FromRatio(3, 8), 45, Q32.One, r.HitMin, r.HitMax), Blow(TerrainClass.Ditch));
        Assert.Equal(TerrainCombat.HitChance(50, Q32.One, 45, Q32.One, r.HitMin, r.HitMax), Blow(TerrainClass.Open));
        Assert.Equal((Q32.FromRatio(1, 2), Q32.FromRatio(1, 2)), (fx.Footing(TerrainClass.Ditch), fx.OrderCap(TerrainClass.Ditch)));
    }

    /// <summary>
    /// MATERIAL EFFECT IN BOTH RESOLVERS (MEASURED; lane P's hold-line-open: HI 50 assaulting HI 50 holding on open ground, seeds 1-8):
    /// with complete works (Earthwork under the line, a Ditch in front) the attacker loses more and the defender less, summed over the
    /// seeds, than on the same ground without works, and the defender wins no fewer battles — in the TacticalResolver AND in the
    /// AutoResolver (D-079 §4 D11: terrain influence "in both resolvers together"; the AutoResolver's model 2 evaluates the same
    /// TerrainEffects coefficients at the formations' points, lane P). The attacker in contact stands in the ditch (footing and order
    /// 1/2); the holder fights from firm ground. Magnitudes are the PLACEHOLDER table's and differ between the resolvers (printed; a
    /// parity matter for lane P, not tuned here); the test pins the direction. [AP round 1, AP-R1-05: the AutoResolver side was
    /// neither measured nor tested.]
    /// </summary>
    [Fact]
    public void AnEntrenchedLine_FaresMateriallyBetter()
    {
        foreach (bool auto in new[] { false, true })
        {
            long attLossBare = 0, defLossBare = 0, attLossWorks = 0, defLossWorks = 0;
            int heldBare = 0, heldWorks = 0;
            for (ulong seed = 1; seed <= 8; seed++)
            {
                var bare = ParityBatteryP.Build(PScenario.HoldLineOpen, seed);
                var dug = bare with { Grid = FieldWorks.Realize(bare, 1, ConcealmentRules.WorksFullPpm, R, out var p) };
                Assert.True(p.CoverCells >= 1 && p.ObstacleCells >= 1);
                BattleResult rb, rw;
                if (auto) { rb = new AutoBattle(bare, new AutoCoefficients()).Run(); rw = new AutoBattle(dug, new AutoCoefficients()).Run(); }
                else { rb = new TacticalBattle(bare, new KnowledgeAi(), new KnowledgeAi()).Run(); rw = new TacticalBattle(dug, new KnowledgeAi(), new KnowledgeAi()).Run(); }
                TT.AssertConserved(rb); TT.AssertConserved(rw);
                attLossBare += rb.Formations.Where(f => f.Side == 0).Sum(f => f.PersonnelLoss);
                defLossBare += rb.Formations.Where(f => f.Side == 1).Sum(f => f.PersonnelLoss);
                attLossWorks += rw.Formations.Where(f => f.Side == 0).Sum(f => f.PersonnelLoss);
                defLossWorks += rw.Formations.Where(f => f.Side == 1).Sum(f => f.PersonnelLoss);
                if (rb.Winner == 1) heldBare++;
                if (rw.Winner == 1) heldWorks++;
            }
            string who = auto ? "AutoResolver" : "TacticalResolver";
            _out.WriteLine($"{who} hold-line-open seeds 1-8: attacker lost {attLossBare} bare / {attLossWorks} with works; defender lost {defLossBare} / {defLossWorks}; defender won {heldBare} / {heldWorks}");
            Assert.True(attLossWorks > attLossBare, $"{who}: attacker losses {attLossWorks} vs {attLossBare}");
            Assert.True(defLossWorks < defLossBare, $"{who}: defender losses {defLossWorks} vs {defLossBare}");
            Assert.True(heldWorks >= heldBare, who);
        }
    }

    // ---------------------------------------------------------------- the engagement -> battle hand-off

    /// <summary>The cells "x,y" under a side's entities at the battle's start, read from the engine's own placement (a battle built on
    /// the setup and not run), sorted.</summary>
    private static string[] CellsUnder(BattleSetup s, int side)
    {
        var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
        return b.Entities().Where(e => e.Side == side).Select(e => s.Grid.CellOf(e.Pos)).Select(c => $"{c.X},{c.Y}")
                .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static string[] CellsOf(TerrainGrid g, TerrainClass cls)
    {
        var l = new List<string>();
        for (int y = 0; y < g.Height; y++) for (int x = 0; x < g.Width; x++) if (g.ClassAt(x, y) == cls) l.Add($"{x},{y}");
        return l.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    /// <summary>The hand-off (EngagementBattle.Build): the target's side is the Defender (READING AP-1 (a)); an ambush makes the
    /// attacker's side the Ambusher and carries its camouflage into the battle as lane N's personal concealment × CamouflageFactor on
    /// its formations' profiles (identity, caps and every other stat unchanged); the target's works at the engagement instant become
    /// cells UNDER THE TARGET'S SIDE: the Earthwork cells are exactly the cells the defender's entities stand on, none is under the
    /// attacker, and each Ditch lies one cell in front of an Earthwork toward the attacker (west of side 1, east of side 0; checked
    /// with the target on either side). A visible attack sets no Ambusher and changes no profile; works below the cover threshold
    /// change no cell. [AP round 1, AP-R1-02: the works were checked only by their placement counts, equal for both sides.]</summary>
    [Fact]
    public void EngagementBattle_CarriesTheRolesTheWorksAndTheCamouflage()
    {
        var field = ParityBatteryP.Build(PScenario.HoldLineOpen, 1) with { Defender = -1 };
        var amb = new EngagementRecord(7, 1_000, 0, 1, 0, 1, true, ConcealKind.Camouflaged | ConcealKind.Hidden, ConcealmentRules.WorksFullPpm, 12);
        var s = EngagementBattle.Build(field, amb, attackerSide: 0, R, out var placed);
        Assert.Equal((1, 0), (s.Defender, s.Ambusher));
        Assert.Equal((1, 1), (placed.CoverCells, placed.ObstacleCells));
        foreach (int attacker in new[] { 0, 1 })
        {
            int target = 1 - attacker;
            var sa = EngagementBattle.Build(field, amb, attackerSide: attacker, R, out _);
            string[] works = CellsOf(sa.Grid, TerrainClass.Earthwork), ditch = CellsOf(sa.Grid, TerrainClass.Ditch);
            Assert.Equal(CellsUnder(field, target), works);                      // exactly under the defender's entities
            Assert.Empty(works.Intersect(CellsUnder(field, attacker)));
            Assert.NotEmpty(ditch);
            int toward = target == 1 ? -1 : 1;                                   // side 1 exits East: the enemy is to the west
            foreach (var d in ditch)
            {
                var xy = d.Split(',');
                Assert.Contains($"{long.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture) - toward},{xy[1]}", works);
            }
            Assert.Equal((target, attacker), (sa.Defender, sa.Ambusher));
        }
        Assert.Equal(new[] { "32,16" }, CellsOf(s.Grid, TerrainClass.Earthwork));          // the measured cells (seed 1)
        Assert.Equal(new[] { "31,16" }, CellsOf(s.Grid, TerrainClass.Ditch));
        var a0 = s.Formations.Single(f => f.Side == 0); var f0 = field.Formations.Single(f => f.Side == 0);
        var a1 = s.Formations.Single(f => f.Side == 1); var f1 = field.Formations.Single(f => f.Side == 1);
        Assert.Equal(Q32.FromRatio(1, 2), a0.Profile.PersonalConcealment);
        Assert.Equal(f0.Profile with { PersonalConcealment = Q32.One }, a0.Profile with { PersonalConcealment = Q32.One });
        Assert.Equal(f1.Profile, a1.Profile);
        var vis = amb with { Ambush = false, AttackerStates = ConcealKind.None, TargetWorksPpm = 499_999 };
        var sv = EngagementBattle.Build(field, vis, attackerSide: 0, R, out var pv);
        Assert.Equal((1, -1, 0, 0), (sv.Defender, sv.Ambusher, pv.CoverCells, pv.ObstacleCells));
        Assert.Equal(field.Formations, sv.Formations);
        var hideOnly = amb with { AttackerStates = ConcealKind.Hidden };   // a hide is not carried (the stationary factor already acts)
        Assert.Equal(f0.Profile, EngagementBattle.Build(field, hideOnly, 0, R, out _).Formations.Single(f => f.Side == 0).Profile);
        // the battle runs on the realized grid, and its reported winner follows the rule with the hand-off's roles
        var r = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()).Run();
        TT.AssertConserved(r);
        Assert.Equal(EqualSides.Decide(r.Reason, r.Side, 1, 0, false), (r.Winner, r.WinBasis));
    }

    /// <summary>DETECTION INTERPLAY in battle (common random numbers): a formation carried in camouflaged (personal concealment × 1/2)
    /// is first detected no earlier than the same formation uncamouflaged, in both resolvers, on lane P's jungle-ambush (the waiting
    /// ambusher, side 1), tuning seeds 1-8 (tactical) and 1-20 (auto): a lower per-draw chance against the same counter draws can only
    /// delay the first success while the two runs are identical before it.</summary>
    [Fact]
    public void CarriedCamouflage_NeverHastensTheFirstDetection_InBothResolvers()
    {
        static BattleSetup Camo(BattleSetup s) => s with
        {
            Formations = s.Formations.Select(f => f.Side == 1 ? f with { Profile = f.Profile with { PersonalConcealment = f.Profile.PersonalConcealment.Mul(Q32.FromRatio(1, 2)) } } : f).ToArray(),
        };
        int later = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var s = ParityBatteryP.Build(PScenario.JungleAmbush, seed);
            var plain = new AutoBattle(s, new AutoCoefficients()); plain.Run();
            var camo = new AutoBattle(Camo(s), new AutoCoefficients()); camo.Run();
            long a = plain.FirstKnownStep(0, 2), b = camo.FirstKnownStep(0, 2);    // -1 = never known
            long na = a < 0 ? long.MaxValue : a, nb = b < 0 ? long.MaxValue : b;  // never = later than any step
            Assert.True(nb >= na, $"auto seed {seed}: camouflaged first known at {b}, plain at {a}");
            if (nb > na) later++;
        }
        for (ulong seed = 1; seed <= 8; seed++)
        {
            long First(BattleSetup s)
            {
                var b = new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi());
                b.Start();
                long ids0 = 2000, idsN = 2049;   // the ambusher's generated entity ids (formation 2: 2 x 1000 + slot)
                while (b.Advance())
                    for (long id = ids0; id <= idsN; id++)
                        if (b.EverDetected(0, id)) return b.Scheduler.NowMs;
                return long.MaxValue;
            }
            var s = ParityBatteryP.Build(PScenario.JungleAmbush, seed);
            long p0 = First(s), p1 = First(Camo(s));
            Assert.True(p1 >= p0, $"tactical seed {seed}: {p1} < {p0}");
            if (p1 > p0) later++;
        }
        _out.WriteLine($"first detection later with camouflage in {later} of 28 runs");
        Assert.True(later > 0);
    }
}
