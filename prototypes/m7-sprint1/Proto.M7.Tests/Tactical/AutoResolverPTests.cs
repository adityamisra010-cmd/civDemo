using System.Security.Cryptography;
using System.Text;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// [P] Lane P: the AutoResolver's model 2 — terrain mechanisms mirroring the lane-TR tactical coefficients (exact wiring
/// against <see cref="TerrainCombat"/> / <see cref="TerrainSight"/>), the path axis, Naismith climb, Defend doctrine, special
/// abilities (fire interval, personal concealment), per-entity detection aggregation, the DIR5 hidden-stationary port, the
/// refusals and the default-resolver policy. Every setup is a TEST INPUT.
/// </summary>
public class AutoResolverPTests
{
    private static readonly AutoCoefficients V2 = new();

    private static T RunBounded<T>(Func<T> f)
    {
        var t = Task.Run(f);
        if (!t.Wait(TimeSpan.FromSeconds(60))) Assert.Fail("HARNESS GUARD: AutoResolver did not terminate within 60 s (non-termination is a FAILURE)");
        return t.Result;
    }

    private static (AutoBattle B, BattleResult R) Logged(BattleSetup s, AutoCoefficients? c = null)
    {
        var b = new AutoBattle(s, c ?? V2) { RecordLog = true };
        var r = RunBounded(b.Run);
        TT.AssertConserved(r);
        return (b, r);
    }

    // ------------------------------------------------------------------ identity on flat open ground

    [Theory]
    [InlineData(PScenario.EqualInfantry)]
    [InlineData(PScenario.CombinedArms)]
    [InlineData(PScenario.HoldLineOpen)]
    public void EveryTerrainTermIsExactlyOneOnFlatOpenGround(PScenario id)
    {
        var (b, _) = Logged(ParityBatteryP.Build(id, 1));
        Assert.NotEmpty(b.TerrainTerms);
        foreach (var t in b.TerrainTerms)
        {
            Assert.Equal(Q32.One, t.AttackFactor);
            Assert.Equal(Q32.One, t.DefenceFactor);
            Assert.Equal(Q32.One, t.Cover);
            Assert.Equal(0, t.GradientRaw);
        }
    }

    // ------------------------------------------------------------------ melee: slope, footing, cohesion (exact wiring)

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void CrestMeleeUsesTheTacticalSlopeTermsExactly(ulong seed)
    {
        var s = ParityBatteryP.Build(PScenario.HillCrest, seed);
        var (b, _) = Logged(s);
        var fx = s.Effects;
        Q32 half = Q32.FromRatio(1, 2);
        var up = b.TerrainTerms.Where(t => t.Mode == 1 && t.AttackerId == 1 && t.GradientRaw > 0).ToList();
        var down = b.TerrainTerms.Where(t => t.Mode == 1 && t.AttackerId == 2 && t.GradientRaw < 0).ToList();
        Assert.NotEmpty(up); Assert.NotEmpty(down);
        foreach (var t in up)
        {
            Assert.Equal(half.Raw, t.GradientRaw);                                                   // 10 m over 20 m
            Assert.Equal(Q32.FromRatio(3, 4), t.AttackFactor);                                       // 1 - 1/2 * 1/2
            Assert.Equal(TerrainCombat.HitChance(50, Q32.FromRatio(3, 4), 45, Q32.One, s.Rules.HitMin, s.Rules.HitMax), t.HitChance);
        }
        foreach (var t in down)
        {
            Assert.Equal(-half.Raw, t.GradientRaw);
            Assert.Equal(Q32.FromRatio(5, 4), t.AttackFactor);
            Assert.Equal(TerrainCombat.HitChance(50, Q32.FromRatio(5, 4), 45, Q32.One, s.Rules.HitMin, s.Rules.HitMax), t.HitChance);
        }
        // every melee term equals the lane-TR function at the logged points (the auto evaluates the tactical coefficients)
        foreach (var t in b.TerrainTerms.Where(t => t.Mode == 1))
            Assert.Equal(TerrainCombat.GradientRaw(s.Grid, t.AttackerPos, t.TargetPos, fx.GradientCap), t.GradientRaw);
    }

    [Fact]
    public void FordWaderFightsWithoutFootingOrOrder()
    {
        var s = ParityBatteryP.Build(PScenario.FordCrossing, 1);
        var (b, _) = Logged(s);
        var wader = b.TerrainTerms.Where(t => t.Mode == 1 && t.AttackerId == 1).ToList();
        var bank = b.TerrainTerms.Where(t => t.Mode == 1 && t.AttackerId == 2).ToList();
        Assert.NotEmpty(wader); Assert.NotEmpty(bank);
        Q32 threeEighths = Q32.FromRatio(3, 8);                     // footing 1/2 x cohesion (1/2 + 1/2 x order 1/2 = 3/4)
        foreach (var t in wader)
        {
            Assert.Equal(TerrainClass.Ford, s.Grid.ClassAt(s.Grid.CellOf(t.AttackerPos).X, s.Grid.CellOf(t.AttackerPos).Y));
            Assert.Equal(threeEighths, t.AttackFactor);
            Assert.Equal(Q32.One, t.DefenceFactor);
        }
        foreach (var t in bank) { Assert.Equal(Q32.One, t.AttackFactor); Assert.Equal(threeEighths, t.DefenceFactor); }
    }

