using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

internal static class TT
{
    /// <summary>
    /// HARNESS SAFETY GUARD (spec §4): the engine has no pulse cap; a test that exceeds this bound is reported as a FAILURE
    /// (a bug: non-termination), never treated as a battle rule.
    /// </summary>
    public const long HarnessMaxPulses = 2_000;

    public static BattleResult RunGuarded(TacticalBattle b, long maxPulses = HarnessMaxPulses)
    {
        b.Start();
        while (b.Advance())
            if (b.CurrentPulse > maxPulses) Assert.Fail($"HARNESS GUARD: battle still running after {maxPulses} pulses — non-termination is a FAILURE, not a rule");
        return b.Result!;
    }

    public static BattleSetup Setup(TerrainGrid grid, SideSpec[] sides, ulong seed, TacticalRules? rules, params FormationSpec[] forms)
        => new(grid, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), sides, forms, seed)
        { Rules = rules ?? new TacticalRules() };

    /// <summary>Fills the rectangle [x0..x1] x [y0..y1] (cells, inclusive) with a class.</summary>
    public static void Fill(TerrainGrid g, int x0, int y0, int x1, int y1, TerrainClass c)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) g.Set(x, y, c);
    }

    /// <summary>A profile variant with a different sight (test input; labelled at the call site).</summary>
    public static UnitProfile WithSight(UnitProfile p, long cells) => p with { SightCells = cells };

    public static void AssertConserved(BattleResult r)
    {
        foreach (var f in r.Formations)
        {
            Assert.Equal(f.Start, f.Survivors + f.Killed + f.Surrendered);
            Assert.Equal(f.Survivors, f.ActiveAtEnd + f.Escaped);
            Assert.True(f.RoutedEscaped + f.DisengagedEscaped <= f.Escaped);
        }
    }

    /// <summary>Tight test rules for the inactivity scenarios (test inputs): near-perfect stationary concealment.</summary>
    public static TacticalRules HideRules(bool activeConcealment = true) => new()
    {
        StationaryConcealment = Q32.FromRatio(1, 1 << 20),     // TEST INPUT
        ActiveConcealmentRule = activeConcealment,
    };
}
