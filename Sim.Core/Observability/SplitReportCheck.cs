using static System.FormattableString;

namespace Sim.Core.Observability;

/// <summary>The verdict of <see cref="SplitReportCheck.Check"/>: every property that FAILED, as "Pn: reason" lines in
/// property order (empty: the report passed), and what the report showed for each existence property (the steps that
/// witness it).</summary>
public sealed record SplitCheckResult(string[] Failures, string[] Findings)
{
    public bool Passed => Failures.Length == 0;

    /// <summary>Whether property <paramref name="property"/> (e.g. "P3") failed.</summary>
    public bool Failed(string property)
    {
        for (int i = 0; i < Failures.Length; i++)
            if (Failures[i].StartsWith(property + ":", StringComparison.Ordinal)) return true;
        return false;
    }
}

/// <summary>The KIND-12 TWIN of a report (`sim split-check REPORT --twin TWIN --order-at T:B`; property PT): the report of
/// the same run WITHOUT the kind-12 order stamped <see cref="OrderTurn"/> that set building share
/// <see cref="BuildingsPercent"/> — the ci.yml twin run from the same save with that one order dropped.</summary>
public sealed record SplitTwin(IReadOnlyList<SplitReportRow> Rows, long OrderTurn, int BuildingsPercent);

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — THE JUDGEMENT OF A SPLIT REPORT (`sim split-check`), in tested C# — the CorridorStatus
/// precedent: the cross-process allocation leg's assertions live here, where Sim.Tests can feed doctored reports to them,
/// not in an untested script. The recon's P0–P9 (batch-4 recon, allocation-ci §5.3), RESTATED WITH THE D-055 SET-ASIDES
/// ("holds"), plus the hold property:
/// <list type="bullet">
/// <item>P0 (non-vacuity): some step ran under a non-default split, 0 &lt; b &lt; 100, with a delivered road order.</item>
/// <item>P1 (the step did what the rule forecast, every row and good): building work drew what the split forecast, set aside
///   what it forecast, road works were charged what it forecast (only in a step where one Empire alone has a road order:
///   the forecast plans on PREV's routes, an approximation when two Empires develop roads in one step); and the heads'
///   draws and set-asides sum to the Empire's measured draw and its published set-aside.</item>
/// <item>P2 (grant order, the grant spent, no overdraw): E_b + E_r = S, build + holds = G_b ≤ D_b, G_b ≤ cap, and build +
///   holds + road ≤ S. build + holds = G_b holds on EVERY row and good, not only where the split binds: under the D-055 hold
///   rule each unreleased head of the walk draws d and then sets aside min(its remaining need, the budget less d) — together
///   min(its need at the step's start, the budget left) — and a released head neither, so the walk spends min(R, budget), R
///   the unreleased heads' need; D_b is that walk on S, so G_b ≤ D_b ≤ R, and the step's walk on min(S, G_b) = G_b spends
///   exactly G_b. Where G_b &lt; D_b this is the design's "scarcity means build + holds = G_b &lt; D_b" (fixer round,
///   finding T3-L2-T3-V2-F2); where G_b = D_b it also catches an under-spend scarcity cannot see (set-asides counted twice).
///   A run with the hold rule OFF would fail it legitimately (no set-asides: a head held back by another good draws less of
///   this one), so <see cref="SplitProbe"/> refuses that configuration and every report is a D-055 report. D-058
///   (2026-10-19, the Director's "build one at a time" ruling): a completing head's leftover capacity carries its queue's
///   next entry into the same walk, so the set of heads walked depends on whether a head completes — on every good — and
///   the walk under the grant can reach fewer entries than the claim's walk on S. The clause is therefore checked as build
///   + holds = min(G_b, R), R the row's own unreleased heads' needs (the telescoped walk), which is G_b whenever R ≥ G_b —
///   always under D-055 alone.</item>
/// <item>P3 (the availability guarantee): with a road order and the guard not fired, S − build − holds ≥ min(D_r, E_r).</item>
/// <item>P4 (scarcity binds): some split step's good has build + holds = G_b &lt; D_b (that it is spent at every such step
///   is P2's).</item>
/// <item>P5 (road share → building): some split step's good has D_b &gt; E_b, D_r &lt; E_r and build + holds &gt; E_b.</item>
/// <item>P6 (building share → roads): some split step's good has D_b &lt; E_b and road &gt; E_r.</item>
/// <item>P7 (competing heads, in the building order): in a binding step, a head earlier in the building order drew OR set
///   aside some of the binding good while a later head of ANOTHER settlement that still needed it waited (its progress
///   unchanged).</item>
/// <item>P8 (`--control`): at the report's first binding step (turn, polity, good — ascending), the control's building work
///   drew and set aside more of the good than the report's did.</item>
/// <item>P9 (`--control`, A against its control): the control never ran a non-default split (b = 100 on every row), and it
///   branches from the report's world — its first step for the report's first polity is at the same turn with the same
///   purse S of every good — and every control row is itself the rule's (P1, P2, P3, P11 and P12 hold on it; T3 round 2).
///   The byte-level half of P9 (hash logs and reports) is ci.yml's cmp/diff.</item>
/// <item>P10 (a hold binds): some head is HeldForEarlier on a good, limited by materials, its named holder is a head AHEAD of
///   it in the same row that sets that good aside, and its draw fell because of the hold: it drew less than
///   min(its need, the walk's starting budget less what the heads ahead of it DREW).</item>
/// <item>P11 (THE SPLIT'S ARITHMETIC, RE-DERIVED — every row and good; T3 round 2, finding F2): from the row's own S, b,
///   D_b and D_r, in this file's own integer arithmetic (never the rule's functions, so a mutated rule cannot vouch for
///   itself): S ≥ 0, b in [0, 100], D_b ≥ 0, D_r ≥ 0, E_b = floor(S × b / 100), E_r = S − E_b,
///   cap = E_b + max(0, E_r − D_r) and G_b = min(D_b, cap) — with D_r taken as 0 in a row that records the starvation guard
///   (ResourceAllocation's header: the split is then recomputed with D_r = 0). A share rounded the wrong way, a cap or a grant
///   computed otherwise, fails with the row named.</item>
/// <item>P12 (THE WALK, RE-DERIVED — every row; T3 round 2, finding F3): the row's heads (its Empire purse's) are listed in
///   THE BUILDING ORDER, strictly ascending (EnqueuedTurn, SettlementId, Slot); and per good, walking them in that order,
///   the walk started at G_b (S at b = 100, where the split never binds), each head was resolved against exactly what the
///   heads ahead of it left (its budget), and each UNRELEASED head drew and set aside together min(its need, that budget)
///   — the D-055 rule per head: it draws d and sets aside min(need − d, budget − d) — while a released head took nothing.
///   So draw + hold ≤ the budget left, the heads ahead are served first, and the per-head figures sum to P1's totals by the
///   walk's own arithmetic: a walk in another order, or one that spends a budget it was not given, fails with the row and
///   the head named. The probe lists the heads in the walk's order, and P12's budget chain ties that listing to the order
///   the walk actually spent in. And (T3 round 3, finding T3-L1-T3-V1L1-N1) THE DRAW/SET-ASIDE DIVISION: a head reported
///   limited by materials (Materials, or Unobtainable) was short of some good — its budget for that good was below its need.
///   The resolver binds a head on materials only where some input's units — at most its budget, which by the walk's
///   induction never exceeds what the purse physically holds — fall short of what that input still needs, and its need
///   counts every input of the good; so a head whose budget covered its whole need of every good is not materials-limited.
///   The walk's equality alone cannot see a walk that hands every head its budget but lets it only set aside (it never
///   draws, never progresses, and its spending totals are unchanged); this clause can.</item>
/// <item>PK (`--expect-hold-at K`): the step that produced world K (the row whose "to" is K) published a set-aside and has a
///   head that satisfies P10's condition — world K holds a set-aside that binds a younger head.</item>
/// <item>PT (`--twin TWIN --order-at T:B`; T3 round 2, finding F1): the kind-12 order stamped T, delivered in the step from T,
///   CHANGED SPENDING IN THAT VERY STEP. The report's step T→T+1 ran at b = B; the twin's (the same run without that order)
///   did not; the twin's rows before T are the report's byte for byte; at T both saw the same purse S, claim D_b and road
///   demand D_r of every good (only the split differs); the twin's own step T satisfies P1, P2, P3, P11 and P12 (its draw is
///   the rule's for ITS b); and building work drew a different amount of some good in the two runs at T. A split applied to
///   spending a step late (the report's step T drawing what the twin's draws) fails here — and P1 there.</item>
/// </list>
/// Pure: rows walked in file order, integer arithmetic, no dictionary. Each failed property is reported once, with its first
/// counterexample (and, for P1–P3, P11 and P12, how many there were).
/// </summary>
public static class SplitReportCheck
{
    public const string HeldForEarlier = "HeldForEarlier";

