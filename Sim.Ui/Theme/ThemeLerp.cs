using Sim.Ui.Render;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Theme;

/// <summary>
/// THE TOKEN CROSS-FADE — an interpolated theme between two eras, for the short presentation-only
/// fade when the Age changes. Colours mix, measures interpolate, and discrete tokens (material,
/// corner cut, motif, face, case, progress style, era identity) switch at the midpoint. t = 0 is
/// <c>from</c> exactly and t = 1 is <c>to</c> exactly (canonical text equal), so the headless
/// renderer, which renders the end state, and the game after the fade agree byte for byte.
/// </summary>
public static class ThemeLerp
{
    public static EraTheme Between(EraTheme from, EraTheme to, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        if (t <= 0.0) return from;
        if (t >= 1.0) return to;
        bool late = t >= 0.5;
        T Pick<T>(T a, T b) => late ? b : a;

        MaterialTokens ma = from.Material, mb = to.Material;
        var material = new MaterialTokens(Pick(ma.Kind, mb.Kind),
            C(ma.Field, mb.Field, t), C(ma.FieldAlt, mb.FieldAlt, t), C(ma.Panel, mb.Panel, t), C(ma.PanelRaised, mb.PanelRaised, t),
            C(ma.PanelSunken, mb.PanelSunken, t), C(ma.Chrome, mb.Chrome, t), C(ma.Border, mb.Border, t), C(ma.BorderStrong, mb.BorderStrong, t),
            C(ma.Hairline, mb.Hairline, t), C(ma.Accent, mb.Accent, t), C(ma.AccentSoft, mb.AccentSoft, t),
            C(ma.Grain, mb.Grain, t), D(ma.GrainDensity, mb.GrainDensity, t), D(ma.GrainAlpha, mb.GrainAlpha, t), D(ma.GrainSize, mb.GrainSize, t));
        InkTokens ia = from.Ink, ib = to.Ink;
        var ink = new InkTokens(C(ia.Text, ib.Text, t), C(ia.TextSoft, ib.TextSoft, t), C(ia.TextDim, ib.TextDim, t),
            C(ia.OnAccent, ib.OnAccent, t), C(ia.Rule, ib.Rule, t));
        SemanticTokens sa = from.Semantic, sb = to.Semantic;
        LaneHues la = sa.Lanes, lb = sb.Lanes;
        var semantic = new SemanticTokens(
            C(sa.Available, sb.Available, t), C(sa.Active, sb.Active, t), C(sa.Completed, sb.Completed, t), C(sa.Locked, sb.Locked, t),
            C(sa.Progress, sb.Progress, t), C(sa.Danger, sb.Danger, t), C(sa.Positive, sb.Positive, t), C(sa.Food, sb.Food, t),
            C(sa.Knowledge, sb.Knowledge, t), C(sa.Military, sb.Military, t), C(sa.Infrastructure, sb.Infrastructure, t),
            C(sa.Prerequisite, sb.Prerequisite, t), C(sa.Dependent, sb.Dependent, t),
            C(sa.AvailableFill, sb.AvailableFill, t), C(sa.ActiveFill, sb.ActiveFill, t), C(sa.CompletedFill, sb.CompletedFill, t),
            C(sa.LockedFill, sb.LockedFill, t),
            new LaneHues(C(la.Main, lb.Main, t), C(la.Military, lb.Military, t), C(la.Medicine, lb.Medicine, t),
                C(la.Engineering, lb.Engineering, t), C(la.NaturalScience, lb.NaturalScience, t), C(la.Agriculture, lb.Agriculture, t),
                C(la.External, lb.External, t), C(la.Civics, lb.Civics, t)));
        EdgeTokens ea = from.Edge, eb = to.Edge;
        var edge = new EdgeTokens(D(ea.Roughness, eb.Roughness, t), D(ea.JitterPx, eb.JitterPx, t), Pick(ea.Corner, eb.Corner),
            D(ea.CornerPx, eb.CornerPx, t), D(ea.BorderPx, eb.BorderPx, t), Pick(ea.DoubleRule, eb.DoubleRule), Pick(ea.Fasteners, eb.Fasteners));
        var ornament = new OrnamentTokens(I(from.Ornament.Level, to.Ornament.Level, t), Pick(from.Ornament.Motif, to.Ornament.Motif));
        TypographyTokens ta = from.Type, tb = to.Type;
        var type = new TypographyTokens(S(ta.Body, tb.Body, t), S(ta.Heading, tb.Heading, t), S(ta.Title, tb.Title, t),
            S(ta.Numeric, tb.Numeric, t), S(ta.Caps, tb.Caps, t), D(ta.SizeScale, tb.SizeScale, t), D(ta.LineHeight, tb.LineHeight, t));
        DensityTokens da = from.Density, db = to.Density;
        var density = new DensityTokens(I(da.Level, db.Level, t), D(da.Padding, db.Padding, t), D(da.Gap, db.Gap, t), Pick(da.CardDetail, db.CardDetail));
        ControlTokens ca = from.Controls, cb = to.Controls;
        var controls = new ControlTokens(Pick(ca.Granularity, cb.Granularity), Pick(ca.Progress, cb.Progress), Pick(ca.ProgressSegments, cb.ProgressSegments),
            D(ca.StrokePx, cb.StrokePx, t), D(ca.GrabPx, cb.GrabPx, t));
        IconTokens xa = from.Icons, xb = to.Icons;
        var icons = new IconTokens(Pick(xa.Style, xb.Style), D(xa.StrokePx, xb.StrokePx, t), D(xa.WobblePx, xb.WobblePx, t), Pick(xa.Filled, xb.Filled));
        ChartTokens ha = from.Charts, hb = to.Charts;
        var charts = new ChartTokens(I(ha.Sophistication, hb.Sophistication, t), Pick(ha.GridLines, hb.GridLines), Pick(ha.Ticks, hb.Ticks),
            Pick(ha.Labels, hb.Labels), D(ha.LineWidth, hb.LineWidth, t));
        // Map identity inks are equal in every era; the neutral ones mix.
        MapInkTokens pa = from.Map, pb = to.Map;
        MapInkTokens map = pb with
        {
            Ink = C(pa.Ink, pb.Ink, t), InkDark = C(pa.InkDark, pb.InkDark, t),
            LegendPaper = C(pa.LegendPaper, pb.LegendPaper, t), LegendInk = C(pa.LegendInk, pb.LegendInk, t),
        };
        return new EraTheme(Pick(from.Era, to.Era), Pick(from.Name, to.Name), Pick(from.Medium, to.Medium),
            material, ink, semantic, edge, ornament, type, density, controls, icons, charts, map);
    }

