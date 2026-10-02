using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Ui.Art;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Ages;

/// <summary>
/// HEADLESS PREVIEWS of the Age surfaces and the world lens (sim-ui --age-preview [dir]) from a REAL
/// seed-42 world played forward through the real session and order pathway: the player's research
/// target is always set (by the screen-equivalent order) toward the next Age's unmet core research
/// milestone — the cheapest available node among it and its prerequisites — and otherwise to the
/// cheapest available Technology node, and End Turn is pressed until <see cref="AgeQuery.IsEligible"/>
/// holds. Nothing is placed by hand: every value drawn is the stepped world's.
/// </summary>
public static class AgePreview
{
    public const double Width = 1600, Height = 1000;

    /// <summary>Plays the session forward until the player is eligible for the next Age (or
    /// <paramref name="maxTurns"/>). Returns the turn count reached.</summary>
    public static int PlayToEligible(UiSession session, int maxTurns = 600)
    {
        AgeContent ages = session.Config.Ages!;
        ResearchContent research = session.Config.Research!;
        PolityId me = UiPlayer.Empire;
        for (int t = 0; t < maxTurns; t++)
        {
            if (AgeQuery.IsEligible(session.World, ages, me)) return t;
            if (!ResearchQuery.TryGetTarget(session.World, me, out _) && NextNode(session.World, research, ages, me) is ResearchNodeId next)
                session.EmitResearchOrder(next);
            session.EndTurn();
        }
        return maxTurns;
    }

    /// <summary>The next research target toward the unmet core milestones of the next Age.</summary>
    public static ResearchNodeId? NextNode(IReadOnlyWorldState world, ResearchContent research, AgeContent ages, PolityId me)
    {
        bool[] done = ResearchQuery.CompletedMask(world, research, me);
        bool[] avail = ResearchQuery.AvailableMask(research, done);
        if (AgeQuery.Evaluate(world, ages, me) is { } report)
        {
            var wanted = new bool[research.Nodes.Count];
            foreach (MilestoneStatus m in report.Core)
            {
                if (m.Met || m.Milestone.Fact.Kind != MilestoneFactKind.Research) continue;
                foreach (int key in m.Milestone.Fact.NodeKeys) Mark(research, research.IndexOf(new ResearchNodeId(key)), wanted);
            }
            foreach (MilestoneStatus m in report.Supporting)
            {
                if (m.Met || m.Milestone.Fact.Kind != MilestoneFactKind.Research) continue;
                foreach (int key in m.Milestone.Fact.NodeKeys) Mark(research, research.IndexOf(new ResearchNodeId(key)), wanted);
            }
            int best = -1;
            for (int i = 0; i < wanted.Length; i++)
                if (wanted[i] && avail[i] && !done[i] && (best < 0 || research.Nodes[i].BaseCost < research.Nodes[best].BaseCost)) best = i;
            if (best >= 0) return research.Nodes[best].Key;
        }
        return ResearchQuery.CheapestAvailable(world, research, me, ResearchTree.Technology);
    }

    private static void Mark(ResearchContent c, int node, bool[] wanted)
    {
        if (node < 0 || wanted[node]) return;
        wanted[node] = true;
        foreach (int p in c.Nodes[node].PrerequisiteNodes) Mark(c, p, wanted);
    }

