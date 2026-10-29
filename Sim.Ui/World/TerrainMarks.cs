using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World;

/// <summary>The kinds of terrain mark the object layer draws (<see cref="TerrainMarks"/>).</summary>
public enum TerrainMarkKind : byte { None = 0, Hill = 1, Mountain = 2, Peak = 3, Forest = 4 }

/// <summary>M6 E1.3: one cultivated field — a known, fertile catchment node of a cultivating settlement — its lattice node's
/// box in world px (<paramref name="X"/>, <paramref name="Y"/> its top-left, <paramref name="Size"/> its side).</summary>
public readonly record struct FieldMark(double X, double Y, double Size, int Node, int Settlement);

/// <summary>
/// M6 E1.3 — TERRAIN RELIEF, FORESTS AND FIELDS IN THE OBJECT LAYER (D-038 G2 "terrain has relief"; style bible §4 items 8–9,
/// §8). The SUBSTRATE IS UNTOUCHED: the parchment bake, the washes, coasts and rivers are the GPU pass's and stay
/// byte-identical in every era (<c>EraSurfaceTests.TheTerrainSubstrate_IsByteIdenticalInEveryEra</c>); these are OBJECTS
/// drawn on the paper by the lens, under the one light (<see cref="ObjectLight"/>), in identity inks (the same in every era).
/// <para>THE BAKE (<see cref="Of"/>): once per terrain (immutable, ADR-008), a mark per cell of a <see cref="PitchPx"/>-px
/// grid, classified by hard thresholds on the worldgen rasters at the cell's centre — elevation above sea on a fixed scale
/// (<see cref="ReliefSpan"/>; no whole-raster statistic, so unknown terrain moves nothing known), for <see cref="TerrainMarkKind.Hill"/>,
/// <see cref="TerrainMarkKind.Mountain"/> and <see cref="TerrainMarkKind.Peak"/>; moisture, temperature and fertility below
/// the hills for <see cref="TerrainMarkKind.Forest"/>. (Hard classes are right for OBJECTS — a mountain is there or not; the
/// style bible's continuity rule binds the washes, which this does not touch.) DECORATIVE: the marks re-draw what the
/// rasters already hold at a 4 km cell (D-015) and never suggest a resource or a class the simulation lacks — the legend
/// says so.</para>
/// <para>THE PAINT (<see cref="Paint"/>): only cells on nodes the observer KNOWS, only in view, decimated by a power of two so
/// the marks stand at least <see cref="MinScreenPitchPx"/> apart on screen (a mark then stands for its decimated block),
/// each placed at a jitter seeded from its cell index (<see cref="FrameNoise"/>: pure, the same every frame and process).</para>
/// </summary>
public sealed class TerrainMarks
{
    /// <summary>The bake grid's pitch in world px: two lattice nodes (8 km at the canonical 4 km per px).</summary>
    public const int PitchPx = 8;

    /// <summary>The least screen distance between two drawn marks: the decimation step is the smallest power of two that
    /// reaches it.</summary>
    public const double MinScreenPitchPx = 30.0;

    /// <summary>The elevation span (raster units above the sea level) the relief thresholds are fractions of. MEASURED: the
    /// seed-42 terrains (1024 and 256 px) rise 0.297 and 0.307 above their sea levels.</summary>
    public const double ReliefSpan = 0.30;

    /// <summary>Elevation above sea as a fraction of <see cref="ReliefSpan"/> from which a cell is a hill, a mountain, a
    /// peak with a pale cap.</summary>
    public const double HillAt = 0.30, MountainAt = 0.46, PeakAt = 0.66;

    /// <summary>A cell below the hills is forest when its moisture reaches <see cref="ForestMoisture"/>, its temperature lies
    /// in [<see cref="ForestMinC"/>, <see cref="ForestMaxC"/>] and its fertility reaches <see cref="ForestFertility"/>.</summary>
    public const double ForestMoisture = 0.56, ForestMinC = -4.0, ForestMaxC = 26.0, ForestFertility = 0.18;

    /// <summary>A catchment node is FERTILE ground for a field from this fertility.</summary>
    public const double FieldFertility = 0.30;

    private const int JitterSalt = 0x4531, SizeSalt = 0x4532, ThinSalt = 0x4534;

    /// <summary>The largest screen pitch one mark stands for; closer in, a cell holds up to <see cref="MaxSub"/>² marks.</summary>
    public const double MaxMarkPitchPx = 48.0;

