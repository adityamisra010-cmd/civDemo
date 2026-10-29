using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;

namespace Sim.Core.Systems.Military;

/// <summary>Tables handed to <see cref="MilitarySystem"/> (ADR-055). It is the SOLE OWNER of FormationPersonnel, Reequips,
/// ReequipEscrow and ReequipLog (schema v39 §70–§73). MilitaryUnits is a SANCTIONED SHARED TABLE with AgeTransitionSystem;
/// Buckets and GoodStocks are the sanctioned shared stocks (SystemCatalog's ownership record); <c>Holds</c> is construction's
/// NEXT ConstructionHolds, handed READ-ONLY (a declared same-step edge, as discovery's and road development's).</summary>
public sealed record MilitaryTables(
    Table<MilitaryUnitRow> Units, Table<FormationPersonnelRow> Personnel, Table<ReequipRow> Reequips,
    Table<ReequipEscrowRow> Escrow, Table<ReequipLogRow> Log,
    Table<BucketRow> Buckets, Table<GoodStockRow> GoodStocks, IReadOnlyTable<ConstructionHoldRow> Holds);

/// <summary>
/// ADR-055 (M7 recruitment slice; unit charter §6.5–§6.6; D-065 R1; Q7) — RECRUITMENT, PERSONNEL, UPKEEP AND RE-EQUIPMENT.
/// SystemId 30; pipeline slot AFTER agetransition, so an Age transition's free modernization (ADR-031) is applied and logged
/// before a re-equipment completes in the same step (§6.6 "Interaction with ADR-031").
///
/// THE STEP, in this FIXED order (knowledge, control and the roster from PREV; stocks, buckets and formations from NEXT —
/// the shared tables, after every earlier system of the step):
///  1. UPKEEP. For every manned formation (personnel rows in ascending unit order) the grain its people eat this step,
///     personnel × military.upkeepGrainPerPersonYear × dtYears + the banked remainder, whole units drawn from its settlement
///     first then the owner's purse nearest-first (EmpireStockpile.Holders) beyond construction's set-asides, SUNK under
///     reason FormationUpkeep; the sub-unit fraction is banked (D-004); what the purse cannot supply is added to UnfedUnits.
///  2. ORDERS, in log order (each against what earlier ones left; one ReequipFormation per formation per step — the first
///     valid one wins):
///     • DisbandFormation — the issuer's formation stationed at a settlement: a pending re-equipment's escrow returns to that
///       settlement (Ledger.Transfer), its people return to that settlement's adult buckets (Ledger.Transfer,
///       RecruitmentQuery.AllocateReturn), its rows are removed, ReequipLogRow(cause 3) records it.
///     • RecruitFormation — RecruitmentQuery.CheckRecruit (§6.5): the inputs are drawn and SUNK (reason Recruitment), the
///       personnel transferred out of the settlement's adult buckets (RecruitmentQuery.AllocateDraw) into a new personnel
///       row, and a MilitaryUnitRow appended with the next id.
///     • ReequipFormation, Amount 0 — CANCEL: the escrow returns to the formation's settlement, the rows are removed,
///       ReequipLogRow(cause 2).
///     • ReequipFormation, Amount ≥ 1 — RecruitmentQuery.CheckReequip (§6.6): the target's inputs move into ESCROW
///       (Ledger.Transfer, beyond construction's set-asides), the personnel difference is drawn or returned
///       (Ledger.Transfer), and a ReequipRow is inserted (its accrual starts in the next step).
///  3. RE-EQUIPMENT PROGRESS, after the orders (so a cancel delivered in a step wins over a completion that step would
///     reach). Every pending re-equipment accepted in an EARLIER step accrues dtDays; one whose accrual reaches RequiredDays
///     COMPLETES: its escrow is SUNK (reason Reequipment), the formation becomes the target (family and identity), its
///     experience is kept (classes b, c) or multiplied by military.reequipExperienceTransfer (class a, read from the
///     formation's family NOW — after this step's modernization), and ReequipLogRow(cause 1) is written.
/// ATOMIC: every order is judged in full before anything moves, so a refused order changes nothing. Conservation: people
/// move only by Ledger.Transfer between buckets and personnel rows; goods leave only by Ledger.Flow sinks (Recruitment,
/// FormationUpkeep, Reequipment) or move by Ledger.Transfer into and out of escrow. Inert with sim.json military off.
/// </summary>
public sealed class MilitarySystem(SimConfig cfg) : ISimSystem<MilitaryTables>
{
    public static readonly SystemId WellKnownId = new(30);
    public const string Name = "military";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<MilitaryTables> ctx)
    {
        if (!RecruitmentQuery.Enabled(_cfg) || _cfg.Goods is null) return;
        IReadOnlyWorldState prev = ctx.Prev;
        MilitaryTables t = ctx.Owned;
        UnitFamilyContent families = _cfg.UnitFamilies!;

        Upkeep(ctx, prev, t);

        var reequipped = new List<int>();
        for (int i = 0; i < ctx.Orders.Count; i++)
        {
            OrderRecord order = ctx.Orders[i];
            switch (order.Kind)
            {
                case OrderKind.DisbandFormation:
                    if (order.Amount == 0.0) Disband(ctx, prev, t, order);
                    break;
                case OrderKind.RecruitFormation:
                    Recruit(ctx, prev, t, families, order);
                    break;
                case OrderKind.ReequipFormation:
                    if (reequipped.Contains(order.TargetId)) break;
                    if (Reequip(ctx, prev, t, families, order)) reequipped.Add(order.TargetId);
                    break;
            }
        }

        Progress(ctx, prev, t, families);

        if (RecruitmentQuery.InvariantViolation(t.Units, t.Personnel, t.Reequips, t.Escrow, t.Log) is { } broken)
            throw new InvalidOperationException("military: " + broken + " at the end of the step.");
    }

    // ------------------------------------------------------------------ 1. upkeep

    private void Upkeep(SimContext<MilitaryTables> ctx, IReadOnlyWorldState prev, MilitaryTables t)
    {
        MilitaryConfig m = _cfg.Military!;
        var grain = new GoodId(_cfg.Goods!.GrainId);
        for (int p = 0; p < t.Personnel.Count; p++)
        {
            ref FormationPersonnelRow row = ref t.Personnel.Ref(p);
            if (row.Count.Value <= 0) continue;
            int u = RecruitmentQuery.UnitIndex(t.Units, row.Unit);
            if (u < 0) continue;
            double exact = row.Count.Value * m.UpkeepGrainPerPersonYear * ctx.DtYears + row.UpkeepRemainder;
            long whole = ConservedMath.WholeUnits(exact, $"formation {row.Unit} upkeep");
            row.UpkeepRemainder = exact - whole;
            if (whole <= 0) continue;
            SettlementId at = t.Units[u].Location;
            long take = 0;
            if (at.Value >= 0)
            {
                SettlementId[] holders = EmpireStockpile.Holders(prev, at);
                long free = DiscoveryQuery.FreeHolding(prev, _cfg, t.GoodStocks, t.Holds, holders, grain, out long[]? keep);
                take = Math.Min(whole, free);
                if (take > 0) EmpireStockpile.Draw(ctx.Ledger, t.GoodStocks, holders, grain, take, ReasonIds.FormationUpkeep, keep);
            }
            ref FormationPersonnelRow again = ref t.Personnel.Ref(p);
            again.UnfedUnits = checked(again.UnfedUnits + (whole - take));
        }
    }

    // ------------------------------------------------------------------ 3. re-equipment progress

    private void Progress(SimContext<MilitaryTables> ctx, IReadOnlyWorldState prev, MilitaryTables t, UnitFamilyContent families)
    {
        long turn = prev.Clock.Turn + 1;   // the state the completion first appears in
        for (int r = 0; r < t.Reequips.Count;)
        {
            ReequipRow row = t.Reequips[r];
            if (row.AcceptedTurn > prev.Clock.Turn) { r++; continue; }   // accepted in this very step: its work starts next step
            long elapsed = checked(row.ElapsedDays + ctx.DtDays);
            if (elapsed < row.RequiredDays)
            {
                t.Reequips[r] = row with { ElapsedDays = elapsed };
                r++;
                continue;
            }
            int u = RecruitmentQuery.UnitIndex(t.Units, row.Unit);
            if (u < 0) throw new InvalidOperationException($"military: re-equipment of formation {row.Unit}, which does not exist.");
            MilitaryUnitRow unit = t.Units[u];
            // The escrowed materials are consumed (no salvage credit for the old equipment, §6.6).
            for (int e = 0; e < t.Escrow.Count; e++)
            {
                ReequipEscrowRow x = t.Escrow[e];
                if (x.Unit != row.Unit) continue;
                ctx.Ledger.Flow(ref t.Escrow.Ref(e).Units, ConservedQuantityIds.OfGood(new GoodId(x.Good)),
                    ReasonIds.Reequipment, x.Units.Value, FlowDirection.Sink, OverdrawPolicy.Throw);
            }
            RemoveEscrowOf(t.Escrow, row.Unit);
            ReequipClass cls = RecruitmentQuery.CompletionClass(families, unit.Family, unit.Identity, row.TargetIdentity);
            double after = RecruitmentQuery.ExperienceAfter(_cfg, cls, unit.Experience);
            t.Log.Add(new ReequipLogRow(turn, unit.Id, unit.Owner, unit.Family, unit.Identity, row.TargetFamily, row.TargetIdentity,
                1, row.PersonnelDelta, unit.Experience, after));
            t.Units[u] = unit with { Family = row.TargetFamily, Identity = row.TargetIdentity, Experience = after };
            RemoveAt(t.Reequips, r);
        }
    }

    // ------------------------------------------------------------------ 2. orders

    private void Disband(SimContext<MilitaryTables> ctx, IReadOnlyWorldState prev, MilitaryTables t, OrderRecord order)
    {
        int u = RecruitmentQuery.UnitIndex(t.Units, order.TargetId);
        if (u < 0) return;
        MilitaryUnitRow unit = t.Units[u];
        if (unit.Owner.Value != order.ActorId || unit.Location.Value < 0) return;
        SettlementId at = unit.Location;
        int pi = RecruitmentQuery.PersonnelIndex(t.Personnel, unit.Id);
        long people = pi < 0 ? 0 : t.Personnel[pi].Count.Value;
        long[]? back = people > 0 ? RecruitmentQuery.AllocateReturn(t.Buckets, at, people) : [];
        if (back is null) return;   // the settlement has no bucket to return people to: refused, nothing moves
        if (!EscrowReturnable(t, unit.Id, at)) return;

        int target = unit.Identity, targetFamily = unit.Family;
        long delta = 0;
        int ri = RecruitmentQuery.ReequipIndex(t.Reequips, unit.Id);
        if (ri >= 0)
        {
            target = t.Reequips[ri].TargetIdentity;
            targetFamily = t.Reequips[ri].TargetFamily;
            delta = t.Reequips[ri].PersonnelDelta;
            ReturnEscrow(ctx, t, unit.Id, at);
            RemoveAt(t.Reequips, ri);
        }
        if (pi >= 0)
        {
            for (int b = 0; b < back.Length; b++)
                if (back[b] > 0)
                    ctx.Ledger.Transfer(ref t.Personnel.Ref(pi).Count, ref t.Buckets.Ref(b).Count, back[b], OverdrawPolicy.Throw);
            RemoveAt(t.Personnel, pi);
        }
        t.Log.Add(new ReequipLogRow(prev.Clock.Turn + 1, unit.Id, unit.Owner, unit.Family, unit.Identity, targetFamily, target,
            3, delta, unit.Experience, unit.Experience));
        RemoveAt(t.Units, RecruitmentQuery.UnitIndex(t.Units, unit.Id));
    }

    private void Recruit(SimContext<MilitaryTables> ctx, IReadOnlyWorldState prev, MilitaryTables t, UnitFamilyContent families, OrderRecord order)
    {
        if (order.TargetId < 0 || !(order.Amount >= 1.0) || order.Amount != Math.Floor(order.Amount) || order.Amount > int.MaxValue) return;
        var settlement = new SettlementId(order.TargetId);
        int key = (int)order.Amount;
        var view = new MilitaryView(t.GoodStocks, t.Holds, t.Buckets, t.Units, t.Personnel, t.Reequips);
        if (!RecruitmentQuery.CheckRecruit(prev, _cfg, view, order.Actor, settlement, key).Ok) return;
        UnitIdentity identity = families.IdentityByKey(key)!;
        UnitRecruitment r = identity.Recruitment!;
        long[] draw = RecruitmentQuery.AllocateDraw(t.Buckets, settlement, r.Personnel)
            ?? throw new InvalidOperationException("military: the recruitment predicate passed but the adults cannot be drawn.");

        SettlementId[] holders = EmpireStockpile.Holders(prev, settlement);
        for (int i = 0; i < r.Inputs.Count; i++)
        {
            var good = new GoodId(r.Inputs[i].GoodId);
            DiscoveryQuery.FreeHolding(prev, _cfg, t.GoodStocks, t.Holds, holders, good, out long[]? keep);
            EmpireStockpile.Draw(ctx.Ledger, t.GoodStocks, holders, good, r.Inputs[i].Qty, ReasonIds.Recruitment, keep);
        }

        int id = RecruitmentQuery.NextUnitId(t.Units, prev.UnitConversions, t.Log);
        (double x, double y) = SitePosition(prev, settlement);
        t.Units.Add(new MilitaryUnitRow(id, order.Actor, identity.FamilyKey, identity.Key, settlement, x, y, Experience: 0.0, Army: 0));
        int pi = InsertPersonnel(t.Personnel, id);
        for (int b = 0; b < draw.Length; b++)
            if (draw[b] > 0)
                ctx.Ledger.Transfer(ref t.Buckets.Ref(b).Count, ref t.Personnel.Ref(pi).Count, draw[b], OverdrawPolicy.Throw);
    }

    private bool Reequip(SimContext<MilitaryTables> ctx, IReadOnlyWorldState prev, MilitaryTables t, UnitFamilyContent families, OrderRecord order)
    {
        if (!(order.Amount >= 0.0) || order.Amount != Math.Floor(order.Amount) || order.Amount > int.MaxValue) return false;
        int u = RecruitmentQuery.UnitIndex(t.Units, order.TargetId);
        if (u < 0 || t.Units[u].Owner.Value != order.ActorId) return false;
        MilitaryUnitRow unit = t.Units[u];

        if (order.Amount == 0.0)
        {
            // CANCEL: the escrow returns to the formation's settlement; the labour already spent is not refunded and the
            // people already moved stay where they are.
            int ri = RecruitmentQuery.ReequipIndex(t.Reequips, unit.Id);
            if (ri < 0 || unit.Location.Value < 0 || !EscrowReturnable(t, unit.Id, unit.Location)) return false;
            ReequipRow row = t.Reequips[ri];
            ReturnEscrow(ctx, t, unit.Id, unit.Location);
            t.Log.Add(new ReequipLogRow(prev.Clock.Turn + 1, unit.Id, unit.Owner, unit.Family, unit.Identity,
                row.TargetFamily, row.TargetIdentity, 2, row.PersonnelDelta, unit.Experience, unit.Experience));
            RemoveAt(t.Reequips, ri);
            return true;
        }

        int key = (int)order.Amount;
        var view = new MilitaryView(t.GoodStocks, t.Holds, t.Buckets, t.Units, t.Personnel, t.Reequips);
        if (!RecruitmentQuery.CheckReequip(prev, _cfg, view, order.Actor, unit.Id, key, out _).Ok) return false;
        UnitIdentity target = families.IdentityByKey(key)!;
        UnitRecruitment r = target.Recruitment!;
        SettlementId at = unit.Location;
        long current = RecruitmentQuery.PersonnelOf(t.Personnel, unit.Id);
        long delta = r.Personnel - current;
        long[]? people = delta > 0 ? RecruitmentQuery.AllocateDraw(t.Buckets, at, delta)
            : delta < 0 ? RecruitmentQuery.AllocateReturn(t.Buckets, at, -delta) : [];
        if (people is null) return false;   // the predicate admitted it; a settlement with no bucket row cannot take people back

        // Materials into escrow (reserved: out of the stockpile, so no later consumer can spend them).
        SettlementId[] holders = EmpireStockpile.Holders(prev, at);
        for (int i = 0; i < r.Inputs.Count; i++)
        {
            var good = new GoodId(r.Inputs[i].GoodId);
            DiscoveryQuery.FreeHolding(prev, _cfg, t.GoodStocks, t.Holds, holders, good, out long[]? keep);
            int e = InsertEscrow(t.Escrow, unit.Id, good.Value);
            TransferFromPurse(ctx.Ledger, t.GoodStocks, holders, good, r.Inputs[i].Qty, keep, t.Escrow, e);
        }
        // The personnel difference.
        int pi = RecruitmentQuery.PersonnelIndex(t.Personnel, unit.Id);
        if (delta != 0 && pi < 0) pi = InsertPersonnel(t.Personnel, unit.Id);
        for (int b = 0; b < people.Length; b++)
        {
            if (people[b] <= 0) continue;
            if (delta > 0) ctx.Ledger.Transfer(ref t.Buckets.Ref(b).Count, ref t.Personnel.Ref(pi).Count, people[b], OverdrawPolicy.Throw);
            else ctx.Ledger.Transfer(ref t.Personnel.Ref(pi).Count, ref t.Buckets.Ref(b).Count, people[b], OverdrawPolicy.Throw);
        }
        InsertReequip(t.Reequips, new ReequipRow(unit.Id, unit.Owner, target.FamilyKey, target.Key, prev.Clock.Turn, prev.Clock.Turn + 1,
            RecruitmentQuery.RequiredDays(target), 0, delta));
        return true;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Moves exactly <paramref name="qty"/> of <paramref name="good"/> from the purse into escrow row
    /// <paramref name="escrowRow"/>, holder by holder in the order given, each giving at most its amount less its floor —
    /// EmpireStockpile.Draw's walk, as a TRANSFER (the units stay in the world).</summary>
    private static void TransferFromPurse(
        Ledger ledger, Table<GoodStockRow> stocks, SettlementId[] holders, GoodId good, long qty, long[]? keep,
        Table<ReequipEscrowRow> escrow, int escrowRow)
    {
        long left = qty;
        for (int p = 0; p < holders.Length && left > 0; p++)
        {
            int idx = GoodStockIndex.IndexOf(stocks, holders[p], good);
            if (idx < 0) continue;
            long floor = keep is null ? 0 : keep[p];
            long take = Math.Min(left, Math.Max(0, stocks[idx].Amount.Value - floor));
            if (take <= 0) continue;
            ledger.Transfer(ref stocks.Ref(idx).Amount, ref escrow.Ref(escrowRow).Units, take, OverdrawPolicy.Throw);
            left -= take;
        }
        if (left != 0)
            throw new InvalidOperationException(
                $"military: the purse's free holding of good {good.Value} was checked but did not cover the escrow ({left} of {qty} not moved).");
    }

    /// <summary>Whether every escrow row of <paramref name="unit"/> has a stock row at <paramref name="at"/> to return to.</summary>
    private static bool EscrowReturnable(MilitaryTables t, int unit, SettlementId at)
    {
        for (int e = 0; e < t.Escrow.Count; e++)
            if (t.Escrow[e].Unit == unit && GoodStockIndex.IndexOf(t.GoodStocks, at, new GoodId(t.Escrow[e].Good)) < 0) return false;
        return true;
    }

    /// <summary>Transfers every escrow row of <paramref name="unit"/> back to <paramref name="at"/>'s stock and removes them.</summary>
    private static void ReturnEscrow(SimContext<MilitaryTables> ctx, MilitaryTables t, int unit, SettlementId at)
    {
        for (int e = 0; e < t.Escrow.Count; e++)
        {
            ReequipEscrowRow x = t.Escrow[e];
            if (x.Unit != unit) continue;
            int idx = GoodStockIndex.IndexOf(t.GoodStocks, at, new GoodId(x.Good));
            ctx.Ledger.Transfer(ref t.Escrow.Ref(e).Units, ref t.GoodStocks.Ref(idx).Amount, x.Units.Value, OverdrawPolicy.Throw);
        }
        RemoveEscrowOf(t.Escrow, unit);
    }

    private static (double X, double Y) SitePosition(IReadOnlyWorldState prev, SettlementId settlement)
    {
        int size = prev.Terrain?.Size ?? 1;
        int site = 0;
        for (int s = 0; s < prev.Settlements.Count; s++)
            if (prev.Settlements[s].Id.Value == settlement.Value) site = prev.Settlements[s].SiteCell;
        return Pathing.MapTopology.CellPosition(site, size);
    }

    private static int InsertPersonnel(Table<FormationPersonnelRow> rows, int unit)
    {
        int at = rows.Count;
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Unit > unit) { at = i; break; }
        InsertAt(rows, at, new FormationPersonnelRow(unit, Conserved.Zero, 0.0, 0));
        return at;
    }

    private static int InsertEscrow(Table<ReequipEscrowRow> rows, int unit, int good)
    {
        int at = rows.Count;
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Unit > unit || (rows[i].Unit == unit && rows[i].Good > good)) { at = i; break; }
        InsertAt(rows, at, new ReequipEscrowRow(unit, good, Conserved.Zero));
        return at;
    }

    private static void InsertReequip(Table<ReequipRow> rows, ReequipRow row)
    {
        int at = rows.Count;
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Unit > row.Unit) { at = i; break; }
        InsertAt(rows, at, row);
    }

    /// <summary>Removes the escrow rows of <paramref name="unit"/> — each emptied by a Ledger operation first (a row is removed
    /// only at zero, so nothing conserved vanishes).</summary>
    private static void RemoveEscrowOf(Table<ReequipEscrowRow> rows, int unit)
    {
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Unit != unit) continue;
            if (rows[i].Units.Value != 0)
                throw new InvalidOperationException($"military: escrow row of formation {unit} removed holding {rows[i].Units.Value} units.");
            RemoveAt(rows, i);
        }
    }

    private static void InsertAt<T>(Table<T> table, int index, T row) where T : unmanaged
    {
        table.Add(row);
        for (int i = table.Count - 1; i > index; i--) table[i] = table[i - 1];
        table[index] = row;
    }

    /// <summary>Removes row <paramref name="index"/>, keeping the others in order (rows are copied, values unchanged — a
    /// personnel or escrow row is removed only once a Ledger transfer has emptied it).</summary>
    private static void RemoveAt<T>(Table<T> table, int index) where T : unmanaged
    {
        var kept = new T[table.Count];
        int n = 0;
        for (int i = 0; i < table.Count; i++)
            if (i != index) kept[n++] = table[i];
        table.Clear();
        for (int i = 0; i < n; i++) table.Add(kept[i]);
    }
}