    /// <summary>The release name of an unreleased head (HoldRelease.None).</summary>
    public const string NotReleased = "None";

    /// <summary>The limit names of a materials-limited head (HeadLimitKind.Materials, HeadLimitKind.Unobtainable).</summary>
    public const string HeadLimitMaterials = "Materials";
    public const string HeadLimitUnobtainable = "Unobtainable";

    // The universal (every-row) properties, in reporting order: P1, P2, P3, P11, P12.
    private static readonly int[] UniversalIds = [1, 2, 3, 11, 12];

    public static SplitCheckResult Check(
        IReadOnlyList<SplitReportRow> report, IReadOnlyList<SplitReportRow>? control = null, long? expectHoldAt = null,
        SplitTwin? twin = null)
    {
        var failures = new List<string>();
        var findings = new List<string>();
        var first = new string?[13];
        var count = new int[13];
        bool p0 = false;
        var binding = new List<(long Turn, int Polity, int Good, string Name)>();
        var roadToBuilding = new List<string>();
        var buildingToRoad = new List<string>();
        var competing = new List<string>();
        var held = new List<string>();
        var violations = new List<(int Property, string Reason)>();

        for (int i = 0; i < report.Count; i++)
        {
            SplitReportRow r = report[i];
            bool split = r.BuildingsPercent > 0 && r.BuildingsPercent < 100 && r.RoadPercent is not null;
            p0 |= split;
            violations.Clear();
            Universal(r, RoadOrdersAt(report, r.Turn) <= 1, violations);
            for (int v = 0; v < violations.Count; v++)
            {
                count[violations[v].Property]++;
                first[violations[v].Property] ??= violations[v].Reason;
            }
            for (int j = 0; j < r.Goods.Length; j++)
            {
                if (!split) continue;
                SplitGoodFigures g = r.Goods[j];
                long claimed = checked(g.Build + g.Holds);
                if (claimed == g.Gb && g.Gb < g.Db) binding.Add((r.Turn, r.Polity, g.Id, g.Good));
                if (g.Db > g.Eb && g.Dr < g.Er && claimed > g.Eb) roadToBuilding.Add($"{r.Turn} {g.Good}");
                if (g.Db < g.Eb && g.Road > g.Er) buildingToRoad.Add($"{r.Turn} {g.Good}");
            }

            if (split && Competing(r, binding) is { } who) competing.Add(who);
            for (int h = 0; h < r.Heads.Length; h++)
                for (int j = 0; j < r.Heads[h].Goods.Length; j++)
                    if (HeldBecauseOfAHold(r, h, r.Heads[h].Goods[j]))
                        held.Add($"{r.Turn} s{r.Heads[h].Settlement}:{r.Heads[h].Slot} {r.Heads[h].Goods[j].Good} " +
                                 $"(holder s{r.Heads[h].Goods[j].Holder!.Settlement}:{r.Heads[h].Goods[j].Holder!.Slot})");
        }

        if (!p0) failures.Add("P0: no step ran under a non-default split 0 < b < 100 with a delivered road order (the report is vacuous)");
        for (int u = 0; u < 3; u++)
            if (first[UniversalIds[u]] is { } why) failures.Add($"P{UniversalIds[u]}: {why}{More(count[UniversalIds[u]])}");
        if (binding.Count == 0) failures.Add("P4: the split never held building work back (no step with build + holds = G_b < D_b)");
        if (roadToBuilding.Count == 0) failures.Add("P5: no step where building work's claim above its share used road works' unused share");
        if (buildingToRoad.Count == 0) failures.Add("P6: no step where road works used building work's unused share");
        if (competing.Count == 0)
            failures.Add("P7: no binding step where a head earlier in the building order drew or set aside the binding good while " +
                         "another settlement's head waited");
        if (held.Count == 0)
            failures.Add("P10: no head was held for an earlier head: none is HeldForEarlier with its holder ahead of it setting the " +
                         "good aside and its draw below min(its need, the budget less the draws ahead of it)");
        for (int u = 3; u < UniversalIds.Length; u++)
            if (first[UniversalIds[u]] is { } why) failures.Add($"P{UniversalIds[u]}: {why}{More(count[UniversalIds[u]])}");
        if (control is not null) CheckControl(report, control, binding, failures);
        if (expectHoldAt is { } k) CheckHoldAt(report, k, failures);
        string? twinFinding = twin is not null ? CheckTwin(report, twin, failures) : null;

        findings.Add("binding steps (P4): " + List(binding.ConvertAll(b => $"{b.Turn} {b.Name}")));
        findings.Add("road share to building (P5): " + List(roadToBuilding));
        findings.Add("building share to roads (P6): " + List(buildingToRoad));
        findings.Add("competing heads (P7): " + List(competing));
        findings.Add("held for an earlier head (P10): " + List(held));
        if (twinFinding is not null) findings.Add(twinFinding);
        return new SplitCheckResult([.. failures], [.. findings]);
    }

