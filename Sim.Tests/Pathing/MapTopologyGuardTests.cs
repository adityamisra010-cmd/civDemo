using System.Text.RegularExpressions;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Pathing;

/// <summary>
/// Batch 3 WS3 — THE TOPOLOGY SOURCE GUARD. On a cylinder an x delta, an x neighbour or an x bound computed from raw
/// cell arithmetic is wrong across the seam, and it is wrong SILENTLY (a flat-world test never sees it). So no file of
/// Sim.Core or Sim.Ui outside the topology chokepoint — <c>Pathing/MapTopology.cs</c>, <c>Pathing/TraversalLattice.cs</c>
/// (the lattice's own id↔coords) and <c>Worldgen/Worldgen.cs</c> (it builds the raster before any TerrainSet exists, and
/// its every neighbour goes through MapTopology.NeighbourX) — may write one of the idioms below. Every pre-batch-3 site
/// the topology recon listed (topology.md §2, class A) matched one of them; each now goes through
/// <see cref="Sim.Core.Pathing.MapTopology"/>. A FUTURE MOVEMENT SYSTEM (M7, order kind 10) cannot bypass the topology
/// without failing here.
///
/// The ALLOWLIST is per LINE (file, exact trimmed text and the number of times that text occurs in the file), each with
/// its reason, so a new violation in an allowlisted file — even a copy of an allowlisted line — still fails; an entry
/// whose count no longer matches fails too (no stale exemptions).
///
/// W1 fix F4 (the verifier's mutants M45 <c>long dx = (cellA % size) - (cellB % size)</c> and M46
/// <c>int xa = cellA % size, xb = cellB % size; long dx = xb - xa</c> passed the historical idioms): two idioms are
/// STRUCTURAL rather than historical — every COLUMN EXTRACTION (<c>cell % size</c>, however spelt) and every x BOUND
/// AGAINST A SIZE (<c>x &lt; size</c>, <c>nx &gt; size − 1</c>, either side) is flagged, because each is where raw
/// east-west geometry starts: a column is the operand of every x delta, and a bound is the edge a cylinder does not
/// have. Every such line outside the chokepoint is reviewed and allowlisted with its reason (a cell's own column for
/// display, a position the rendering stage will draw at every visible copy, a column whose neighbours go through
/// MapTopology.NeighbourX). The full-row scan <c>for (int x = 0; x &lt; size; x++)</c> is not a bound.
/// Second, the CONFIG guard: only Worldgen.Generate (and the loader that validates it) reads WorldgenConfig.Topology —
/// everything else reads TerrainSet.WrapX, so no system can disagree with the terrain it steps.
/// </summary>
public class MapTopologyGuardTests
{
    private static readonly (string Name, Regex Pattern)[] Idioms =
    [
        ("raw column delta (cell % size − …)", new(@"%\s*[A-Za-z_.]*[sS]ize\w*\s*-|-\s*[A-Za-z_.]+\s*%\s*[A-Za-z_.]*[sS]ize\b")),
        ("raw x bound (x >= size)", new(@"\b(x|nx|xx|wx)\s*>=\s*[A-Za-z_.]*[sS]ize\b")),
        ("raw x bound (x < size − 1 &&)", new(@"\bx\s*(<|>)\s*[A-Za-z_.]*[sS]ize\s*-\s*1\s*&&")),
        ("raw x window clamp (Math.Max(0, x − r))", new(@"Math\.(Max|Min)\(\s*(0|[A-Za-z_.]*[sS]ize\w*\s*-\s*1)\s*,\s*(x|cx|wx|xx)\s*[-+]")),
        ("raw x adjacency (Math.Abs(ax − bx))", new(@"Math\.Abs\(\s*\w*x\s*-\s*\w*x\s*\)")),
        ("raw x neighbour step (nx = x + dx)", new(@"\b(nx|xx)\s*=\s*\w*x\w*\s*\+")),
        ("raw x difference (dx = x − gx)", new(@"\bdx\s*=\s*\w*x\s*-\s*\w*x\b")),
        // W1 fix F4 — structural idioms (see the class summary).
        (ColumnExtraction, new(@"%\s*\(?\s*[\w.]*[sS]ize\w*\b")),
        (XBound, new(@"\b[A-Za-z_]*[xX]\d?\s*(>=|>|<=|<)\s*\(?\s*[\w.]*[sS]ize\w*\b|\b[\w.]*[sS]ize\w*\s*(-\s*1\s*)?(>=|>|<=|<)\s*[A-Za-z_]*[xX]\d?\b")),
    ];

