using System.Globalization;
using Sim.Ui.Render;
using Sim.Ui.World;

namespace Sim.Ui.Headless;

/// <summary>
/// M5 map workstream P9 — THE GATE'S OVERLAY ROWS. In every state: the overlays tab unfolds the controls (a checkbox per
/// layer, the two opacity steps); each layer's checkbox shows it — the lens draws it, its key is painted (its name, what
/// it measures, for a channel every good it drives with its spread, its range, and the discovered / present / accessible /
/// extractable rows), every node it draws is one the player knows, no tooltip window — and hides it again; each opacity
/// step moves the overlay's opacity one tenth and the deepest fill drawn follows it; the tab folds the controls. In a
/// state whose knowledge hides ground (the hidden-rival rig): with every layer shown, nothing is drawn on an undiscovered
/// node. Every row is a CAMERA row: looking changes neither the world hash nor the queued orders.
/// </summary>
public static partial class PlayabilityGate
{
    /// <summary>P9: the overlay rows alone, exactly as the sweep runs them, on <paramref name="h"/>.</summary>
    public static IReadOnlyList<GateRow> OverlayRows(UiFrameHarness h, string state)
    {
        var report = new GateReport();
        var run = new StateRun(h, state, report, null);
        run.Overlays();
        return report.Rows;
    }

    private sealed partial class StateRun
    {
        /// <summary>P9: the overlay rows (the public seam <see cref="PlayabilityGate.OverlayRows"/>).</summary>
        public void Overlays() => OverlayRows();

        private void OverlayRows()
        {
            IReadOnlyList<OverlayLayer> layers = Ui.OverlayLayers;
            if (layers.Count == 0) return;
            LookCheck("map: overlays", "overlays (unfold: its tab)", () =>
            {
                CloseEverything();
                HideAllOverlays();
                if (Ui.OverlayControlsOpen) h.ClickControl(GameUi.OverlaysControl);
                bool clicked = h.ClickControl(GameUi.OverlaysControl);
                h.Idle(1);
                var missing = new List<string>();
                foreach (OverlayLayer l in layers) if (Ui.Controls.Find(l.Control) is null) missing.Add(l.Control);
                foreach (string c in new[] { GameUi.OverlayOpacityDown, GameUi.OverlayOpacityUp }) if (Ui.Controls.Find(c) is null) missing.Add(c);
                return Expect(clicked && Ui.OverlayControlsOpen && missing.Count == 0 && Ui.LensFrame?.Overlay is null,
                    "clicked " + clicked + ", open " + Ui.OverlayControlsOpen + ", not drawn: " + string.Join(", ", missing));
            });
            foreach (OverlayLayer layer in layers)
            {
                LookCheck("map: overlays", layer.Name + " (show: its checkbox)", () =>
                {
                    if (!Ui.OverlayControlsOpen) h.ClickControl(GameUi.OverlaysControl);
                    bool clicked = h.ClickControl(layer.Control);
                    h.Idle(1);
                    return clicked ? OverlayLayerShown(layer) : (GateResult.Fail, "the checkbox is not drawn");
                });
                LookCheck("map: overlays", layer.Name + " (hide: its checkbox)", () =>
                {
                    bool clicked = h.ClickControl(layer.Control);
                    h.Idle(1);
                    bool gone = !Ui.OverlayShown(layer.Index) && Ui.LensFrame?.Overlay is null
                        && !Ui.FrameText.Contains(ResourceOverlay.KeyTitle, FrameSurface.Lens);
                    return Expect(clicked && gone, "clicked " + clicked + ", shown " + Ui.OverlayShown(layer.Index)
                        + ", overlay painted " + (Ui.LensFrame?.Overlay is not null));
                });
            }
            LookCheck("map: overlays", "overlay key (every layer: inside the clear map, off its centre line; the wheel over it scrolls every row into view)", OverlayKeyFits);
            OverlayLayer probe = layers[0];
            foreach (OverlayLayer l in layers) if (l.Kind == OverlayKind.Suitability) probe = l;
            LookCheck("map: overlays", "overlay opacity (-: one tenth less)", () => OpacityStep(probe, GameUi.OverlayOpacityDown, -1));
            LookCheck("map: overlays", "overlay opacity (+: one tenth more)", () => OpacityStep(probe, GameUi.OverlayOpacityUp, +1));
            if (S.Knowledge.AllKnown == false)
                LookCheck("map: knowledge", "the overlay draws nothing on undiscovered ground", () =>
                {
                    foreach (OverlayLayer l in layers) if (!Ui.OverlayShown(l.Index)) h.ClickControl(l.Control);
                    h.Idle(1);
                    if (S.Overlay is not { } bake) return (GateResult.Fail, "no overlay bake");
                    if (Ui.LensFrame?.Overlay is not { } f) return (GateResult.Fail, "the overlay was not painted");
                    int unknown = 0, withheld = 0;
                    for (int n = 0; n < bake.NodeCount; n++) if (!S.Knowledge.Known(n)) unknown++;
                    foreach (OverlayLayer l in layers)
                    {
                        withheld += bake.Withheld(l.Index);
                        for (int n = 0; n < bake.NodeCount; n++)
                            if (bake.Level(l.Index, n) != ResourceOverlay.NotDrawn && !S.Knowledge.Known(n))
                                return (GateResult.Fail, l.Name + " draws undiscovered node " + n.ToString(CultureInfo.InvariantCulture));
                        if (f.Nodes[l.Index] > bake.Drawn(l.Index))
                            return (GateResult.Fail, l.Name + " painted " + f.Nodes[l.Index].ToString(CultureInfo.InvariantCulture) + " nodes; the bake draws " + bake.Drawn(l.Index).ToString(CultureInfo.InvariantCulture));
                    }
                    HideAllOverlays();
                    return unknown > 0
                        ? (GateResult.Pass, unknown.ToString(CultureInfo.InvariantCulture) + " undiscovered nodes, none drawn; "
                            + withheld.ToString(CultureInfo.InvariantCulture) + " known values not judged (their reach is undiscovered)")
                        : (GateResult.Fail, "the knowledge hides no node");
                });
            LookCheck("map: overlays", "overlays (fold: its tab)", () =>
            {
                HideAllOverlays();
                if (!Ui.OverlayControlsOpen) h.ClickControl(GameUi.OverlaysControl);
                bool clicked = h.ClickControl(GameUi.OverlaysControl);
                h.Idle(1);
                return Expect(clicked && !Ui.OverlayControlsOpen && Ui.Controls.Find(layers[0].Control) is null,
                    "clicked " + clicked + ", open " + Ui.OverlayControlsOpen);
            });
            CloseEverything();
        }

