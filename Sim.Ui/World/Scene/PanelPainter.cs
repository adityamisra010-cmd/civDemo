using Sim.Ui.Art;
using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;

namespace Sim.Ui.World.Scene;

/// <summary>Transient UI over the map: the provenance banner (DEMO / LIVE — always present when
/// panels are drawn), the legend with the civilization summary, and the details panel.</summary>
internal static class PanelPainter
{
    public const double DetailWidth = 340;

    public static void Paint(SceneContext c, DrawList dl)
    {
        if (!c.Options.DrawPanels) return;
        Banner(c, dl);
        Legend(c, dl);
        if (c.Ui.Selected is WorldEntityId id) Details(c, dl, id);
    }

    private static void Banner(SceneContext c, DrawList dl)
    {
        bool demo = c.View.IsPlaceholder;
        string head = demo ? "DEMO / PLACEHOLDER WORLD - not simulation output" : "LIVE simulation - read-only view";
        string sub = (c.StepLabel is string step ? step + "   |   " : "") + "observer: " + c.View.Observer
            + (c.Options.BannerExtra is string extra ? "   |   " + extra : "");
        double w = Math.Max(ApproxTextMeasure.Instance.Width(head, 15, FontRole.Caps) * 1.18, ApproxTextMeasure.Instance.Width(sub, 12, FontRole.Body) * 1.08) + 30;
        var r = new RectD(12, 12, Math.Min(w, c.Proj.ViewW - 24), 50);
        dl.Rect(r, Ink.With(demo ? ParchmentPalette.IronRed : ParchmentPalette.InkPrimary, 0.93), ParchmentPalette.GoldLeaf, 1.2, 4);
        dl.Text(r.X + 14, r.Y + 7, head, 15, ParchmentPalette.PaperLight, TextAlign.Left, FontRole.Caps);
        dl.Text(r.X + 14, r.Y + 29, Ink.Fit(c.Measure, sub, 12, r.W - 24), 12, ParchmentPalette.PaperMid);
    }

    private static void Legend(SceneContext c, DrawList dl)
    {
        var rows = new List<(string Left, string Right)>();
        foreach (SummaryLine s in c.View.Summary)
            rows.Add((s.Name, $"{Morphology.Num(s.Reported)} in {s.Settlements} settlement{(s.Settlements == 1 ? "" : "s")}"));
        var agentTypes = new List<string>();
        var agentCounts = new List<int>();
        foreach (AgentView a in c.View.Agents)
        {
            if (!a.Drawable) continue;
            int i = agentTypes.IndexOf(a.Type.Name);
            if (i < 0) { agentTypes.Add(a.Type.Name); agentCounts.Add(1); } else agentCounts[i]++;
        }
        List<string> mobile = [];
        if (agentTypes.Count > 0)
        {
            var parts = new List<string>();
            for (int i = 0; i < agentTypes.Count; i++) parts.Add($"{agentTypes[i]} {agentCounts[i]}");
            mobile = Ink.Wrap(c.Measure, "Mobile (one token each): " + string.Join(", ", parts), 11, 306);
        }
        double h = 58 + 17 * Math.Max(1, rows.Count) + 15 * mobile.Count + 34;
        var r = new RectD(12, c.Proj.ViewH - h - 12, 330, h);
        dl.Rect(r, Ink.With(ParchmentPalette.PaperLight, 0.94), ParchmentPalette.InkSoft, 1.0, 4);
        dl.Text(r.X + 12, r.Y + 8, "REPORTED STRUCTURES", 12, ParchmentPalette.InkPrimary, TextAlign.Left, FontRole.Caps);
        dl.Text(r.X + 12, r.Y + 26, "counts of reports (sum of multiplicity) - never inferred", 10.5, ParchmentPalette.InkSoft);
        double y = r.Y + 46;
        if (rows.Count == 0) { dl.Text(r.X + 12, y, "none reported", 12, ParchmentPalette.InkSoft); y += 17; }
        foreach ((string left, string right) in rows)
        {
            dl.Text(r.X + 12, y, left, 12, ParchmentPalette.InkPrimary);
            dl.Text(r.Right - 12, y, right, 12, ParchmentPalette.InkPrimary, TextAlign.Right, FontRole.Numeric);
            y += 17;
        }
        foreach (string line in mobile) { dl.Text(r.X + 12, y, line, 11, ParchmentPalette.InkSoft); y += 15; }
        string lod = c.Lod switch { WorldLod.Near => "near", WorldLod.Mid => "mid", _ => "far" };
        dl.Text(r.X + 12, r.Bottom - 22, $"detail: {lod}   |   thresholds and stages are VIEW choices", 10.5, ParchmentPalette.InkSoft);
    }

