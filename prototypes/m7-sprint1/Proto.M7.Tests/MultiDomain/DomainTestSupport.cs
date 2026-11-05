using Proto.M7.Events;
using Proto.M7.Spatial;
using Proto.M7.Tactical;

namespace Proto.M7.Tests.MultiDomain;

internal static class DT
{
    public const long HarnessMaxPulses = 400;

    /// <summary>Runs to the end; after EVERY completed instant checks the personnel ledger against the entity states.
    /// Exceeding the harness bound is a FAILURE (non-termination), never a rule.</summary>
    public static BattleResult RunChecked(TacticalBattle b, long maxPulses = HarnessMaxPulses, Action? onInstant = null)
    {
        b.Start();
        while (!b.IsOver)
        {
            if (b.Scheduler.TryPeekKey(out var k) && k.TimeMs > b.Scheduler.NowMs)
            {
                var err = b.PersonnelConsistency();
                if (err is not null) Assert.Fail($"personnel ledger inconsistent at {b.Scheduler.NowMs} ms: {err}");
                onInstant?.Invoke();
            }
            b.Advance();
            if (b.CurrentPulse > maxPulses) Assert.Fail($"HARNESS GUARD: still running after {maxPulses} pulses (non-termination is a FAILURE)");
        }
        Assert.Null(b.PersonnelConsistency());
        return b.Result!;
    }

    /// <summary>Executes events until the predicate holds at a completed instant, or the battle ends / the guard trips.</summary>
    public static void RunUntil(TacticalBattle b, Func<bool> done, long maxPulses = HarnessMaxPulses)
    {
        if (b.Scheduler.ExecutedCount == 0 && !b.IsOver) b.Start();
        while (!b.IsOver)
        {
            if (b.Scheduler.TryPeekKey(out var k) && k.TimeMs > b.Scheduler.NowMs && done()) return;
            b.Advance();
            if (b.CurrentPulse > maxPulses) Assert.Fail($"HARNESS GUARD: condition not reached after {maxPulses} pulses");
        }
    }

    public static DomainEntityView D(TacticalBattle b, long id)
    {
        foreach (var d in b.DomainEntities()) if (d.Id == id) return d;
        throw new ArgumentException("no such domain entity");
    }

    public static EntityView L(TacticalBattle b, long id)
    {
        foreach (var e in b.Entities()) if (e.Id == id) return e;
        throw new ArgumentException("no such land entity");
    }

    public static IEnumerable<TraceEntry> Executed(TacticalBattle b, int kind)
    {
        foreach (var e in b.Scheduler.Trace.Entries) if (e.Kind == kind && e.Outcome == TraceOutcome.Executed) yield return e;
    }

    public static ScriptedController Script(params (long Pulse, OrderRequest Order)[] orders)
    {
        var c = new ScriptedController();
        foreach (var (p, o) in orders) c.Add(p, o);
        return c;
    }

    public static Vec2L M(long xMetres, long yMetres) => new(xMetres * 1000, yMetres * 1000);

    /// <summary>Open-sea setup (64 x 40 cells of cellMm) from explicit formations.</summary>
    public static BattleSetup Sea(long cellMm, ulong seed, DomainRules? rules, params FormationSpec[] forms)
        => new(MultiDomainScenarios.OpenSea(cellMm), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
               MultiDomainScenarios.Sides(), forms, seed)
        { MultiDomain = new MultiDomainSetup { Rules = rules ?? new DomainRules() } };

    /// <summary>One hull at an explicit position (id = formationId * 1000).</summary>
    public static FormationSpec Ship(long id, int side, VesselClass v, Vec2L pos)
        => MultiDomainScenarios.Fleet(id, side, v, 1, pos) with { Entities = new[] { new EntitySpec(id * 1000, pos) } };

    /// <summary>A coast: water for x &lt;= waterCols-1, open land beyond (64 x 40 cells of cellMm).</summary>
    public static TerrainGrid Coast(long cellMm, int waterCols)
    {
        var g = new TerrainGrid(64, 40, cellMm, TerrainClass.Open);
        for (int y = 0; y < 40; y++) for (int x = 0; x < waterCols; x++) g.Set(x, y, TerrainClass.Water);
        return g;
    }

    public static BattleSetup OnGrid(TerrainGrid g, ulong seed, DomainRules? rules, TacticalRules? tr, params FormationSpec[] forms)
        => new(g, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), MultiDomainScenarios.Sides(), forms, seed)
        { MultiDomain = new MultiDomainSetup { Rules = rules ?? new DomainRules() }, Rules = tr ?? new TacticalRules() };
}
