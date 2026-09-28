namespace Sim.Ui.Art.Glyphs;

/// <summary>
/// THE CONTACT SHEET — every base × every state, plus one row per axis, baked
/// and composited over parchment so the Director can read the grammar without
/// building it (docs/architecture/glyph-sheet-v1.png; regenerate with
/// <c>sim-ui --glyph-sheet [path]</c>). Row order, top to bottom:
///
///   rows  0–17 : the eighteen bases, one per row. Columns 0–6: the seven states
///                (Locked, Available, InProgress ½, Complete, Stalled ½, Decayed,
///                Discovered) at 32 px. Columns 8–11: the same base, Complete, at
///                16 / 24 / 32 / 48 px — the LOD ladder per base (§2.7);
///   row     18 : the five registers on Hall (cols 0–4) and on Tower (cols 6–10) at 32 px;
///   row     19 : maturity wash on Portico at 48 px — 0.15, 0.4, 0.65, 0.9 (cols 0–3);
///                maturity STAGE pips on Portico at 48 px — 0 … 4 (cols 5–9);
///   row     20 : veterancy on Formation (cols 0–3) and on a Standard carrying a spear
///                (cols 5–8) at 48 px — Recruit, Regular, Veteran, Elite;
///   rows 21–23 : every domain / object mark on the Emblem base at 48 px, in enum order;
///   row     24 : compositions — a base carrying a mark, as the Trees content uses them;
///   row     25 : Panel vs Map placement (cols 0–3) and the Link stroke vocabulary (cols 5–9).
///
/// The sheet is a PRESENTATION artifact: compositing ink over paper blends RGB,
/// so the palette-exactness test runs on the individual glyph bakes (the assets),
/// never on this image. The sheet is pinned for determinism only.
/// </summary>
public static class GlyphSheet
{
    public const int Cell = 56;
    public const int Columns = 12;
    public const int Rows = 26;
    public const int Gutter = 8;

    public static int Width => Columns * Cell + Gutter;
    public static int Height => Rows * Cell + Gutter;

    /// <summary>The seven states in sheet order (the six of v0, then Discovered).</summary>
    public static readonly GlyphState[] StateOrder =
    [
        GlyphState.Locked, GlyphState.Available, GlyphState.InProgress,
        GlyphState.Complete, GlyphState.Stalled, GlyphState.Decayed,
        GlyphState.Discovered,
    ];

    /// <summary>Row 24: bases carrying marks. Illustrative pairings only — the binding
    /// of a node type to a base and a mark is CONTENT (ui-content/trees), not grammar.</summary>
    private static readonly (GlyphBase Base, GlyphDomain Mark)[] Compositions =
    [
        (GlyphBase.Node, GlyphDomain.Knowledge),
        (GlyphBase.Hexagon, GlyphDomain.Anvil),
        (GlyphBase.Portico, GlyphDomain.Dividers),
        (GlyphBase.Link, GlyphDomain.None),
        (GlyphBase.Tower, GlyphDomain.Industry),
        (GlyphBase.Shield, GlyphDomain.Military),
        (GlyphBase.Standard, GlyphDomain.Cannon),
        (GlyphBase.Scroll, GlyphDomain.Civic),
        (GlyphBase.Heap, GlyphDomain.Applications),
        (GlyphBase.Lozenge, GlyphDomain.Links),
        (GlyphBase.Star, GlyphDomain.Hourglass),
        (GlyphBase.Monument, GlyphDomain.Laurel),
    ];

