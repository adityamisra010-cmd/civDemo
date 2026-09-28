using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Trees.View;

/// <summary>
/// THE GALLERY — placeholder buildings, institutions and units drawn by the same glyph
/// grammar (task §14–§17), and samples of the presentation animations. Every card shows
/// only what the gallery state source REPORTS: no effect, cost, statistic, formula or
/// multiplier exists here, and the cards say so.
/// </summary>
internal static class GalleryPainter
{
    private static readonly Rgba InkP = ParchmentPalette.InkPrimary;
    private static readonly Rgba InkS = ParchmentPalette.InkSoft;
    private static readonly Rgba Gold = ParchmentPalette.GoldLeaf;

    public static void Paint(DrawList dl, List<Hit> hits, TreesFrameInput input, TreesScreenLayout lay)
    {
        GalleryDocument gal = input.Content.Gallery;
        GallerySnapshot st = input.Gallery;
        ITextMeasure m = input.Measure;
        double W = lay.Screen.W, H = lay.Screen.H;
        double x0 = 16, w = W - 32;
        double y = lay.Header.Bottom + 10;

        // Buildings and institutions.
        y = Chrome.SectionTitle(dl, x0, y, w, "Buildings & institutions");
        double tagX = x0 + m.Width("BUILDINGS & INSTITUTIONS", Chrome.Caps, FontRole.Caps) + 12;
        tagX += Chrome.Tag(dl, m, tagX, y - 22, "PLACEHOLDER — NOT GAMEPLAY OBJECTS", ParchmentPalette.IronRed) + 12;
        foreach (MaturityStageDef ms in gal.MaturityStages)
        {
            dl.Glyph(tagX, y - 24, 20, new GlyphSpec(GlyphBase.Hall, GlyphState.Complete, SizeClass.Px24, Stage: ms.Stage,
                Maturity: ms.Stage / (double)GlyphSpec.MaxStage));
            dl.Text(tagX + 22, y - 21, ms.Id, 10.5, InkS, TextAlign.Left, FontRole.Caps);
            tagX += 34 + m.Width(ms.Id, 10.5, FontRole.Caps);
        }
        int bcols = 5;
        double bw = (w - (bcols - 1) * 8) / bcols, bh = 104;
        for (int i = 0; i < gal.Buildings.Count; i++)
        {
            var card = new RectD(x0 + i % bcols * (bw + 8), y + i / bcols * (bh + 8), bw, bh);
            BuildingCard(dl, input, gal, st, gal.Buildings[i], card);
        }
        y += (gal.Buildings.Count + bcols - 1) / bcols * (bh + 8) + 6;

        // Units.
        y = Chrome.SectionTitle(dl, x0, y, w, "Units");
        Chrome.Tag(dl, m, x0 + m.Width("UNITS", Chrome.Caps, FontRole.Caps) + 12, y - 22,
            "PLACEHOLDER — NO COMBAT STATISTICS · VETERANCY HAS NO FORMULA OR MULTIPLIER", ParchmentPalette.IronRed);
        int ucols = 7;
        double uw = (w - (ucols - 1) * 8) / ucols, uh = 104;
        for (int i = 0; i < gal.Units.Count; i++)
        {
            var card = new RectD(x0 + i % ucols * (uw + 8), y + i / ucols * (uh + 8), uw, uh);
            UnitCard(dl, input, gal, st, gal.Units[i], card);
        }
        y += (gal.Units.Count + ucols - 1) / ucols * (uh + 8) + 6;

        // Animation samples.
        if (y < H - 60) Samples(dl, input, new RectD(x0, y, w, H - y - 8));
    }

