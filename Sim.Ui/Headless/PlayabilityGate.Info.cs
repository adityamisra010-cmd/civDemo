using System.Globalization;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Info;
using Sim.Ui.Progression;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Headless;

/// <summary>
/// M5 polish (directive §9, §4 "Shift+Left Click info") — THE GATE'S INFO AREA. In every state it Shift+clicks one
/// inspectable object of every kind each surface paints (POLICY, the Age panel, the status band, the map, the research
/// tree and its lenses, the player views) where the UI drew it, and passes a row only when the card opened for exactly
/// that subject with the title <see cref="InfoQuery"/> gives it, NO order was queued and the world hash is unchanged
/// (Shift+click never acts), and Escape then closed the card first (no exit, the surface beneath still open). A kind a
/// surface does not paint in the state is "not offered" with the reason; a click that opens nothing is a FAIL.
/// </summary>
public static partial class PlayabilityGate
{
    private sealed partial class StateRun
    {
        private InfoHit? FirstHit(InfoKind kind, Func<InfoHit, bool>? also = null)
        {
            foreach (InfoHit x in Ui.InfoRegistry.Hits)
                if (x.Subject.Kind == kind && (also is null || also(x))) return x;
            return null;
        }

        /// <summary>Shift+clicks <paramref name="hit"/> and checks the card, the order log, the world, then Escape.</summary>
        private (GateResult, string) ShiftCheck(InfoHit hit, Func<bool>? alsoUnchanged = null)
        {
            int q = Queued;
            string before = WorldHash.ComputeHex(S.World);
            int exits = h.ExitRequests;
            bool tree = Ui.ProgressionOpen;
            Section section = Ui.OpenSection;
            h.ShiftClick(hit.Rect.CenterX, hit.Rect.CenterY);
            string expected = InfoQuery.Card(S.World, S.Config, Me, hit.Subject).Title;
            bool opened = Ui.Inspector.IsOpen && Ui.Inspector.Subject == hit.Subject;
            string title = Ui.Inspector.Card?.Title ?? "";
            string status = Ui.Inspector.Card?.StatusLine ?? "";
            bool same = Queued == q && WorldHash.ComputeHex(S.World) == before && (alsoUnchanged?.Invoke() ?? true);
            h.Key(Keys.Escape);
            bool closed = !Ui.Inspector.IsOpen && h.ExitRequests == exits && Ui.ProgressionOpen == tree && Ui.OpenSection == section;
            string detail = "'" + hit.Label + "' at (" + hit.Rect.CenterX.ToString("0", CultureInfo.InvariantCulture) + ","
                + hit.Rect.CenterY.ToString("0", CultureInfo.InvariantCulture) + ") -> " + (opened ? "card '" + title + "'" : "no card for " + hit.Subject)
                + (title == expected ? "" : " (expected '" + expected + "')") + (same ? "" : "; ORDER OR STATE CHANGED")
                + (closed ? "" : "; Escape did not close the card first");
            return Expect(opened && title.Length > 0 && title == expected && status.Length > 0 && same && closed, detail);
        }

        private void InfoArea()
        {
            // ---- POLICY: every kind the action surface paints, scrolled into view.
            foreach (InfoKind kind in new[] { InfoKind.LabourSector, InfoKind.Good, InfoKind.Learning, InfoKind.ResearchNode, InfoKind.Age,
                         InfoKind.Project, InfoKind.Recipe, InfoKind.RoadClass, InfoKind.TaxEdict, InfoKind.Formation, InfoKind.Baseline, InfoKind.UniversityType })
            {
                InfoKind k = kind;
                Check("info", "shift-click " + k + " (POLICY)", () =>
                {
                    CloseEverything();
                    OpenPolicy();
                    PanelRect p = Ui.ContextRect;
                    for (int step = 0; step < 50; step++)
                    {
                        if (FirstHit(k, x => !x.Passive && x.Rect.Y > p.Y + 40 && x.Rect.Bottom < p.Y + p.Height - 32) is { } hit)
                            return ShiftCheck(hit);
                        double before = Ui.Actions.Hits.Count > 0 ? Ui.Actions.Hits[0].Rect.Y : 0;
                        h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, -1);
                        if (Ui.Actions.Hits.Count > 0 && Math.Abs(Ui.Actions.Hits[0].Rect.Y - before) < 0.5 && step > 2) break;   // the bottom
                    }
                    return (GateResult.NotOffered, "POLICY paints no " + k + " in this state");
                });
            }

