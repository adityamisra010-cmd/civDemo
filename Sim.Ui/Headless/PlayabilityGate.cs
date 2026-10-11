using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework.Input;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Ages;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Headless;

/// <summary>The verdict for one control in one state.</summary>
public enum GateResult
{
    /// <summary>Clicked: no crash, and the intended UI or state transition happened.</summary>
    Pass,
    /// <summary>Not offered in this state, and the UI says why (a locked node shows its lock; a block is absent
    /// because the capability is not known yet) — never a control that silently does nothing.</summary>
    NotOffered,
    /// <summary>A crash, a silent no-op, or the wrong transition.</summary>
    Fail,
}

/// <summary>One row of the coverage table.</summary>
public sealed record GateRow(string State, string Area, string Control, GateResult Result, string Detail)
{
    /// <summary>P6: the coverage keys (<see cref="UiCoverage"/>) of what the row pressed while it ran — the controls,
    /// hit kinds and (Shift) inspectable kinds it exercised. A passing row covers them for its state.</summary>
    public IReadOnlyList<string> Covers { get; init; } = [];
}

/// <summary>What one gate run found.</summary>
public sealed class GateReport
{
    public List<GateRow> Rows { get; } = [];
    /// <summary>Crashes, draw-data violations, duplicate ids, ImGui error tooltips, failed rigs.</summary>
    public List<string> Problems { get; } = [];
    public List<string> States { get; } = [];
    public List<string> Notes { get; } = [];
    public int Frames { get; set; }
    public int MonkeyActions { get; set; }
    public int MaxListVertices { get; set; }
    public long Milliseconds { get; set; }

    public int Count(GateResult r) { int n = 0; foreach (GateRow row in Rows) if (row.Result == r) n++; return n; }
    public bool Ok => Problems.Count == 0 && Count(GateResult.Fail) == 0;

