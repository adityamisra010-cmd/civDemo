using System.Globalization;
using Sim.Core.Observability;

namespace Sim.Ui.ViewModel;

/// <summary>One WHAT CHANGED line and the decomposition it expands to.</summary>
public sealed record AuditLine(string Key, string Line, IReadOnlyList<string> Detail);

/// <summary>One WHERE row: clicking it selects the settlement and centres the
/// camera on it. <see cref="Magnitude"/> is the sort key's first half, kept so
/// the tie-dense test can see what was compared.</summary>
public sealed record WhereRow(int Settlement, string Line, double Magnitude);

/// <summary>
/// T4.19 lane B (B3) — THE TURN AUDIT, from the latest TurnRecord and its
/// SettlementRecords (docs/observability-architecture.md §2, §3). Pure: takes
/// the records and a name lookup, returns strings and rows; reads no world.
///
/// WHAT CHANGED reproduces the record's accounts NUMERICALLY — every figure on
/// a line is a field of the record, and the detail under it is the account's
/// named legs, so a reader can add the legs and get the closing figure or see
/// exactly where the account failed. WHY prints the CausesRecord identity as
/// the record states it (population Δ = births − natural deaths − starvation;
/// grain Δ = harvest − eaten − spoilage − overflow, + endowment) and the
/// Reconciles flag in plain words: "reconciles to the unit" or "DISCREPANCY n —
/// simulation defect". Never rounded, never softened: a conserved stock that
/// moved without a ledger row is a defect and the audit says so (§2).
///
/// WHERE ranks settlements by a COMPOSITE KEY — (magnitude DESC, settlement id
/// ASC), compared as two explicit halves — never by a double alone and never
/// by table order. Two settlements that lost the same number of people would
/// otherwise come out in whatever order the record array happened to hold,
/// and a click on "the worst" would land on a different settlement on a
/// different machine (T1.3 / T2.1 precedent: order over doubles carries an
/// integer tie-break and a tie-dense test).
/// </summary>
public static class TurnAuditModel
{
    /// <summary>How many WHERE rows each list shows: enough to see the outliers,
    /// few enough that the panel is a ranking rather than a roster.</summary>
    public const int WhereTop = 6;

    public const string PopulationKey = "population";
    public const string GrainKey = "grain";
    public const string DwellingsKey = "dwellings";
    public const string MigrantsKey = "migrants";
    public const string FoundedKey = "founded";
    public const string ControlKey = "control";
    public const string TradeKey = "trade";

    private static string N(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static string Signed(long v) => v.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture);

    private static string Reconcile(bool reconciles, long discrepancy) => reconciles
        ? "reconciles to the unit"
        : string.Create(CultureInfo.InvariantCulture, $"DISCREPANCY {Signed(discrepancy)} - simulation defect");

    /// <summary>WHAT CHANGED, in the fixed order the packet names.</summary>
    public static IReadOnlyList<AuditLine> ChangedLines(TurnRecord turn, SettlementRecord[] settlements, Func<int, string> name)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(settlements);
        var lines = new List<AuditLine>(7);

        PopulationAccount p = turn.Population;
        lines.Add(new AuditLine(PopulationKey,
            string.Create(CultureInfo.InvariantCulture,
                $"population {N(p.Opening)} -> {N(p.Closing)}  ({Signed(p.Closing - p.Opening)})"),
            [
                string.Create(CultureInfo.InvariantCulture, $"  births          {Signed(p.Births)}"),
                string.Create(CultureInfo.InvariantCulture, $"  natural deaths  {Signed(-p.NaturalDeaths)}"),
                string.Create(CultureInfo.InvariantCulture, $"  starvation      {Signed(-p.Starvation)}"),
                "  " + Reconcile(p.Reconciles, p.Discrepancy),
            ]));

        GrainAccount g = turn.Grain;
        var grain = new List<string>(6)
        {
            string.Create(CultureInfo.InvariantCulture, $"  harvest         {Signed(g.Harvest)}"),
            string.Create(CultureInfo.InvariantCulture, $"  eaten           {Signed(-g.Eaten)}"),
            string.Create(CultureInfo.InvariantCulture, $"  spoilage        {Signed(-g.Spoilage)}"),
            string.Create(CultureInfo.InvariantCulture, $"  overflow        {Signed(-g.Overflow)}"),
        };
        if (g.Endowment != 0)
            grain.Insert(0, string.Create(CultureInfo.InvariantCulture, $"  endowment       {Signed(g.Endowment)}"));
        grain.Add("  " + Reconcile(g.Reconciles, g.Discrepancy));
        lines.Add(new AuditLine(GrainKey,
            string.Create(CultureInfo.InvariantCulture,
                $"grain {N(g.Opening)} -> {N(g.Closing)}  ({Signed(g.Closing - g.Opening)})"),
            grain));

