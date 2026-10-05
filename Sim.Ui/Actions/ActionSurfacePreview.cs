using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Ui.Ages;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Actions;

/// <summary>
/// HEADLESS PREVIEWS OF THE REAL ACTION SURFACE (sim-ui --action-preview [dir];
/// docs/architecture/action-surface.md). Each shot paints, through the SVG writer, the SAME
/// <see cref="ActionSurfaceScreen"/> the game replays through ImGui, over a model built by
/// <see cref="ActionSurface.Build"/> from a real session — never the era theme's illustrative sample:
/// <list type="number">
/// <item>TURN 1 on the canonical founded world (seed 42, 1024 px, 12 settlements, A1): the whole game screen
///   with the POLICY panel open, and the surface alone at full height.</item>
/// <item>A LATER STATE: the same world with the Taxation civic and a road class known (their prerequisite
///   closures as ResearchCompleted rows — the constructed part of the rig), the player's Age row set to A3, then
///   played through the real session: Root and tuber cultivation researched to completion through the order
///   pathway, a levy declared, a granary queued. Painted at A3 and, with only the Age row changed, at A8.</item>
/// </list>
/// THE TAX GATE IS READ FROM CONTENT (<see cref="TaxationGate"/>): the rigs complete every node named by sim.json
/// <c>governance.taxationRequires</c> (since R5 the single Civics node <c>taxation</c>) with its prerequisite closure,
/// and stand at <see cref="RigAge"/> = Age III or later, because Taxation is an A3 capability (Director 2026-10-05 §7).
/// Until 2026-10-05 they granted <c>arithmetic_babylonian</c> — a refinement that stopped opening the gate at R5 — and
/// crashed ("could not declare a 20% levy"); <c>PreviewToolProcessTests</c> now run both tools as separate processes.
/// Deterministic: the same build writes byte-identical SVGs (their SHA-256 are logged).
/// </summary>
public static class ActionSurfacePreview
{
    public const double ScreenW = 1280, ScreenH = 800, PanelW = 396;

    /// <summary>One rendered state: the session, the world the surface reads (the session's, or a copy with
    /// only the Age row changed) and the settlement selected.</summary>
    public sealed record State(string Stem, string Description, UiSession Session, WorldState World, int Selected);

    private static readonly PolityId Me = UiPlayer.Empire;

    /// <summary>The turn-1 state: the canonical founded world, the capital selected (as the game launches).</summary>
    public static State TurnOne()
    {
        UiSession s = UiSession.Start(42);
        return new State("turn-1-a1", "Turn 1, canonical founded world (seed 42, 12 settlements), Age I", s, s.World, Capital(s.World));
    }

    /// <summary>The Age the later rig stands at: Age III, the Bronze Age — the first Age in which the Taxation
    /// capability is operational (Director 2026-10-05 §7), so the rig's levy is legal under the Age half of the gate
    /// as well as the knowledge half.</summary>
    public const int RigAge = 3;

    /// <summary>
    /// THE TAX EDICT'S RESEARCH GATE, read from content: the ids of every research node named by sim.json
    /// <c>governance.taxationRequires</c>, in the expression's own order (R5: the single Civics node <c>taxation</c>,
    /// key 1007). Completing all of them — with their prerequisite closure (<see cref="WithAncestors"/>) — satisfies
    /// the expression whatever its AND/OR shape, because research requirements carry no NOT (monotone, ADR-029 §2.2).
    /// No node id is named here: when the content moves the gate, the rigs follow it.
    /// </summary>
    public static string[] TaxationGate(Sim.Core.Systems.SimConfig cfg)
    {
        ResearchContent research = cfg.Research ?? throw new InvalidOperationException("action preview rig: no research content");
        Sim.Core.Systems.GovernanceConfig governance = cfg.Governance
            ?? throw new InvalidOperationException("action preview rig: no governance content (sim.json governance)");
        Sim.Core.Systems.ClassMobility.Predicate requirement = ResearchContentLoader.ParseRequirement(
            research, governance.TaxationRequires, "sim.json governance.taxationRequires");
        var ids = new List<string>();
        foreach (int atom in requirement.AtomIds) ids.Add(research.Nodes[atom].Id);
        if (ids.Count == 0) throw new InvalidOperationException("action preview rig: governance.taxationRequires names no research node");
        return [.. ids];
    }

