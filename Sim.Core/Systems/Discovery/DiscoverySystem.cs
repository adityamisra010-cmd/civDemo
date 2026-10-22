using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Discovery;

/// <summary>Tables owned by <see cref="DiscoverySystem"/> (schema v35 sections 63–66). GoodStocks is the SANCTIONED SHARED
/// STOCK (SystemCatalog's ownership record — discovery is a sink-only holder); <c>Holds</c> is construction's NEXT
/// ConstructionHolds table, handed READ-ONLY (a declared same-step edge, as road development's).</summary>
public sealed record DiscoveryTables(
    Table<KnownExtentRow> KnownExtents, Table<ExplorationRow> Explorations,
    Table<SettlementSightingRow> Sightings, Table<SettlementSurveyRow> Surveys,
    Table<GoodStockRow> GoodStocks, IReadOnlyTable<ConstructionHoldRow> Holds);

/// <summary>
/// M6 Track EX (D-062; ADR-044) — DISCOVERY: the Explore (OrderKind 14) and Prospect (OrderKind 15) orders, the known extent,
/// the explored radii, the settlement sightings and the surveys. SystemId 29; pipeline slot after colonization.
///
/// THE STEP, in this FIXED order (every signal from PREV; the owned tables are rebuilt from PREV's rows, which the kernel's
/// clone carried into NEXT; the stocks are NEXT's — the shared stock — less construction's set-asides of THIS step):
///  1. ORDERS, in log order (one per (polity, kind, target) per turn: a later duplicate is refused):
///     • Explore — <see cref="DiscoveryQuery.CheckExplore(IReadOnlyWorldState, SimConfig, PolityId, SettlementId, IReadOnlyTable{GoodStockRow}, IReadOnlyTable{ConstructionHoldRow})"/>
///       on PREV and the NEXT purse: Ok ⇒ fog.exploreGrain grain is drawn through EmpireStockpile.Draw (the origin first,
///       never below what construction set aside) and SUNK under ReasonIds.Expedition; the origin's explored radius grows by
///       fog.exploreStepNodes (capped), and the disc of the new radius around its node is revealed.
///     • Prospect — <see cref="DiscoveryQuery.CheckProspect(IReadOnlyWorldState, SimConfig, PolityId, SettlementId, IReadOnlyTable{GoodStockRow}, IReadOnlyTable{ConstructionHoldRow})"/>:
///       Ok ⇒ the prospect's tools are drawn and SUNK under ReasonIds.Prospecting and SettlementSurveyRow(polity,
///       settlement, PREV's turn) is written. Anything else changes nothing and charges nothing.
///  2. THE KNOWN EXTENT (fog on): for every polity of the roster, of a control row or of a stored extent, ascending, the
///     stored extent ∪ the seed discs of the settlements it controls ∪ its formations' sight (DiscoveryQuery.KnownBits on
///     PREV — rules E1/E2/E4) ∪ this step's revealed discs, rewritten as its non-zero words (monotone: never a bit cleared).
///  3. SIGHTINGS (fog on): every settlement whose node is VISIBLE to a polity on PREV, or lies in a disc an Explore revealed
///     this step, gets SettlementSightingRow(polity, settlement, PREV's turn) — inserted or its turn updated.
/// Every owned table ends in strictly ascending key order (DiscoveryQuery.InvariantViolation, asserted at the end).
/// SWITCHES: fog off ⇒ no Explore is applied and nothing is written to sections 63–65; discovery off ⇒ no Prospect is
/// applied and section 66 is untouched; both off ⇒ the step does nothing at all (the attribution control).
/// </summary>
public sealed class DiscoverySystem(SimConfig cfg) : ISimSystem<DiscoveryTables>
{
    public static readonly SystemId WellKnownId = new(29);
    public const string Name = "discovery";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<DiscoveryTables> ctx)
    {
        bool fog = DiscoveryQuery.FogOn(_cfg) && ctx.Prev.Terrain is not null && _cfg.Goods is not null;
        bool discovery = DepositConcealment.Enabled(_cfg);
        if (!fog && !discovery) return;

        IReadOnlyWorldState prev = ctx.Prev;
        DiscoveryTables owned = ctx.Owned;
        long turn = prev.Clock.Turn;
        var revealed = new List<(int Polity, int Origin, int Radius)>();   // this step's Explore discs
        var seen = new List<(int Polity, OrderKind Kind, int Target)>();

        // 1. Orders, in log order.
        for (int i = 0; i < ctx.Orders.Count; i++)
        {
            OrderRecord order = ctx.Orders[i];
            if (order.Kind is not (OrderKind.Explore or OrderKind.Prospect)) continue;
            if (Contains(seen, order.ActorId, order.Kind, order.TargetId)) continue;   // one per (polity, kind, target) per turn
            seen.Add((order.ActorId, order.Kind, order.TargetId));
            if (order.Amount != 0.0 || order.TargetId < 0) continue;   // the load rule, re-applied (an in-memory log is unvalidated)
            var settlement = new SettlementId(order.TargetId);

            if (order.Kind == OrderKind.Explore)
            {
                if (!fog) continue;
                if (DiscoveryQuery.CheckExplore(prev, _cfg, order.Actor, settlement, owned.GoodStocks, owned.Holds) != ExploreVerdict.Ok) continue;
                Charge(ctx, prev, settlement, new GoodId(_cfg.Goods!.GrainId), DiscoveryQuery.ExploreCost(_cfg), ReasonIds.Expedition);
                int radius = DiscoveryQuery.NextRadius(prev, _cfg.Fog!, order.Actor, settlement);
                UpsertExploration(owned.Explorations, order.Actor, settlement, radius);
                revealed.Add((order.ActorId, DiscoveryQuery.NodeOfSettlement(prev, settlement), radius));
            }
            else
            {
                if (!discovery) continue;
                if (DiscoveryQuery.CheckProspect(prev, _cfg, order.Actor, settlement, owned.GoodStocks, owned.Holds) != ProspectVerdict.Ok) continue;
                Charge(ctx, prev, settlement, new GoodId(_cfg.Goods!.IdOf(DepositConcealment.ProspectGood)),
                    DiscoveryQuery.ProspectCost(prev, _cfg, order.Actor), ReasonIds.Prospecting);
                InsertSurvey(owned.Surveys, new SettlementSurveyRow(order.Actor, settlement, turn));
            }
        }

        if (fog)
        {
            FogConfig f = _cfg.Fog!;
            (int l, _, _, bool wrap) = DiscoveryQuery.Lattice(prev);
            int words = DiscoveryQuery.WordCount(l);
            int[] polities = Polities(prev);

            // 2. The known extent, rebuilt polity by polity (ascending) from PREV's rows.
            var rows = new List<KnownExtentRow>();
            foreach (int p in polities)
            {
                var polity = new PolityId(p);
                ulong[] bits = DiscoveryQuery.KnownBits(prev, f, polity) ?? new ulong[words];
                foreach ((int who, int origin, int radius) in revealed)
                    if (who == p) DiscoveryQuery.OrDisc(bits, l, wrap, origin, radius);
                for (int w = 0; w < bits.Length; w++)
                    if (bits[w] != 0) rows.Add(new KnownExtentRow(polity, w, bits[w]));
            }
            owned.KnownExtents.Clear();
            foreach (KnownExtentRow row in rows) owned.KnownExtents.Add(row);

            // 3. Sightings: what each polity sees now (PREV), and what this step's expeditions revealed.
            foreach (int p in polities)
            {
                var polity = new PolityId(p);
                ulong[]? visible = DiscoveryQuery.VisibleBits(prev, f, polity);
                for (int s = 0; s < prev.Settlements.Count; s++)
                {
                    SettlementRow row = prev.Settlements[s];
                    int node = DiscoveryQuery.NodeOfCell(prev, row.SiteCell);
                    bool sighted = DiscoveryQuery.Has(visible, node);
                    if (!sighted)
                        foreach ((int who, int origin, int radius) in revealed)
                            if (who == p && DiscoveryQuery.InDisc(l, wrap, origin, radius, node)) { sighted = true; break; }
                    if (sighted) UpsertSighting(owned.Sightings, polity, row.Id, turn);
                }
            }
        }

        if (DiscoveryQuery.InvariantViolation(owned.KnownExtents, owned.Explorations, owned.Sightings, owned.Surveys) is { } broken)
            throw new InvalidOperationException("discovery: " + broken + " at the end of the step.");
    }

    /// <summary>Draws <paramref name="qty"/> of <paramref name="good"/> from <paramref name="settlement"/>'s purse
    /// (EmpireStockpile.Holders) above construction's set-asides, sunk under <paramref name="reason"/>. The verdict was
    /// checked against the same purse, so a short purse here is a bug and throws.</summary>
    private void Charge(SimContext<DiscoveryTables> ctx, IReadOnlyWorldState prev, SettlementId settlement, GoodId good, long qty, ReasonId reason)
    {
        if (qty <= 0) return;
        SettlementId[] holders = EmpireStockpile.Holders(prev, settlement);
        DiscoveryQuery.FreeHolding(prev, _cfg, ctx.Owned.GoodStocks, ctx.Owned.Holds, holders, good, out long[]? keep);
        EmpireStockpile.Draw(ctx.Ledger, ctx.Owned.GoodStocks, holders, good, qty, reason, keep);
    }

    /// <summary>Every polity the step stores an extent for: the roster, every controller, and every polity with a stored
    /// extent (a polity that lost its last settlement keeps what it knew), ascending and distinct.</summary>
    private static int[] Polities(IReadOnlyWorldState prev)
    {
        var ids = new List<int>();
        for (int i = 0; i < prev.Polities.Count; i++) ids.Add(prev.Polities[i].Id.Value);
        for (int i = 0; i < prev.Controls.Count; i++) ids.Add(prev.Controls[i].Polity.Value);
        for (int i = 0; i < prev.KnownExtents.Count; i++) ids.Add(prev.KnownExtents[i].Polity.Value);
        ids.Sort();
        var distinct = new List<int>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] >= 0 && (distinct.Count == 0 || distinct[^1] != ids[i])) distinct.Add(ids[i]);
        return [.. distinct];
    }

    private static bool Contains(List<(int Polity, OrderKind Kind, int Target)> seen, int polity, OrderKind kind, int target)
    {
        for (int i = 0; i < seen.Count; i++)
            if (seen[i].Polity == polity && seen[i].Kind == kind && seen[i].Target == target) return true;
        return false;
    }

    private static int Compare(int ap, int @as, int bp, int bs) => ap != bp ? ap.CompareTo(bp) : @as.CompareTo(bs);

    private static void UpsertExploration(Table<ExplorationRow> table, PolityId polity, SettlementId settlement, int radius)
    {
        int at = table.Count;
        for (int i = 0; i < table.Count; i++)
        {
            int c = Compare(table[i].Polity.Value, table[i].Settlement.Value, polity.Value, settlement.Value);
            if (c == 0) { table[i] = table[i] with { RadiusNodes = radius }; return; }
            if (c > 0) { at = i; break; }
        }
        InsertAt(table, at, new ExplorationRow(polity, settlement, radius));
    }

    private static void UpsertSighting(Table<SettlementSightingRow> table, PolityId polity, SettlementId settlement, long turn)
    {
        int at = table.Count;
        for (int i = 0; i < table.Count; i++)
        {
            int c = Compare(table[i].Polity.Value, table[i].Settlement.Value, polity.Value, settlement.Value);
            if (c == 0) { table[i] = table[i] with { LastSeenTurn = turn }; return; }
            if (c > 0) { at = i; break; }
        }
        InsertAt(table, at, new SettlementSightingRow(polity, settlement, turn));
    }

    private static void InsertSurvey(Table<SettlementSurveyRow> table, SettlementSurveyRow row)
    {
        int at = table.Count;
        for (int i = 0; i < table.Count; i++)
        {
            int c = Compare(table[i].Polity.Value, table[i].Settlement.Value, row.Polity.Value, row.Settlement.Value);
            if (c == 0) return;   // already surveyed (CheckProspect refused it on PREV; never twice)
            if (c > 0) { at = i; break; }
        }
        InsertAt(table, at, row);
    }

    /// <summary>Inserts <paramref name="row"/> at <paramref name="index"/>, shifting the tail (Table has append and indexed
    /// set only).</summary>
    private static void InsertAt<T>(Table<T> table, int index, T row) where T : unmanaged
    {
        table.Add(row);
        for (int i = table.Count - 1; i > index; i--) table[i] = table[i - 1];
        table[index] = row;
    }
}
