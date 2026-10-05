using ImGuiNET;
using Microsoft.Xna.Framework.Input;

namespace Sim.Ui.ImGuiIntegration;

/// <summary>
/// The ONE translation of polled input state into ImGui IO events, shared by the MonoGame renderer
/// (<see cref="ImGuiRenderer.BeforeLayout"/>) and the headless frame harness (<see cref="Headless.HeadlessImGui"/>),
/// so a scripted click reaches ImGui exactly as a real one does. <see cref="MouseState"/> and
/// <see cref="KeyboardState"/> are plain value types: constructing them needs no window or device.
/// </summary>
public static class ImGuiInput
{
    /// <summary>The keys ImGui is told about (navigation, editing, and the letters its shortcuts use).</summary>
    public static readonly (Keys Key, ImGuiKey ImGuiKey)[] KeyMap =
    [
        (Keys.Tab, ImGuiKey.Tab), (Keys.Left, ImGuiKey.LeftArrow), (Keys.Right, ImGuiKey.RightArrow),
        (Keys.Up, ImGuiKey.UpArrow), (Keys.Down, ImGuiKey.DownArrow), (Keys.PageUp, ImGuiKey.PageUp),
        (Keys.PageDown, ImGuiKey.PageDown), (Keys.Home, ImGuiKey.Home), (Keys.End, ImGuiKey.End),
        (Keys.Delete, ImGuiKey.Delete), (Keys.Back, ImGuiKey.Backspace), (Keys.Enter, ImGuiKey.Enter),
        (Keys.Escape, ImGuiKey.Escape), (Keys.Space, ImGuiKey.Space), (Keys.A, ImGuiKey.A),
        (Keys.C, ImGuiKey.C), (Keys.V, ImGuiKey.V), (Keys.X, ImGuiKey.X), (Keys.Y, ImGuiKey.Y),
        (Keys.Z, ImGuiKey.Z),
    ];

    /// <summary>Queues this frame's mouse position, buttons, wheel delta (in notches, 120 units each) and key
    /// states into ImGui. <paramref name="lastScroll"/> carries the cumulative wheel value between frames.</summary>
    public static void Feed(ImGuiIOPtr io, MouseState mouse, KeyboardState keyboard, ref int lastScroll)
    {
        io.AddMousePosEvent(mouse.X, mouse.Y);
        io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
        io.AddMouseWheelEvent(0f, (mouse.ScrollWheelValue - lastScroll) / 120f);
        lastScroll = mouse.ScrollWheelValue;

        foreach ((Keys key, ImGuiKey imguiKey) in KeyMap)
            io.AddKeyEvent(imguiKey, keyboard.IsKeyDown(key));
        io.AddKeyEvent(ImGuiKey.ModCtrl,
            keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModShift,
            keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModAlt,
            keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
    }
}
