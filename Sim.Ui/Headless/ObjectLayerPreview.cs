using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ImGuiNET;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Ui.Art;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Headless;

/// <summary>
/// M6 E1.6 — THE OBJECT LAYER'S EVIDENCE (<c>sim-ui --e1-preview [DIR]</c>): the world lens painted by the game's own call
/// (<see cref="WorldLens.Paint(DrawList, ITextMeasure, WorldProjection, MapDetail, Func{double, double, ValueTuple{double, double}}, double, RectD, int, bool, MapInk?, EraTheme?, double, LegendAnchor?, int, OverlayPaint?, bool)"/>,
/// the era's ink, hand and type, the cylinder's copies) at THREE ZOOMS — the World band at the window's fit, the Regional
/// and the Settlement band on the capital — in three eras (Ages I, V and IX: the player's Age row only, as
/// <see cref="EraPreview.WorldAt"/> sets it), on three worlds: the canonical world stepped to turn 150 (fields appear where
/// cultivation is known), and the high-count worlds of the frame-cost sweep — 300 settlements at the canonical size and 800
/// at 2560 px (the canonical cylinder sites at most 362) — at turn 1, each over its own parchment bake (a PNG beside the
/// SVGs, the same image in every era). Plus a PARTS SHEET: every part kind at every count stage in every register rung, the
/// scaffold and the overflow part, and the silhouette token of every identity of the content.
/// <para>THE ONLY CONSTRUCTED STATE (illustration, never persisted): on each world a copy of the stepped world receives, at
/// the capital and its three nearest settlements, structure rows (granary 30, workshop 12 — 1 and 1 — 10), founded
/// institution rows (types 1 × 20, 3 × 1, 4 × 10; type 2 × 1), one queued construction of the type-5 university, and two more
/// formations of the capital's own identity beside its Warband plus one other — so the count stages, the register, the
/// scaffold and a grouped token are on the map. Everything else is the real stepped world. The log lists every file's
/// SHA-256 and the terrain-mark census.</para>
/// </summary>
public static class ObjectLayerPreview
{
    public const double W = 1280, H = 800;

    public static readonly int[] Ages = [1, 5, 9];

