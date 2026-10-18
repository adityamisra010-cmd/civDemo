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

    /// <summary>
    /// D-056 — THE SAME-STEP READS this system makes: the names of the systems whose NEXT tables of THIS step it reads (a
    /// table another system writes, handed to this one read-only by the SystemCatalog). Each is a producer → consumer edge
    /// the executor enforces (<see cref="TurnExecutor"/>'s constructor): in a pipeline holding both, the producer runs
    /// first, so the consumer always reads this step's output — never the previous step's, carried into NEXT by the clone.
    /// Empty for every system that reads only PREV and its own tables.
    /// </summary>
    public IReadOnlyList<string> ReadsThisStepFrom { get; }

    internal SystemRegistration(
        SystemId id, string name,
        Action<IReadOnlyWorldState, WorldState, RngRegistry, long, double, OrderBatch> invoke,
        IReadOnlyList<string>? readsThisStepFrom = null)
    {
        Id = id;
        Name = name;
        Invoke = invoke;
        ReadsThisStepFrom = readsThisStepFrom ?? [];
    }
}

/// <summary>Receives per-phase timing, and the read-only work-in-progress state,
/// from an observed Step: the bench (`sim bench`) and the read-only split report
/// (`sim run --split-report`, Observability/SplitProbe — D-055 / ADR-040). Sim
/// behavior never depends on these values or on any observer.</summary>
public interface ITurnObserver
{
    void OnPhase(string phase, long elapsedTimestampTicks, long allocatedBytes);

    /// <summary>
    /// INTRA-TURN STATE CHECKPOINT (food-anomaly investigation). Called with the
    /// WORK-IN-PROGRESS Next world immediately after the named phase completes,
    /// so an observer can see stock between two systems instead of only at the
    /// turn boundary. The first call is `"clone"` — Next before ANY system has
    /// run, i.e. true beginning-of-turn state.
    ///
    /// IT IS A READ-ONLY WINDOW AND THAT IS LOAD-BEARING. The parameter is typed
    /// <see cref="IReadOnlyWorldState"/> so an observer cannot write, and the
    /// default body is empty so no existing observer and no production path is
    /// altered by its existence. When `observer` is null — which is every
    /// canonical, golden and replay path, and every CLI run but `sim bench`
    /// and `sim run --split-report` — neither this nor
    /// <see cref="OnPhase"/> is reached at all.
    /// </summary>
    void OnPhaseState(string phase, IReadOnlyWorldState next) { }
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
    /// <exception cref="PipelineFormatException">D-056: a system that reads another's output of the same step
    /// (<see cref="SystemRegistration.ReadsThisStepFrom"/>) is placed before that producer. Order is data (§3.3), but an
    /// order in which a same-step read would see the previous step's table is refused when the pipeline is composed —
    /// before any step — rather than run with a meaning that depends on where the producer happens to sit. A pipeline
    /// without the producer is accepted: there is no same-step output to read, and the consumer reads the table as the
    /// world carries it.</exception>
    public TurnExecutor(EraTable eraTable, SystemRegistration[] pipeline, OrderLog? orders = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        for (int i = 0; i < pipeline.Length; i++)
            foreach (string producer in pipeline[i].ReadsThisStepFrom)
                for (int j = i + 1; j < pipeline.Length; j++)
                    if (string.Equals(pipeline[j].Name, producer, StringComparison.Ordinal))
                        throw new PipelineFormatException(
                            $"pipeline order: '{pipeline[i].Name}' (position {i + 1}) reads the table '{producer}' writes in " +
                            $"the same step, so '{producer}' must run before it, but it is at position {j + 1}. Move " +
                            $"'{producer}' ahead of '{pipeline[i].Name}' (D-056: a same-step read never sees the previous " +
                            "step's table).");
        _eraTable = eraTable;
        _pipeline = pipeline;
        _orders = orders;
    }

    /// <summary>Runs one turn. Never mutates <paramref name="prev"/>.</summary>
    public WorldState Step(WorldState prev) => Step(prev, null);

    /// <summary>
    /// As <see cref="Step(WorldState)"/>, reporting per-phase wall time and
    /// allocations to <paramref name="observer"/> (T0.9 bench instrumentation;
    /// measurement never alters execution — the phases run identically when
    /// observer is null).
    /// </summary>
    public WorldState Step(WorldState prev, ITurnObserver? observer)
    {
        long dtDays = _eraTable.DtDaysAt(prev.Clock.SimDays);          // (1) dt at turn start
        double dtYears = dtDays / (double)SimClock.YearDays;

        long t0 = 0, a0 = 0;
        if (observer is not null)
        {
            t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            a0 = GC.GetAllocatedBytesForCurrentThread();
        }

        WorldState next = prev.Clone();                                 // (2) double buffer
        Observe("clone");

        var rng = new RngRegistry(next);                                //     draws persist in Next
        OrderBatch batch = _orders?.BatchFor(prev.Clock.Turn) ?? OrderBatch.Empty;

        for (int i = 0; i < _pipeline.Length; i++)                      // (3) fixed order
        {
            _pipeline[i].Invoke(prev, next, rng, dtDays, dtYears, batch);
            Observe(_pipeline[i].Name);
        }

        next.Clock = new SimClock(prev.Clock.Turn + 1, prev.Clock.SimDays + dtDays, dtDays); // (4)
        return next;

        void Observe(string phase)
        {
            if (observer is null) return;
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            long a1 = GC.GetAllocatedBytesForCurrentThread();
            observer.OnPhase(phase, t1 - t0, a1 - a0);
            observer.OnPhaseState(phase, next);
            t0 = t1;
            a0 = a1;
        }
    }

    /// <summary>Runs <paramref name="turns"/> turns and returns the final state.</summary>
    public WorldState Run(WorldState initial, int turns)
    {
        WorldState world = initial;
        for (int i = 0; i < turns; i++) world = Step(world);
        return world;
    }
}
