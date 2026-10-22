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

        /// <summary>P6: the first inspectable region inside the docked Age panel matching <paramref name="match"/>, scrolling
        /// the panel down (the wheel over it) until one is in view or the panel stops moving.</summary>
        private InfoHit? ScrollAgePanelTo(Func<InfoHit, bool> match)
        {
            for (int step = 0; step < 40; step++)
            {
                Sim.Ui.Render.RectD panel = Ui.AgePanelBounds;
                foreach (InfoHit x in Ui.InfoRegistry.Hits)
                    if (panel.Contains(x.Rect.CenterX, x.Rect.CenterY) && x.Rect.Y >= panel.Y && x.Rect.Bottom <= panel.Bottom && match(x)) return x;
                string before = Signature();
                h.Wheel(panel.CenterX, panel.CenterY, -1);
                if (Signature() == before && step > 0) break;   // the panel's end
            }
            return null;

            string Signature()
            {
                var sb = new System.Text.StringBuilder();
                foreach (InfoHit x in Ui.InfoRegistry.Hits) sb.Append(x.Rect.Y.ToString("0.0", CultureInfo.InvariantCulture)).Append(';');
                return sb.ToString();
            }
        }

        /// <summary>P8: the pointer moved onto KNOWN LAND in the clear map around the player's capital (the camera centred on
        /// it; points tried on rings 12 to 200 px out, every 45 degrees, so the capital's own token is not under it), and the
        /// passive place region the UI registered there — the top-most region at the pointer — or null.</summary>
        private InfoHit? KnownLandUnderPointer()
        {
            if (Ui.World.Terrain is not { } terrain) return null;
            if (EmpireQuery.TryGetCapital(Ui.World, Me, out SettlementId cap)) CameraFocus.CenterOn(Ui.Camera, Ui.World, cap.Value, Ui.MapRect);
            h.Idle(2);
            Sim.Ui.Render.RectD map = Ui.MapRect;
            // M6 EX (ADR-044): under the fog the known land around the capital is its seed disc, so nearer rings are tried first.
            foreach (int ring in new[] { 12, 20, 28, 40, 80, 120, 160, 200 })
                for (int k = 0; k < 8; k++)
                {
                    double x = Math.Round(map.CenterX + ring * Math.Cos(k * Math.PI / 4)), y = Math.Round(map.CenterY + ring * Math.Sin(k * Math.PI / 4));
                    if (!map.Contains(x, y)) continue;
                    int cell = MapCells.At(Ui.Camera, map, x, y, terrain.Size);
                    if (cell < 0 || terrain.Water[cell] >= 0.5 || !S.Knowledge.KnowsCell(cell)) continue;
                    h.MoveTo(x, y);
                    if (Ui.InfoRegistry.HitTest(x, y) is { Passive: true, Subject.Kind: InfoKind.Cell } hit) return hit;
                }
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
                         InfoKind.Project, InfoKind.Recipe, InfoKind.RoadClass, InfoKind.TaxEdict, InfoKind.Formation, InfoKind.Baseline, InfoKind.UniversityType,
                         InfoKind.Founding, InfoKind.MaterialsSplit })
            {
                InfoKind k = kind;
                Check("info", "shift-click " + k + " (POLICY)", () =>
                {
                    CloseEverything();
                    OpenPolicy();
                    PanelRect p = Ui.ContextRect;
                    for (int step = 0; step < 80; step++)   // M6 EX: 50 -> 80, the Actions block lengthens POLICY
                    {
                        if (FirstHit(k, x => !x.Passive && x.Rect.Y > p.Y + 40 && x.Rect.Bottom < p.Y + p.Height - 32) is { } hit)
                            return ShiftCheck(hit);
                        double before = Ui.Actions.Hits.Count > 0 ? Ui.Actions.Hits[0].Rect.Y : 0;
                        h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, -1);
                        if (Ui.Actions.Hits.Count > 0 && Math.Abs(Ui.Actions.Hits[0].Rect.Y - before) < 0.5 && step > 2) break;   // the bottom
                    }
                    return (GateResult.NotOffered, "POLICY paints no " + k + " in this state"
                        + (k == InfoKind.ResearchNode ? " (no research target: the sweep's earlier 'Stop' check cleared it; pinned in InfoPanelTests)" : ""));
                });
            }

            // ---- The Age panel: a milestone, the Ages, the research-next line. P6 (coverage): the panel SCROLLS and
            // registers only what is in view, so each check scrolls it (the wheel over it) as a player would before
            // calling a kind "not offered" — at 1280x800 the milestones sit below the panel's first screen.
            foreach (InfoKind kind in new[] { InfoKind.AgeMilestone, InfoKind.Age, InfoKind.ResearchNode })
            {
                InfoKind k = kind;
                Check("info", "shift-click " + k + " (Age panel)", () =>
                {
                    CloseEverything();
                    if (Ui.Controls.Find("band-age") is null) return (GateResult.NotOffered, "no Age content");
                    h.ClickControl("band-age");
                    if (ScrollAgePanelTo(x => x.Subject.Kind == k) is not { } hit)
                        return (GateResult.NotOffered, "the Age panel paints no " + k + " in this state, scrolled to its end (a research-next line shows only under an unmet research milestone)");
                    return ShiftCheck(hit, () => Ui.AgePanelOpen);
                });
                AgePanelHome();
            }
            Check("info", "Show in research tree (from a milestone's card)", () =>
            {
                CloseEverything();
                if (Ui.Controls.Find("band-age") is null) return (GateResult.NotOffered, "no Age content");
                h.ClickControl("band-age");
                if (ScrollAgePanelTo(x => x.Subject.Kind == InfoKind.AgeMilestone
                        && InfoQuery.Card(S.World, S.Config, Me, x.Subject).ResearchNode is not null) is not { } hit)
                    return (GateResult.NotOffered, "no milestone of the next Age names research (or the final Age), scrolled to the panel's end");
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
            AgePanelHome();

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
                if (mine[0].Location.Value >= 0) CameraFocus.CenterOn(Ui.Camera, Ui.World, mine[0].Location.Value, Ui.MapRect);
                h.Idle(2);
                if (FirstHit(InfoKind.Formation, x => x.Passive && x.Subject.Id == mine[0].Id) is not { } hit) return (GateResult.Fail, "token not registered");
                int selected = Ui.SelectedUnit;
                return ShiftCheck(hit, () => Ui.SelectedUnit == selected);
            });

            // ---- P8: the map GROUND — a place's card. The known ground under the pointer is a passive region (a plain click
            // stays the map's: it selects nothing new and moves nothing); Shift+click opens its card (InfoQuery's Cell card,
            // built from SiteAssessment) — the card, no order and an unchanged hash, as every info row checks.
            Check("info", "shift-click Cell (map ground: the place card)", () =>
            {
                CloseEverything();
                if (KnownLandUnderPointer() is not { } hit) return (GateResult.NotOffered, "no known land in the clear map around the capital");
                int selected = Ui.SelectedSettlement, unit = Ui.SelectedUnit;
                return ShiftCheck(hit, () => Ui.SelectedSettlement == selected && Ui.SelectedUnit == unit);
            });
            Check("info", "hover hint over the map ground (painted only while Shift is held, never a tooltip)", () =>
            {
                CloseEverything();
                if (KnownLandUnderPointer() is not { } hit) return (GateResult.NotOffered, "no known land in the clear map around the capital");
                string? plain = Ui.InfoHint;
                (string? held, int tips) = h.HoverWith(hit.Rect.CenterX, hit.Rect.CenterY, Keys.LeftShift, () => (Ui.InfoHint, h.Gui.LastTooltipWindows.Count));
                return Expect(plain is null && held == InfoPanel.CellHintText && tips == 0,
                    "hint without Shift '" + plain + "', with Shift '" + held + "', tooltip windows " + tips.ToString(CultureInfo.InvariantCulture));
            });

            // ---- P8: UNDISCOVERED GROUND is never offered as a place (drawn only where the player's knowledge hides something —
            // the hidden-rival state): no place region is registered where a hidden settlement stands, the Shift hint is not
            // painted there, and a Shift+click there opens no card. A camera row: looking changes no hash or order.
            if (HiddenSettlements() is { Count: > 0 } hidden)
                LookCheck("info", "undiscovered ground offers no place card (Shift+click opens nothing)", () =>
                {
                    CloseEverything();
                    (GateResult zoomed, string why) = WheelUntil(WorldZoom.Settlement, +1, 40);
                    if (zoomed != GateResult.Pass) return (GateResult.Fail, "could not zoom to the Settlement band: " + why);
                    try
                    {
                        foreach (int target in hidden)
                        {
                            CameraFocus.CenterOn(Ui.Camera, Ui.World, target, Ui.MapRect);
                            h.Idle(2);
                            SettlementRow row = default;
                            for (int i = 0; i < Ui.World.Settlements.Count; i++) if (Ui.World.Settlements[i].Id.Value == target) row = Ui.World.Settlements[i];
                            LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(row, Ui.World.Terrain!.Size);
                            (double sx, double sy) = Ui.Camera.WorldToScreenNearest(pos.X, pos.Y, Ui.MapRect);
                            sx = Math.Round(sx);
                            sy = Math.Round(sy);
                            int cell = MapCells.At(Ui.Camera, Ui.MapRect, sx, sy, Ui.World.Terrain.Size);
                            if (cell < 0 || S.Knowledge.KnowsCell(cell)) return (GateResult.Fail, "rig: the ground under hidden settlement " + target.ToString(CultureInfo.InvariantCulture) + " is known");
                            string? hint = h.HoverWith(sx, sy, Keys.LeftShift, () => Ui.InfoHint);
                            foreach (Sim.Ui.Info.InfoHit x in Ui.InfoRegistry.Hits)
                                if (x.Subject.Kind == InfoKind.Cell && x.Rect.Contains(sx, sy)) return (GateResult.Fail, "a place region is registered over undiscovered ground (cell " + cell.ToString(CultureInfo.InvariantCulture) + ")");
                            if (hint is not null) return (GateResult.Fail, "a hint '" + hint + "' is painted over undiscovered ground");
                            h.ShiftClick(sx, sy);
                            if (Ui.Inspector.IsOpen) return (GateResult.Fail, "a Shift+click on undiscovered ground opened '" + Ui.Inspector.Card?.Title + "'");
                        }
                        return (GateResult.Pass, "");
                    }
                    finally { WheelUntil(WorldZoom.World, -1, 60); }
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
                // UR-4 (merge of m5p-ui): the detail panel SCROLLS, in decision order, and registers a region only where
                // it is visible — in a small window ENABLES can sit below the fold, so the player scrolls the panel.
                Sim.Ui.Render.RectD detail = Screen.DetailRect;
                for (int step = 0; step < 30 && FirstHit(InfoKind.Entity) is null && Screen.Selected == pick; step++)
                    h.Wheel(detail.CenterX, detail.Bottom - 30, -1);
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

            InfoCardControls();

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

        /// <summary>The first hit of the pinned card of <paramref name="kind"/> whose centre is inside the card's visible
        /// region (<paramref name="visible"/>), or null.</summary>
        private InfoPanelHit? CardHit(InfoHitKind kind, Sim.Ui.Render.RectD visible)
        {
            foreach (InfoPanelHit x in Ui.Inspector.Hits)
                if (x.Kind == kind && visible.Contains(x.Rect.CenterX, x.Rect.CenterY)) return x;
            return null;
        }

        /// <summary>The right-hand column the pinned card is drawn in over the map, less its header row and a scrollbar.</summary>
        private Sim.Ui.Render.RectD CardBody()
        {
            PanelRect p = Ui.ContextRect;
            double top = ChromeGeometry.ContentTop(new ChromeElement(p, RulePlacement.UnderHeaderRow), ImGuiNET.ImGui.GetFrameHeight(), Ui.Scale) + 2;
            return new Sim.Ui.Render.RectD(p.X, top, p.Width - 20, p.Y + p.Height - 32 - top);
        }

        /// <summary>
        /// P6 (coverage): THE PINNED CARD'S OWN CONTROLS, which the info rows above only ever close with Escape — a link
        /// to another subject and Back, the header's close button, "Show in research tree" over the map, and the card's
        /// close button over the research screen. Each opens the card on a POLICY subject (or a research card) the way a
        /// player does, presses the control where the card drew it, and asserts its transition — and that no order was
        /// queued and the world is unchanged (a card never acts).
        /// </summary>
        private void InfoCardControls()
        {
            // Opens the card on the first POLICY subject (scrolled into view, in paint order) whose card has a hit of
            // <kind>; true when one is open.
            bool OpenCardWith(InfoHitKind kind)
            {
                CloseEverything();
                OpenPolicy();
                PanelRect p = Ui.ContextRect;
                var tried = new List<InfoSubject>();
                for (int step = 0; step < 50 && tried.Count < 12; step++)
                {
                    InfoHit? next = null;
                    foreach (InfoHit x in Ui.InfoRegistry.Hits)
                        if (!x.Passive && x.Rect.Y > p.Y + 40 && x.Rect.Bottom < p.Y + p.Height - 32 && !tried.Contains(x.Subject)) { next = x; break; }
                    if (next is { } hit)
                    {
                        tried.Add(hit.Subject);
                        h.ShiftClick(hit.Rect.CenterX, hit.Rect.CenterY);
                        if (Ui.Inspector.IsOpen && CardHit(kind, CardBody()) is not null) return true;
                        if (Ui.Inspector.IsOpen) h.Key(Keys.Escape);
                        continue;
                    }
                    double before = Ui.Actions.Hits.Count > 0 ? Ui.Actions.Hits[0].Rect.Y : 0;
                    h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, -1);
                    if (Ui.Actions.Hits.Count > 0 && Math.Abs(Ui.Actions.Hits[0].Rect.Y - before) < 0.5 && step > 2) break;
                }
                return false;
            }

            Check("info", "card: a link opens its subject, Back returns", () =>
            {
                if (!OpenCardWith(InfoHitKind.Link)) return (GateResult.NotOffered, "no POLICY subject's card links to another subject in this state");
                int q = Queued;
                string hash = WorldHash.ComputeHex(S.World);
                InfoSubject? first = Ui.Inspector.Subject;
                InfoPanelHit link = CardHit(InfoHitKind.Link, CardBody())!.Value;
                h.Click(link.Rect.CenterX, link.Rect.CenterY);
                bool followed = Ui.Inspector.Subject == link.Subject;
                if (CardHit(InfoHitKind.Back, CardBody()) is not { } back) return (GateResult.Fail, "followed " + followed + ", but no Back on the card");
                h.Click(back.Rect.CenterX, back.Rect.CenterY);
                bool returned = Ui.Inspector.Subject == first;
                return Expect(followed && returned && Queued == q && WorldHash.ComputeHex(S.World) == hash,
                    "followed " + followed + ", back " + returned + ", queued +" + (Queued - q));
            });
            Check("info", "card: the close button (over the map)", () =>
            {
                CloseEverything();
                OpenPolicy();
                PanelRect p = Ui.ContextRect;
                if (FirstHit(InfoKind.LabourSector, x => !x.Passive && x.Rect.Y > p.Y + 40 && x.Rect.Bottom < p.Y + p.Height - 32) is not { } any)
                    return (GateResult.NotOffered, "no labour row to inspect in view");
                h.ShiftClick(any.Rect.CenterX, any.Rect.CenterY);
                if (!Ui.Inspector.IsOpen) return (GateResult.Fail, "no card opened");
                if (!h.ClickControl("info-close")) return (GateResult.Fail, "the card's close button is not drawn");
                return Expect(!Ui.Inspector.IsOpen && Ui.OpenSection == Section.Policy, "card " + Ui.Inspector.IsOpen + ", section " + Ui.OpenSection);
            });
            Check("info", "card: Show in research tree (over the map)", () =>
            {
                if (!OpenCardWith(InfoHitKind.ShowInTree)) return (GateResult.NotOffered, "no POLICY subject's card names a research node in this state");
                int q = Queued;
                ResearchNodeId node = Ui.Inspector.Card!.ResearchNode!.Value;
                InfoPanelHit show = CardHit(InfoHitKind.ShowInTree, CardBody())!.Value;
                h.Click(show.Rect.CenterX, show.Rect.CenterY);
                h.Idle(10);
                ResearchContent c = S.Config.Research!;
                bool ok = Ui.ProgressionOpen && Ui.Progression!.Selected == c.IndexOf(node) && Queued == q;
                return Expect(ok, "tree " + Ui.ProgressionOpen + ", selected " + Ui.Progression?.Selected + " (want " + c.IndexOf(node) + "), queued +" + (Queued - q));
            });
            Check("info", "card: the close button (over the research screen)", () =>
            {
                CloseEverything();
                OpenResearch();
                Screen.ResetView();
                h.Idle(30);
                if (FirstHit(InfoKind.ResearchNode, x => Screen.Canvas.Contains(x.Rect.CenterX, x.Rect.CenterY) && !Screen.Obscured(x.Rect.CenterX, x.Rect.CenterY)) is not { } hit)
                    return (GateResult.NotOffered, "no research card in view");
                h.ShiftClick(hit.Rect.CenterX, hit.Rect.CenterY);
                if (!Ui.Inspector.IsOpen) return (GateResult.Fail, "no card opened");
                InfoPanelHit? close = null;
                foreach (InfoPanelHit x in Ui.Inspector.Hits) if (x.Kind == InfoHitKind.Close) close = x;
                if (close is not { } c) return (GateResult.Fail, "the card over the research screen has no close button");
                h.Click(c.Rect.CenterX, c.Rect.CenterY);
                return Expect(!Ui.Inspector.IsOpen && Ui.ProgressionOpen, "card " + Ui.Inspector.IsOpen + ", tree " + Ui.ProgressionOpen);
            });
        }
    }
}
