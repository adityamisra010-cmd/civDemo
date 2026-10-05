using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using Sim.Ui.Art;
using Sim.Ui.ImGuiIntegration;

namespace Sim.Ui.Headless;

/// <summary>
/// A REAL Dear ImGui context with no window and no GPU (M5 hardening H1): the native library builds its font
/// atlas with stb_truetype on the CPU, runs NewFrame / widgets / Render exactly as in the game, and leaves the
/// draw data for <see cref="ImGuiDrawData.Check"/> instead of a GPU. The font atlas texture and every image
/// are given stand-in ids — nothing is uploaded, nothing is drawn, every vertex and index is real.
///
/// Dear ImGui keeps ONE current context per process: create one at a time, and dispose it.
/// </summary>
public sealed class HeadlessImGui : IDisposable
{
    /// <summary>The stand-in texture id of the font atlas (any non-zero value: nothing samples it).</summary>
    public static readonly IntPtr FontAtlasId = new(1);

    private readonly IntPtr _context;
    private int _lastScroll;

    public UiTheme.Fonts Fonts { get; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>The draw-data report of the last <see cref="EndFrame"/>.</summary>
    public ImGuiDrawReport? LastReport { get; private set; }

    /// <summary>Names of the ImGui windows that drew this frame whose name marks a tooltip. The game draws no
    /// tooltips, so any tooltip is ImGui's own error tooltip — the duplicate-ID warning
    /// (<c>io.ConfigDebugHighlightIdConflicts</c>) or an error-recovery report.</summary>
    public IReadOnlyList<string> LastTooltipWindows { get; private set; } = [];

    /// <param name="vtxOffset">False reproduces the PRE-FIX configuration (the renderer never declared
    /// <c>RendererHasVtxOffset</c>): a draw list past 65,535 vertices then trips ImGui's assertion and ABORTS the
    /// process. Only the out-of-process control uses it.</param>
    public HeadlessImGui(string assetsRoot, int width = 1280, int height = 800, bool vtxOffset = true)
    {
        _context = ImGui.CreateContext();
        ImGui.SetCurrentContext(_context);
        ImGuiIOPtr io = ImGui.GetIO();
        unsafe { io.NativePtr->IniFilename = null; }
        if (vtxOffset) ImGuiRenderer.PrepareContext(ownsContext: false); // the windowed renderer's own setup
        else io.ConfigDebugHighlightIdConflicts = true;
        Fonts = UiTheme.LoadFonts(assetsRoot);
        unsafe { io.Fonts.GetTexDataAsRGBA32(out byte* _, out int _, out int _, out int _); }
        io.Fonts.SetTexID(FontAtlasId);
        Resize(width, height);
    }

    public void Resize(int width, int height)
    {
        Width = width;
        Height = height;
        ImGuiIOPtr io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(width, height);
        io.DisplayFramebufferScale = System.Numerics.Vector2.One;
    }

    /// <summary>Feeds this frame's input through the game's own translation (<see cref="ImGuiInput.Feed"/>) and
    /// starts the frame.</summary>
    public void BeginFrame(MouseState mouse, KeyboardState keyboard, double dtSeconds)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.DeltaTime = (float)Math.Max(1e-4, dtSeconds);
        ImGuiInput.Feed(io, mouse, keyboard, ref _lastScroll);
        ImGui.NewFrame();
    }

    /// <summary>Ends the frame (<c>ImGui.Render</c>) and checks its draw data.</summary>
    public ImGuiDrawReport EndFrame()
    {
        ImGui.Render();
        ImDrawDataPtr data = ImGui.GetDrawData();
        LastReport = ImGuiDrawData.Check(data, ImGui.GetIO());
        var tips = new List<string>();
        unsafe
        {
            for (int l = 0; l < data.CmdListsCount; l++)
            {
                ImDrawListPtr list = data.CmdLists[l];
                if (list.VtxBuffer.Size == 0) continue;
                string? owner = Marshal.PtrToStringUTF8((IntPtr)list.NativePtr->_OwnerName);
                if (owner is not null && owner.Contains("##Tooltip", StringComparison.Ordinal)) tips.Add(owner);
            }
        }
        LastTooltipWindows = tips;
        return LastReport;
    }

    public void Dispose() => ImGui.DestroyContext(_context);
}
