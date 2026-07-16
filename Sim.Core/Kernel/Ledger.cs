using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>Overdraw handling — always an explicit caller choice, never a silent default.</summary>
public enum OverdrawPolicy
{
    /// <summary>Insufficient stock throws <see cref="LedgerOverdrawException"/>.</summary>
    Throw,
    /// <summary>Insufficient stock moves what is available; the return value reports it truthfully.</summary>
    ClampToAvailable,
}

/// <summary>Direction of a flow: value entering the world (source) or leaving it (sink).</summary>
public enum FlowDirection
{
    Source,
    Sink,
}

/// <summary>Overdraw under <see cref="OverdrawPolicy.Throw"/>.</summary>
public sealed class LedgerOverdrawException(string message) : Exception(message);

/// <summary>
/// Any Ledger arithmetic that would exceed Int64 — thrown, never wrapped (S3
/// overflow discipline at its chokepoint).
/// </summary>
public sealed class LedgerOverflowException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// THE mutation channel for conserved stocks (law 1 made mechanical, §3.6). This
/// file is the only code allowed to call Conserved.UNSAFE_LedgerSet — the CI grep
/// gate enforces it. All arithmetic is checked; failed operations mutate NOTHING
/// (compute first, commit last). An instance is bound to one world's flow table
/// (sources/sinks live in WorldState rows so they clone and snapshot); the kernel
/// hands it to systems via SimContext.
/// </summary>
public sealed class Ledger
{
    private readonly Table<LedgerFlowRow> _flows;

    public Ledger(Table<LedgerFlowRow> flows) => _flows = flows;

    /// <summary>
    /// Moves <paramref name="amount"/> between two stocks of the same quantity.
    /// Conserves by construction (no flow entry). Returns the amount actually
    /// moved. Negative amounts always throw.
    /// </summary>
    public long Transfer(ref Conserved from, ref Conserved to, long amount, OverdrawPolicy policy)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Ledger amounts are never negative.");

        long available = from.Value;
        long moved = amount <= available ? amount : policy switch
        {
            OverdrawPolicy.ClampToAvailable => available,
            _ => throw new LedgerOverdrawException(
                $"transfer of {amount} exceeds available stock {available} under OverdrawPolicy.Throw."),
        };

        // Self-transfer (from and to are the same stock): net zero by definition —
        // report the movable amount, mutate nothing.
        if (System.Runtime.CompilerServices.Unsafe.AreSame(ref from, ref to))
            return moved;

        long newTo;
        try
        {
            newTo = checked(to.Value + moved);
        }
        catch (OverflowException e)
        {
            throw new LedgerOverflowException(
                $"transfer of {moved} into a stock holding {to.Value} would overflow Int64.", e);
        }

        // Commit only after all computation succeeded — a failed op mutates nothing.
        from = Conserved.UNSAFE_LedgerSet(available - moved);
        to = Conserved.UNSAFE_LedgerSet(newTo);
        return moved;
    }

    /// <summary>
    /// Creates (source) or destroys (sink) stock, recording the counterweight in
    /// the flow table keyed (quantity, reason) so world totals stay exactly
    /// auditable: Σ stocks + Σ sunk − Σ sourced = 0. Returns the amount actually
    /// flowed. Negative amounts always throw; sinks obey the overdraw policy.
    /// </summary>
    public long Flow(
        ref Conserved stock, ConservedQuantityId quantity, ReasonId reason,
        long amount, FlowDirection direction, OverdrawPolicy policy)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Ledger amounts are never negative.");

        // Read-only lookup — the row is created only at commit time, so a failed
        // Flow on a first-use (quantity, reason) pair leaves NO phantom row behind
        // (atomicity: failed ops mutate nothing, including table layout).
        int flowIndex = FindFlowRow(quantity, reason);
        long currentSourced = flowIndex >= 0 ? _flows[flowIndex].TotalSourced : 0;
        long currentSunk = flowIndex >= 0 ? _flows[flowIndex].TotalSunk : 0;

        if (direction == FlowDirection.Source)
        {
            long newStock, newSourced;
            try
            {
                newStock = checked(stock.Value + amount);
                newSourced = checked(currentSourced + amount);
            }
            catch (OverflowException e)
            {
                throw new LedgerOverflowException(
                    $"sourcing {amount} of quantity {quantity.Value} (stock {stock.Value}, " +
                    $"total sourced {currentSourced}) would overflow Int64.", e);
            }
            // Commit.
            if (flowIndex < 0) flowIndex = AddFlowRow(quantity, reason);
            stock = Conserved.UNSAFE_LedgerSet(newStock);
            ref LedgerFlowRow sourceRow = ref _flows.Ref(flowIndex);
            sourceRow = sourceRow with { TotalSourced = newSourced };
            return amount;
        }
        else
        {
            long available = stock.Value;
            long sunk = amount <= available ? amount : policy switch
            {
                OverdrawPolicy.ClampToAvailable => available,
                _ => throw new LedgerOverdrawException(
                    $"sinking {amount} exceeds available stock {available} under OverdrawPolicy.Throw."),
            };
            long newSunk;
            try
            {
                newSunk = checked(currentSunk + sunk);
            }
            catch (OverflowException e)
            {
                throw new LedgerOverflowException(
                    $"sinking {sunk} of quantity {quantity.Value} (total sunk {currentSunk}) " +
                    "would overflow Int64.", e);
            }
            // Commit.
            if (flowIndex < 0) flowIndex = AddFlowRow(quantity, reason);
            stock = Conserved.UNSAFE_LedgerSet(available - sunk);
            ref LedgerFlowRow sinkRow = ref _flows.Ref(flowIndex);
            sinkRow = sinkRow with { TotalSunk = newSunk };
            return sunk;
        }
    }

    private int FindFlowRow(ConservedQuantityId quantity, ReasonId reason)
    {
        for (int i = 0; i < _flows.Count; i++)
        {
            ref LedgerFlowRow row = ref _flows.Ref(i);
            if (row.Quantity == quantity && row.Reason == reason) return i;
        }
        return -1;
    }

    private int AddFlowRow(ConservedQuantityId quantity, ReasonId reason) =>
        _flows.Add(new LedgerFlowRow(quantity, reason, TotalSourced: 0, TotalSunk: 0));
}