    /// <summary>Completes, for the player, every node in <paramref name="nodeIds"/> and its prerequisite closure
    /// (ResearchCompleted rows; a node already complete is not duplicated).</summary>
    private static void Know(WorldState w, ResearchContent research, params string[] nodeIds)
    {
        foreach (string id in WithAncestors(research, nodeIds))
        {
            ResearchNodeId key = research.Nodes[research.IndexOfId(id)].Key;
            if (!ResearchQuery.IsCompleted(w, Me, key)) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, key));
        }
    }

    /// <summary>The later rig (see the header), at <see cref="RigAge"/>.</summary>
    public static State LaterRig(int maxTurns = 80)
    {
        WorldState w = UiFounding.Found(42);
        SimConfigLike cfg = new(UiFounding.ProductionConfig());
        ResearchContent research = cfg.Research;
        AgeContent ages = cfg.Ages;
        Know(w, research, [.. TaxationGate(cfg.Config), "track_road"]);
        w = EraPreview.WorldAt(w, ages, Me, RigAge);
        UiSession s = UiSession.StartFrom(w, 42);
        Require(Governance.CanLevyTax(s.World, s.Config, Me),
            "open the tax gate (sim.json governance.taxationRequires completed, Age " + RigAge.ToString(CultureInfo.InvariantCulture) + ")");
        int capital = Capital(s.World);
        ResearchNodeId crop = research.Nodes[research.IndexOfId("root_crop")].Key;
        Require(s.EmitResearchOrder(crop), "choose Root and tuber cultivation");
        s.EndTurn();
        Require(s.EmitTaxOrder(20), "declare a 20% levy");
        Require(s.EmitConstructionOrder(capital, s.Config.Goods!.Projects![0].Id), "queue a granary");
        Require(s.EmitSectorOrders([45, 15, 15, 15, 10], capital), "set the capital's labour");
        for (int t = 0; t < maxTurns && !ResearchQuery.IsCompleted(s.World, Me, crop); t++) s.EndTurn();
        Require(ResearchQuery.IsCompleted(s.World, Me, crop), "Root and tuber cultivation completed within the rig's turns");
        // The turn after the crop is learned: the surface announces it and marks Farming as new.
        if (ResearchQuery.CheapestAvailable(s.World, research, Me, ResearchTree.Technology) is ResearchNodeId next) s.EmitResearchOrder(next);
        return new State("later-a3", "A later state: a crop, the Taxation civic and a road class known; Age III; the turn Farming appeared",
            s, s.World, capital);
    }

    /// <summary>R1 — THE RESEARCHED STATE: the canonical founded world with the prerequisite closures of agriculture
    /// (cereal_cultivation), pottery (pottery_open_fired), bronze casting (tin_bronze), taxation (the Taxation civic,
    /// <see cref="TaxationGate"/>), a road class (track_road) and the university (building.university and
    /// inst.university: medicine_hippocratic, geometry_axiomatic, cuneiform, stamp_seal, legal_code_roman) completed
    /// (ResearchCompleted rows — the constructed part of the rig), the Age row set to A4, then one End Turn through
    /// the real session. Every control it shows comes from the query: nothing here names a control.</summary>
    public static State ResearchedRig()
    {
        WorldState w = UiFounding.Found(42);
        SimConfigLike cfg = new(UiFounding.ProductionConfig());
        ResearchContent research = cfg.Research;
        Know(w, research, [.. TaxationGate(cfg.Config), "cereal_cultivation", "pottery_open_fired", "tin_bronze",
            "track_road", "medicine_hippocratic", "geometry_axiomatic", "cuneiform", "stamp_seal", "legal_code_roman"]);
        w = EraPreview.WorldAt(w, cfg.Ages, Me, 4);   // Age IV: past the Taxation Age (RigAge)
        UiSession s = UiSession.StartFrom(w, 42);
        Require(Governance.CanLevyTax(s.World, s.Config, Me), "open the tax gate in the researched rig");
        s.EndTurn();
        return new State("researched-a4", "Researched state: agriculture, pottery, bronze casting, taxation, a road class and the university's prerequisites known; Age IV",
            s, s.World, Capital(s.World));
    }

    /// <summary>The same state with only the player's Age row changed.</summary>
    public static State AtAge(State state, int age, string stem, string description)
    {
        AgeContent ages = state.Session.Config.Ages!;
        return state with { Stem = stem, Description = description, World = EraPreview.WorldAt(state.World, ages, Me, age) };
    }

    private sealed record SimConfigLike(Sim.Core.Systems.SimConfig Config)
    {
        public ResearchContent Research => Config.Research!;
        public AgeContent Ages => Config.Ages!;
    }

    private static void Require(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException("action preview rig: could not " + what);
    }

    private static int Capital(IReadOnlyWorldState w) =>
        EmpireQuery.TryGetCapital(w, Me, out SettlementId cap) ? cap.Value : -1;

    /// <summary>Every prerequisite-ancestor of the nodes, the nodes included, content order.</summary>
    public static string[] WithAncestors(ResearchContent research, params string[] nodeIds)
    {
        var seen = new bool[research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in nodeIds) stack.Push(research.IndexOfId(id));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int p in research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        var ids = new List<string>();
        for (int i = 0; i < seen.Length; i++) if (seen[i]) ids.Add(research.Nodes[i].Id);
        return [.. ids];
    }

    // ------------------------------------------------------------------ models and painting

    /// <summary>The theme the state's world presents (the game's derivation).</summary>
    public static EraTheme ThemeOf(State s) => EraThemes.For(UiEras.Of(s.World, s.Session.Config.Ages, Me));

    /// <summary>The surface model of a state, exactly as the game builds it.</summary>
    public static ActionSurfaceModel ModelOf(State s) =>
        ActionSurface.Build(new ActionSurfaceInput(s.World, s.Session.PreviousWorld, s.Session.Config, UiSession.ProductionEra(), Me,
            s.Selected, s.Session.QueuedOrders(), id => s.Session.Names.Name(id), ThemeOf(s)));

    private static ActionSurfaceScreen ScreenOf(State s)
    {
        var screen = new ActionSurfaceScreen { Theme = ThemeOf(s) };
        screen.Refresh(ModelOf(s), s.World, s.Session.Config, Me, id => s.Session.Names.Name(id));
        return screen;
    }

    /// <summary>The surface alone, on its panel, at full height.</summary>
    public static string PanelSvg(State s, string? fontDir)
    {
        EraTheme t = ThemeOf(s);
        ActionSurfaceScreen screen = ScreenOf(s);
        var body = new DrawList();
        double h = screen.Paint(body, ApproxTextMeasure.Instance, 24, 64, PanelW - 48);
        double height = Math.Ceiling(h + 64 + 32);
        var d = new DrawList();
        PanelFrame.Field(d, new RectD(0, 0, PanelW + 40, height + 40), t, 2);
        PanelFrame.Paint(d, new RectD(20, 20, PanelW, height), t, 3, FrameKind.Panel);
        d.Write(t, 34, 32, "Policy", 19, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        PanelFrame.Rule(d, new RectD(34, 58, PanelW - 28, Math.Max(2, t.Edge.BorderPx)), t, 4);
        foreach (DrawCmd c in Offset(body, 20, 6).Commands) d.Add(c);
        return SvgWriter.Write(d, PanelW + 40, height + 40, fontDir);
    }

    /// <summary>The whole game screen at its design size with the POLICY panel open on the surface.</summary>
    public static string ScreenSvg(State s, string? fontDir, string? terrainDataUri)
    {
        EraTheme t = ThemeOf(s);
        var d = new DrawList();
        ITextMeasure m = ApproxTextMeasure.Instance;
        IReadOnlyWorldState w = s.World;
        UiSession session = s.Session;

        double size = w.Terrain!.Size;
        double zoom = Math.Min(ScreenW, ScreenH - 104) / size;
        double cx = size / 2, cy = size / 2;
        WorldProjection p = WorldProjection.Build(w, session.Config, id => session.Names.Name(id), Me);
        WorldLens.Paint(d, m, p, WorldLens.LevelFor(zoom, ScreenW, ScreenH, size),
            (x, y) => ((x - cx) * zoom + ScreenW / 2, (y - cy) * zoom + ScreenH / 2 + 4), zoom, new RectD(0, 0, ScreenW, ScreenH), s.Selected,
            ink: MapInk.For(t));

        float fh = Art.UiTheme.FrameHeightPx;
        foreach (ChromeElement e in ChromeGeometry.Elements) ChromeFurniture.Paint(d, t, e, fh);
        HudModel hud = HudModel.From(w, s.Selected, null, s.Selected >= 0 ? session.Names.Name(s.Selected) : null, session.Config);
        ActionSurfaceModel model = ModelOf(s);
        AgePanelModel agePanel = AgePanelModel.Build(w, session.Config.Ages, session.QueuedOrders(), Me);

        // Status band: the clock and world figures, then research and the compact Age indicator (no panel over the map).
        PanelRect sb = PanelLayout.Status;
        double sx = sb.X + PanelLayout.Margin + 2, sy = sb.Y + PanelLayout.Margin;
        foreach (string figure in new[] { hud.ClockLine, hud.WorldPopulationFigure + " " + hud.SettlementCountFigure, hud.WorldFoodFigure })
        {
            d.Write(t, sx, sy, figure, 15, t.Ink.Text, TextAlign.Left, FontRole.Numeric);
            sx += m.Width(t, figure, 15, FontRole.Numeric) + 22;
        }
        // The research figure is the band's way into the trees (a button), then the compact Age indicator.
        ResearchFigure rf = StatusFigures.Research(model.Research);
        double rw = m.Width(t, rf.Text, 15) + 22;
        var research = new RectD(sx, sb.Y + 9, rw, 29);
        PanelFrame.Paint(d, research, t, 21, FrameKind.Button);
        d.Write(t, research.CenterX, research.Y + 6, rf.Text, 15, rf.Idle ? t.Semantic.Progress : t.Semantic.Active, TextAlign.Center);
        AgeFigure af = StatusFigures.Age(agePanel);
        double aw = Math.Min(m.Width(t, af.Text, 15) + 22, sb.Width - research.Right - 28);
        var ageChip = new RectD(research.Right + 14, sb.Y + 9, aw, 29);
        PanelFrame.Paint(d, ageChip, t, 23, FrameKind.Button);
        d.Write(t, ageChip.CenterX, ageChip.Y + 6, ThemeText.Fit(m, t, af.Text, 15, aw - 12), 15, af.Eligible ? t.Material.Accent : t.Ink.Text, TextAlign.Center);

        // Selection card.
        PanelRect sc = PanelLayout.Selection;
        double cy0 = sc.Y + PanelLayout.Margin;
        d.Write(t, sc.X + 14, cy0, ThemeText.Fit(m, t, hud.TitleLine, 18, sc.Width - 28, FontRole.Heading), 18, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        cy0 += fh + 6;
        foreach (string line in new[] { hud.PopulationLine, hud.FoodLine, hud.HappinessLine, hud.GrievanceLine })
        {
            d.Write(t, sc.X + 14, cy0, ThemeText.Fit(m, t, line, 15, sc.Width - 28, FontRole.Numeric), 15, t.Ink.TextSoft, TextAlign.Left, FontRole.Numeric);
            cy0 += t.Type.Line(15) + 3;
        }

        // Command bar, POLICY pressed.
        ScreenRect end = ChromeGeometry.EndTurnButton;
        PanelFrame.Paint(d, new RectD(end.X, end.Y, end.Width, end.Height), t, 22, FrameKind.Button, ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.2), t.Material.Accent, 1.2);
        d.Write(t, end.CenterX, end.Y + 6, "End Turn [Space]", 17, t.Ink.Text, TextAlign.Center);
        for (int i = 0; i < GameSections.PlayerOrder.Count; i++)
        {
            ScreenRect nav = ChromeGeometry.NavButton(i);
            bool open = GameSections.PlayerOrder[i] == Section.Policy;
            PanelFrame.Paint(d, new RectD(nav.X, nav.Y, nav.Width, nav.Height), t, 30 + i, FrameKind.Button,
                open ? ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.34) : t.Material.PanelRaised, open ? t.Material.Accent : t.Material.Border, open ? 1.2 : 0.8);
            string label = GameSections.Label(GameSections.PlayerOrder[i]);
            double ns = ThemeText.FitSize(m, t, label, 15, nav.Width - 10, FontRole.Caps);
            d.Write(t, nav.CenterX, nav.Y + 7 + (15 - ns) / 2, ThemeText.Fit(m, t, label, ns, nav.Width - 8, FontRole.Caps), ns, t.Ink.Text, TextAlign.Center, FontRole.Caps);
        }

        // The POLICY panel: its header row, and the real surface in the scrolling content region.
        PanelRect c = PanelLayout.Context;
        ScreenRect head = ChromeGeometry.HeaderRow(ChromeGeometry.Context, fh);
        d.Write(t, head.X + 2, head.Y + 4, GameSections.Title(Section.Policy), 19, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        ScreenRect close = ChromeGeometry.CloseButton(ChromeGeometry.Context, fh);
        var cr = new RectD(close.X, close.Y, close.Width, close.Height);
        PanelFrame.Paint(d, cr, t, 50, FrameKind.Button);
        EraMarks.Close(d, t, cr, t.Ink.Text, 51);
        double top = ChromeGeometry.ContentTop(ChromeGeometry.Context, fh) + 4;
        var content = new RectD(c.X + 14, top, c.Width - 28, c.Y + c.Height - ChromeGeometry.FrameBorderPx - top);
        d.PushClip(content);
        ScreenOf(s).Paint(d, m, content.X, content.Y, content.W);
        d.PopClip();

        string svg = SvgWriter.Write(d, ScreenW, ScreenH, fontDir);
        if (terrainDataUri is null) return svg;
        string img = string.Create(CultureInfo.InvariantCulture,
            $"<image href=\"{terrainDataUri}\" x=\"{(0 - cx) * zoom + ScreenW / 2:0.##}\" y=\"{(0 - cy) * zoom + ScreenH / 2 + 4:0.##}\" width=\"{size * zoom:0.##}\" height=\"{size * zoom:0.##}\" preserveAspectRatio=\"none\"/>");
        int at = svg.IndexOf("</style>\n", StringComparison.Ordinal);
        return svg.Insert(at < 0 ? 0 : at + 9, "<rect x=\"0\" y=\"0\" width=\"1280\" height=\"800\" fill=\"#7f97a0\"/>\n" + img + "\n");
    }

    /// <summary>A copy of <paramref name="list"/> moved by (dx, dy).</summary>
    private static DrawList Offset(DrawList list, double dx, double dy)
    {
        if (dx == 0 && dy == 0) return list;
        var o = new DrawList();
        foreach (DrawCmd c in list.Commands)
        {
            o.Add(c switch
            {
                RectCmd r => r with { Rect = r.Rect with { X = r.Rect.X + dx, Y = r.Rect.Y + dy } },
                LineCmd l => l with { X0 = l.X0 + dx, Y0 = l.Y0 + dy, X1 = l.X1 + dx, Y1 = l.Y1 + dy },
                BezierCmd b => b with { P0 = (b.P0.X + dx, b.P0.Y + dy), P1 = (b.P1.X + dx, b.P1.Y + dy), P2 = (b.P2.X + dx, b.P2.Y + dy), P3 = (b.P3.X + dx, b.P3.Y + dy) },
                PolygonCmd p => p with { Points = Shift(p.Points, dx, dy) },
                PolylineCmd p => p with { Points = Shift(p.Points, dx, dy) },
                CircleCmd ci => ci with { Cx = ci.Cx + dx, Cy = ci.Cy + dy },
                ArcCmd a => a with { Cx = a.Cx + dx, Cy = a.Cy + dy },
                TextCmd tx => tx with { X = tx.X + dx, Y = tx.Y + dy },
                ClipPushCmd cp => cp with { Rect = cp.Rect with { X = cp.Rect.X + dx, Y = cp.Rect.Y + dy } },
                _ => c,
            });
        }
        return o;
    }

    private static (double X, double Y)[] Shift((double X, double Y)[] pts, double dx, double dy)
    {
        var r = new (double X, double Y)[pts.Length];
        for (int i = 0; i < pts.Length; i++) r[i] = (pts[i].X + dx, pts[i].Y + dy);
        return r;
    }

    /// <summary>Writes the preview SVGs and a log (each SVG's SHA-256, each state's world hash and model
    /// domains) into <paramref name="outDir"/>.</summary>
    public static IReadOnlyList<string> Run(string outDir, string? fontDir)
    {
        fontDir = Sim.Ui.Render.SvgWriter.FontDirectoryFor(fontDir, outDir); // F3: portable @font-face URLs
        Directory.CreateDirectory(outDir);
        var written = new List<string>();
        var log = new List<string> { "action surface preview: the real ActionSurfaceScreen over ActionSurface.Build, seed 42, canonical 1024 px world" };
        State one = TurnOne();
        State later = LaterRig();
        State industrial = AtAge(later, 8, "later-a8", "The same later state with only the Age row changed to Age VIII");
        State researched = ResearchedRig();
        foreach (State s in new[] { one, later, industrial, researched })
        {
            ActionSurfaceModel model = ModelOf(s);
            log.Add(s.Stem + ": " + s.Description + "; turn " + s.World.Clock.Turn.ToString(CultureInfo.InvariantCulture)
                + ", world hash " + WorldHash.ComputeHex(s.World) + ", era " + model.Era + ", control " + model.Control.Kind
                + ", layout " + model.Layout + ", domains " + string.Join("/", model.Domains));
            string terrain = AgePreview.TerrainDataUri(s.World);
            foreach ((string kind, string svg) in new[] { ("screen", ScreenSvg(s, fontDir, terrain)), ("panel", PanelSvg(s, fontDir)) })
            {
                string path = Path.Combine(outDir, s.Stem + "-" + kind + ".svg");
                File.WriteAllText(path, svg);
                written.Add(path);
                log.Add("  " + Path.GetFileName(path) + "  sha256 " + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(svg))));
            }
        }
        File.WriteAllLines(Path.Combine(outDir, "preview-log.txt"), log);
        return written;
    }
}
