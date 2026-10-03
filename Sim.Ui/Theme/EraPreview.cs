using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Ui.Ages;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Theme;

/// <summary>
/// HEADLESS ERA PREVIEWS (sim-ui --era-preview [dir]; docs/architecture/era-ui.md) — for each of the
/// nine Ages, on the SAME world state with only the player's Age differing, the three surfaces the
/// era language shows most: the Technology tree, the capital's Age panel, and a chrome sample (the
/// game screen at 1280×800: map, status band, selection card, docked Age panel, command bar and a
/// context panel holding an illustrative action list and chart). The world is the research preview's
/// real stepped seed-42 world; the ONLY constructed state is the player's Age row (and the one
/// transition row that says when it was entered), which is exactly the variable under study. The era
/// is derived from that world through <see cref="UiEras.Of"/> — the game's own derivation — and every
/// surface is painted by the game's own painters into the same DrawList the ImGui backend replays.
/// Deterministic: the same build writes byte-identical SVGs (their SHA-256 are logged).
/// </summary>
public static class EraPreview
{
    public const double TreeW = 1600, TreeH = 1000, AgeW = 560, AgeH = 1000, ChromeW = 1280, ChromeH = 800;

    /// <summary>The stepped world every era is painted on (the research preview's).</summary>
    public static UiSession Session() => ProgressionPreview.SteppedSession();

    /// <summary><paramref name="w"/> with ONLY the player's Age changed: its Age row, and (above the
    /// founding Age) the transition row recording that it was entered this turn.</summary>
    public static WorldState WorldAt(WorldState w, AgeContent ages, PolityId player, int age)
    {
        WorldState c = w.Clone();
        int surge = ages.Surges[0].Key;
        long turn = c.Clock.Turn;
        var row = new AgeStateRow(player, age, turn, surge, turn);
        int at = -1;
        for (int i = 0; i < c.AgeStates.Count; i++) if (c.AgeStates[i].Polity.Value == player.Value) at = i;
        if (at >= 0) c.AgeStates[at] = row; else c.AgeStates.Add(row);
        if (age > ages.FoundingAge) c.AgeTransitions.Add(new AgeTransitionRow(player, age - 1, age, surge, turn - 1, turn));
        return c;
    }

    /// <summary>The theme a world presents — the game's derivation.</summary>
    public static EraTheme ThemeOf(UiSession s, IReadOnlyWorldState w) =>
        EraThemes.For(UiEras.Of(w, s.Config.Ages, UiPlayer.Empire));

    // ------------------------------------------------------------------ the three surfaces

    /// <summary>The Technology tree exactly as it opens (zoom 1, the lagging branch's frontier, nothing selected).</summary>
    public static string TreeSvg(UiSession s, WorldState w, string? fontDir)
    {
        PolityId me = UiPlayer.Empire;
        var screen = new ProgressionScreen(s.Config.Research!, me)
        {
            Theme = ThemeOf(s, w),
            Age = AgePanelModel.Build(w, s.Config.Ages, [], me),
        };
        screen.Refresh(w);
        screen.Paint(TreeW, TreeH, ApproxTextMeasure.Instance);   // first frame: frames the lagging branch
        return SvgWriter.Write(screen.Paint(TreeW, TreeH, ApproxTextMeasure.Instance), TreeW, TreeH, fontDir);
    }

    /// <summary>The capital's Age panel on the era's ground.</summary>
    public static string AgeSvg(UiSession s, WorldState w, string? fontDir)
    {
        EraTheme t = ThemeOf(s, w);
        var screen = new AgeScreen(UiPlayer.Empire) { Theme = t };
        screen.Refresh(w, s.Config.Ages, s.Config.UnitFamilies, []);
        var d = new DrawList();
        PanelFrame.Field(d, new RectD(0, 0, AgeW, AgeH), t, 1);
        screen.PaintPanel(d, ApproxTextMeasure.Instance, new RectD(20, 20, AgeW - 40, AgeH - 40), CapitalName(s));
        return SvgWriter.Write(d, AgeW, AgeH, fontDir);
    }

