using ImGuiNET;
using Xunit;
using Sim.Ui.Art;
using Sim.Ui.Render;
using Sim.Ui.Theme;

namespace Sim.Ui.Tests;

/// <summary>ImGui keeps one global current context; tests that create one never run beside each other.</summary>
[CollectionDefinition("ImGui context", DisableParallelization = true)]
public sealed class ImGuiContextCollection;

/// <summary>
/// THE IMGUI TEXT BOUNDARY on a REAL atlas (CPU only — Dear ImGui builds its atlas with stb_truetype,
/// no GPU): the shipped faces loaded through <see cref="UiTheme.LoadFonts"/> resolve every
/// typographic character DrawList knows to a glyph — the em dash that reached
/// ImGui.TextUnformatted as '?' (recon-ui §3) now sets as DrawList's hyphen, in every face.
/// </summary>
[Collection("ImGui context")]
public class ImGuiTextBoundaryTests
{
    private static string AssetsRoot()
    {
        string dir = AppContext.BaseDirectory;
        return Path.Combine(dir, "assets");
    }

    [Fact]
    public void EveryFace_ResolvesTheTypographicCharacters_NoQuestionMarks()
    {
        IntPtr ctx = ImGui.CreateContext();
        try
        {
            UiTheme.Fonts fonts = UiTheme.LoadFonts(AssetsRoot());
            Assert.Contains("IBM Plex Sans", fonts.Note, StringComparison.Ordinal);
            ImFontAtlasPtr atlas = ImGui.GetIO().Fonts;
            Assert.Equal(7, atlas.Fonts.Size);   // Garamond 19/25, Plex Serif 17/19, Plex Sans 17/19/25
            for (int i = 0; i < atlas.Fonts.Size; i++)
            {
                ImFontPtr f = atlas.Fonts[i];
                float hyphen = Advance(f, '-');
                foreach ((char from, char to) in UiText.Remaps)
                {
                    Assert.True(Has(f, from), $"font {i}: U+{(int)from:X4} unresolved");
                    Assert.Equal(Advance(f, to), Advance(f, from));   // set as its Latin-1 stand-in
                }
                foreach (char c in UiText.RealGlyphs) Assert.True(Has(f, c), $"font {i}: U+{(int)c:X4} has no glyph");
                Assert.Equal(hyphen, Advance(f, '—'));
                // The em-dash string the trade panel shows every turn measures as its DrawList form.
                string raw = "no trade this turn — every price within reach";
                Assert.Equal(f.CalcTextSizeA(19f, float.MaxValue, 0f, DrawList.Latin1(raw)).X, f.CalcTextSizeA(19f, float.MaxValue, 0f, raw).X);
                for (char c = ' '; c <= '~'; c++) Assert.True(Has(f, c));   // Latin-1 stays complete
            }
            // The era faces resolve: Plex Sans for the modern era's body and numbers.
            (ImFontPtr body, _, ImFontPtr numeric) = fonts.For(EraThemes.For(UiEra.Modern));
            Assert.True(UiTheme.SameFont(body, fonts.SansBody));
            Assert.True(UiTheme.SameFont(numeric, fonts.SansNumeric));
            (ImFontPtr pBody, ImFontPtr pHead, ImFontPtr pNum) = fonts.For(EraThemes.For(UiEra.Medieval));
            Assert.True(UiTheme.SameFont(pBody, fonts.Body));
            Assert.True(UiTheme.SameFont(pHead, fonts.Header));
            Assert.True(UiTheme.SameFont(pNum, fonts.Numeric));
        }
        finally
        {
            ImGui.DestroyContext(ctx);
        }
    }

    private static bool Has(ImFontPtr f, char c) => UiTheme.HasGlyph(f, c);

    private static float Advance(ImFontPtr f, char c) => f.CalcTextSizeA(f.FontSize, float.MaxValue, 0f, c.ToString()).X;
}
