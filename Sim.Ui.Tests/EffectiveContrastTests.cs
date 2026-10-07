using ImGuiNET;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Art;
using Sim.Ui.Headless;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;
using Xunit.Abstractions;

namespace Sim.Ui.Tests;

/// <summary>
/// EFFECTIVE CONTRAST ON THE RENDERED FRAME (M5 polish UR-7; the UI audit's probe method, headless). The Research
/// screen is painted through the REAL backend into a real ImGui frame, its draw data is rasterised on the CPU exactly
/// as the renderer composes it (font atlas, bilinear sampling, alpha blend, scissor, VtxOffset) and the interface's
/// share of the fibre overlay is multiplied over it — then each sampled run's ink (the darkest 4 % of its box) is
/// compared with its ground (the box's median). Token contrast alone overstated what the eye gets (the audit measured
/// "research idle" at 2.41:1, locked costs at 2.03:1, the veiled tree at 1.4–2.4:1): this measures what is drawn.
/// </summary>
[Collection("ImGui context")]
public class EffectiveContrastTests(CanonicalTurnOneFixture fx, ITestOutputHelper output) : IClassFixture<CanonicalTurnOneFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private sealed class Frame(int w, int h)
    {
        public readonly int W = w, H = h;
        public readonly float[] R = new float[w * h], G = new float[w * h], B = new float[w * h];

        public double Luminance(int x, int y)
        {
            int i = y * W + x;
            static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            return 0.2126 * Lin(R[i]) + 0.7152 * Lin(G[i]) + 0.0722 * Lin(B[i]);
        }

        /// <summary>The probe's measure: ink = mean of the darkest 4 % of the box, ground = its median.</summary>
        public double Contrast(RectD box)
        {
            var v = new List<double>();
            for (int y = Math.Max(0, (int)box.Y); y < Math.Min(H, (int)Math.Ceiling(box.Bottom)); y++)
                for (int x = Math.Max(0, (int)box.X); x < Math.Min(W, (int)Math.Ceiling(box.Right)); x++) v.Add(Luminance(x, y));
            v.Sort();
            int k = Math.Max(1, v.Count / 25);
            double dark = 0;
            for (int i = 0; i < k; i++) dark += v[i];
            dark /= k;
            double med = v[v.Count / 2];
            return (med + 0.05) / (dark + 0.05);
        }
    }

    private sealed class Tex(int w, int h, byte[] rgba)
    {
        public void Sample(float u, float v, out float r, out float g, out float b, out float a)
        {
            float x = u * w - 0.5f, y = v * h - 0.5f;
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            r = g = b = a = 0;
            for (int k = 0; k < 4; k++)
            {
                int xi = ((x0 + (k & 1)) % w + w) % w, yi = ((y0 + (k >> 1)) % h + h) % h;
                float wgt = ((k & 1) == 1 ? fx : 1 - fx) * ((k >> 1) == 1 ? fy : 1 - fy);
                int o = (yi * w + xi) * 4;
                r += wgt * rgba[o] / 255f; g += wgt * rgba[o + 1] / 255f; b += wgt * rgba[o + 2] / 255f; a += wgt * rgba[o + 3] / 255f;
            }
        }
    }

    private static unsafe void Rasterise(Frame f, Tex font)
    {
        ImDrawDataPtr dd = ImGui.GetDrawData();
        for (int l = 0; l < dd.CmdListsCount; l++)
        {
            ImDrawListPtr list = dd.CmdLists[l];
            byte* vtx = (byte*)list.VtxBuffer.Data;
            ushort* idx = (ushort*)list.IdxBuffer.Data;
            for (int ci = 0; ci < list.CmdBuffer.Size; ci++)
            {
                ImDrawCmdPtr cmd = list.CmdBuffer[ci];
                if (cmd.ElemCount == 0 || cmd.UserCallback != IntPtr.Zero || cmd.TextureId != HeadlessImGui.FontAtlasId) continue;
                int cx0 = Math.Max(0, (int)cmd.ClipRect.X), cy0 = Math.Max(0, (int)cmd.ClipRect.Y);
                int cx1 = Math.Min(f.W, (int)cmd.ClipRect.Z), cy1 = Math.Min(f.H, (int)cmd.ClipRect.W);
                if (cx1 <= cx0 || cy1 <= cy0) continue;
                for (long e = cmd.IdxOffset; e < cmd.IdxOffset + cmd.ElemCount; e += 3)
                {
                    float* v0 = (float*)(vtx + (cmd.VtxOffset + idx[e]) * 20);
                    float* v1 = (float*)(vtx + (cmd.VtxOffset + idx[e + 1]) * 20);
                    float* v2 = (float*)(vtx + (cmd.VtxOffset + idx[e + 2]) * 20);
                    Tri(f, font, cx0, cy0, cx1, cy1, v0, v1, v2);
                }
            }
        }
    }

    private static unsafe void Tri(Frame f, Tex font, int cx0, int cy0, int cx1, int cy1, float* p0, float* p1, float* p2)
    {
        float x0 = p0[0], y0 = p0[1], x1 = p1[0], y1 = p1[1], x2 = p2[0], y2 = p2[1];
        float area = (x1 - x0) * (y2 - y0) - (y1 - y0) * (x2 - x0);
        if (MathF.Abs(area) < 1e-9f) return;
        if (area < 0) { float* tmp = p1; p1 = p2; p2 = tmp; (x1, x2) = (x2, x1); (y1, y2) = (y2, y1); area = -area; }
        int minX = Math.Max(cx0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2)))), maxX = Math.Min(cx1 - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
        int minY = Math.Max(cy0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2)))), maxY = Math.Min(cy1 - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));
        uint c0 = *(uint*)(p0 + 4), c1 = *(uint*)(p1 + 4), c2 = *(uint*)(p2 + 4);
        for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                float fx = px + 0.5f, fy = py + 0.5f;
                float w0 = (x2 - x1) * (fy - y1) - (y2 - y1) * (fx - x1);
                float w1 = (x0 - x2) * (fy - y2) - (y0 - y2) * (fx - x2);
                float w2 = (x1 - x0) * (fy - y0) - (y1 - y0) * (fx - x0);
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                float l0 = w0 / area, l1 = w1 / area, l2 = w2 / area;
                float Ch(uint c, int sh) => ((c >> sh) & 255) / 255f;
                float r = l0 * Ch(c0, 0) + l1 * Ch(c1, 0) + l2 * Ch(c2, 0), g = l0 * Ch(c0, 8) + l1 * Ch(c1, 8) + l2 * Ch(c2, 8);
                float b = l0 * Ch(c0, 16) + l1 * Ch(c1, 16) + l2 * Ch(c2, 16), a = l0 * Ch(c0, 24) + l1 * Ch(c1, 24) + l2 * Ch(c2, 24);
                font.Sample(l0 * p0[2] + l1 * p1[2] + l2 * p2[2], l0 * p0[3] + l1 * p1[3] + l2 * p2[3], out float tr, out float tg, out float tb, out float ta);
                r *= tr; g *= tg; b *= tb; a *= ta;
                if (a <= 0) continue;
                int i = py * f.W + px;
                f.R[i] = r * a + f.R[i] * (1 - a); f.G[i] = g * a + f.G[i] * (1 - a); f.B[i] = b * a + f.B[i] * (1 - a);
            }
    }

    private static void Overlay(Frame f, ArtImage tex)
    {
        for (int y = 0; y < f.H; y++)
            for (int x = 0; x < f.W; x++)
            {
                int o = ((y % tex.Height) * tex.Width + (x % tex.Width)) * 4, i = y * f.W + x;
                f.R[i] *= tex.Rgba[o] / 255f; f.G[i] *= tex.Rgba[o + 1] / 255f; f.B[i] *= tex.Rgba[o + 2] / 255f;
            }
    }

    [Fact]
    public unsafe void TheResearchScreen_AsRendered_KeepsItsWordsAboveTheFloors()
    {
        const int W = 1366, H = 768;
        using var gui = new HeadlessImGui(Assets(), W, H);
        var m = new DrawListImGuiBackend(gui.Fonts);
        UiSession s = fx.Session;
        ResearchContent content = s.Config.Research!;
        var screen = new ProgressionScreen(content, Me)
        {
            Theme = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)),
            Age = Sim.Ui.Ages.AgePanelModel.Build(s.World, s.Config.Ages, [], Me),
        };
        screen.Refresh(s.World);
        screen.Paint(W, H, m);
        // A node selected: its drawer, and the rest of the tree receding (UR-2: fills fade, words keep their ink).
        int pick = screen.FrontierNode();
        screen.Focus(pick, jump: true);
        screen.PointerMove(-1, -1);
        gui.BeginFrame(default, default, 1.0 / 60);
        DrawList list = screen.Paint(W, H, m);
        m.Render(ImGui.GetBackgroundDrawList(), list);
        Assert.True(gui.EndFrame().Ok);
        io_font(out Tex font);
        var f = new Frame(W, H);
        Rasterise(f, font);
        Overlay(f, FibreOverlay.Soft(AssetLibrary.Load(Assets()).Get("parchment/grain")));

        EraTheme t = screen.Theme;
        RectD canvas = screen.Canvas;
        RectD drawer = screen.DetailRect;
        // Sample every visible run of the classes that carry information.
        // Floors on THIS measure (ink = the darkest 4 % of the run's box, so thin regular strokes read lower than their
        // tokens — the body ink's regular Garamond measures ~4.5–5:1 here against ~9:1 on the tokens): primary BOLD
        // runs (names, titles, the turn-1 call to action) ≥ 7:1; figures ≥ 4.5:1; information in a regular weight
        // (state lines, the tier's Ages, the drawer's prose) ≥ 3.5:1 — above every failure the audit measured on this
        // screen (1.4–2.4:1: the veil, the dim inks, the idle capsule).
        var classes = new Dictionary<string, (double Floor, List<double> Seen)>
        {
            ["card name"] = (7.0, []), ["card cost / Age"] = (4.5, []), ["card state line"] = (3.5, []),
            ["tier Ages"] = (3.5, []), ["capsule"] = (3.5, []), ["drawer body"] = (3.5, []), ["drawer title"] = (7.0, []),
        };
        // The drawer's own runs are those painted from its kicker on (the cards under it were painted before it).
        List<TextCmd> runs = list.Commands.OfType<TextCmd>().ToList();
        string kicker = screen.Snapshot!.Nodes[pick].BranchName.ToUpperInvariant();
        int drawerFrom = runs.FindIndex(c => c.Text == kicker && drawer.Contains(c.X + 1, c.Y + 1));
        Assert.True(drawerFrom > 0);
        for (int ri = 0; ri < runs.Count; ri++)
        {
            TextCmd c = runs[ri];
            double width = m.Width(c.Text, c.Size, c.Role, c.Style);
            double x0 = c.Align switch { TextAlign.Center => c.X - width / 2, TextAlign.Right => c.X - width, _ => c.X };
            var box = new RectD(x0, c.Y + c.Size * 0.18, Math.Max(2, width), c.Size * 0.95);
            if (box.X < 0 || box.Right > W || box.Bottom > H || box.Y < 0) continue;
            string? cls = null;
            bool inCanvas = ri < drawerFrom && canvas.Contains(box.X + 1, box.Y + 1) && canvas.Contains(box.Right - 1, box.Bottom - 1)
                && box.Right < drawer.X - 16 && !screen.MinimapRect().Contains(box.X + 1, box.Y + 1);
            // (Not the foot of the drawer's view, where the "more below" mark lies over the next line.)
            bool inDrawer = ri >= drawerFrom && drawer.Contains(box.X + 1, box.Y + 1) && drawer.Contains(box.Right - 1, box.Bottom - 1)
                && box.Bottom < drawer.Bottom - 56;
            if (inCanvas && c.Role == FontRole.Heading && c.Size >= TypeScale.Px(t, TypeRole.Body, FontRole.Heading) * t.Type.SizeScale - 0.01 && c.Color == t.Ink.Text) cls = "card name";
            else if (inCanvas && c.Role == FontRole.Numeric && c.Text.Contains(" RP", StringComparison.Ordinal)) cls = "card cost / Age";
            else if (inCanvas && (c.Text.StartsWith("Needs", StringComparison.Ordinal) || c.Text.StartsWith("Available", StringComparison.Ordinal))) cls = "card state line";
            else if (inCanvas && c.Text.StartsWith("Ages ", StringComparison.Ordinal)) cls = "tier Ages";
            else if (c.Text.StartsWith("Research idle", StringComparison.Ordinal)) cls = "capsule";
            else if (inDrawer && c.Role == FontRole.Title) cls = "drawer title";
            else if (inDrawer && c.Role == FontRole.Body && c.Text.Length > 6) cls = "drawer body";
            if (cls is null) continue;
            double cr = f.Contrast(box);
            classes[cls].Seen.Add(cr);
            if (Environment.GetEnvironmentVariable("CONTRAST_DEBUG") == "1") output.WriteLine($"  {cls}: '{c.Text}' {c.Size:0.0} {c.Color} -> {cr:0.00}");
        }
        foreach ((string cls, (double floor, List<double> seen)) in classes)
        {
            Assert.True(seen.Count > 0, "no " + cls + " sampled");
            seen.Sort();
            double median = seen[seen.Count / 2], worst = seen[0];
            output.WriteLine($"{cls}: {seen.Count} runs, worst {worst:0.00}:1, median {median:0.00}:1 (floor {floor}:1)");
            Assert.True(median >= floor, $"{cls}: median {median:0.00}:1 < {floor}:1");
            Assert.True(worst >= floor * 0.8, $"{cls}: worst {worst:0.00}:1");
        }
    }

    private static unsafe void io_font(out Tex font)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* px, out int w, out int h);
        var bytes = new byte[w * h * 4];
        new ReadOnlySpan<byte>(px, bytes.Length).CopyTo(bytes);
        font = new Tex(w, h, bytes);
    }
}