        /// <summary>P9b: with every layer shown (each through its checkbox), the key lies inside the clear map and off its
        /// centre line (a margin either side: where the camera frames what the player focuses on); the wheel over it — the
        /// pointer there first, as a player's is — scrolls it a row a notch to its last row, every row drawn at some scroll,
        /// and never zooms the map; then it scrolls back to its top and the layers are hidden again.</summary>
        private (GateResult, string) OverlayKeyFits()
        {
            if (!Ui.OverlayControlsOpen) h.ClickControl(GameUi.OverlaysControl);
            foreach (OverlayLayer l in Ui.OverlayLayers) if (!Ui.OverlayShown(l.Index)) h.ClickControl(l.Control);
            h.Idle(1);
            try
            {
                if (Ui.LensFrame?.Overlay?.Key is not { } key) return (GateResult.Fail, "the key was not painted");
                RectD map = Ui.MapRect;
                double band = Sim.Ui.ViewModel.PanelLayout.Margin * Ui.Scale;
                if (key.Rect.X < map.X || key.Rect.Y < map.Y || key.Rect.Right > map.Right || key.Rect.Bottom > map.Bottom)
                    return (GateResult.Fail, "the key " + key.Rect + " leaves the clear map " + map);
                if (key.Rect.Y < map.CenterY + band && key.Rect.Bottom > map.CenterY - band)
                    return (GateResult.Fail, "the key " + key.Rect + " lies over the clear map's centre line y " + map.CenterY.ToString("0.0", CultureInfo.InvariantCulture));
                double zoom = Ui.Camera.TargetZoom;
                (double x, double y) = (Math.Round(key.Rect.CenterX), Math.Round(key.Rect.CenterY));
                h.MoveTo(x, y);
                var seen = new SortedSet<int>();
                int notches = 0, rows = key.Rows.Count, max = key.MaxScroll;
                while (true)
                {
                    key = Ui.LensFrame!.Overlay!.Key!;
                    for (int r = key.Scroll; r < key.Scroll + key.RowsDrawn; r++) seen.Add(r);
                    if (key.Scroll >= key.MaxScroll || notches > rows) break;
                    int before = key.Scroll;
                    h.Wheel(x, y, -1);
                    notches++;
                    if (Ui.LensFrame?.Overlay?.Key is not { } next || next.Scroll != before + GameUi.OverlayKeyRowsPerNotch)
                        return (GateResult.Fail, "a notch over the key at row " + before.ToString(CultureInfo.InvariantCulture) + " did not scroll it a row");
                }
                bool zoomed = Ui.Camera.TargetZoom != zoom;
                h.Wheel(x, y, max + 1);   // back to its top
                bool top = Ui.LensFrame?.Overlay?.Key?.Scroll == 0;
                return Expect(seen.Count == rows && !zoomed && top, rows.ToString(CultureInfo.InvariantCulture) + " rows, "
                    + seen.Count.ToString(CultureInfo.InvariantCulture) + " drawn scrolling through "
                    + notches.ToString(CultureInfo.InvariantCulture) + " notches (the box " + key.Rect.H.ToString("0.0", CultureInfo.InvariantCulture)
                    + " px of the clear map's " + map.H.ToString("0.0", CultureInfo.InvariantCulture) + "), map zoomed " + zoomed + ", back at the top " + top);
            }
            finally { HideAllOverlays(); }
        }