    /// <summary>
    /// THE UNIVERSAL PROPERTIES OF ONE ROW — P1, P2, P3 and P11 per good, P12 per row (its building order) and per good (its
    /// walk) — appended to <paramref name="into"/> as (property number, reason); at most one P1, P2, P3, P11 and P12 reason per
    /// good (the first broken clause), one P12 reason for the row's order and one for its first head reported limited by
    /// materials that its budget did not leave short (round 3). <paramref name="oneRoadOrder"/>: at most one
    /// Empire had a road order in the row's step (P1 then holds road works to the forecast).
    /// </summary>
    private static void Universal(SplitReportRow r, bool oneRoadOrder, List<(int Property, string Reason)> into)
    {
        for (int j = 0; j < r.Goods.Length; j++)
        {
            SplitGoodFigures g = r.Goods[j];
            string at = $"step {r.Turn}->{r.To} (polity {r.Polity}) {g.Good}";
            long drawn = 0, setAside = 0;
            for (int h = 0; h < r.Heads.Length; h++)
                if (r.Heads[h].GoodOf(g.Id) is { } hg) { drawn = checked(drawn + hg.Draw); setAside = checked(setAside + hg.Hold); }

            string? v1 = g.Build != g.ForecastBuild ? $"{at}: building work drew {g.Build} but the split forecast {g.ForecastBuild}"
                : g.Holds != g.ForecastHolds ? $"{at}: {g.Holds} were set aside but the split forecast {g.ForecastHolds}"
                : oneRoadOrder && g.Road != g.ForecastRoad ? $"{at}: road works drew {g.Road} but the split forecast {g.ForecastRoad}"
                : drawn != g.Build ? $"{at}: the heads drew {drawn} in all but the purse fell by {g.Build} in construction"
                : setAside != g.Holds ? $"{at}: the heads set aside {setAside} in all but construction published {g.Holds}"
                : null;
            if (v1 is not null) into.Add((1, v1));

            long claimed = checked(g.Build + g.Holds);
            // D-058 (2026-10-19): the walk spends min(G_b, R), R the row's unreleased heads' needs of the good — Σ min(need,
            // left) telescopes to min(Σ need, start). Under D-055 alone R ≥ D_b ≥ G_b, so this is "the whole grant"; with a
            // completion's leftover capacity carried to the next entry, a walk under the grant can reach fewer entries than
            // the claim's walk on S did (another good stopped a head short of completion), and then it spends exactly R.
            long need = 0;
            for (int h = 0; h < r.Heads.Length; h++)
                if (r.Heads[h].Release == NotReleased && r.Heads[h].GoodOf(g.Id) is { } hn) need = checked(need + hn.Need);
            long spends = Math.Min(g.Gb, need);
            string? v2 = checked(g.Eb + g.Er) != g.S ? $"{at}: the shares E_b = {g.Eb} and E_r = {g.Er} do not split the purse S = {g.S}"
                : claimed > g.Gb ? $"{at}: building work drew {g.Build} and set aside {g.Holds}, more than its grant G_b = {g.Gb}"
                : claimed < spends ? $"{at}: building work drew {g.Build} and set aside {g.Holds}, less than min(its grant G_b = {g.Gb}, " +
                                   $"its heads' need {need}) = {spends} (D_b = {g.Db}): the walk spends the whole grant its heads need"
                : g.Gb > g.Db ? $"{at}: the grant G_b = {g.Gb} exceeds the claim D_b = {g.Db}"
                : g.Gb > g.Cap ? $"{at}: the grant G_b = {g.Gb} exceeds the cap {g.Cap}"
                : checked(claimed + g.Road) > g.S ? $"{at}: build {g.Build} + holds {g.Holds} + road {g.Road} exceed the purse S = {g.S}"
                : null;
            if (v2 is not null) into.Add((2, v2));

            if (r.RoadPercent is not null && !r.Guarded && checked(g.S - claimed) < Math.Min(g.Dr, g.Er))
                into.Add((3, $"{at}: building work left road works S - build - holds = {g.S - claimed} < min(D_r, E_r) = {Math.Min(g.Dr, g.Er)}"));

            if (Arithmetic(r, g, at) is { } v11) into.Add((11, v11));
            if (Walk(r, g, at) is { } v12) into.Add((12, v12));
        }
        if (BuildingOrder(r) is { } order) into.Add((12, order));
        if (MaterialsLimit(r) is { } limit) into.Add((12, limit));
    }

