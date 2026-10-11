using System.Globalization;
using ImGuiNET;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui;

/// <summary>
/// M5 map workstream P10 — THE 'NOW USABLE' SHORTCUT'S CONTROLS. While the session's history
/// (<see cref="UiSession.NowUsableHistory"/>) offers a shortcut on the world now shown (a step fired it and the clock has
/// not reached its expiry turn), the overlays tab opens with a NOW USABLE block, kept short so the map stays clear: per
/// shortcut its headline, a PERMISSION TO USE line (the eligibility that fired: newly known by the player's research), a
/// KNOWLEDGE OF THE RESOURCE line (not simulated), and a "Show on map" button. The button SHOWS the shortcut's overlay
/// layer(s) (the P9 layers — the same state the OVERLAYS checkboxes set) and outlines their qualifying places, which are
/// only places the player knows (<see cref="ResourceOverlay.Outline"/>); the overlay key opens with the shortcut's rows —
/// its headline, the outline's count, the full permission line, the extraction note (a recipe: no research gates
/// extraction) and the knowledge statement. When the clock reaches the shortcut's expiry turn the block entry, the outline
/// and the rows go — turn-exactly — and the layers stay in OVERLAYS, where the player keeps them or hides them (permanent
/// manual access). LOOKING ONLY: UI state, never an order, never a write; nothing here is labelled 'discovered'.
/// </summary>
public sealed partial class GameUi
{
    /// <summary>The NOW USABLE block's text width at UI scale 1.</summary>
    public const float NowUsableTextWidth = 260f;

    /// <summary>The block's short permission line (the key, once shown on the map, carries the full one).</summary>
    public const string NowUsablePermissionShort = "Permission to use: newly known (your research)";

    /// <summary>The block's short knowledge line (the key carries <see cref="DiscoveryShortcuts.KnowledgeStatement"/>).</summary>
    public const string NowUsableKnowledgeShort = "Knowledge of the resource: not simulated";

    /// <summary>The block's title.</summary>
    public const string NowUsableTitle = "NOW USABLE";

    /// <summary>The prefix of a shortcut's "Show on map" control (its history index follows).</summary>
    public const string NowUsableControlPrefix = "now-usable-";

    /// <summary>The "Show on map" control of the shortcut at history index <paramref name="index"/>.</summary>
    public static string NowUsableControl(int index) => NowUsableControlPrefix + index.ToString(CultureInfo.InvariantCulture);

    // The history index of the shortcut whose outline is shown, or -1.
    private int _nowUsableFocus = -1;

    /// <summary>P10: the shortcuts offered on the world now shown (history index, entry), oldest first.</summary>
    public IReadOnlyList<(int Index, NowUsable Entry)> NowUsableOffered => _session.NowUsableHistory.Active(_world.Clock.Turn);

    /// <summary>P10: the history index of the shortcut whose outline is shown, or -1 (none, or it has expired).</summary>
    public int NowUsableFocus => FocusedNowUsable() is { } f ? f.Index : -1;

    /// <summary>The focused shortcut while it is still offered; an expired focus is dropped here.</summary>
    private (int Index, NowUsable Entry)? FocusedNowUsable()
    {
        if (_nowUsableFocus < 0) return null;
        IReadOnlyList<NowUsable> all = _session.NowUsableHistory.Entries;
        if (_nowUsableFocus >= all.Count || !all[_nowUsableFocus].ActiveAt(_world.Clock.Turn))
        {
            _nowUsableFocus = -1;   // turn-exact: the outline goes with the offer
            return null;
        }
        return (_nowUsableFocus, all[_nowUsableFocus]);
    }

    /// <summary>The overlay layer indices of a shortcut's layer keys (the session's layers; a key it has not, skipped).</summary>
    private List<int> NowUsableLayers(NowUsable e)
    {
        var r = new List<int>();
        IReadOnlyList<OverlayLayer> layers = _session.OverlayLayers;
        foreach (string key in e.Layers)
            for (int i = 0; i < layers.Count; i++)
                if (string.Equals(layers[i].Key, key, StringComparison.Ordinal) && !r.Contains(i)) r.Add(i);
        return r;
    }

