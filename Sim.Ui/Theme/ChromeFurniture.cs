using Sim.Ui.Render;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Theme;

/// <summary>
/// THE CHROME'S FURNITURE in the era's hand — one painter for the four chrome windows (status band,
/// selection card, command bar, context panel): the era's frame over the window rect and the era's
/// rule at the rect <see cref="ChromeGeometry.HeaderRule"/> computes. The live renderer replays it
/// behind each ImGui window (SimUiGame.DrawPanelFurniture); the headless era preview paints it into
/// SVG. Same geometry, same visual-state logic, both paths.
/// </summary>
public static class ChromeFurniture
{
    /// <summary>A stable frame seed per chrome element (so its hand-cut edge never shimmers).</summary>
    public static int IdOf(in ChromeElement element) => element.Rule switch
    {
        RulePlacement.BottomEdge => 11,
        RulePlacement.TopEdge => 12,
        RulePlacement.UnderTitleLine => 13,
        _ => 14,
    };

    /// <summary>Paints <paramref name="element"/>'s frame and rule. <paramref name="frameHeight"/> is the
    /// MEASURED frame height in the game and <see cref="Art.UiTheme.FrameHeightPx"/> headless.</summary>
    public static void Paint(DrawList d, EraTheme t, in ChromeElement element, float frameHeight)
    {
        PanelRect p = element.Panel;
        var rect = new RectD(p.X, p.Y, p.Width, p.Height);
        int id = IdOf(element);
        PanelFrame.Paint(d, rect, t, id, FrameKind.Panel);
        ScreenRect rule = ChromeGeometry.HeaderRule(element, frameHeight);
        PanelFrame.Rule(d, new RectD(rule.X + 6, rule.Y, rule.Width - 12, rule.Height), t, id + 100);
    }
}