        DwellingsAccount d = turn.Dwellings;
        lines.Add(new AuditLine(DwellingsKey,
            string.Create(CultureInfo.InvariantCulture,
                $"dwellings {N(d.Opening)} -> {N(d.Closing)}  ({Signed(d.Closing - d.Opening)})"),
            [
                string.Create(CultureInfo.InvariantCulture, $"  built           {Signed(d.Built)}"),
                string.Create(CultureInfo.InvariantCulture, $"  decayed         {Signed(-d.Decayed)}"),
                "  " + Reconcile(d.Reconciles, d.Discrepancy),
            ]));

        FlowsSummary f = turn.Flows;
        var moved = new List<string> { "  sum of MigrationFlowRow.Inflow over every settlement (= sum of Outflow)" };
        for (int i = 0; i < settlements.Length; i++)
        {
            SettlementRecord r = settlements[i];
            if (r.Population.Inflow == 0 && r.Population.Outflow == 0) continue;
            moved.Add(string.Create(CultureInfo.InvariantCulture,
                $"  {name(r.Settlement)}  in {Signed(r.Population.Inflow)}  out {Signed(-r.Population.Outflow)}"));
        }
        moved.Add("  pairwise From->To: not recorded (MigrationSystem discards the matrix, section 8 gap 5)");
        lines.Add(new AuditLine(MigrantsKey,
            string.Create(CultureInfo.InvariantCulture, $"migrants moved {N(f.MigrantsMoved)}"), moved));

        var founded = new List<string> { "  next.Settlements.Count - prev.Settlements.Count" };
        for (int i = 0; i < settlements.Length; i++)
        {
            if (!settlements[i].Founded) continue;
            founded.Add(string.Create(CultureInfo.InvariantCulture,
                $"  {name(settlements[i].Settlement)} founded: party {N(settlements[i].Population.Closing)}, provisions {N(settlements[i].Food.GrainClosing)} (source not recorded, section 8 gap 3)"));
        }
        lines.Add(new AuditLine(FoundedKey,
            string.Create(CultureInfo.InvariantCulture, $"settlements founded {f.SettlementsFounded}"), founded));

        lines.Add(new AuditLine(ControlKey,
            string.Create(CultureInfo.InvariantCulture, $"control lost {f.ControlLost}"),
            ["  ControlRows present before the step and absent after it (revolt leaves only this, section 8 gap 11)"]));