    /// <summary>"Show on map": shows the shortcut's layers (the OVERLAYS state) and outlines them.</summary>
    private void ShowNowUsable(int index)
    {
        EnsureOverlayState();
        IReadOnlyList<NowUsable> all = _session.NowUsableHistory.Entries;
        if ((uint)index >= (uint)all.Count || !all[index].ActiveAt(_world.Clock.Turn)) return;
        foreach (int li in NowUsableLayers(all[index])) _overlayShown[li] = true;
        _nowUsableFocus = index;
    }

    /// <summary>The player-facing turn through which a shortcut is offered (the clock reaches ExpiresAt after it).</summary>
    private static string ThroughTurn(NowUsable e) => PlayerTurn.Current(e.ExpiresAt - 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>The highlight the lens is given: the focused shortcut's layers and the rows the key opens with.</summary>
    private OverlayHighlight? NowUsableHighlight(ResourceOverlay bake)
    {
        if (FocusedNowUsable() is not { } f) return null;
        List<int> layers = NowUsableLayers(f.Entry);
        if (layers.Count == 0) return null;
        int nodes = 0;
        var names = new List<string>();
        foreach (int li in layers)
        {
            nodes += bake.Outline(li).Nodes;
            names.Add(bake.Layers[li].Name);
        }
        var rows = new List<string>
        {
            f.Entry.Headline,
            "Outlined: the " + nodes.ToString("#,0", CultureInfo.InvariantCulture) + " known places where " + string.Join(", ", names)
                + (names.Count == 1 ? " is" : " are") + " above 0; shown through turn " + ThroughTurn(f.Entry),
        };
        rows.AddRange(f.Entry.Lines);
        return new OverlayHighlight(layers, rows);
    }

    /// <summary>Greedy word wrap of <paramref name="text"/> to <paramref name="width"/> px (the widest body face).</summary>
    private List<string> WrapLabel(string text, float width)
    {
        var lines = new List<string>();
        string line = "";
        foreach (string word in text.Split(' '))
        {
            string trial = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && LabelWidth(trial) > width) { lines.Add(line); line = word; }
            else line = trial;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    /// <summary>The block's text rows per offered shortcut (headline and lines, wrapped), for sizing and drawing alike.</summary>
    private List<(int Index, NowUsable Entry, List<string> Rows)> NowUsableBlock(float width)
    {
        var block = new List<(int, NowUsable, List<string>)>();
        foreach ((int index, NowUsable e) in NowUsableOffered)
        {
            var rows = new List<string>();
            rows.AddRange(WrapLabel(e.Headline, width));
            rows.AddRange(WrapLabel(NowUsablePermissionShort, width));
            rows.AddRange(WrapLabel(NowUsableKnowledgeShort, width));
            block.Add((index, e, rows));
        }
        return block;
    }

    /// <summary>The block's height (0 when nothing is offered): the title, then per shortcut its rows and its button row.</summary>
    private float NowUsableHeight(List<(int Index, NowUsable Entry, List<string> Rows)> block)
    {
        if (block.Count == 0) return 0f;
        float text = ImGui.GetTextLineHeightWithSpacing(), button = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y;
        float h = text;
        foreach ((_, _, List<string> rows) in block) h += rows.Count * text + button;
        return h;
    }

    /// <summary>Draws the block at the cursor (inside the overlays tab's window); returns the shortcut pressed, or -1.</summary>
    private int DrawNowUsableBlock(List<(int Index, NowUsable Entry, List<string> Rows)> block, float margin)
    {
        if (block.Count == 0) return -1;
        int pressed = -1;
        int focus = NowUsableFocus;
        ImGui.SetCursorPosX(margin);
        Text(NowUsableTitle);
        foreach ((int index, NowUsable e, List<string> rows) in block)
        {
            foreach (string row in rows)
            {
                ImGui.SetCursorPosX(margin);
                Text(row);
            }
            ImGui.SetCursorPosX(margin);
            string control = NowUsableControl(index);
            if (Button("Show on map##" + control)) pressed = index;
            Controls.Record(control);
            ImGui.SameLine();
            Text((focus == index ? "outlined; " : "") + "shown through turn " + ThroughTurn(e));
        }
        return pressed;
    }
}