    /// <summary>The coverage table: one row per control, one column per state.</summary>
    public string Markdown()
    {
        var sb = new StringBuilder();
        var controls = new List<(string Area, string Control)>();
        foreach (GateRow r in Rows) if (!controls.Contains((r.Area, r.Control))) controls.Add((r.Area, r.Control));
        sb.Append("| area | control |");
        foreach (string s in States) sb.Append(' ').Append(s).Append(" |");
        sb.Append('\n').Append("|---|---|");
        foreach (string _ in States) sb.Append("---|");
        sb.Append('\n');
        foreach ((string area, string control) in controls)
        {
            sb.Append("| ").Append(area).Append(" | `").Append(control).Append("` |");
            foreach (string s in States)
            {
                GateRow? hit = null;
                foreach (GateRow r in Rows) if (r.State == s && r.Area == area && r.Control == control) { hit = r; break; }
                sb.Append(' ').Append(hit is null ? "-" : hit.Result switch
                {
                    GateResult.Pass => "PASS",
                    GateResult.NotOffered => "n/o",
                    _ => "**FAIL**",
                }).Append(" |");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Every NotOffered and Fail row with its detail (the reason shown to the player, or the defect).</summary>
    public string Details()
    {
        var sb = new StringBuilder();
        foreach (GateRow r in Rows)
            if (r.Result != GateResult.Pass)
                sb.Append("- ").Append(r.State).Append(" / `").Append(r.Control).Append("`: ")
                  .Append(r.Result == GateResult.Fail ? "**FAIL** " : "not offered - ").Append(r.Detail).Append('\n');
        return sb.ToString();
    }
}

/// <summary>How a gate run is set up.</summary>
public sealed record GateOptions(
    string AssetsRoot, string WorkDir,
    int? AiEmpires = null, int MonkeySteps = 400, ulong MonkeySeed = 20261005UL,
    Action<string>? Log = null, IReadOnlyList<string>? OnlyStates = null);

/// <summary>
/// THE M5 PLAYABILITY GATE (Director §2 / §15, M5 hardening H1). For each meaningful game state, it drives the
/// REAL per-frame UI (<see cref="GameUi"/> in <see cref="UiFrameHarness"/>) the way a player does — clicks where the
/// control is drawn, keys, the wheel, drags — and checks, for EVERY interactive control the code draws, that the
/// click does not crash and produces its intended transition; a control that is not available must be absent with
/// a stated reason or visibly locked, never a silent no-op. Then a seeded "monkey" clicks, drags, scrolls and
/// presses keys at random for many frames. Every frame is checked by the harness (draw data inside ImGui's 16-bit
/// contract, no duplicate item ids, no ImGui error tooltip). Nothing here is a model of the UI: the controls are
/// found from what the UI itself drew (<see cref="UiControls"/>, the screens' hit regions).
/// </summary>
public static partial class PlayabilityGate
{
    private static readonly PolityId Me = UiPlayer.Empire;

    /// <summary>A named state and how it is built (by playing, or by a stated rig).</summary>
    public sealed record GateState(string Name, string How, Func<UiSession> Build, int Width = 1280, int Height = 800);

    /// <summary>The states the Director listed (§2), in order.</summary>
    public static IReadOnlyList<GateState> States(int? aiEmpires)
    {
        int ai = aiEmpires ?? 0;
        string aiTag = ai == 0 ? "" : " (" + ai.ToString(CultureInfo.InvariantCulture) + " AI)";
        var list = new List<GateState>
        {
            new("A1 turn 1" + aiTag, "canonical founded world, seed 42, turn 1", () => UiSession.Start(42, aiEmpiresOverride: ai)),
        };
        if (aiEmpires is null)
            list.Add(new("one AI", "canonical founded world with --ai-empires 1, turn 1", () => UiSession.Start(42, aiEmpiresOverride: 1)));
        list.Add(new("wide window" + aiTag, "turn 1 in a maximised 1920x1009 window (the chrome follows the window; the A1 tree opens past 65,535 vertices)",
            () => UiSession.Start(42, aiEmpiresOverride: ai), 1920, 1009));
        list.Add(new("narrow window" + aiTag, "turn 1 in the smallest window the game allows (" + MinSizeText + "; the window cannot be resized below it, and the command bar must fit)",
            () => UiSession.Start(42, aiEmpiresOverride: ai), PanelLayout.MinWindowWidth, PanelLayout.MinWindowHeight));
        list.Add(new("target set" + aiTag, "turn 1 + the cheapest Technology node ordered, one End Turn", () => TargetSet(ai)));
        list.Add(new("research done" + aiTag, "played: cheapest Technology node targeted and End Turn until one node completes", () => ResearchDone(ai)));
        list.Add(new("tax available" + aiTag, "rig: the Taxation civic and a road class (track_road) with their prerequisites completed, Age III, one End Turn", () => TaxAvailable(ai)));
        list.Add(new("age advance" + aiTag, "rig: Age II's research milestones completed; the advance is then made THROUGH THE UI", () => AgeEligible(ai)));
        list.Add(new("colony+revolt" + aiTag, "rig: a colony founded by the real ColonizationSystem (stranded source) and one settlement passed to a new AI polity as RevoltSystem does", () => SettlementChanges(ai)));
        if (aiEmpires is null)
            list.Add(new("hidden rival", "P7: the one-AI world at turn 1 seen through a knowledge stub that hides the AI polity whole (KnowledgeStubs.HidePolity) - every surface draws, Tab and clicks skip what the player does not know", HiddenRival));
        return list;
    }

    /// <summary>P7: the AI polity the hidden-rival state hides (the one-AI world's only rival).</summary>
    public static readonly PolityId HiddenPolity = new(2);

    /// <summary>P7: the canonical one-AI world, one End Turn played, its session's player knowledge a stub that hides
    /// <see cref="HiddenPolity"/> (recomputed after every step, so a colony it founds is hidden too). The world, the orders
    /// and every hash are the one-AI world's; only what the UI shows differs.</summary>
    public static UiSession HiddenRival()
    {
        UiSession first = UiSession.Start(42, aiEmpiresOverride: 1);
        UiSession s = UiSession.StartFrom(first.World.Clone(), 42, null, null,
            knowledgeOf: w => KnowledgeStubs.HidePolity(w, Me, HiddenPolity));
        s.EndTurn();
        if (KnowledgeStubs.SettlementsOf(s.World, HiddenPolity).Count == 0) throw new InvalidOperationException("hidden-rival rig: the AI holds no settlement");
        return s;
    }

    private static string MinSizeText => PanelLayout.MinWindowWidth.ToString(CultureInfo.InvariantCulture) + "x"
        + PanelLayout.MinWindowHeight.ToString(CultureInfo.InvariantCulture);

    public static GateReport Run(GateOptions o)
    {
        var report = new GateReport();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (GateState state in States(o.AiEmpires))
        {
            if (o.OnlyStates is { } only && !only.Contains(state.Name)) continue;
            report.States.Add(state.Name);
            report.Notes.Add(state.Name + ": " + state.How);
            o.Log?.Invoke("state: " + state.Name + " - building");
            UiSession session;
            try { session = state.Build(); }
            catch (Exception e) { report.Problems.Add(state.Name + ": rig failed: " + e.GetType().Name + ": " + e.Message); continue; }
            string dir = Path.Combine(o.WorkDir, Slug(state.Name));
            using UiFrameHarness h = UiFrameHarness.Start(session, dir, o.AssetsRoot, developer: false, state.Width, state.Height);
            h.Context = state.Name;
            var run = new StateRun(h, state.Name, report, o.Log);
            try
            {
                run.Sweep();
                run.Monkey(o.MonkeySteps, o.MonkeySeed);
                run.Coverage();
            }
            catch (Exception e)
            {
                report.Problems.Add(state.Name + ": UNHANDLED " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
            report.Problems.AddRange(h.Problems);
            report.Frames += h.Frames;
            report.MaxListVertices = Math.Max(report.MaxListVertices, h.MaxListVertices);
            o.Log?.Invoke("state: " + state.Name + " - " + h.Frames.ToString(CultureInfo.InvariantCulture) + " frames, "
                + report.Count(GateResult.Fail).ToString(CultureInfo.InvariantCulture) + " fail rows so far");
        }
        report.Milliseconds = clock.ElapsedMilliseconds;
        return report;
    }

    /// <summary>P6: the camera rows alone — W A S D, the map's settlement selection (CameraFocus.CenterOn + click),
    /// CenterOn itself, drag pan and the wheel zooms — exactly as the sweep runs them, on <paramref name="h"/>.
    /// <paramref name="lookMutant"/> is the verification seam (StateRun's lookMutant): null in every real run.</summary>
    public static IReadOnlyList<GateRow> CameraRows(UiFrameHarness h, string state, Action<UiSession>? lookMutant = null)
    {
        var report = new GateReport();
        var run = new StateRun(h, state, report, null, lookMutant);
        run.Camera();
        return report.Rows;
    }

    /// <summary>P7: the knowledge rows alone — "Tab never reaches a hidden settlement" and "a click on a hidden position
    /// selects nothing" — exactly as the sweep runs them in the hidden-rival state, on <paramref name="h"/> (none when the
    /// session's knowledge hides nothing).</summary>
    public static IReadOnlyList<GateRow> KnowledgeRows(UiFrameHarness h, string state)
    {
        var report = new GateReport();
        var run = new StateRun(h, state, report, null);
        run.Knowledge();
        return report.Rows;
    }

    private static string Slug(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        return sb.ToString();
    }

    // ================================================================== rigs

    private static UiSession TargetSet(int ai)
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: ai);
        ResearchContent c = s.Config.Research!;
        if (ResearchQuery.CheapestAvailable(s.World, c, Me, ResearchTree.Technology) is not ResearchNodeId n || !s.EmitResearchOrder(n))
            throw new InvalidOperationException("no research node could be ordered at turn 1");
        s.EndTurn();
        return s;
    }

    private static UiSession ResearchDone(int ai)
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: ai);
        ResearchContent c = s.Config.Research!;
        for (int t = 0; t < 200 && ResearchQuery.CompletedNodes(s.World, c, Me).Length == 0; t++)
        {
            if (!ResearchQuery.TryGetTarget(s.World, Me, out _) && ResearchQuery.CheapestAvailable(s.World, c, Me, ResearchTree.Technology) is ResearchNodeId n)
                s.EmitResearchOrder(n);
            s.EndTurn();
        }
        if (ResearchQuery.CompletedNodes(s.World, c, Me).Length == 0) throw new InvalidOperationException("no research completed in 200 turns");
        return s;
    }

    /// <summary>Every prerequisite-ancestor of the named nodes, the nodes included (content order).</summary>
    public static List<int> Ancestry(ResearchContent c, params string[] ids)
    {
        var seen = new bool[c.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in ids)
        {
            int at = c.IndexOfId(id);
            if (at < 0) throw new InvalidOperationException("unknown research node " + id);
            stack.Push(at);
        }
        while (stack.Count > 0)
        {
            int n = stack.Pop();
            if (seen[n]) continue;
            seen[n] = true;
            foreach (int p in c.Nodes[n].PrerequisiteNodes) stack.Push(p);
        }
        var r = new List<int>();
        for (int i = 0; i < seen.Length; i++) if (seen[i]) r.Add(i);
        return r;
    }

    private static void Complete(WorldState w, ResearchContent c, IEnumerable<int> nodes)
    {
        foreach (int n in nodes)
            if (!ResearchQuery.IsCompleted(w, Me, c.Nodes[n].Key)) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, c.Nodes[n].Key));
    }

    /// <summary>The Taxation civic and its prerequisites completed AND the player at Age III: available under the
    /// research gate alone and under an Age-III gate.</summary>
    private static UiSession TaxAvailable(int ai)
    {
        WorldState w = UiFounding.Found(42, aiEmpiresOverride: ai);
        var cfg = UiFounding.ProductionConfig();
        Complete(w, cfg.Research!, Ancestry(cfg.Research!, "taxation", "track_road"));
        w = EraPreview.WorldAt(w, cfg.Ages!, Me, 3);
        UiSession s = UiSession.StartFrom(w, 42);
        s.EndTurn();
        if (!Sim.Core.State.Governance.CanLevyTax(s.World, s.Config, Me)) throw new InvalidOperationException("tax rig: the edict is still unavailable");
        return s;
    }

    /// <summary>Age II's research milestones (core and supporting) completed with their prerequisites; then played
    /// forward (bounded) until the player is eligible.</summary>
    private static UiSession AgeEligible(int ai)
    {
        WorldState w = UiFounding.Found(42, aiEmpiresOverride: ai);
        var cfg = UiFounding.ProductionConfig();
        ResearchContent c = cfg.Research!;
        AgeContent ages = cfg.Ages!;
        var wanted = new List<string>();
        if (AgeQuery.Evaluate(w, ages, Me) is { } report)
        {
            foreach (MilestoneStatus m in report.Core)
                if (m.Milestone.Fact.Kind == MilestoneFactKind.Research)
                    foreach (int key in m.Milestone.Fact.NodeKeys) wanted.Add(c.Nodes[c.IndexOf(new ResearchNodeId(key))].Id);
            foreach (MilestoneStatus m in report.Supporting)
                if (m.Milestone.Fact.Kind == MilestoneFactKind.Research)
                    foreach (int key in m.Milestone.Fact.NodeKeys) wanted.Add(c.Nodes[c.IndexOf(new ResearchNodeId(key))].Id);
        }
        Complete(w, c, Ancestry(c, [.. wanted]));
        UiSession s = UiSession.StartFrom(w, 42);
        AgePreview.PlayToEligible(s, 120);
        if (!AgeQuery.IsEligible(s.World, ages, Me)) throw new InvalidOperationException("age rig: not eligible for Age II");
        return s;
    }

    /// <summary>A colony founded by the real pipeline (ColonizationTests' stranded-source rig: every settlement but
    /// one loses its food, the one keeps a deficit — so its migrants have no viable destination and found a new
    /// settlement), and one of the player's other settlements passed to a new AI polity exactly as RevoltSystem
    /// passes it (control row, polity row, knowledge copied).</summary>
    private static UiSession SettlementChanges(int ai)
    {
        UiSession first = UiSession.Start(42, aiEmpiresOverride: ai);
        first.EndTurn();   // SettlementDistances exist from turn 1 on (migration reads them)
        WorldState original = first.World;
        WorldState w = original.Clone();
        EmpireQuery.TryGetCapital(w, Me, out SettlementId capital);
        SettlementId source = capital;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            if (w.Settlements[s].Id == source) continue;
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Settlement == w.Settlements[s].Id)
                    w.GoodStocks[i] = w.GoodStocks[i] with { Amount = Conserved.Zero, LastProducedUnits = 0 };
        }
        w.ConsumptionDeficits.Clear();   // only the source is short of food (the others have nothing at all)
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(source, 0.40, 1000));
        int before = w.Settlements.Count;
        // The real systems, as ColonizationTests' hand-off runs them: catchment, migration, colonization.
        SimConfig cfg = first.Config;
        var handoff = new TurnExecutor(UiSession.ProductionEra(),
            [SystemCatalog.Catchment(cfg), SystemCatalog.Migration(cfg), SystemCatalog.Colonization(cfg, UiSession.ProductionWorldgen())]);
        w = handoff.Step(w);
        if (w.Settlements.Count <= before) throw new InvalidOperationException("colony rig: no settlement was founded");
        // The rest of the world gets its food back (only the stranding was rigged).
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            GoodStockRow row = w.GoodStocks[i];
            if (row.Settlement == source) continue;
            for (int j = 0; j < original.GoodStocks.Count; j++)
                if (original.GoodStocks[j].Settlement == row.Settlement && original.GoodStocks[j].Good == row.Good)
                {
                    w.GoodStocks[i] = row with { Amount = original.GoodStocks[j].Amount, LastProducedUnits = original.GoodStocks[j].LastProducedUnits };
                    break;
                }
        }
        w.ConsumptionDeficits.Clear();

        // The revolt: the player's first non-capital settlement, as RevoltSystem's pass 2–3 write it.
        SettlementId revolted = default;
        bool found = false;
        for (int s = 0; s < w.Settlements.Count && !found; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            if (id == capital) continue;
            if (EmpireQuery.TryGetController(w, id, out PolityId ruler) && ruler.Value == Me.Value) { revolted = id; found = true; }
        }
        if (!found) throw new InvalidOperationException("revolt rig: the player controls only its capital");
        int nextId = 0;
        for (int i = 0; i < w.Polities.Count; i++) nextId = Math.Max(nextId, w.Polities[i].Id.Value);
        var rebel = new PolityId(nextId + 1);
        var keep = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != revolted) keep.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow r in keep) w.Controls.Add(r);
        w.Polities.Add(new PolityRow(rebel, CommandSource.Ai));
        w.Controls.Add(new ControlRow(rebel, revolted, 1.0));
        KnowledgeTransfer.MergeInto(w.ResearchCompleted, Me, rebel);

        UiSession s2 = UiSession.StartFrom(w, 42);
        s2.EndTurn();
        return s2;
    }

    // ================================================================== one state's run

    /// <param name="lookMutant">VERIFICATION SEAM (P6, ADR-015 mutant kill; the precedent is
    /// <see cref="UiFrameHarness.Start"/>'s <c>vtxOffset</c> control): when set, it runs inside every camera row, after
    /// the camera input — standing in for a camera operation that writes. Null in every real run.</param>
    private sealed partial class StateRun(UiFrameHarness h, string state, GateReport report, Action<string>? log,
        Action<UiSession>? lookMutant = null)
    {
        private GameUi Ui => h.Ui;
        private UiSession S => h.Ui.Session;

        /// <summary>P6: appended to every area name while the sweep revisits surfaces after its End Turns.</summary>
        private string _phase = "";

        /// <summary>P6: whether the first developer pass found a played turn (the TURN audit, the grievance account).</summary>
        private bool _devHadTurn;

        private void Row(string area, string control, GateResult r, string detail = "", IReadOnlyList<string>? covers = null)
        {
            area += _phase;
            report.Rows.Add(new GateRow(state, area, control, r, detail) { Covers = covers ?? [] });
            if (r == GateResult.Fail) log?.Invoke("  FAIL " + state + " / " + control + ": " + detail);
        }

        private void Check(string area, string control, Func<(GateResult, string)> act)
        {
            int mark = h.Presses.Count;
            try
            {
                (GateResult r, string d) = act();
                Row(area, control, r, d, PressedSince(mark));
            }
            catch (Exception e)
            {
                Row(area, control, GateResult.Fail, "exception: " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>P6: the coverage keys under every press made since press <paramref name="mark"/>.</summary>
        private IReadOnlyList<string> PressedSince(int mark)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = mark; i < h.Presses.Count; i++)
                foreach (string k in h.Presses[i].Reached) keys.Add(k);
            return [.. keys];
        }

        /// <summary>
        /// P6: THE COVERAGE CHECK, after the sweep and the monkey. Every control family, hit kind and inspectable kind
        /// the UI drew in this state (<see cref="UiFrameHarness.Seen"/>) must have been pressed by a PASSING row of this
        /// state, or be on <see cref="UiCoverage.Allowlist"/> with its reason; each one that is not is a FAIL row.
        /// </summary>
        public void Coverage()
        {
            var mine = new List<GateRow>();
            foreach (GateRow r in report.Rows) if (r.State == state) mine.Add(r);
            IReadOnlyList<string> missing = UiCoverage.Uncovered(h.Seen, mine, UiCoverage.Allowlist);
            foreach (string key in missing)
                Row("coverage", key, GateResult.Fail,
                    "drawn in this state, but no passing gate row pressed it and it is not on the allowlist (UiCoverage.Allowlist)");
            int allowed = 0;
            foreach (CoverageAllowance a in UiCoverage.Allowlist) if (h.Seen.Contains(a.Key)) allowed++;
            Row("coverage", "every drawn control, hit kind and inspectable kind has a gate row",
                missing.Count == 0 ? GateResult.Pass : GateResult.Fail,
                h.Seen.Count.ToString(CultureInfo.InvariantCulture) + " keys drawn: "
                + (h.Seen.Count - allowed - missing.Count).ToString(CultureInfo.InvariantCulture) + " pressed by a passing row, "
                + allowed.ToString(CultureInfo.InvariantCulture) + " allowlisted, "
                + missing.Count.ToString(CultureInfo.InvariantCulture) + " uncovered");
        }

        /// <summary>
        /// P6: a CAMERA row — the "looking changes nothing" idiom of ShiftCheck. <paramref name="act"/> drives the camera
        /// and checks it moved; the row then also requires the world hash and the queued-order list (every
        /// <see cref="OrderRecord"/>, in order) to be exactly as they were before it.
        /// </summary>
        private void LookCheck(string area, string control, Func<(GateResult, string)> act)
        {
            Check(area, control, () =>
            {
                string hash = WorldHash.ComputeHex(S.World);
                OrderRecord[] queued = [.. S.QueuedOrders()];
                (GateResult r, string d) = act();
                lookMutant?.Invoke(S);
                if (Changed(hash, queued) is { } changed)
                    return (GateResult.Fail, (d.Length > 0 ? d + "; " : "") + "LOOKING CHANGED THE GAME: " + changed);
                return (r, d);
            });
        }

        /// <summary>What changed in the world hash and the queued-order list since the snapshot, or null.</summary>
        private string? Changed(string hash, OrderRecord[] queued)
        {
            var what = new List<string>();
            string now = WorldHash.ComputeHex(S.World);
            if (now != hash) what.Add("world hash " + hash[..12] + " -> " + now[..12]);
            IReadOnlyList<OrderRecord> after = S.QueuedOrders();
            bool same = after.Count == queued.Length;
            for (int i = 0; same && i < queued.Length; i++) same = after[i] == queued[i];
            if (!same)
            {
                string first = "";
                for (int i = 0; i < after.Count && first.Length == 0; i++)
                    if (i >= queued.Length || after[i] != queued[i]) first = "; first difference: " + after[i].Kind + " (actor " + after[i].ActorId.ToString(CultureInfo.InvariantCulture) + ")";
                what.Add("queued orders " + queued.Length.ToString(CultureInfo.InvariantCulture) + " -> " + after.Count.ToString(CultureInfo.InvariantCulture) + first);
            }
            return what.Count == 0 ? null : string.Join("; ", what);
        }

        private static (GateResult, string) Expect(bool ok, string what) => ok ? (GateResult.Pass, "") : (GateResult.Fail, what);

        private void CloseEverything()
        {
            if (Ui.Inspector.IsOpen) h.Key(Keys.Escape);   // M5 polish: the info card closes first
            for (int i = 0; i < 4 && (Ui.ProgressionOpen || Ui.Age.FlowOpen); i++) h.Key(Keys.Escape);
            if (Ui.ContextFolded) h.ClickControl("panel-unfold");   // P1: a folded column's close button is on its panel
            if (Ui.SelectionFolded) h.ClickControl("card-fold");
            if (Ui.AgePanelOpen) h.ClickControl("band-age");
            if (Ui.SelectedUnit >= 0) h.Key(Keys.Escape);
            if (Ui.OpenSection != Section.None) h.ClickControl("close");
            if (Ui.Developer) h.Key(Keys.F12);
        }

        private int Queued => S.QueuedOrders().Count;

        public void Sweep()
        {
            CloseEverything();
            Sections();
            CloseEverything();
            Figures();
            CloseEverything();
            Territory();
            Research();
            CloseEverything();
            Policy();            // before the Age flow: the POLICY "Advance..." is offered only while eligible
            CloseEverything();
            AgeSurfaces();
            CloseEverything();
            Developer();
            CloseEverything();
            InfoArea();          // M5 polish (directive §9): Shift+click one subject of every kind
            CloseEverything();
            Keyboard();
            CloseEverything();
            Map();
            CloseEverything();
            EndTurnChecks();
            CloseEverything();
            AfterEndTurns();
            CloseEverything();
        }

        /// <summary>
        /// P6: the surfaces that only fill once turns have been played, revisited after <see cref="EndTurnChecks"/> (the
        /// coverage check found them drawn — the monkey reached them — with no row): the ANNALS' "all entries" toggle
        /// (past 12 entries), the Age panel's research-next line (an Age advance brings new milestones) and, in a state
        /// whose first developer pass came before any End Turn, the developer surfaces
        /// (the TURN audit, the grievance account). Their rows carry the area suffix " (after End Turn)".
        /// </summary>
        private void AfterEndTurns()
        {
            _phase = " (after End Turn)";
            h.ClickControl("nav:Annals");
            Annals();
            CloseEverything();
            AgeResearchNextRow();
            CloseEverything();
            if (!_devHadTurn)
            {
                Developer();
                CloseEverything();
            }
            _phase = "";
        }

        // -------------------------------------------------------------- the command bar

        private void Sections()
        {
            foreach (Section section in GameSections.Roster(false))
            {
                string nav = "nav:" + section;
                Check("command bar", nav, () =>
                {
                    if (!h.ClickControl(nav)) return (GateResult.Fail, "not drawn");
                    return Expect(Ui.OpenSection == section, "open section is " + Ui.OpenSection);
                });
                if (section == Section.Annals) Annals();
                if (section == Section.Trends) Trends();
                Check("command bar", nav + " (again: closes)", () =>
                {
                    h.ClickControl(nav);
                    return Expect(Ui.OpenSection == Section.None, "open section is " + Ui.OpenSection);
                });
                Check("panel", "close (" + section + ")", () =>
                {
                    h.ClickControl(nav);
                    if (!h.ClickControl("close")) return (GateResult.Fail, "close button not drawn");
                    return Expect(Ui.OpenSection == Section.None, "open section is " + Ui.OpenSection);
                });
            }
        }

        private void Annals()
        {
            Check("annals", "annals-all", () =>
            {
                if (Ui.Controls.Find("annals-all") is null)
                    return (GateResult.NotOffered, S.AnnalLines.Count.ToString(CultureInfo.InvariantCulture) + " entries: all are shown (the toggle appears past 12)");
                bool before = Ui.AnnalsShowAll;
                h.ClickControl("annals-all");
                bool flipped = Ui.AnnalsShowAll != before;
                h.ClickControl("annals-all");
                return Expect(flipped && Ui.AnnalsShowAll == before, "toggle did not flip");
            });
        }

        private void Trends()
        {
            for (int i = 0; i < Ui.TrendMetrics.Count; i++)
            {
                int k = i;
                string id = "trend-" + k.ToString(CultureInfo.InvariantCulture);
                Check("trends", id, () =>
                {
                    if (!h.ClickControl(id)) return (GateResult.Fail, "metric button not drawn");
                    return Expect(Ui.TrendIndex == k, "trend index " + Ui.TrendIndex);
                });
            }
            Check("trends", "trend-place", () => { h.ClickControl("trend-place"); return Expect(!Ui.TrendWorldScope, "scope still world"); });
            Check("trends", "trend-world", () => { h.ClickControl("trend-world"); return Expect(Ui.TrendWorldScope, "scope still settlement"); });
        }

        private void Figures()
        {
            foreach (string fig in new[] { "fig:WorldPopulation", "fig:WorldFood", "fig:SettlementPopulation", "fig:SettlementFood",
                         "fig:SettlementHappiness", "fig:SettlementGrievance" })
            {
                Check("status band / selection card", fig, () =>
                {
                    CloseEverything();
                    if (!h.ClickControl(fig)) return (GateResult.Fail, "not drawn");
                    return Expect(Ui.OpenSection != Section.None, "the figure opened nothing");
                });
            }
        }

        private void Territory()
        {
            Check("command bar", "territory", () =>
            {
                bool before = Ui.ShowTerritory;
                h.ClickControl("territory");
                bool flipped = Ui.ShowTerritory != before;
                h.ClickControl("territory");
                return Expect(flipped && Ui.ShowTerritory == before, "toggle did not flip");
            });
        }

        // -------------------------------------------------------------- research / Knowledge

        private ProgressionScreen Screen => Ui.Progression!;

        private HitRegion? FindHit(HitKind kind, int arg = int.MinValue)
        {
            foreach (HitRegion r in Screen.Hits) if (r.Kind == kind && (arg == int.MinValue || r.Arg == arg)) return r;
            return null;
        }

        private bool ClickHit(HitKind kind, int arg = int.MinValue)
        {
            if (FindHit(kind, arg) is not { } r) return false;
            h.Click(r.Rect.CenterX, r.Rect.CenterY);
            return true;
        }

        private void OpenResearch()
        {
            if (!Ui.ProgressionOpen) h.ClickControl("band-research");
        }

        private void Research()
        {
            CloseEverything();
            Check("research", "band-research (open)", () =>
            {
                if (!h.ClickControl("band-research")) return (GateResult.Fail, "not drawn");
                return Expect(Ui.ProgressionOpen, "the Research screen did not open");
            });
            Check("research", "K key (close / open)", () =>
            {
                h.Key(Keys.K);
                bool closed = !Ui.ProgressionOpen;
                h.Key(Keys.K);
                return Expect(closed && Ui.ProgressionOpen, "K did not toggle the screen");
            });
            foreach (TreeTab tab in new[] { TreeTab.Civics, TreeTab.Technology })
                Check("research", "tab:" + tab, () =>
                {
                    if (!ClickHit(HitKind.Tab, (int)tab)) return (GateResult.Fail, "tab not drawn");
                    return Expect(Screen.Tab == tab, "tab is " + Screen.Tab);
                });
            foreach (Lens lens in Lenses.All)
                Check("research", "lens:" + lens, () =>
                {
                    if (!ClickHit(HitKind.Lens, (int)lens)) return (GateResult.Fail, "lens not drawn");
                    bool ok = Screen.Lens == lens;
                    h.Idle(2);
                    return Expect(ok, "lens is " + Screen.Lens);
                });
            ClickHit(HitKind.Lens, (int)Lens.KnowledgeAndTechnology);
            foreach (TreeTab tab in new[] { TreeTab.Technology, TreeTab.Civics })
            {
                ClickHit(HitKind.Tab, (int)tab);
                Nodes(tab);
                TreeNavigation(tab);
            }
            ClickHit(HitKind.Tab, (int)TreeTab.Technology);
            Check("research", "jump to target / frontier", () =>
            {
                int f = Screen.FrontierNode();
                if (!ClickHit(HitKind.Frontier)) return (GateResult.Fail, "not drawn");
                return Expect(f < 0 || Screen.Selected == f, "selected " + Screen.Selected + " not the frontier " + f);
            });
            Check("research", "age chip (opens the Age panel)", () =>
            {
                if (FindHit(HitKind.AgeOpen) is null)
                    return (GateResult.NotOffered, "the tab bar has no room for the Age chip at " + h.Width.ToString(CultureInfo.InvariantCulture)
                        + " px (it needs 150 px beside the target capsule); the Age is on the status band");
                ClickHit(HitKind.AgeOpen);
                bool ok = !Ui.ProgressionOpen && (Ui.AgePanelOpen || Ui.Age.FlowOpen);
                return Expect(ok, "tree open " + Ui.ProgressionOpen + ", Age panel " + Ui.AgePanelOpen);
            });
            DetailControls();
            CloseEverything();
            OpenResearch();
            Check("research", "close button", () =>
            {
                if (!ClickHit(HitKind.Close)) return (GateResult.Fail, "not drawn");
                return Expect(!Ui.ProgressionOpen, "still open");
            });
            OpenResearch();
            // UR-4: in a narrow window a selected node's detail DRAWER is open over the tree; the first Escape closes the
            // drawer (the screen stays), the next closes the screen.
            Check("research", "Escape (closes the detail drawer, then the screen)", () =>
            {
                bool drawer = Screen.DrawerOpen;
                h.Key(Keys.Escape);
                if (drawer)
                {
                    if (!Ui.ProgressionOpen || Screen.DrawerOpen) return (GateResult.Fail, "the first Escape did not close only the drawer");
                    h.Key(Keys.Escape);
                }
                return Expect(!Ui.ProgressionOpen, "still open");
            });
        }

        /// <summary>
        /// P6 (coverage): the detail panel's own regions — SET AS RESEARCH TARGET for an available node selected WITHOUT
        /// ordering it (from its card's "Show in research tree": the only way a player selects an available node without
        /// targeting it once the frontier is the target), the drawer's close button (narrow windows), and the panel's
        /// body, which is inert (it absorbs a click: nothing under it is selected or ordered).
        /// </summary>
        private void DetailControls()
        {
            Check("research", "an available node selected from its card (Show in research tree), then SET AS RESEARCH TARGET", () =>
            {
                CloseEverything();
                OpenResearch();
                Screen.ResetView();
                h.Idle(30);
                Sim.Ui.Info.InfoHit? pick = null;
                foreach (Sim.Ui.Info.InfoHit x in Ui.InfoRegistry.Hits)
                {
                    if (x.Subject.Kind != InfoKind.ResearchNode || !Screen.Canvas.Contains(x.Rect.CenterX, x.Rect.CenterY) || Screen.Obscured(x.Rect.CenterX, x.Rect.CenterY)) continue;
                    int ci = Screen.Content.IndexOf(new ResearchNodeId((int)x.Subject.Id));
                    ResearchNodeView v = Screen.Snapshot!.Nodes[ci];
                    if (v.Available && !v.IsTarget && Screen.PendingTarget != ci) { pick = x; break; }
                }
                if (pick is not { } hit) return (GateResult.NotOffered, "no available node besides the target in view");
                int node = Screen.Content.IndexOf(new ResearchNodeId((int)hit.Subject.Id));
                h.ShiftClick(hit.Rect.CenterX, hit.Rect.CenterY);
                Sim.Ui.Info.InfoPanelHit? show = null;
                foreach (Sim.Ui.Info.InfoPanelHit x in Ui.Inspector.Hits) if (x.Kind == Sim.Ui.Info.InfoHitKind.ShowInTree) show = x;
                if (show is not { } sh) return (GateResult.Fail, "the node's card has no Show in research tree");
                int q = Queued;
                h.Click(sh.Rect.CenterX, sh.Rect.CenterY);
                h.Idle(10);
                if (Screen.Selected != node || Queued != q || Ui.Inspector.IsOpen)
                    return (GateResult.Fail, "selected " + Screen.Selected + " (want " + node + "), queued +" + (Queued - q) + ", card " + Ui.Inspector.IsOpen);
                if (!ClickHit(HitKind.SetTarget)) return (GateResult.Fail, "a selected available node shows no SET AS RESEARCH TARGET");
                return Expect(S.QueuedResearchChoice() == Screen.Content.Nodes[node].Key.Value, "no order for the node");
            });
            Check("research", "detail panel body (inert: a click on it selects and orders nothing)", () =>
            {
                CloseEverything();
                OpenResearch();
                if (Screen.Selected < 0) ClickHit(HitKind.Frontier);
                h.Idle(10);
                if (FindHit(HitKind.Panel) is not { } panel) return (GateResult.NotOffered, "no detail panel drawn (no node selected, narrow window)");
                (double X, double Y)? spot = null;
                for (double y = panel.Rect.Y + 8; y < panel.Rect.Bottom - 8 && spot is null; y += 10)
                    for (double x = panel.Rect.X + 8; x < panel.Rect.Right - 8 && spot is null; x += 10)
                    {
                        bool other = false;
                        foreach (HitRegion r in Screen.Hits) if (r.Kind != HitKind.Panel && r.Rect.Contains(x, y)) { other = true; break; }
                        if (!other) spot = (x, y);
                    }
                if (spot is not { } at) return (GateResult.NotOffered, "every point of the panel is another control");
                int selected = Screen.Selected, q = Queued;
                h.Click(at.X, at.Y);
                return Expect(Screen.Selected == selected && Queued == q && Ui.ProgressionOpen,
                    "selected " + selected + " -> " + Screen.Selected + ", queued +" + (Queued - q));
            });
            Check("research", "detail drawer close button (narrow windows: deselects, the tree stays)", () =>
            {
                CloseEverything();
                OpenResearch();
                if (Screen.Selected < 0) ClickHit(HitKind.Frontier);
                h.Idle(10);
                if (FindHit(HitKind.CloseDetail) is null)
                    return (GateResult.NotOffered, Screen.Selected < 0 ? "no node selected"
                        : "the detail panel is docked beside the tree at " + h.Width.ToString(CultureInfo.InvariantCulture) + " px: it has no close button");
                int q = Queued;
                ClickHit(HitKind.CloseDetail);
                return Expect(Screen.Selected == -1 && Ui.ProgressionOpen && Queued == q, "selected " + Screen.Selected + ", tree " + Ui.ProgressionOpen);
            });
        }

        private void Nodes(TreeTab tab)
        {
            Screen.ResetView();
            h.Idle(30);
            int locked = -1, available = -1;
            foreach (int v in Screen.VisibleVertices())
            {
                int ci = Screen.Graph.Vertices[v].ContentIndex;
                if (Screen.Graph.Vertices[v].External || Screen.Layout.Placed[v].Hidden) continue;
                ResearchNodeView view = Screen.Snapshot!.Nodes[ci];
                (double x, double y) = CardCentre(v);
                if (!Screen.Canvas.Contains(x, y) || Screen.Obscured(x, y)) continue;
                if (view.Available && !view.IsTarget && available < 0) available = v;
                if (view.State == NodeState.Locked && locked < 0) locked = v;
            }
            Check("research", "node hover (" + tab + ")", () =>
            {
                int v = available >= 0 ? available : locked;
                if (v < 0) return (GateResult.NotOffered, "no card in view");
                (double x, double y) = CardCentre(v);
                h.MoveTo(x, y);
                return Expect(Screen.Hovered == Screen.Graph.Vertices[v].ContentIndex, "hovered " + Screen.Hovered);
            });
            Check("research", "locked node (" + tab + ")", () =>
            {
                if (locked < 0) return (GateResult.NotOffered, "no locked card in view");
                int q = Queued;
                (double x, double y) = CardCentre(locked);
                h.Click(x, y);
                int ci = Screen.Graph.Vertices[locked].ContentIndex;
                bool selected = Screen.Selected == ci;
                // A locked node is selected (the detail panel shows WHY it is locked) and never orders anything.
                return Expect(selected && Queued == q && Screen.Snapshot!.Nodes[ci].Lock != LockReason.None,
                    "selected " + Screen.Selected + ", queued " + (Queued - q) + ", lock " + Screen.Snapshot!.Nodes[ci].Lock);
            });
            Check("research", "jump to frontier, then the detail panel's SET AS RESEARCH TARGET (" + tab + ")", () =>
            {
                int f = Screen.FrontierNode();
                if (f < 0) return (GateResult.NotOffered, "no available node in this tree (every node is locked behind prerequisites - each card says which)");
                ClickHit(HitKind.Frontier);
                h.Idle(30);
                if (Screen.Selected != f) return (GateResult.Fail, "frontier not selected");
                if (FindHit(HitKind.SetTarget) is null)
                {
                    ResearchNodeView v = Screen.Snapshot!.Nodes[f];
                    return v.IsTarget || Screen.PendingTarget == f
                        ? (GateResult.NotOffered, "the frontier is already the target - the button reads CURRENT RESEARCH TARGET / ORDERED")
                        : (GateResult.Fail, "an available node shows no SET AS RESEARCH TARGET button");
                }
                ClickHit(HitKind.SetTarget);
                return Expect(S.QueuedResearchChoice() == Screen.Content.Nodes[f].Key.Value, "no order for the frontier");
            });
            Check("research", "available node card: click = select + set target (" + tab + ")", () =>
            {
                int pick = -1;
                foreach (int v in Screen.VisibleVertices())
                {
                    int ci = Screen.Graph.Vertices[v].ContentIndex;
                    if (Screen.Graph.Vertices[v].External || Screen.Layout.Placed[v].Hidden) continue;
                    ResearchNodeView view = Screen.Snapshot!.Nodes[ci];
                    (double x, double y) = CardCentre(v);
                    if (!Screen.Canvas.Contains(x, y) || Screen.Obscured(x, y)) continue;
                    if (view.Available && !view.IsTarget && Screen.PendingTarget != ci) { pick = v; break; }
                }
                if (pick < 0) return (GateResult.NotOffered, "no other available card in view");
                int node = Screen.Graph.Vertices[pick].ContentIndex;
                (double px, double py) = CardCentre(pick);
                h.Click(px, py);
                return Expect(Screen.Selected == node && S.QueuedResearchChoice() == Screen.Content.Nodes[node].Key.Value,
                    "selected " + Screen.Selected + ", queued choice " + S.QueuedResearchChoice());
            });
        }

        private (double X, double Y) CardCentre(int v)
        {
            PlacedVertex p = Screen.Layout.Placed[v];
            RectD c = Screen.Canvas;
            return (Screen.Camera.ToScreenX(p.CenterX, c.X), Screen.Camera.ToScreenY(p.CenterY, c.Y));
        }

        private void TreeNavigation(TreeTab tab)
        {
            Check("research", "wheel scroll (" + tab + ")", () =>
            {
                Screen.ResetView();
                h.Idle(30);
                RectD c = Screen.Canvas;
                double before = Screen.Camera.TargetPanY;
                h.Wheel(c.X + c.W * 0.4, c.Y + c.H * 0.5, -3);
                return Expect(Screen.Camera.TargetPanY > before || before >= MaxPan(), "pan " + before + " -> " + Screen.Camera.TargetPanY);
            });
            Check("research", "drag pan (" + tab + ")", () =>
            {
                RectD c = Screen.Canvas;
                double before = Screen.Camera.TargetPanY;
                h.Drag(c.X + c.W * 0.4, c.Y + c.H * 0.7, c.X + c.W * 0.4, c.Y + c.H * 0.3);
                return Expect(Screen.Camera.TargetPanY != before || before >= MaxPan(), "pan unchanged");
            });
            Check("research", "keyboard scroll (" + tab + ")", () =>
            {
                double before = Screen.Camera.TargetPanY;
                h.Hold(Keys.S, 10);
                return Expect(Screen.Camera.TargetPanY != before || before >= MaxPan(), "pan unchanged");
            });
            Check("research", "ctrl+wheel zoom (" + tab + ")", () =>
            {
                RectD c = Screen.Canvas;
                double before = Screen.Camera.TargetZoom;
                h.WheelWith(c.X + c.W * 0.4, c.Y + c.H * 0.5, -2, Keys.LeftControl);
                bool zoomed = Screen.Camera.TargetZoom < before;
                h.WheelWith(c.X + c.W * 0.4, c.Y + c.H * 0.5, 2, Keys.LeftControl);
                return Expect(zoomed, "zoom " + before + " -> " + Screen.Camera.TargetZoom);
            });
            Check("research", "lane collapse / expand (" + tab + ")", () =>
            {
                if (!ClickHit(HitKind.LaneToggle, 0)) return (GateResult.Fail, "lane chip not drawn");
                bool collapsed = Screen.IsLaneCollapsed(0);
                ClickHit(HitKind.LaneToggle, 0);
                return Expect(collapsed && !Screen.IsLaneCollapsed(0), "lane 0 did not toggle");
            });
            Check("research", "fit (" + tab + ")", () =>
            {
                if (!ClickHit(HitKind.Fit)) return (GateResult.Fail, "not drawn");
                h.Idle(20);
                return Expect(Screen.Camera.TargetZoom < 1.0 || Screen.Layout.Height <= Screen.Canvas.H, "zoom " + Screen.Camera.TargetZoom);
            });
            Check("research", "scroll through the whole tree (" + tab + ")", () =>
            {
                Screen.ResetView();
                Screen.Camera.Set(1.0, 0, 0);
                for (int k = 0; k < 400 && Screen.Camera.TargetPanY < MaxPan() - 1; k++)
                {
                    Screen.ScrollBy(0, 360);
                    h.Idle(1);
                }
                return Expect(Screen.Camera.TargetPanY >= MaxPan() - 1, "did not reach the bottom");
            });
            Check("research", "lagging-branch reset (" + tab + ")", () =>
            {
                if (!ClickHit(HitKind.Reset)) return (GateResult.Fail, "not drawn");
                h.Idle(30);
                return Expect(Math.Abs(Screen.Camera.TargetZoom - 1.0) < 1e-9, "zoom " + Screen.Camera.TargetZoom);
            });
            Check("research", "minimap click (" + tab + ")", () =>
            {
                if (FindHit(HitKind.Minimap) is not { } m) return (GateResult.Fail, "minimap not drawn");
                double before = Screen.Camera.TargetPanY;
                h.Click(m.Rect.CenterX, m.Rect.Y + m.Rect.H * 0.85);
                return Expect(Screen.Camera.TargetPanY != before, "pan unchanged");
            });
            Check("research", "minimap toggle (" + tab + ")", () =>
            {
                if (!ClickHit(HitKind.MinimapToggle)) return (GateResult.Fail, "not drawn");
                bool hidden = !Screen.MinimapVisible;
                ClickHit(HitKind.MinimapToggle);
                return Expect(hidden && Screen.MinimapVisible, "did not toggle");
            });
        }

        private double MaxPan()
        {
            RectD c = Screen.Canvas;
            return Math.Max(0, Screen.Layout.Height - c.H / Screen.Camera.TargetZoom);
        }

        // -------------------------------------------------------------- Age

        /// <summary>P6 (coverage): the Age panel's research-next line — under an unmet research milestone of the next Age,
        /// it opens the tree at that node. The panel scrolls, so the line is scrolled into view; run again after the End
        /// Turns (an Age advance brings the next Age's milestones).</summary>
        private void AgeResearchNextRow()
        {
            Check("age", "Age panel: the research-next line (opens the tree at its node)", () =>
            {
                // P6 (coverage): the line under an unmet research milestone; the panel scrolls, so scroll it into view.
                CloseEverything();
                if (Ui.Controls.Find("band-age") is null) return (GateResult.NotOffered, "no Age content");
                h.ClickControl("band-age");
                AgeHitRegion? line = null;
                for (int step = 0; step < 40 && (line = AgeHit(Sim.Ui.Ages.AgeHit.FocusNode)) is null; step++)
                {
                    RectD panel = Ui.AgePanelBounds;
                    h.Wheel(panel.CenterX, panel.CenterY, -1);
                }
                if (line is not { } r) return (GateResult.NotOffered, "no research-next line: no unmet research milestone names its next node (scrolled to the panel's end)");
                int q = Queued;
                h.Click(r.Rect.CenterX, r.Rect.CenterY);
                int want = S.Config.Research!.IndexOf(new ResearchNodeId(r.Arg));
                bool ok = Ui.ProgressionOpen && Ui.Progression!.Selected == want && Queued == q;
                string detail = "tree " + Ui.ProgressionOpen + ", selected " + Ui.Progression?.Selected + " (want " + want + "), queued +" + (Queued - q);
                CloseEverything();
                return Expect(ok, detail);
            });
            AgePanelHome();
        }

        /// <summary>P6: opens the docked Age panel and scrolls it back to its top (the wheel over it), then closes it —
        /// a check that scrolled it must not leave the next one (the ADVANCE AGE button sits at its top) a scrolled panel.</summary>
        private void AgePanelHome()
        {
            CloseEverything();
            if (Ui.Controls.Find("band-age") is null) return;
            h.ClickControl("band-age");
            RectD panel = Ui.AgePanelBounds;
            for (int k = 0; k < 20; k++) h.Wheel(panel.CenterX, panel.CenterY, 3);
            CloseEverything();
        }

        private AgeHitRegion? AgeHit(AgeHit kind)
        {
            foreach (AgeHitRegion r in Ui.Age.Hits) if (r.Kind == kind) return r;
            return null;
        }

        private void AgeSurfaces()
        {
            Check("age", "band-age (opens the Age panel)", () =>
            {
                if (Ui.Controls.Find("band-age") is null) return (GateResult.NotOffered, "no Age content");
                h.ClickControl("band-age");
                return Expect(Ui.AgePanelOpen, "panel not open");
            });
            Check("age", "Age panel: open Knowledge", () =>
            {
                if (AgeHit(Sim.Ui.Ages.AgeHit.OpenKnowledge) is not { } k) return (GateResult.Fail, "not drawn");
                h.Click(k.Rect.CenterX, k.Rect.CenterY);
                bool ok = Ui.ProgressionOpen;
                CloseEverything();
                return Expect(ok, "the trees did not open");
            });
            AgeResearchNextRow();
            Check("age", "Age panel: close button", () =>
            {
                h.ClickControl("band-age");
                if (AgeHit(Sim.Ui.Ages.AgeHit.ClosePanel) is not { } c) return (GateResult.Fail, "not drawn");
                h.Click(c.Rect.CenterX, c.Rect.CenterY);
                return Expect(!Ui.AgePanelOpen, "still open");
            });
            Check("age", "Escape (closes the Age panel, does not exit)", () =>
            {
                h.ClickControl("band-age");
                int exits = h.ExitRequests;
                h.Key(Keys.Escape);
                return Expect(!Ui.AgePanelOpen && h.ExitRequests == exits, "panel " + Ui.AgePanelOpen + ", exit requests +" + (h.ExitRequests - exits));
            });
            Check("age", "band-age (again: closes)", () =>
            {
                h.ClickControl("band-age");
                h.ClickControl("band-age");
                return Expect(!Ui.AgePanelOpen, "still open");
            });
            AdvanceFlow();
        }

        /// <summary>The ADVANCE AGE flow: offered only when the player is eligible; otherwise the panel shows the
        /// unmet milestones and there is no advance button.</summary>
        private void AdvanceFlow()
        {
            AgeContent? ages = S.Config.Ages;
            bool eligible = ages is not null && AgeQuery.IsEligible(S.World, ages, Me) && S.PendingPlayerAdvance is null;
            if (!eligible)
            {
                Row("age", "ADVANCE AGE (flow)", GateResult.NotOffered, "not eligible: the panel lists the unmet milestones and draws no advance button");
                return;
            }
            int ageBefore = AgeQuery.CurrentAge(S.World, ages!, Me);
            Check("age", "ADVANCE AGE (opens the flow)", () =>
            {
                h.ClickControl("band-age");
                if (AgeHit(Sim.Ui.Ages.AgeHit.OpenAdvance) is not { } a) return (GateResult.Fail, "eligible but no advance button");
                h.Click(a.Rect.CenterX, a.Rect.CenterY);
                return Expect(Ui.Age.FlowOpen, "flow not open");
            });
            Check("age", "flow is modal (the chrome beneath does not answer)", () =>
            {
                long turn = S.World.Clock.Turn;
                if (Ui.Controls.Find("end-turn") is { } end) h.Click(end.CenterX, end.CenterY);
                if (Ui.Controls.Find("nav:Policy") is { } nav) h.Click(nav.CenterX, nav.CenterY);
                return Expect(S.World.Clock.Turn == turn && Ui.OpenSection == Section.None && Ui.Age.FlowOpen,
                    "turn " + turn + " -> " + S.World.Clock.Turn + ", section " + Ui.OpenSection + ", flow " + Ui.Age.FlowOpen);
            });
            Check("age", "flow: CONFIRM before a surge (disabled)", () =>
            {
                // No surge chosen: CONFIRM is drawn dim with "Choose a surge emphasis to confirm." and is not a hit.
                return Expect(AgeHit(Sim.Ui.Ages.AgeHit.Confirm) is null || Ui.Age.SelectedSurge != 0, "confirm live without a surge");
            });
            Check("age", "flow: NOT NOW", () =>
            {
                if (AgeHit(Sim.Ui.Ages.AgeHit.Cancel) is not { } c) return (GateResult.Fail, "not drawn");
                h.Click(c.Rect.CenterX, c.Rect.CenterY);
                bool closed = !Ui.Age.FlowOpen;
                return Expect(closed, "flow still open");
            });
            Check("age", "flow: surge + CONFIRM ADVANCE", () =>
            {
                CloseEverything();
                h.ClickControl("band-age");
                if (AgeHit(Sim.Ui.Ages.AgeHit.OpenAdvance) is { } a) h.Click(a.Rect.CenterX, a.Rect.CenterY);
                if (AgeHit(Sim.Ui.Ages.AgeHit.Surge) is not { } surge) return (GateResult.Fail, "no surge card");
                h.Click(surge.Rect.CenterX, surge.Rect.CenterY);
                if (AgeHit(Sim.Ui.Ages.AgeHit.Confirm) is not { } confirm) return (GateResult.Fail, "confirm not live after a surge");
                h.Click(confirm.Rect.CenterX, confirm.Rect.CenterY);
                return Expect(S.PendingPlayerAdvance is not null, "no AdvanceAge order");
            });
            Check("age", "End Turn -> new Age + transition toast", () =>
            {
                CloseEverything();
                h.ClickControl("end-turn");
                int after = AgeQuery.CurrentAge(S.World, ages!, Me);
                return Expect(after == ageBefore + 1 && Ui.Age.ToastVisible, "age " + after + ", toast " + Ui.Age.ToastVisible);
            });
            Check("age", "transition toast fades", () =>
            {
                h.Idle(60 * 7, 1.0 / 60.0);
                return Expect(!Ui.Age.ToastVisible, "toast still visible after 7 s");
            });
        }

        // -------------------------------------------------------------- Policy: the action surface

        private ActionHit? Find(ActionHitKind kind, int a = int.MinValue, int b = int.MinValue)
        {
            foreach (ActionHit x in Ui.Actions.Hits)
                if (x.Kind == kind && (a == int.MinValue || x.A == a) && (b == int.MinValue || x.B == b)) return x;
            return null;
        }

        /// <summary>Scrolls the POLICY panel until the hit is inside its visible body, then clicks it.</summary>
        private bool ClickAction(ActionHitKind kind, int a = int.MinValue, int b = int.MinValue)
        {
            PanelRect p = Ui.ContextRect;
            // The panel's scrolling body ends a window padding above the panel's edge (12 px × the UI scale): a point
            // in that band is clipped, never a click on the surface. (M5 polish UR-3: the taller controls made a hit's
            // centre land in it — the gate clicked the clipped band and saw no order.)
            double top = ChromeGeometry.ContentTop(ChromeGeometry.Context, ImGuiNET.ImGui.GetFrameHeight()),
                bottom = p.Y + p.Height - Sim.Ui.Art.UiTheme.WindowPaddingPx.Y * Ui.Scale - 6;
            for (int k = 0; k < 60; k++)
            {
                if (Find(kind, a, b) is not { } hit) return false;
                if (hit.Rect.CenterY >= top + 2 && hit.Rect.CenterY <= bottom)
                {
                    h.Click(hit.Rect.CenterX, hit.Rect.CenterY);
                    return true;
                }
                h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, hit.Rect.CenterY < top + 2 ? 1 : -1);
            }
            return false;
        }

        private void OpenPolicy()
        {
            if (Ui.OpenSection != Section.Policy) h.ClickControl("nav:Policy");
            // back to the top of the panel
            for (int k = 0; k < 30; k++) h.Wheel(Ui.ContextRect.X + 100, Ui.ContextRect.Y + 300, 3);
        }

        private void Policy()
        {
            OpenPolicy();
            ActionSurfaceModel? model = Ui.Actions.Model;
            if (model is null) { Row("policy", "action surface", GateResult.Fail, "no model"); return; }

            // Labour: one slot per sector, the era's control (pebbles / counters / notches / slider).
            if (model.Labour is { } labour)
            {
                foreach (LabourEntry e in labour.Entries)
                {
                    int sector = e.Sector;
                    Check("policy: labour", "labour control: " + Sectors(sector), () =>
                    {
                        int before = Ui.Actions.Draft[sector];
                        bool clicked = model.Control.IsSlider
                            ? ClickAction(ActionHitKind.LabourPlus, sector) || ClickAction(ActionHitKind.LabourMinus, sector)
                            : ClickAction(ActionHitKind.LabourSlot, sector, Math.Max(0, before == 0 ? 0 : before - 1));
                        if (!clicked) return (GateResult.Fail, "control not drawn");
                        return Expect(Ui.Actions.Draft[sector] != before, "draft unchanged at " + before);
                    });
                }
                Check("policy: labour", "Undo", () =>
                {
                    if (!Ui.Actions.LabourChanged) return (GateResult.NotOffered, "nothing to undo");
                    if (!ClickAction(ActionHitKind.LabourReset)) return (GateResult.Fail, "draft changed but no Undo");
                    return Expect(!Ui.Actions.LabourChanged, "draft still changed");
                });
                Check("policy: labour", "Apply / Set them to work", () =>
                {
                    if (!Ui.Actions.LabourChanged)
                    {
                        int s0 = labour.Entries[0].Sector;
                        ClickAction(model.Control.IsSlider ? ActionHitKind.LabourPlus : ActionHitKind.LabourSlot, s0,
                            model.Control.IsSlider ? int.MinValue : Math.Max(0, Ui.Actions.Draft[s0] - 1));
                    }
                    if (!Ui.Actions.LabourChanged) return (GateResult.Fail, "could not change the draft");
                    int q = Queued;
                    if (!ClickAction(ActionHitKind.LabourApply)) return (GateResult.Fail, "no Apply button for a changed draft");
                    return Expect(Queued > q, "no labour order");
                });
                Check("policy: labour", "settlement « »", () =>
                {
                    if (Find(ActionHitKind.NextSettlement) is null) return (GateResult.NotOffered, "one controlled settlement");
                    int before = Ui.SelectedSettlement;
                    ClickAction(ActionHitKind.NextSettlement);
                    bool moved = Ui.SelectedSettlement != before;
                    ClickAction(ActionHitKind.PrevSettlement);
                    return Expect(moved && Ui.SelectedSettlement == before, "selection did not cycle");
                });
            }
            else Row("policy: labour", "labour control", GateResult.NotOffered, "the selected settlement is not the player's");

            // Learning.
            Check("policy: learning", "Choose / Open the trees", () =>
            {
                OpenPolicy();
                if (!ClickAction(ActionHitKind.ResearchOpen)) return (GateResult.Fail, "not drawn");
                bool ok = Ui.ProgressionOpen;
                h.Key(Keys.Escape);
                OpenPolicy();
                return Expect(ok, "the trees did not open");
            });
            Check("policy: learning", "Stop (clear target)", () =>
            {
                OpenPolicy();
                if (Find(ActionHitKind.ResearchClear) is null)
                    return (GateResult.NotOffered, "no target in force (the block reads \"idle - choose what to learn\")");
                if (!ClickAction(ActionHitKind.ResearchClear)) return (GateResult.Fail, "not reachable");
                return Expect(S.QueuedResearchChoice() == -1, "no clear order");
            });

            // The Age.
            Check("policy: age", "Advance...", () =>
            {
                OpenPolicy();
                if (Find(ActionHitKind.AgeAdvance) is null) return (GateResult.NotOffered, "not eligible for the next Age (no block)");
                ClickAction(ActionHitKind.AgeAdvance);
                bool ok = Ui.Age.FlowOpen;
                CloseEverything();
                return Expect(ok, "the advance flow did not open");
            });

            // Construction: every Build button.
            OpenPolicy();
            if (Ui.Actions.Model?.Construction is { } construction)
            {
                foreach (ProjectEntry p in construction.Projects)
                {
                    ProjectEntry project = p;
                    Check("policy: building", "Build: " + project.Name, () =>
                    {
                        OpenPolicy();
                        int q = Queued;
                        if (!ClickAction(ActionHitKind.Build, construction.Settlement.Value, project.ProjectId)) return (GateResult.Fail, "not drawn");
                        if (Queued > q) return (GateResult.Pass, project.Blocker is { } b ? "queued; waits: " + b : "queued");
                        return (GateResult.Fail, "SILENT: the order was refused" + (project.Blocker is { } b2 ? " (blocker shown: " + b2 + ")" : ""));
                    });
                }
            }
            else Row("policy: building", "Build", GateResult.NotOffered, "no construction block for the selection");

            // Roads.
            OpenPolicy();
            if (Ui.Actions.Model?.Roads is { } roads)
            {
                Check("policy: roads", "road share control", () =>
                {
                    int before = Ui.Actions.RoadDraft;
                    if (roads.OrderedPercent is not null) return (GateResult.NotOffered, "already ordered this turn (the block says so)");
                    bool clicked = ClickAction(ActionHitKind.RoadSlot, int.MinValue, 0) || ClickAction(ActionHitKind.RoadTrack);
                    if (!clicked) return (GateResult.Fail, "not drawn");
                    return Expect(Ui.Actions.RoadDraft != before || before == model.Control.PercentPerUnit, "draft unchanged");
                });
                Check("policy: roads", "Develop", () =>
                {
                    OpenPolicy();
                    if (Find(ActionHitKind.RoadApply) is null) return (GateResult.NotOffered, "already ordered this turn (the block says so)");
                    int q = Queued;
                    ClickAction(ActionHitKind.RoadApply);
                    if (Queued > q) return (GateResult.Pass, "");
                    return (GateResult.Fail, "SILENT: the order was refused" + (roads.Blocker is { } b ? " (blocker shown: " + b + ")" : ""));
                });
            }
            else Row("policy: roads", "Develop roads", GateResult.NotOffered, "no road class known yet (the block appears with one)");

            // The tax edict.
            OpenPolicy();
            if (Ui.Actions.Model?.Governance is not null)
            {
                Check("policy: tax", "levy control", () =>
                {
                    int before = Ui.Actions.TaxDraft;
                    bool clicked = model.Control.IsSlider ? ClickAction(ActionHitKind.TaxTrack) : ClickAction(ActionHitKind.TaxSlot, int.MinValue, 1);
                    if (!clicked) return (GateResult.Fail, "not drawn");
                    return Expect(Ui.Actions.TaxDraft != before, "draft unchanged at " + before);
                });
                Check("policy: tax", "Declare", () =>
                {
                    OpenPolicy();
                    if (Find(ActionHitKind.TaxApply) is null) return (GateResult.Fail, "the draft differs but there is no Declare button");
                    int q = Queued;
                    ClickAction(ActionHitKind.TaxApply);
                    return Expect(Queued > q, "SILENT: no tax order");
                });
            }
            else Row("policy: tax", "tax edict", GateResult.NotOffered,
                "not available: the block is absent until Taxation is learned (and the Age allows it)");

            Check("policy", "labour-record", () =>
            {
                OpenPolicy();
                for (int k = 0; k < 30 && Ui.Controls.Find("labour-record") is { } c && c.Y1 > Ui.ContextRect.Y + Ui.ContextRect.Height - 10; k++)
                    h.Wheel(Ui.ContextRect.X + 100, Ui.ContextRect.Y + 300, -2);
                if (Ui.Controls.Find("labour-record") is null) return (GateResult.NotOffered, "no settlement selected");
                bool before = Ui.PolicyRecordOpen;
                h.ClickControl("labour-record");
                bool flipped = Ui.PolicyRecordOpen != before;
                h.ClickControl("labour-record");
                return Expect(flipped, "toggle did not flip");
            });
        }

        private static string Sectors(int s) => s switch { 0 => "Farming", 1 => "Herding", 2 => "Extraction", 3 => "Crafting", _ => "Construction" };

        // -------------------------------------------------------------- developer surfaces

        private void Developer()
        {
            Check("developer", "F12 (on)", () => { h.Key(Keys.F12); return Expect(Ui.Developer && Ui.Controls.Find("nav:Developer") is not null, "no DEV tab"); });
            Check("developer", "nav:Developer", () => { h.ClickControl("nav:Developer"); return Expect(Ui.OpenSection == Section.Developer, "section " + Ui.OpenSection); });
            foreach (Section tab in GameSections.DeveloperTabs)
            {
                string id = "dev-" + tab;
                Check("developer", id, () =>
                {
                    if (!h.ClickControl(id)) return (GateResult.Fail, "not drawn");
                    bool ok = Ui.DeveloperTab == tab;
                    if (tab == Section.Settlement) SettlementTabs();
                    if (tab == Section.Turn) TurnAudit();
                    return Expect(ok, "tab " + Ui.DeveloperTab);
                });
            }
            Check("developer", "per-turn-table (POLICY labour record)", () =>
            {
                h.ClickControl("nav:Policy");
                for (int k = 0; k < 40 && Ui.Controls.Find("labour-record") is { } c && c.Y1 > Ui.ContextRect.Y + Ui.ContextRect.Height - 10; k++)
                    h.Wheel(Ui.ContextRect.X + 100, Ui.ContextRect.Y + 300, -2);
                if (Ui.Controls.Find("labour-record") is null) return (GateResult.NotOffered, "no settlement selected");
                if (!Ui.PolicyRecordOpen) h.ClickControl("labour-record");
                for (int k = 0; k < 40 && Ui.Controls.Find("per-turn-table") is { } c && c.Y1 > Ui.ContextRect.Y + Ui.ContextRect.Height - 10; k++)
                    h.Wheel(Ui.ContextRect.X + 100, Ui.ContextRect.Y + 300, -2);
                if (Ui.Controls.Find("per-turn-table") is null) return (GateResult.Fail, "not drawn with the record open in developer mode");
                bool before = Ui.PolicyShowStates;
                h.ClickControl("per-turn-table");
                bool flipped = Ui.PolicyShowStates != before;
                h.ClickControl("nav:Developer");
                return Expect(flipped, "toggle did not flip");
            });
            Check("developer", "F12 (off)", () => { h.Key(Keys.F12); return Expect(!Ui.Developer && Ui.OpenSection != Section.Developer, "still on"); });
        }

        private void SettlementTabs()
        {
            foreach (SettlementTab tab in GameSections.Tabs)
            {
                string id = "tab-" + tab;
                Check("developer: settlement", id, () =>
                {
                    if (!h.ClickControl(id)) return (GateResult.Fail, "not drawn");
                    bool ok = Ui.SettlementTabOpen == tab;
                    if (tab == SettlementTab.Economy && Ui.Controls.Last.Count > 0)
                    {
                        foreach (UiControl c in Ui.Controls.Last)
                            if (c.Name.StartsWith("good", StringComparison.Ordinal) && c.Y1 < Ui.ContextRect.Y + Ui.ContextRect.Height - 10)
                            {
                                (double gx, double gy) = h.Aim(c);
                                h.Click(gx, gy);
                                if (Ui.SelectedGood.ToString(CultureInfo.InvariantCulture) != c.Name[4..]) ok = false;
                                break;
                            }
                    }
                    if (tab == SettlementTab.Grievance)
                    {
                        bool factors = Ui.ShowHappinessFactors;
                        if (h.ClickControl("happiness") && Ui.ShowHappinessFactors == factors) ok = false;
                        if (!Ui.ShowHappinessFactors) h.ClickControl("happiness");
                        foreach (UiControl c in Ui.Controls.Last)
                            if (c.Name.StartsWith("factor-", StringComparison.Ordinal) && c.Y1 < Ui.ContextRect.Y + Ui.ContextRect.Height - 10)
                            {
                                (double fx, double fy) = h.Aim(c);
                                h.Click(fx, fy);
                                if (Ui.ExpandedFactor.ToString(CultureInfo.InvariantCulture) != c.Name[7..]) ok = false;
                                break;
                            }
                        foreach (UiControl c in Ui.Controls.Last)
                            if (c.Name.StartsWith("need-", StringComparison.Ordinal) && c.Y1 < Ui.ContextRect.Y + Ui.ContextRect.Height - 10)
                            {
                                (double nx, double ny) = h.Aim(c);
                                h.Click(nx, ny);
                                if (Ui.ExpandedNeed.Need < 0) ok = false;
                                break;
                            }
                        foreach (UiControl c in Ui.Controls.Last)
                            if (c.Name.StartsWith("lever-", StringComparison.Ordinal) && c.Y1 < Ui.ContextRect.Y + Ui.ContextRect.Height - 10)
                            { (double lx, double ly) = h.Aim(c); h.Click(lx, ly); if (Ui.OpenSection != Section.Policy) ok = false; break; }
                    }
                    if (Ui.OpenSection != Section.Developer) h.ClickControl("nav:Developer");
                    return Expect(ok, "tab " + Ui.SettlementTabOpen + " (or a control inside it did nothing)");
                });
            }
            GrievanceControls();
        }

        /// <summary>P6 (coverage): the grievance account's rows one by one, each scrolled into view — a contributor
        /// unfolds its chain, and the lever under a factor and under a contributor opens POLICY. (The tab row above
        /// clicks only what its first screen shows.)</summary>
        private void GrievanceControls()
        {
            void OpenGrievance()
            {
                if (Ui.OpenSection != Section.Developer) h.ClickControl("nav:Developer");
                if (Ui.DeveloperTab != Section.Settlement) h.ClickControl("dev-Settlement");
                if (Ui.SettlementTabOpen != SettlementTab.Grievance) h.ClickControl("tab-Grievance");
            }
            const string noAccount = "the grievance account needs the previous turn's world (the tab says so)";
            Check("developer: settlement", "grievance contributor (unfolds its chain)", () =>
            {
                OpenGrievance();
                if (ScrollToControl("need-") is not { } ctl) return (GateResult.NotOffered, noAccount);
                (int, int) before = Ui.ExpandedNeed;
                (double x, double y) = h.Aim(ctl);
                h.Click(x, y);
                return Expect(Ui.ExpandedNeed != before, ctl.Name + " did not unfold or fold");
            });
            foreach ((string lever, string unfold, string what) in new[] { ("lever-factor-", "factor-", "a happiness factor"), ("lever-need-", "need-", "a contributor") })
            {
                string l = lever, u = unfold;
                Check("developer: settlement", "lever under " + what + " (opens POLICY)", () =>
                {
                    OpenGrievance();
                    if (u == "factor-" && !Ui.ShowHappinessFactors && ScrollToControl("happiness") is { } hap)
                    {
                        (double hx, double hy) = h.Aim(hap);
                        h.Click(hx, hy);
                    }
                    bool open = u == "factor-" ? Ui.ExpandedFactor >= 0 : Ui.ExpandedNeed.Need >= 0;
                    if (!open && ScrollToControl(u) is { } row)
                    {
                        (double rx, double ry) = h.Aim(row);
                        h.Click(rx, ry);
                    }
                    if (ScrollToControl(l) is not { } ctl)
                        return (GateResult.NotOffered, "no lever under " + what + " in this state (" + noAccount + ", or its chain ends at a condition with no lever)");
                    (double x, double y) = h.Aim(ctl);
                    h.Click(x, y);
                    Section opened = Ui.OpenSection;
                    h.ClickControl("nav:Developer");
                    return Expect(opened == Section.Policy, ctl.Name + " opened " + opened);
                });
            }
            OpenGrievance();
            ScrollContextHome();   // the tab rows are where the next checks click
        }

        private void TurnAudit()
        {
            int audit = 0;
            foreach (UiControl c in Ui.Controls.Last)
            {
                if (!c.Name.StartsWith("audit-", StringComparison.Ordinal)) continue;
                int index = audit++;
                if (c.Y1 > Ui.ContextRect.Y + Ui.ContextRect.Height - 10 || index > 0) continue;
                UiControl ctl = c;
                Check("developer: turn", "audit line (unfolds its account)", () =>
                {
                    bool before = Ui.AuditExpanded[index];
                    (double ax, double ay) = h.Aim(ctl);
                    h.Click(ax, ay);
                    return Expect(Ui.AuditExpanded[index] != before, ctl.Name + " did not unfold");
                });
            }
            if (audit == 0) Row("developer: turn", "audit line (unfolds its account)", GateResult.NotOffered, "no turn played yet (the tab says so)");
            // P6 (coverage): the first WHERE row is scrolled into view (it sat below the fold in a narrow window, and the
            // row was then silently skipped); with none drawn the row says why.
            LookCheck("developer: turn", "where row (selects and centres)", () =>
            {
                if (ScrollToControl("where-") is not { } ctl)
                    return (GateResult.NotOffered, audit == 0 ? "no turn played yet (the tab says so)" : "the tab lists no WHERE row");
                int id = int.Parse(ctl.Name[(ctl.Name.LastIndexOf('-') + 1)..], CultureInfo.InvariantCulture);
                (double ax, double ay) = h.Aim(ctl);
                h.Click(ax, ay);
                return Expect(Ui.SelectedSettlement == id, "selected " + Ui.SelectedSettlement);
            });
            if (audit > 0) _devHadTurn = true;
            // P6 (coverage): the deficit WHERE rows — the row above clicks the first 'where-' row, a population one.
            LookCheck("developer: turn", "where row: largest deficit (selects and centres)", () =>
            {
                if (ScrollToControl("where-def-") is not { } ctl)
                    return (GateResult.NotOffered, audit == 0 ? "no turn played yet (the tab says so)" : "no settlement ran a deficit last turn (the tab lists none)");
                int id = int.Parse(ctl.Name[(ctl.Name.LastIndexOf('-') + 1)..], CultureInfo.InvariantCulture);
                (double ax, double ay) = h.Aim(ctl);
                h.Click(ax, ay);
                return Expect(Ui.SelectedSettlement == id, "selected " + Ui.SelectedSettlement);
            });
            ScrollContextHome();   // the tab row above is where the next checks click
        }

        /// <summary>P6: scrolls the context panel's body (the wheel over it, Shift + wheel sideways — as a player does)
        /// until the first control whose name starts with <paramref name="prefix"/> lies wholly inside the visible body;
        /// null when the panel draws no such control (or it cannot be brought into view).</summary>
        private UiControl? ScrollToControl(string prefix)
        {
            PanelRect p = Ui.ContextRect;
            double top = ChromeGeometry.ContentTop(new ChromeElement(p, RulePlacement.UnderHeaderRow), ImGuiNET.ImGui.GetFrameHeight(), Ui.Scale) + 2,
                bottom = p.Y + p.Height - Sim.Ui.Art.UiTheme.WindowPaddingPx.Y * Ui.Scale - 6,
                left = p.X + 4, right = p.X + p.Width - 20;
            for (int k = 0; k < 80; k++)
            {
                UiControl? found = null;
                foreach (UiControl c in Ui.Controls.Last)
                    if (c.Name.StartsWith(prefix, StringComparison.Ordinal)) { found = c; break; }
                if (found is not { } ctl) return null;
                if (ctl.Y0 < top || ctl.Y1 > bottom) h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, ctl.Y0 < top ? 1 : -1);
                // Sideways only when too little of it shows to aim at (a full-width row runs past the edge by design:
                // UiFrameHarness.Aim clicks near its start).
                else if (ctl.X0 > right - 24) h.WheelWith(p.X + p.Width / 2, p.Y + p.Height / 2, -1, Keys.LeftShift);
                else if (ctl.X1 < left + 24) h.WheelWith(p.X + p.Width / 2, p.Y + p.Height / 2, 1, Keys.LeftShift);
                else return ctl;
            }
            return null;
        }

        /// <summary>P6: scrolls the context panel's body back to its top-left corner.</summary>
        private void ScrollContextHome()
        {
            PanelRect p = Ui.ContextRect;
            for (int k = 0; k < 30; k++) h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, 3);
            for (int k = 0; k < 10; k++) h.WheelWith(p.X + p.Width / 2, p.Y + p.Height / 2, 3, Keys.LeftShift);
        }

        // -------------------------------------------------------------- keys

        private void Keyboard()
        {
            IReadOnlyList<Section> roster = GameSections.Roster(false);
            for (int d = 1; d <= roster.Count; d++)
            {
                int digit = d;
                Check("keys", "digit " + digit.ToString(CultureInfo.InvariantCulture), () =>
                {
                    CloseEverything();
                    h.Key(Keys.D0 + digit);
                    return Expect(Ui.OpenSection == roster[digit - 1], "section " + Ui.OpenSection);
                });
            }
            Check("keys", "Escape (closes a panel)", () =>
            {
                h.Key(Keys.D1);
                h.Key(Keys.Escape);
                return Expect(Ui.OpenSection == Section.None, "section " + Ui.OpenSection);
            });
            Check("keys", "Escape with nothing open (exit request)", () =>
            {
                CloseEverything();
                int before = h.ExitRequests;
                h.Key(Keys.Escape);
                return Expect(h.ExitRequests == before + 1, "no exit request");
            });
            Check("keys", "Tab (next settlement)", () =>
            {
                int before = Ui.SelectedSettlement;
                h.Key(Keys.Tab);
                return Expect(Ui.SelectedSettlement != before || Ui.World.Settlements.Count < 2, "selection unchanged");
            });
            WasdRow();
        }

        /// <summary>The W A S D camera row (a camera row: the world hash and the queued orders must not change).</summary>
        private void WasdRow()
        {
            LookCheck("keys", "W A S D (pan)", () =>
            {
                // All four keys, both directions: a camera already clamped at one map edge (the world's own
                // geometry decides where the selected settlement sits — after ADR-035 the age-advance rig's camera
                // starts at the bottom clamp at zoom 1) cannot move further that way, which is not a dead control.
                double x = Ui.Camera.CenterX, y = Ui.Camera.CenterY;
                h.Hold(Keys.D, 20);
                h.Hold(Keys.S, 20);
                bool forward = Ui.Camera.CenterX != x || Ui.Camera.CenterY != y;
                double x2 = Ui.Camera.CenterX, y2 = Ui.Camera.CenterY;
                h.Hold(Keys.A, 20);
                h.Hold(Keys.W, 20);
                bool back = Ui.Camera.CenterX != x2 || Ui.Camera.CenterY != y2;
                return Expect(forward || back, "camera did not move (D/S nor A/W)");
            });
        }

        // -------------------------------------------------------------- the map

        /// <summary>A point on the map just below its centre (P1: the MAP RECT's centre — the clear map the chrome leaves).</summary>
        private (double X, double Y) Centre() => (Ui.MapRect.CenterX, Ui.MapRect.CenterY + 30);

        /// <summary>How far (screen px) a settlement's marker is from the map rect's centre (P1: where CenterOn puts it).</summary>
        private double FromCentre(SettlementRow row)
        {
            LineGeometry.Vertex p = OverlayMeshes.SettlementPosition(row, Ui.World.Terrain!.Size);
            RectD map = Ui.MapRect;
            (double sx, double sy) = Ui.Camera.WorldToScreen(p.X, p.Y, map);
            return Math.Sqrt((sx - map.CenterX) * (sx - map.CenterX) + (sy - map.CenterY) * (sy - map.CenterY));
        }

        /// <summary>P1: whether the camera's clamp lets <paramref name="row"/> sit exactly at the map rect's centre at the
        /// current zoom (it is at least half the rect's view, in world px, from each world edge on both axes).</summary>
        private bool ClampAllowsCentre(SettlementRow row)
        {
            LineGeometry.Vertex p = OverlayMeshes.SettlementPosition(row, Ui.World.Terrain!.Size);
            double size = Ui.Camera.WorldSize, hw = Ui.MapRect.W / 2.0 / Ui.Camera.Zoom, hh = Ui.MapRect.H / 2.0 / Ui.Camera.Zoom;
            return 2 * hw < size && 2 * hh < size && p.X >= hw && p.X <= size - hw && p.Y >= hh && p.Y <= size - hh;
        }

        /// <summary>The camera rows of <see cref="Keyboard"/> and <see cref="Map"/>, in their sweep order.</summary>
        public void Camera()
        {
            WasdRow();
            SelectRows();
            CameraLookRows();
            PanelFoldRows();
            LegendRows();
        }

        private void Map()
        {
            SelectRows();
            Units();
            CameraLookRows();
            PanelFoldRows();
            LegendRows();
            KnowledgeRows();
        }

        /// <summary>P7: the settlements that exist and the player does not know (none while the knowledge is all-known —
        /// every state but the hidden-rival rig, which therefore alone draws the knowledge rows).</summary>
        private List<int> HiddenSettlements()
        {
            var hidden = new List<int>();
            for (int i = 0; i < Ui.World.Settlements.Count; i++)
                if (S.Knowledge.IsHiddenSettlement(Ui.World.Settlements[i].Id.Value)) hidden.Add(Ui.World.Settlements[i].Id.Value);
            return hidden;
        }

        /// <summary>
        /// P7: THE KNOWLEDGE ROWS, in a state whose player knowledge hides settlements. Tab, pressed once more than there
        /// are settlements, never selects a hidden one and reaches every known one; a click where a hidden settlement
        /// stands (the camera centred on it, at a close zoom) selects nothing, picks no formation, and the lens drew
        /// neither it nor any formation of the hidden polity. Both are camera rows: looking changes no hash or order.
        /// </summary>
        /// <summary>P7: the knowledge rows (the public seam <see cref="PlayabilityGate.KnowledgeRows"/>).</summary>
        public void Knowledge() => KnowledgeRows();

        private void KnowledgeRows()
        {
            List<int> hidden = HiddenSettlements();
            if (hidden.Count == 0) return;
            LookCheck("map: knowledge", "Tab never reaches a hidden settlement", () =>
            {
                CloseEverything();
                var reached = new List<int>();
                int known = Ui.World.Settlements.Count - hidden.Count;
                for (int k = 0; k <= Ui.World.Settlements.Count; k++)
                {
                    h.Key(Keys.Tab);
                    int now = Ui.SelectedSettlement;
                    if (hidden.Contains(now)) return (GateResult.Fail, "Tab selected hidden settlement " + now.ToString(CultureInfo.InvariantCulture));
                    if (!reached.Contains(now)) reached.Add(now);
                }
                return Expect(reached.Count == known, "Tab reached " + reached.Count.ToString(CultureInfo.InvariantCulture)
                    + " of the " + known.ToString(CultureInfo.InvariantCulture) + " known settlements");
            });
            LookCheck("map: knowledge", "a click on a hidden position selects nothing", () =>
            {
                CloseEverything();
                // Close in (the Settlement band), so no known settlement's target reaches the hidden site being clicked.
                (GateResult zoomed, string why) = WheelUntil(WorldZoom.Settlement, +1, 40);
                if (zoomed != GateResult.Pass) return (GateResult.Fail, "could not zoom to the Settlement band: " + why);
                foreach (int target in hidden)
                {
                    int before = Ui.SelectedSettlement;
                    CameraFocus.CenterOn(Ui.Camera, Ui.World, target, Ui.MapRect);
                    h.Idle(2);
                    SettlementRow row = default;
                    for (int i = 0; i < Ui.World.Settlements.Count; i++) if (Ui.World.Settlements[i].Id.Value == target) row = Ui.World.Settlements[i];
                    LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(row, Ui.World.Terrain!.Size);
                    (double sx, double sy) = Ui.Camera.WorldToScreen(pos.X, pos.Y, Ui.MapRect);
                    h.Click(sx, sy);
                    if (Ui.SelectedSettlement != before) return (GateResult.Fail, "the click at hidden settlement " + target.ToString(CultureInfo.InvariantCulture) + " selected " + Ui.SelectedSettlement.ToString(CultureInfo.InvariantCulture));
                    if (Ui.SelectedUnit >= 0) return (GateResult.Fail, "the click at hidden settlement " + target.ToString(CultureInfo.InvariantCulture) + " picked formation " + Ui.SelectedUnit.ToString(CultureInfo.InvariantCulture));
                    if (Ui.LensFrame is { } f)
                    {
                        bool drawn = f.SettlementsDrawn.Contains(target);
                        foreach (SettlementHit hit in f.SettlementHits) drawn |= hit.Id == target;
                        if (drawn) return (GateResult.Fail, "the lens drew hidden settlement " + target.ToString(CultureInfo.InvariantCulture));
                    }
                    if (Ui.LensFrame is { } g)
                        foreach (int u in g.UnitTokens)
                            for (int i = 0; i < Ui.World.MilitaryUnits.Count; i++)
                                if (Ui.World.MilitaryUnits[i].Id == u && Ui.World.MilitaryUnits[i].Owner.Value == HiddenPolity.Value)
                                    return (GateResult.Fail, "the lens drew formation " + u.ToString(CultureInfo.InvariantCulture) + " of the hidden polity");
                }
                WheelUntil(WorldZoom.World, -1, 60);
                return (GateResult.Pass, "");
            });
            CloseEverything();
        }

        /// <summary>
        /// P3: THE MAP LEGEND — its tab shows it and hides it, and so does L. Shown, it must be PAINTED by the lens (its
        /// runs in the lens's DrawList and in FrameText under the lens surface, spelling its title, one entry per layer
        /// the frame drew — exactly LensFrame.Painted — and the projection's absent lines), with no ImGui tooltip window;
        /// hidden, none of it is drawn. Each is a CAMERA row: looking at the legend changes neither the world hash nor
        /// the queued orders.
        /// </summary>
        private void LegendRows()
        {
            LookCheck("map: legend", "legend (show: its tab)", () =>
            {
                CloseEverything();
                if (Ui.ShowLegend) h.ClickControl("legend");
                bool clicked = h.ClickControl("legend");
                h.Idle(1);
                return clicked ? LegendShown() : (GateResult.Fail, "the legend tab is not drawn");
            });
            LookCheck("map: legend", "legend (hide: its tab)", () =>
            {
                bool clicked = h.ClickControl("legend");
                h.Idle(1);
                return Expect(clicked && LegendHidden(), "clicked " + clicked + ", shown " + Ui.ShowLegend + ", legend painted " + (Ui.LensFrame?.Legend is not null));
            });
            LookCheck("keys", "L (legend: show, then hide)", () =>
            {
                CloseEverything();
                bool before = Ui.ShowLegend;
                h.Key(Keys.L);
                h.Idle(1);
                (GateResult r, string d) = before ? (LegendHidden() ? GateResult.Pass : GateResult.Fail, "L did not hide it") : LegendShown();
                h.Key(Keys.L);
                h.Idle(1);
                bool back = Ui.ShowLegend == before && (before || LegendHidden());
                return r != GateResult.Pass ? (r, "L: " + d) : Expect(back, "the second L did not toggle it back");
            });
            CloseEverything();
        }

        /// <summary>The legend as the last frame drew it, checked: painted, listing exactly the drawn layers and the absent
        /// lines, its runs spelling exactly that in the lens's commands and in FrameText, no tooltip window.</summary>
        private (GateResult, string) LegendShown()
        {
            if (!Ui.ShowLegend) return (GateResult.Fail, "the toggle did not flip");
            if (Ui.LensFrame is not { Legend: { } legend } f) return (GateResult.Fail, "no legend painted");
            if (!legend.Layers.SequenceEqual(f.Painted) || legend.Layers.Count == 0)
                return (GateResult.Fail, "lists " + string.Join(", ", legend.Layers) + "; the frame drew " + string.Join(", ", f.Painted));
            var want = new List<string> { legend.Title };
            foreach (WorldLayer l in legend.Layers) want.Add(WorldLens.LayerLabel(l));
            if (legend.Absent.Count > 0) want.Add(WorldLens.LegendAbsentHeading);
            want.AddRange(legend.Absent);
            var runs = new List<string>();
            IReadOnlyList<DrawCmd> cmds = Ui.LastLensCommands;
            for (int i = cmds.Count - f.LayerDraws[(int)MapLayer.Legend]; i < cmds.Count; i++) if (cmds[i] is TextCmd t) runs.Add(t.Text);
            if (string.Join(" ", runs) != string.Join(" ", want)) return (GateResult.Fail, "the legend's runs do not spell its entries");
            IReadOnlyList<string> lens = Ui.FrameText.Texts(FrameSurface.Lens);
            foreach (string r in runs) if (!lens.Contains(r)) return (GateResult.Fail, "'" + r + "' is not in FrameText (lens)");
            if (h.Gui.LastTooltipWindows.Count > 0) return (GateResult.Fail, "a tooltip window: " + h.Gui.LastTooltipWindows[0]);
            return (GateResult.Pass, legend.Layers.Count.ToString(CultureInfo.InvariantCulture) + " layers drawn, "
                + legend.Absent.Count.ToString(CultureInfo.InvariantCulture) + " absent lines");
        }

        /// <summary>Whether the legend is hidden: off, not painted, its title in no lens text of the last frame.</summary>
        private bool LegendHidden() =>
            !Ui.ShowLegend && Ui.LensFrame is { Legend: null } f && f.LayerDraws[(int)MapLayer.Legend] == 0
            && !Ui.FrameText.Contains(WorldLens.LegendTitle(f.Zoom), FrameSurface.Lens);

        /// <summary>
        /// P1: THE PANELS THE MAP TAKES BACK — the context panel folded to its tab by its fold button and unfolded by the
        /// tab, the selection card folded to its title and unfolded, and H folding every panel and unfolding them. Each
        /// is a CAMERA row (folding is looking: the world hash and the queued orders must not change), and each checks
        /// the map rect: the full window width while the column is folded, the panel's left edge while it is shown.
        /// </summary>
        private void PanelFoldRows()
        {
            Section first = GameSections.Roster(Ui.Developer)[0];
            static string Px(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
            LookCheck("map: panels", "fold the context panel (its fold button)", () =>
            {
                CloseEverything();
                h.ClickControl("nav:" + first);
                if (!Ui.ContextShown) return (GateResult.Fail, "the " + first + " section did not open");
                RectD shown = Ui.MapRect;
                bool clicked = h.ClickControl("panel-fold");
                h.Idle(1);
                bool ok = clicked && Ui.ContextFolded && !Ui.ContextShown && Ui.OpenSection == first
                    && Ui.MapRect.W == h.Width && Ui.MapRect.W > shown.W && Ui.ContextTabRect.Width > 0;
                return Expect(ok, "clicked " + clicked + ", folded " + Ui.ContextFolded + ", section " + Ui.OpenSection
                    + ", map rect width " + Px(shown.W) + " -> " + Px(Ui.MapRect.W));
            });
            LookCheck("map: panels", "unfold the context panel (its tab)", () =>
            {
                if (!Ui.ContextFolded) return (GateResult.Fail, "the column is not folded");
                bool clicked = h.ClickControl("panel-unfold");
                h.Idle(1);
                bool ok = clicked && Ui.ContextShown && Ui.OpenSection == first && Ui.MapRect.Right == Ui.ContextRect.X;
                return Expect(ok, "clicked " + clicked + ", shown " + Ui.ContextShown + ", section " + Ui.OpenSection
                    + ", map rect right " + Px(Ui.MapRect.Right) + " vs panel " + Px(Ui.ContextRect.X));
            });
            float cardHeight = 0;
            LookCheck("map: panels", "fold the selection card", () =>
            {
                CloseEverything();
                cardHeight = Ui.SelectionRect.Height;
                bool clicked = h.ClickControl("card-fold");
                h.Idle(1);
                return Expect(clicked && Ui.SelectionFolded && Ui.SelectionRect.Height < cardHeight,
                    "clicked " + clicked + ", folded " + Ui.SelectionFolded + ", height " + Px(cardHeight) + " -> " + Px(Ui.SelectionRect.Height));
            });
            LookCheck("map: panels", "unfold the selection card", () =>
            {
                bool clicked = h.ClickControl("card-fold");
                h.Idle(1);
                return Expect(clicked && !Ui.SelectionFolded && Ui.SelectionRect.Height == cardHeight,
                    "clicked " + clicked + ", folded " + Ui.SelectionFolded + ", height " + Px(Ui.SelectionRect.Height) + " (was " + Px(cardHeight) + ")");
            });
            LookCheck("keys", "H (fold every panel, then unfold them)", () =>
            {
                CloseEverything();
                h.ClickControl("nav:" + first);
                h.Key(Keys.H);
                h.Idle(1);
                bool folded = Ui.ContextFolded && Ui.SelectionFolded && Ui.MapRect.W == h.Width;
                h.Key(Keys.H);
                h.Idle(1);
                bool unfolded = !Ui.ContextFolded && !Ui.SelectionFolded && Ui.ContextShown && Ui.MapRect.Right == Ui.ContextRect.X;
                return Expect(folded && unfolded, "folded all " + folded + ", unfolded all " + unfolded);
            });
            CloseEverything();
        }

        /// <summary>Selecting the capital and two other settlements on the map, where the camera shows them (each a
        /// camera row: CenterOn and a click select; neither may write).</summary>
        private void SelectRows()
        {
            IReadOnlyWorldState w = Ui.World;
            EmpireQuery.TryGetCapital(w, Me, out SettlementId cap);
            var picks = new List<int>();
            if (cap.Value >= 0 && EmpireQuery.TryGetCapital(w, Me, out _)) picks.Add(cap.Value);
            for (int i = w.Settlements.Count - 1; i >= 0 && picks.Count < 3; i--)
                if (!picks.Contains(w.Settlements[i].Id.Value) && !S.Knowledge.IsHiddenSettlement(w.Settlements[i].Id.Value))   // P7: only what can be clicked
                    picks.Add(w.Settlements[i].Id.Value);
            foreach (int id in picks)
            {
                int target = id;
                string label = target == cap.Value ? "capital" : "settlement " + target.ToString(CultureInfo.InvariantCulture);
                LookCheck("map", "select " + label, () =>
                {
                    // Click the settlement's own marker where the camera shows it (centred, at a close zoom).
                    if (Ui.SelectedSettlement == target) { h.Key(Keys.Tab); }
                    CameraFocus.CenterOn(Ui.Camera, Ui.World, target, Ui.MapRect);
                    h.Idle(2);
                    SettlementRow row = default;
                    for (int i = 0; i < Ui.World.Settlements.Count; i++) if (Ui.World.Settlements[i].Id.Value == target) row = Ui.World.Settlements[i];
                    LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(row, Ui.World.Terrain!.Size);
                    (double sx, double sy) = Ui.Camera.WorldToScreen(pos.X, pos.Y, Ui.MapRect);
                    h.Click(sx, sy);
                    return Expect(Ui.SelectedSettlement == target, "selected " + Ui.SelectedSettlement);
                });
            }
        }

        /// <summary>P2: the frames a wheel row lets the eased camera take to settle (ProgressionCamera's rate: a notch settles
        /// in about 35 frames at 60 Hz; many notches at once take longer).</summary>
        public const int SettleFrames = 240;

        /// <summary>P2: the band of the camera's TARGET view over the map rect (where the ease is going).</summary>
        private WorldZoom TargetBand() =>
            MapDetail.At(Ui.Camera.TargetZoom, Ui.MapRect.W, Ui.MapRect.H, Ui.World.Terrain!.KmPerPx).Band;

        /// <summary>P2: one wheel row — notches of <paramref name="notch"/> at the map's centre (at most
        /// <paramref name="max"/>) until the target view's band is <paramref name="want"/>, then the ease settles.</summary>
        private (GateResult, string) WheelUntil(WorldZoom want, int notch, int max)
        {
            (double x, double y) = Centre();
            for (int k = 0; k < max && TargetBand() != want; k++) h.Wheel(x, y, notch);
            int frames = h.Settle(SettleFrames);
            return Expect(frames >= 0 && Ui.LensFrame?.Zoom == want,
                "lens " + Ui.LensFrame?.Zoom + ", target band " + TargetBand()
                + (frames < 0 ? ", the camera did not settle in " + SettleFrames.ToString(CultureInfo.InvariantCulture) + " frames" : ""));
        }

        /// <summary>Drag pan, the wheel zooms and CenterOn (camera rows).</summary>
        private void CameraLookRows()
        {
            LookCheck("map", "drag pan", () =>
            {
                (double x, double y) = Centre();
                double cx = Ui.Camera.CenterX, cy = Ui.Camera.CenterY;
                h.Drag(x, y, x + 120, y + 40);
                return Expect(Ui.Camera.CenterX != cx || Ui.Camera.CenterY != cy, "camera did not move");
            });
            // P2 — RESTATED DELIBERATELY: the wheel moves the camera's TARGET and the shown view EASES to it, so a row that
            // stopped at the first frame the lens showed the band could pass mid-glide, on a view the camera then left.
            // Each wheel row now notches until the TARGET view's band (MapDetail, km) is the one wanted, lets the ease
            // settle (bounded: SettleFrames — a camera that does not settle fails the row), and then requires the lens
            // to draw that band from the settled view. (The point under the cursor once it settles is pinned where the
            // clamp cannot intervene, by ContinuousLevelOfDetailTests / EasedCameraTests.)
            foreach (WorldZoom level in new[] { WorldZoom.Regional, WorldZoom.Settlement })
            {
                WorldZoom want = level;
                LookCheck("map", "wheel zoom to " + want, () => WheelUntil(want, +1, 40));
            }
            LookCheck("map", "wheel zoom out to World", () => WheelUntil(WorldZoom.World, -1, 60));
            LookCheck("map", "centre on a settlement (CameraFocus.CenterOn)", () =>
            {
                // The first settlement (table order) whose marker is more than 50 px from the map rect's centre; after
                // CenterOn it must sit no farther from the centre (as near as the camera's clamp allows) — and P1: where
                // the clamp allows it, within 0.5 px of the MAP RECT's centre (not the window's).
                IReadOnlyWorldState w = Ui.World;
                int pick = -1;
                double before = 0;
                for (int i = 0; i < w.Settlements.Count && pick < 0; i++)
                    if (FromCentre(w.Settlements[i]) is var d && d > 50) { before = d; pick = i; }
                if (pick < 0) return (GateResult.NotOffered, "every settlement is already within 50 px of the centre");
                double cx = Ui.Camera.CenterX, cy = Ui.Camera.CenterY;
                bool ok = CameraFocus.CenterOn(Ui.Camera, Ui.World, w.Settlements[pick].Id.Value, Ui.MapRect);
                h.Idle(2);
                double after = FromCentre(Ui.World.Settlements[pick]);
                bool allows = ClampAllowsCentre(Ui.World.Settlements[pick]);
                bool moved = Ui.Camera.CenterX != cx || Ui.Camera.CenterY != cy;
                return Expect(ok && after <= before + 0.5 && (!allows || after <= 0.5),
                    "CenterOn " + ok + ", " + before.ToString("0.0", CultureInfo.InvariantCulture) + " px from the centre -> "
                    + after.ToString("0.0", CultureInfo.InvariantCulture) + (allows ? " (the clamp allows the centre)" : "")
                    + (moved ? "" : ", camera did not move"));
            });
        }

        private void Units()
        {
            MilitaryUnitRow[] mine = MilitaryQuery.Units(Ui.World, Me);
            if (mine.Length == 0) { Row("map: formation", "select formation", GateResult.NotOffered, "the player has no formation"); return; }
            int unit = mine[0].Id;
            LookCheck("map: formation", "select formation (click token)", () =>
            {
                if (mine[0].Location.Value >= 0) CameraFocus.CenterOn(Ui.Camera, Ui.World, mine[0].Location.Value, Ui.MapRect);
                h.Idle(2);
                UnitPlacement? at = null;
                foreach (UnitPlacement p in Ui.LensFrame!.UnitPlacements) if (p.Id == unit) at = p;
                if (at is not { } placed) return (GateResult.Fail, "token not drawn");
                h.Click(placed.X, placed.Y);
                return Expect(Ui.SelectedUnit == unit && Ui.UnitCard is not null, "selected unit " + Ui.SelectedUnit);
            });
            Check("map: formation", "unit card states the M7 limitation", () =>
            {
                if (Ui.UnitCard is not { } card) return (GateResult.Fail, "no card");
                bool says = card.Limitation.Contains("Battle Layer", StringComparison.Ordinal) && card.Limitation.Contains("cannot be moved", StringComparison.Ordinal);
                // The card's only control is its close button: nothing pretends to move the formation.
                int cardControls = 0;
                foreach (UiControl c in Ui.Controls.Last)
                    if (c.X0 >= Ui.UnitCardRect.X && c.X1 <= Ui.UnitCardRect.X + Ui.UnitCardRect.Width
                        && c.Y0 >= Ui.UnitCardRect.Y && c.Y1 <= Ui.UnitCardRect.Y + Ui.UnitCardRect.Height) cardControls++;
                return Expect(says && cardControls == 1, "limitation stated " + says + ", controls on card " + cardControls);
            });
            LookCheck("map: formation", "right-click / drag on map does not move it", () =>
            {
                MilitaryUnitRow before = mine[0];
                int q = Queued;
                (double x, double y) = Centre();
                h.RightClick(x + 80, y + 40);
                h.Drag(x, y, x - 60, y - 30);
                MilitaryUnitRow after = MilitaryQuery.Units(Ui.World, Me)[0];
                return Expect(after.X == before.X && after.Y == before.Y && after.Location == before.Location && Queued == q, "it moved");
            });
            Check("map: formation", "unit card close", () =>
            {
                if (Ui.SelectedUnit < 0)
                {
                    foreach (UnitPlacement p in Ui.LensFrame!.UnitPlacements) if (p.Id == unit) { h.Click(p.X, p.Y); break; }
                }
                if (!h.ClickControl("unit-close")) return (GateResult.Fail, "close not drawn");
                return Expect(Ui.SelectedUnit < 0 && Ui.UnitCard is null, "card still open");
            });
        }

        // -------------------------------------------------------------- End Turn

        private void EndTurnChecks()
        {
            Check("command bar", "end-turn", () =>
            {
                long t = Ui.World.Clock.Turn;
                h.ClickControl("end-turn");
                return Expect(Ui.World.Clock.Turn == t + 1, "turn " + Ui.World.Clock.Turn);
            });
            Check("keys", "Space (End Turn)", () =>
            {
                long t = Ui.World.Clock.Turn;
                h.Key(Keys.Space);
                return Expect(Ui.World.Clock.Turn == t + 1, "turn " + Ui.World.Clock.Turn);
            });
            Check("research", "Space inside the Research screen (End Turn)", () =>
            {
                OpenResearch();
                long t = Ui.World.Clock.Turn;
                h.Key(Keys.Space);
                bool ok = Ui.World.Clock.Turn == t + 1 && Ui.ProgressionOpen;
                h.Key(Keys.Escape);
                return Expect(ok, "turn " + Ui.World.Clock.Turn);
            });
            Check("notices", "notices (shown after End Turn, then fade)", () =>
            {
                // Keep ending turns (Space) until the step brings something new — a research completion or a
                // new activity — and the surface announces it; a research target keeps one coming.
                ResearchContent c = S.Config.Research!;
                for (int k = 0; k < 40 && !Ui.Notices.Visible; k++)
                {
                    if (!ResearchQuery.TryGetTarget(S.World, Me, out _) && S.QueuedResearchChoice() is null
                        && ResearchQuery.CheapestAvailable(S.World, c, Me, ResearchTree.Technology) is ResearchNodeId n)
                        S.EmitResearchOrder(n);
                    h.Key(Keys.Space);
                }
                if (!Ui.Notices.Visible) return (GateResult.NotOffered, "nothing new in 40 turns");
                int lines = Ui.Notices.Lines.Count;
                h.Idle(60 * 7);
                return Expect(lines > 0 && !Ui.Notices.Visible, "still visible after 7 s");
            });
        }

        // -------------------------------------------------------------- the monkey

        /// <summary>A seeded random player: clicks, right-clicks, drags, wheel turns and key taps anywhere, for
        /// <paramref name="steps"/> actions. Escape-with-nothing-open is acknowledged (it would exit the window).</summary>
        public void Monkey(int steps, ulong seed)
        {
            ulong x = seed ^ 0x9E3779B97F4A7C15UL;
            ulong Next() { x += 0x9E3779B97F4A7C15UL; ulong z = x; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); }
            int Pick(int n) => (int)(Next() % (ulong)n);
            Keys[] keys = [Keys.K, Keys.Escape, Keys.Tab, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.F12,
                Keys.W, Keys.A, Keys.S, Keys.D, Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Space, Keys.Enter, Keys.H, Keys.L];
            int endTurns = 0;
            for (int i = 0; i < steps; i++)
            {
                int kind = Pick(10);
                double px = Pick(h.Width), py = Pick(h.Height);
                long turn = Ui.World.Clock.Turn;
                switch (kind)
                {
                    case <= 4: h.Click(px, py); break;
                    case 5: h.RightClick(px, py); break;
                    case 6: h.Drag(px, py, Pick(h.Width), Pick(h.Height), 4); break;
                    case 7: h.Wheel(px, py, Pick(7) - 3); break;
                    default:
                    {
                        Keys k = keys[Pick(keys.Length)];
                        if (k == Keys.Space && endTurns >= 6) k = Keys.K;
                        if (k == Keys.W || k == Keys.S || k == Keys.Up || k == Keys.Down) h.Hold(k, 4); else h.Key(k);
                        break;
                    }
                }
                if (Ui.World.Clock.Turn != turn) endTurns++;
                report.MonkeyActions++;
            }
            Row("monkey", "random clicks / drags / wheel / keys", GateResult.Pass,
                steps.ToString(CultureInfo.InvariantCulture) + " actions, " + endTurns.ToString(CultureInfo.InvariantCulture) + " End Turns");
        }
    }
}