        /// <summary>Hides every shown overlay layer through its checkbox (the controls unfolded for it).</summary>
        private void HideAllOverlays()
        {
            bool any = false;
            foreach (OverlayLayer l in Ui.OverlayLayers) any |= Ui.OverlayShown(l.Index);
            if (!any) return;
            if (!Ui.OverlayControlsOpen) h.ClickControl(GameUi.OverlaysControl);
            foreach (OverlayLayer l in Ui.OverlayLayers) if (Ui.OverlayShown(l.Index)) h.ClickControl(l.Control);
            h.Idle(1);
        }

        /// <summary>The layer as the last frame drew it, checked: shown; drawn by the lens (unless its channel cannot be
        /// evaluated on this map, which its key then says); every node it draws known; the key painted with its name, its
        /// measure, its goods (a channel) and the four status rows, its runs in FrameText (lens); no tooltip window.</summary>
        private (GateResult, string) OverlayLayerShown(OverlayLayer layer)
        {
            if (!Ui.OverlayShown(layer.Index)) return (GateResult.Fail, "the checkbox did not show the layer");
            if (S.Overlay is not { } bake) return (GateResult.Fail, "no overlay bake");
            if (Ui.LensFrame?.Overlay is not { Key: { } key } f) return (GateResult.Fail, "the overlay or its key was not painted");
            bool evaluated = layer.Kind != OverlayKind.Channel || bake.ChannelsUnavailable is null;
            if (evaluated && !(f.Layers.Contains(layer.Index) && f.Rects[layer.Index] > 0 && bake.Drawn(layer.Index) > 0))
                return (GateResult.Fail, "the lens drew no rect of the layer");
            for (int n = 0; n < bake.NodeCount; n++)
                if (bake.Level(layer.Index, n) != ResourceOverlay.NotDrawn && !S.Knowledge.Known(n))
                    return (GateResult.Fail, "draws undiscovered node " + n.ToString(CultureInfo.InvariantCulture));
            var want = new List<string> { layer.Name, layer.Measure };
            if (layer.Kind == OverlayKind.Channel)
                foreach (OverlayGood g in layer.Goods) want.Add(g.Name);
            string all = string.Join("\n", key.Rows);
            foreach (string w in want) if (!all.Contains(w, StringComparison.Ordinal)) return (GateResult.Fail, "the key does not say '" + w + "'");
            if (layer.Kind == OverlayKind.Channel && !all.Contains(ResourceOverlay.AbundanceStatement, StringComparison.Ordinal))
                return (GateResult.Fail, "the key does not state '" + ResourceOverlay.AbundanceStatement + "'");
            if (!evaluated && !all.Contains("Not evaluated", StringComparison.Ordinal)) return (GateResult.Fail, "an unevaluated channel is not said to be");
            foreach (string status in new[] { "Discovered:", "Present:", "Accessible:", "Extractable:" })
            {
                bool found = false;
                foreach (string r in key.Rows) found |= r.StartsWith(status, StringComparison.Ordinal);
                if (!found) return (GateResult.Fail, "the key has no '" + status + "' row");
            }
            if (!Ui.FrameText.Contains(ResourceOverlay.KeyTitle, FrameSurface.Lens) || !Ui.FrameText.Contains(layer.Name, FrameSurface.Lens))
                return (GateResult.Fail, "the key's title or the layer's name is not in FrameText (lens): "
                    + string.Join(" | ", Ui.FrameText.Texts(FrameSurface.Lens).Where(t => t.Contains("channel", StringComparison.OrdinalIgnoreCase) || t.Contains("overlay", StringComparison.OrdinalIgnoreCase))));
            if (h.Gui.LastTooltipWindows.Count > 0) return (GateResult.Fail, "a tooltip window: " + h.Gui.LastTooltipWindows[0]);
            RectD map = Ui.MapRect;
            if (key.Rect.X < map.X || key.Rect.Y < map.Y || key.Rect.Right > map.Right || key.Rect.Bottom > map.Bottom)
                return (GateResult.Fail, "the key " + key.Rect + " leaves the clear map " + map);
            return (GateResult.Pass, (evaluated ? bake.Drawn(layer.Index).ToString(CultureInfo.InvariantCulture) + " nodes drawn in "
                + f.Rects[layer.Index].ToString(CultureInfo.InvariantCulture) + " runs" : "not evaluated here: " + bake.ChannelsUnavailable)
                + ", " + key.Rows.Count.ToString(CultureInfo.InvariantCulture) + " key rows");
        }