    /// <summary>The chrome sample: the game screen at its design size, chrome in the era's hand over
    /// the (era-invariant) parchment map.</summary>
    public static string ChromeSvg(UiSession s, WorldState w, string? fontDir, string? terrainDataUri)
    {
        EraTheme t = ThemeOf(s, w);
        PolityId me = UiPlayer.Empire;
        var d = new DrawList();
        ITextMeasure m = ApproxTextMeasure.Instance;

        // The map lens at World zoom (the parchment substrate is frozen; only what is drawn on it reads the world).
        double size = w.Terrain!.Size;
        double zoom = Math.Min(ChromeW, ChromeH - 104) / size;
        double cx = size / 2, cy = size / 2;
        WorldProjection p = WorldProjection.Build(w, s.Config, id => s.Names.Name(id), me);
        int capital = EmpireQuery.TryGetCapital(w, me, out SettlementId cap) ? cap.Value : -1;
        WorldLens.Paint(d, m, p, WorldLens.LevelFor(zoom, ChromeW, ChromeH, size),
            (x, y) => ((x - cx) * zoom + ChromeW / 2, (y - cy) * zoom + ChromeH / 2 + 4), zoom, new RectD(0, 0, ChromeW, ChromeH), capital,
            ink: MapInk.For(t));   // the era's map ink, exactly as the game derives it

        // The four chrome windows: the same furniture painter the game runs behind its ImGui windows.
        float fh = Art.UiTheme.FrameHeightPx;
        foreach (ChromeElement e in ChromeGeometry.Elements) ChromeFurniture.Paint(d, t, e, fh);
        HudModel hud = HudModel.From(w, capital, null, capital >= 0 ? s.Names.Name(capital) : null, s.Config);

        // Status band.
        PanelRect sb = PanelLayout.Status;
        double sx = sb.X + PanelLayout.Margin + 2, sy = sb.Y + PanelLayout.Margin;
        foreach (string figure in new[] { hud.ClockLine, hud.WorldPopulationFigure + " " + hud.SettlementCountFigure, hud.WorldFoodFigure })
        {
            d.Write(t, sx, sy, figure, 17, t.Ink.Text, TextAlign.Left, FontRole.Numeric);
            sx += m.Width(t, figure, 17, FontRole.Numeric) + 28;
        }
        var knowledge = new RectD(sx, sb.Y + 9, 150, 29);
        PanelFrame.Paint(d, knowledge, t, 21, FrameKind.Button);
        d.Write(t, knowledge.CenterX, knowledge.Y + 6, "Knowledge [K]", 17, t.Ink.Text, TextAlign.Center);
        d.Write(t, sb.Width - 22, sy, "Age " + AgePanelModel.Numeral(t.Ordinal) + " - " + p.PlayerAgeName, 15, t.Material.Accent, TextAlign.Right, FontRole.Heading);

        // Selection card.
        PanelRect sc = PanelLayout.Selection;
        double cy0 = sc.Y + PanelLayout.Margin;
        d.Write(t, sc.X + 14, cy0, ThemeText.Fit(m, t, hud.TitleLine, 18, sc.Width - 28, FontRole.Heading), 18, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        cy0 += fh + 6;
        foreach (string line in new[] { hud.PopulationLine, hud.FoodLine, hud.HappinessLine + "   " + hud.GrievanceLine })
        {
            d.Write(t, sc.X + 14, cy0, ThemeText.Fit(m, t, line, 15, sc.Width - 28, FontRole.Numeric), 15, t.Ink.TextSoft, TextAlign.Left, FontRole.Numeric);
            cy0 += t.Type.Line(15) + 3;
        }

        // The docked Age panel (the capital is selected at turn 1, as in the game).
        var age = new AgeScreen(me) { Theme = t };
        age.Refresh(w, s.Config.Ages, s.Config.UnitFamilies, []);
        age.PaintPanel(d, m, new RectD(12, 202, 440, ChromeH - 202 - 56 - 12), capital >= 0 ? s.Names.Name(capital) : "Capital");

        // Command bar.
        ScreenRect end = ChromeGeometry.EndTurnButton;
        PanelFrame.Paint(d, new RectD(end.X, end.Y, end.Width, end.Height), t, 22, FrameKind.Button, ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.2), t.Material.Accent, 1.2);
        d.Write(t, end.CenterX, end.Y + 6, "End Turn [Space]", 17, t.Ink.Text, TextAlign.Center);
        for (int i = 0; i < GameSections.PlayerOrder.Count; i++)
        {
            ScreenRect nav = ChromeGeometry.NavButton(i);
            bool open = i == 1;
            // The open section reads as pressed (ImGuiCol.ButtonActive in the game: UiTheme.StyleFor).
            PanelFrame.Paint(d, new RectD(nav.X, nav.Y, nav.Width, nav.Height), t, 30 + i, FrameKind.Button,
                open ? ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.34) : t.Material.PanelRaised, open ? t.Material.Accent : t.Material.Border, open ? 1.2 : 0.8);
            string label = GameSections.Label(GameSections.PlayerOrder[i]);
            double ns = ThemeText.FitSize(m, t, label, 15, nav.Width - 10, FontRole.Caps);
            d.Write(t, nav.CenterX, nav.Y + 7 + (15 - ns) / 2, ThemeText.Fit(m, t, label, ns, nav.Width - 8, FontRole.Caps), ns, t.Ink.Text, TextAlign.Center, FontRole.Caps);
        }
        double tx = ChromeGeometry.TerritoryToggleX, ty = ChromeGeometry.ButtonRow.Y + 6;
        PanelFrame.Paint(d, new RectD(tx, ty, 18, 18), t, 40, FrameKind.Chip, t.Material.PanelRaised, t.Material.Border, 1.0);
        EraMarks.Check(d, tx + 9, ty + 9, 12, t.Ink.Text, Math.Max(1.4, t.Icons.StrokePx * 0.8));
        d.Write(t, tx + 26, ty, "territory", 17, t.Ink.Text);