            // ---- The Age panel: a milestone, the Ages, the research-next line.
            foreach (InfoKind kind in new[] { InfoKind.AgeMilestone, InfoKind.Age, InfoKind.ResearchNode })
            {
                InfoKind k = kind;
                Check("info", "shift-click " + k + " (Age panel)", () =>
                {
                    CloseEverything();
                    if (Ui.Controls.Find("band-age") is null) return (GateResult.NotOffered, "no Age content");
                    h.ClickControl("band-age");
                    Sim.Ui.Render.RectD panel = Ui.AgePanelBounds;
                    if (FirstHit(k, x => panel.Contains(x.Rect.CenterX, x.Rect.CenterY)) is not { } hit)
                        return (GateResult.NotOffered, "the Age panel paints no " + k + " in this state (a research-next line shows only under an unmet research milestone)");
                    return ShiftCheck(hit, () => Ui.AgePanelOpen);
                });
            }
            Check("info", "Show in research tree (from a milestone's card)", () =>
            {
                CloseEverything();
                if (Ui.Controls.Find("band-age") is null) return (GateResult.NotOffered, "no Age content");
                h.ClickControl("band-age");
                Sim.Ui.Render.RectD panel = Ui.AgePanelBounds;
                InfoHit? pick = null;
                foreach (InfoHit x in Ui.InfoRegistry.Hits)
                    if (x.Subject.Kind == InfoKind.AgeMilestone && panel.Contains(x.Rect.CenterX, x.Rect.CenterY)
                        && InfoQuery.Card(S.World, S.Config, Me, x.Subject).ResearchNode is not null) { pick = x; break; }
                if (pick is not { } hit) return (GateResult.NotOffered, "no milestone of the next Age names research (or the final Age)");
                int q = Queued;
                h.ShiftClick(hit.Rect.CenterX, hit.Rect.CenterY);
                ResearchNodeId? node = Ui.Inspector.Card?.ResearchNode;
                InfoPanelHit? show = null;
                foreach (InfoPanelHit ph in Ui.Inspector.Hits) if (ph.Kind == InfoHitKind.ShowInTree) show = ph;
                if (node is null || show is not { } s) return (GateResult.Fail, "the card has no Show in research tree");
                h.Click(s.Rect.CenterX, s.Rect.CenterY);
                h.Idle(10);
                ResearchContent c = S.Config.Research!;
                bool ok = Ui.ProgressionOpen && Ui.Progression!.Selected == c.IndexOf(node.Value) && Queued == q;
                return Expect(ok, "tree " + Ui.ProgressionOpen + ", selected " + Ui.Progression?.Selected + " (want " + c.IndexOf(node.Value) + "), queued +" + (Queued - q));
            });

            // ---- The status band's chips: Shift+click shows the card instead of opening the screen.
            foreach (string chip in new[] { "band-research", "band-age" })
            {
                string id = chip;
                Check("info", "shift-click " + id + " (status band)", () =>
                {
                    CloseEverything();
                    if (Ui.Controls.Find(id) is not { } c) return (GateResult.NotOffered, "not drawn");
                    if ((FirstHit(id == "band-research" ? InfoKind.ResearchNode : InfoKind.Age, x => x.Rect.Contains(c.CenterX, c.CenterY))
                        ?? FirstHit(InfoKind.Learning, x => x.Rect.Contains(c.CenterX, c.CenterY))) is not { } hit)
                        return (GateResult.Fail, "the chip registers no subject");
                    return ShiftCheck(hit, () => !Ui.ProgressionOpen && !Ui.AgePanelOpen);
                });
            }

            // ---- The map: a formation token (a passive region; a plain click would select it).
            Check("info", "shift-click Formation (map token)", () =>
            {
                CloseEverything();
                MilitaryUnitRow[] mine = MilitaryQuery.Units(Ui.World, Me);
                if (mine.Length == 0) return (GateResult.NotOffered, "the player has no formation");
                if (mine[0].Location.Value >= 0) CameraFocus.CenterOn(Ui.Camera, Ui.World, mine[0].Location.Value, h.Width, h.Height);
                h.Idle(2);
                if (FirstHit(InfoKind.Formation, x => x.Passive && x.Subject.Id == mine[0].Id) is not { } hit) return (GateResult.Fail, "token not registered");
                int selected = Ui.SelectedUnit;
                return ShiftCheck(hit, () => Ui.SelectedUnit == selected);
            });

