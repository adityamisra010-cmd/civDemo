using Microsoft.Xna.Framework.Input;
using Sim.Ui.Art;
using Sim.Ui.ImGuiIntegration;

namespace Sim.Ui.Headless;

/// <summary>
/// THE HEADLESS UI FRAME HARNESS (M5 hardening H1): the game's real per-frame UI (<see cref="GameUi"/>) in a real
/// native ImGui context (<see cref="HeadlessImGui"/>), with no window and no GPU, driven by scripted input. Each
/// <see cref="Frame"/> does what one MonoGame tick of <see cref="SimUiGame"/> does — <see cref="GameUi.Update"/>
/// with this frame's mouse and keyboard, then <see cref="GameUi.PrepareFrame"/>, <c>ImGui.NewFrame</c> fed through
/// the game's own input translation, <see cref="GameUi.Draw"/> and <c>ImGui.Render</c> — and then checks the frame:
/// the draw data's 16-bit contract (<see cref="ImGuiDrawData.Check"/>), duplicate item ids
/// (<see cref="UiControls.Duplicates"/>) and ImGui's own error tooltip (its duplicate-ID warning). Problems are
/// collected, never thrown, so a run reports every one.
/// </summary>
public sealed class UiFrameHarness : IDisposable
{
    public HeadlessImGui Gui { get; }
    public GameUi Ui { get; }
    public string SessionLogPath { get; }

    /// <summary>Frames run so far.</summary>
    public int Frames { get; private set; }

    /// <summary>Times the UI asked to exit (Escape with nothing open); the harness acknowledges and goes on.</summary>
    public int ExitRequests { get; private set; }

    /// <summary>Every defect seen, with the frame it was seen on.</summary>
    public List<string> Problems { get; } = [];

    /// <summary>The largest single draw list of any frame (the old 16-bit ceiling is 65,535).</summary>
    public int MaxListVertices { get; private set; }

    /// <summary>A label prefixed to problems (the scenario state).</summary>
    public string Context { get; set; } = "";

    private int _x = 640, _y = 400, _wheel;
    private bool _left, _right;
    private readonly HashSet<Keys> _keys = [];

    private UiFrameHarness(HeadlessImGui gui, GameUi ui, string logPath)
    {
        Gui = gui;
        Ui = ui;
        SessionLogPath = logPath;
    }

    /// <summary>A harness over <paramref name="session"/>; its files go to <paramref name="sessionDir"/>.</summary>
    /// <param name="vtxOffset">False only for the out-of-process control that reproduces the pre-fix crash.</param>
    public static UiFrameHarness Start(UiSession session, string sessionDir, string assetsRoot,
        bool developer = false, int width = 1280, int height = 800, bool vtxOffset = true)
    {
        Directory.CreateDirectory(sessionDir);
        string log = Path.Combine(sessionDir, "orders-harness.bin");
        var gui = new HeadlessImGui(assetsRoot, width, height, vtxOffset);
        try
        {
            var ui = new GameUi(session, log, developer, AssetLibrary.Load(assetsRoot), gui.Fonts,
                new UiTextureIds(new IntPtr(2), new IntPtr(3)), width, height);
            var h = new UiFrameHarness(gui, ui, log);
            h.Frame();   // one frame so every control has a rect and ImGui's capture flags are real
            return h;
        }
        catch
        {
            gui.Dispose();
            throw;
        }
    }

    public int Width => Gui.Width;
    public int Height => Gui.Height;
    public int MouseX => _x;
    public int MouseY => _y;

    public MouseState Mouse => new(_x, _y, _wheel,
        _left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
        _right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);

    public KeyboardState Keyboard => new([.. _keys]);

    /// <summary>One tick: Update, then the ImGui frame, then the frame's checks.</summary>
    public void Frame(double dt = 1.0 / 60.0)
    {
        MouseState mouse = Mouse;
        KeyboardState keyboard = Keyboard;
        Ui.SetViewport(Gui.Width, Gui.Height);
        Ui.Update(mouse, keyboard, dt, active: true);
        if (Ui.ExitRequested) { ExitRequests++; Ui.ExitRequested = false; }
        Ui.PrepareFrame();
        Gui.BeginFrame(mouse, keyboard, dt);
        Ui.Draw();
        ImGuiDrawReport r = Gui.EndFrame();
        Frames++;
        if (r.MaxListVertices > MaxListVertices) MaxListVertices = r.MaxListVertices;
        foreach (string v in r.Violations) Problem("draw data: " + v);
        foreach (string d in Ui.Controls.Duplicates) Problem("duplicate ImGui id: " + d);
        foreach (string t in Gui.LastTooltipWindows) Problem("ImGui error tooltip (duplicate-ID warning) in " + t + " at (" + _x + "," + _y + ")");
    }

