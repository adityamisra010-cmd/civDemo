using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Headless;

/// <summary>
/// M5 map workstream P10 — THE GATE'S 'NOW USABLE' ROWS and the research-done state they pass in.
/// <list type="bullet">
/// <item>THE STATE "research done: now usable": played from the canonical world — the cheapest Technology node available
///   at turn 1 whose completion alone fires a shortcut (<see cref="NowUsableNode"/>: probed through
///   <see cref="DiscoveryShortcuts.Transitions"/> over the content's own source entities; no node id here) is targeted
///   and End Turn is pressed until a shortcut fires.</item>
/// <item>In a state that offers a shortcut: the offer row (the entity is knowledge-eligible now; the block is drawn with its
///   headline, its permission line and the knowledge statement, and says nothing is 'discovered') and the "Show on map"
///   row (the shortcut's layers shown, their qualifying places outlined — every one known to the player — the key opening
///   with the shortcut's rows). Both are camera rows: looking changes neither the world hash nor the queued orders.</item>
/// <item>In a state whose last step completed research that makes nothing usable (the plain "research done" state): the
///   row that nothing is offered — the shortcut fires on an eligibility transition only.</item>
/// </list>
/// </summary>
public static partial class PlayabilityGate
{
    /// <summary>P10: the name of the research-done state a shortcut fires in (the AI tag follows).</summary>
    public const string NowUsableStateName = "research done: now usable";

    /// <summary>P10: the 'now usable' rows alone, exactly as the sweep runs them, on <paramref name="h"/>.</summary>
    public static IReadOnlyList<GateRow> NowUsableRows(UiFrameHarness h, string state)
    {
        var report = new GateReport();
        var run = new StateRun(h, state, report, null);
        run.NowUsable();
        return report.Rows;
    }

    /// <summary>
    /// P10: the cheapest research node (by the composite key (EffectiveCost, dense index) — <see cref="Cheapest"/>) AVAILABLE to
    /// the player on <paramref name="w"/> whose completion alone fires a 'now usable' shortcut: each available node named in
    /// a source entity's requirement (<see cref="DiscoveryShortcuts.SourceEntities"/>) is probed by adding its completed row
    /// to a clone and asking <see cref="DiscoveryShortcuts.Transitions"/>. Null when none does.
    /// </summary>
    public static ResearchNodeId? NowUsableNode(WorldState w, SimConfig cfg, DiscoveryShortcutContent ui)
    {
        ResearchContent c = cfg.Research ?? throw new InvalidOperationException("no research content");
        bool[] available = ResearchQuery.AvailableMask(c, ResearchQuery.CompletedMask(w, c, Me));
        var firing = new List<int>();
        foreach (int e in DiscoveryShortcuts.SourceEntities(cfg, ui))
            foreach (int n in c.Entities[e].NodeAtoms)
            {
                if (!available[n] || firing.Contains(n)) continue;
                WorldState probe = w.Clone();
                probe.ResearchCompleted.Add(new ResearchCompletedRow(Me, c.Nodes[n].Key));
                if (DiscoveryShortcuts.Transitions(w, probe, cfg, ui, Me).Count > 0) firing.Add(n);
            }
        int best = Cheapest(firing, n => ResearchQuery.EffectiveCost(w, c, Me, n));
        return best < 0 ? null : c.Nodes[best].Key;
    }

    /// <summary>The node of <paramref name="nodes"/> with the lowest COMPOSITE KEY (cost, dense index) — equal costs go to
    /// the lower index (key order), whatever order the nodes come in; -1 when there is none.</summary>
    public static int Cheapest(IReadOnlyList<int> nodes, Func<int, double> cost)
    {
        int best = -1;
        double bestCost = 0.0;
        foreach (int n in nodes)
        {
            double k = cost(n);
            if (best < 0 || k < bestCost || (k == bestCost && n < best)) { best = n; bestCost = k; }
        }
        return best;
    }