    private static void BuildingCard(DrawList dl, TreesFrameInput input, GalleryDocument gal, GallerySnapshot st, BuildingDef def, RectD r)
    {
        ITextMeasure m = input.Measure;
        dl.Rect(r, ParchmentPalette.PaperLight, Ink.With(InkS, 0.6), 1, 4);
        BuildingStatus? b = st.Building(def.Id);
        StatusGlyphDef? status = b is null ? null : gal.OperationalStatus(b.OperationalStatus);
        MaturityStageDef? stage = gal.Maturity(b?.MaturityStage);
        BuildingMotion motion = input.Animator.Building(def.Id, input.Now);
        GlyphState gs = status?.GlyphState ?? GlyphState.Locked;
        var spec = new GlyphSpec(def.Base, gs, SizeClass.Px48, Domain: def.Mark, Era: def.Era,
            Maturity: NodeVisuals.Quantise(b?.MaturityProgress ?? 0), Progress: NodeVisuals.Quantise(b?.ConstructionProgress ?? 0),
            Stage: stage?.Stage ?? 0);
        dl.Glyph(r.X + 6, r.Y + 8, 52, spec);
        if (motion.PipFillT >= 0) dl.Circle(r.X + 32, r.Y + 34, 26 + 10 * motion.PipFillT, null, Ink.With(Gold, 1 - motion.PipFillT), 2.5);
        double tx = r.X + 64, tw = r.Right - tx - 6;
        dl.Text(tx, r.Y + 6, Ink.Fit(m, def.Name, 14, tw), 14, InkP);
        dl.Text(tx, r.Y + 24, stage is null ? "—" : $"{stage.Id} · stage {stage.Stage}/{GlyphSpec.MaxStage}", 10.5, stage is null ? InkS : Gold, TextAlign.Left, FontRole.Caps);
        double y = r.Y + 40;
        if (b is not null && b.ConstructionProgress < 1.0)
        {
            var bar = new RectD(tx, y + 3, tw - 34, 5);
            dl.Bar(bar, b.ConstructionProgress, Gold, Ink.With(InkS, 0.6), ParchmentPalette.PaperMid);
            if (motion.SweepT >= 0)
            {
                dl.PushClip(bar);
                dl.Rect(new RectD(bar.X + bar.W * motion.SweepT - 10, bar.Y, 20, bar.H), Ink.With(ParchmentPalette.PaperLight, 0.7));
                dl.PopClip();
            }
            dl.Text(r.Right - 6, y, $"{b.ConstructionProgress * 100:0} %", 10.5, InkS, TextAlign.Right, FontRole.Numeric);
            y += 13;
        }
        if (b?.PersonnelCapacity is long cap && cap > 0)
        {
            long cur = b.PersonnelCurrent ?? 0;
            string label = $"personnel {cur}/{cap}";
            dl.Text(tx, y, label, 11, InkS, TextAlign.Left, FontRole.Numeric);
            double lw = m.Width(label, 11, FontRole.Numeric) + 6;
            if (tw - lw > 16) dl.Bar(new RectD(tx + lw, y + 4, tw - lw, 4), cur / (double)cap, ParchmentPalette.Verdigris, Ink.With(InkS, 0.5));
            y += 14;
        }
        double limit = r.Bottom - 30;
        if (b?.Specialization is string spec2 && y <= limit) { dl.Text(tx, y, Ink.Fit(m, "spec. " + spec2, 11, tw), 11, InkS); y += 13; }
        if (b?.AgeBuilt is string ab && input.Content.Ages.TryAge(ab, out AgeDef age) && y <= limit) { dl.Text(tx, y, "built " + age.DisplayName, 11, InkS); y += 13; }
        string sname = status?.Name ?? "no state reported";
        dl.Text(r.X + 8, r.Bottom - 17, sname.ToUpperInvariant(), 10, gs == GlyphState.Decayed ? ParchmentPalette.IronRed : InkS, TextAlign.Left, FontRole.Caps);
    }