        var trade = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"  {f.TradeFlowCount} flow row(s); grain never trades (numeraire)"),
        };
        if (f.UnattributedGrainTransfer)
            trade.Add("  UNATTRIBUTED GRAIN TRANSFER: a settlement's store-loss residual is negative - an unrecorded transfer (appropriation, section 8 gap 4)");
        lines.Add(new AuditLine(TradeKey,
            string.Create(CultureInfo.InvariantCulture, $"trade units {N(f.TradeUnits)}"), trade));

        return lines;
    }

    /// <summary>WHY — the CausesRecord identities, verbatim, and the reconcile
    /// flags in plain words.</summary>
    public static IReadOnlyList<string> WhyLines(TurnRecord turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        CausesRecord c = turn.Causes;
        PopulationAccount p = turn.Population;
        GrainAccount g = turn.Grain;
        return
        [
            string.Create(CultureInfo.InvariantCulture,
                $"population delta {Signed(c.PopulationDelta)} = births {N(p.Births)} - natural deaths {N(p.NaturalDeaths)} - starvation {N(p.Starvation)} = {Signed(c.PopulationExplained)}"),
            "  " + Reconcile(p.Reconciles, p.Discrepancy),
            string.Create(CultureInfo.InvariantCulture,
                $"grain delta {Signed(c.GrainDelta)} = endowment {N(g.Endowment)} + harvest {N(g.Harvest)} - eaten {N(g.Eaten)} - spoilage {N(g.Spoilage)} - overflow {N(g.Overflow)} = {Signed(c.GrainExplained)}"),
            "  " + Reconcile(g.Reconciles, g.Discrepancy),
            "the accounts are first differences of the ledger's cumulative flow rows; when they reconcile the delta IS its named legs (an identity, not a finding)",
        ];
    }

    /// <summary>The settlements with the largest |population delta| this turn,
    /// (|delta| DESC, id ASC), at most <paramref name="top"/>.</summary>
    public static IReadOnlyList<WhereRow> LargestPopulationDelta(
        SettlementRecord[] settlements, Func<int, string> name, int top = WhereTop)
    {
        ArgumentNullException.ThrowIfNull(settlements);
        var rows = new WhereRow[settlements.Length];
        for (int i = 0; i < settlements.Length; i++)
        {
            SettlementRecord r = settlements[i];
            long delta = r.Population.Closing - r.Population.Opening;
            rows[i] = new WhereRow(r.Settlement,
                string.Create(CultureInfo.InvariantCulture,
                    $"{name(r.Settlement)}  pop {Signed(delta)}  (births {N(r.Population.Births)}, deaths {N(r.Population.Deaths)}, in {N(r.Population.Inflow)}, out {N(r.Population.Outflow)})"),
                Math.Abs((double)delta));
        }
        return SortedTop(rows, top);
    }

    /// <summary>The settlements with the largest deficit ratio this turn,
    /// (deficit DESC, id ASC), at most <paramref name="top"/>.</summary>
    public static IReadOnlyList<WhereRow> LargestDeficit(
        SettlementRecord[] settlements, Func<int, string> name, int top = WhereTop)
    {
        ArgumentNullException.ThrowIfNull(settlements);
        var rows = new WhereRow[settlements.Length];
        for (int i = 0; i < settlements.Length; i++)
        {
            SettlementRecord r = settlements[i];
            rows[i] = new WhereRow(r.Settlement,
                string.Create(CultureInfo.InvariantCulture,
                    $"{name(r.Settlement)}  deficit {r.Food.DeficitRatio:F2}  (demand {N(r.Food.DemandUnits)}, obtained {N(r.Food.FoodObtained)}, store {N(r.Food.GrainClosing)})"),
                r.Food.DeficitRatio);
        }
        return SortedTop(rows, top);
    }

    /// <summary>(Magnitude DESC, Settlement ASC): a sorts strictly before b.
    /// Two explicit halves — the MigrationExplanation.Before shape.</summary>
    public static bool Before(WhereRow a, WhereRow b)
    {
        if (a.Magnitude > b.Magnitude) return true;
        if (a.Magnitude < b.Magnitude) return false;
        return a.Settlement < b.Settlement;
    }

    /// <summary>Insertion sort on <see cref="Before"/> (no comparer over doubles,
    /// no LINQ), then the first <paramref name="top"/>.</summary>
    private static IReadOnlyList<WhereRow> SortedTop(WhereRow[] rows, int top)
    {
        for (int i = 1; i < rows.Length; i++)
        {
            WhereRow x = rows[i];
            int j = i - 1;
            while (j >= 0 && Before(x, rows[j])) { rows[j + 1] = rows[j]; j--; }
            rows[j + 1] = x;
        }
        int n = Math.Min(top, rows.Length);
        var result = new WhereRow[n];
        Array.Copy(rows, result, n);
        return result;
    }

    /// <summary>The status-band digest after an End Turn: "pop -289 · food -930 ·
    /// 2 in deficit" — the same record, three figures, read-only.</summary>
    public static string Digest(TurnRecord turn, SettlementRecord[] settlements)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(settlements);
        return string.Create(CultureInfo.InvariantCulture,
            $"pop {Signed(turn.Causes.PopulationDelta)} · food {Signed(turn.Causes.GrainDelta)} · {InDeficit(settlements)} in deficit");
    }

    /// <summary>Settlements whose ConsumptionDeficitRow ratio is positive this turn.</summary>
    public static int InDeficit(SettlementRecord[] settlements)
    {
        int n = 0;
        for (int i = 0; i < settlements.Length; i++) if (settlements[i].Food.DeficitRatio > 0.0) n++;
        return n;
    }

    /// <summary>The audit's header: which turn, which year, which dt.</summary>
    public static string HeaderLine(TurnRecord turn) =>
        string.Create(CultureInfo.InvariantCulture,
            $"turn {turn.Turn}  year {turn.Year:F0}  dt {turn.DtYears:F1} yr  ({turn.Orders.Length} order(s) applied)");

    /// <summary>ECONOMY: the world GoodAccount table — one line per non-grain
    /// good, every leg of its account and whether it closes.</summary>
    public static IReadOnlyList<string> GoodAccountLines(TurnRecord turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        var lines = new List<string>(turn.Goods.Length + 1)
        {
            "good         open  +prod  -inputs  -wear  -eaten  -housing  -constr  = close",
        };
        for (int i = 0; i < turn.Goods.Length; i++)
        {
            GoodAccount a = turn.Goods[i];
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{a.Name,-10} {a.Opening,6} {a.Produced,6} {a.InputsConsumed,8} {a.ToolWear,6} {a.Eaten,7} {a.HousingMaterials,9} {a.ConstructionMaterials,8}  = {a.Closing,6}  {(a.Reconciles ? "ok" : "DISCREPANCY " + Signed(a.Discrepancy))}"));
        }
        return lines;
    }
}