    private const string ColumnExtraction = "raw column extraction (cell % size)";
    private const string XBound = "raw x bound against a size (x < size, nx > size − 1)";

    /// <summary>The full-row scan <c>for (int x = 0; x &lt; size; x++)</c> visits every column once — not a bound a
    /// cylinder lacks.</summary>
    private static readonly Regex FullRowScan = new(@"^for\s*\(\s*int\s+(\w+)\s*=\s*0\s*;\s*\1\s*<\s*[\w.]*[sS]ize\w*\s*;\s*\1\s*\+\+\s*\)");

    /// <summary>Whether <paramref name="line"/> (trimmed) matches idiom <paramref name="name"/>.</summary>
    private static bool Flags(string name, Regex pattern, string line) =>
        pattern.IsMatch(line) && !(name == XBound && FullRowScan.IsMatch(line));

    /// <summary>Files that own the topology (whole-file exclusion).</summary>
    private static readonly string[] Chokepoint =
    [
        "Sim.Core/Pathing/MapTopology.cs",
        "Sim.Core/Pathing/TraversalLattice.cs",
        "Sim.Core/Worldgen/Worldgen.cs",
    ];

    /// <summary>(file, exact trimmed line, reason), optionally with the number of occurrences of that line in the file
    /// (default 1). Each must still match exactly that often.</summary>
    private static readonly (string File, string Line, string Reason)[] Allowlist =
    [
        ("Sim.Core/Worldgen/SettlementSiting.cs",
            "int x0 = full ? 0 : Math.Max(0, x - r), x1 = full ? size - 1 : Math.Min(size - 1, x + r);",
            "BoxSum's FLAT, full-width or interior window — taken only when the window does not cross the seam; seam windows are split explicitly under wrapX (pinned bit-identical by CylinderWorldgenTests)."),
        ("Sim.Core/State/PolityKnowledge.cs",
            "if (!(x >= 0.0) || x >= _terrainSize) return -1;",
            "NodeAt's FLAT branch (a position off a flat map is refused); the wrapped branch normalises through MapTopology.WrapX."),
        ("Sim.Core/State/SiteAssessment.cs",
            "int x0 = Math.Max(0, x - r), x1 = Math.Min(size - 1, x + r);",
            "KnowsSquare's FLAT branch (BoxSum's clamped flat window); the wrapped branch walks MapTopology.NeighbourX."),
        ("Sim.Ui/Art/HeaderRuleBaker.cs",
            "double dx = px - cx;",
            "chrome art (a header rule bitmap), not map geometry."),
        // ---- WS3 RENDERING STAGE (W2): the Sim.Ui map sites W1 left pending, each converted and re-reviewed. What remains
        // here is a FLAT branch (the wrapped branch goes through MapTopology) or screen-space geometry (no world x delta).
        ("Sim.Ui/World/MapCells.cs",
            "if (!(wx >= 0.0) || wx >= terrainSize) return -1;",
            "MapCells.At's FLAT branch (a pointer off a flat map picks nothing); the wrapped branch floors and normalises through MapTopology.WrapX."),
        ("Sim.Ui/World/ResourceOverlay.cs",
            "int x0 = Math.Max(0, x - r), x1 = Math.Min(size - 1, x + r);",
            "WindowSum's FLAT branch (BoxSum's clamped flat window); the wrapped branch walks MapTopology.NeighbourX at each column's shortest offset, mirroring BoxSum's modular window."),
        ("Sim.Ui/World/RoadLens.cs",
            "double dx = bx - ax, dy = by - ay, len = Math.Sqrt(dx * dx + dy * dy);",
            "SCREEN-space (after toScreen): a route label's direction along a route already unwrapped in the projection (MapTopology.UnwrapNear) — no world x delta."),
        ("Sim.Ui/Headless/PlayabilityGate.cs",
            "return 2 * hw < size && p.X >= hw && p.X <= size - hw && yAllows;",
            "ClampAllowsCentre's FLAT branch (a flat camera's x clamp); on a wrapped camera the row is a wrap assertion (y alone can stop the centring)."),
        // ---- W1 fix F4: the structural idioms' reviewed lines. Sim.Core first.
        ("Sim.Core/Worldgen/Hydrology.cs",
            "int x = i % size, y = i / size;",
            "x3: the expanded cell's own column — the flat border test / pole-only sink test, and the start of MapTopology.NeighbourX."),
        ("Sim.Core/Worldgen/SettlementSiting.cs",
            "int x = i % size, y = i / size, d = dist[i] + 1;",
            "WaterDistanceBfs: the expanded cell's column; its W and E neighbours go through MapTopology.NeighbourX."),
        ("Sim.Core/Worldgen/WorldFounding.cs",
            "Pathing.MapTopology.WrapCoord(site % size, size), site / size, Experience: 0.0, Army: 0));",
            "MilitaryUnitRow X: the site's column, written through MapTopology.WrapCoord."),
        ("Sim.Core/Worldgen/WorldFounding.cs",
            "int cx = site % size, cy = site / size;",
            "HinterlandMeans: the disc's centre column; its offsets go through MapTopology (each column once, at its shortest offset)."),
        ("Sim.Core/Worldgen/WorldgenStats.cs",
            "int x = i % size, y = i / size;",
            "FertilityRiverCorrelation's BFS: the expanded cell's column; neighbours via MapTopology.NeighbourX."),
        ("Sim.Core/Worldgen/WorldgenConfig.cs",
            "if (cfg.ContinentalMask.EdgeTaperPx < 1 || cfg.ContinentalMask.EdgeTaperPx > cfg.SizePx / 4)",
            "config validation (the taper width against the map size), not a coordinate."),
        ("Sim.Core/State/InfoQuery.cs",
            "b.Title = (terrain.Water[cell] >= 0.5 ? \"Water\" : \"Land\") + \" at \" + Inv(cell % size) + \", \" + Inv(cell / size);",
            "display: a cell's own (normalised) column in the card title."),
        ("Sim.Core/State/SiteAssessment.cs",
            "return new Report { Cell = cell, X = cell >= 0 ? cell % size : -1, Y = cell >= 0 ? cell / size : -1, Known = false };",
            "display: the refused cell's own column."),
        ("Sim.Core/State/SiteAssessment.cs",
            "int x = cell % size, y = cell / size;",
            "the assessed cell's column: the centre handed to the Knows* reach helpers, which wrap via PolityKnowledge.WrapX."),
        // ---- Sim.Ui. Positions only (no x delta): each is a cell's or node's own column, drawn or reported where it is.
        // The WS3 rendering stage draws every one of them at each visible copy k (spec "Copies") and must re-review them.
        ("Sim.Ui/GameUi.Placement.cs",
            "string where = \"Chosen: the place at \" + (_placement.Selected % size).ToString(System.Globalization.CultureInfo.InvariantCulture)",
            "display: the chosen cell's own column in the placement text."),
        ("Sim.Ui/Headless/PlayabilityGate.Placement.cs",
            "return Ui.Camera.WorldToScreenNearest(cell % size + 0.5, cell / size + 0.5, Ui.MapRect);",
            "gate: a cell's own centre, placed at its copy nearest the view (Camera.WorldToScreenNearest)."),
        ("Sim.Ui/Art/UiTheme.cs",
            "if (px >= sizePx - 0.01) return font;",
            "a font raster size, not map geometry."),
        ("Sim.Ui/Art/UiTheme.cs",
            "foreach ((" + "fl" + "oat px, _) in rasters) if (px >= sizePx - 0.01) return px;",   // split: the banned-constructs gate greps the keyword
            "a font raster size, not map geometry."),
        ("Sim.Ui/World/MapCells.cs",
            "int x = cell % terrainSize, y = cell / terrainSize;",
            "Footprint: a (normalised) cell's own column; on a wrapped camera its copy is chosen by MapTopology.NearestCopy."),
        ("Sim.Ui/World/PlacementOverlay.cs",
            "private static (int X, int Y) CellXY(int cell, int size) => (cell % size, cell / size);",
            "a placement mark's own cell position; its copies are drawn at world x + k·size, the route unwrapped by MapTopology.UnwrapNear."),
        ("Sim.Ui/World/ResourceOverlay.cs",
            "int x = cell % size, y = cell / size;",
            "the sampled cell's column, handed to SiteAssessment's wrapped reach helpers (no delta)."),
        ("Sim.Ui/World/ResourceOverlay.cs",
            "int x = row.SiteCell % size, y = row.SiteCell / size;",
            "a site's column, handed to SiteAssessment.KnowsDisc (wrapped via PolityKnowledge.WrapX)."),
        ("Sim.Ui/World/WorldLens.cs",
            "id.Value, name(id.Value), cell % size + 0.5, cell / size + 0.5, ctl, capital, ctl == player.Value,",
            "a settlement's own position in [0, size); the lens draws it at every visible copy (WorldLens.Paint wrapX)."),
        ("Sim.Ui/World/WorldLens.cs",
            "x = cell % size + 0.5;",
            "a route end's own position (SiteOf); the far end is unwrapped by MapTopology.UnwrapNear."),
        ("Sim.Ui/World/WorldLens.cs",
            "(node % lsize * stride + stride / 2.0, node / lsize * stride + stride / 2.0);",
            "a lattice node's own centre (geometry from MapTopology.LatticeGrid); a step's far end is unwrapped by MapTopology.UnwrapNear."),
        ("Sim.Ui/ViewModel/CameraFocus.cs",
            "(double sx, double sy) = camera.WorldToScreenNearest(cell % terrainSize + 0.5, cell / terrainSize + 0.5, view);",
            "a cell's own centre, placed at its copy nearest the view (Camera.WorldToScreenNearest)."),
        ("Sim.Ui/ViewModel/OverlayMeshes.cs",
            "new(node % latticeSize * stride + stride / 2.0,",
            "TEST-ONLY mesh (WorldLayerOwnershipTests forbids it in the renderer): a lattice node's own centre; flat geometry, never drawn by the game."),
        ("Sim.Ui/ViewModel/OverlayMeshes.cs",
            "double x0 = node % latticeSize * stride, y0 = node / latticeSize * stride;",
            "x2: TEST-ONLY mesh: a lattice block's own corner; never drawn by the game."),
        ("Sim.Ui/ViewModel/OverlayMeshes.cs",
            "new(settlement.SiteCell % terrainSize + 0.5, settlement.SiteCell / terrainSize + 0.5);",
            "a settlement's own position in [0, size) (CameraFocus places it at its nearest copy)."),
        ("Sim.Ui/ViewModel/RiverMesh.cs",
            "new(cell % terrainSize + 0.5, cell / terrainSize + 0.5);",
            "a river cell's own centre; each segment's far end is unwrapped relative to its first (RiverMesh.Unwrapped, MapTopology.UnwrapNear)."),
        ("Sim.Ui/ViewModel/UnitCardModel.cs",
            "return unit.X == cell % size && unit.Y == cell / size;",
            "identity, not distance: a unit's normalised X (written through WrapCoord) against its settlement cell's column."),
    ];

