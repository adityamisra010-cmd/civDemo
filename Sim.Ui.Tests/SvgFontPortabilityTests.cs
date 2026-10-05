using Xunit;
using Sim.Ui.Render;

namespace Sim.Ui.Tests;

/// <summary>F3 item 7 (M5 hardening): the SVG previews' @font-face URLs are relative to the SVG, so a committed
/// preview's bytes (and the hashes its log records) do not depend on where the repository is checked out.</summary>
public class SvgFontPortabilityTests
{
    private static string Root(string name) => Path.Combine(Path.GetTempPath(), name);

    [Fact]
    public void FontDirectoryFor_is_the_same_relative_route_from_any_checkout()
    {
        string a = SvgWriter.FontDirectoryFor(Path.Combine(Root("checkout-a"), "assets", "fonts"),
            Path.Combine(Root("checkout-a"), "docs", "architecture", "r2-previews"))!;
        string b = SvgWriter.FontDirectoryFor(Path.Combine(Root("elsewhere"), "deeper", "checkout-b", "assets", "fonts"),
            Path.Combine(Root("elsewhere"), "deeper", "checkout-b", "docs", "architecture", "r2-previews"))!;
        Assert.Equal("../../../assets/fonts", a);
        Assert.Equal(a, b);
        Assert.Null(SvgWriter.FontDirectoryFor(null, Root("x")));
    }

    [Fact]
    public void Svg_from_two_checkouts_is_byte_identical_and_carries_no_absolute_path()
    {
        string Svg(string checkout)
        {
            string fonts = SvgWriter.FontDirectoryFor(Path.Combine(checkout, "assets", "fonts"), Path.Combine(checkout, "docs", "p"))!;
            return SvgWriter.Write(new DrawList(), 10, 10, fonts);
        }
        string one = Svg(Root("co-1")), two = Svg(Path.Combine(Root("co-2"), "nested"));
        Assert.Equal(one, two);
        Assert.Contains("url('../../assets/fonts/EBGaramond-Variable.ttf')", one, StringComparison.Ordinal);
        Assert.DoesNotContain("file:", one, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), one, StringComparison.Ordinal);
    }

    [Fact]
    public void An_absolute_font_directory_still_renders_as_a_file_uri()
    {
        string svg = SvgWriter.Write(new DrawList(), 10, 10, Path.Combine(Root("abs"), "fonts"));
        Assert.Contains("url('file:", svg, StringComparison.Ordinal);
    }
}
