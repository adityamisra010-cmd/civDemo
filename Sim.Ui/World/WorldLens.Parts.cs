using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World;

/// <summary>What a part painter needs to know of one part: whether it is an institution (else a structure kind), its key
/// (the project id or the type key), its content name, and — for a structure — its visual kind.</summary>
public readonly record struct PartSpec(bool Institution, int Key, string Name, StructureKind Kind)
{
    public static PartSpec Of(StructureView st) => new(false, st.ProjectId, st.Name, st.Kind);

    public static PartSpec Of(InstitutionView iv) => new(true, iv.TypeKey, iv.Name, StructureKind.Other);
}

public static partial class WorldLens
{
    // ---------------------------------------------------------------- M6 E1.2: the composed settlement's part painters

    /// <summary>The half-side (× the glyph size g) of the square the stage layouts are authored in.</summary>
    public const double PartHalfBox = 0.72;

    /// <summary>How much larger than its glyph box a part is drawn: 1.3 × <see cref="PartHalfBox"/> ≈ the lattice's
    /// <see cref="CompositionLattice.GlyphHalfBox"/>, so with its contact shadow a part on the outer ring (3.6 units) reaches
    /// at most about 5.2 units from the centre — inside the SMALLEST footprint (5.5 units) at every zoom — while reading at
    /// the size the lot allows (MEASURED on the previews: at 1.0 the parts were a third of the plaza mark's width).</summary>
    public const double PartScale = 1.3;

    /// <summary>One building of a stage's layout: its foot's offset from the lot's centre (× g), its scale, and whether it is
    /// the stage's MAIN building (an institution's emblem goes on its face).</summary>
    private readonly record struct Slot(double Dx, double Base, double Scale, bool Main);

    /// <summary>THE COUNT STAGES' LAYOUTS (D-043 D2), back to front (the painter's order): one building (small), a building
    /// and a wing (expanded), three about a court (campus), five — four about a court and the main hall before them
    /// (complex). Each inside <see cref="PartHalfBox"/>.</summary>
    private static readonly Slot[][] StageLayouts =
    [
        [new(0.0, 0.50, 1.00, true)],
        [new(-0.18, 0.50, 0.82, true), new(0.38, 0.58, 0.50, false)],
        [new(0.02, 0.10, 0.58, false), new(-0.36, 0.58, 0.60, true), new(0.32, 0.62, 0.56, false)],
        [new(-0.34, 0.06, 0.48, false), new(0.30, 0.04, 0.48, false), new(-0.40, 0.62, 0.46, false), new(0.36, 0.64, 0.46, false),
         new(-0.02, 0.44, 0.58, true)],
    ];

    /// <summary>The number of buildings a part draws at <paramref name="stage"/> (0..3).</summary>
    public static int BuildingsAt(int stage) => StageLayouts[Math.Clamp(stage, 0, StageLayouts.Length - 1)].Length;

    /// <summary>
    /// M6 E1.2 — A PART (one structure kind or one institution type, aggregated) at count stage <paramref name="stage"/>,
    /// centred on its lot (<paramref name="x"/>, <paramref name="y"/>), glyph size <paramref name="g"/>, under the one light:
    /// each building a solid with its contact shadow (<see cref="ObjectPaint.Building"/>). A structure keeps its identity
    /// ink (granary silos with domed roofs, a workshop shed with a chimney, any other kind a pitched building) and takes its
    /// roofs from the settlement's style register (<paramref name="register"/>, E1.4 — realized state, never the Age); an
    /// institution is a pedimented hall in its type's ink with its emblem on the main hall's face, chosen from content
    /// (<see cref="PartManifest.InstitutionStyleOf"/>).
    /// </summary>
    public static void PartGlyph(DrawList d, double x, double y, double g, PartSpec spec, int stage, ObjectRegister register,
        MapInk? ink = null)
    {
        MapInk k = ink ?? MapInk.Default;
        g *= PartScale;
        Slot[] layout = StageLayouts[Math.Clamp(stage, 0, StageLayouts.Length - 1)];
        foreach (Slot sl in layout)
        {
            double cx = x + sl.Dx * g, foot = y + sl.Base * g, sc = sl.Scale * g;
            double w = 0.80 * sc, h = 0.50 * sc, roofH = 0.32 * sc, depth = 0.20 * sc;
            if (spec.Institution)
            {
                Rgba wall = k.InstitutionInk(spec.Key);
                ObjectPaint.Building(d, k, cx, foot, w, h, roofH, depth, RoofForm.Gable, wall, k.GlyphInk);
                if (sl.Main) HallEmblem(d, k, cx, foot - h * 0.5, Math.Min(w, h) * 0.36, PartManifest.InstitutionStyleOf(spec.Key, spec.Name), spec.Key);
                continue;
            }
            switch (spec.Kind)
            {
                case StructureKind.Storage:
                    ObjectPaint.Building(d, k, cx, foot, w * 0.8, h * 1.1, roofH, depth, RoofForm.Dome, k.Granary, k.Granary);
                    break;
                case StructureKind.Production:
                    ObjectPaint.Building(d, k, cx - 0.08 * sc, foot, w, h * 0.85, roofH * 0.8, depth, RoofForm.Gable, k.Workshop, k.RoofOf((int)register));
                    // The chimney: its own small solid at the east end of the ridge.
                    double chx = cx + 0.18 * sc, chTop = foot - h * 0.85 - roofH * 0.8 - 0.16 * sc;
                    ObjectPaint.Solid(d, k, ObjectPaint.Box(chx - 0.05 * sc, chTop, chx + 0.05 * sc, foot - h * 0.85 - roofH * 0.3), MapInk.Shade(k.Workshop));
                    break;
                default:
                    ObjectPaint.Building(d, k, cx, foot, w, h, roofH, depth, RoofForm.Gable, k.StructureOther, k.RoofOf((int)register));
                    break;
            }
        }
    }