    /// <summary>
    /// P12's division clause for row <paramref name="r"/> (T3 round 3, finding T3-L1-T3-V1L1-N1): every head reported limited
    /// by materials (<see cref="HeadLimitMaterials"/> or <see cref="HeadLimitUnobtainable"/>) was short of some good — its
    /// budget for it below its need (a null budget, unbounded, is never short). Null when every such head was.
    /// </summary>
    private static string? MaterialsLimit(SplitReportRow r)
    {
        for (int h = 0; h < r.Heads.Length; h++)
        {
            SplitHeadFigures head = r.Heads[h];
            if (head.Limit != HeadLimitMaterials && head.Limit != HeadLimitUnobtainable) continue;
            bool shortOfSome = false;
            var covered = new System.Text.StringBuilder();
            for (int j = 0; j < head.Goods.Length; j++)
            {
                SplitHeadGood g = head.Goods[j];
                if (g.Budget is { } b && b < g.Need) { shortOfSome = true; break; }
                covered.Append(j == 0 ? "" : ", ").Append(g.Budget is { } x
                    ? Invariant($"{g.Good} budget {x} >= need {g.Need}") : Invariant($"{g.Good} unbounded, need {g.Need}"));
            }
            if (shortOfSome) continue;
            return Invariant($"step {r.Turn}->{r.To} (polity {r.Polity}): head s{head.Settlement}:{head.Slot} (enqueued {head.Enqueued}) ") +
                   Invariant($"is reported limited by {head.Limit}, but its budget covered its whole need of every good ({covered}) ") +
                   Invariant($"and it drew {DrawnBy(head)} and set aside {HeldBy(head)}: the resolver binds a head on materials only ") +
                   "where some input's budget falls short of what it still needs";
        }
        return null;
    }