    public static IReadOnlyList<string> Run(string outDir, string? fontDir)
    {
        Directory.CreateDirectory(outDir);
        var written = new List<string>();
        var log = new List<string>();
        PolityId me = UiPlayer.Empire;
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        AgeContent ages = session.Config.Ages!;
        string terrain = TerrainDataUri(session.World);

        // 01: not eligible (turn 12 of the real run).
        for (int t = 0; t < 12; t++)
        {
            if (!ResearchQuery.TryGetTarget(session.World, me, out _) && NextNode(session.World, session.Config.Research!, ages, me) is ResearchNodeId n0)
                session.EmitResearchOrder(n0);
            session.EndTurn();
        }
        written.Add(PanelShot(outDir, fontDir, session, terrain, "01-capital-age-panel-not-eligible"));
        log.Add($"01 turn {session.World.Clock.Turn}: eligible={AgeQuery.IsEligible(session.World, ages, me)}");

        // World lens at three zooms (same not-yet-advanced world).
        written.Add(LensShot(outDir, fontDir, session, terrain, "05-world-zoom-world", WorldZoom.World));
        written.Add(LensShot(outDir, fontDir, session, terrain, "06-world-zoom-regional", WorldZoom.Regional));
        written.Add(LensShot(outDir, fontDir, session, terrain, "07-world-zoom-settlement", WorldZoom.Settlement));

        int reached = PlayToEligible(session);
        log.Add($"eligible after {reached} more turns, at turn {session.World.Clock.Turn}: {AgeQuery.IsEligible(session.World, ages, me)}");
        written.Add(PanelShot(outDir, fontDir, session, terrain, "02-capital-age-panel-eligible"));

        // 03: the advance flow with a surge chosen.
        var screen = new AgeScreen(me) { Theme = ThemeOf(session) };
        screen.Refresh(session.World, ages, session.Config.UnitFamilies, session.QueuedOrders());
        screen.OpenFlow();
        screen.SelectedSurge = ages.Surges[Math.Min(1, ages.Surges.Count - 1)].Key;
        written.Add(Write(outDir, fontDir, "03-advance-age-surge-and-modernization", d =>
        {
            Sim.Ui.Theme.PanelFrame.Field(d, new RectD(0, 0, Width, Height), screen.Theme, 1);
            screen.PaintPanel(d, ApproxTextMeasure.Instance, PanelRect(), CapitalName(session));
            screen.PaintFlow(d, ApproxTextMeasure.Instance, Width, Height);
        }, null));

        // Confirm through the session order path; 04: pending.
        AgeCommand cmd = screen.Confirm();
        if (cmd.Order is { } order) session.EmitAdvanceAge(order.TargetId, cmd.SurgeKey);
        written.Add(PanelShot(outDir, fontDir, session, terrain, "04-capital-age-panel-pending"));
        int before = AgeQuery.CurrentAge(session.World, ages, me);
        session.EndTurn();
        int after = AgeQuery.CurrentAge(session.World, ages, me);
        log.Add($"advance: age {before} -> {after} at turn {session.World.Clock.Turn}");

        // 08: after the transition — the world lens (regional) with the new banner style and the toast.
        written.Add(LensShot(outDir, fontDir, session, terrain, "08-after-transition-world", WorldZoom.Regional, toast: true));
        written.Add(PanelShot(outDir, fontDir, session, terrain, "09-capital-age-panel-after-transition"));
        File.WriteAllLines(Path.Combine(outDir, "preview-log.txt"), log);
        return written;
    }

    private static ParchmentPalette.Rgba Rgba(uint hex) => ParchmentPalette.Rgba.Hex(hex);
    private static RectD PanelRect() => new(Width - 520, 70, 500, Height - 90);

    private static string CapitalName(UiSession s) =>
        EmpireQuery.TryGetCapital(s.World, UiPlayer.Empire, out SettlementId cap) ? s.Names.Name(cap.Value) : "Capital";

    /// <summary>The era the session's world presents (the game's derivation, ADR-033 D8).</summary>
    private static Sim.Ui.Theme.EraTheme ThemeOf(UiSession s) =>
        Sim.Ui.Theme.EraThemes.For(Sim.Ui.Theme.UiEras.Of(s.World, s.Config.Ages, UiPlayer.Empire));

