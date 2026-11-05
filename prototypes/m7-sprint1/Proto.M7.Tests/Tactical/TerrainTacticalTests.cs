using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// [TR] Terrain in the TacticalResolver (DIR5 §2.7): every coefficient of TerrainCombat is checked (1) as a pure equation,
/// (2) WIRED in the engine with exact expected hit chances re-derived here by independent Int128 arithmetic, and (3) for
/// slope and river footing, as a material influence on battle outcomes over fixed seeds, each against a flat/open control.
/// Field: TacticalScenarios.OpenField (48 x 32 cells of 20,000 mm). HI = melee 50 / defence 45; archers ranged 75.
/// </summary>
public class TerrainTacticalTests
{
    private const long One32 = Q32.OneRaw;

    private static FormationSpec One(long fid, int side, UnitProfile p, long eid, Vec2L pos)
        => new(fid, side, p, 1, pos) { Entities = new[] { new EntitySpec(eid, pos) } };

    /// <summary>Independent reference: floor(A*fa / (A*fa + D*fd) * 2^32) clamped to the default [1/20, 19/20].</summary>
    private static Q32 Ref(long a, long faRaw, long d, long fdRaw)
    {
        Int128 an = (Int128)a * faRaw, dn = (Int128)d * fdRaw;
        long p = (long)(an * One32 / (an + dn));
        var r = new TacticalRules();
        return new Q32(Math.Clamp(p, r.HitMin.Raw, r.HitMax.Raw));
    }

    private static long Mul(long a, long b) => (long)((Int128)a * b / One32);   // Q32 product, floor (non-negative)

    private static void StepUntil(TacticalBattle b, long tMs)
    {
        b.Start();
        while (b.Scheduler.TryPeekKey(out var k) && k.TimeMs <= tMs && !b.IsOver) b.Advance();
    }

    // ------------------------------------------------------------------ (1) identity

    /// <summary>With every factor One (flat open ground, no impetus) the terrain hit chance IS the Slice D hit chance for every
    /// (attack, defence) in [0, 120]^2 — the reason the open-field goldens do not move.</summary>
    [Fact]
    public void HitChanceIsTheSliceDValueWhenEveryFactorIsOne()
    {
        var r = new TacticalRules();
        var fx = TerrainEffects.Placeholder;
        Q32 fa = TerrainCombat.MeleeAttackFactor(fx, 0, Q32.One, TerrainClass.Open, Q32.One);
        Q32 fd = TerrainCombat.MeleeDefenceFactor(fx, TerrainClass.Road, Q32.One);
        Assert.Equal(Q32.One, fa);
        Assert.Equal(Q32.One, fd);
        Assert.Equal(Q32.One, TerrainCombat.MissileElevationFactor(fx, 0));
        for (long a = 0; a <= 120; a++)
            for (long d = 0; d <= 120; d++)
            {
                Q32 legacy = a + d <= 0 ? Q32.Zero : Q32.FromRatio(a, a + d);
                if (legacy < r.HitMin) legacy = r.HitMin;
                if (legacy > r.HitMax) legacy = r.HitMax;
                Assert.Equal(legacy, TerrainCombat.HitChance(a, fa, d, fd, r.HitMin, r.HitMax));
            }
    }

    // ------------------------------------------------------------------ (2) slope (melee)

    /// <summary>
    /// Melee across a 5 m step between adjacent 20 m cells: g = 5,000/20,000 = 1/4 (exact Q32 2^30). Uphill attacker:
    /// S = 1 - (1/2)(1/4) = 7/8 -> p = 43.75/88.75; downhill: S = 9/8 -> p = 56.25/101.25; same blow on the flat: 50/95.
    /// Entities 1,000 mm apart across the cell boundary (in contact: 2r + reach = 1,200).
    /// </summary>
    [Fact]
    public void MeleeSlopeCoefficientIsWiredExactly()
    {
        TacticalBattle Make(long dh)
        {
            var g = OpenField();
            for (int y = 0; y < FieldH; y++) for (int x = 11; x < FieldW; x++) g.Set(x, y, TerrainClass.Open, dh);
            return new TacticalBattle(TT.Setup(g, DefaultSides(), 1, null,
                One(1, 0, UnitProfiles.HeavyInfantry, 10, new Vec2L(219_500, 330_000)),
                One(2, 1, UnitProfiles.HeavyInfantry, 20, new Vec2L(220_500, 330_000))), PassiveController.Instance, PassiveController.Instance);
        }
        var hill = Make(5_000);
        Assert.Equal(Ref(50, 7 * One32 / 8, 45, One32), hill.MeleeHitChanceOf(10, 20));   // attacking uphill
        Assert.Equal(Ref(50, 9 * One32 / 8, 45, One32), hill.MeleeHitChanceOf(20, 10));   // striking downhill
        var flat = Make(0);
        Assert.Equal(Q32.FromRatio(50, 95), flat.MeleeHitChanceOf(10, 20));
        Assert.True(hill.MeleeHitChanceOf(10, 20) < flat.MeleeHitChanceOf(10, 20));
        Assert.True(hill.MeleeHitChanceOf(20, 10) > flat.MeleeHitChanceOf(20, 10));
        // Cap: a 40 m cliff (g = 2) is clamped to 1 -> S = 1/2.
        Assert.Equal(Ref(50, One32 / 2, 45, One32), Make(40_000).MeleeHitChanceOf(10, 20));
    }