    private static long DrawnBy(SplitHeadFigures head)
    {
        long d = 0;
        for (int j = 0; j < head.Goods.Length; j++) d = checked(d + head.Goods[j].Draw);
        return d;
    }

    private static long HeldBy(SplitHeadFigures head)
    {
        long d = 0;
        for (int j = 0; j < head.Goods.Length; j++) d = checked(d + head.Goods[j].Hold);
        return d;
    }

    /// <summary>
    /// P11 for good <paramref name="g"/> of row <paramref name="r"/>: the split's arithmetic re-derived from S, b, D_b and D_r
    /// (D_r as 0 where the guard fired) — E_b = floor(S × b / 100) in 128-bit integers, E_r = S − E_b, cap = E_b +
    /// max(0, E_r − D_r), G_b = min(D_b, cap). Null when the row's figures are those.
    /// </summary>
    private static string? Arithmetic(SplitReportRow r, SplitGoodFigures g, string at)
    {
        int b = r.BuildingsPercent;
        if (b is < 0 or > 100) return Invariant($"{at}: the building share b = {b} is not a whole percentage in [0, 100]");
        if (g.S < 0 || g.Db < 0 || g.Dr < 0)
            return Invariant($"{at}: the purse S = {g.S}, the claim D_b = {g.Db} and the road demand D_r = {g.Dr} must not be negative");
        long eb = (long)((Int128)g.S * b / 100);
        long er = g.S - eb;
        long dr = r.Guarded ? 0 : g.Dr;
        long cap = checked(eb + Math.Max(0, er - dr));
        long gb = Math.Min(g.Db, cap);
        if (g.Eb != eb) return Invariant($"{at}: E_b = {g.Eb}, but floor(S x b / 100) = floor({g.S} x {b} / 100) = {eb}");
        if (g.Er != er) return Invariant($"{at}: E_r = {g.Er}, but S - E_b = {g.S} - {eb} = {er}");
        if (g.Cap != cap)
            return Invariant($"{at}: the cap is {g.Cap}, but E_b + max(0, E_r - D_r) = {eb} + max(0, {er} - {dr}) = {cap}") +
                   (r.Guarded ? " (D_r taken as 0: the starvation guard fired)" : "");
        if (g.Gb != gb) return Invariant($"{at}: G_b = {g.Gb}, but min(D_b, cap) = min({g.Db}, {cap}) = {gb}");
        return null;
    }

    /// <summary>P12's order clause for row <paramref name="r"/>: its heads strictly ascending in (EnqueuedTurn, SettlementId,
    /// Slot) — THE BUILDING ORDER. Null when they are.</summary>
    private static string? BuildingOrder(SplitReportRow r)
    {
        for (int h = 1; h < r.Heads.Length; h++)
        {
            SplitHeadFigures a = r.Heads[h - 1], b = r.Heads[h];
            if (CompareBuildingOrder(a, b) < 0) continue;
            return Invariant($"step {r.Turn}->{r.To} (polity {r.Polity}): head s{a.Settlement}:{a.Slot} (enqueued {a.Enqueued}) is walked ") +
                   Invariant($"before s{b.Settlement}:{b.Slot} (enqueued {b.Enqueued}), out of THE BUILDING ORDER ") +
                   "(EnqueuedTurn, SettlementId, Slot strictly ascending)";
        }
        return null;
    }

    /// <summary>THE BUILDING ORDER's composite key (ConstructionQuery.HeadsInBuildingOrder): EnqueuedTurn, then SettlementId,
    /// then Slot, all integers — a total order on a row's heads (one head per settlement).</summary>
    public static int CompareBuildingOrder(SplitHeadFigures a, SplitHeadFigures b)
    {
        int c = a.Enqueued.CompareTo(b.Enqueued);
        if (c != 0) return c;
        c = a.Settlement.CompareTo(b.Settlement);
        return c != 0 ? c : a.Slot.CompareTo(b.Slot);
    }

