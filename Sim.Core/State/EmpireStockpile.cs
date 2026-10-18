using Sim.Core.Kernel;

namespace Sim.Core.State;

/// <summary>
/// D-051 §6 applied to construction and roads (authority: D-051 §6 and D-053 §1 — the Director's 2026-10-13 instruction,
/// item 1: construction and roads draw on the empire stockpile; D-053 §5 records that this instruction supersedes D-052
/// §11's deferral for construction and roads) — THE CIVILIZATION'S PURSE for a settlement that USES goods: which
/// settlements' GoodStocks rows a draw may touch, in what order, how much they hold together, and the draw itself.
/// A static helper with no state and no cache (the ConstructionQuery / RoadDevelopmentQuery shape): it decides
/// nothing about WHAT is built or WHEN; its callers do.
///
/// <list type="number">
/// <item><b>Holders</b> (<see cref="Holders"/>). A settlement with a controller (EmpireQuery.TryGetController — the
///   D-037 control relation; were a malformed world to carry two rows for one place, the LOWEST PolityId, a function
///   of the row set) draws on EVERY settlement that polity controls. A settlement no Empire controls is its own
///   single-settlement stockpile (D-051 §6), and so is EVERY settlement of a world with no Controls rows at all (a
///   hand-built toy): there is no empire to pool over. That is deliberately NOT RoadDevelopmentQuery's toy rule,
///   which treats every settlement of a Controls-less world as the issuer's — road development is an Empire-level
///   action and needs some purse; construction is a settlement's queue and keeps its own stock there, so hand-built
///   construction worlds keep their semantics.</item>
/// <item><b>Draw order</b>. The user settlement FIRST, then the other holders in ADR-032's nearest-first order relative
///   to the user's site — <see cref="RoadDevelopmentQuery.PayingSettlements(IReadOnlyWorldState, PolityId, SettlementId, SettlementId)"/>
///   with both route ends at the user: exact integer squared pixel distance from the holder's site cell to the user's,
///   ties to the LOWER SettlementId (a composite integer key; no double is compared). Where every site cell is distinct
///   (every founded or colonized world: FoundingQuery refuses an occupied site) the user is the unique holder at
///   distance 0, so this IS that order. Pinning the user first only decides the degenerate ties that rule leaves to the
///   id — a terrain-less world, where every distance is 0 — in favour of the settlement's own stock.</item>
/// <item><b>Available</b> (<see cref="Available"/>): the exact long sum of the holders' rows of one good (checked).</item>
/// <item><b>Draw</b> (<see cref="Draw"/>): Ledger.Flow SINKS, holder by holder in the order given, each taking
///   min(remaining, held), until the quantity is met. Instant and lossless (D-051 §6: no transport, no delay, no
///   wastage — every unit that leaves a donor is a unit of the charge). It refuses BEFORE the first flow when the
///   holders cannot cover the quantity, so a refused draw mutates nothing; and it checks AFTER the loop that the whole
///   quantity was drawn (the post-condition road development carried before this helper existed), so a holder list the
///   sum over-counts — a settlement listed twice — throws instead of silently drawing less than the charge.</item>
/// </list>
///
/// ONE DRAW, TWO CALLERS: ConstructionSystem consumes a resolved queue head through it, and RoadDevelopmentSystem pays
/// a development step through it (its payers in ITS order — nearest to the route). The sum is
/// RoadDevelopmentQuery.CivilizationStock's. The sum and the flow sequence are the very loops roads used before this
/// helper existed, moved here unchanged, so a road charge flows the same units from the same rows in the same order.
/// </summary>
public static class EmpireStockpile
{
    /// <summary>The settlements whose stocks <paramref name="user"/> draws on, in draw order (see the header):
    /// <paramref name="user"/> first, then its controller's other settlements nearest-first, ties by id; or
    /// <paramref name="user"/> alone when no Empire controls it.</summary>
    public static SettlementId[] Holders(IReadOnlyWorldState world, SettlementId user)
    {
        if (!EmpireQuery.TryGetController(world, user, out PolityId polity)) return [user];
        SettlementId[] order = RoadDevelopmentQuery.PayingSettlements(world, polity, user, user);
        var result = new SettlementId[order.Length + (Contains(order, user) ? 0 : 1)];
        result[0] = user;
        int n = 1;
        for (int i = 0; i < order.Length; i++)
            if (order[i].Value != user.Value) result[n++] = order[i];
        return result;
    }