    // ------------------------------------------------------------------ footing + cohesion

    /// <summary>
    /// Attacker standing in a FORD (footing 1/2; its formation's cohesion = order cap 1/2 -> K = 1/2 + 1/2*1/2 = 3/4) against
    /// a defender on the open bank: factor 1/2 * 3/4 = 3/8 exactly -> p = 18.75/63.75. Marsh: the same coefficients. Both
    /// in the ford: attacker 3/8, defender 3/8 -> the ratio is the open-ground 50/95 (footing and order are relative).
    /// </summary>
    [Fact]
    public void FootingAndCohesionAreWiredExactly()
    {
        TacticalBattle Make(TerrainClass atk, TerrainClass def)
        {
            var g = OpenField();
            g.SetClass(10, 16, atk); g.SetClass(11, 16, def);
            return new TacticalBattle(TT.Setup(g, DefaultSides(), 1, null,
                One(1, 0, UnitProfiles.HeavyInfantry, 10, new Vec2L(219_500, 330_000)),
                One(2, 1, UnitProfiles.HeavyInfantry, 20, new Vec2L(220_500, 330_000))), PassiveController.Instance, PassiveController.Instance);
        }
        long threeEighths = 3 * One32 / 8;
        var ford = Make(TerrainClass.Ford, TerrainClass.Open);
        Assert.Equal(Q32.FromRatio(1, 2), ford.Cohesion(1));
        Assert.Equal(Q32.One, ford.Cohesion(2));
        Assert.Equal(Ref(50, threeEighths, 45, One32), ford.MeleeHitChanceOf(10, 20));
        Assert.Equal(Ref(50, One32, 45, threeEighths), ford.MeleeHitChanceOf(20, 10));       // the bank strikes at a footing-less defender
        Assert.Equal(Ref(50, threeEighths, 45, One32), Make(TerrainClass.Marsh, TerrainClass.Open).MeleeHitChanceOf(10, 20));
        Assert.Equal(Q32.FromRatio(50, 95), Make(TerrainClass.Ford, TerrainClass.Ford).MeleeHitChanceOf(10, 20));
        // Forest: footing 1 but order cap 1/2 -> cohesion factor 3/4 only.
        Assert.Equal(Ref(50, 3 * One32 / 4, 45, One32), Make(TerrainClass.Forest, TerrainClass.Open).MeleeHitChanceOf(10, 20));
    }

