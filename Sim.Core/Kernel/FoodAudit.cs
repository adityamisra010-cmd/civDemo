using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// THE FOOD CONSERVATION AUDIT — a closed accounting of the grain stock across
/// one turn, or across any pair of phase checkpoints inside one turn.
///
/// WHY IT CAN EXIST WITHOUT NEW BOOKKEEPING. Every unit of grain that enters or
/// leaves the world does so through <see cref="Ledger.Flow"/>, which already
/// records a running <c>TotalSourced</c>/<c>TotalSunk</c> per (quantity, reason)
/// in <c>WorldState.LedgerFlows</c> — serialized, cloned, and therefore present
/// in every snapshot. Grain that merely CHANGES HANDS moves through
/// <see cref="Ledger.Transfer"/>, which writes no flow row because it conserves
/// by construction. So the accounting closes with nothing added to the sim:
///
///     stock(end) - stock(start) = Σ Δsourced - Σ Δsunk
///
/// and <see cref="FoodTurnAccount.Residual"/> is that identity's error term.
///
/// A NON-ZERO RESIDUAL IS THE FINDING, NOT A ROUNDING NUISANCE. Everything here
/// is `long`; there is no epsilon. A residual can only be non-zero if grain
/// changed by some route other than the Ledger — a raw stock assignment, a
/// GoodStockRow dropped or replaced wholesale, a settlement removed with its
/// store still in it, or a Transfer whose other end is not counted by
/// <see cref="GrainStock"/>. Each of those is a law-1 defect, and this type
/// exists to name which one.
///
/// IT IS AN OBSERVER. It reads <see cref="IReadOnlyWorldState"/>, writes nothing,
/// is consulted by no system, and reimplements no formula — the reason totals are
/// read from the rows the Ledger itself wrote.
/// </summary>
public static class FoodAudit
{
    /// <summary>Grain's conserved-quantity id, derived the same way every system
    /// derives it (<c>ConservedQuantityIds.OfGood</c>) rather than hardcoded.</summary>
    public static ConservedQuantityId QuantityOf(int grainGoodId) =>
        ConservedQuantityIds.OfGood(new GoodId(grainGoodId));

