using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// [TR] DIR5 §2.6 "There is no fixed 6–12 command-pulse ceiling. Battle duration depends on the number of units and the
/// evolving battle state." The engine has no pulse cap; these battles run far beyond any former ceiling and end ONLY by a
/// battle-end condition (elimination / rout / surrender, or the whole-field Standoff), each under a harness guard far above
/// its measured length that is never approached. Counters that grow with time saturate (no overflow = no implicit ceiling).
/// The guards are sized (about 10-50x the measured battle) so that a mutant which removes an end condition FAILS within the
/// mutant time bound instead of hanging (ADR-015 §7.1; the first lane-TR mutant run hung on the former 10^6-pulse guards).
/// </summary>
public class NoPulseCeilingTests
{
    private static readonly BattleEndReason[] EndConditions = { BattleEndReason.SideEliminated, BattleEndReason.BothEliminated, BattleEndReason.Standoff };

    /// <summary>Harness guards (test-only, never a rule): the duel measured 367 pulses (seed 17), the march about 215.</summary>
    private const long DuelGuard = 20_000, MarchGuard = 2_000, ArmsGuard = 20_000;

    /// <summary>
    /// A long attrition duel: two HI entities in contact, hit chance clamped to exactly 1/2000 (TEST INPUT; HitMin &gt; 0 keeps
    /// termination almost sure), 5 hits of 10 HP each to kill. Every blow is progress, so neither the hidden-stationary rule
    /// nor the standoff can fire; the battle lasts hundreds of pulses and ends when an entity dies.
    /// </summary>
    [Fact]
    public void LongAttritionDuelEndsOnlyByElimination()
    {
        var rules = new TacticalRules { HitMin = Q32.FromRatio(1, 2_000), HitMax = Q32.FromRatio(1, 2_000) };
        Vec2L a = Cell(20, 16), b = a + new Vec2L(1_000, 0);
        var s = TT.Setup(OpenField(), DefaultSides(), 17, rules,
            new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 1, a) { Entities = new[] { new EntitySpec(10, a) } },
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 1, b) { Entities = new[] { new EntitySpec(20, b) } });
        var battle = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
        var r = TT.RunGuarded(battle, DuelGuard);
        Assert.Contains(r.Reason, EndConditions);
        Assert.NotEqual(BattleEndReason.Standoff, r.Reason);
        Assert.True(r.Pulses > 100, $"pulses {r.Pulses}");
        Assert.Equal(1, r.Formations[0].Killed + r.Formations[1].Killed + (r.Reason == BattleEndReason.BothEliminated ? 1 : 0));
        Assert.Equal(0, battle.FieldIdleCounter);
        Assert.Empty(battle.InactivityVerdicts);
        TT.AssertConserved(r);
    }

    /// <summary>
    /// A long march: a half-speed column (650 mm/s, 39 m per pulse) crosses an 8 km field (400 x 20 cells) to its objective
    /// while the enemy holds far out of sight (explicit Hold: exempt). Every pulse brings it at least one cell closer
    /// (progress), so the battle lives as long as the march (&gt; 150 pulses) and ends only when the field falls idle after
    /// arrival (Standoff, five idle pulses after the last progress). The holding enemy's idle counter saturates at 5 and its
    /// verdict is logged once, not once per pulse. After arrival nothing progresses, so a battle still running more than
    /// 2N + 1 pulses after it is a FAILURE (the Standoff end condition did not fire).
    /// </summary>
    [Fact]
    public void LongMarchLivesAsLongAsTheStateProgresses()
    {
        var g = new TerrainGrid(400, 20, CellMm);
        Vec2L C(long x, long y) => new(x * CellMm + CellMm / 2, y * CellMm + CellMm / 2);
        var slow = UnitProfiles.HeavyInfantry with { Name = "test/slow", SpeedNum = 1, SpeedDen = 2 };    // TEST INPUT
        var sides = new[]
        {
            new SideSpec(ExitEdge.West) { Objectives = new[] { C(396, 18) } },
            new SideSpec(ExitEdge.East),
        };
        var s = new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), sides, new[]
        {
            new FormationSpec(1, 0, slow, 10, C(3, 18)),
            new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, C(200, 1)),
        }, 23);
        var march = new ScriptedController().Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, C(396, 18)));
        var hold = new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Hold));
        var b = new TacticalBattle(s, march, hold);
        b.Start();
        long maxMarchIdle = 0, arrived = -1;
        int n = s.Rules.InactivityPulses;
        while (b.Advance())
        {
            if (b.Anchor(1) != C(396, 18)) maxMarchIdle = Math.Max(maxMarchIdle, b.IdleCounter(1));
            else if (arrived < 0) arrived = b.CurrentPulse;
            Assert.True(b.IdleCounter(2) <= 5 && b.FieldIdleCounter <= 5);
            if (arrived >= 0 && b.CurrentPulse > arrived + 2 * n + 1) Assert.Fail($"no battle end {2 * n + 1} pulses after arrival (pulse {arrived})");
            if (b.CurrentPulse > MarchGuard) Assert.Fail("HARNESS GUARD");
        }
        var r = b.Result!;
        Assert.Equal(BattleEndReason.Standoff, r.Reason);
        Assert.True(r.Pulses > 150, $"pulses {r.Pulses}");
        Assert.Equal(0, maxMarchIdle);                                     // marching toward the objective is progress every pulse
        Assert.Equal(C(396, 18), b.Anchor(1));
        Assert.Equal(5, b.IdleCounter(2));                                 // saturated, not counting toward an overflow
        Assert.Single(b.InactivityVerdicts, v => v.FormationId == 2);     // logged once for the whole stretch
        Assert.All(r.Formations, f => Assert.Equal(f.Start, f.ActiveAtEnd));
    }

    /// <summary>The same initial state run with the harness guard at 20,000 and at its default (2,000) produces the same
    /// battle — the guard is never what ends a battle (it only turns non-termination into a test FAILURE).</summary>
    [Fact]
    public void TheHarnessGuardIsNeverTheTerminator()
    {
        var a = TT.RunGuarded(new TacticalBattle(CombinedArms(5), new KnowledgeAi(), new KnowledgeAi()), ArmsGuard);
        var b = TT.RunGuarded(new TacticalBattle(CombinedArms(5), new KnowledgeAi(), new KnowledgeAi()));
        Assert.Equal(a.TraceHash, b.TraceHash);
        Assert.Contains(a.Reason, EndConditions);
    }
}
