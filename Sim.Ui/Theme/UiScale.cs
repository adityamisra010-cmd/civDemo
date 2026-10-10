namespace Sim.Ui.Theme;

/// <summary>
/// THE UI SCALE (M5 polish, UI readability UR-1): one factor <c>s</c> for the type scale, the font atlas and the
/// chrome geometry. The 1920×1080 window is the reference (s = 1); a taller window or a high-DPI display grows the
/// interface, a smaller window never shrinks it below the reference (its layout reflows instead):
/// <code>s = clamp(snap⅛(max(H / 1080, osDpiScale)), 1, 2) × userScale</code>
/// 1440p → 1.375, 4K → 2. <c>userScale</c> is the player's own choice from <see cref="UserSteps"/> (Ctrl+= / Ctrl+-
/// / Ctrl+0 in the game, <c>--ui-scale</c> at launch). Presentation only, per machine: never in the session, the
/// save, the replay or the simulation. Pure and deterministic.
/// </summary>
public static class UiScale
{
    /// <summary>The reference client height (s = 1).</summary>
    public const double ReferenceHeight = 1080.0;

    /// <summary>The automatic factor's bounds.</summary>
    public const double Min = 1.0, Max = 2.0;

    /// <summary>The snapping step of the automatic factor.</summary>
    public const double Step = 0.125;

    /// <summary>How far the raw factor must leave the current one before a resize changes it (a ⅛ step plus half a
    /// step of slack, so a window dragged across a step boundary does not rebuild the atlas back and forth).</summary>
    public const double Hysteresis = 0.1875;

    /// <summary>The player's scale choices, smallest first; 1.0 is the default.</summary>
    public static IReadOnlyList<double> UserSteps { get; } = [0.9, 1.0, 1.1, 1.25, 1.5];

    /// <summary>The default user step.</summary>
    public const double DefaultUser = 1.0;

    /// <summary>Rounds to the nearest ⅛.</summary>
    public static double Snap(double v) => Math.Round(v / Step, MidpointRounding.AwayFromZero) * Step;

    /// <summary>The automatic factor for a client of <paramref name="height"/> px on a display whose OS scale is
    /// <paramref name="osDpiScale"/> (1 = 96 dpi).</summary>
    public static double Auto(int height, double osDpiScale = 1.0)
    {
        double raw = Math.Max(height / ReferenceHeight, double.IsFinite(osDpiScale) && osDpiScale > 0 ? osDpiScale : 1.0);
        return Math.Clamp(Snap(raw), Min, Max);
    }

    /// <summary>The automatic factor after a resize, with hysteresis: the current factor is kept while the raw one
    /// stays within <see cref="Hysteresis"/> of it.</summary>
    public static double Follow(double current, int height, double osDpiScale = 1.0)
    {
        double raw = Math.Clamp(Math.Max(height / ReferenceHeight, double.IsFinite(osDpiScale) && osDpiScale > 0 ? osDpiScale : 1.0), Min, Max);
        if (current >= Min && current <= Max && Math.Abs(raw - current) < Hysteresis) return current;
        return Math.Clamp(Snap(raw), Min, Max);
    }

    /// <summary>The effective scale: the automatic factor times the user's step.</summary>
    public static double Effective(double auto, double user) => auto * NearestStep(user);

    /// <summary>The user step nearest <paramref name="user"/> (an unknown value snaps to a known step).</summary>
    public static double NearestStep(double user)
    {
        double best = DefaultUser, bestD = double.MaxValue;
        foreach (double s in UserSteps)
        {
            double d = Math.Abs(s - user);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    /// <summary>The next larger (<paramref name="direction"/> &gt; 0) or smaller user step, clamped at the ends.</summary>
    public static double StepUser(double user, int direction)
    {
        int at = 0;
        double cur = NearestStep(user);
        for (int i = 0; i < UserSteps.Count; i++) if (UserSteps[i] == cur) at = i;
        int next = Math.Clamp(at + Math.Sign(direction), 0, UserSteps.Count - 1);
        return UserSteps[next];
    }

    /// <summary>Parses a <c>--ui-scale</c> value (invariant culture) to the nearest user step, or null.</summary>
    public static double? ParseUser(string? text) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v)
            && double.IsFinite(v) && v > 0 ? NearestStep(v) : null;
}
