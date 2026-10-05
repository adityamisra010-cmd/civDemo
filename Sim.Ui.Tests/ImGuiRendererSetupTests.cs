using System.Reflection;
using ImGuiNET;
using Xunit;
using Sim.Ui.ImGuiIntegration;

namespace Sim.Ui.Tests;

/// <summary>
/// F3 item 1 (M5 hardening): the one line that fixes the Director's Research-screen crash in the WINDOWED game is the
/// renderer declaring <see cref="ImGuiBackendFlags.RendererHasVtxOffset"/>. The windowed renderer needs a GPU, so its
/// device-free setup is factored into <see cref="ImGuiRenderer.PrepareContext"/>, which the constructor calls and the
/// headless harness reuses. These tests pin (a) that the setup declares the capability behaviourally, on a real native
/// ImGui context, and (b) that the constructor actually runs that setup (IL call-site check — the GPU half of the
/// constructor cannot run in a test host).
/// </summary>
[Collection("ImGui context")]
public class ImGuiRendererSetupTests
{
    [Fact]
    public void PrepareContext_declares_vertex_offset_support_on_a_fresh_context()
    {
        IntPtr previous = ImGui.GetCurrentContext();
        IntPtr ctx = ImGui.CreateContext();
        try
        {
            ImGui.SetCurrentContext(ctx);
            Assert.False(ImGui.GetIO().BackendFlags.HasFlag(ImGuiBackendFlags.RendererHasVtxOffset)); // control
            ImGuiRenderer.PrepareContext(ownsContext: false);
            Assert.True(ImGui.GetIO().BackendFlags.HasFlag(ImGuiBackendFlags.RendererHasVtxOffset));
            Assert.True(ImGui.GetIO().ConfigDebugHighlightIdConflicts);
        }
        finally
        {
            ImGui.DestroyContext(ctx);
            if (previous != IntPtr.Zero) ImGui.SetCurrentContext(previous);
        }
    }

    [Fact]
    public void PrepareContext_owning_creates_a_context_that_declares_vertex_offset_support()
    {
        IntPtr previous = ImGui.GetCurrentContext();
        ImGuiRenderer.PrepareContext(ownsContext: true);
        IntPtr ctx = ImGui.GetCurrentContext();
        try
        {
            Assert.NotEqual(IntPtr.Zero, ctx);
            Assert.NotEqual(previous, ctx);
            Assert.True(ImGui.GetIO().BackendFlags.HasFlag(ImGuiBackendFlags.RendererHasVtxOffset));
        }
        finally
        {
            ImGui.DestroyContext(ctx);
            if (previous != IntPtr.Zero) ImGui.SetCurrentContext(previous);
        }
    }

    [Fact]
    public void The_windowed_renderer_constructor_runs_PrepareContext()
    {
        ConstructorInfo ctor = typeof(ImGuiRenderer).GetConstructors().Single();
        byte[] il = ctor.GetMethodBody()!.GetILAsByteArray()!;
        MethodInfo target = typeof(ImGuiRenderer).GetMethod(nameof(ImGuiRenderer.PrepareContext))!;
        bool found = false;
        for (int i = 0; i + 4 < il.Length && !found; i++)
        {
            if (il[i] != 0x28) continue; // call
            int token = BitConverter.ToInt32(il, i + 1);
            try { found = ctor.Module.ResolveMethod(token) == target; }
            catch (ArgumentException) { }
        }
        Assert.True(found, "ImGuiRenderer's constructor must call PrepareContext (declares RendererHasVtxOffset).");
    }
}
