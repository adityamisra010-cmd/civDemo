using ImGuiNET;

namespace Sim.Ui.ImGuiIntegration;

/// <summary>One GPU draw call the renderer issues for one ImGui draw command: which texture, which scissor,
/// and where its triangles live in the frame's concatenated vertex and index buffers.</summary>
public readonly record struct ImGuiDrawCall(
    IntPtr TextureId, float ClipX0, float ClipY0, float ClipX1, float ClipY1,
    int BaseVertex, int StartIndex, int PrimitiveCount, int List, int Command);

/// <summary>What <see cref="ImGuiDrawData.Check"/> found in one frame's draw data. <c>RebasedCommands</c> counts
/// the commands drawn with a non-zero VtxOffset: the extra 16-bit windows ImGui opened because the renderer
/// declared the capability (0 for a frame that never passed 65,535 vertices in one list).</summary>
public sealed record ImGuiDrawReport(
    int Lists, int TotalVertices, int TotalIndices, int MaxListVertices, int DrawCalls,
    bool VtxOffsetDeclared, int RebasedCommands, IReadOnlyList<string> Violations)
{
    public bool Ok => Violations.Count == 0;

    /// <summary>Whether this frame would have tripped ImGui's 16-bit assertion
    /// (<c>draw_list->_VtxCurrentIdx &lt; (1 &lt;&lt; 16)</c>, imgui_draw.cpp) WITHOUT the VtxOffset path: one draw
    /// list holding 65,536 vertices or more.</summary>
    public bool WouldOverflowWithoutVtxOffset => MaxListVertices > ushort.MaxValue;
}

/// <summary>
/// THE IMGUI DRAW-DATA CONTRACT shared by the MonoGame renderer and the headless frame harness (M5 hardening H1).
///
/// The Research-screen crash (Director playtest, 2026-10-04): <c>imgui_draw.cpp:2261</c>,
/// <c>draw_list->_VtxCurrentIdx &lt; (1 &lt;&lt; 16)</c>, "Too many vertices in ImDrawList using 16-bit indices".
/// ImGui indexes vertices with 16-bit indices. A draw list may exceed 65,535 vertices only when the renderer
/// DECLARES <see cref="ImGuiBackendFlags.RendererHasVtxOffset"/>: ImGui then starts a new draw command whose
/// <c>VtxOffset</c> rebases the indices, and the renderer must add that offset to every index of the command.
/// The vendored renderer already added <c>cmd.VtxOffset</c> to its base vertex but never set the flag, so ImGui
/// kept every list in one 16-bit window and asserted as soon as the Research screen's A1 tree (≈60k vertices at
/// 1280×800, ≈95k at 1920×1009) was replayed into the background list. <see cref="Configure"/> sets the flag;
/// <see cref="Plan"/> is the one place the base vertex and start index are computed; <see cref="Check"/> proves,
/// index by index, that every draw call stays inside its own list's vertices and inside 16 bits.
/// </summary>
public static class ImGuiDrawData
{
    /// <summary>Declares the renderer's capabilities to the CURRENT ImGui context. Must run before the first
    /// <c>NewFrame</c> (ImGui copies the flag into every draw list's flags at NewFrame). Also keeps ImGui's
    /// built-in duplicate-ID detector on (1.91: <c>io.ConfigDebugHighlightIdConflicts</c>).</summary>
    public static unsafe void Configure(ImGuiIOPtr io)
    {
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.ConfigDebugHighlightIdConflicts = true;
    }

    /// <summary>The draw calls for a frame, in submission order: commands with no elements are skipped; the
    /// base vertex is the list's start in the concatenated vertex buffer PLUS the command's VtxOffset, and the
    /// start index is the list's start in the concatenated index buffer plus the command's IdxOffset.</summary>
    public static List<ImGuiDrawCall> Plan(ImDrawDataPtr drawData)
    {
        var calls = new List<ImGuiDrawCall>();
        int vtxOffset = 0, idxOffset = 0;
        for (int l = 0; l < drawData.CmdListsCount; l++)
        {
            ImDrawListPtr list = drawData.CmdLists[l];
            for (int c = 0; c < list.CmdBuffer.Size; c++)
            {
                ImDrawCmdPtr cmd = list.CmdBuffer[c];
                if (cmd.ElemCount == 0) continue;
                calls.Add(new ImGuiDrawCall(cmd.TextureId, cmd.ClipRect.X, cmd.ClipRect.Y, cmd.ClipRect.Z, cmd.ClipRect.W,
                    vtxOffset + (int)cmd.VtxOffset, idxOffset + (int)cmd.IdxOffset, (int)cmd.ElemCount / 3, l, c));
            }
            vtxOffset += list.VtxBuffer.Size;
            idxOffset += list.IdxBuffer.Size;
        }
        return calls;
    }

    /// <summary>
    /// Validates a rendered frame (after <c>ImGui.Render()</c>): the VtxOffset capability is declared; every
    /// draw command's index range lies inside its list's index buffer; every index, rebased by the command's
    /// VtxOffset, addresses a vertex of the SAME list (so the planned base vertex never reaches into another
    /// list); every list's open 16-bit window (<c>_VtxCurrentIdx</c>) is below 65,536; and the plan's element
    /// counts are whole triangles. Reads the real index buffers.
    /// </summary>
    public static unsafe ImGuiDrawReport Check(ImDrawDataPtr drawData, ImGuiIOPtr io)
    {
        var violations = new List<string>();
        bool declared = (io.BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0;
        if (!declared) violations.Add("io.BackendFlags lacks RendererHasVtxOffset");
        int totalV = 0, totalI = 0, maxV = 0, rebased = 0;
        for (int l = 0; l < drawData.CmdListsCount; l++)
        {
            ImDrawListPtr list = drawData.CmdLists[l];
            int nv = list.VtxBuffer.Size, ni = list.IdxBuffer.Size;
            totalV += nv;
            totalI += ni;
            if (nv > maxV) maxV = nv;
            if (list._VtxCurrentIdx >= 1u << 16)
                violations.Add($"list {l}: _VtxCurrentIdx {list._VtxCurrentIdx} >= 65536");
            ushort* idx = (ushort*)list.IdxBuffer.Data;
            for (int c = 0; c < list.CmdBuffer.Size; c++)
            {
                ImDrawCmdPtr cmd = list.CmdBuffer[c];
                if (cmd.ElemCount == 0) continue;
                if (cmd.VtxOffset > 0) rebased++;
                long first = cmd.IdxOffset, end = first + cmd.ElemCount;
                if (cmd.ElemCount % 3 != 0) violations.Add($"list {l} cmd {c}: ElemCount {cmd.ElemCount} not a multiple of 3");
                if (end > ni) { violations.Add($"list {l} cmd {c}: indices {first}..{end} exceed the list's {ni}"); continue; }
                if (cmd.VtxOffset >= (uint)Math.Max(1, nv)) { violations.Add($"list {l} cmd {c}: VtxOffset {cmd.VtxOffset} >= {nv} vertices"); continue; }
                int maxIdx = 0;
                for (long k = first; k < end; k++) if (idx[k] > maxIdx) maxIdx = idx[k];
                if (cmd.VtxOffset + (uint)maxIdx >= (uint)nv)
                    violations.Add($"list {l} cmd {c}: VtxOffset {cmd.VtxOffset} + index {maxIdx} addresses past the list's {nv} vertices");
            }
        }
        List<ImGuiDrawCall> plan = Plan(drawData);
        return new ImGuiDrawReport(drawData.CmdListsCount, totalV, totalI, maxV, plan.Count, declared, rebased, violations);
    }
}
