using System.Globalization;
using Sim.Core.Observability.Explain;

namespace Sim.Ui.ViewModel;

/// <summary>One link of a chain as rendered: "label  value  (kind, source) - note",
/// or for a GAP "label: not recorded: note" — plus the lever of THIS link's own
/// node (<see cref="Levers.For"/>), so a weather link says it is a condition
/// while a share link says which slider it is.</summary>
public sealed record ChainLine(string Text, bool IsGap, LeverLine Lever);

/// <summary>The lever under a node: the sectors of the labour allocation that
/// reach it, or the honest reason none does.</summary>
public sealed record LeverLine(string Text, bool IsNone, int[] Sectors);

/// <summary>One need as a contributor row, with its chain and lever under it.</summary>
public sealed record ContributorRow(
    int NeedId, string Line, bool IsPrimary, double MarginalLift,
    IReadOnlyList<ChainLine> Chain, LeverLine Lever);

/// <summary>One class present in the settlement.</summary>
public sealed record ClassGrievanceBlock(
    int ClassId, string HeaderLine, string AccrualLine, string PrimaryLine,
    IReadOnlyList<ContributorRow> Contributors, IReadOnlyList<string> NotSimulated);

/// <summary>One happiness factor with its chain and lever.</summary>
public sealed record HappinessFactorRow(string Line, IReadOnlyList<ChainLine> Chain, LeverLine Lever);

/// <summary>The whole Grievance tab.</summary>
public sealed record GrievanceView(
    string HappinessLine, IReadOnlyList<HappinessFactorRow> Factors, string ScopeNote,
    IReadOnlyList<ClassGrievanceBlock> Classes, string AttributionNote);

/// <summary>
/// T4.19 lane B (A6-A9, B6) — THE CENTRE OF THE PACKET: summary → click →
/// decomposition → click → cause → lever, and nothing else. Pure: takes the
/// explain queries' records (docs/explain-queries.md §1-§4) and returns
/// lines; it computes no simulation quantity of its own.
///
/// ORDER OF THE CONTRIBUTORS. The primary is named first because the packet
/// asks for it first, and the rest follow in the SAME key the query ranks the
/// primary by — (MarginalLift DESC, need id ASC), two explicit halves — so the
/// list is one ranking, not "the primary, then registry order". A double-only
/// sort would put two equally-lifting needs in table order, and the row the
/// director clicks first would differ between machines; the tie-dense test
/// pins that every bound need equally unmet lists ascending by id.
///
/// EVERY LINK CARRIES ITS NOTE AND ITS OWN LEVER. A link's Note is the query's
/// citation of where the value came from and what it means (the file:line, the
/// one-turn lag, "a condition, no lever"); dropping it would leave a bare
/// number the player cannot check. A GAP link is rendered "not recorded:
/// &lt;note&gt;" and never as a number. The lever is looked up PER NODE
/// (<see cref="Levers.For"/>) and rendered beside each link, so the causes the
/// core marks as conditions — weather, arable land, deposit abundance,
/// nutritional demand, grain imports — read "condition, no lever - &lt;reason&gt;"
/// and are not confused with the levered links around them
/// (docs/observability-architecture.md §5: "the explanation names them as
/// conditions"). The chain's HEAD lever — the need's own satisfaction node, by
/// construction the set of sectors that reach the contributor — is kept as the
/// summary under the chain, where the [open POLICY] button sits. No mechanic is
/// invented to make a row actionable.
/// </summary>
public static class GrievanceViewModel
{
    public const string NoLeverPrefix = "condition, no lever - ";
    public const string AllocationPrefix = "lever: labour allocation - ";

    private static string F(double v, string fmt) => double.IsNaN(v) ? "-" : v.ToString(fmt, CultureInfo.InvariantCulture);

