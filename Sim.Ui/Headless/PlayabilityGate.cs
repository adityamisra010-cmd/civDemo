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
public sealed record GateRow(string State, string Area, string Control, GateResult Result, string Detail);

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
public static class PlayabilityGate
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
        return list;
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

    private sealed class StateRun(UiFrameHarness h, string state, GateReport report, Action<string>? log)
    {
        private GameUi Ui => h.Ui;
        private UiSession S => h.Ui.Session;

        private void Row(string area, string control, GateResult r, string detail = "")
        {
            report.Rows.Add(new GateRow(state, area, control, r, detail));
            if (r == GateResult.Fail) log?.Invoke("  FAIL " + state + " / " + control + ": " + detail);
        }

        private void Check(string area, string control, Func<(GateResult, string)> act)
        {
            try
            {
                (GateResult r, string d) = act();
                Row(area, control, r, d);
            }
            catch (Exception e)
            {
                Row(area, control, GateResult.Fail, "exception: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static (GateResult, string) Expect(bool ok, string what) => ok ? (GateResult.Pass, "") : (GateResult.Fail, what);

        private void CloseEverything()
        {
            for (int i = 0; i < 4 && (Ui.ProgressionOpen || Ui.Age.FlowOpen); i++) h.Key(Keys.Escape);
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
            Keyboard();
            CloseEverything();
            Map();
            CloseEverything();
            EndTurnChecks();
            CloseEverything();
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
            CloseEverything();
            OpenResearch();
            Check("research", "close button", () =>
            {
                if (!ClickHit(HitKind.Close)) return (GateResult.Fail, "not drawn");
                return Expect(!Ui.ProgressionOpen, "still open");
            });
            OpenResearch();
            Check("research", "Escape (closes)", () => { h.Key(Keys.Escape); return Expect(!Ui.ProgressionOpen, "still open"); });
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
                if (!Screen.Canvas.Contains(x, y) || (Screen.MinimapVisible && Screen.MinimapRect().Contains(x, y))) continue;
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
                    if (!Screen.Canvas.Contains(x, y) || (Screen.MinimapVisible && Screen.MinimapRect().Contains(x, y))) continue;
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
            double top = ChromeGeometry.ContentTop(ChromeGeometry.Context, ImGuiNET.ImGui.GetFrameHeight()), bottom = p.Y + p.Height - 10;
            for (int k = 0; k < 60; k++)
            {
                if (Find(kind, a, b) is not { } hit) return false;
                if (hit.Rect.CenterY >= top + 2 && hit.Rect.CenterY <= bottom - 2)
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
            foreach (UiControl c in Ui.Controls.Last)
            {
                if (!c.Name.StartsWith("where-", StringComparison.Ordinal)) continue;
                if (c.Y1 > Ui.ContextRect.Y + Ui.ContextRect.Height - 10) continue;
                UiControl ctl = c;
                int id = int.Parse(ctl.Name[(ctl.Name.LastIndexOf('-') + 1)..], CultureInfo.InvariantCulture);
                Check("developer: turn", "where row (selects and centres)", () =>
                {
                    (double ax, double ay) = h.Aim(ctl);
                    h.Click(ax, ay);
                    return Expect(Ui.SelectedSettlement == id, "selected " + Ui.SelectedSettlement);
                });
                break;
            }
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
            Check("keys", "W A S D (pan)", () =>
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

        private (double X, double Y) Centre() => (h.Width / 2.0, h.Height / 2.0 + 30);

        private void Map()
        {
            IReadOnlyWorldState w = Ui.World;
            EmpireQuery.TryGetCapital(w, Me, out SettlementId cap);
            var picks = new List<int>();
            if (cap.Value >= 0 && EmpireQuery.TryGetCapital(w, Me, out _)) picks.Add(cap.Value);
            for (int i = w.Settlements.Count - 1; i >= 0 && picks.Count < 3; i--)
                if (!picks.Contains(w.Settlements[i].Id.Value)) picks.Add(w.Settlements[i].Id.Value);
            foreach (int id in picks)
            {
                int target = id;
                string label = target == cap.Value ? "capital" : "settlement " + target.ToString(CultureInfo.InvariantCulture);
                Check("map", "select " + label, () =>
                {
                    // Click the settlement's own marker where the camera shows it (centred, at a close zoom).
                    if (Ui.SelectedSettlement == target) { h.Key(Keys.Tab); }
                    CameraFocus.CenterOn(Ui.Camera, Ui.World, target, h.Width, h.Height);
                    h.Idle(2);
                    SettlementRow row = default;
                    for (int i = 0; i < Ui.World.Settlements.Count; i++) if (Ui.World.Settlements[i].Id.Value == target) row = Ui.World.Settlements[i];
                    LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(row, Ui.World.Terrain!.Size);
                    (double sx, double sy) = Ui.Camera.WorldToScreen(pos.X, pos.Y, h.Width, h.Height);
                    h.Click(sx, sy);
                    return Expect(Ui.SelectedSettlement == target, "selected " + Ui.SelectedSettlement);
                });
            }
            Units();
            Check("map", "drag pan", () =>
            {
                (double x, double y) = Centre();
                double cx = Ui.Camera.CenterX, cy = Ui.Camera.CenterY;
                h.Drag(x, y, x + 120, y + 40);
                return Expect(Ui.Camera.CenterX != cx || Ui.Camera.CenterY != cy, "camera did not move");
            });
            foreach (WorldZoom level in new[] { WorldZoom.Regional, WorldZoom.Settlement })
            {
                WorldZoom want = level;
                Check("map", "wheel zoom to " + want, () =>
                {
                    (double x, double y) = Centre();
                    for (int k = 0; k < 40 && Ui.LensFrame?.Zoom != want; k++) h.Wheel(x, y, 1);
                    return Expect(Ui.LensFrame?.Zoom == want, "lens " + Ui.LensFrame?.Zoom);
                });
            }
            Check("map", "wheel zoom out to World", () =>
            {
                (double x, double y) = Centre();
                for (int k = 0; k < 60 && Ui.LensFrame?.Zoom != WorldZoom.World; k++) h.Wheel(x, y, -1);
                return Expect(Ui.LensFrame?.Zoom == WorldZoom.World, "lens " + Ui.LensFrame?.Zoom);
            });
        }

        private void Units()
        {
            MilitaryUnitRow[] mine = MilitaryQuery.Units(Ui.World, Me);
            if (mine.Length == 0) { Row("map: formation", "select formation", GateResult.NotOffered, "the player has no formation"); return; }
            int unit = mine[0].Id;
            Check("map: formation", "select formation (click token)", () =>
            {
                if (mine[0].Location.Value >= 0) CameraFocus.CenterOn(Ui.Camera, Ui.World, mine[0].Location.Value, h.Width, h.Height);
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
                    if (c.X0 >= GameUi.UnitCardRect.X && c.X1 <= GameUi.UnitCardRect.X + GameUi.UnitCardRect.Width
                        && c.Y0 >= GameUi.UnitCardRect.Y && c.Y1 <= GameUi.UnitCardRect.Y + GameUi.UnitCardRect.Height) cardControls++;
                return Expect(says && cardControls == 1, "limitation stated " + says + ", controls on card " + cardControls);
            });
            Check("map: formation", "right-click / drag on map does not move it", () =>
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
                Keys.W, Keys.A, Keys.S, Keys.D, Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Space, Keys.Enter];
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