    private static string PanelShot(string outDir, string? fontDir, UiSession session, string terrain, string name)
    {
        var screen = new AgeScreen(UiPlayer.Empire) { Theme = ThemeOf(session) };
        screen.Refresh(session.World, session.Config.Ages, session.Config.UnitFamilies, session.QueuedOrders());
        return Write(outDir, fontDir, name, d =>
        {
            PaintLens(d, session, WorldZoom.Regional, capitalLeft: true);
            screen.PaintPanel(d, ApproxTextMeasure.Instance, PanelRect(), CapitalName(session));
        }, terrain, WorldZoom.Regional, session, capitalLeft: true);
    }

    private static string LensShot(string outDir, string? fontDir, UiSession session, string terrain, string name, WorldZoom zoom, bool toast = false)
    {
        return Write(outDir, fontDir, name, d =>
        {
            PaintLens(d, session, zoom, capitalLeft: false);
            if (toast && session.Config.Ages is { } ages)
            {
                var screen = new AgeScreen(UiPlayer.Empire) { Theme = ThemeOf(session) };
                AgeTransitionRow[] tr = AgeQuery.Transitions(session.World, UiPlayer.Empire);
                if (tr.Length > 0)
                {
                    int converted = 0;
                    foreach (UnitConversionRow c in MilitaryQuery.Conversions(session.World, UiPlayer.Empire))
                        if (c.ToAge == tr[^1].ToAge && c.FromIdentity != c.ToIdentity) converted++;
                    screen.ShowTransition(tr[^1].ToAge, ages.Age(tr[^1].ToAge).Name, ages.SurgeByKey(tr[^1].Surge)?.Name, converted);
                    screen.HoldToast();
                    screen.PaintToast(d, ApproxTextMeasure.Instance, Width, 70);
                }
            }
        }, terrain, zoom, session);
    }

    /// <summary>The camera for a preview zoom: world fits; regional centres the player's capital at
    /// ~30% of the world; settlement zoom frames the capital closely.</summary>
    public static (double Cx, double Cy, double Zoom) CameraFor(UiSession s, WorldZoom z, bool capitalLeft)
    {
        double size = s.World.Terrain!.Size;
        double cx = size / 2, cy = size / 2;
        if (EmpireQuery.TryGetCapital(s.World, UiPlayer.Empire, out SettlementId cap))
            for (int i = 0; i < s.World.Settlements.Count; i++)
                if (s.World.Settlements[i].Id == cap) { cx = s.World.Settlements[i].SiteCell % (int)size + 0.5; cy = s.World.Settlements[i].SiteCell / (int)size + 0.5; }
        double zoom = z switch
        {
            WorldZoom.World => Math.Min(Width, Height) / size,
            WorldZoom.Regional => Math.Min(Width, Height) / (size * 0.30),
            _ => Math.Min(Width, Height) / (size * 0.10),
        };
        if (z == WorldZoom.World) { cx = size / 2; cy = size / 2; }
        if (capitalLeft) cx += 260 / zoom;   // keep the capital clear of the docked panel
        return (cx, cy, zoom);
    }

    private static void PaintLens(DrawList d, UiSession s, WorldZoom z, bool capitalLeft)
    {
        (double cx, double cy, double zoom) = CameraFor(s, z, capitalLeft);
        WorldProjection p = WorldProjection.Build(s.World, s.Config, id => s.Names.Name(id), UiPlayer.Empire);
        WorldZoom level = WorldLens.LevelFor(zoom, Width, Height, p.WorldSize);
        int sel = EmpireQuery.TryGetCapital(s.World, UiPlayer.Empire, out SettlementId cap) ? cap.Value : -1;
        WorldLens.Paint(d, ApproxTextMeasure.Instance, p, level,
            (x, y) => ((x - cx) * zoom + Width / 2, (y - cy) * zoom + Height / 2), zoom, new RectD(0, 0, Width, Height), sel,
            ink: MapInk.For(ThemeOf(s)));   // the era's map ink, exactly as the game derives it
        LensLegend(d, p, level);
    }

