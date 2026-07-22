namespace Sim.Ui.ViewModel;

/// <summary>
/// The territory palette (T2.4): a FIXED 12-color table keyed by settlement id
/// (id mod 12) — deterministic by construction, no hashing, no state. The map's
/// first political geography: distinctness at a glance beats beauty, so the 12
/// hues are evenly spaced on the color wheel at matched saturation/value and
/// INTERLEAVED (0°, 180°, 90°, 270°, …) so consecutive settlement ids — which
/// founding often places on comparable terrain — land on opposite hues.
/// Alpha is the renderer's business (translucent fills), not the palette's.
/// </summary>
public static class SettlementPalette
{
    /// <summary>RGB per palette slot (hue-interleaved, S ≈ 0.55, V ≈ 0.91).</summary>
    private static readonly (byte R, byte G, byte B)[] Colors =
    [
        (232, 104, 104), // 0°   red
        (104, 232, 232), // 180° cyan
        (168, 232, 104), // 90°  lime
        (168, 104, 232), // 270° violet
        (232, 168, 104), // 30°  orange
        (104, 168, 232), // 210° azure
        (104, 232, 104), // 120° green
        (232, 104, 232), // 300° magenta
        (232, 232, 104), // 60°  yellow
        (104, 104, 232), // 240° blue
        (104, 232, 168), // 150° spring
        (232, 104, 168), // 330° rose
    ];

    public const int Count = 12;

    public static (byte R, byte G, byte B) Color(int settlementId)
    {
        int index = settlementId % Count;
        if (index < 0) index += Count;
        return Colors[index];
    }
}