            // ---- The research tree: an AVAILABLE card (a plain click would order it), an "opens" line, a lens item.
            Check("info", "shift-click an available research card (no order)", () =>
            {
                CloseEverything();
                OpenResearch();
                Screen.ResetView();
                h.Idle(30);
                if (FirstHit(InfoKind.ResearchNode, x => Screen.Canvas.Contains(x.Rect.CenterX, x.Rect.CenterY)
                        && Screen.Snapshot!.Nodes[Screen.Content.IndexOf(new ResearchNodeId((int)x.Subject.Id))].Available) is not { } hit)
                    return (GateResult.NotOffered, "no available card in view");
                int? choice = S.QueuedResearchChoice();
                return ShiftCheck(hit, () => S.QueuedResearchChoice() == choice);
            });
            Check("info", "shift-click an 'opens the way to' entity (research detail)", () =>
            {
                CloseEverything();
                OpenResearch();
                ResearchContent c = Screen.Content;
                int pick = -1;
                for (int i = 0; i < c.Nodes.Count && pick < 0; i++) if (c.Nodes[i].UnlockedEntities.Count > 0 && c.Nodes[i].Tree == ResearchTree.Technology) pick = i;
                if (pick < 0) return (GateResult.NotOffered, "no node opens an entity");
                Screen.Focus(pick, jump: true);
                h.MoveTo(h.Width - 20, h.Height - 120);   // off the tree: the detail panel shows the selected node
                h.Idle(5);
                if (FirstHit(InfoKind.Entity) is not { } hit) return (GateResult.Fail, "the detail panel registers no entity for " + c.Nodes[pick].Id);
                return ShiftCheck(hit);
            });
            Check("info", "shift-click a lens item (INSTITUTIONS lens)", () =>
            {
                CloseEverything();
                OpenResearch();
                if (!ClickHit(HitKind.Lens, (int)Lens.Institutions)) return (GateResult.Fail, "lens not drawn");
                h.Idle(3);
                InfoHit? hit = FirstHit(InfoKind.Entity) ?? FirstHit(InfoKind.ResearchNode);
                if (hit is not { } x) return (GateResult.NotOffered, "the lens lists nothing in this state");
                (GateResult r, string d) = ShiftCheck(x);
                ClickHit(HitKind.Lens, (int)Lens.KnowledgeAndTechnology);
                return (r, d);
            });

            // ---- The player views: text lines registered as passive regions.
            foreach ((string nav, InfoKind kind) in new[] { ("nav:Place", InfoKind.LabourSector), ("nav:Empire", InfoKind.TaxEdict), ("nav:Institutions", InfoKind.UniversityType) })
            {
                string n = nav;
                InfoKind k = kind;
                Check("info", "shift-click " + k + " (" + n[4..] + " view)", () =>
                {
                    CloseEverything();
                    h.ClickControl(n);
                    // ImGui sizes the body from its previous frame's content: its scrollbars appear a frame after the
                    // view opens, so the opening frame's regions are not final (a bottom line then goes under them).
                    h.Idle(3);
                    PanelRect p = Ui.ContextRect;
                    for (int step = 0; step < 40; step++)
                    {
                        // Clear of the bottom edge by more than a scrollbar, as a player would scroll a line into view.
                        if (FirstHit(k, x => x.Passive && x.Rect.H > 4 && x.Rect.Bottom < p.Y + p.Height - 32) is { } hit) return ShiftCheck(hit);
                        h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, -1);
                    }
                    return (GateResult.NotOffered, "the view shows no " + k + " line in this state" + (k == InfoKind.UniversityType ? " (no institution founded)" : ""));
                });
            }

            // ---- The hover hint: painted beside the pointer, never an ImGui tooltip.
            Check("info", "hover hint ('Shift+click: details', painted, not a tooltip)", () =>
            {
                CloseEverything();
                OpenPolicy();
                if (FirstHit(InfoKind.LabourSector, x => !x.Passive) is not { } hit) return (GateResult.NotOffered, "no labour row");
                h.MoveTo(hit.Rect.CenterX, hit.Rect.CenterY);
                return Expect(Ui.InfoHint == InfoPanel.HintText && h.Gui.LastTooltipWindows.Count == 0,
                    "hint '" + Ui.InfoHint + "', tooltip windows " + h.Gui.LastTooltipWindows.Count.ToString(CultureInfo.InvariantCulture));
            });
        }
    }
}