    private static void Details(SceneContext c, DrawList dl, WorldEntityId id)
    {
        InspectorDetails? d = WorldInspector.Details(c.View, c.Morph, id);
        var r = new RectD(c.Proj.ViewW - DetailWidth - 12, 74, DetailWidth, 0);
        if (d is null)
        {
            r = r with { H = 64 };
            dl.Rect(r, Ink.With(ParchmentPalette.PaperLight, 0.96), ParchmentPalette.InkSoft, 1.0, 4);
            dl.Text(r.X + 14, r.Y + 12, id.ToString(), 13, ParchmentPalette.InkPrimary, TextAlign.Left, FontRole.Heading);
            dl.Text(r.X + 14, r.Y + 34, "not reported in the current state", 12, ParchmentPalette.InkSoft);
            return;
        }
        const double labelW = 118;
        double valueW = DetailWidth - labelW - 30;
        var rows = new List<(string Label, List<string> Value, DetailTag Tag)>();
        double h = 92;
        foreach (DetailLine line in d.Lines)
        {
            List<string> wrapped = Ink.Wrap(c.Measure, line.Value, 12, valueW);
            rows.Add((line.Label, wrapped, line.Tag));
            h += 16 * wrapped.Count + 3;
        }
        List<string> note = Ink.Wrap(c.Measure, d.Note, 10.5, DetailWidth - 28);
        h += 14 * Math.Min(note.Count, 6) + 30;
        r = r with { H = h };
        dl.Rect(r, Ink.With(ParchmentPalette.PaperLight, 0.97), ParchmentPalette.InkSoft, 1.0, 4);
        dl.Rect(new RectD(r.X, r.Y, r.W, 5), c.View.IsPlaceholder ? ParchmentPalette.IronRed : ParchmentPalette.Verdigris, null, 0, 2);
        dl.Text(r.X + 14, r.Y + 12, Ink.Fit(c.Measure, d.Title, 17, r.W - 28, FontRole.Heading), 17, ParchmentPalette.InkPrimary, TextAlign.Left, FontRole.Heading);
        dl.Text(r.X + 14, r.Y + 36, d.Kind.ToUpperInvariant(), 11, ParchmentPalette.InkSoft, TextAlign.Left, FontRole.Caps);
        dl.Text(r.X + 14, r.Y + 52, Ink.Fit(c.Measure, d.Provenance, 10.5, r.W - 28), 10.5,
            c.View.IsPlaceholder ? ParchmentPalette.IronRed : ParchmentPalette.Verdigris);
        double y = r.Y + 76;
        foreach ((string label, List<string> value, DetailTag tag) in rows)
        {
            dl.Text(r.X + 14, y, label, 12, ParchmentPalette.InkSoft);
            var ink = tag == DetailTag.NotReported ? ParchmentPalette.InkSoft : ParchmentPalette.InkPrimary;
            for (int i = 0; i < value.Count; i++)
                dl.Text(r.X + 14 + labelW, y + 16 * i, value[i], 12, ink, TextAlign.Left, tag == DetailTag.View ? FontRole.Body : FontRole.Numeric);
            if (tag == DetailTag.View) dl.Text(r.Right - 12, y, "view", 9.5, ParchmentPalette.GoldLeaf, TextAlign.Right, FontRole.Caps);
            y += 16 * value.Count + 3;
        }
        y += 8;
        dl.Line(r.X + 14, y, r.Right - 14, y, Ink.With(ParchmentPalette.InkSoft, 0.5), 0.8);
        y += 6;
        for (int i = 0; i < Math.Min(note.Count, 6); i++) { dl.Text(r.X + 14, y, note[i], 10.5, ParchmentPalette.InkSoft); y += 14; }
    }
}
