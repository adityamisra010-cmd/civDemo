using Xunit;
using Sim.Ui.Art;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// DIRECTOR PLAYTEST RULING 2026-10-29, "panels only" (docs/architecture/era-ui.md §9b): "advancing an Age is visibly
/// different". The map's parchment substrate stays unthemed and the parchment era (A6) stays byte-identical; every
/// other era's panel, field and bar materials are tuned so that each Age step is a plainly different surface (CIE
/// ΔE*ab, <see cref="ThemeColor.DeltaE"/>), and the hue journey runs rough natural materials → well-defined paper →
/// crisp, computerised stock. Thresholds are exact; the readability floors are pinned elsewhere and untouched.
/// </summary>
public class EraSeparationTests
{
    /// <summary>Consecutive eras differ by at least this on the PANEL or on the FIELD (≥ 12 reads as plainly
    /// different surfaces at a glance — the UR-4 state-fill yardstick). Before the ruling A1→A2 was 7.0 / 11.7.</summary>
    public const double SurfaceStep = 12.0;

    /// <summary>Consecutive eras' bars (the dark chrome) differ by at least this. Before: A1→A2 11.4, A2→A3 8.2.</summary>
    public const double ChromeStep = 15.0;

    /// <summary>The parchment era's full canonical hash — unchanged by this ruling (the preview log before it
    /// recorded the same 16-hex prefix, 7e2e3eed8bb95fdb).</summary>
    public const string A6CanonicalHash = "7e2e3eed8bb95fdb3e230f41524fb52a72b265446fb78d6b38eddebd4fc5287b";

    private static EraTheme T(int age) => EraThemes.For((UiEra)age);

    [Fact]
    public void EveryAgeStep_IsAPlainlyDifferentSurface_AndAPlainlyDifferentBar()
    {
        for (int e = 1; e < 9; e++)
        {
            MaterialTokens a = T(e).Material, b = T(e + 1).Material;
            double panel = ThemeColor.DeltaE(a.Panel, b.Panel), field = ThemeColor.DeltaE(a.Field, b.Field);
            double chrome = ThemeColor.DeltaE(a.Chrome, b.Chrome);
            Assert.True(Math.Max(panel, field) >= SurfaceStep, $"A{e}->A{e + 1}: panel dE {panel:0.0}, field dE {field:0.0}");
            Assert.True(chrome >= ChromeStep, $"A{e}->A{e + 1}: chrome dE {chrome:0.0}");
        }
    }

    [Fact]
    public void TheFirstAgeStep_TheOneThePlaytestMissed_ChangesThePanelItself()
    {
        // A1 → A2 is the step every player meets first: the panel alone (the surface every chrome window is painted
        // on) must carry it, not only the field behind the research tree.
        MaterialTokens a1 = T(1).Material, a2 = T(2).Material;
        Assert.True(ThemeColor.DeltaE(a1.Panel, a2.Panel) >= SurfaceStep, $"A1->A2 panel dE {ThemeColor.DeltaE(a1.Panel, a2.Panel):0.0}");
        Assert.True(ThemeColor.DeltaE(a1.Field, a2.Field) >= SurfaceStep, $"A1->A2 field dE {ThemeColor.DeltaE(a1.Field, a2.Field):0.0}");
    }