    /// <summary>The holders' combined holding of <paramref name="good"/>: the exact sum of each holder's FIRST row of
    /// it (GoodStockIndex — the row every reader takes), a holder with no row contributing 0.</summary>
    public static long Available(IReadOnlyTable<GoodStockRow> stocks, SettlementId[] holders, GoodId good)
    {
        long total = 0;
        checked
        {
            for (int p = 0; p < holders.Length; p++)
            {
                int idx = GoodStockIndex.IndexOf(stocks, holders[p], good);
                if (idx >= 0) total += stocks[idx].Amount.Value;
            }
        }
        return total;
    }

    /// <summary>
    /// Draws exactly <paramref name="qty"/> units of <paramref name="good"/> from <paramref name="holders"/> in the
    /// order given: a Ledger.Flow sink under <paramref name="reason"/> per row touched, each taking min(remaining,
    /// held), skipping a holder with no row or nothing held. Throws (and flows nothing) when the holders cannot cover
    /// it — the caller established availability first, so a short purse here means the state moved under it and
    /// silence would hide that. Throws too when the loop ends with any of the quantity undrawn (the holders' sum counted
    /// a unit the loop could not take — a duplicated holder): never a silent short charge.
    /// </summary>
    public static void Draw(
        Ledger ledger, Table<GoodStockRow> stocks, SettlementId[] holders, GoodId good, long qty, ReasonId reason)
    {
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty), qty, "a draw is never negative.");
        long held = Available(stocks, holders, good);
        if (held < qty)
            throw new LedgerOverdrawException(
                $"empire stockpile: drawing {qty} of good {good.Value} exceeds the {held} its {holders.Length} holder(s) hold.");

        long left = qty;
        for (int p = 0; p < holders.Length && left > 0; p++)
        {
            int idx = GoodStockIndex.IndexOf(stocks, holders[p], good);
            if (idx < 0) continue;
            long take = Math.Min(left, stocks[idx].Amount.Value);
            if (take <= 0) continue;
            ledger.Flow(ref stocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(good),
                reason, take, FlowDirection.Sink, OverdrawPolicy.Throw);
            left -= take;
        }
        if (left != 0)
            throw new InvalidOperationException(
                $"empire stockpile: the holders' holding of good {good.Value} was checked but did not cover the charge ({left} of {qty} undrawn).");
    }

    /// <summary>
    /// D-055 (road works, §1.5 of the batch-4 design): <see cref="Draw(Ledger, Table{GoodStockRow}, SettlementId[], GoodId, long, ReasonId)"/>
    /// with a per-holder floor — holder p gives at most its row's amount less <paramref name="keep"/>[p] (the units its OWN
    /// single-settlement purse sets aside for a construction head), never below it. Null <paramref name="keep"/>: the plain
    /// draw, unit for unit. Refuses BEFORE the first flow when the holders' free units cannot cover the quantity, and checks
    /// after the loop that the whole quantity was drawn — never a silent short charge.
    /// </summary>
    public static void Draw(
        Ledger ledger, Table<GoodStockRow> stocks, SettlementId[] holders, GoodId good, long qty, ReasonId reason, long[]? keep)
    {
        if (keep is null)
        {
            Draw(ledger, stocks, holders, good, qty, reason);
            return;
        }
        if (keep.Length != holders.Length)
            throw new ArgumentException($"empire stockpile: {keep.Length} floors for {holders.Length} holders.", nameof(keep));
        if (qty < 0) throw new ArgumentOutOfRangeException(nameof(qty), qty, "a draw is never negative.");
        long free = 0;
        checked
        {
            for (int p = 0; p < holders.Length; p++)
            {
                int idx = GoodStockIndex.IndexOf(stocks, holders[p], good);
                if (idx >= 0) free += Math.Max(0, stocks[idx].Amount.Value - keep[p]);
            }
        }
        if (free < qty)
            throw new LedgerOverdrawException(
                $"empire stockpile: drawing {qty} of good {good.Value} exceeds the {free} its {holders.Length} holder(s) hold beyond their set-asides.");

        long left = qty;
        for (int p = 0; p < holders.Length && left > 0; p++)
        {
            int idx = GoodStockIndex.IndexOf(stocks, holders[p], good);
            if (idx < 0) continue;
            long take = Math.Min(left, Math.Max(0, stocks[idx].Amount.Value - keep[p]));
            if (take <= 0) continue;
            ledger.Flow(ref stocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(good),
                reason, take, FlowDirection.Sink, OverdrawPolicy.Throw);
            left -= take;
        }
        if (left != 0)
            throw new InvalidOperationException(
                $"empire stockpile: the holders' free holding of good {good.Value} was checked but did not cover the charge ({left} of {qty} undrawn).");
    }

    private static bool Contains(SettlementId[] ids, SettlementId id)
    {
        for (int i = 0; i < ids.Length; i++) if (ids[i].Value == id.Value) return true;
        return false;
    }
}