    /// <summary>
    /// P12's walk clauses for good <paramref name="g"/> of row <paramref name="r"/> (rows with heads only): the walk started
    /// at G_b (S at b = 100); each head carrying the good was resolved against what the heads ahead of it left; an
    /// unreleased head drew and set aside together min(its need, that budget), a released head nothing. Null when the walk
    /// is that.
    /// </summary>
    private static string? Walk(SplitReportRow r, SplitGoodFigures g, string at)
    {
        if (r.Heads.Length == 0) return null;
        bool full = r.BuildingsPercent == 100;
        long want = full ? g.S : g.Gb;
        if (g.Start is not { } start)
            return $"{at}: the purse has {r.Heads.Length} head(s) but the walk recorded no starting budget";
        if (start != want)
            return Invariant($"{at}: the walk started at {start}, not at {(full ? "S" : "G_b")} = {want}") +
                   (full ? " (b = 100: the split never binds)" : " (the split's grant)");
        long left = start;
        for (int h = 0; h < r.Heads.Length; h++)
        {
            SplitHeadFigures head = r.Heads[h];
            if (head.GoodOf(g.Id) is not { } hg) continue;
            string who = Invariant($"head s{head.Settlement}:{head.Slot} (enqueued {head.Enqueued})");
            if (hg.Budget is not { } budget || budget != left)
                return Invariant($"{at}: {who} was resolved against {(hg.Budget is { } x ? x.ToString(System.Globalization.CultureInfo.InvariantCulture) : "no budget")}, ") +
                       Invariant($"but the heads ahead of it in the building order left {left} of the walk's {start}");
            if (hg.Draw < 0 || hg.Hold < 0 || hg.Need < 0)
                return Invariant($"{at}: {who} drew {hg.Draw}, set aside {hg.Hold} and needed {hg.Need}: none may be negative");
            long spent = checked(hg.Draw + hg.Hold);
            if (head.Release != NotReleased)
            {
                if (spent != 0)
                    return Invariant($"{at}: {who} is released ({head.Release}) but drew {hg.Draw} and set aside {hg.Hold}: a released head takes nothing");
            }
            else if (spent != Math.Min(hg.Need, left))
            {
                return Invariant($"{at}: {who} drew {hg.Draw} and set aside {hg.Hold}, but the D-055 walk gives an unreleased head ") +
                       Invariant($"min(its need, what the heads ahead left) = min({hg.Need}, {left}) = {Math.Min(hg.Need, left)}");
            }
            left -= spent;
        }
        return null;
    }

    /// <summary>P10's condition for good <paramref name="g"/> of head <paramref name="h"/> of row <paramref name="r"/>.</summary>
    public static bool HeldBecauseOfAHold(SplitReportRow r, int h, SplitHeadGood g)
    {
        if (g.Cause != HeldForEarlier || g.Holder is not { } holder) return false;
        SplitHeadFigures head = r.Heads[h];
        if (head.Limit != "Materials") return false;
        int e = -1;
        for (int i = 0; i < h; i++)
            if (r.Heads[i].Settlement == holder.Settlement && r.Heads[i].Slot == holder.Slot && r.Heads[i].Project == holder.Project)
            { e = i; break; }
        if (e < 0 || r.Heads[e].GoodOf(g.Id) is not { Hold: > 0 }) return false;
        if (r.GoodOf(g.Id)?.Start is not { } start) return false;
        long drawnAhead = 0;
        for (int i = 0; i < h; i++) drawnAhead = checked(drawnAhead + (r.Heads[i].GoodOf(g.Id)?.Draw ?? 0));
        return g.Draw < Math.Min(g.Need, checked(start - drawnAhead));
    }

    /// <summary>P7 in one row: a binding good of the row, a head that drew or set some of it aside, and a LATER head of
    /// another settlement that needed it and did not move. Null when none.</summary>
    private static string? Competing(SplitReportRow r, List<(long Turn, int Polity, int Good, string Name)> binding)
    {
        for (int b = binding.Count - 1; b >= 0 && binding[b].Turn == r.Turn && binding[b].Polity == r.Polity; b--)
        {
            int good = binding[b].Good;
            for (int a = 0; a < r.Heads.Length; a++)
            {
                if (r.Heads[a].GoodOf(good) is not { } ag || checked(ag.Draw + ag.Hold) <= 0) continue;
                for (int w = a + 1; w < r.Heads.Length; w++)
                {
                    SplitHeadFigures waiter = r.Heads[w];
                    if (waiter.Settlement == r.Heads[a].Settlement || waiter.After != waiter.Before) continue;
                    if (waiter.GoodOf(good) is { Need: > 0 })
                        return $"{r.Turn} {binding[b].Name}: s{r.Heads[a].Settlement}:{r.Heads[a].Slot} served, s{waiter.Settlement}:{waiter.Slot} waited";
                }
            }
        }
        return null;
    }

