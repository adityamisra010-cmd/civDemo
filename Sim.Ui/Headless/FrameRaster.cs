using System.Numerics;
using ImGuiNET;
using Sim.Ui.Art;

namespace Sim.Ui.Headless;

/// <summary>
/// HEADLESS EVIDENCE: a small software rasterizer for ImGui's draw data, so a REAL frame of the game's UI — the same
/// <see cref="GameUi"/> per-frame code the headless gate drives — can be written to a PNG without a GPU. Triangles are
/// filled at pixel centres inside their command's clip rect, the vertex colours interpolated, the FONT ATLAS sampled
/// (nearest texel) and the result alpha-blended over the frame, in submission order — what the MonoGame renderer does.
/// Every other texture (the baked terrain and the parchment the windowed host uploads to the GPU) is drawn as a flat
/// stand-in colour: the preview is evidence of the chrome and its text, not of the art. Never a player surface.
/// </summary>
public static class FrameRaster
{
    /// <summary>The stand-in colour of a texture other than the font atlas (RGBA).</summary>
    public static readonly (byte R, byte G, byte B) StandIn = (196, 182, 150);

    /// <summary>Rasterizes the current context's draw data (after <c>ImGui.Render</c>) to a
    /// <paramref name="width"/> × <paramref name="height"/> image on a dark field — or (M6 E1.6) over
    /// <paramref name="under"/>, an image of the same size (the object-layer preview's parchment, sampled for the camera).</summary>
    public static unsafe ArtImage Rasterize(int width, int height, ArtImage? under = null)
    {
        ImDrawDataPtr data = ImGui.GetDrawData();
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* atlasPtr, out int aw, out int ah, out int _);
        IntPtr atlasId = io.Fonts.TexID;
        var px = new double[width * height * 3];
        for (int i = 0; i < width * height; i++) { px[i * 3] = 40; px[i * 3 + 1] = 44; px[i * 3 + 2] = 48; }
        if (under is { } u && u.Width == width && u.Height == height)
            for (int i = 0; i < width * height; i++) { px[i * 3] = u.Rgba[i * 4]; px[i * 3 + 1] = u.Rgba[i * 4 + 1]; px[i * 3 + 2] = u.Rgba[i * 4 + 2]; }

        for (int l = 0; l < data.CmdListsCount; l++)
        {
            ImDrawListPtr list = data.CmdLists[l];
            ImDrawVert* vtx = (ImDrawVert*)list.VtxBuffer.Data;
            ushort* idx = (ushort*)list.IdxBuffer.Data;
            for (int c = 0; c < list.CmdBuffer.Size; c++)
            {
                ImDrawCmdPtr cmd = list.CmdBuffer[c];
                if (cmd.ElemCount == 0 || cmd.UserCallback != IntPtr.Zero) continue;
                Vector4 clip = cmd.ClipRect;
                bool atlas = cmd.TextureId == atlasId;
                for (uint e = 0; e < cmd.ElemCount; e += 3)
                {
                    ImDrawVert a = vtx[cmd.VtxOffset + idx[cmd.IdxOffset + e]];
                    ImDrawVert b = vtx[cmd.VtxOffset + idx[cmd.IdxOffset + e + 1]];
                    ImDrawVert d = vtx[cmd.VtxOffset + idx[cmd.IdxOffset + e + 2]];
                    Triangle(px, width, height, clip, a, b, d, atlas ? atlasPtr : null, aw, ah);
                }
            }
        }
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            rgba[i * 4] = (byte)Math.Clamp(Math.Round(px[i * 3]), 0, 255);
            rgba[i * 4 + 1] = (byte)Math.Clamp(Math.Round(px[i * 3 + 1]), 0, 255);
            rgba[i * 4 + 2] = (byte)Math.Clamp(Math.Round(px[i * 3 + 2]), 0, 255);
            rgba[i * 4 + 3] = 255;
        }
        return new ArtImage(width, height, rgba);
    }

    private static unsafe void Triangle(double[] px, int width, int height, Vector4 clip,
        ImDrawVert a, ImDrawVert b, ImDrawVert c, byte* atlas, int aw, int ah)
    {
        double x0 = a.pos.X, y0 = a.pos.Y, x1 = b.pos.X, y1 = b.pos.Y, x2 = c.pos.X, y2 = c.pos.Y;
        double area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
        if (Math.Abs(area) < 1e-9) return;
        int minX = (int)Math.Max(Math.Floor(Math.Min(x0, Math.Min(x1, x2))), Math.Max(0, Math.Floor(clip.X)));
        int maxX = (int)Math.Min(Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))), Math.Min(width, Math.Ceiling(clip.Z)));
        int minY = (int)Math.Max(Math.Floor(Math.Min(y0, Math.Min(y1, y2))), Math.Max(0, Math.Floor(clip.Y)));
        int maxY = (int)Math.Min(Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))), Math.Min(height, Math.Ceiling(clip.W)));
        for (int y = minY; y < maxY; y++)
        {
            double py = y + 0.5;
            for (int x = minX; x < maxX; x++)
            {
                double qx = x + 0.5;
                double w0 = ((x1 - qx) * (y2 - py) - (x2 - qx) * (y1 - py)) / area;
                double w1 = ((x2 - qx) * (y0 - py) - (x0 - qx) * (y2 - py)) / area;
                double w2 = 1.0 - w0 - w1;
                if (w0 < -1e-9 || w1 < -1e-9 || w2 < -1e-9) continue;
                double r = Ch(a.col, 0) * w0 + Ch(b.col, 0) * w1 + Ch(c.col, 0) * w2;
                double g = Ch(a.col, 8) * w0 + Ch(b.col, 8) * w1 + Ch(c.col, 8) * w2;
                double bl = Ch(a.col, 16) * w0 + Ch(b.col, 16) * w1 + Ch(c.col, 16) * w2;
                double al = Ch(a.col, 24) * w0 + Ch(b.col, 24) * w1 + Ch(c.col, 24) * w2;
                double tr, tg, tb, ta;
                if (atlas != null)
                {
                    double u = a.uv.X * w0 + b.uv.X * w1 + c.uv.X * w2, v = a.uv.Y * w0 + b.uv.Y * w1 + c.uv.Y * w2;
                    int tx = Math.Clamp((int)(u * aw), 0, aw - 1), ty = Math.Clamp((int)(v * ah), 0, ah - 1);
                    byte* t = atlas + (ty * aw + tx) * 4;
                    tr = t[0] / 255.0; tg = t[1] / 255.0; tb = t[2] / 255.0; ta = t[3] / 255.0;
                }
                else { tr = StandIn.R / 255.0; tg = StandIn.G / 255.0; tb = StandIn.B / 255.0; ta = 1.0; }
                double alpha = al / 255.0 * ta;
                if (alpha <= 0) continue;
                int o = (y * width + x) * 3;
                px[o] = r * tr * alpha + px[o] * (1 - alpha);
                px[o + 1] = g * tg * alpha + px[o + 1] * (1 - alpha);
                px[o + 2] = bl * tb * alpha + px[o + 2] * (1 - alpha);
            }
        }
    }

    private static double Ch(uint col, int shift) => (col >> shift) & 0xFF;
}