    private static Rgba C(Rgba a, Rgba b, double t) => ThemeColor.Mix(a, b, t);
    private static double D(double a, double b, double t) => a + (b - a) * t;
    private static int I(int a, int b, double t) => (int)Math.Round(a + (b - a) * t, MidpointRounding.AwayFromZero);

    private static TextStyle S(TextStyle a, TextStyle b, double t) => new(
        t >= 0.5 ? b.Face : a.Face,
        (int)Math.Round(a.Weight + (b.Weight - a.Weight) * t, MidpointRounding.AwayFromZero),
        a.TrackingEm + (b.TrackingEm - a.TrackingEm) * t,
        t >= 0.5 ? b.Case : a.Case);
}

/// <summary>
/// THE AGE-TRANSITION FADE — UI time only: it holds the era the interface showed and the era the
/// simulation now says, and advances with frame time (wall-clock is legal in Sim.Ui, ADR-009). It
/// never reads or writes simulation state. Headless renderers do not advance it: they render
/// <see cref="Target"/>, the end state.
/// </summary>
public sealed class EraTransition
{
    /// <summary>The fade's length in seconds: short and restrained, no cinematic.</summary>
    public const double Seconds = 1.6;

    public EraTheme From { get; }
    public EraTheme Target { get; }
    public double Elapsed { get; private set; }

    public EraTransition(EraTheme from, EraTheme target)
    {
        From = from;
        Target = target;
    }

    public bool Done => Elapsed >= Seconds;

    /// <summary>The fade's progress in [0,1], eased (smoothstep).</summary>
    public double Progress
    {
        get
        {
            double x = Math.Clamp(Elapsed / Seconds, 0.0, 1.0);
            return x * x * (3.0 - 2.0 * x);
        }
    }

    /// <summary>The theme to paint this frame.</summary>
    public EraTheme Current => Done ? Target : ThemeLerp.Between(From, Target, Progress);

    public void Advance(double seconds) { if (seconds > 0) Elapsed = Math.Min(Seconds, Elapsed + seconds); }
}