    private static void CheckControl(
        IReadOnlyList<SplitReportRow> report, IReadOnlyList<SplitReportRow> control,
        List<(long Turn, int Polity, int Good, string Name)> binding, List<string> failures)
    {
        // P8: the contrast at the first binding step.
        if (binding.Count == 0) failures.Add("P8: the report has no binding step to contrast with the control (P4)");
        else
        {
            (long t, int polity, int good, string name) = binding[0];
            SplitGoodFigures a = Row(report, t, polity)!.GoodOf(good)!;
            SplitGoodFigures? c = Row(control, t, polity)?.GoodOf(good);
            if (c is null)
                failures.Add($"P8: the control has no {name} figures for step {t}->{t + 1} (polity {polity}), the report's first binding step");
            else if (checked(c.Build + c.Holds) <= checked(a.Build + a.Holds))
                failures.Add($"P8: at the first binding step {t}->{t + 1} ({name}) the control's building work drew {c.Build} and set " +
                             $"aside {c.Holds}, no more than the split run's {a.Build} and {a.Holds}");
        }

        // P9: the control is the default-split twin, branched from the same world.
        for (int i = 0; i < control.Count; i++)
            if (control[i].BuildingsPercent != 100)
            {
                failures.Add($"P9: the control ran a non-default split at step {control[i].Turn}->{control[i].To} " +
                             $"(polity {control[i].Polity}, b = {control[i].BuildingsPercent}): it is not the default-split twin");
                return;
            }
        if (report.Count == 0 || control.Count == 0)
        {
            failures.Add("P9: the report or the control has no rows to compare");
            return;
        }
        SplitReportRow first = report[0];
        SplitReportRow? twin = null;
        for (int i = 0; i < control.Count && twin is null; i++) if (control[i].Polity == first.Polity) twin = control[i];
        if (twin is null || twin.Turn != first.Turn)
        {
            failures.Add($"P9: the control does not start at the report's first step {first.Turn}->{first.To} (polity {first.Polity})");
            return;
        }
        for (int j = 0; j < first.Goods.Length; j++)
        {
            SplitGoodFigures g = first.Goods[j];
            if (twin.GoodOf(g.Id) is not { } cg || cg.S != g.S)
            {
                failures.Add($"P9: the control does not branch from the report's world: at step {first.Turn}->{first.To} its purse " +
                             $"held {(twin.GoodOf(g.Id) is { } x ? x.S.ToString(System.Globalization.CultureInfo.InvariantCulture) : "no")} " +
                             $"{g.Good}, the report's {g.S}");
                return;
            }
        }
        // The control's own steps are the rule's (T3 round 2): its spending, split and walk re-derived as the report's are.
        var violations = new List<(int Property, string Reason)>();
        for (int i = 0; i < control.Count; i++)
        {
            Universal(control[i], RoadOrdersAt(control, control[i].Turn) <= 1, violations);
            if (violations.Count == 0) continue;
            failures.Add(Invariant($"P9: the control's step {control[i].Turn}->{control[i].To} (polity {control[i].Polity}) breaks ") +
                         Invariant($"P{violations[0].Property}: {violations[0].Reason}"));
            return;
        }
    }

    private static void CheckHoldAt(IReadOnlyList<SplitReportRow> report, long k, List<string> failures)
    {
        bool any = false;
        for (int i = 0; i < report.Count; i++)
        {
            SplitReportRow r = report[i];
            if (r.To != k) continue;
            any = true;
            long set = 0;
            for (int j = 0; j < r.Goods.Length; j++) set = checked(set + r.Goods[j].Holds);
            if (set <= 0) continue;
            for (int h = 0; h < r.Heads.Length; h++)
                for (int j = 0; j < r.Heads[h].Goods.Length; j++)
                    if (HeldBecauseOfAHold(r, h, r.Heads[h].Goods[j])) return;
        }
        failures.Add(any
            ? $"PK: world {k} holds no set-aside that binds a younger head (no row ending at turn {k} publishes a set-aside with a head held for it)"
            : $"PK: the report has no step that produced world {k}");
    }