    /// <summary>The most mark places (drawn or left bare) one frame's viewport holds — about a 1280 × 800 window at the
    /// 30-px minimum pitch.</summary>
    public const double MaxPlacesPerFrame = 1150.0;

    /// <summary>The most marks per cell side.</summary>
    public const int MaxSub = 3;

    /// <summary>The share of mark places left bare (seeded per place), and how far a mark strays from its place (× the
    /// pitch) — so the marks read as a hand's, not as a grid.</summary>
    public const double ThinFraction = 0.28, JitterFraction = 0.34;

    /// <summary>The grid's side in cells.</summary>
    public int Cells { get; }

    /// <summary>The terrain's side in world px.</summary>
    public int Size { get; }

    private readonly TerrainMarkKind[] _kind;

    private TerrainMarks(int size, TerrainMarkKind[] kind)
    {
        Size = size;
        Cells = size / PitchPx;
        _kind = kind;
    }

    /// <summary>The mark of cell (<paramref name="gx"/>, <paramref name="gy"/>).</summary>
    public TerrainMarkKind KindAt(int gx, int gy) => _kind[gy * Cells + gx];

    /// <summary>How many cells carry each kind (indexed by <see cref="TerrainMarkKind"/>).</summary>
    public int[] Census()
    {
        var n = new int[5];
        foreach (TerrainMarkKind k in _kind) n[(int)k]++;
        return n;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TerrainSet, TerrainMarks> Baked = new();

    /// <summary>The marks of <paramref name="terrain"/>, baked once (a memo keyed by the immutable terrain, never state).</summary>
    public static TerrainMarks Of(TerrainSet terrain) => Baked.GetValue(terrain, Bake);

    /// <summary>The bake (see the type's remarks). Pure: the rasters in, the marks out.</summary>
    public static TerrainMarks Bake(TerrainSet t)
    {
        int n = t.Size, cells = n / PitchPx;
        ReadOnlySpan<double> elev = t.Elevation, water = t.Water, moist = t.Moisture, temp = t.Temperature, fert = t.Fertility, rivers = t.Rivers;
        // A FIXED scale above the sea, never a statistic of the whole raster (the terrain's maximum): a global statistic would
        // let terrain the player has not seen move a mark on terrain he has (the fog's leak tests perturb unknown cells).
        double sea = t.SeaLevel, span = ReliefSpan;
        var kind = new TerrainMarkKind[cells * cells];
        for (int gy = 0; gy < cells; gy++)
            for (int gx = 0; gx < cells; gx++)
            {
                int i = (gy * PitchPx + PitchPx / 2) * n + gx * PitchPx + PitchPx / 2;
                if (water[i] >= 0.5 || rivers[i] > 0.5) continue;
                double e = (elev[i] - sea) / span;
                TerrainMarkKind k = e >= PeakAt ? TerrainMarkKind.Peak
                    : e >= MountainAt ? TerrainMarkKind.Mountain
                    : e >= HillAt ? TerrainMarkKind.Hill
                    : moist[i] >= ForestMoisture && temp[i] >= ForestMinC && temp[i] <= ForestMaxC && fert[i] >= ForestFertility ? TerrainMarkKind.Forest
                    : TerrainMarkKind.None;
                kind[gy * cells + gx] = k;
            }
        return new TerrainMarks(n, kind);
    }

    /// <summary>
    /// M6 E1.3 — THE FIELDS of the cultivating settlements (<paramref name="cultivating"/>: settlement id and farming share,
    /// in table order): for each, its catchment nodes the observer knows on fertile land (fertility ≥
    /// <see cref="FieldFertility"/>, not water), nearest its site first (the composite key (squared distance, node index) —
    /// integers both), the first ⌈share × count⌉ of them. A read of state; nothing here is stored.
    /// </summary>
    public static List<FieldMark> FieldsOf(IReadOnlyWorldState world, IReadOnlyList<(int Settlement, double Share)> cultivating,
        PlayerKnowledge known, int lsize, int stride)
    {
        var fields = new List<FieldMark>();
        TerrainSet? t = world.Terrain;
        if (t is null || cultivating.Count == 0 || lsize <= 0) return fields;
        int n = t.Size;
        ReadOnlySpan<double> fert = t.Fertility, water = t.Water;
        foreach ((int id, double share) in cultivating)
        {
            int site = -1;
            for (int s = 0; s < world.Settlements.Count; s++) if (world.Settlements[s].Id.Value == id) site = world.Settlements[s].SiteCell;
            if (site < 0) continue;
            int sx = site % n, sy = site / n;
            var nodes = new List<(long Dist, int Node)>();
            for (int c = 0; c < world.CatchmentNodes.Count; c++)
            {
                CatchmentNodeRow row = world.CatchmentNodes[c];
                if (row.Settlement.Value != id || !known.Known(row.LatticeNode)) continue;
                int nx = row.LatticeNode % lsize * stride + stride / 2, ny = row.LatticeNode / lsize * stride + stride / 2;
                if (nx >= n || ny >= n) continue;
                int cell = ny * n + nx;
                if (water[cell] >= 0.5 || fert[cell] < FieldFertility) continue;
                long dx = Sim.Core.Pathing.MapTopology.AbsDx(nx, sx, n, t.WrapX);
                long dy = ny - sy;
                nodes.Add((dx * dx + dy * dy, row.LatticeNode));
            }
            nodes.Sort(static (a, b) => a.Dist != b.Dist ? a.Dist.CompareTo(b.Dist) : a.Node.CompareTo(b.Node));
            int take = (int)Math.Min(nodes.Count, Math.Ceiling(Math.Clamp(share, 0.0, 1.0) * nodes.Count));
            for (int i = 0; i < take; i++)
            {
                int node = nodes[i].Node;
                fields.Add(new FieldMark(node % lsize * stride, node / lsize * stride, stride, node, id));
            }
        }
        return fields;
    }

    /// <summary>The decimation step at <paramref name="scale"/> screen px per world px: the smallest power of two putting
    /// two drawn marks at least <see cref="MinScreenPitchPx"/> apart.</summary>
    public static int StepAt(double scale)
    {
        int step = 1;
        while (step < 1 << 12 && PitchPx * step * scale < MinScreenPitchPx) step <<= 1;
        return step;
    }

    /// <summary>
    /// Paints the fields, then the relief and forest marks, of <paramref name="p"/> in view. <paramref name="at"/> maps a
    /// world point of copy c to the screen (the lens's affine camera); <paramref name="copies"/> are the wrapped paint's
    /// copies. Returns the draws made at a copy other than 0 (the lens's WS3 count).
    /// </summary>
    public static int Paint(DrawList d, MapInk k, WorldProjection p, Func<int, double, double, (double X, double Y)> at, double scale,
        RectD viewport, int[] copies, double worldW)
    {
        int extra = 0;
        // The fields first (they lie flattest), then the marks over them.
        foreach (FieldMark f in p.Fields)
            foreach (int c in copies)
            {
                (double x0, double y0) = at(c, f.X, f.Y);
                double s = f.Size * scale;
                if (x0 > viewport.Right || y0 > viewport.Bottom || x0 + s < viewport.X || y0 + s < viewport.Y) continue;
                Field(d, k, x0, y0, s, f.Node);
                if (c != 0) extra++;
            }
        if (p.Terrain is not { } marks) return extra;
        HashSet<int>? fieldNodes = null;
        if (p.Fields.Count > 0)
        {
            fieldNodes = new HashSet<int>();
            foreach (FieldMark f in p.Fields) fieldNodes.Add(f.Node);   // membership only: never iterated
        }
        int step = StepAt(scale);
        double cellPx = PitchPx * step * scale;
        // Closer in, a cell holds several marks (sub × sub), each at least MinScreenPitchPx apart, so a forest stays a forest
        // of trees and a mark never outgrows MaxMarkPitchPx.
        int sub = step > 1 ? 1 : Math.Clamp((int)Math.Floor(cellPx / MaxMarkPitchPx), 1, MaxSub);
        // A BOUNDED NUMBER OF MARK PLACES PER FRAME (frame cost, MEASURED: on a 4K window the 30-px pitch puts nine times the
        // 1280 x 800 window's marks on screen): fewer marks per cell, then a coarser decimation, until the viewport holds at
        // most MaxPlacesPerFrame places — so a larger window draws larger marks, not more of them.
        double Places(double pitchPx) => viewport.W / pitchPx * (viewport.H / pitchPx);
        while (sub > 1 && Places(cellPx / sub) > MaxPlacesPerFrame) sub--;
        while (sub == 1 && step < 1 << 12 && Places(cellPx) > MaxPlacesPerFrame) { step <<= 1; cellPx *= 2; }
        double pitch = cellPx / sub;
        foreach (int c in copies)
        {
            (double ox, double oy) = at(c, 0, 0);
            // The cells whose marks (each within a pitch of its sub-cell) can touch the viewport.
            int gx0 = (int)Math.Floor(((viewport.X - cellPx - ox) / scale) / (PitchPx * step)) * step;
            int gx1 = (int)Math.Ceiling(((viewport.Right + cellPx - ox) / scale) / (PitchPx * step)) * step;
            int gy0 = (int)Math.Floor(((viewport.Y - cellPx - oy) / scale) / (PitchPx * step)) * step;
            int gy1 = (int)Math.Ceiling(((viewport.Bottom + cellPx - oy) / scale) / (PitchPx * step)) * step;
            gx0 = Math.Max(0, gx0); gy0 = Math.Max(0, gy0);
            gx1 = Math.Min(marks.Cells - 1, gx1); gy1 = Math.Min(marks.Cells - 1, gy1);
            for (int gy = gy0; gy <= gy1; gy += step)
                for (int gx = gx0; gx <= gx1; gx += step)
                {
                    TerrainMarkKind kind = marks.KindAt(gx, gy);
                    if (kind == TerrainMarkKind.None) continue;
                    // A cleared field is no forest: a forest mark whose cell's node is cultivated is not drawn.
                    if (kind == TerrainMarkKind.Forest && fieldNodes is not null
                        && fieldNodes.Contains((gy * PitchPx + PitchPx / 2) / p.Stride * p.LatticeSize + (gx * PitchPx + PitchPx / 2) / p.Stride)) continue;
                    // The mark's kind is sampled at its cell's centre: drawn only when THAT node is known (and, below, where it
                    // stands), so nothing of an unknown node's terrain reaches the map.
                    if (p.KnownNodes is { } sampled && !KnownAt(sampled, p.LatticeSize, p.Stride, gx * PitchPx + PitchPx / 2, gy * PitchPx + PitchPx / 2)) continue;
                    int cell = gy * marks.Cells + gx;
                    for (int j = 0; j < sub; j++)
                        for (int i = 0; i < sub; i++)
                        {
                            int seed = cell * (MaxSub * MaxSub) + j * MaxSub + i;
                            if (FrameNoise.U(seed, ThinSalt, 0) < ThinFraction) continue;   // break the grid: some places bare
                            double wx = (gx + (i + 0.5) * step / (double)sub) * PitchPx, wy = (gy + (j + 0.5) * step / (double)sub) * PitchPx;
                            if (p.KnownNodes is { } knownNodes && !KnownAt(knownNodes, p.LatticeSize, p.Stride, wx, wy)) continue;
                            (double sx, double sy) = at(c, wx, wy);
                            sx += FrameNoise.S(seed, JitterSalt, 0) * pitch * JitterFraction;
                            sy += FrameNoise.S(seed, JitterSalt, 1) * pitch * JitterFraction;
                            if (sx + pitch < viewport.X || sx - pitch > viewport.Right || sy + pitch < viewport.Y || sy - pitch > viewport.Bottom) continue;
                            Mark(d, k, kind, sx, sy, pitch, seed);
                            if (c != 0) extra++;
                        }
                }
        }
        return extra;
    }

    private static bool KnownAt(bool[] known, int lsize, int stride, double wx, double wy)
    {
        int nx = (int)(wx / stride), ny = (int)(wy / stride);
        if (nx < 0 || ny < 0 || nx >= lsize || ny >= lsize) return false;
        return known[ny * lsize + nx];
    }

    /// <summary>
    /// ONE TERRAIN MARK at (<paramref name="x"/>, <paramref name="y"/>) for a block <paramref name="pitch"/> px across, under the
    /// one light: a PEAK (a mountain with a pale cap), a MOUNTAIN (a ridge: its west face lit, its east face in shade), a
    /// HILL (a low mound, lit on its west shoulder), or FOREST (two crowns, each lit on its north-west). Its size varies by a
    /// seed of its <paramref name="cell"/>; every polygon convex.
    /// </summary>
    public static void Mark(DrawList d, MapInk k, TerrainMarkKind kind, double x, double y, double pitch, int cell)
    {
        double v = FrameNoise.U(cell, SizeSalt, 0);
        switch (kind)
        {
            case TerrainMarkKind.Peak:
            case TerrainMarkKind.Mountain:
            {
                double w = pitch * (0.62 + 0.18 * v), h = pitch * (kind == TerrainMarkKind.Peak ? 0.62 : 0.46) * (0.85 + 0.3 * v);
                double foot = y + h * 0.45, apexX = x + (v - 0.5) * w * 0.2, apexY = foot - h;
                (double X, double Y)[] body = [(x - w / 2, foot), (apexX, apexY), (x + w / 2, foot)];
                ObjectPaint.Solid(d, k, body, MapInk.Shade(k.ReliefUpland));
                d.Polygon([(x - w / 2, foot), (apexX, apexY), (apexX + w * 0.06, foot)], MapInk.Lit(k.ReliefUpland));
                if (kind == TerrainMarkKind.Peak)
                {
                    double t = 0.32;   // the cap: the top third of the mountain
                    d.Polygon([(apexX + (x - w / 2 - apexX) * t, apexY + h * t), (apexX, apexY), (apexX + (x + w / 2 - apexX) * t, apexY + h * t)], k.ReliefPeak);
                }
                break;
            }
            case TerrainMarkKind.Hill:
            {
                double w = pitch * (0.56 + 0.16 * v), h = pitch * 0.22 * (0.85 + 0.3 * v), foot = y + h * 0.5;
                (double X, double Y)[] mound = Mound(x, foot, w / 2, h, 7);
                ObjectPaint.Solid(d, k, mound, MapInk.Shade(k.ReliefHill));
                d.Polygon(Mound(x - w * 0.1, foot, w * 0.36, h * 0.92, 5), MapInk.Lit(k.ReliefHill));
                break;
            }
            case TerrainMarkKind.Forest:
            {
                double r = pitch * (0.16 + 0.05 * v);
                bool two = pitch >= 30;
                if (two) Crown(d, k, x - r * 0.9, y - r * 0.35, r * 0.9);
                Crown(d, k, two ? x + r * 0.55 : x, y + r * 0.25, r);
                break;
            }
        }
    }

    /// <summary>A tree crown of radius <paramref name="r"/>: the solid in the forest's shade, its lit cap to the north-west.</summary>
    private static void Crown(DrawList d, MapInk k, double x, double y, double r)
    {
        ObjectPaint.Solid(d, k, ObjectPaint.Ngon(x, y, r, r, 8), MapInk.Shade(k.Forest));
        d.Polygon(ObjectPaint.Ngon(x - r * 0.22, y - r * 0.22, r * 0.62, r * 0.62, 7), k.Forest);
    }

    /// <summary>The upper half of an ellipse of half-width <paramref name="rx"/> and height <paramref name="h"/> standing on
    /// (<paramref name="cx"/>, <paramref name="foot"/>), <paramref name="n"/> points (convex).</summary>
    private static (double X, double Y)[] Mound(double cx, double foot, double rx, double h, int n)
    {
        var p = new (double X, double Y)[n];
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI * i / (n - 1);
            p[i] = (cx - rx * Math.Cos(a), foot - h * Math.Sin(a));
        }
        return p;
    }