    public static ChainLine LinkLine(in Link link)
    {
        LeverLine lever = LeverFor(link.Node);
        if (link.Kind == LinkKind.Gap)
            return new ChainLine(string.Create(CultureInfo.InvariantCulture, $"  {link.Label}: not recorded: {link.Note}"), true, lever);
        string source = link.World == SourceWorld.None || link.SourceIndex < 0
            ? link.World.ToString().ToLowerInvariant()
            : string.Create(CultureInfo.InvariantCulture, $"{link.World.ToString().ToLowerInvariant()} {link.SourceTable}[{link.SourceIndex}]");
        string text = string.Create(CultureInfo.InvariantCulture,
            $"  {link.Label}  {F(link.Value, "G6")}  ({link.Kind.ToString().ToLowerInvariant()}, {source})");
        if (!string.IsNullOrEmpty(link.Note)) text = text + " - " + link.Note;
        return new ChainLine(text, false, lever);
    }

    public static IReadOnlyList<ChainLine> ChainLines(Link[] links)
    {
        var lines = new ChainLine[links.Length];
        for (int i = 0; i < links.Length; i++) lines[i] = LinkLine(links[i]);
        return lines;
    }

    /// <summary>The lever of one node: "lever: labour allocation - &lt;sectors&gt;
    /// (&lt;reason&gt;)" or "condition, no lever - &lt;reason&gt;". The two prefixes
    /// are disjoint so a None node can never read as levered.</summary>
    public static LeverLine LeverFor(ChainNode node)
    {
        Lever lever = Levers.For(node);
        if (lever.IsNone) return new LeverLine(NoLeverPrefix + lever.Reason, true, []);
        var text = new System.Text.StringBuilder(AllocationPrefix);
        for (int i = 0; i < lever.Sectors.Length; i++)
        {
            if (i > 0) text.Append(", ");
            text.Append(SectorBarModel.SectorNames[lever.Sectors[i]]);
        }
        text.Append(" (").Append(lever.Reason).Append(')');
        return new LeverLine(text.ToString(), false, lever.Sectors);
    }

    /// <summary>The chain's head-node lever, or None with a reason for an empty chain.</summary>
    private static LeverLine HeadLever(Link[] links) =>
        links.Length == 0 ? new LeverLine("no lever - the chain is empty", true, []) : LeverFor(links[0].Node);

    public static GrievanceView Build(
        HappinessExplanation happiness,
        IReadOnlyList<GrievanceExplanation> classes,
        Func<int, int, CausalChain?> chainFor)
    {
        ArgumentNullException.ThrowIfNull(happiness);
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(chainFor);

        var factors = new HappinessFactorRow[happiness.Factors.Length];
        for (int i = 0; i < factors.Length; i++)
        {
            HappinessFactor f = happiness.Factors[i];
            factors[i] = new HappinessFactorRow(
                string.Create(CultureInfo.InvariantCulture, $"{f.Name} {f.Value:F3}"),
                ChainLines(f.Chain), HeadLever(f.Chain));
        }

        var blocks = new ClassGrievanceBlock[classes.Count];
        for (int c = 0; c < classes.Count; c++) blocks[c] = ClassBlock(classes[c], chainFor);

        return new GrievanceView(
            string.Create(CultureInfo.InvariantCulture, $"happiness {happiness.Happiness:F1} (0..100)"),
            factors, HappinessExplanation.ScopeNote, blocks, GrievanceExplanation.AttributionNote);
    }

