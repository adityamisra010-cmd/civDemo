namespace Sim.Ui.Art.Glyphs;

/// <summary>
/// THE CONTACT SHEET — every base × every state, plus one row per axis, baked
/// and composited over parchment so the Director can read the grammar without
/// building it (docs/architecture/glyph-sheet-v0.png; regenerate with
/// <c>sim-ui --glyph-sheet [path]</c>). Row order, top to bottom:
///
///   rows  0–9 : the ten bases (Hall, Tower, Ring, Dome, Portico, Formation, Node,
///               Lozenge, Heap, Link), each across the six states
///               (Locked, Available, InProgress ½, Complete, Stalled ½, Decayed) at 32 px;
///   row    10 : the size ladder — Hall · Medicine · Industrial · maturity 0.6 · Complete
///               at 16, 24, 32, 48 px (the LOD contract, §2.7);
///   row    11 : the five registers on Hall at 32 px (Primitive … Modern);
///   row    12 : maturity on Portico at 48 px — 0.15, 0.4, 0.65, 0.9;
///   row    13 : veterancy on Formation at 48 px — Recruit, Regular, Veteran, Elite;
///   row    14 : the eight domain marks on Node at 32 px;
///   row    15 : Panel vs Map placement (contact shadow) — Hall Complete at 32 and 48 px;
///   row    16 : Link stroke vocabulary — None, Commerce (road), Craft (bridge),
///               Agriculture (canal), Maritime (sea lane) at 32 px.
///
/// The sheet is a PRESENTATION artifact: compositing ink over paper blends RGB,
/// so the palette-exactness test runs on the individual glyph bakes (the assets),
/// never on this image. The sheet is pinned for determinism only.
/// </summary>
public static class GlyphSheet
{
    public const int Cell = 56;
    public const int Columns = 8;
    public const int Rows = 17;
    public const int Gutter = 8;

    public static int Width => Columns * Cell + Gutter;
    public static int Height => Rows * Cell + Gutter;

    public static ArtImage Bake()
    {
        var img = new ArtImage(Width, Height, new byte[Width * Height * 4]);
        ParchmentPalette.Rgba paper = ParchmentPalette.PaperMid;
        for (int i = 0; i < Width * Height; i++)
        {
            int o = i * 4;
            img.Rgba[o] = paper.R; img.Rgba[o + 1] = paper.G; img.Rgba[o + 2] = paper.B; img.Rgba[o + 3] = 255;
        }

        // A hairline under every row group so the eye can count rows.
        for (int r = 1; r < Rows; r++)
        {
            int y = Gutter / 2 + r * Cell;
            for (int x = Gutter / 2; x < Width - Gutter / 2; x++) Blend(img, x, y, ParchmentPalette.InkSoft, 0.25);
        }

        GlyphState[] states =
        [
            GlyphState.Locked, GlyphState.Available, GlyphState.InProgress,
            GlyphState.Complete, GlyphState.Stalled, GlyphState.Decayed,
        ];
        int row = 0;
        for (int b = 0; b < 10; b++, row++)
            for (int c = 0; c < states.Length; c++)
                Place(img, row, c, GlyphBaker.Bake(GlyphSpec.Exemplar((GlyphBase)b, states[c], SizeClass.Px32)));

        // Row 10: the size ladder.
        SizeClass[] ladder = [SizeClass.Px16, SizeClass.Px24, SizeClass.Px32, SizeClass.Px48];
        for (int c = 0; c < ladder.Length; c++)
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, ladder[c],
                Domain: GlyphDomain.Medicine, Era: EraRegister.Industrial, Maturity: 0.6)));
        row++;

        // Row 11: the registers.
        for (int c = 0; c < 5; c++)
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px32, Era: (EraRegister)c)));
        row++;

        // Row 12: maturity.
        double[] maturities = [0.15, 0.4, 0.65, 0.9];
        for (int c = 0; c < maturities.Length; c++)
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Portico, GlyphState.Complete, SizeClass.Px48,
                Domain: GlyphDomain.Letters, Era: EraRegister.Classical, Maturity: maturities[c])));
        row++;

        // Row 13: veterancy.
        for (int c = 0; c < 4; c++)
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Formation, GlyphState.Complete, SizeClass.Px48,
                Domain: GlyphDomain.Military, Veterancy: (Veterancy)c, Strength: 1.0 - 0.2 * c)));
        row++;

        // Row 14: domain marks.
        for (int c = 0; c < 8; c++)
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Node, GlyphState.Complete, SizeClass.Px32, Domain: (GlyphDomain)(c + 1))));
        row++;

        // Row 15: placement.
        Place(img, row, 0, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px32, Placement: Placement.Panel)));
        Place(img, row, 1, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px32, Placement: Placement.Map)));
        Place(img, row, 2, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px48, Placement: Placement.Panel)));
        Place(img, row, 3, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px48, Placement: Placement.Map)));
        row++;

        // Row 16: link vocabulary.
        GlyphDomain[] links = [GlyphDomain.None, GlyphDomain.Commerce, GlyphDomain.Craft, GlyphDomain.Agriculture, GlyphDomain.Maritime];
        for (int c = 0; c < links.Length; c++)
            Place(img, row, c, GlyphBaker.Bake(new GlyphSpec(GlyphBase.Link, GlyphState.Complete, SizeClass.Px32, Domain: links[c])));

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