    /// <summary>Formation cohesion is the mean order cap of its ACTIVE members' cells: 10 entities, 5 in jungle (1/4) and 5 in
    /// the open (1): (5/4 + 5)/10 = 5/8 exactly (Q32 floor of the mean of raw values).</summary>
    [Fact]
    public void CohesionIsTheMeanOrderCapOfTheMembers()
    {
        var g = OpenField();
        TT.Fill(g, 10, 0, 10, FieldH - 1, TerrainClass.Jungle);
        var es = new EntitySpec[10];
        for (int k = 0; k < 10; k++) es[k] = new EntitySpec(100 + k, new Vec2L(k < 5 ? 210_000 : 230_000, 100_000 + k * 2_000));
        var b = new TacticalBattle(TT.Setup(g, DefaultSides(), 1, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, new Vec2L(220_000, 110_000)) { Entities = es },
            One(2, 1, UnitProfiles.HeavyInfantry, 900, Cell(40, 16))), PassiveController.Instance, PassiveController.Instance);
        Assert.Equal(new Q32((5 * (One32 / 4) + 5 * One32) / 10), b.Cohesion(1));
        Assert.Equal(Q32.FromRatio(5, 8), b.Cohesion(1));
    }

    // ------------------------------------------------------------------ charge impetus

    /// <summary>Pure: 1 + chi * min(1, moved/ref); chi 0 or no move -> exactly One.</summary>
    [Fact]
    public void ImpetusFactorEquation()
    {
        Q32 half = Q32.FromRatio(1, 2);
        Assert.Equal(new Q32(One32 + One32 / 2), TerrainCombat.ImpetusFactor(half, 3_900, 3_900));
        Assert.Equal(new Q32(One32 + One32 / 2), TerrainCombat.ImpetusFactor(half, 9_000, 3_900));      // capped at the sprint
        Assert.Equal(new Q32(One32 + half.Mul(Q32.FromRatio(1_300, 3_900)).Raw), TerrainCombat.ImpetusFactor(half, 1_300, 3_900));
        Assert.Equal(Q32.One, TerrainCombat.ImpetusFactor(half, 0, 3_900));
        Assert.Equal(Q32.One, TerrainCombat.ImpetusFactor(Q32.Zero, 3_900, 3_900));
    }

    /// <summary>
    /// A TEST-INPUT shock profile (HI with Impetus 1/2) attacks an enemy 30 m away. Its first run-up step (t = 2 s) covers the
    /// full open-ground sprint 1,300 * 3/2 * 2 = 3,900 mm on Open, but only 1,300 mm out of a Marsh cell (cost 3000): the
    /// impetus shrinks by MECHANISM (the run-up), and in the marsh footing 1/2 and cohesion 3/4 apply on top.
    /// Open: factor 3/2 -> p = 75/120 = 5/8. Marsh: (1 + 1/2*floor(2^32/3)/2^32) * 1/2 * 3/4 (floor at each product).
    /// </summary>
    [Fact]
    public void ChargeImpetusShrinksWithTheTerrainSlowedRunUp()
    {
        var shock = UnitProfiles.HeavyInfantry with { Name = "test/shock", Impetus = Q32.FromRatio(1, 2) };   // TEST INPUT
        TacticalBattle Make(TerrainClass ground)
        {
            var g = OpenField();
            g.SetClass(10, 16, ground);
            var atk = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.Attack, 0, 2));
            return new TacticalBattle(TT.Setup(g, DefaultSides(), 1, null,
                One(1, 0, shock, 10, new Vec2L(210_000, 330_000)),
                One(2, 1, UnitProfiles.HeavyInfantry, 20, new Vec2L(240_000, 330_000))), atk, PassiveController.Instance);
        }
        var open = Make(TerrainClass.Open);
        StepUntil(open, 2_000);
        Assert.Equal(3_900, open.LastStepMm(10));
        Assert.Equal(Ref(50, 3 * One32 / 2, 45, One32), open.MeleeHitChanceOf(10, 20));
        Assert.Equal(Q32.FromRatio(5, 8), open.MeleeHitChanceOf(10, 20));

        var marsh = Make(TerrainClass.Marsh);
        StepUntil(marsh, 2_000);
        Assert.Equal(1_300, marsh.LastStepMm(10));
        long imp = One32 + Mul(One32 / 2, Q32.FromRatio(1_300, 3_900).Raw);
        long fa = Mul(Mul(imp, One32 / 2), 3 * One32 / 4);
        Assert.Equal(Ref(50, fa, 45, One32), marsh.MeleeHitChanceOf(10, 20));
        // Control: the same marsh run-up WITHOUT impetus only pays footing and cohesion.
        Assert.True(marsh.MeleeHitChanceOf(10, 20) > Ref(50, 3 * One32 / 8, 45, One32));
        Assert.True(marsh.MeleeHitChanceOf(10, 20) < open.MeleeHitChanceOf(10, 20));
    }

    // ------------------------------------------------------------------ missiles: elevation + cover + structures

    /// <summary>
    /// Archers (ranged 75) at HI (defence 45). Adjacent 20 m cells, shooter cell 10 m higher: g_s = 1/2, E = 5/4 -> p =
    /// 93.75/138.75; shooter 10 m lower: E = 3/4 -> 56.25/101.25; flat: 75/120. Two cells apart with a forest cell between:
    /// flat p * 3/4 (cover); a building between: Zero (structure blocks the shot).
    /// </summary>
    [Fact]
    public void MissileElevationCoverAndStructuresAreWiredExactly()
    {
        TacticalBattle Make(Action<TerrainGrid> terrain, long targetX)
        {
            var g = OpenField();
            terrain(g);
            return new TacticalBattle(TT.Setup(g, DefaultSides(), 1, null,
                One(1, 0, UnitProfiles.Archers, 10, Cell(10, 16)),
                One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(targetX, 16))), PassiveController.Instance, PassiveController.Instance);
        }
        long struck;
        Assert.Equal(Q32.FromRatio(75, 120), Make(_ => { }, 11).MissileHitChanceOf(10, 20, out struck));
        Assert.Equal(20, struck);
        Assert.Equal(Ref(75, 5 * One32 / 4, 45, One32), Make(g => g.Set(10, 16, TerrainClass.Hills, 10_000), 11).MissileHitChanceOf(10, 20, out _));
        Assert.Equal(Ref(75, 3 * One32 / 4, 45, One32), Make(g => g.Set(11, 16, TerrainClass.Hills, 10_000), 11).MissileHitChanceOf(10, 20, out _));
        Q32 flat2 = Make(_ => { }, 12).MissileHitChanceOf(10, 20, out _);
        Assert.Equal(Q32.FromRatio(75, 120), flat2);
        Assert.Equal(flat2.Mul(Q32.FromRatio(3, 4)), Make(g => g.SetClass(11, 16, TerrainClass.Forest), 12).MissileHitChanceOf(10, 20, out _));
        Assert.Equal(flat2.Mul(Q32.FromRatio(3, 4)), Make(g => g.SetClass(12, 16, TerrainClass.Forest), 12).MissileHitChanceOf(10, 20, out _));
        Assert.Equal(Q32.Zero, Make(g => g.SetClass(11, 16, TerrainClass.Building), 12).MissileHitChanceOf(10, 20, out struck));
        Assert.Equal(-1, struck);
    }

    // ------------------------------------------------------------------ detection over / behind terrain

    /// <summary>
    /// An enemy standing still 6 cells away behind a 3-cell forest belt, near-perfect stationary concealment (2^-20): from the
    /// flat it is never acquired in the first 10 steps; from a 30 m hill the sight line clears the 15 m canopy (clarity 1, so
    /// the stationary factor does not apply) and it is acquired at t = 0. A building in the way blocks even the hill-less
    /// view of an enemy in the OPEN that is otherwise acquired at t = 0.
    /// </summary>
    [Fact]
    public void DetectionSeesOverTheCanopyFromAHillAndNotThroughABuilding()
    {
        TacticalBattle Make(Action<TerrainGrid> terrain)
        {
            var g = OpenField();
            terrain(g);
            return new TacticalBattle(TT.Setup(g, DefaultSides(), 3, TT.HideRulesDir5(),
                One(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
                One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(16, 16))), PassiveController.Instance, PassiveController.Instance);
        }
        void Forest(TerrainGrid g) { for (int x = 12; x <= 14; x++) g.SetClass(x, 16, TerrainClass.Forest); }
        var low = Make(Forest);
        StepUntil(low, 18_000);
        Assert.False(low.EverDetected(0, 20));
        var high = Make(g => { Forest(g); g.Set(10, 16, TerrainClass.Hills, 30_000); g.Set(16, 16, TerrainClass.Hills, 30_000); });
        StepUntil(high, 0);
        Assert.True(high.IsTracked(0, 20));
        var open = Make(_ => { });
        StepUntil(open, 0);
        Assert.True(open.IsTracked(0, 20));
        var walled = Make(g => g.SetClass(13, 16, TerrainClass.Building));
        StepUntil(walled, 18_000);
        Assert.False(walled.EverDetected(0, 20));
    }

    // ------------------------------------------------------------------ movement: climb in the engine

    /// <summary>
    /// A one-entity formation ordered East onto a 5 m terrace (every cell x &gt;= 11 at 5,000 mm). Step t = 0 consumes the
    /// degenerate first waypoint (its own cell centre) on the flat: 2,600 mm (1,300 mm/s * 2 s). Step t = 2 s climbs the
    /// segment (10,16) -&gt; (11,16): cost = 1000 + floor(5000*25000/(3*20000)) = 3083 -&gt; floor(1300*2000/3083) = 843 mm.
    /// Flat control: 2,600 + 2,600.
    /// </summary>
    [Fact]
    public void AnchorClimbsSlowerExactly()
    {
        long Run(long dh)
        {
            var g = OpenField();
            for (int y = 0; y < FieldH; y++) for (int x = 11; x < FieldW; x++) g.Set(x, y, TerrainClass.Open, dh);
            var mv = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(13, 16)));
            var b = new TacticalBattle(TT.Setup(g, DefaultSides(), 1, null,
                One(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(10, 16)),
                One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(40, 30))), mv, PassiveController.Instance);
            StepUntil(b, 2_000);
            return b.Anchor(1).X - Cell(10, 16).X;
        }
        Assert.Equal(2_600 + 2_600, Run(0));
        Assert.Equal(2_600 + 843, Run(5_000));
    }

    /// <summary>The engine's planner pays the climb: a MoveTo straight across a 30 m mound (rows 12..20, columns 20..24) bends
    /// around it under the placeholder table (climb 250 M cost units vs a detour of well under 100 M); with the
    /// Slice-C-equivalent table (no climb) the anchor goes straight. The mover's side has the destination as objective (its
    /// approach is progress) and the far enemy holds on an explicit order (exempt), so the battle outlives the march.</summary>
    [Fact]
    public void MoveToRoutesAroundAMoundWhenClimbCosts()
    {
        long MaxDeviation(TerrainEffects fx)
        {
            var g = OpenField();
            for (int y = 12; y <= 20; y++) for (int x = 20; x <= 24; x++) g.Set(x, y, TerrainClass.Open, 30_000);
            var mv = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(30, 16)));
            var hold = new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Hold));
            var s = TT.Setup(g, DefaultSides(new[] { Cell(30, 16) }, null), 1, null,
                One(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(14, 16)),
                One(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(46, 1))) with { Effects = fx };
            var b = new TacticalBattle(s, mv, hold);
            b.Start();
            long dev = 0;
            while (b.Advance() && b.CurrentPulse < 12) dev = Math.Max(dev, Math.Abs(b.Anchor(1).Y - Cell(14, 16).Y));
            return dev;
        }
        Assert.Equal(0, MaxDeviation(TerrainEffects.SliceCEquivalent()));
        Assert.True(MaxDeviation(TerrainEffects.Placeholder) >= 4 * CellMm, "the anchor must leave row 16 to skirt the mound");
    }

    // ------------------------------------------------------------------ (3) material influence on outcomes

    /// <summary>Defender anchor: its front rank stands at x = 500,400 mm, 400 mm inside column 25 (the crest / the far bank),
    /// so an attacker in contact from the west (contact 1,200 mm, discs 600 mm apart) stands in column 24.</summary>
    private static readonly Vec2L Crest = new(500_400, 330_000);

    private static (long atkKilled, long defKilled) Assault(Action<TerrainGrid> terrain, ulong seed)
    {
        var g = OpenField();
        terrain(g);
        var hold = new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Hold));
        var s = TT.Setup(g, DefaultSides(new[] { Crest }, null), seed, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(16, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Crest));
        var r = TT.RunGuarded(new TacticalBattle(s, new KnowledgeAi(), hold));
        TT.AssertConserved(r);
        return (r.Formations[0].Killed, r.Formations[1].Killed);
    }

    /// <summary>
    /// Material influence of SLOPE: 20 HI assault 20 HI holding the crest of a 10 m terrace (columns &gt;= 25), seeds 1..8,
    /// against the identical assault on the flat. Across the crest the attacker's hit chance is 37.5/82.5 and the defender's
    /// 62.5/107.5 (vs 50/95 each on the flat): the attacker must lose clearly more and kill clearly fewer in total.
    /// </summary>
    [Fact]
    public void AttackingUphillCostsTheAttackerMaterially()
    {
        long aFlat = 0, dFlat = 0, aHill = 0, dHill = 0;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var f = Assault(_ => { }, seed);
            var h = Assault(g => { for (int y = 0; y < FieldH; y++) for (int x = 25; x < FieldW; x++) g.Set(x, y, TerrainClass.Open, 10_000); }, seed);
            aFlat += f.atkKilled; dFlat += f.defKilled; aHill += h.atkKilled; dHill += h.defKilled;
        }
        Assert.True(aHill - dHill > aFlat - dFlat + 10, $"uphill net attacker losses {aHill - dHill} vs flat {aFlat - dFlat}");
        Assert.True(dHill < dFlat, $"defender losses on the hill {dHill} vs flat {dFlat}");
    }

    /// <summary>
    /// Material influence of a RIVER: the defender holds the far bank (column 25) of a 2-column ford (columns 23-24, footing
    /// 1/2, order 1/2) that the attacker must wade and fight from, seeds 1..8, against the same assault over open ground.
    /// </summary>
    [Fact]
    public void AttackingAcrossAFordCostsTheAttackerMaterially()
    {
        long aOpen = 0, dOpen = 0, aFord = 0, dFord = 0;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var o = Assault(_ => { }, seed);
            var f = Assault(g => TT.Fill(g, 23, 0, 24, FieldH - 1, TerrainClass.Ford), seed);
            aOpen += o.atkKilled; dOpen += o.defKilled; aFord += f.atkKilled; dFord += f.defKilled;
        }
        Assert.True(aFord - dFord > aOpen - dOpen + 10, $"ford net attacker losses {aFord - dFord} vs open {aOpen - dOpen}");
    }
}
