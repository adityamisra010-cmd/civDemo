using ImGuiNET;
using Sim.Ui.ImGuiIntegration;

namespace Sim.Ui;

/// <summary>
/// M5 workstream P6 — THE RECORDING TEXT HELPERS. Every ImGui call in Sim.Ui that can put text on screen — a text line,
/// a widget's visible label, a plot's label and overlay, a run added straight to a draw list, a window title — goes
/// through one of the helpers below, which records the string in <see cref="FrameText"/> (with the surface being
/// drawn) and then makes exactly the ImGui call it wraps. This file is the ONLY place in Sim.Ui that calls those ImGui
/// functions directly (the DrawList backend's replay excepted: it reports each replayed <see cref="Render.TextCmd"/>
/// through <see cref="DrawListImGuiBackend.Replayed"/>); <c>FrameTextTests</c> pins it with a source guard over every
/// Sim.Ui file and, independently, with ImGui's own text log of real frames. The helpers only OBSERVE: they change
/// neither what is drawn nor any state the UI or the simulation reads.
/// </summary>
public sealed partial class GameUi
{
    /// <summary>Every string the last frame drew, by surface and source (<see cref="FrameTextLog"/>). Read-only.</summary>
    public FrameTextLog FrameText { get; } = new();

    /// <summary>The surface being drawn (<see cref="FrameSurface"/>): the tag each recorded string carries.</summary>
    private string _surface = FrameSurface.Lens;

    /// <summary>What ImGui draws of a widget label: everything before its "##" id separator (ImGui's
    /// <c>FindRenderedTextEnd</c>; a "###" id hides from its first two '#' as well).</summary>
    public static string VisibleLabel(string label)
    {
        int at = label.IndexOf("##", StringComparison.Ordinal);
        return at < 0 ? label : label[..at];
    }

    private void Record(FrameTextSource source, string text) => FrameText.Add(_surface, source, text);

    /// <summary><c>ImGui.TextUnformatted</c>, recorded.</summary>
    private void Text(string text)
    {
        Record(FrameTextSource.ImGuiText, text);
        ImGui.TextUnformatted(text);
    }

    /// <summary><c>ImGui.Button</c>, its visible label recorded.</summary>
    private bool Button(string label, System.Numerics.Vector2 size = default)
    {
        Record(FrameTextSource.WidgetLabel, VisibleLabel(label));
        return ImGui.Button(label, size);
    }

    /// <summary><c>ImGui.Selectable</c>, its visible label recorded.</summary>
    private bool Selectable(string label, bool selected)
    {
        Record(FrameTextSource.WidgetLabel, VisibleLabel(label));
        return ImGui.Selectable(label, selected);
    }

    /// <summary><c>ImGui.Checkbox</c>, its visible label recorded.</summary>
    private bool Checkbox(string label, ref bool value)
    {
        Record(FrameTextSource.WidgetLabel, VisibleLabel(label));
        return ImGui.Checkbox(label, ref value);
    }

    /// <summary><c>ImGui.RadioButton</c>, its visible label recorded.</summary>
    private bool RadioButton(string label, bool active)
    {
        Record(FrameTextSource.WidgetLabel, VisibleLabel(label));
        return ImGui.RadioButton(label, active);
    }

    /// <summary><c>ImGui.InvisibleButton</c>: it draws nothing (its label is an id), so nothing is recorded — it is
    /// routed here only so that this file stays the one place that submits ImGui widgets.</summary>
    private static bool InvisibleButton(string id, System.Numerics.Vector2 size) => ImGui.InvisibleButton(id, size);

    /// <summary><c>ImGui.PlotLines</c>, its visible label and its overlay (the last value) recorded. The series itself is
    /// a picture, not text: a reader of the plotted values checks the series (TrendsModel / HistoryBuffer).</summary>
    private void PlotLines(string label, float[] series, string overlay, float min, float max, System.Numerics.Vector2 size)
    {
        Record(FrameTextSource.WidgetLabel, VisibleLabel(label));
        Record(FrameTextSource.WidgetLabel, overlay);
        ImGui.PlotLines(label, ref series[0], series.Length, 0, overlay, min, max, size);
    }

    /// <summary><c>ImDrawList.AddText</c>, recorded.</summary>
    private void AddText(ImDrawListPtr dl, ImFontPtr font, float size, System.Numerics.Vector2 at, uint colour, string text)
    {
        Record(FrameTextSource.DrawListText, text);
        dl.AddText(font, size, at, colour, text);
    }

    /// <summary><c>ImGui.Begin</c>: a window's title is recorded when it is drawn (every chrome window is
    /// NoTitleBar, so its title is an id and nothing is recorded).</summary>
    private void BeginWindow(string title, ImGuiWindowFlags flags)
    {
        if ((flags & ImGuiWindowFlags.NoTitleBar) == 0) Record(FrameTextSource.WidgetLabel, VisibleLabel(title));
        ImGui.Begin(title, flags);
    }

    /// <summary>The DrawList backend, created on first use with its replay reporting each TextCmd to
    /// <see cref="FrameText"/> under the surface being drawn.</summary>
    private DrawListImGuiBackend Backend =>
        _drawListBackend ??= new DrawListImGuiBackend(_fonts) { Replayed = t => Record(FrameTextSource.Replayed, t.Text) };
}