    private static IEnumerable<string> SourceFiles(string root)
    {
        foreach (string project in new[] { "Sim.Core", "Sim.Ui" })
            foreach (string f in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/")) continue;
                yield return rel;
            }
    }

    private static bool IsComment(string trimmed) =>
        trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal);

    /// <summary>The number of occurrences an allowlist entry expects: N when its reason starts "xN:", else 1.</summary>
    private static int ExpectedCount((string File, string Line, string Reason) e)
    {
        Match m = Regex.Match(e.Reason, @"^x(\d+):");
        return m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 1;
    }

    /// <summary>Every (file, line number, idiom, trimmed text) the idioms flag outside the chokepoint, in path order.</summary>
    private static List<(string File, int LineNo, string Idiom, string Text)> Flagged(string root, out int scanned)
    {
        var flagged = new List<(string, int, string, string)>();
        scanned = 0;
        foreach (string rel in SourceFiles(root).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (Array.IndexOf(Chokepoint, rel) >= 0) continue;
            scanned++;
            string[] lines = File.ReadAllLines(Path.Combine(root, rel));
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.Length == 0 || IsComment(t)) continue;
                foreach ((string name, Regex pattern) in Idioms)
                    if (Flags(name, pattern, t)) { flagged.Add((rel, i + 1, name, t)); break; }   // a line counts once
            }
        }
        return flagged;
    }

    [Fact]
    public void NoFileOutsideTheChokepoint_ComputesAnXDeltaNeighbourOrBoundFromRawCellArithmetic()
    {
        string root = RepoPaths.Root();
        var violations = new List<string>();
        var count = new int[Allowlist.Length];
        List<(string File, int LineNo, string Idiom, string Text)> flagged = Flagged(root, out int scanned);
        foreach ((string rel, int lineNo, string name, string t) in flagged)
        {
            int a = Array.FindIndex(Allowlist, e => e.File == rel && e.Line == t);
            if (a >= 0) { count[a]++; continue; }
            violations.Add($"{rel}:{lineNo}: {name}: {t}");
        }
        Assert.True(scanned > 100, "the guard scanned almost nothing — the rig is void");
        Assert.True(violations.Count == 0,
            "raw east-west geometry outside MapTopology (route it through Sim.Core.Pathing.MapTopology, or allowlist it with a reason):\n"
            + string.Join("\n", violations));
        var stale = new List<string>();
        for (int a = 0; a < Allowlist.Length; a++)
            if (count[a] != ExpectedCount(Allowlist[a]))
                stale.Add($"{Allowlist[a].File}: {Allowlist[a].Line} (expected {ExpectedCount(Allowlist[a])}, found {count[a]})");
        Assert.True(stale.Count == 0,
            "allowlist entries whose occurrence count no longer matches (a stale exemption, or a new copy of an exempt line):\n"
            + string.Join("\n", stale));
    }

    [Fact]
    public void TheGuardHasTeeth_EveryPreBatch3SiteIdiomIsCaught()
    {
        // The pre-batch-3 text of a site of each idiom (topology.md §2 class A) — each must be flagged.
        string[] before =
        [
            "long dx = cellA % size - cellB % size, dy = cellA / size - cellB / size;",          // GeographicDistance.Km
            "long dx = row.SiteCell % size - x, dy = row.SiteCell / size - y;",                   // SiteAssessment nearest
            "if (nx < 0 || ny < 0 || nx >= lattice.Size || ny >= lattice.Size) continue;",        // Pathfinder.Expand
            "if (x < 0 || x >= lattice.Size) continue;",                                          // LatticeMap ring scan
            "if (a != b && Math.Abs(ax - bx) <= 1 && Math.Abs(ay - by) <= 1)",                    // Pathfinder.LegCosts
            "int nx = x + Dx[d], ny = y + Dy[d];",                                                // every neighbour scan
            "double dx = x - gx, dy = y - gy;",                                                   // Pathfinder.Heuristic
            "if (x < size - 1 && dist[i + 1] == int.MaxValue) { dist[i + 1] = d; queue[tail++] = i + 1; }",   // WaterDistanceBfs
            "int x0 = Math.Max(0, x - r), x1 = Math.Min(size - 1, x + r);",                       // BoxSum window
            // W1 fix F4: equivalent rewrites the historical idioms missed (verifier mutants M45, M46, M47).
            "long dx = (cellA % size) - (cellB % size), dy = cellA / size - cellB / size;",      // M45 parenthesised
            "int xa = cellA % size, xb = cellB % size;",                                          // M46 named columns
            "if (nx < 0 || ny < 0 || nx > lattice.Size - 1 || ny >= lattice.Size) continue;",    // M47 bound as size − 1
            "int col = cell % terrain.Size;",                                                     // a column under any name
            "if (lattice.Size - 1 < unitX) return;",                                              // a bound written the other way
            "if (newX >= _terrainSize) newX -= _terrainSize;",                                    // a movement system's hand-rolled wrap
        ];
        foreach (string line in before)
            Assert.True(Idioms.Any(p => Flags(p.Name, p.Pattern, line)), "the guard misses: " + line);

        // ...and does not flag the full-row scan, which is no bound.
        Assert.DoesNotContain(Idioms, p => Flags(p.Name, p.Pattern, "for (int x = 0; x < size; x++)"));
        Assert.DoesNotContain(Idioms, p => Flags(p.Name, p.Pattern, "for (int y = 0; y < size; y++)"));
    }

    /// <summary>W1 fix F4 — the STRUCTURAL idioms against whole files: the verifier's surviving mutants M45 and M46
    /// (GeographicDistance.Km rewritten without MapTopology) placed in a scratch copy of Sim.Core's tree are each flagged
    /// by the same scan the guard runs — the end-to-end check that the allowlist cannot absorb them (neither line is
    /// allowlisted for that file).</summary>
    [Theory]
    [InlineData("long dx = (cellA % size) - (cellB % size), dy = cellA / size - cellB / size;")]
    [InlineData("int xa = cellA % size, xb = cellB % size;")]
    public void ARawRewriteOfGeographicDistance_IsFlagged_NotAllowlisted(string line)
    {
        Assert.Contains(Idioms, p => Flags(p.Name, p.Pattern, line));
        Assert.DoesNotContain(Allowlist, e => e.File == "Sim.Core/State/GeographicDistance.cs" && e.Line == line);
    }

    [Fact]
    public void OnlyWorldgenGenerate_ReadsTheTopologyConfig()
    {
        // WorldgenConfig.Topology is the switch; TerrainSet.WrapX is the fact. Reads of `.Topology` in Sim.Core, Sim.Ui
        // and Sim.Cli are allowed only in Worldgen.cs (the generator) and WorldgenConfig.cs (the loader's validation and
        // the record itself); writers that override it (`with { Topology = … }`) are config plumbing, not readers.
        string root = RepoPaths.Root();
        var reader = new Regex(@"\.Topology\b(?!\s*=)");
        var violations = new List<string>();
        foreach (string project in new[] { "Sim.Core", "Sim.Ui", "Sim.Cli" })
            foreach (string f in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/")) continue;
                if (rel is "Sim.Core/Worldgen/Worldgen.cs" or "Sim.Core/Worldgen/WorldgenConfig.cs") continue;
                string[] lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].Trim();
                    if (IsComment(t)) continue;
                    if (reader.IsMatch(t)) violations.Add($"{rel}:{i + 1}: {t}");
                }
            }
        Assert.True(violations.Count == 0, "WorldgenConfig.Topology read outside Worldgen.Generate:\n" + string.Join("\n", violations));
    }
}
