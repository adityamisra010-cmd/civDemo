using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Actions;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.ViewModel;

/// <summary>
/// HEADLESS EVIDENCE of the player views (sim-ui --player-views-preview [dir];
/// docs/architecture/player-views-preview/). Paints, through the SVG writer, the SETTLEMENT, EMPIRE and
/// INSTITUTIONS views built by <see cref="PlayerViews"/> from REAL sessions, on the era-themed panel frame,
/// at the era's density:
/// <list type="number">
/// <item>TURN 1 on the canonical founded world (seed 42), Age I — <see cref="ActionSurfacePreview.TurnOne"/>.</item>
/// <item>A DEVELOPED STATE at Age III — <see cref="ActionSurfacePreview.LaterRig"/> (a crop, a taxation node
///   and a road class known, a levy declared, a granary queued, played through the real session) with ONE
///   constructed fact added to the world the views read: a university of the first type founded at the
///   capital at maturity 0.9 (an InstitutionRow — the constructed part of the rig, stated in the log).</item>
/// </list>
/// The painting is a faithful stand-in for the ImGui renderer: the same blocks, headings in the era accent,
/// lines wrapped to the 396 px context panel. Deterministic: SVG SHA-256s are logged.
/// </summary>
public static class PlayerViewsPreview
{
    private const double PanelW = 396;
    private static readonly PolityId Me = UiPlayer.Empire;

    /// <summary>The panel SVG of one view.</summary>
    public static string PanelSvg(PlayerView view, string section, EraTheme t, string? fontDir)
    {
        ITextMeasure m = ApproxTextMeasure.Instance;
        var body = new DrawList();
        double x = 34, y = 72, width = PanelW - 28;
        y = Wrap(body, t, m, x, y, width, view.Title, 15, t.Ink.Text, FontRole.Body);
        foreach (ViewBlock b in view.Blocks)
        {
            y += t.Density.Gap * 0.5 + 4;
            PanelFrame.Rule(body, new RectD(x, y, width, Math.Max(1, t.Edge.BorderPx * 0.6)), t, 5);
            y += 6;
            body.Write(t, x, y, b.Heading, 15, t.Material.Accent, TextAlign.Left, FontRole.Heading);
            y += t.Type.Line(15) + 2;
            foreach (string line in b.Lines) y = Wrap(body, t, m, x, y, width, line, 14, t.Ink.TextSoft, FontRole.Body);
        }
        double height = Math.Ceiling(y + 24);
        var d = new DrawList();
        PanelFrame.Field(d, new RectD(0, 0, PanelW + 40, height + 40), t, 2);
        PanelFrame.Paint(d, new RectD(20, 20, PanelW, height), t, 3, FrameKind.Panel);
        d.Write(t, 34, 32, section, 19, t.Ink.Text, TextAlign.Left, FontRole.Heading);
        PanelFrame.Rule(d, new RectD(34, 58, PanelW - 28, Math.Max(2, t.Edge.BorderPx)), t, 4);
        foreach (DrawCmd c in body.Commands) d.Add(c);
        return SvgWriter.Write(d, PanelW + 40, height + 40, fontDir);
    }

    /// <summary>Greedy word wrap at <paramref name="width"/>; returns the y under the last line.</summary>
    private static double Wrap(DrawList d, EraTheme t, ITextMeasure m, double x, double y, double width, string text,
        double size, Rgba color, FontRole role)
    {
        string[] words = text.Split(' ');
        var line = new StringBuilder();
        foreach (string w in words)
        {
            string candidate = line.Length == 0 ? w : line + " " + w;
            if (line.Length > 0 && m.Width(t, candidate, size, role) > width)
            {
                d.Write(t, x, y, line.ToString(), size, color, TextAlign.Left, role);
                y += t.Type.Line(size);
                line.Clear().Append(w);
            }
            else { line.Clear().Append(candidate); }
        }
        if (line.Length > 0) { d.Write(t, x, y, line.ToString(), size, color, TextAlign.Left, role); y += t.Type.Line(size); }
        return y;
    }

    /// <summary>Writes the preview SVGs and a log into <paramref name="outDir"/>.</summary>
    public static IReadOnlyList<string> Run(string outDir, string? fontDir)
    {
        Directory.CreateDirectory(outDir);
        var written = new List<string>();
        var log = new List<string> { "player views preview: PlayerViews over real sessions, seed 42, canonical 1024 px world" };

        ActionSurfacePreview.State one = ActionSurfacePreview.TurnOne();
        one.Session.EndTurn();   // turn 1 played, so the trend / migration lines have a record to read
        one = one with { World = one.Session.World };
        ActionSurfacePreview.State later = ActionSurfacePreview.LaterRig();
        WorldState developed = later.World.Clone();
        int type = later.Session.Config.Research!.UniversityTypes[0].Key;
        developed.Institutions.Add(new InstitutionRow(1, Me, new SettlementId(later.Selected), type, developed.Clock.Turn, 0.9));
        later = later with { World = developed };

        foreach ((ActionSurfacePreview.State s, string stem, string note) in new[]
        {
            (one, "turn-1-a1", "turn 1 played on the canonical founded world, Age I"),
            (later, "developed-a3", "the action-surface later rig at Age III, plus ONE constructed InstitutionRow (first university type, capital, maturity 0.9)"),
        })
        {
            EraTheme t = ActionSurfacePreview.ThemeOf(s);
            int density = t.Density.Level;
            UiSession session = s.Session;
            Func<int, string> name = session.Names.Name;
            Sim.Core.Observability.SettlementRecord? record = session.Observations.Observations.Count == 0
                ? null : session.Observations.Settlement(session.Observations.LastTurn, s.Selected);
            log.Add(stem + ": " + note + "; turn " + s.World.Clock.Turn.ToString(CultureInfo.InvariantCulture)
                + ", world hash " + WorldHash.ComputeHex(s.World) + ", density level " + density.ToString(CultureInfo.InvariantCulture));
            foreach ((string kind, string title, PlayerView view) in new[]
            {
                ("settlement", "Settlement", PlayerViews.Settlement(s.World, session.Config, Me, record, s.Selected, name, density)),
                ("empire", "Empire", PlayerViews.Empire(s.World, session.Config, Me, name, density)),
                ("institutions", "Institutions", PlayerViews.Institutions(s.World, session.Config, Me, name, density)),
            })
            {
                string svg = PanelSvg(view, title, t, fontDir);
                string path = Path.Combine(outDir, stem + "-" + kind + ".svg");
                File.WriteAllText(path, svg);
                written.Add(path);
                log.Add("  " + Path.GetFileName(path) + "  sha256 " + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(svg))));
            }
        }
        File.WriteAllLines(Path.Combine(outDir, "preview-log.txt"), log);
        return written;
    }
}