    // ------------------------------------------------------------------ missiles: cover and elevation (exact wiring)

    [Fact]
    public void ArcheryIntoForestPaysTheTacticalCoverExactly()
    {
        var s = ParityBatteryP.Build(PScenario.ForestHoldArchers, 1);
        var (b, _) = Logged(s);
        var shots = b.TerrainTerms.Where(t => t.Mode == 2).ToList();
        Assert.NotEmpty(shots);
        foreach (var t in shots)
        {
            var fp = TerrainSight.FirePath(s.Grid, s.Effects, t.AttackerPos, s.Rules.MuzzleHeightMm, t.TargetPos, s.Rules.AimHeightMm, t.TargetPos, 1, 1);
            Assert.False(fp.Blocked);
            Assert.Equal(fp.Transmission, t.Cover);
            Assert.True(t.Cover < Q32.One, "the target stands in the forest: its own cell's transmission applies");
            Assert.Equal(TerrainCombat.HitChance(75, t.AttackFactor, 45, Q32.One, s.Rules.HitMin, s.Rules.HitMax).Mul(t.Cover), t.HitChance);
        }
    }

    [Fact]
    public void ArchersOnTheCrestShootWithTheElevationFactor()
    {
        var s = ParityBatteryP.Build(PScenario.ArchersOnHill, 1);
        var (b, _) = Logged(s);
        var shots = b.TerrainTerms.Where(t => t.Mode == 2 && t.AttackerId == 2).ToList();
        Assert.NotEmpty(shots);
        Assert.Contains(shots, t => t.GradientRaw > 0);
        foreach (var t in shots)
        {
            long g = TerrainCombat.GradientRaw(s.Grid, t.TargetPos, t.AttackerPos, s.Effects.GradientCap);
            Assert.Equal(g, t.GradientRaw);
            Assert.Equal(TerrainCombat.MissileElevationFactor(s.Effects, g), t.AttackFactor);
            if (g > 0) Assert.True(t.AttackFactor > Q32.One);
        }
    }

    // ------------------------------------------------------------------ detection: per-entity aggregation

    [Fact]
    public void AnyOfIsOneMinusTheMissProduct()
    {
        Q32 half = Q32.FromRatio(1, 2);
        Assert.Equal(Q32.FromRatio(3, 4), AutoBattle.AnyOf(half, 2));
        Assert.Equal(Q32.FromRatio(7, 8), AutoBattle.AnyOf(half, 3));
        Assert.Equal(Q32.FromRatio(15, 16), AutoBattle.AnyOf(half, 4));
        Assert.Equal(half, AutoBattle.AnyOf(half, 1));
        Assert.Equal(half, AutoBattle.AnyOf(half, 0));
        Assert.Equal(Q32.One, AutoBattle.AnyOf(Q32.One, 50));
        Assert.Equal(Q32.Zero, AutoBattle.AnyOf(Q32.Zero, 50));
        Q32 p = Q32.FromRatio(1, 256), prev = Q32.Zero;
        for (int n = 1; n <= 60; n++) { var a = AutoBattle.AnyOf(p, n); Assert.True(a >= prev); prev = a; }   // monotone in n
        Assert.True(AutoBattle.AnyOf(p, 50) > Q32.FromRatio(17, 100) && AutoBattle.AnyOf(p, 50) < Q32.FromRatio(18, 100));   // 1 - (255/256)^50 = 0.1774
    }