    private static void UnitCard(DrawList dl, TreesFrameInput input, GalleryDocument gal, GallerySnapshot st, UnitDef def, RectD r)
    {
        ITextMeasure m = input.Measure;
        dl.Rect(r, ParchmentPalette.PaperLight, Ink.With(InkS, 0.6), 1, 4);
        UnitStatus? u = st.Unit(def.Id);
        StatusGlyphDef? state = u is null ? null : gal.UnitState(u.State);
        VeterancyLevelDef? vet = u is null ? null : gal.Veterancy(u.VeterancyLevel);
        UnitMotion motion = input.Animator.Unit(def.Id, input.Now);
        GlyphState gs = state?.GlyphState ?? GlyphState.Locked;
        var spec = new GlyphSpec(def.Base, gs, SizeClass.Px48, Domain: def.Mark, Era: def.Era,
            Progress: NodeVisuals.Quantise(u?.Strength ?? 0), Maturity: gs == GlyphState.InProgress ? NodeVisuals.Quantise(u?.Strength ?? 0) : 1.0,
            Veterancy: (Veterancy)System.Math.Clamp(vet?.Chevrons ?? 0, 0, 3), Strength: u?.Strength ?? 1.0);
        double gx = r.X + 6, gy = r.Y + 6;
        if (motion.PulseAlpha > 0) dl.Circle(gx + 24, gy + 24, 27, Ink.With(Gold, motion.PulseAlpha * 0.3), Ink.With(Gold, motion.PulseAlpha), 2);
        dl.Glyph(gx, gy, 48, spec);
        if (motion.FlashT >= 0) dl.Circle(gx + 24, gy + 24, 24 + 14 * motion.FlashT, null, Ink.With(Gold, 1 - motion.FlashT), 2.5);
        double tx = r.X + 58, tw = r.Right - tx - 5;
        dl.Text(tx, r.Y + 6, Ink.Fit(m, def.Name, 13.5, tw), 13.5, InkP);
        dl.Text(tx, r.Y + 23, Ink.Fit(m, (state?.Name ?? "no state").ToUpperInvariant(), 10, tw), 10, Gold, TextAlign.Left, FontRole.Caps);
        dl.Text(tx, r.Y + 37, Ink.Fit(m, vet?.Name ?? "—", 11, tw), 11, InkS);
        double y = r.Y + 58;
        if (u is not null)
        {
            void Row(string label, double f, Rgba c)
            {
                dl.Text(r.X + 8, y, label, 10.5, InkS);
                dl.Bar(new RectD(r.X + 62, y + 4, r.W - 70, 4), f, c, Ink.With(InkS, 0.5));
                y += 13;
            }
            Row("strength", u.Strength, InkP);
            Row($"xp {u.Experience?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—"}", u.ExperienceProgress, Gold);
            Row("cohesion", u.Cohesion, ParchmentPalette.Verdigris);
            string doctrine = u.Doctrine is string d && input.Graph.TryIndexOf(d, out int di) ? input.Graph.Node(di).Name : "—";
            dl.Text(r.X + 8, y, Ink.Fit(m, $"{u.Training} · {u.Recovery} · {doctrine}", 10, r.W - 14), 10, InkS);
        }
    }

    /// <summary>Presentation samples: the knowledge-diffusion pathway and the building / unit
    /// state sequences. Diffusion is NOT implemented; the stage is what the source reports.</summary>
    private static void Samples(DrawList dl, TreesFrameInput input, RectD r)
    {
        ITextMeasure m = input.Measure;
        double y = Chrome.SectionTitle(dl, r.X, r.Y, r.W, "Animation samples · presentation only");
        string[] stations = ["source", "contact channel", "exposure", "absorption", "local development"];
        int reached = input.Gallery.DiffusionStage ?? -1;
        double sx = r.X + 150, span = System.Math.Min(520, r.W * 0.45);
        dl.Text(r.X, y + 6, "Knowledge diffusion", 12.5, InkP);
        double step = span / (stations.Length - 1);
        dl.Line(sx, y + 14, sx + span, y + 14, Ink.With(InkS, 0.5), 1.5, (3, 3));
        for (int i = 0; i < stations.Length; i++)
        {
            double px = sx + i * step;
            GlyphState gs = i < reached ? GlyphState.Complete : i == reached ? GlyphState.InProgress : GlyphState.Locked;
            dl.Glyph(px - 11, y + 3, 22, new GlyphSpec(GlyphBase.Node, gs, SizeClass.Px24, Maturity: i < reached ? 1 : 0.5, Progress: 0.5));
            dl.Text(px, y + 27, stations[i], 10.5, i <= reached ? InkP : InkS, TextAlign.Center);
        }
        if (reached > 0)
        {
            double[] dots = input.Animator.EdgeDots("diffusesTo", 0, input.Now);
            foreach (double t in dots) dl.Circle(sx + t * step * reached, y + 14, 3.2, ParchmentPalette.River);
        }
        double qx = sx + span + 40;
        dl.Text(qx, y, "Building: planned → construction → partial → completed → maturing → mature", 11.5, InkS);
        dl.Text(qx, y + 17, "Unit: recruiting → training → ready → deployed → experienced", 11.5, InkS);
        dl.Text(qx, y + 34, "Age: current → transition indication → new Age → settled (see the Age view)", 11.5, InkS);
    }
}