        // Context panel: an illustrative action list and chart in the era's controls.
        PaintContextSample(d, m, t);
        string svg = SvgWriter.Write(d, ChromeW, ChromeH, fontDir);
        if (terrainDataUri is null) return svg;
        // The parchment map under everything (era-invariant: the bible's substrate).
        string img = string.Create(CultureInfo.InvariantCulture,
            $"<image href=\"{terrainDataUri}\" x=\"{(0 - cx) * zoom + ChromeW / 2:0.##}\" y=\"{(0 - cy) * zoom + ChromeH / 2 + 4:0.##}\" width=\"{size * zoom:0.##}\" height=\"{size * zoom:0.##}\" preserveAspectRatio=\"none\"/>");
        int at = svg.IndexOf("</style>\n", StringComparison.Ordinal);
        return svg.Insert(at < 0 ? 0 : at + 9, "<rect x=\"0\" y=\"0\" width=\"1280\" height=\"800\" fill=\"#7f97a0\"/>\n" + img + "\n");
    }

    /// <summary>The baseline activities of turn 1 (ADR-033 D1) as an ILLUSTRATIVE action list, and an
    /// illustrative series — the controls and the chart drawn at the era's granularity and
    /// sophistication. Labelled as a sample; the live action surface reads the simulation.</summary>
    private static void PaintContextSample(DrawList d, ITextMeasure m, EraTheme t)
    {
        PanelRect c = PanelLayout.Context;
        float fh = Art.UiTheme.FrameHeightPx;
        ScreenRect head = ChromeGeometry.HeaderRow(ChromeGeometry.Context, fh);
        d.Write(t, head.X + 2, head.Y + 4, "Actions", 19, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        ScreenRect close = ChromeGeometry.CloseButton(ChromeGeometry.Context, fh);
        var cr = new RectD(close.X, close.Y, close.Width, close.Height);
        PanelFrame.Paint(d, cr, t, 50, FrameKind.Button);
        EraMarks.Close(d, t, cr, t.Ink.Text, 51);
        double x = c.X + 18, w = c.Width - 36, y = ChromeGeometry.ContentTop(ChromeGeometry.Context, fh) + 8;
        d.Write(t, x, y, "SAMPLE - ILLUSTRATIVE ACTION LIST", 10.5, t.Ink.TextDim, TextAlign.Left, FontRole.Caps);
        y += 20;
        (string Name, string Note, double Share, bool Order)[] rows =
        [
            ("Gathering", "baseline - food", 0.55, true),
            ("Hunting & fishing", "baseline", 0.15, true),
            ("Gathering wood & stone", "baseline", 0.10, true),
            ("Crafts & toolmaking", "baseline", 0.12, true),
            ("Building", "baseline - paths, shelter", 0.08, true),
            ("Basic fighting", "capability - no order", 0.0, false),
        ];
        int k = 0;
        foreach ((string name, string note, double share, bool order) in rows)
        {
            double rowH = t.Density.Level <= 2 ? 44 : t.Density.Level <= 4 ? 40 : 36;
            var row = new RectD(x, y, w, rowH);
            PanelFrame.Paint(d, row, t, 60 + k, FrameKind.Card, t.Material.PanelRaised, t.Material.Hairline, 0.8);
            EraMarks.State(d, t, row.X + 16, row.Y + rowH / 2, 5.5, order ? MarkKind.Available : MarkKind.Locked, 0, 70 + k);
            bool noted = t.Density.Level >= 3;
            d.Write(t, row.X + 30, row.Y + (noted ? 3 : rowH / 2 - 10), name, 15, t.Ink.Text, TextAlign.Left, FontRole.Heading);
            if (noted) d.Write(t, row.X + 30, row.Y + rowH - 15, note, 10, t.Ink.TextDim);
            if (order)
            {
                var track = new RectD(row.Right - 150, row.Y + rowH / 2 - 4, 104, 8);
                EraMarks.Progress(d, t, track, share, t.Semantic.Food, 80 + k);
                double gx = track.X + track.W * share, g = Math.Max(5, t.Controls.GrabPx * 0.6);
                if (t.Controls.Granularity >= ControlGranularity.Standard)
                    PanelFrame.Paint(d, new RectD(gx - g / 2, track.CenterY - g, g, 2 * g), t, 90 + k, FrameKind.Button, t.Material.PanelRaised, t.Material.Border, 0.9);
                string pct = Math.Round(share * 100).ToString("0", CultureInfo.InvariantCulture) + (t.Density.Level >= 3 ? " %" : "");
                d.Write(t, row.Right - 10, row.Y + rowH / 2 - 8, pct, 13, t.Ink.TextSoft, TextAlign.Right, FontRole.Numeric);
            }
            y += rowH + t.Density.Gap * 0.6;
            k++;
        }
        y += 8;
        d.Write(t, x, y, "SAMPLE - POPULATION (ILLUSTRATIVE SERIES)", 10.5, t.Ink.TextDim, TextAlign.Left, FontRole.Caps);
        y += 18;
        PaintChartSample(d, m, t, new RectD(x, y, w, Math.Min(150, c.Y + c.Height - y - 20)));
    }

    /// <summary>A fixed illustrative series at the era's chart sophistication: counted pebbles (A1),
    /// plain bars (A2–A3), bars on an axis with ticks (A4–A5), a labelled line (A6–A7), a gridded,
    /// labelled, filled line (A8–A9).</summary>
    private static void PaintChartSample(DrawList d, ITextMeasure m, EraTheme t, RectD r)
    {
        if (r.H < 40) return;
        double[] series = [96, 101, 99, 108, 115, 112, 121, 130, 127, 138, 146, 151];
        double lo = 80, hi = 160;
        Rgba ink = t.Semantic.Food, axis = t.Material.Border;
        int s = t.Charts.Sophistication;
        double X(int i) => r.X + 8 + (r.W - 16) * i / (series.Length - 1.0);
        double Y(double v) => r.Bottom - 8 - (r.H - 16) * (v - lo) / (hi - lo);
        if (s == 0)
        {
            // pebbles: one per 20 people, in a column per period (the last six)
            for (int i = 0; i < 6; i++)
            {
                int n = (int)Math.Round(series[series.Length - 6 + i] / 20.0);
                double colX = r.X + 20 + i * (r.W - 40) / 5.0;
                for (int k = 0; k < n; k++)
                    d.Circle(colX + FrameNoise.S(i, 141, k) * 2, r.Bottom - 10 - k * 13, 4.6 + FrameNoise.U(i, 142, k), ThemeColor.Alpha(ink, 0.85));
            }
            return;
        }
        if (s <= 2)
        {
            double bw = (r.W - 16) / series.Length * 0.6;
            for (int i = 0; i < series.Length; i++)
                d.Rect(new RectD(X(i) - bw / 2, Y(series[i]), bw, r.Bottom - 8 - Y(series[i])), ThemeColor.Alpha(ink, 0.8));
            if (s == 2)
            {
                d.Line(r.X + 4, r.Bottom - 8, r.Right - 4, r.Bottom - 8, axis, 1.0);
                for (int i = 0; i < series.Length; i++) d.Line(X(i), r.Bottom - 8, X(i), r.Bottom - 4, axis, 0.8);
            }
            return;
        }
        if (t.Charts.GridLines)
            for (int g = 0; g <= 4; g++)
            {
                double gy = r.Bottom - 8 - (r.H - 16) * g / 4.0;
                d.Line(r.X + 4, gy, r.Right - 4, gy, ThemeColor.Alpha(t.Material.Hairline, 0.6), 0.6);
            }
        if (s >= 4)
        {
            var pts = new List<(double, double)> { (X(0), r.Bottom - 8) };
            for (int i = 0; i < series.Length; i++) pts.Add((X(i), Y(series[i])));
            pts.Add((X(series.Length - 1), r.Bottom - 8));
            // the area is drawn as thin vertical strips (the ImGui backend fills convex shapes only)
            for (int i = 0; i + 1 < series.Length; i++)
                d.Polygon([(X(i), r.Bottom - 8), (X(i), Y(series[i])), (X(i + 1), Y(series[i + 1])), (X(i + 1), r.Bottom - 8)], ThemeColor.Alpha(ink, 0.14));
        }
        for (int i = 0; i + 1 < series.Length; i++) d.Line(X(i), Y(series[i]), X(i + 1), Y(series[i + 1]), ink, t.Charts.LineWidth + 0.6);
        d.Line(r.X + 4, r.Bottom - 8, r.Right - 4, r.Bottom - 8, axis, 1.0);
        if (t.Charts.Ticks) for (int i = 0; i < series.Length; i++) d.Line(X(i), r.Bottom - 8, X(i), r.Bottom - 5, axis, 0.7);
        if (t.Charts.Labels)
        {
            d.Write(t, r.X + 6, r.Y, hi.ToString("0", CultureInfo.InvariantCulture), 10.5, t.Ink.TextDim, TextAlign.Left, FontRole.Numeric);
            d.Write(t, r.Right - 6, Y(series[^1]) - 16, series[^1].ToString("0", CultureInfo.InvariantCulture), 11.5, ink, TextAlign.Right, FontRole.Numeric);
        }
    }

    private static string CapitalName(UiSession s) =>
        EmpireQuery.TryGetCapital(s.World, UiPlayer.Empire, out SettlementId cap) ? s.Names.Name(cap.Value) : "Capital";

    /// <summary>The file stem for an era: "era-1-prehistoric".</summary>
    public static string Stem(UiEra era) => "era-" + era.Ordinal().ToString(CultureInfo.InvariantCulture) + "-" + era.ToString().ToLowerInvariant();

    /// <summary>Writes the 27 SVGs (and the log with each SVG's SHA-256) into <paramref name="outDir"/>.</summary>
    public static IReadOnlyList<string> Run(string outDir, string? fontDir)
    {
        Directory.CreateDirectory(outDir);
        UiSession s = Session();
        AgeContent ages = s.Config.Ages!;
        PolityId me = UiPlayer.Empire;
        string terrain = AgePreview.TerrainDataUri(s.World);
        var written = new List<string>();
        var log = new List<string>
        {
            "era preview: seed 42, 256 px, 4 settlements; the research preview's stepped world at turn "
                + s.World.Clock.Turn.ToString(CultureInfo.InvariantCulture) + " (world hash " + WorldHash.ComputeHex(s.World) + ")",
            "only the player's Age row (and one transition row) differs between eras; each era is derived with UiEras.Of",
        };
        foreach (UiEra era in UiEras.All)
        {
            WorldState w = WorldAt(s.World, ages, me, era.Ordinal());
            UiEra derived = UiEras.Of(w, ages, me);
            if (derived != era) throw new InvalidOperationException($"era {era} derived as {derived}");
            foreach ((string kind, string svg) in new[] { ("tree", TreeSvg(s, w, fontDir)), ("age", AgeSvg(s, w, fontDir)), ("chrome", ChromeSvg(s, w, fontDir, terrain)) })
            {
                string path = Path.Combine(outDir, Stem(era) + "-" + kind + ".svg");
                File.WriteAllText(path, svg);
                written.Add(path);
                log.Add(Stem(era) + "-" + kind + ".svg  sha256 " + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(svg)))
                    + "  theme " + ThemeCanon.Hash(EraThemes.For(derived))[..16]);
            }
        }
        File.WriteAllLines(Path.Combine(outDir, "preview-log.txt"), log);
        return written;
    }
}