    /// <summary>P10: the canonical world, the <see cref="NowUsableNode"/> targeted, End Turn until a shortcut fires.</summary>
    private static UiSession ResearchDoneNowUsable(int ai)
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: ai);
        if (NowUsableNode(s.World, s.Config, s.NowUsableHistory.Content) is not ResearchNodeId n)
            throw new InvalidOperationException("now-usable rig: no node available at turn 1 fires a shortcut");
        for (int t = 0; t < 200 && s.NowUsableHistory.Entries.Count == 0; t++)
        {
            if (!ResearchQuery.TryGetTarget(s.World, Me, out _) && s.QueuedResearchChoice() is null && !s.EmitResearchOrder(n))
                throw new InvalidOperationException("now-usable rig: the node could not be ordered");
            s.EndTurn();
        }
        if (s.NowUsableHistory.Entries.Count == 0) throw new InvalidOperationException("now-usable rig: no shortcut fired in 200 turns");
        return s;
    }

    private sealed partial class StateRun
    {
        /// <summary>P10: the 'now usable' rows (the public seam <see cref="PlayabilityGate.NowUsableRows"/>).</summary>
        public void NowUsable() => NowUsableRows();

        private void NowUsableRows()
        {
            if (Ui.OverlayLayers.Count == 0) return;
            IReadOnlyList<(int Index, NowUsable Entry)> offered = Ui.NowUsableOffered;
            if (offered.Count == 0)
            {
                NothingOfferedRow();
                return;
            }
            foreach ((int index, NowUsable e) in offered)
            {
                LookCheck("map: now usable", e.Name + " (offered: an eligibility transition)", () => Offered(index, e));
                LookCheck("map: now usable", e.Name + " (Show on map: its button)", () => ShowOnMap(index, e));
            }
            HideAllOverlays();
            CloseEverything();
        }

        /// <summary>The last step completed research for the player and fired nothing: nothing is offered or drawn.</summary>
        private void NothingOfferedRow()
        {
            if (S.PreviousWorld is not { } prev || S.Config.Research is not { } c) return;
            ResearchNodeId[] learned = ResearchQuery.CompletedBetween(prev, S.World, Me);
            if (learned.Length == 0) return;
            LookCheck("map: now usable", "nothing offered after a completion that makes nothing usable", () =>
            {
                CloseEverything();
                h.Idle(1);
                var names = new List<string>();
                foreach (ResearchNodeId k in learned) names.Add(c.Nodes[c.IndexOf(k)].Name);
                bool fired = DiscoveryShortcuts.Transitions(prev, S.World, S.Config, S.NowUsableHistory.Content, Me).Count > 0;
                bool drawn = Ui.FrameText.Contains(GameUi.NowUsableTitle, FrameSurface.Overlay)
                    || Ui.Controls.Find(GameUi.NowUsableControl(0)) is not null;
                return Expect(!fired && !drawn && Ui.NowUsableOffered.Count == 0,
                    "learned " + string.Join(", ", names) + ": fired " + fired + ", drawn " + drawn);
            });
        }

        /// <summary>The offer: the entity is knowledge-eligible for the player now and was stamped by a step of this session
        /// (its turn is within the offer); the block says 'now usable', the permission line and the knowledge statement,
        /// and nothing 'discovered'; its button is drawn.</summary>
        private (GateResult, string) Offered(int index, NowUsable e)
        {
            CloseEverything();
            h.Idle(1);
            ResearchContent c = S.Config.Research!;
            bool eligible = ResearchQuery.IsKnowledgeEligible(c, e.Entity, ResearchQuery.CompletedMask(S.World, c, Me));
            long t = S.World.Clock.Turn;
            IReadOnlyList<string> texts = Ui.FrameText.Texts(FrameSurface.Overlay);
            string all = string.Join("\n", texts);
            var missing = new List<string>();
            foreach (string want in new[] { GameUi.NowUsableTitle, DiscoveryShortcuts.Label, "Permission to use:", "Knowledge of the resource:", "Show on map" })
                if (!all.Contains(want, StringComparison.Ordinal)) missing.Add(want);
            bool discovered = all.Contains("discover", StringComparison.OrdinalIgnoreCase);
            bool button = Ui.Controls.Find(GameUi.NowUsableControl(index)) is not null;
            return Expect(eligible && e.ActiveAt(t) && missing.Count == 0 && !discovered && button,
                "eligible " + eligible + ", turn " + t.ToString(CultureInfo.InvariantCulture) + " in [" + e.Turn.ToString(CultureInfo.InvariantCulture)
                + ", " + e.ExpiresAt.ToString(CultureInfo.InvariantCulture) + "), missing: " + string.Join(", ", missing)
                + ", 'discover' said " + discovered + ", button " + button);
        }

        /// <summary>"Show on map": the shortcut's layers are shown, focused and outlined; every outlined place is one the
        /// player knows; the outline is what the bake says (its nodes); the key opens with the shortcut's headline and
        /// carries its permission and knowledge lines.</summary>
        private (GateResult, string) ShowOnMap(int index, NowUsable e)
        {
            HideAllOverlays();
            CloseEverything();
            bool clicked = h.ClickControl(GameUi.NowUsableControl(index));
            h.Idle(1);
            if (!clicked) return (GateResult.Fail, "the button is not drawn");
            if (S.Overlay is not { } bake) return (GateResult.Fail, "no overlay bake");
            var layers = new List<int>();
            foreach (string layerKey in e.Layers)
                foreach (OverlayLayer l in Ui.OverlayLayers) if (l.Key == layerKey) layers.Add(l.Index);
            if (layers.Count == 0) return (GateResult.Fail, "none of the shortcut's layers (" + string.Join(", ", e.Layers) + ") is an overlay layer");
            foreach (int li in layers) if (!Ui.OverlayShown(li)) return (GateResult.Fail, Ui.OverlayLayers[li].Name + " is not shown");
            if (Ui.NowUsableFocus != index) return (GateResult.Fail, "the shortcut is not the focus (" + Ui.NowUsableFocus.ToString(CultureInfo.InvariantCulture) + ")");
            if (Ui.LensFrame?.Overlay is not { Key: { } key } f) return (GateResult.Fail, "the overlay or its key was not painted");
            int nodes = 0;
            foreach (int li in layers)
            {
                if (!f.OutlinedLayers.Contains(li)) return (GateResult.Fail, Ui.OverlayLayers[li].Name + " is not outlined");
                nodes += bake.Outline(li).Nodes;
                for (int n = 0; n < bake.NodeCount; n++)
                    if (bake.Qualifies(li, n) && !S.Knowledge.Known(n))
                        return (GateResult.Fail, "outlines undiscovered node " + n.ToString(CultureInfo.InvariantCulture));
            }
            if (f.OutlineNodes != nodes || f.OutlineSegments <= 0)
                return (GateResult.Fail, "outline: " + f.OutlineNodes.ToString(CultureInfo.InvariantCulture) + " nodes painted (bake "
                    + nodes.ToString(CultureInfo.InvariantCulture) + "), " + f.OutlineSegments.ToString(CultureInfo.InvariantCulture) + " segments");
            if (key.Rows.Count == 0 || key.Rows[0] != e.Headline) return (GateResult.Fail, "the key does not open with the headline");
            string rows = string.Join("\n", key.Rows);
            if (!rows.Contains(e.Permission, StringComparison.Ordinal) || !rows.Contains(DiscoveryShortcuts.KnowledgeStatement, StringComparison.Ordinal))
                return (GateResult.Fail, "the key lacks the permission line or the knowledge statement");
            for (int r = 0; r < 2 + e.Lines.Count && r < key.Rows.Count; r++)
                if (key.Rows[r].Contains("discover", StringComparison.OrdinalIgnoreCase))
                    return (GateResult.Fail, "the shortcut's key row says 'discover': " + key.Rows[r]);
            return (GateResult.Pass, nodes.ToString(CultureInfo.InvariantCulture) + " known places outlined on "
                + string.Join(", ", e.Layers) + " in " + f.OutlineSegments.ToString(CultureInfo.InvariantCulture) + " segments; offered through clock turn "
                + (e.ExpiresAt - 1).ToString(CultureInfo.InvariantCulture));
        }
    }
}
