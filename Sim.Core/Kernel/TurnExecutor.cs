using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// One pipeline entry: a system plus the kernel-built invoker that constructs its
/// typed context. Only the SystemCatalog creates these — that is the single place
/// owned tables are handed out (ownership by construction, §3.1 / ADR-003).
/// </summary>
public sealed class SystemRegistration
{
    public SystemId Id { get; }

    /// <summary>Stable name used by pipeline.json (§3.3: order is data).</summary>
    public string Name { get; }

    internal Action<IReadOnlyWorldState, WorldState, RngRegistry, long, double, OrderBatch> Invoke { get; }

    internal SystemRegistration(
        SystemId id, string name,
        Action<IReadOnlyWorldState, WorldState, RngRegistry, long, double, OrderBatch> invoke)
    {
        Id = id;
        Name = name;
        Invoke = invoke;
    }
}

/// <summary>
/// The turn pipeline runner (kernel contract §3.2–3.4). Ordering is pinned:
/// (1) dt = era-table band at the turn-START date, (2) clone Prev → Next,
/// (3) run systems in configured order — each reads Prev, writes only its own
/// Next tables, (4) advance the clock (SimDays += dtDays, Turn++).
/// The executor knows nothing about concrete systems; the pipeline arrives as
/// resolved registrations in data-configured order.
/// </summary>
public sealed class TurnExecutor
{
    private readonly EraTable _eraTable;
    private readonly SystemRegistration[] _pipeline;
    private readonly OrderLog? _orders;

    /// <param name="orders">Optional order log (§3.9); orders with Turn == t are
    /// delivered to the step executing from turn-t state.</param>
    public TurnExecutor(EraTable eraTable, SystemRegistration[] pipeline, OrderLog? orders = null)
    {
        _eraTable = eraTable;
        _pipeline = pipeline;
        _orders = orders;
    }

    /// <summary>Runs one turn. Never mutates <paramref name="prev"/>.</summary>
    public WorldState Step(WorldState prev)
    {
        long dtDays = _eraTable.DtDaysAt(prev.Clock.SimDays);          // (1) dt at turn start
        double dtYears = dtDays / (double)SimClock.YearDays;

        WorldState next = prev.Clone();                                 // (2) double buffer
        var rng = new RngRegistry(next);                                //     draws persist in Next
        OrderBatch batch = _orders?.BatchFor(prev.Clock.Turn) ?? OrderBatch.Empty;

        for (int i = 0; i < _pipeline.Length; i++)                      // (3) fixed order
            _pipeline[i].Invoke(prev, next, rng, dtDays, dtYears, batch);

        next.Clock = new SimClock(prev.Clock.Turn + 1, prev.Clock.SimDays + dtDays, dtDays); // (4)
        return next;
    }

    /// <summary>Runs <paramref name="turns"/> turns and returns the final state.</summary>
    public WorldState Run(WorldState initial, int turns)
    {
        WorldState world = initial;
        for (int i = 0; i < turns; i++) world = Step(world);
        return world;
    }
}