        /// <summary>One opacity step through <paramref name="control"/> with <paramref name="probe"/> shown: the opacity moves
        /// by <paramref name="dir"/> tenths, and the deepest fill the layer draws is the opacity's alpha (the top level is
        /// drawn at opacity × 1). The step is first made pressable (one step the other way at a bound).</summary>
        private (GateResult, string) OpacityStep(OverlayLayer probe, string control, int dir)
        {
            if (!Ui.OverlayControlsOpen) h.ClickControl(GameUi.OverlaysControl);
            if (!Ui.OverlayShown(probe.Index)) h.ClickControl(probe.Control);
            if (dir < 0 && Ui.OverlayOpacity <= GameUi.OverlayOpacityMin / 10.0 + 1e-9) h.ClickControl(GameUi.OverlayOpacityUp);
            if (dir > 0 && Ui.OverlayOpacity >= GameUi.OverlayOpacityMax / 10.0 - 1e-9) h.ClickControl(GameUi.OverlayOpacityDown);
            h.Idle(1);
            double before = Ui.OverlayOpacity;
            bool clicked = h.ClickControl(control);
            h.Idle(1);
            double after = Ui.OverlayOpacity;
            int deepest = DeepestOverlayAlpha();
            int want = (int)Math.Round(255.0 * after);
            h.ClickControl(probe.Control);   // hide the probe again
            h.Idle(1);
            bool moved = Math.Abs(after - before - dir / 10.0) < 1e-9;
            return Expect(clicked && moved && deepest == want, "clicked " + clicked + ", opacity "
                + before.ToString("0.0", CultureInfo.InvariantCulture) + " -> " + after.ToString("0.0", CultureInfo.InvariantCulture)
                + ", deepest fill alpha " + deepest.ToString(CultureInfo.InvariantCulture) + " (want " + want.ToString(CultureInfo.InvariantCulture) + ")");
        }

        /// <summary>The largest fill alpha among the overlay layers' rects of the last lens paint (they are its first
        /// commands: the overlay is drawn under everything else).</summary>
        private int DeepestOverlayAlpha()
        {
            if (Ui.LensFrame?.Overlay is not { } f) return -1;
            int rects = 0;
            foreach (int r in f.Rects) rects += r;
            int deepest = -1;
            IReadOnlyList<DrawCmd> cmds = Ui.LastLensCommands;
            for (int i = 0; i < rects && i < cmds.Count; i++)
                if (cmds[i] is RectCmd { Fill: { } fill }) deepest = Math.Max(deepest, fill.A);
            return deepest;
        }
    }
}
