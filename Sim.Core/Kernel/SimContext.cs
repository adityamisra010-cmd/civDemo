using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// External inputs to the sim for one turn (kernel contract §3.9). Empty at M0 —
/// present in the contract signature so the order log (T0.7) slots in without an
/// executor change.
/// </summary>
public sealed class OrderBatch
{
    public static readonly OrderBatch Empty = new();
}

/// <summary>
/// Per-system, per-turn context (kernel contract §3.1). Exposes the read-only
/// previous world, this system's typed owned-tables payload (writable refs ONLY to
/// its own Next tables — built exclusively by the SystemCatalog, so a reference to
/// another system's tables does not exist; ADR-003), the system's own RNG streams,
/// both dt forms, and the order batch.
/// </summary>
public sealed class SimContext<TOwned>
{
    /// <summary>Last turn's completed state — the default (one-turn-lag) coupling.</summary>
    public IReadOnlyWorldState Prev { get; }

    /// <summary>Writable handles to THIS system's own tables in Next, and nothing else.</summary>
    public TOwned Owned { get; }

    /// <summary>Turn length in whole days (ADR-002).</summary>
    public long DtDays { get; }

    /// <summary>Turn length in years — every rate integrates against this (law 3).</summary>
    public double DtYears { get; }

    public OrderBatch Orders { get; }

    /// <summary>
    /// The lawful mutation channel for conserved stocks (law 1) — bound by the
    /// kernel to Next's flow table.
    /// </summary>
    public Ledger Ledger { get; }

    private readonly RngRegistry _rng;
    private readonly SystemId _self;

    internal SimContext(
        IReadOnlyWorldState prev, TOwned owned, RngRegistry rng, SystemId self,
        long dtDays, double dtYears, OrderBatch orders, Ledger ledger)
    {
        Prev = prev;
        Owned = owned;
        _rng = rng;
        _self = self;
        DtDays = dtDays;
        DtYears = dtYears;
        Orders = orders;
        Ledger = ledger;
    }

    /// <summary>
    /// This system's stream for a region (D-007: one stream per system × region).
    /// The system id is fixed by the kernel — a system cannot reach another
    /// system's streams.
    /// </summary>
    public RngStream Rng(RegionId region) => _rng.Get(_self, region);
}

/// <summary>Pipeline-facing identity of a system (§3.3).</summary>
public interface ISimSystem
{
    SystemId Id { get; }
}

/// <summary>
/// A simulation system (kernel contract §3.1): a pure function of its context.
/// Systems are STATELESS — no mutable instance fields; every piece of system
/// state, including integration remainder accumulators, lives in WorldState rows.
/// </summary>
public interface ISimSystem<TOwned> : ISimSystem
{
    void Step(SimContext<TOwned> ctx);
}
