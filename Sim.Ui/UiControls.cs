using ImGuiNET;

namespace Sim.Ui;

/// <summary>The ids of the images the UI draws through ImGui (the host binds real textures to them; the
/// headless harness passes stand-ins).</summary>
public readonly record struct UiTextureIds(IntPtr Annals, IntPtr Compass);

/// <summary>One interactive ImGui control as drawn in a frame: its name (stable across frames, e.g.
/// <c>nav:Policy</c>), its screen rect, and ImGui's item id.</summary>
public readonly record struct UiControl(string Name, float X0, float Y0, float X1, float Y1, uint ItemId)
{
    public float CenterX => (X0 + X1) / 2f;
    public float CenterY => (Y0 + Y1) / 2f;
}

/// <summary>
/// THE CONTROL REGISTER (M5 hardening H1): every interactive ImGui widget the game draws records itself here
/// right after it is submitted, with the rect ImGui laid it out at and its item id. The playability harness reads
/// it to find and click each control where the player would see it, and to enumerate the controls the code
/// actually draws (none is invented). A repeated non-zero item id in one frame is a duplicate-ID defect — the
/// exhaustive form of ImGui's own hover-time conflict warning.
/// </summary>
public sealed class UiControls
{
    private List<UiControl> _building = [];
    private List<UiControl> _last = [];
    private readonly List<string> _duplicates = [];
    private List<string> _lastDuplicates = [];

    /// <summary>The controls drawn by the last completed frame, in draw order.</summary>
    public IReadOnlyList<UiControl> Last => _last;

    /// <summary>Duplicate item ids found in the last completed frame ("name A / name B (id)").</summary>
    public IReadOnlyList<string> Duplicates => _lastDuplicates;

    public void BeginFrame()
    {
        _building = [];
        _duplicates.Clear();
    }

    public void EndFrame()
    {
        _last = _building;
        _lastDuplicates = [.. _duplicates];
    }

    /// <summary>Records the ImGui item submitted last under <paramref name="name"/>.</summary>
    public void Record(string name)
    {
        System.Numerics.Vector2 min = ImGui.GetItemRectMin(), max = ImGui.GetItemRectMax();
        uint id = ImGui.GetItemID();
        if (id != 0)
            foreach (UiControl c in _building)
                if (c.ItemId == id) _duplicates.Add(c.Name + " / " + name + " (" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
        _building.Add(new UiControl(name, min.X, min.Y, max.X, max.Y, id));
    }

    /// <summary>The control named <paramref name="name"/> in the last frame, or null when it was not drawn.</summary>
    public UiControl? Find(string name)
    {
        foreach (UiControl c in _last) if (c.Name == name) return c;
        return null;
    }
}