    private void Problem(string what)
    {
        string p = (Context.Length > 0 ? Context + ": " : "") + "frame " + Frames + ": " + what;
        if (Problems.Count < 200 && !Problems.Contains(p)) Problems.Add(p);
    }

    /// <summary>Runs <paramref name="n"/> frames with the input unchanged (time passes: toasts fade, the camera glides).</summary>
    public void Idle(int n, double dt = 1.0 / 60.0) { for (int i = 0; i < n; i++) Frame(dt); }

    /// <summary>Moves the pointer and holds it for three frames: ImGui's hover state settles over one frame, and its
    /// duplicate-ID detector compares with the previous frame's hovered item (its warning tooltip shows from the
    /// fourth hovered frame, which the following press/release frames of a click reach).</summary>
    public void MoveTo(double x, double y)
    {
        _x = (int)Math.Round(x);
        _y = (int)Math.Round(y);
        Frame();
        Frame();
        Frame();
    }

    /// <summary>A click as a player makes it: move, press for a frame, release, and one frame for the result to
    /// show (a button answers on its release frame, after the panels before it were drawn; and ImGui still reports
    /// the keyboard captured on the release frame, which a human never reaches with the next key press).</summary>
    public void Click(double x, double y)
    {
        MoveTo(x, y);
        _left = true;
        Frame();
        _left = false;
        Frame();
        Frame();
    }

    /// <summary>A right-button click (the game binds nothing to it; the monkey checks that stays harmless).</summary>
    public void RightClick(double x, double y)
    {
        MoveTo(x, y);
        _right = true;
        Frame();
        _right = false;
        Frame();
        Frame();
    }

    /// <summary>A drag with the left button from (x0, y0) to (x1, y1) in <paramref name="steps"/> moves.</summary>
    public void Drag(double x0, double y0, double x1, double y1, int steps = 8)
    {
        MoveTo(x0, y0);
        _left = true;
        Frame();
        for (int i = 1; i <= steps; i++)
        {
            _x = (int)Math.Round(x0 + (x1 - x0) * i / steps);
            _y = (int)Math.Round(y0 + (y1 - y0) * i / steps);
            Frame();
        }
        _left = false;
        Frame();
        Frame();
    }

    /// <summary>Wheel notches at a point (positive = away from the user, as MonoGame reports it).</summary>
    public void Wheel(double x, double y, int notches)
    {
        _x = (int)Math.Round(x);
        _y = (int)Math.Round(y);
        _wheel += 120 * notches;
        Frame();
        Frame();
    }

    /// <summary>Wheel notches with keys held (Ctrl + wheel zooms the research tree).</summary>
    public void WheelWith(double x, double y, int notches, params Keys[] held)
    {
        foreach (Keys k in held) _keys.Add(k);
        Wheel(x, y, notches);
        foreach (Keys k in held) _keys.Remove(k);
        Frame();
    }

    /// <summary>A key tap: down for a frame, up for a frame.</summary>
    public void Key(Keys key, params Keys[] modifiers)
    {
        foreach (Keys m in modifiers) _keys.Add(m);
        _keys.Add(key);
        Frame();
        _keys.Remove(key);
        foreach (Keys m in modifiers) _keys.Remove(m);
        Frame();
    }

    /// <summary>Holds a key down for <paramref name="frames"/> frames (keyboard scrolling and panning).</summary>
    public void Hold(Keys key, int frames)
    {
        _keys.Add(key);
        for (int i = 0; i < frames; i++) Frame();
        _keys.Remove(key);
        Frame();
    }

    /// <summary>Clicks the named ImGui control where the last frame drew it. False when it was not drawn.</summary>
    public bool ClickControl(string name)
    {
        if (Ui.Controls.Find(name) is not { } c) return false;
        (double x, double y) = Aim(c);
        Click(x, y);
        return true;
    }

    /// <summary>Where a player clicks a control: its centre, or — for a full-width row that runs past the edge of
    /// its panel or of the screen (the TURN audit lines carry a horizontal scrollbar) — a point near its start.</summary>
    public (double X, double Y) Aim(UiControl c)
    {
        double right = Math.Min(c.X1, Gui.Width - 1);
        ViewModel.PanelRect context = Ui.ContextRect;
        if (c.X0 >= context.X)
            right = Math.Min(right, context.X + context.Width - 16);
        double x = c.X1 <= right ? c.CenterX : Math.Min(c.X0 + 40, (c.X0 + right) / 2);
        return (x, c.CenterY);
    }

    public void Dispose() => Gui.Dispose();
}
