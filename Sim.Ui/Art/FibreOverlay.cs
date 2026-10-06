namespace Sim.Ui.Art;

/// <summary>
/// THE FIBRE/AGE OVERLAY, SPLIT (M5 polish, UI readability UR-2; style bible §4 item 2 as amended 2026-10-06). The
/// overlay is still multiplied over the whole frame — the map AND the interface read as one sheet of paper — but at
/// two amplitudes: the MAP takes the full fibre texture, the INTERFACE (every panel, chip, card and label drawn by
/// ImGui) takes it at <see cref="InterfaceAmplitude"/> of its depth, because at full depth it consumed a quarter of the
/// text's contrast budget (mean factor 0.864, minimum 0.461 on the shipped asset; A1 body text 9.78:1 on its tokens,
/// 7.57:1 rendered).
///
/// The host draws it in two multiply passes, so no screen geometry is needed:
/// <list type="number">
/// <item>after the map and before the interface, <see cref="Rest"/> over the frame (the map only, at this point);</item>
/// <item>after the interface, <see cref="Soft"/> over the frame (map and interface).</item>
/// </list>
/// The map receives Rest × Soft = the full fibre (to within a byte's rounding); the interface receives Soft only.
/// Pure: both textures are derived from the fibre asset's pixels, byte for byte.
/// </summary>
public static class FibreOverlay
{
    /// <summary>The share of the fibre's depth kept over the interface: <c>factor' = 1 − a·(1 − fibre)</c>.</summary>
    public const double InterfaceAmplitude = 0.35;

    /// <summary>The interface pass: each channel 255 − a·(255 − v); alpha opaque.</summary>
    public static ArtImage Soft(ArtImage fibre)
    {
        ArgumentNullException.ThrowIfNull(fibre);
        var px = new byte[fibre.Rgba.Length];
        for (int i = 0; i < px.Length; i += 4)
        {
            for (int c = 0; c < 3; c++) px[i + c] = SoftByte(fibre.Rgba[i + c]);
            px[i + 3] = 255;
        }
        return new ArtImage(fibre.Width, fibre.Height, px);
    }

    /// <summary>The map-only pass: each channel v / soft(v) (≤ 1), so the map's two passes multiply back to the fibre.</summary>
    public static ArtImage Rest(ArtImage fibre)
    {
        ArgumentNullException.ThrowIfNull(fibre);
        var px = new byte[fibre.Rgba.Length];
        for (int i = 0; i < px.Length; i += 4)
        {
            for (int c = 0; c < 3; c++) px[i + c] = RestByte(fibre.Rgba[i + c]);
            px[i + 3] = 255;
        }
        return new ArtImage(fibre.Width, fibre.Height, px);
    }

    /// <summary>One channel of the interface pass.</summary>
    public static byte SoftByte(byte v) => (byte)Math.Round(255.0 - InterfaceAmplitude * (255 - v), MidpointRounding.AwayFromZero);

    /// <summary>One channel of the map-only pass.</summary>
    public static byte RestByte(byte v)
    {
        byte soft = SoftByte(v);
        return soft == 0 ? (byte)0 : (byte)Math.Min(255, Math.Round(255.0 * v / soft, MidpointRounding.AwayFromZero));
    }

    /// <summary>The mean multiply factor of a texture's first channel (1 = no darkening).</summary>
    public static double MeanFactor(ArtImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        double sum = 0;
        long n = 0;
        for (int i = 0; i < image.Rgba.Length; i += 4) { sum += image.Rgba[i] / 255.0; n++; }
        return n == 0 ? 1.0 : sum / n;
    }
}