    /// <summary>An institution's emblem on its hall's face, in the era's hand seeded from the TYPE (every hall of a type is
    /// drawn alike): the five university emblems of H1 (blades, cross, gear, orbit, sheaf), the scales of a court, and the
    /// generic boss for any other type.</summary>
    private static void HallEmblem(DrawList d, MapInk k, double x, double cy, double e, InstitutionEmblem emblem, int typeKey)
    {
        Rgba mark = emblem == InstitutionEmblem.Cross ? k.MedicalMark : k.EmblemMark;
        int n = 0;
        void L(double x0, double y0, double x1, double y1, double w) =>
            HandLine(d, k, x0, y0, x1, y1, mark, Emblem(k, w), typeKey, HallEmblemSalt, n++);
        switch (emblem)
        {
            case InstitutionEmblem.Blades: L(x - e, cy - e, x + e, cy + e, 1.2); L(x - e, cy + e, x + e, cy - e, 1.2); break;
            case InstitutionEmblem.Cross: d.Rect(new RectD(x - e * 0.3, cy - e, e * 0.6, e * 2), mark); d.Rect(new RectD(x - e, cy - e * 0.3, e * 2, e * 0.6), mark); break;
            case InstitutionEmblem.Gear: d.Circle(x, cy, e * 0.7, null, mark, Emblem(k, 1.2)); break;
            case InstitutionEmblem.Orbit: d.Circle(x, cy, e * 0.3, mark, null); d.Circle(x, cy, e * 0.9, null, mark, Emblem(k, 1.0)); break;
            case InstitutionEmblem.Sheaf: L(x, cy + e, x, cy - e, 1.1); L(x, cy - e * 0.2, x - e * 0.7, cy - e * 0.9, 1.0); L(x, cy - e * 0.2, x + e * 0.7, cy - e * 0.9, 1.0); break;
            case InstitutionEmblem.Scales:
                L(x, cy + e, x, cy - e, 1.0);
                L(x - e, cy - e * 0.6, x + e, cy - e * 0.6, 1.0);
                d.Polygon([(x - e * 1.1, cy + e * 0.1), (x - e * 0.5, cy + e * 0.1), (x - e * 0.8, cy - e * 0.5)], mark);
                d.Polygon([(x + e * 0.5, cy + e * 0.1), (x + e * 1.1, cy + e * 0.1), (x + e * 0.8, cy - e * 0.5)], mark);
                break;
            default: d.Circle(x, cy, e * 0.5, mark, null); break;
        }
    }