    /// <summary>
    /// ONE FIELD on a lattice node's box (top-left (<paramref name="x"/>, <paramref name="y"/>), <paramref name="s"/> px):
    /// flat on the ground (a field casts no shadow) — two strips, crop and fallow, inset from the node's edge, ruled one way
    /// or the other by a seed of its <paramref name="node"/>. Below 6 px one strip.
    /// </summary>
    public static void Field(DrawList d, MapInk k, double x, double y, double s, int node)
    {
        double inset = s * 0.12, x0 = x + inset, y0 = y + inset, x1 = x + s - inset, y1 = y + s - inset;
        // M6 integration (E1 × the continuous level of detail): the fallow strip FADES in from 6 px to 9 px instead of popping
        // in at full strength when the field first splits into two strips.
        Rgba crop = Ink.With(k.FieldCrop, 0.55), fallow = Ink.With(k.FieldFallow, 0.55 * Math.Clamp((s - 6.0) / 3.0, 0.0, 1.0));
        if (s < 6)
        {
            d.Polygon(ObjectPaint.Box(x0, y0, x1, y1), crop);
            return;
        }
        bool across = FrameNoise.U(node, 0x4533, 0) < 0.5;
        if (across)
        {
            double mid = (y0 + y1) / 2;
            d.Polygon(ObjectPaint.Box(x0, y0, x1, mid - s * 0.03), crop);
            d.Polygon(ObjectPaint.Box(x0, mid + s * 0.03, x1, y1), fallow);
        }
        else
        {
            double mid = (x0 + x1) / 2;
            d.Polygon(ObjectPaint.Box(x0, y0, mid - s * 0.03, y1), crop);
            d.Polygon(ObjectPaint.Box(mid + s * 0.03, y0, x1, y1), fallow);
        }
    }
}