    /// <summary>Writes the evidence into <paramref name="outDir"/>. <paramref name="quick"/>: the parts sheet and the canonical
    /// world at turn 1 only (an iteration aid; the evidence run is the full one).</summary>
    public static IReadOnlyList<string> Run(string outDir, string? fontDir, Action<string>? log = null, bool quick = false)
    {
        log ??= _ => { };
        fontDir = SvgWriter.FontDirectoryFor(fontDir, outDir);
        Directory.CreateDirectory(outDir);
        var written = new List<string>();
        var lines = new List<string> { "E1 object-layer preview (" + BuildInfo.Describe() + ")" };

        written.Add(PartsSheet(outDir, fontDir, lines, out DrawList sheet, out double sheetH));
        written.Add(Png(Path.Combine(outDir, "e1-parts-sheet.png"), sheet, 1600, (int)Math.Ceiling(sheetH), null));
        foreach ((string name, int? size, int? settlements, int turns) in new (string, int?, int?, int)[]
                 { ("canonical", null, null, quick ? 1 : 150), ("s300", null, 300, 1), ("s800", 2560, 800, 1) })
        {
            if (quick && name != "canonical") continue;
            log("e1 preview: founding " + name);
            UiSession s = settlements is null ? UiSession.Start(42) : MapFrameCost.StartHighCount(settlements.Value, size);
            while (s.World.Clock.Turn < turns) s.EndTurn();
            WorldState w = Illustrate(s.World.Clone(), s.Config, out string what);
            // ILLUSTRATION (the canonical world only): its settlements' cultivation treated as realized for the projection
            // (sim.json farming.preCultivation off, read by the lens's predicate only — no step runs under it), so fields
            // are on the map; at turn 150 the seed-42 player has not yet researched cultivation.
            SimConfig view = name == "canonical" && s.Config.Farming.PreCultivation is { } pre
                ? s.Config with { Farming = s.Config.Farming with { PreCultivation = pre with { Enabled = false } } } : s.Config;
            if (!ReferenceEquals(view, s.Config)) what += "; cultivation treated as realized for the projection (fields shown)";
            string terrainFile = "terrain-" + name + ".png";
            ParchmentBaker.Result bake = ParchmentBaker.Bake(w.Terrain!, AssetLibrary.Load(), w.Seed);
            PngCodec.Write(Path.Combine(outDir, terrainFile), new ArtImage(bake.Size, bake.Size, bake.Rgba));
            TerrainMarks marks = TerrainMarks.Of(w.Terrain!);
            int[] census = marks.Census();
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{name}: {w.Terrain!.Size} px, turn {w.Clock.Turn}, {w.Settlements.Count} settlements, world hash (stepped, before illustration) {WorldHash.ComputeHex(s.World)}; {what}"));
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{name}: terrain marks on a {marks.Cells}x{marks.Cells} grid: hill {census[1]}, mountain {census[2]}, peak {census[3]}, forest {census[4]}; fields {WorldProjection.Build(w, view, id => s.Names.Name(id), UiPlayer.Empire).Fields.Count}"));
            foreach (int age in Ages)
            {
                WorldState wa = EraPreview.WorldAt(w, s.Config.Ages!, UiPlayer.Empire, age);
                foreach ((string zoomName, WorldZoom band) in new[] { ("world", WorldZoom.World), ("regional", WorldZoom.Regional), ("settlement", WorldZoom.Settlement) })
                {
                    string file = name + "-age" + age.ToString(CultureInfo.InvariantCulture) + "-" + zoomName + ".svg";
                    (string svg, int commands, LensFrame f, DrawList list, double zoom, double cx, double cy) = MapSvg(s, view, wa, band, fontDir, terrainFile);
                    File.WriteAllText(Path.Combine(outDir, file), svg);
                    written.Add(Path.Combine(outDir, file));
                    written.Add(Png(Path.Combine(outDir, Path.ChangeExtension(file, ".png")), list, (int)W, (int)H,
                        Under(bake, w.Terrain!.Size, zoom, cx, cy, w.Terrain.WrapX)));
                    lines.Add(string.Create(CultureInfo.InvariantCulture,
                        $"{file}  sha256 {Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(svg)))}  commands {commands}, terrain-layer commands {f.LayerDraws[(int)MapLayer.ObjectTerrain]}, settlements drawn {f.SettlementsDrawn.Count}, parts {f.Parts.Count}, tokens {f.UnitTokens.Count}"));
                }
            }
        }
        File.WriteAllLines(Path.Combine(outDir, "e1-preview-log.txt"), lines);
        return written;
    }