    /// <summary>Metamorphic and exact: the detection draw of a formation is keyed (seed, target, step, side), independent of
    /// p, so a LARGER detection chance can only find the target at the same step or earlier — and until it is found the two
    /// battles are identical. TEST INPUT: a 2-person sniper team that never fires (Ranged 0) holds (Defend) on open ground; an
    /// HI formation marches on it. With the lane-N personal concealment 1/8 it must never be found earlier than without.</summary>
    [Fact]
    public void PersonalConcealmentNeverHastensDetection()
    {
        int later = 0;
        var log = new List<string>();
        var hidden = DomainProfiles.Sniper with { Ranged = 0, RangeCells = 0 };
        for (ulong seed = 1; seed <= 12; seed++)
        {
            BattleSetup S(UnitProfile p) => TT.Setup(OpenField(), DefaultSides(new[] { Cell(33, 16) }, null), seed, null,
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(14, 16)),
                new FormationSpec(2, 1, p, 2, Cell(33, 16)) { Posture = Posture.Defend });
            var b1 = new AutoBattle(S(hidden), V2); RunBounded(b1.Run);
            var b0 = new AutoBattle(S(hidden with { PersonalConcealment = Q32.One }), V2); RunBounded(b0.Run);
            long k1 = b1.FirstKnownStep(0, 2), k0 = b0.FirstKnownStep(0, 2);
            long a1 = k1 < 0 ? long.MaxValue : k1, a0 = k0 < 0 ? long.MaxValue : k0;
            Assert.True(a0 <= a1, $"seed {seed}: concealed team found at {k1}, plain at {k0}");
            if (a0 < a1) later++;
            log.Add($"{seed}:{k0}/{k1}");
        }
        Assert.True(later > 0, "teeth: the concealment must delay detection in some seed: " + string.Join(";", log));
    }

    // ------------------------------------------------------------------ the path axis

    [Fact]
    public void PathAxisRunsThroughTheFordTheGapAndTheStreet()
    {
        foreach (var (id, col) in new[] { (PScenario.FordCrossing, 30L), (PScenario.Defile, 28L), (PScenario.UrbanStreet, 27L) })
        {
            var s = ParityBatteryP.Build(id, 1);
            var axis = new AutoBattle(s, V2).Axis!;
            Assert.True(axis.OnPath, $"{id}");
            foreach (var p in axis.Points) Assert.True(s.Grid.IsPassable(s.Costs, s.Grid.CellOf(p).X, s.Grid.CellOf(p).Y), $"{id}: axis point {p} impassable");
            Assert.Contains(axis.Points, p => s.Grid.CellOf(p).X == col);
            for (long a = 0; a <= axis.Length; a += 1_000)                      // every point of the polyline is passable too
            {
                var c = s.Grid.CellOf(axis.At(a));
                Assert.True(s.Grid.IsPassable(s.Costs, c.X, c.Y), $"{id}: arc {a} in an impassable cell");
            }
        }
        var open = new AutoBattle(ParityBatteryP.Build(PScenario.EqualInfantry, 1), V2).Axis!;
        Assert.All(open.Points, p => Assert.Equal(330_000, p.Y));               // straight along row 16 on open ground
        Assert.Equal(19 * CellMm, open.Length);
    }

    [Fact]
    public void AxisProjectionTieBreaksOnTheLowerSegment()
    {
        var axis = AutoAxis.Of(new Vec2L(0, 0), new Vec2L(10_000, 0), new Vec2L(10_000, 10_000));
        Assert.Equal(20_000, axis.Length);
        Assert.Equal(5_000, axis.Project(new Vec2L(5_000, 5_000)));          // equidistant (5,000 mm) from both segments: segment 0
        Assert.Equal(new Vec2L(5_000, 0), axis.At(5_000));
        Assert.Equal(15_000, axis.Project(new Vec2L(11_000, 5_000)));         // nearer segment 1
        Assert.Equal(new Vec2L(10_000, 5_000), axis.At(15_000));
        // [P fix r2, P2-V3] MOVED PIN (was 0 and 20,000: clamped to the ends): the axis is extended beyond both ends along its
        // end segments, so a point behind the start projects to a negative arc length and one past the end beyond Length
        Assert.Equal(-3_000, axis.Project(new Vec2L(-3_000, -4_000)));       // behind the start on segment 0's line
        Assert.Equal(new Vec2L(-3_000, 0), axis.At(-3_000));
        Assert.Equal(109_000, axis.Project(new Vec2L(10_000, 99_000)));     // past the end on the last segment's line
        Assert.Equal(new Vec2L(10_000, 99_000), axis.At(109_000));
        var mirrored = AutoAxis.Of(new Vec2L(0, 0), new Vec2L(-10_000, 0), new Vec2L(-10_000, -10_000));
        Assert.Equal(new Vec2L(-5_000, 0), mirrored.At(5_000));               // rounding toward zero keeps mirror images exact
        Assert.Equal(new Vec2L(-10_000, -3_333), mirrored.At(13_333));
        Assert.Equal(new Vec2L(10_000, 3_333), axis.At(13_333));
        Assert.Equal(new Vec2L(3_000, 0), mirrored.At(-3_000));              // the extension is mirror-exact too
        Assert.Equal(-3_000, mirrored.Project(new Vec2L(3_000, 4_000)));
    }

    // ------------------------------------------------------------------ movement: Naismith climb, Defend never advances

    private static BattleSetup Climb(AutoCoefficients? _ = null)
    {
        var g = OpenField();
        for (int y = 0; y < FieldH; y++) for (int x = 10; x < FieldW; x++) g.Set(x, y, TerrainClass.Open, 10_000);
        return TT.Setup(g, DefaultSides(new[] { Cell(40, 16) }, null), 3, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 10, Cell(5, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, Cell(40, 16)) { Posture = Posture.Defend });
    }

    [Fact]
    public void ClimbingTheTerraceSlowsTheAdvanceByNaismithExactly()
    {
        // flat: 1,300 mm/s x 2 s = 2,600 mm; climbing 10 m over a 20 m run: cost 1000 + floor(10,000 x 1000 x 25 / (3 x 20,000))
        // = 1000 + 4,166 = 5,166 -> floor(1,300 x 2,000 / 5,166) = 503 mm per step.
        var (b, _) = Logged(Climb());
        var x = b.Log.Where(r => r.FormationId == 1).Select(r => r.PosMm).ToList();
        var steps = new List<long>();
        for (int i = 1; i < x.Count; i++) if (x[i] != x[i - 1]) steps.Add(x[i] - x[i - 1]);
        Assert.Contains(2_600L, steps);
        Assert.Contains(503L, steps);
        Assert.All(steps, d => Assert.True(d <= 2_600 || d == 3_900, $"step {d}"));
        var (b1, _) = Logged(Climb(), AutoCoefficients.Sprint1);              // model 1 has no climb term
        var x1 = b1.Log.Where(r => r.FormationId == 1).Select(r => r.PosMm).ToList();
        for (int i = 1; i < x1.Count; i++) Assert.NotEqual(503L, x1[i] - x1[i - 1]);
    }

    [Theory]
    [InlineData(PScenario.HoldLineOpen)]
    [InlineData(PScenario.HillCrest)]
    [InlineData(PScenario.ArchersOnHill)]
    public void DefendFormationNeverAdvances(PScenario id)
    {
        var (b, _) = Logged(ParityBatteryP.Build(id, 1));
        var rows = b.Log.Where(r => r.FormationId == 2).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, r => { Assert.False(r.Moved); Assert.Equal(rows[0].PosMm, r.PosMm); });
    }

    /// <summary>Order-delivery pin of the Defend doctrine in the TACTICAL engine: exactly one explicit Hold, issued at pulse 0
    /// and delivered at t = 0 (step offset 0), accepted; no order for it at pulses 1+; the line's anchor never moves.</summary>
    [Fact]
    public void DefendHoldIsDeliveredAtPulseZeroOnly()
    {
        var b = new TacticalBattle(ParityBatteryP.Build(PScenario.HoldLineOpen, 1), new KnowledgeAi(), new KnowledgeAi());
        b.Start();
        var anchor = b.Anchor(2);
        Assert.False(b.HoldExplicit(2));                                       // before pulse 0 is processed
        b.RunUntilPulse(1);
        Assert.True(b.HoldExplicit(2));
        Assert.Equal(OrderKind.Hold, b.Directive(2));
        while (b.Advance()) Assert.True(b.CurrentPulse <= TT.HarnessMaxPulses, "HARNESS GUARD");
        var r = b.Result!;
        var mine = b.Orders.Where(o => o.Request.FormationId == 2 && o.Request.Kind != OrderKind.Retreat).ToList();
        Assert.Single(mine);
        Assert.Equal(0, mine[0].Pulse); Assert.Equal(0, mine[0].AtMs); Assert.True(mine[0].Accepted);
        Assert.Equal(OrderKind.Hold, mine[0].Request.Kind);
        Assert.Equal(anchor, b.Anchor(2));
        TT.AssertConserved(r);
    }

    /// <summary>Decision latency (the controller decides only at pulse starts): archers marching on their MoveTo order learn of
    /// the forest-hidden infantry mid-pulse, keep marching, and loose their first volley only after the next pulse start
    /// (30 steps per pulse) — never in the pulse in which they learned of it. [P fix r1, P-V8] A LOWER-BOUND property test, not a
    /// turn-exact pin: the first volley also waits until the target is in range (measured: known at 58-60, volley at 103), so
    /// it cannot see when the order took effect. The turn-exact delivery pin with its ±1 neighbours is
    /// <see cref="AHaltedFormationAttacksExactlyAtTheFirstPulseStartAfterItLearns"/>.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void OrdersChangeOnlyAtPulseStarts(ulong seed)
    {
        var (b, _) = Logged(ParityScenarios.Build(ParityScenarioId.ArchersVsInfantryForest, seed, seed % 2 == 0));
        long known = b.FirstKnownStep(0, 2);
        Assert.True(known >= 0);
        long nextPulse = (known / 30 + 1) * 30;
        var first = b.Log.First(r => r.FormationId == 1 && (r.Mode & 2) != 0);
        Assert.True(first.Step >= nextPulse, $"first volley at step {first.Step}, enemy known at {known}, next pulse start {nextPulse}");
        Assert.All(b.Log.Where(r => r.FormationId == 1 && r.Step > known && r.Step < nextPulse), r => Assert.Equal(0, r.Mode));
    }

    /// <summary>Decision latency — the TURN-EXACT order-delivery pin ([P fix r1, P-V8]: the former version asserted only a lower
    /// bound). Formation 1 marches 40 m to its objective and halts there; an enemy marching on it in the open (2,600 mm per step,
    /// detected the step it comes within the 12-cell sight) is learned at step K; the Attack order takes effect EXACTLY at the
    /// first pulse start strictly after K, (K / 30 + 1) x 30. The enemy's start is shifted by whole steps (2,600 mm) so K lands
    /// on the pulse boundary 120 and its ±1 neighbours: K = 119 -&gt; first move 120; K = 120 (detected in the pulse-start step,
    /// AFTER that step's decision, as in the tactical engine where PulseStart decides before the step's Detect) -&gt; 150;
    /// K = 121 -&gt; 150; and the unshifted K = 123 -&gt; 150. The step before the effect is still halted.</summary>
    [Theory]
    [InlineData(1UL, 4L, 119L, 120L)]
    [InlineData(1UL, 3L, 120L, 150L)]
    [InlineData(1UL, 2L, 121L, 150L)]
    [InlineData(1UL, 0L, 123L, 150L)]
    [InlineData(2UL, 4L, 119L, 120L)]
    [InlineData(2UL, 3L, 120L, 150L)]
    public void AHaltedFormationAttacksExactlyAtTheFirstPulseStartAfterItLearns(ulong seed, long shiftSteps, long known, long effect)
    {
        var s = TT.Setup(OpenField(), DefaultSides(new[] { Cell(12, 16) }, new[] { Cell(10, 16) }), seed, null,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(10, 16)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(40, 16) - new Vec2L(2_600 * shiftSteps, 0)));
        var (b, _) = Logged(s);
        Assert.Equal(known, b.FirstKnownStep(0, 2));
        Assert.Equal(effect, (known / 30 + 1) * 30);
        var rows = b.Log.Where(r => r.FormationId == 1).ToList();
        Assert.All(rows.Where(r => r.Step > 20 && r.Step < effect), r => Assert.False(r.Moved, $"moved at step {r.Step} before the pulse start {effect}"));
        Assert.True(rows.Single(r => r.Step == effect).Moved, $"the Attack order must take effect at step {effect}");
        Assert.False(rows.Single(r => r.Step == effect - 1).Moved);
    }

    // ------------------------------------------------------------------ special abilities: fire interval

    [Fact]
    public void SnipersFireAtMostOncePerFireInterval()
    {
        var (b, _) = Logged(ParityBatteryP.Build(PScenario.SniperScreen, 1));
        var volleys = b.Log.Where(r => r.FormationId == 2 && (r.Mode & 2) != 0).Select(r => r.Step).ToList();
        Assert.True(volleys.Count >= 2);
        for (int i = 1; i < volleys.Count; i++) Assert.True(volleys[i] - volleys[i - 1] >= DomainProfiles.Sniper.FireIntervalSteps);
        Assert.Contains(volleys.Zip(volleys.Skip(1), (a, c) => c - a), d => d == DomainProfiles.Sniper.FireIntervalSteps);
    }

    // ------------------------------------------------------------------ termination floor

    [Fact]
    public void AValidAttackAlwaysConsumesHpInModelTwo()
    {
        long r = AutoBattle.ExpectedMilliOf(1, 1, Q32.FromRatio(1, 128_000), 100);
        Assert.Equal(0, r);                                                    // 1000 milli x 1/128,000 x 1/10 floors to 0
        Assert.Equal(1, AutoBattle.ValidAttackMilli(r, 2));
        Assert.Throws<InvalidOperationException>(() => AutoBattle.ValidAttackMilli(r, 1));
        Assert.Equal(7, AutoBattle.ValidAttackMilli(7, 1));
        Assert.Equal(7, AutoBattle.ValidAttackMilli(7, 2));
        Assert.Equal(5_000, AutoBattle.ExpectedMilliOf(2, 10, Q32.FromRatio(1, 4), 1_000));
    }

    // ------------------------------------------------------------------ fitted model-2 mechanisms: exact formulas

    [Fact]
    public void WoundBufferDelaysDeathsAndStaysMonotone()
    {
        // 50 HP entities, start 50, KillEff 1000: buffer 25 permille = 25 x 50 x 50 x 1000 = 62,500,000 (62.5 HP x 1e6)
        Assert.Equal(0, AutoBattle.DeadOf(62_000, 1000, 0, 25, 50, 50));      // 62 HP of damage: still wounds only
        Assert.Equal(0, AutoBattle.DeadOf(112_499, 1000, 0, 25, 50, 50));     // 112.499 HP - 62.5 = 49.999: no death yet
        Assert.Equal(1, AutoBattle.DeadOf(112_500, 1000, 0, 25, 50, 50));     // exactly one entity's HP beyond the buffer
        Assert.Equal(2, AutoBattle.DeadOf(100_000, 1000, 0, 0, 50, 50));      // no buffer: the Sprint 1 formula, 100 HP = 2
        Assert.Equal(1, AutoBattle.DeadOf(100_000, 600, 0, 0, 50, 50));       // floor(100 x 0.6 / 50)
        Assert.Equal(2, AutoBattle.DeadOf(100_000, 600, 40_000_000, 0, 50, 50));   // + phase 40 HP-equivalents
        long prev = 0;
        for (long d = 0; d <= 3_000_000; d += 7_919)
        {
            long x = AutoBattle.DeadOf(d, 700, 12_345_678, 25, 50, 50);
            Assert.True(x >= prev); prev = x;
        }
    }

    [Fact]
    public void ConvergenceCapFollowsTheTerrainMobility()
    {
        Assert.Equal(2000, AutoBattle.ConvergenceCapOf(2000, 0, 5_166));                 // off
        Assert.Equal(2000, AutoBattle.ConvergenceCapOf(2000, 1000, 1000));              // open ground: m = 1000
        Assert.Equal(2000, AutoBattle.ConvergenceCapOf(2000, 1000, 700));               // road: m capped at 1000
        Assert.Equal(386, AutoBattle.ConvergenceCapOf(2000, 1000, 1000 + 4_166));      // climbing the 10 m crest: m = 193
        Assert.Equal(666, AutoBattle.ConvergenceCapOf(2000, 1000, 3000));               // ford / marsh: m = 333
        Assert.Equal(1192, AutoBattle.ConvergenceCapOf(2000, 500, 1000 + 4_166));       // half weight: 2000 x (500 + 96)/1000
        Assert.Equal(1, AutoBattle.ConvergenceCapOf(1, 1000, TerrainCostTable.MaxCost));
    }

    [Fact]
    public void FireSaturatesOnTheExposedFrontOnly()
    {
        Assert.Equal(30, AutoBattle.SaturatedShots(60, 50, 10, 3000));                 // 60 archers on a 10-wide front: 30 count
        Assert.Equal(60, AutoBattle.SaturatedShots(60, 50, 10, 1_000_000));            // uncapped
        Assert.Equal(2, AutoBattle.SaturatedShots(2, 50, 10, 3000));                   // a sniper team is never saturated
        Assert.Equal(9, AutoBattle.SaturatedShots(60, 3, 10, 3000));                   // a remnant of 3 exposes only 3
        Assert.Equal(1, AutoBattle.SaturatedShots(60, 1, 10, 500));                    // at least one shot (Share floor 1)
    }

    /// <summary>C37 metamorphic (M7.2/M7.3 revision): more favourable defensive terrain must not raise the defender's losses
    /// in the AutoResolver — the same assault on the same seeds against a line holding on flat ground, on the 10 m crest,
    /// and behind the ford; the attacker must pay more on the crest and at the ford.</summary>
    [Fact]
    public void FavourableDefensiveTerrainNeverRaisesTheDefendersLosses()
    {
        long Sum(PScenario id, int side)
        {
            long t = 0;
            for (ulong seed = 1; seed <= 20; seed++)
                foreach (var f in new AutoBattle(ParityBatteryP.Build(id, seed), V2).Run().Formations) if (f.Side == side) t += f.PersonnelLoss;
            return t;
        }
        long defFlat = Sum(PScenario.HoldLineOpen, 1), atkFlat = Sum(PScenario.HoldLineOpen, 0);
        foreach (var id in new[] { PScenario.HillCrest, PScenario.FordCrossing, PScenario.HighRidge })
        {
            Assert.True(Sum(id, 1) <= defFlat, $"{id}: defender losses {Sum(id, 1)} vs flat {defFlat}");
            Assert.True(Sum(id, 0) > atkFlat, $"{id}: attacker losses {Sum(id, 0)} vs flat {atkFlat}");
        }
    }

    // ------------------------------------------------------------------ DIR5 hidden-stationary rule (port)

    /// <summary>Formation 1 (side 0) idles in a jungle patch whose concealment is 0 (TEST INPUT: never detected except by
    /// contact), with no objective and no known enemy; formation 2 (side 1, Defend: exemption (b)) holds 740 m away.</summary>
    private static BattleSetup Idle(Posture p1 = Posture.Normal, Vec2L[]? obj0 = null, long f2X = -1, long sight1 = 12, int pulses = 5)
    {
        var g = OpenField();
        TT.Fill(g, 4, 14, 6, 18, TerrainClass.Jungle);
        var conceal = ConcealmentTable.Create(new[]
        {
            Q32.One, Q32.One, Q32.FromRatio(1, 2), Q32.Zero, Q32.One, Q32.One, Q32.One, Q32.Zero, Q32.One, Q32.One, Q32.Zero,
            Q32.FromRatio(1, 2), Q32.One,   // [AP] ClassCount 11 -> 13 (Earthwork, Ditch: the placeholder values); neither occurs on this grid
        });
        Vec2L a = Cell(5, 16);
        Vec2L bpos = f2X < 0 ? Cell(42, 16) : new Vec2L(f2X, a.Y);
        return new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), conceal, DefaultSides(obj0, null), new[]
        {
            new FormationSpec(1, 0, TT.WithSight(UnitProfiles.HeavyInfantry, sight1), 10, a) { Posture = p1 },
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, bpos) { Posture = Posture.Defend },
        }, 7) { Rules = new TacticalRules { InactivityPulses = pulses } };
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void GenuinelyIdleHiddenFormationRoutsAtTheEndOfTheNthPulse(int n)
    {
        var (b, r) = Logged(Idle(pulses: n));
        Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
        Assert.Equal(0, r.Side); Assert.Equal(1, r.HoldsField);
        Assert.Equal(n, r.Pulses);
        Assert.Equal(n * 60_000L, r.EndMs);
        var f1 = r.Formations.Single(f => f.FormationId == 1);
        Assert.Equal(0, f1.Killed); Assert.Equal(10, f1.Escaped); Assert.Equal(10, f1.RoutedEscaped);
        Assert.Equal(new[]
        {
            new InactivityVerdict(n - 1, 1, 0, n, InactivityExemption.None, true),
            new InactivityVerdict(n - 1, 2, 1, n, InactivityExemption.DefensiveHold, false),
        }, b.InactivityVerdicts);
    }

    [Theory]
    [InlineData(Posture.Defend, InactivityExemption.DefensiveHold)]
    [InlineData(Posture.Ambush, InactivityExemption.AmbushWait)]
    public void ExemptIdleFormationsEndTheBattleAsAStandoff(Posture p, InactivityExemption ex)
    {
        var (b, r) = Logged(Idle(p1: p));
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.Equal(5, r.Pulses); Assert.Equal(-1, r.HoldsField);
        Assert.All(r.Formations, f => { Assert.Equal(10, f.ActiveAtEnd); Assert.Equal(0, f.Killed); });
        Assert.Contains(new InactivityVerdict(4, 1, 0, 5, ex, false), b.InactivityVerdicts);
    }

    [Fact]
    public void StandingOnItsOwnObjectiveIsADefensiveHold()
    {
        var (b, r) = Logged(Idle(obj0: new[] { Cell(5, 16) }));
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.Contains(new InactivityVerdict(4, 1, 0, 5, InactivityExemption.DefensiveHold, false), b.InactivityVerdicts);
    }

    /// <summary>Exemption (a) boundary: the enemy within reach = contact 1,200 + 1,300 mm/s x 60 s = 79,200 mm along the axis
    /// keeps the concealed formation exempt; 1 mm farther does not (formation 1 sees only 1 cell, so it never learns of it).</summary>
    [Theory]
    [InlineData(79_200L, true)]
    [InlineData(79_201L, false)]
    public void ConcealedWithAnEnemyInReachIsExemptExactlyToTheReach(long gap, bool exempt)
    {
        var (b, r) = Logged(Idle(f2X: Cell(5, 16).X + gap, sight1: 1));
        if (exempt)
        {
            Assert.Equal(BattleEndReason.Standoff, r.Reason);
            Assert.Contains(b.InactivityVerdicts, v => v.FormationId == 1 && v.Exemptions == InactivityExemption.ConcealedEnemyInReach && !v.Routed);
        }
        else
        {
            Assert.Equal(BattleEndReason.SideEliminated, r.Reason);
            Assert.Contains(new InactivityVerdict(4, 1, 0, 5, InactivityExemption.None, true), b.InactivityVerdicts);
        }
        Assert.Equal(5, r.Pulses);
    }

    /// <summary>[P fix r1, P-V4(c)] The legacy rule is selected by the SETUP's rules (TacticalRules.Inactivity), as in the
    /// TacticalResolver — not by an AutoResolver coefficient — so one setup is resolved under one rule by both resolvers.</summary>
    [Fact]
    public void TheLegacyRuleRemainsSelectableInModelTwo()
    {
        var s = Idle();
        var (_, r) = Logged(s with { Rules = s.Rules with { Inactivity = InactivityModel.LegacySideDisengage } });
        Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
        Assert.Equal(5, r.Pulses);
        Assert.Equal(BattleEndReason.SideEliminated, Logged(s).R.Reason);              // the default (DIR5) rule on the same setup
    }

    // ------------------------------------------------------------------ refusals and the default-resolver policy

    [Fact]
    public void AutoRefusesDomainWeaponsAndEmbarkedFormations()
    {
        var s = ParityBatteryP.Build(PScenario.EqualInfantry, 1);
        var gun = s with { Formations = new[] { s.Formations[0] with { Weapons = new[] { new WeaponSpec("test_gun", TargetDomains.Surface, MountArc.Turret, MaxRangeMm: 8_000_000, ReloadMs: 10_000, Accuracy: 1_000) } }, s.Formations[1] } };
        var emb = s with { Formations = new[] { s.Formations[0] with { EmbarkedOn = 9 }, s.Formations[1] } };
        Assert.Throws<NotSupportedException>(() => new AutoBattle(gun, V2));
        Assert.Throws<NotSupportedException>(() => new AutoBattle(emb, V2));
        Assert.Throws<NotSupportedException>(() => new AutoBattle(gun, AutoCoefficients.Sprint1));
        Assert.NotNull(ResolverPolicy.AutoUnsupportedReason(gun));
        Assert.NotNull(ResolverPolicy.AutoUnsupportedReason(emb));
        Assert.Null(ResolverPolicy.AutoUnsupportedReason(s));
    }

    [Fact]
    public void DefaultResolverIsAutoOnlyForAiVsAiLandBattles()
    {
        var land = ParityBatteryP.Build(PScenario.CombinedArms, 1);
        Assert.Equal(ResolverKind.Auto, ResolverPolicy.Default(land, Commander.Ai, Commander.Ai));
        Assert.Equal(ResolverKind.Tactical, ResolverPolicy.Default(land, Commander.Player, Commander.Ai));
        Assert.Equal(ResolverKind.Tactical, ResolverPolicy.Default(land, Commander.Ai, Commander.Player));
        var naval = MultiDomainScenarios.LineBattle(DomainProfiles.Galley, 50_000, 1);
        Assert.Equal(ResolverKind.Tactical, ResolverPolicy.Default(naval, Commander.Ai, Commander.Ai));      // naval never defaults to auto
        var amph = MultiDomainScenarios.Amphibious(1);
        Assert.Equal(ResolverKind.Tactical, ResolverPolicy.Default(amph, Commander.Ai, Commander.Ai));
        Assert.Throws<NotSupportedException>(() => new AutoBattle(naval, V2));
    }

    // ------------------------------------------------------------------ determinism, order, symmetry

    [Theory]
    [InlineData(3L, 5L, false)]
    [InlineData(5L, 3L, false)]
    [InlineData(3L, 5L, true)]
    [InlineData(5L, 3L, true)]
    public void ModelTwoTargetTieBreaksOnTheLowerFormationId(long northId, long southId, bool reverseInput)
    {
        var forms = new List<FormationSpec>
        {
            new(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(14, 16)),
            new(northId, 1, UnitProfiles.HeavyInfantry, 20, Cell(33, 10)),
            new(southId, 1, UnitProfiles.HeavyInfantry, 20, Cell(33, 22)),
        };
        if (reverseInput) forms.Reverse();
        var s = new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { Cell(33, 16) }, new[] { Cell(14, 16) }), forms.ToArray(), 5);
        var (b, _) = Logged(s);
        Assert.Equal(3, b.Log.First(r => r.FormationId == 1 && r.Target >= 0).Target);
    }

    [Theory]
    [InlineData(PScenario.CombinedArms, 1UL)]
    [InlineData(PScenario.HillCrest, 3UL)]
    [InlineData(PScenario.SniperScreen, 5UL)]
    public void ModelTwoIgnoresFormationInputOrderAndRepeats(PScenario id, ulong seed)
    {
        var s = ParityBatteryP.Build(id, seed);
        var r1 = new AutoBattle(s, V2).Run();
        var r2 = new AutoBattle(s, V2).Run();
        var rev = s with { Formations = s.Formations.Reverse().ToArray() };
        var r3 = new AutoBattle(rev, V2).Run();
        Assert.Equal(r1.ResultHash(), r2.ResultHash()); Assert.Equal(r1.TraceHash, r2.TraceHash);
        Assert.Equal(r1.ResultHash(), r3.ResultHash()); Assert.Equal(r1.TraceHash, r3.TraceHash);
    }

    [Theory]
    [InlineData(ParityScenarioId.EqualInfantry)]
    [InlineData(ParityScenarioId.CombinedArms)]
    [InlineData(ParityScenarioId.ArchersVsInfantryForest)]
    public void ModelTwoGivesTheSameOutcomeInBothOrientations(ParityScenarioId id)
    {
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var a = new AutoBattle(ParityScenarios.Build(id, seed, false), V2).Run();
            var m = new AutoBattle(ParityScenarios.Build(id, seed, true), V2).Run();
            Assert.Equal(a.ResultHash(), m.ResultHash());
            Assert.Equal(a.TraceHash, m.TraceHash);
        }
    }
}
