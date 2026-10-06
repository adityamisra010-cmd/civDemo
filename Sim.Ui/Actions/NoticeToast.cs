using Sim.Ui.Render;
using Sim.Ui.Theme;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Actions;

/// <summary>
/// THE END-TURN ANNOUNCEMENT (audit E26: research completions were announced nowhere): a restrained panel at
/// the top of the map for a few seconds of UI time after an End Turn that changed the action space — research
/// learned, an activity that changed with it ("new: Farming (Root and tuber cultivation)"), a domain that
/// appeared ("new: the tax edict"). The lines are the action surface's own notices
/// (<see cref="ActionSurfaceModel.Notices"/>), so the toast and the POLICY panel say the same thing. UI time
/// only: it never reads or writes simulation state.
/// </summary>
public sealed class NoticeToast
{
    public const double Seconds = 6.0;

    private IReadOnlyList<string> _lines = [];
    private double _age = double.MaxValue;

    public bool Visible => _lines.Count > 0 && _age < Seconds;
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Shows <paramref name="lines"/> (nothing when empty).</summary>
    public void Show(IReadOnlyList<string> lines)
    {
        _lines = lines;
        _age = lines.Count > 0 ? 0.0 : double.MaxValue;
    }

    public void Advance(double dt) { if (_age < double.MaxValue) _age += dt; }

    /// <summary>For previews: the toast frozen at full opacity.</summary>
    public void Hold() => _age = 1.0;

    /// <summary>Paints the toast centred at <paramref name="top"/> across a view <paramref name="width"/> wide.</summary>
    public void Paint(DrawList d, ITextMeasure m, EraTheme t, double width, double top)
    {
        if (!Visible) return;
        double fade = Math.Clamp(Math.Min(_age / 0.35, (Seconds - _age) / 0.8), 0, 1);
        // UR-6: the kicker and the lines by role (Caption caps, Body) in the accent's text ink — they were 11 and 13 px.
        double cap = TypeScale.Px(t, TypeRole.Caption, FontRole.Caps), body = TypeScale.Px(t, TypeRole.Body, FontRole.Heading);
        double w = Math.Min(720, width - 40);
        int shown = Math.Min(_lines.Count, 4);
        var r = new RectD((width - w) / 2, top, w, 14 + t.Type.Line(cap) + 4 + shown * t.Type.Line(body) + 14);
        var frame = new DrawList();
        PanelFrame.Paint(frame, r, t, 560, FrameKind.Toast, null, t.Material.Accent, 1.2);
        foreach (DrawCmd c in frame.Commands) d.Add(Faded(c, fade));
        d.Write(t, r.CenterX, r.Y + 14, "THIS TURN", cap, ThemeColor.Alpha(t.TextInk.Accent, fade), TextAlign.Center, FontRole.Caps);
        double y = r.Y + 14 + t.Type.Line(cap) + 4;
        for (int i = 0; i < shown; i++)
        {
            d.Write(t, r.CenterX, y, ThemeText.Fit(m, t, _lines[i], body, r.W - 40, FontRole.Heading), body, ThemeColor.Alpha(t.Ink.Text, fade),
                TextAlign.Center, FontRole.Heading);
            y += t.Type.Line(body);
        }
    }

    private static DrawCmd Faded(DrawCmd c, double f) => c switch
    {
        RectCmd r => r with { Fill = r.Fill is Rgba a ? Scale(a, f) : null, Stroke = r.Stroke is Rgba b ? Scale(b, f) : null },
        LineCmd l => l with { Color = Scale(l.Color, f) },
        PolygonCmd p => p with { Fill = Scale(p.Fill, f) },
        PolylineCmd p => p with { Color = Scale(p.Color, f) },
        CircleCmd ci => ci with { Fill = ci.Fill is Rgba a ? Scale(a, f) : null, Stroke = ci.Stroke is Rgba b ? Scale(b, f) : null },
        _ => c,
    };

    private static Rgba Scale(Rgba c, double f) => new(c.R, c.G, c.B, (byte)Math.Round(c.A * Math.Clamp(f, 0, 1)));
}