    public static ArtImage Bake()
    {
        var img = new ArtImage(Width, Height, new byte[Width * Height * 4]);
        ParchmentPalette.Rgba paper = ParchmentPalette.PaperMid;
        for (int i = 0; i < Width * Height; i++)
        {
            int o = i * 4;
            img.Rgba[o] = paper.R; img.Rgba[o + 1] = paper.G; img.Rgba[o + 2] = paper.B; img.Rgba[o + 3] = 255;
        }

        // A hairline under every row so the eye can count rows.
        for (int r = 1; r < Rows; r++)
        {
            int y = Gutter / 2 + r * Cell;
            for (int x = Gutter / 2; x < Width - Gutter / 2; x++) Blend(img, x, y, ParchmentPalette.InkSoft, 0.25);
        }

        // Rows 0–17: bases × states, then the per-base LOD ladder.
        GlyphBase[] bases = Enum.GetValues<GlyphBase>();
        SizeClass[] ladder = [SizeClass.Px16, SizeClass.Px24, SizeClass.Px32, SizeClass.Px48];
        int row = 0;
        foreach (GlyphBase b in bases)
        {
            for (int c = 0; c < StateOrder.Length; c++)
                Place(img, row, c, GlyphBaker.Bake(GlyphSpec.Exemplar(b, StateOrder[c], SizeClass.Px32)));
            for (int c = 0; c < ladder.Length; c++)
                Place(img, row, 8 + c, GlyphBaker.Bake(new GlyphSpec(b, GlyphState.Complete, ladder[c],
                    Domain: b == GlyphBase.Emblem ? GlyphDomain.Knowledge : GlyphDomain.Medicine,
                    Era: EraRegister.Industrial, Maturity: 0.6)));
            row++;
        }

        // Row 18: the registers.
        for (int c = 0; c < 5; c++)
        {
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px32, Era: (EraRegister)c)));
            Place(img, row, 6 + c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Tower, GlyphState.Complete, SizeClass.Px32, Era: (EraRegister)c)));
        }
        row++;

        // Row 19: maturity wash, then maturity stage pips.
        double[] maturities = [0.15, 0.4, 0.65, 0.9];
        for (int c = 0; c < maturities.Length; c++)
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Portico, GlyphState.Complete, SizeClass.Px48,
                Domain: GlyphDomain.Letters, Era: EraRegister.Classical, Maturity: maturities[c])));
        for (int st = 0; st <= GlyphSpec.MaxStage; st++)
            Place(img, row, 5 + st, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Portico, GlyphState.Complete, SizeClass.Px48,
                Domain: GlyphDomain.Dividers, Maturity: st / (double)GlyphSpec.MaxStage, Stage: st)));
        row++;

        // Row 20: veterancy.
        for (int c = 0; c < 4; c++)
        {
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Formation, GlyphState.Complete, SizeClass.Px48,
                Domain: GlyphDomain.Military, Veterancy: (Veterancy)c, Strength: 1.0 - 0.2 * c)));
            Place(img, row, 5 + c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Standard, GlyphState.Complete, SizeClass.Px48,
                Domain: GlyphDomain.Spear, Veterancy: (Veterancy)c)));
        }
        row++;

        // Rows 21–23: every mark on the Emblem at 48 px.
        GlyphDomain[] marks = Enum.GetValues<GlyphDomain>();
        int k = 0;
        foreach (GlyphDomain d in marks)
        {
            if (d == GlyphDomain.None) continue;
            Place(img, row + k / Columns, k % Columns, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Emblem, GlyphState.Complete, SizeClass.Px48, Domain: d)));
            k++;
        }
        row += (k + Columns - 1) / Columns;

        // Row 24: compositions.
        for (int c = 0; c < Compositions.Length; c++)
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(Compositions[c].Base, GlyphState.Complete, SizeClass.Px48,
                Domain: Compositions[c].Mark, Maturity: 0.5)));
        row++;

        // Row 25: placement, then the link vocabulary.
        Place(img, row, 0, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px32, Placement: Placement.Panel)));
        Place(img, row, 1, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px32, Placement: Placement.Map)));
        Place(img, row, 2, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px48, Placement: Placement.Panel)));
        Place(img, row, 3, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px48, Placement: Placement.Map)));
        GlyphDomain[] links = [GlyphDomain.None, GlyphDomain.Commerce, GlyphDomain.Craft, GlyphDomain.Agriculture, GlyphDomain.Maritime];
        for (int c = 0; c < links.Length; c++)
            Place(img, row, 5 + c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Link, GlyphState.Complete, SizeClass.Px32, Domain: links[c])));

        return img;
    }

    /// <summary>Composite a glyph, centred in its cell, over the paper.</summary>
    private static void Place(ArtImage sheet, int row, int col, ArtImage glyph)
    {
        int x0 = Gutter / 2 + col * Cell + (Cell - glyph.Width) / 2;
        int y0 = Gutter / 2 + row * Cell + (Cell - glyph.Height) / 2;
        for (int y = 0; y < glyph.Height; y++)
        {
            for (int x = 0; x < glyph.Width; x++)
            {
                int o = (y * glyph.Width + x) * 4;
                byte a = glyph.Rgba[o + 3];
                if (a == 0) continue;
                var ink = new ParchmentPalette.Rgba(glyph.Rgba[o], glyph.Rgba[o + 1], glyph.Rgba[o + 2]);
                Blend(sheet, x0 + x, y0 + y, ink, a / 255.0);
            }
        }
    }

    private static void Blend(ArtImage img, int x, int y, ParchmentPalette.Rgba ink, double alpha)
    {
        if (x < 0 || y < 0 || x >= img.Width || y >= img.Height) return;
        int o = (y * img.Width + x) * 4;
        img.Rgba[o] = (byte)System.Math.Round(img.Rgba[o] + (ink.R - img.Rgba[o]) * alpha);
        img.Rgba[o + 1] = (byte)System.Math.Round(img.Rgba[o + 1] + (ink.G - img.Rgba[o + 1]) * alpha);
        img.Rgba[o + 2] = (byte)System.Math.Round(img.Rgba[o + 2] + (ink.B - img.Rgba[o + 2]) * alpha);
    }
}