    /// <summary>
    /// M6 E1.2 — A PART UNDER CONSTRUCTION on its kind's lot (P12b; the state is a <c>ConstructionQueueRow</c>, or a founding
    /// project built and not yet founded): a half-raised wall under the one light inside two poles and a cross-brace — never
    /// the finished part (only what is built is a part). When the kind already stands there (<paramref name="beside"/>),
    /// only the poles and the brace are drawn, at the lot's north-west corner, so the built part is not hidden.
    /// </summary>
    public static void ScaffoldGlyph(DrawList d, double x, double y, double g, bool beside, MapInk? ink = null)
    {
        MapInk k = ink ?? MapInk.Default;
        double w = Stroke(k, 1.0);
        if (beside)
        {
            double sx = x - 0.62 * g, sy = y - 0.62 * g, s = 0.34 * g;
            d.Line(sx, sy + s, sx, sy - s * 0.2, k.Scaffold, w);
            d.Line(sx + s * 0.8, sy + s, sx + s * 0.8, sy - s * 0.2, k.Scaffold, w);
            d.Line(sx, sy + s * 0.4, sx + s * 0.8, sy, k.Scaffold, w);
            return;
        }
        double foot = y + 0.5 * g, half = 0.42 * g;
        ObjectPaint.Solid(d, k, ObjectPaint.Box(x - half, foot - 0.28 * g, x + half, foot), MapInk.Lit(k.Scaffold));
        d.Line(x - half, foot, x - half, y - 0.6 * g, k.Scaffold, w);
        d.Line(x + half, foot, x + half, y - 0.6 * g, k.Scaffold, w);
        d.Line(x - half, y - 0.1 * g, x + half, y - 0.6 * g, k.Scaffold, w);
        d.Line(x - half, y - 0.6 * g, x + half, y - 0.6 * g, k.Scaffold, w);
    }

    /// <summary>M6 E1.2 (D-066 A4) — THE OVERFLOW PART: three small blocks stacked under the one light, standing for the kinds
    /// folded out of the composite (its number, at high zoom, is drawn by the lens).</summary>
    public static void OverflowGlyph(DrawList d, double x, double y, double g, ObjectRegister register, MapInk? ink = null)
    {
        MapInk k = ink ?? MapInk.Default;
        Rgba wall = k.StructureOther, roof = k.RoofOf((int)register);
        ObjectPaint.Building(d, k, x - 0.28 * g, y + 0.10 * g, 0.36 * g, 0.24 * g, 0.12 * g, 0.08 * g, RoofForm.Flat, wall, roof);
        ObjectPaint.Building(d, k, x + 0.24 * g, y + 0.12 * g, 0.36 * g, 0.24 * g, 0.12 * g, 0.08 * g, RoofForm.Flat, wall, roof);
        ObjectPaint.Building(d, k, x - 0.02 * g, y + 0.58 * g, 0.44 * g, 0.28 * g, 0.14 * g, 0.10 * g, RoofForm.Flat, wall, roof);
    }

    /// <summary>
    /// M6 E1.2 — ONE DWELLING BLOCK (one per <see cref="CompositionLattice.DwellingsPerBlock"/> dwellings, on its fixed lot) in
    /// the settlement's style register (E1.4): a cottage under the one light — its roof the register's form and ink, its wall
    /// the register's — inside the block's rect (<paramref name="bw"/> × <paramref name="bh"/> about (bx, by)). Below
    /// <see cref="DwellingShadedPx"/> wide the block is drawn FLAT, exactly as P12a drew it (one rect, the dwelling ink).
    /// </summary>
    public static void DwellingBlock(DrawList d, double bx, double by, double bw, double bh, ObjectRegister register, MapInk? ink = null)
    {
        MapInk k = ink ?? MapInk.Default;
        if (bw < DwellingShadedPx)
        {
            d.Rect(new RectD(bx - bw / 2, by - bh / 2, bw, bh), A(k.Dwelling, 0.85));
            return;
        }
        int rung = (int)register;
        Rgba wall = k.WallOf(rung), roof = k.RoofOf(rung);
        double w = bw * 0.80, depth = bw * 0.18, x0 = bx - bw / 2, x1 = x0 + w, foot = by + bh / 2;
        double top = foot - bh * 0.46, apex = by - bh / 2 + depth * 0.5, cx = (x0 + x1) / 2;
        var pts = new List<(double X, double Y)> { (x0, foot), (x1, foot), (x1 + depth, foot - depth * 0.5), (x1 + depth, top - depth * 0.5), (x0, top) };
        RoofForm form = StyleRegister.RoofOf(register);
        (double X, double Y)[] roofPoly = form switch
        {
            RoofForm.Gable => [(x0, top), (cx, apex), (x1, top)],
            RoofForm.Dome => [(x0, top), (x0 + w * 0.2, apex + (top - apex) * 0.3), (cx, apex), (x1 - w * 0.2, apex + (top - apex) * 0.3), (x1, top)],
            _ => [(x0, top), (x1, top), (x1 + depth, top - depth * 0.5), (x0 + depth, top - depth * 0.5)],
        };
        pts.AddRange(roofPoly);
        ObjectPaint.Solid(d, k, ObjectPaint.Hull(pts), MapInk.Shade(wall));
        d.Polygon(ObjectPaint.Box(x0, top, x1, foot), wall);
        d.Polygon(roofPoly, MapInk.Lit(roof));
    }

    /// <summary>The block width (screen px) from which a dwelling block is drawn as a lit cottage rather than flat.</summary>
    public const double DwellingShadedPx = 6.0;
}