    private static void LensLegend(DrawList d, WorldProjection p, WorldZoom z)
    {
        List<string> layerLines = Ink.Wrap(ApproxTextMeasure.Instance, "Layers: " + string.Join(", ", WorldLens.LayersFor(z)), 10.5, 446);
        var r = new RectD(16, 16, 470, 34 + 14 * layerLines.Count + 15 * p.Absent.Count);
        d.Rect(r, Ink.With(Rgba(0xF4EBD3), 0.92), Rgba(0x6B5A3E), 1, 6);
        string zl = z switch { WorldZoom.World => "WORLD ZOOM", WorldZoom.Regional => "REGIONAL ZOOM", _ => "SETTLEMENT ZOOM" };
        d.Text(r.X + 12, r.Y + 8, zl + "  -  your civilization: Age " + AgePanelModel.Numeral(p.PlayerAge) + " " + p.PlayerAgeName, 12.5, Rgba(0x3A2E1F), TextAlign.Left, FontRole.Caps);
        double y = r.Y + 26;
        foreach (string l in layerLines) { d.Text(r.X + 12, y, l, 10.5, Rgba(0x6B5A3E)); y += 14; }
        y += 2;
        foreach (string a in p.Absent) { d.Text(r.X + 12, y, a, 10.5, Rgba(0x8C4A3A)); y += 15; }
    }

    /// <summary>The world lens of any session at a zoom, over the game's terrain bake, with the legend —
    /// as an SVG string (used by tests to preview constructed fixture worlds; writes nothing).</summary>
    public static string LensSvg(UiSession session, WorldZoom zoom, string? fontDir = null) =>
        Svg(fontDir, d => PaintLens(d, session, zoom, false), TerrainDataUri(session.World), zoom, session, false);

    private static string Write(string outDir, string? fontDir, string name, Action<DrawList> paint, string? terrain,
        WorldZoom zoom = WorldZoom.World, UiSession? session = null, bool capitalLeft = false)
    {
        string svg = Svg(fontDir, paint, terrain, zoom, session, capitalLeft);
        string path = Path.Combine(outDir, name + ".svg");
        File.WriteAllText(path, svg);
        return path;
    }

    private static string Svg(string? fontDir, Action<DrawList> paint, string? terrain,
        WorldZoom zoom, UiSession? session, bool capitalLeft)
    {
        var d = new DrawList();
        paint(d);
        string svg = SvgWriter.Write(d, Width, Height, fontDir);
        if (terrain is not null && session is not null)
        {
            (double cx, double cy, double z) = CameraFor(session, zoom, capitalLeft);
            double size = session.World.Terrain!.Size;
            string img = string.Create(CultureInfo.InvariantCulture,
                $"<image href=\"{terrain}\" x=\"{(0 - cx) * z + Width / 2:0.##}\" y=\"{(0 - cy) * z + Height / 2:0.##}\" width=\"{size * z:0.##}\" height=\"{size * z:0.##}\" preserveAspectRatio=\"none\"/>");
            int at = svg.IndexOf("</style>\n", StringComparison.Ordinal);
            svg = svg.Insert(at < 0 ? 0 : at + 9, "<rect x=\"0\" y=\"0\" width=\"1600\" height=\"1000\" fill=\"#7f97a0\"/>\n" + img + "\n");
        }
        return svg;
    }

    /// <summary>The parchment terrain bake of the real world as a PNG data URI (the game's own map art).</summary>
    internal static string TerrainDataUri(IReadOnlyWorldState world)
    {
        ParchmentBaker.Result bake = ParchmentBaker.Bake(world.Terrain!, AssetLibrary.Load(), world.Seed);
        // A file of this call's own: a fixed per-seed name raced when two previews (parallel test classes,
        // or two processes sharing the temp directory) wrote it at once.
        string tmp = Path.Combine(Path.GetTempPath(), "age-preview-terrain-" + world.Seed.ToString(CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            PngCodec.Write(tmp, new ArtImage(bake.Size, bake.Size, bake.Rgba));
            return "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(tmp));
        }
        finally { File.Delete(tmp); }
    }
}