    /// <summary>Total grain held across every settlement's store — the same sum
    /// <c>ReplayReport.totalFood</c> reports, extracted so both read one
    /// definition.</summary>
    public static long GrainStock(IReadOnlyWorldState w, int grainGoodId)
    {
        long total = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == grainGoodId) total += w.GoodStocks[i].Amount.Value;
        return total;
    }

    /// <summary>Grain held by ONE settlement, or 0 when it has no grain row.
    /// "No row" and "a row holding zero" are different states and the caller is
    /// told which by <see cref="HasGrainRow"/>.</summary>
    public static long GrainStockOf(IReadOnlyWorldState w, int grainGoodId, SettlementId settlement)
    {
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            GoodStockRow row = w.GoodStocks[i];
            if (row.Settlement == settlement && row.Good.Value == grainGoodId) return row.Amount.Value;
        }
        return 0;
    }

    /// <summary>Whether a settlement has a grain row at all. A settlement with no
    /// row cannot be consumed from, produced into by the fallback path, or
    /// appropriated — <c>Consume</c> returns early on index &lt; 0 — so its
    /// absence is a behavioural fact, not a storage detail.</summary>
    public static bool HasGrainRow(IReadOnlyWorldState w, int grainGoodId, SettlementId settlement)
    {
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            GoodStockRow row = w.GoodStocks[i];
            if (row.Settlement == settlement && row.Good.Value == grainGoodId) return true;
        }
        return false;
    }

    /// <summary>
    /// The cumulative grain ledger at one instant: the stock, plus the world's
    /// running source/sink totals per reason. Reasons are kept as parallel arrays
    /// indexed by <see cref="ReasonId.Value"/> so the audit stays exhaustive — it
    /// never has to know in advance which reasons touch grain, and a reason
    /// introduced later shows up in <see cref="OtherSourced"/>/<see cref="OtherSunk"/>
    /// rather than vanishing from the identity.
    /// </summary>
    public readonly struct FoodSnapshot
    {
        public long Turn { get; }
        public string Phase { get; }
        public long Stock { get; }
        private readonly long[] _sourced;
        private readonly long[] _sunk;

        internal FoodSnapshot(long turn, string phase, long stock, long[] sourced, long[] sunk)
        {
            Turn = turn; Phase = phase; Stock = stock; _sourced = sourced; _sunk = sunk;
        }

        public long Sourced(ReasonId reason) =>
            reason.Value >= 0 && reason.Value < _sourced.Length ? _sourced[reason.Value] : 0;

        public long Sunk(ReasonId reason) =>
            reason.Value >= 0 && reason.Value < _sunk.Length ? _sunk[reason.Value] : 0;

        public long TotalSourced { get { long t = 0; for (int i = 0; i < _sourced.Length; i++) t += _sourced[i]; return t; } }
        public long TotalSunk { get { long t = 0; for (int i = 0; i < _sunk.Length; i++) t += _sunk[i]; return t; } }
    }

    /// <summary>Bound on the reason ids the audit indexes. A grain flow carrying
    /// a reason at or above it THROWS rather than being dropped — the accounting
    /// is allowed to fail loudly, never to lose a term quietly.</summary>
    private const int ReasonCapacity = 64;

    /// <summary>Reads the cumulative grain ledger out of a world. Table order is
    /// index order — never a dictionary — so the result is deterministic.</summary>
    public static FoodSnapshot Snapshot(IReadOnlyWorldState w, int grainGoodId, string phase)
    {
        ConservedQuantityId grain = QuantityOf(grainGoodId);
        var sourced = new long[ReasonCapacity];
        var sunk = new long[ReasonCapacity];
        for (int i = 0; i < w.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = w.LedgerFlows[i];
            if (row.Quantity != grain) continue;
            int r = row.Reason.Value;
            if (r < 0 || r >= ReasonCapacity)
                throw new InvalidOperationException(
                    $"grain flow reason {r} exceeds the audit's reason capacity {ReasonCapacity}; " +
                    "widen FoodAudit.ReasonCapacity rather than letting the accounting lose a term.");
            sourced[r] += row.TotalSourced;
            sunk[r] += row.TotalSunk;
        }
        return new FoodSnapshot(w.Clock.Turn, phase, GrainStock(w, grainGoodId), sourced, sunk);
    }

    /// <summary>
    /// The accounting between two snapshots. `Residual` is the identity's error:
    ///
    ///     Residual = (Stock(end) - Stock(start)) - ΔSourced + ΔSunk
    ///
    /// EXACTLY ZERO is the only conserving value. Longs throughout; no epsilon.
    /// </summary>
    public readonly record struct FoodTurnAccount(
        long FromTurn, string FromPhase, long ToTurn, string ToPhase,
        long StockStart, long StockEnd,
        long Harvest, long Eaten, long Spoilage, long GranaryOverflow,
        long OtherSourced, long OtherSunk)
    {
        public long StockDelta => StockEnd - StockStart;
        public long TotalSourced => Harvest + OtherSourced;
        public long TotalSunk => Eaten + Spoilage + GranaryOverflow + OtherSunk;
        public long Residual => StockDelta - TotalSourced + TotalSunk;
        public bool Reconciles => Residual == 0;

        public string Line() =>
            string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"t{FromTurn}:{FromPhase} -> t{ToTurn}:{ToPhase}  " +
                $"start={StockStart} +harvest={Harvest} -eaten={Eaten} -spoilage={Spoilage} " +
                $"-granary={GranaryOverflow} +otherSrc={OtherSourced} -otherSink={OtherSunk} " +
                $"= end={StockEnd}  residual={Residual}");
    }

    /// <summary>Differences two snapshots into a closed account. `start` must be
    /// the earlier one; the reasons named individually are grain's four known
    /// flow reasons, and everything else is aggregated into Other* so the
    /// identity stays exhaustive by construction.</summary>
    public static FoodTurnAccount Account(in FoodSnapshot start, in FoodSnapshot end)
    {
        long harvest = end.Sourced(ReasonIds.Harvest) - start.Sourced(ReasonIds.Harvest);
        long eaten = end.Sunk(ReasonIds.Eaten) - start.Sunk(ReasonIds.Eaten);
        long spoilage = end.Sunk(ReasonIds.Spoilage) - start.Sunk(ReasonIds.Spoilage);
        long granary = end.Sunk(ReasonIds.GranaryOverflow) - start.Sunk(ReasonIds.GranaryOverflow);
        long otherSourced = (end.TotalSourced - start.TotalSourced) - harvest;
        long otherSunk = (end.TotalSunk - start.TotalSunk) - eaten - spoilage - granary;
        return new FoodTurnAccount(
            start.Turn, start.Phase, end.Turn, end.Phase,
            start.Stock, end.Stock,
            harvest, eaten, spoilage, granary, otherSourced, otherSunk);
    }
}