    public static ClassGrievanceBlock ClassBlock(GrievanceExplanation g, Func<int, int, CausalChain?> chainFor)
    {
        ArgumentNullException.ThrowIfNull(g);
        string header = string.Create(CultureInfo.InvariantCulture,
            $"{g.ClassName}: grievance {g.Total:F2}  ({g.Delta:+0.00;-0.00;0.00} this turn; {g.ClassPopulation} people)");
        string accrual = string.Create(CultureInfo.InvariantCulture,
            $"  accrual +{g.Accrual:F2} (W {g.WeightSum:F2} x (1 - S {g.Aggregate:F3}) x dt {g.DtYears:F0})  decay -{g.Decay:F2} (rate {g.DecayRatePerYear:F4}/yr)  {(g.Recomputed == g.Total ? "reproduces exactly" : "DOES NOT REPRODUCE - observer drift")}");

        var notSimulated = new List<string>();
        for (int i = 0; i < g.Needs.Length; i++)
        {
            NeedComponent n = g.Needs[i];
            if (!n.Bound)
                notSimulated.Add(string.Create(CultureInfo.InvariantCulture, $"  {n.Name}: {n.Note}"));
        }
        IReadOnlyList<NeedComponent> ranked = Rank(g.Needs);

        var rows = new ContributorRow[ranked.Count];
        for (int i = 0; i < ranked.Count; i++)
        {
            NeedComponent n = ranked[i];
            bool primary = n.NeedId == g.PrimaryNeedId;
            CausalChain? chain = chainFor(g.Class.Value, n.NeedId);
            Link[] links = chain?.Links ?? [];
            rows[i] = new ContributorRow(n.NeedId,
                string.Create(CultureInfo.InvariantCulture,
                    $"{(primary ? "PRIMARY " : "")}{n.Name}  satisfaction {n.Satisfaction:F3}  weighted shortfall {n.WeightedShortfall:F3}  marginal lift {n.MarginalLift:F4}{(n.IsTierAGate ? "  [tier-A gate]" : "")}"),
                primary, n.MarginalLift, ChainLines(links), HeadLever(links));
        }

        string primaryLine = g.PrimaryNeedId < 0
            ? "primary grievance: none (no bound need published a satisfaction row)"
            : string.Create(CultureInfo.InvariantCulture,
                $"primary grievance: {NameOf(g, g.PrimaryNeedId)}  (marginal lift {LiftOf(g, g.PrimaryNeedId):F4} - the aggregate's rise if this need alone were fully met)");

        return new ClassGrievanceBlock(g.Class.Value, header, accrual, primaryLine, rows, notSimulated);
    }

    /// <summary>The bound needs that published a row, ranked (MarginalLift
    /// DESC, NeedId ASC) by an insertion sort on <see cref="Before"/> — no
    /// comparer over doubles alone, no LINQ. Unbound / unpublished needs
    /// (Bound false) are excluded; the caller lists them separately.</summary>
    public static IReadOnlyList<NeedComponent> Rank(NeedComponent[] needs)
    {
        ArgumentNullException.ThrowIfNull(needs);
        var ranked = new List<NeedComponent>(needs.Length);
        for (int i = 0; i < needs.Length; i++)
        {
            NeedComponent n = needs[i];
            if (!n.Bound) continue;
            int j = ranked.Count - 1;
            ranked.Add(n);
            while (j >= 0 && Before(n, ranked[j])) { ranked[j + 1] = ranked[j]; j--; }
            ranked[j + 1] = n;
        }
        return ranked;
    }

    /// <summary>(MarginalLift DESC, NeedId ASC): a sorts strictly before b.</summary>
    public static bool Before(in NeedComponent a, in NeedComponent b)
    {
        if (a.MarginalLift > b.MarginalLift) return true;
        if (a.MarginalLift < b.MarginalLift) return false;
        return a.NeedId < b.NeedId;
    }

    private static string NameOf(GrievanceExplanation g, int needId)
    {
        for (int i = 0; i < g.Needs.Length; i++) if (g.Needs[i].NeedId == needId) return g.Needs[i].Name;
        return "need " + needId.ToString(CultureInfo.InvariantCulture);
    }

    private static double LiftOf(GrievanceExplanation g, int needId)
    {
        for (int i = 0; i < g.Needs.Length; i++) if (g.Needs[i].NeedId == needId) return g.Needs[i].MarginalLift;
        return double.NaN;
    }
}