    /// <summary>
    /// PT (`--twin TWIN --order-at T:B`): the kind-12 order stamped T changed building work's draw in its delivery step (the
    /// class header). Appends one "PT: reason" failure when it did not; returns the finding line when it did (null on a
    /// failure).
    /// </summary>
    private static string? CheckTwin(IReadOnlyList<SplitReportRow> report, SplitTwin twin, List<string> failures)
    {
        long t = twin.OrderTurn;
        int b = twin.BuildingsPercent;
        SplitReportRow? r = null;
        for (int i = 0; i < report.Count && r is null; i++)
            if (report[i].Turn == t && report[i].BuildingsPercent == b) r = report[i];
        if (r is null)
        {
            failures.Add(Invariant($"PT: the report has no step {t}->{t + 1} run at b = {b}: the order stamped {t} did not set the split in its delivery step"));
            return null;
        }
        SplitReportRow? w = Row(twin.Rows, t, r.Polity);
        if (w is null)
        {
            failures.Add(Invariant($"PT: the twin has no step {t}->{t + 1} for polity {r.Polity}, the step that delivers the order"));
            return null;
        }
        if (w.BuildingsPercent == b)
        {
            failures.Add(Invariant($"PT: the twin ran b = {b} at step {t}->{t + 1} too: it is not the run without the order"));
            return null;
        }

        // Before T the twin IS the report (the same orders): its rows byte for byte.
        for (int i = 0; i < twin.Rows.Count; i++)
        {
            SplitReportRow x = twin.Rows[i];
            if (x.Turn >= t) continue;
            SplitReportRow? y = Row(report, x.Turn, x.Polity);
            if (y is null || !SameBytes(x, y))
            {
                failures.Add(Invariant($"PT: the twin and the report differ at step {x.Turn}->{x.To} (polity {x.Polity}), before the order's step {t}"));
                return null;
            }
        }

        // At T: the same purse, claim and road demand of every good — only the split differs.
        for (int j = 0; j < r.Goods.Length; j++)
        {
            SplitGoodFigures g = r.Goods[j];
            if (w.GoodOf(g.Id) is not { } c || c.S != g.S || c.Db != g.Db || c.Dr != g.Dr)
            {
                failures.Add(Invariant($"PT: at step {t}->{t + 1} the twin's {g.Good} differs before the split applies ") +
                             Invariant($"(the report's S {g.S}, D_b {g.Db}, D_r {g.Dr}): the twins were not delivered the order in the same world"));
                return null;
            }
        }

        // The twin's own step T is the rule's for ITS b.
        var violations = new List<(int Property, string Reason)>();
        Universal(w, RoadOrdersAt(twin.Rows, t) <= 1, violations);
        if (violations.Count > 0)
        {
            failures.Add(Invariant($"PT: the twin's step {t}->{t + 1} (b = {w.BuildingsPercent}) breaks P{violations[0].Property}: {violations[0].Reason}"));
            return null;
        }

        // ... and building work drew a different amount of some good in the order's own step (listed: the goods whose grant
        // the new b changed first, then the rest, each ascending id).
        var differs = new System.Text.StringBuilder();
        for (int pass = 0; pass < 2; pass++)
            for (int j = 0; j < r.Goods.Length; j++)
            {
                SplitGoodFigures g = r.Goods[j], c = w.GoodOf(g.Id)!;
                if (g.Build == c.Build || (g.Gb != c.Gb) != (pass == 0)) continue;
                differs.Append(differs.Length == 0 ? "" : ", ")
                    .Append(Invariant($"{g.Good} {g.Build} vs {c.Build} (E_b {g.Eb} vs {c.Eb}, G_b {g.Gb} vs {c.Gb})"));
            }
        if (differs.Length == 0)
        {
            failures.Add(Invariant($"PT: the order changed b from {w.BuildingsPercent} to {b} at step {t}->{t + 1}, but building work drew ") +
                         "the same of every good as in the twin: the new split did not change spending in its delivery step");
            return null;
        }
        return Invariant($"the kind-12 order at {t} (PT): b {w.BuildingsPercent} -> {b} in step {t}->{t + 1}; building work drew, ") +
               "against the twin without it: " + differs;
    }

    private static bool SameBytes(SplitReportRow a, SplitReportRow b)
    {
        using var x = new MemoryStream();
        using var y = new MemoryStream();
        SplitReport.Write(x, a);
        SplitReport.Write(y, b);
        return x.ToArray().AsSpan().SequenceEqual(y.ToArray());
    }

    private static SplitReportRow? Row(IReadOnlyList<SplitReportRow> rows, long turn, int polity)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].Turn == turn && rows[i].Polity == polity) return rows[i];
        return null;
    }

    private static int RoadOrdersAt(IReadOnlyList<SplitReportRow> rows, long turn)
    {
        int n = 0;
        for (int i = 0; i < rows.Count; i++) if (rows[i].Turn == turn && rows[i].RoadPercent is not null) n++;
        return n;
    }

    private static string More(int n) => n > 1 ? $" ({n} violations in all)" : "";

    private static string List(List<string> items)
    {
        if (items.Count == 0) return "none";
        const int shown = 12;
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < items.Count && i < shown; i++) text.Append(i == 0 ? "" : "; ").Append(items[i]);
        if (items.Count > shown) text.Append($"; ... ({items.Count} in all)");
        return text.ToString();
    }
}