    /// <summary>The illustration rows (see the type's remarks) on the clone <paramref name="w"/>.</summary>
    public static WorldState Illustrate(WorldState w, SimConfig cfg, out string what)
    {
        PolityId me = UiPlayer.Empire;
        if (!EmpireQuery.TryGetCapital(w, me, out SettlementId cap)) { what = "no capital: nothing illustrated"; return w; }
        int size = w.Terrain!.Size;
        int capCell = -1;
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == cap) capCell = w.Settlements[i].SiteCell;
        var near = new List<(long D, int Id)>();
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            if (w.Settlements[i].Id == cap) continue;
            int c = w.Settlements[i].SiteCell;
            near.Add((Sim.Core.Pathing.MapTopology.SquaredPx(c, capCell, size, w.Terrain.WrapX), w.Settlements[i].Id.Value));
        }
        near.Sort(static (a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.Id.CompareTo(b.Id));
        int granary = -1, workshop = -1;
        foreach (ConstructionProjectEntry p in cfg.Goods!.Projects!)
        {
            if (p.Name == "granary") granary = p.Id;
            if (p.Name == "workshop") workshop = p.Id;
        }
        int nextInst = 1;
        for (int i = 0; i < w.Institutions.Count; i++) nextInst = Math.Max(nextInst, w.Institutions[i].Id + 1);
        void Found(SettlementId at, int type, int n)
        {
            PolityId owner = EmpireQuery.TryGetController(w, at, out PolityId o) ? o : me;
            for (int k = 0; k < n; k++) w.Institutions.Add(new InstitutionRow(nextInst++, owner, at, type, w.Clock.Turn, 0.6));
        }
        w.Structures.Add(new StructureRow(cap, granary, 30));
        w.Structures.Add(new StructureRow(cap, workshop, 12));
        Found(cap, 1, 20);
        Found(cap, 3, 1);
        Found(cap, 4, 10);
        if (InstitutionContent.ProjectOfType(cfg, 5) is { } uni5) w.ConstructionQueue.Add(new ConstructionQueueRow(cap, 99, uni5.Id));
        if (near.Count > 0)
        {
            var b = new SettlementId(near[0].Id);
            w.Structures.Add(new StructureRow(b, granary, 1));
            w.Structures.Add(new StructureRow(b, workshop, 1));
            Found(b, 2, 1);
        }
        if (near.Count > 1) w.Structures.Add(new StructureRow(new SettlementId(near[1].Id), granary, 10));
        if (near.Count > 2) w.Structures.Add(new StructureRow(new SettlementId(near[2].Id), workshop, 1));
        int nextUnit = 0;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) nextUnit = Math.Max(nextUnit, w.MilitaryUnits[i].Id + 1);
        MilitaryUnitRow[] mine = MilitaryQuery.Units(w, me);
        if (mine.Length > 0)
        {
            w.MilitaryUnits.Add(mine[0] with { Id = nextUnit++ });
            w.MilitaryUnits.Add(mine[0] with { Id = nextUnit++ });
            if (cfg.UnitFamilies?.FamilyById("spear_anti_cavalry") is { } spear)
                w.MilitaryUnits.Add(mine[0] with { Id = nextUnit++, Family = spear.Key, Identity = spear.Line[0].Key });
        }
        what = "illustrated: capital " + cap.Value.ToString(CultureInfo.InvariantCulture) + " (granary 30, workshop 12, institutions 1x20 3x1 4x10, type-5 queued, 3 warbands + 1 spearmen)"
            + (near.Count > 0 ? ", " + near[0].Id.ToString(CultureInfo.InvariantCulture) + " (granary 1, workshop 1, institution 2x1)" : "")
            + (near.Count > 1 ? ", " + near[1].Id.ToString(CultureInfo.InvariantCulture) + " (granary 10)" : "")
            + (near.Count > 2 ? ", " + near[2].Id.ToString(CultureInfo.InvariantCulture) + " (workshop 1)" : "");
        return w;
    }

    /// <summary>The lens at <paramref name="band"/> — the World band at the window's fit (one circumference across), the
    /// others centred on the capital at a zoom inside the band's plateau — over the parchment image.</summary>
    public static (string Svg, int Commands, LensFrame Frame, DrawList List, double Zoom, double Cx, double Cy) MapSvg(UiSession s, SimConfig cfg, WorldState w, WorldZoom band, string? fontDir, string terrainHref)
    {
        EraTheme t = EraPreview.ThemeOf(s, w);
        PolityId me = UiPlayer.Empire;
        double size = w.Terrain!.Size, km = w.Terrain.KmPerPx;
        int capital = EmpireQuery.TryGetCapital(w, me, out SettlementId cap) ? cap.Value : -1;
        double cx = size / 2, cy = size / 2;
        for (int i = 0; i < w.Settlements.Count; i++)
            if (w.Settlements[i].Id.Value == capital) { cx = w.Settlements[i].SiteCell % (int)size + 0.5; cy = w.Settlements[i].SiteCell / (int)size + 0.5; }
        // The visible km across the window inside each band's plateau (MapDetail's edges 2253 and 655 km, ±2^0.25).
        double zoom = band switch
        {
            WorldZoom.World => W / size,
            WorldZoom.Regional => W / (1200.0 / km),
            _ => W / (200.0 / km),   // deep in the Settlement band: "high zoom", where a part shows its number
        };
        if (band == WorldZoom.World) cy = size / 2;
        bool wrap = w.Terrain.WrapX;
        MapDetail detail = wrap ? MapDetail.Across(zoom, W, km) : MapDetail.At(zoom, W, H, km);
        WorldProjection p = WorldProjection.Build(w, cfg, id => s.Names.Name(id), me);
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, detail,
            (x, y) => ((x - cx) * zoom + W / 2, (y - cy) * zoom + H / 2), zoom, new RectD(0, 0, W, H), capital,
            ink: MapInk.For(t), theme: t, wrapX: wrap);
        string svg = SvgWriter.Write(d, W, H, fontDir);
        var img = new StringBuilder("<rect x=\"0\" y=\"0\" width=\"1280\" height=\"800\" fill=\"#7f97a0\"/>\n");
        foreach (int c in wrap ? new[] { -1, 0, 1 } : new[] { 0 })
            img.Append(string.Create(CultureInfo.InvariantCulture,
                $"<image href=\"{terrainHref}\" x=\"{(c * size - cx) * zoom + W / 2:0.##}\" y=\"{(0 - cy) * zoom + H / 2:0.##}\" width=\"{size * zoom:0.##}\" height=\"{size * zoom:0.##}\" preserveAspectRatio=\"none\"/>\n"));
        int at = svg.IndexOf("</style>\n", StringComparison.Ordinal);
        return (svg.Insert(at < 0 ? 0 : at + 9, img.ToString()), d.Commands.Count, f, d, zoom, cx, cy);
    }

    /// <summary>The parchment bake sampled for the camera (nearest texel; the cylinder wraps in x; off the sheet: the sea
    /// grey of the SVGs) — the PNG's ground.</summary>
    private static ArtImage Under(ParchmentBaker.Result bake, int size, double zoom, double cx, double cy, bool wrap)
    {
        var rgba = new byte[(int)W * (int)H * 4];
        double k = bake.Size / (double)size;
        for (int y = 0; y < (int)H; y++)
            for (int x = 0; x < (int)W; x++)
            {
                double wx = (x + 0.5 - W / 2) / zoom + cx, wy = (y + 0.5 - H / 2) / zoom + cy;
                if (wrap) wx = Sim.Core.Pathing.MapTopology.WrapCoord(wx, size);
                int o = (y * (int)W + x) * 4;
                if (wx < 0 || wy < 0 || wx >= size || wy >= size) { rgba[o] = 0x7f; rgba[o + 1] = 0x97; rgba[o + 2] = 0xa0; rgba[o + 3] = 255; continue; }
                int tx = Math.Min(bake.Size - 1, (int)(wx * k)), ty = Math.Min(bake.Size - 1, (int)(wy * k));
                int t = (ty * bake.Size + tx) * 4;
                rgba[o] = bake.Rgba[t]; rgba[o + 1] = bake.Rgba[t + 1]; rgba[o + 2] = bake.Rgba[t + 2]; rgba[o + 3] = 255;
            }
        return new ArtImage((int)W, (int)H, rgba);
    }

    /// <summary>A PNG of <paramref name="list"/> through the game's ImGui replay and the headless rasterizer
    /// (<see cref="FrameRaster"/>), over <paramref name="under"/> (or the paper colour) — the same frame the game draws, as
    /// pixels, for reviewers without an SVG viewer.</summary>
    private static string Png(string path, DrawList list, int w, int h, ArtImage? under)
    {
        if (under is null)
        {
            var paper = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++) { paper[i * 4] = 0xE2; paper[i * 4 + 1] = 0xD3; paper[i * 4 + 2] = 0xAE; paper[i * 4 + 3] = 255; }
            under = new ArtImage(w, h, paper);
        }
        using var gui = new HeadlessImGui(AssetManifest.DefaultRoot(), w, h);
        var backend = new DrawListImGuiBackend(gui.Fonts) { Cull = true };
        gui.BeginFrame(default, default, 1.0 / 60);
        backend.Render(ImGui.GetBackgroundDrawList(), list);
        gui.EndFrame();
        PngCodec.Write(path, FrameRaster.Rasterize(w, h, under));
        return path;
    }

    /// <summary>THE PARTS SHEET: every part kind × count stage × register rung, the scaffold (alone and beside), the overflow
    /// part, every terrain mark kind, a field, and every identity's silhouette token (single and grouped) — the default
    /// (parchment) ink, on paper.</summary>
    public static string PartsSheet(string outDir, string? fontDir, List<string> lines, out DrawList sheet, out double height)
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        SimConfig cfg = s.Config;
        var d = new DrawList();
        MapInk k = MapInk.Default;
        const double G = 34, Pitch = 92;
        d.Rect(new RectD(0, 0, 1600, 1500), k.FogUnknown);
        var specs = new List<(string Label, PartSpec Spec)>
        {
            ("granary", new PartSpec(false, 1, "granary", StructureKind.Storage)),
            ("workshop", new PartSpec(false, 2, "workshop", StructureKind.Production)),
            ("other structure", new PartSpec(false, 99, "structure", StructureKind.Other)),
        };
        foreach (Sim.Core.Systems.Research.UniversityType u in cfg.Research!.UniversityTypes) specs.Add((u.Name, new PartSpec(true, u.Key, u.Name, StructureKind.Other)));
        specs.Add(("Law court (named fallback)", new PartSpec(true, 6, "Law court", StructureKind.Other)));
        specs.Add(("unknown institution (generic)", new PartSpec(true, 42, "Guild hall", StructureKind.Other)));
        double y = 60;
        d.Text(20, 14, "E1 parts sheet - every part x count stage (1 small, 10 expanded, 20 campus, 30 complex) x register (earthen, timber, masonry, engineered); one light from the north-west", 15, k.Ink);
        foreach ((string label, PartSpec spec) in specs)
        {
            d.Text(20, y - 8, label, 13, k.Ink);
            for (int reg = 0; reg < 4; reg++)
                for (int stage = 0; stage < 4; stage++)
                {
                    double x = 260 + (reg * 4 + stage) * 80;
                    WorldLens.PartGlyph(d, x, y + 14, G, spec, stage, (ObjectRegister)reg, k);
                }
            y += Pitch;
        }
        // Scaffold, overflow, dwellings per register, terrain marks, a field.
        d.Text(20, y - 8, "scaffold (alone, beside a part), overflow, dwelling per register, peak, mountain, hill, forest, field", 13, k.Ink);
        WorldLens.ScaffoldGlyph(d, 260, y + 14, G, false, k);
        WorldLens.PartGlyph(d, 340, y + 14, G, specs[0].Spec, 0, ObjectRegister.Timber, k);
        WorldLens.ScaffoldGlyph(d, 340, y + 14, G, true, k);
        WorldLens.OverflowGlyph(d, 420, y + 14, G, ObjectRegister.Masonry, k);
        for (int reg = 0; reg < 4; reg++) WorldLens.DwellingBlock(d, 500 + reg * 40, y + 14, 24, 19, (ObjectRegister)reg, k);
        int cell = 7;
        foreach (TerrainMarkKind m in new[] { TerrainMarkKind.Peak, TerrainMarkKind.Mountain, TerrainMarkKind.Hill, TerrainMarkKind.Forest })
            TerrainMarks.Mark(d, k, m, 700 + (int)m * 70, y + 14, 60, cell++);
        TerrainMarks.Field(d, k, 1000, y - 16, 56, 3);
        y += Pitch + 20;
        d.Text(20, y - 8, "formation silhouettes, one per identity (form pips below; a grouped token carries the number of formations above)", 13, k.Ink);
        int n = 0;
        foreach (UnitFamily fam in cfg.UnitFamilies!.Families)
            foreach (UnitIdentity ident in fam.Line)
            {
                double x = 40 + n % 16 * 96, yy = y + 30 + n / 16 * 70;
                WorldLens.FormationToken(d, x, yy, 22, UnitEmblem.Of(cfg.UnitFamilies, fam.Key, ident.Key), k.PolityOf(n % 7, 0), k, n % 5 == 0 ? 3 : 1);
                d.Text(x - 40, yy + 20, ident.Name, 9, k.Ink);
                n++;
            }
        string file = Path.Combine(outDir, "e1-parts-sheet.svg");
        height = y + 30 + (n / 16 + 1) * 70;
        sheet = d;
        string svg = SvgWriter.Write(d, 1600, height, fontDir);
        File.WriteAllText(file, svg);
        lines.Add("e1-parts-sheet.svg  sha256 " + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(svg))) + "  commands " + d.Commands.Count.ToString(CultureInfo.InvariantCulture));
        return file;
    }
}
