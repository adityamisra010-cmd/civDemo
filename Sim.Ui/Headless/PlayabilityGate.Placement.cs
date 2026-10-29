using System.Globalization;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Ui.Actions;
using Sim.Ui.Info;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Headless;

/// <summary>
/// M5 S1d (D-051 §5, ADR-036) — THE GATE'S PLACEMENT AREA. In every state: POLICY's "Choose a site..." enters the
/// settlement-placement mode (or is not offered, with the reason, when no settlement of the player's can spare settlers and
/// provisions); a legal place hovered shows the painted hint, the route and the docked place card; an illegal place
/// clicked is selected with its reason while Confirm stays disabled and Enter appends nothing; the camera in the mode
/// changes nothing; Escape and Cancel leave it; F enters and leaves it; and a legal place clicked then Confirmed appends
/// exactly ONE FoundSettlement order — FoundingQuery.FoundOrder, through UiSession.EmitFoundSettlement. In a state whose
/// knowledge hides settlements, undiscovered ground is neither hovered nor chosen. Every row that only looks is a camera
/// row (the world hash and the queued orders unchanged).
/// </summary>
public static partial class PlayabilityGate
{
    /// <summary>
    /// M5 S1d: THE PLACEMENT STATE's rig — turn 1, then a Found Settlement order placed through the UI's own guarded emitter
    /// (UiSession.EmitFoundSettlement: the capital to its best legal place, the AI's ranking over THE predicate's Legal
    /// cells) and one End Turn, so the colony stands and the mode judges every later place against it.
    /// </summary>
    public static UiSession PlacementRig(int ai)
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: ai);
        if (!EmpireQuery.TryGetCapital(s.World, Me, out SettlementId capital)) throw new InvalidOperationException("placement rig: no capital");
        int site = LegalPlace(s, capital.Value);
        // M6 EX (ADR-044): under the fog the capital's seed disc lies inside its own catchment, so a place must be
        // discovered first — the player's Explore (the guarded emitter), one per turn, until a legal place is known.
        for (int k = 0; k < 4 && site < 0; k++)
        {
            if (!s.EmitExplore(capital.Value)) break;
            s.EndTurn();
            site = LegalPlace(s, capital.Value);
        }
        if (site < 0) throw new InvalidOperationException("placement rig: no legal place from the capital");
        if (!s.EmitFoundSettlement(capital.Value, site)) throw new InvalidOperationException("placement rig: the emitter refused a legal place");
        s.EndTurn();
        for (int i = 0; i < s.World.Settlements.Count; i++) if (s.World.Settlements[i].SiteCell == site) return s;
        throw new InvalidOperationException("placement rig: the order founded nothing");
    }

    /// <summary>The best place THE predicate answers Legal for from <paramref name="source"/>, against the batch this turn's
    /// queued orders already make, among the cells the player KNOWS (the AI's own ranking, AiFoundingPolicy.BestSiteFor,
    /// with the player's knowledge and — D-066 A18 — the whole-map valuation); -1 when there is none.</summary>
    public static int LegalPlace(UiSession s, int source)
    {
        if (s.Sites is not { } site || s.World.Terrain is not { } terrain) return -1;
        SettlementSiting.FrontierSiting f = site.Frontier;
        FoundSettlementOrderFactory.Pending pending = FoundSettlementOrderFactory.Standing(s.World, s.Config, f, s.QueuedOrders());
        if (pending.Founds(new SettlementId(source))) return -1;
        // D-066 A18 (M6 closure track FOG): the gate needs ANY place the player may found — THE predicate decides and the scan
        // still admits only cells the player KNOWS — so it ranks with the whole-map valuation (knownTerrainOnly off), not the
        // AI's stricter known-terrain floor. MEASURED: with the AI's valuation the gate found no legal known place in 12 of its
        // 13 states (found-confirm drawn but never pressed). The ranking only chooses which legal known place the gate
        // clicks; nothing of it is shown to the player.
        Sim.Core.Systems.SimConfig finder = s.Config.Colonization?.AiFounding is { } ai
            ? s.Config with { Colonization = s.Config.Colonization with { AiFounding = ai with { KnownTerrainOnly = false } } }
            : s.Config;
        return AiFoundingPolicy.BestSiteFor(s.World, finder, f, terrain, pending.Sites, Me, new SettlementId(source),
            FoundingQuery.Check, s.Knowledge.Core);
    }

    private sealed partial class StateRun
    {
        /// <summary>Frames <paramref name="cell"/> at the map rect's centre, 8 screen px a cell; returns the point over it.</summary>
        private (double X, double Y) AimAt(int cell)
        {
            int size = Ui.World.Terrain!.Size;
            CameraFocus.CenterOnCell(Ui.Camera, cell, size, Ui.MapRect, zoom: 8.0);
            h.Idle(2);
            return Ui.Camera.WorldToScreenNearest(cell % size + 0.5, cell / size + 0.5, Ui.MapRect);
        }

        /// <summary>The camera back to the whole map (immediately).</summary>
        private void Unzoom()
        {
            Sim.Ui.Render.RectD map = Ui.MapRect;
            Ui.Camera.Stop();
            Ui.Camera.ZoomAt(map.CenterX, map.CenterY, Ui.Camera.MinZoom(map) / Ui.Camera.Zoom, map);
            h.Idle(1);
        }

        private int SiteOf(int settlement)
        {
            for (int i = 0; i < Ui.World.Settlements.Count; i++) if (Ui.World.Settlements[i].Id.Value == settlement) return Ui.World.Settlements[i].SiteCell;
            return -1;
        }

        /// <summary>Enters the mode through POLICY's "Choose a site..." (the block's source). True when it did.</summary>
        private bool EnterPlacement()
        {
            if (Ui.Placement.Active) return true;
            OpenPolicy();
            if (Ui.Actions.Model?.Founding is null) return false;
            return ClickFounding() && Ui.Placement.Active;
        }

        /// <summary>Scrolls POLICY until "Choose a site..." lies well inside the panel's visible body (a band of a full button
        /// height from either edge: the block is the surface's last control block, so it often sits at the body's foot, where
        /// the scrolling body's clip ends above the panel's edge at the larger UI scales), then clicks it.</summary>
        private bool ClickFounding()
        {
            PanelRect p = Ui.ContextRect;
            for (int k = 0; k < 60; k++)
            {
                if (Find(ActionHitKind.FoundSettlement) is not { } hit) return false;
                double band = hit.Rect.H + 12;
                if (hit.Rect.CenterY >= p.Y + 2 * band && hit.Rect.CenterY <= p.Y + p.Height - 2 * band)
                {
                    h.Click(hit.Rect.CenterX, hit.Rect.CenterY);
                    return true;
                }
                h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, hit.Rect.CenterY < p.Y + 2 * band ? 1 : -1);
            }
            return false;
        }

        /// <summary>The placement state: the monkey starts inside the mode (F), so its clicks choose places, its Enter
        /// confirms and its Escape cancels.</summary>
        public void EnterForMonkey()
        {
            CloseEverything();
            h.Key(Keys.F);
            if (!Ui.Placement.Active) Row("placement", "the monkey starts in the mode (F)", GateResult.Fail, "F did not enter the mode");
        }

        public void Placement()
        {
            Check("policy: settle", "Choose a site... (enters the placement mode)", () =>
            {
                CloseEverything();
                OpenPolicy();
                if (Ui.Actions.Model?.Founding is not { } fb)
                    return (GateResult.NotOffered, "no settlement of yours can spare settlers and provisions now (the block is absent)");
                if (!ClickFounding()) return (GateResult.Fail, "the block is listed but its button is not drawn");
                return Expect(Ui.Placement.Active && Ui.Placement.Source == fb.Source.Value && Ui.Controls.Find("found-confirm") is not null,
                    "the mode did not open from " + fb.Source.Value.ToString(CultureInfo.InvariantCulture) + " (active " + Ui.Placement.Active
                    + ", source " + Ui.Placement.Source.ToString(CultureInfo.InvariantCulture) + ", bar " + (Ui.Controls.Find("found-confirm") is not null)
                    + ", last policy click " + Ui.Actions.LastClick.Kind + ")");
            });
            if (!Ui.Placement.Active) return;
            int source = Ui.Placement.Source;
            int legal = LegalPlace(S, source);

            LookCheck("placement", "hover a legal place (painted hint, route, docked place card; never a tooltip)", () =>
            {
                if (legal < 0) return (GateResult.NotOffered, "no place you know is legal from " + S.Names.Name(source));
                (double x, double y) = AimAt(legal);
                h.MoveTo(x, y);
                h.Idle(PlacementMode.CardRestFrames + 1);
                if (Ui.Placement.Hovered != legal) return (GateResult.Fail, "hovered " + Ui.Placement.Hovered.ToString(CultureInfo.InvariantCulture));
                if (Ui.Placement.HoveredVerdict != FoundingVerdict.Legal) return (GateResult.Fail, "judged " + Ui.Placement.HoveredVerdict);
                if (Ui.PlacementHint is not { } hint || !hint.StartsWith("Found here", StringComparison.Ordinal)) return (GateResult.Fail, "hint: " + Ui.PlacementHint);
                if (h.Gui.LastTooltipWindows.Count > 0) return (GateResult.Fail, "an ImGui tooltip window");
                if (Ui.Inspector.Subject != InfoSubject.OfCell(legal, source)) return (GateResult.Fail, "card " + Ui.Inspector.Subject);
                if (Ui.Placement.SeesAll && Ui.Placement.Route is not { Found: true }) return (GateResult.Fail, "no route shown");
                return (GateResult.Pass, "");
            });
            LookCheck("placement", "click an illegal place (chosen with its reason; Confirm disabled; Enter appends nothing)", () =>
            {
                int own = SiteOf(source);
                (double x, double y) = AimAt(own);
                h.Click(x, y);
                if (Ui.Placement.Selected != own || Ui.Placement.SelectedVerdict != FoundingVerdict.Occupied)
                    return (GateResult.Fail, "chose " + Ui.Placement.Selected.ToString(CultureInfo.InvariantCulture) + " judged " + Ui.Placement.SelectedVerdict);
                string reason = PlacementMode.ReasonText(FoundingVerdict.Occupied, Ui.Placement.SourceName, Ui.Placement.SeesAll);
                if (!Ui.FrameText.Contains(reason, FrameSurface.Placement)) return (GateResult.Fail, "the bar does not say why");
                h.ClickControl("found-confirm");
                h.Key(Keys.Enter);
                return Expect(Ui.Placement.Active && Ui.Placement.Refusal is { } r && r.Contains(reason, StringComparison.Ordinal), "refusal: " + Ui.Placement.Refusal);
            });
            LookCheck("placement", "the camera in the mode (wheel, drag): looking changes nothing", () =>
            {
                (double x, double y) = AimAt(legal >= 0 ? legal : SiteOf(source));
                h.Wheel(x, y, 1);
                h.Wheel(x, y, -1);
                h.Drag(x, y, x + 40, y + 12);
                return Expect(Ui.Placement.Active, "the camera left the mode");
            });
            KnowledgePlacementRow();
            LookCheck("placement", "Escape cancels (nothing queued, no exit request)", () =>
            {
                if (!EnterPlacement()) return (GateResult.Fail, "could not re-enter the mode");
                int exits = h.ExitRequests;
                h.Key(Keys.Escape);
                return Expect(!Ui.Placement.Active && h.ExitRequests == exits, "active " + Ui.Placement.Active + ", exit requests +" + (h.ExitRequests - exits).ToString(CultureInfo.InvariantCulture));
            });
            LookCheck("placement", "found-cancel", () =>
            {
                if (!EnterPlacement()) return (GateResult.Fail, "could not re-enter the mode");
                if (!h.ClickControl("found-cancel")) return (GateResult.Fail, "Cancel is not drawn");
                return Expect(!Ui.Placement.Active, "still in the mode");
            });
            LookCheck("keys", "F (the placement mode: enter, then leave)", () =>
            {
                CloseEverything();
                h.Key(Keys.F);
                bool entered = Ui.Placement.Active;
                h.Key(Keys.F);
                return Expect(entered && !Ui.Placement.Active, "entered " + entered + ", still " + Ui.Placement.Active);
            });
            ConfirmRow(source, legal);
            Unzoom();
            CloseEverything();
        }

        /// <summary>Set when this state's placement sweep found no legal place the player knows (under the fog, before any
        /// Explore has widened what it knows): the Confirm row is then revisited after End Turn.</summary>
        private bool _confirmDeferred;

        /// <summary>The Confirm row: a legal place clicked, then Confirm appends ONE FoundSettlement order.</summary>
        private void ConfirmRow(int source, int legal)
        {
            _confirmDeferred = legal < 0 && _phase.Length == 0;
            Check("placement", "click a legal place, then Confirm (ONE FoundSettlement order, through EmitFoundSettlement)", () =>
            {
                if (legal < 0) return (GateResult.NotOffered, "no place you know is legal from " + S.Names.Name(source));
                if (!EnterPlacement() || Ui.Placement.Source != source) return (GateResult.Fail, "could not re-enter the mode from the same source");
                (double x, double y) = AimAt(legal);
                h.Click(x, y);
                if (!Ui.Placement.CanConfirm) return (GateResult.Fail, "chose " + Ui.Placement.Selected.ToString(CultureInfo.InvariantCulture) + " judged " + Ui.Placement.SelectedVerdict);
                OrderRecord expected = FoundingQuery.FoundOrder(S.World, Me, new SettlementId(source), legal);
                int q = Queued;
                h.ClickControl("found-confirm");
                IReadOnlyList<OrderRecord> after = S.QueuedOrders();
                if (after.Count != q + 1) return (GateResult.Fail, "SILENT: " + (after.Count - q).ToString(CultureInfo.InvariantCulture) + " orders appended");
                return Expect(after[^1] == expected && !Ui.Placement.Active, "appended " + after[^1].Kind + ", mode " + Ui.Placement.Active);
            });
        }

        /// <summary>
        /// M6 Track EX (D-062 E8; ADR-044): under the fog a state can start with NO legal place the player knows — the seed
        /// disc lies inside its own settlements' spacing — so the Confirm row is not offered before End Turn. The sweep's
        /// POLICY pass has pressed Explore (an admitted expedition widens the known extent at the next turn), so the row is
        /// revisited here, after the End Turns, on what the player now knows.
        /// </summary>
        private void PlacementAfterTurns()
        {
            if (!_confirmDeferred) return;
            CloseEverything();
            if (!EnterPlacement())
            {
                Row("placement", "click a legal place, then Confirm (ONE FoundSettlement order, through EmitFoundSettlement)", GateResult.NotOffered,
                    "no settlement of yours can spare settlers and provisions now (the block is absent)");
                return;
            }
            int source = Ui.Placement.Source;
            ConfirmRow(source, LegalPlace(S, source));
            Unzoom();
            CloseEverything();
        }

        /// <summary>In a state whose knowledge hides settlements: in the mode, the ground under a hidden settlement is not
        /// hovered and not chosen (a camera row).</summary>
        private void KnowledgePlacementRow()
        {
            List<int> hidden = HiddenSettlements();
            if (hidden.Count == 0) return;
            LookCheck("placement", "undiscovered ground is neither hovered nor chosen", () =>
            {
                foreach (int target in hidden)
                {
                    int cell = SiteOf(target);
                    if (S.Knowledge.KnowsCell(cell)) continue;
                    (double x, double y) = AimAt(cell);
                    h.MoveTo(x, y);
                    int chosen = Ui.Placement.Selected;
                    if (Ui.Placement.Hovered >= 0 || Ui.PlacementHint is not null)
                        return (GateResult.Fail, "hovered undiscovered cell " + cell.ToString(CultureInfo.InvariantCulture));
                    h.Click(x, y);
                    if (Ui.Placement.Selected != chosen) return (GateResult.Fail, "chose undiscovered cell " + cell.ToString(CultureInfo.InvariantCulture));
                    return (GateResult.Pass, "");
                }
                return (GateResult.NotOffered, "every hidden settlement stands on ground the player knows");
            });
        }
    }
}
