using System.Globalization;

namespace Sim.Ui;

/// <summary>
/// Command-line parsing as a pure function (T1.9 adversarial hardening: the
/// no-args defaults — seed 42, CANONICAL size — are replay-fidelity surface;
/// a silently-added default size override would make every played session
/// unreplayable at canonical size, so the defaults are pinned by tests).
/// </summary>
public static class UiArgs
{
    public static (ulong Seed, int? SizeOverridePx, int? SettlementsOverride) Parse(string[] args)
    {
        ulong seed = 42;
        int? sizeOverride = null;
        int? settlementsOverride = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--seed" && ulong.TryParse(args[i + 1],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong s)) seed = s;
            if (args[i] == "--size" && int.TryParse(args[i + 1],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int px)) sizeOverride = px;
            if (args[i] == "--settlements" && int.TryParse(args[i + 1],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 1)
                settlementsOverride = n;
        }
        return (seed, sizeOverride, settlementsOverride);
    }
}