    [Fact]
    public void TheHueJourney_RunsFromNaturalMaterials_ThroughPaper_ToComputerisedStock()
    {
        static (double L, double A, double B, double C) Lab(Rgba c)
        {
            (double l, double a, double b) = ThemeColor.Lab(c);
            return (l, a, b, Math.Sqrt(a * a + b * b));
        }
        var p = new (double L, double A, double B, double C)[10];
        var c = new (double L, double A, double B, double C)[10];
        for (int e = 1; e <= 9; e++) { p[e] = Lab(T(e).Material.Panel); c[e] = Lab(T(e).Material.Chrome); }

        Assert.True(p[1].C < 5, $"A1 stone is a cool grey (chroma {p[1].C:0.0})");
        Assert.True(p[2].A > 5 && p[2].B > 12, $"A2 is warm red clay (a* {p[2].A:0.0}, b* {p[2].B:0.0})");
        Assert.True(c[2].A > 15, $"A2's bar is red clay (a* {c[2].A:0.0})");
        Assert.True(p[3].B > 20 && p[3].A < p[2].A, $"A3 is golden sandstone (b* {p[3].B:0.0})");
        Assert.True(c[3].A < -5, $"A3's bar is verdigris (a* {c[3].A:0.0})");
        Assert.True(p[4].A < 0 && p[4].C < 8 && c[4].L < 15 && c[4].C < 5, $"A4 is cool lime plaster under near-black iron (bar L* {c[4].L:0.0})");
        Assert.True(p[5].L > 95 && p[5].C < 3, $"A5 is white marble (L* {p[5].L:0.0})");
        Assert.True(c[5].A > 25, $"A5's bar is a deep classical red (a* {c[5].A:0.0})");
        Assert.True(p[7].L > p[6].L && p[7].C < p[6].C, "A7's laid paper is whiter and crisper than A6's vellum");
        Assert.True(c[7].L < 10 && c[7].C < 2, $"A7's bar is printer's black (L* {c[7].L:0.0})");
        Assert.True(p[8].B < -4, $"A8 is cool blue-white drafting film (b* {p[8].B:0.0})");
        Assert.True(c[8].B < -25, $"A8's bar is a technical blue (b* {c[8].B:0.0})");
        Assert.True(p[9].C < 1 && p[9].L > 98, $"A9 is neutral coated white (chroma {p[9].C:0.00})");
        Assert.True(c[9].C < 10 && c[9].B < 0, $"A9's bar is a dark slate (chroma {c[9].C:0.0})");

        // Computerised at the end: flat (no grain), sharp (a ≤ 2 px corner), a hairline border.
        EraTheme a9 = T(9);
        Assert.Equal(0.0, a9.Material.GrainAlpha);
        Assert.True(a9.Edge.CornerPx <= 2.0);
        Assert.True(a9.Edge.BorderPx <= 1.0);
        // …and the texture still fades: A9's grain is the lowest, A1's edge the roughest.
        foreach (EraTheme t in EraThemes.All) Assert.True(t.Material.GrainAlpha >= a9.Material.GrainAlpha);
    }

    [Fact]
    public void TheParchmentEra_IsByteIdentical_AndTheMapSubstrateIsUnthemed()
    {
        EraTheme a6 = EraThemes.For(UiEra.Medieval);
        string hash = ThemeCanon.Hash(a6);
        Assert.True(hash == A6CanonicalHash, hash);
        Assert.Equal(ParchmentPalette.PaperLight, a6.Material.Panel);
        Assert.Equal(ParchmentPalette.PaperMid, a6.Material.Field);
        Assert.Equal(Sim.Ui.World.MapInk.Default, Sim.Ui.World.MapInk.For(a6));
    }

    [Fact]
    public void TheAgeCrossFade_IsLongEnoughToSee_ButStillRestrained()
    {
        Assert.InRange(EraTransition.Seconds, 2.0, 3.0);
    }

    [Fact]
    public void TheRetune_KeepsTheStateFillsDistinct_InEveryEraButTheFixedParchment()
    {
        // The re-tuned raised and sunken surfaces give EraThemes.DistinctStates room: every re-tuned era's four card
        // fills sit ≥ StateFillDistinct apart (before the ruling five eras met it; A6 is fixed and keeps its 9.8).
        int met = 0;
        foreach (EraTheme t in EraThemes.All)
        {
            SemanticTokens s = t.Semantic;
            Rgba[] f = [s.CompletedFill, s.ActiveFill, s.AvailableFill, s.LockedFill];
            double m = double.MaxValue;
            for (int i = 0; i < 4; i++) for (int j = i + 1; j < 4; j++) m = Math.Min(m, ThemeColor.DeltaE(f[i], f[j]));
            if (t.Era != UiEra.Medieval) Assert.True(m >= EraThemes.StateFillDistinct, $"{t.Era} state fills min dE {m:0.0}");
            if (m >= EraThemes.StateFillDistinct) met++;
        }
        Assert.Equal(8, met);
    }
}
