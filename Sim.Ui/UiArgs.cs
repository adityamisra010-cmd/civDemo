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
    /// <summary>The usage line the UI prints for its launch options (Program.cs, README).</summary>
    public const string Usage =
        "sim-ui [--seed N] [--size PX] [--settlements N] [--ai-empires N] [--dev] [--resume DIR|MANIFEST]   "
        + "(--ai-empires: found N AI-commanded Empires to play against; default worldgen.json's aiEmpires, which is 0; "
        + "--dev: open with the developer surfaces - turn audit, records, tables, build - shown; F12 toggles them in play; --resume: continue a saved session by replaying its order log)";

    /// <summary>ADR-033 D9: whether the UI opens with the developer surfaces shown (<c>--dev</c>). Off by
    /// default: the player command bar shows only the player sections. Not part of the world's identity.</summary>
    public static bool Developer(string[] args) => Array.IndexOf(args, "--dev") >= 0;

    /// <summary>The argument of <c>--resume</c>, or null.</summary>
    public static string? ResumePath(string[] args)
    {
        int at = Array.IndexOf(args, "--resume");
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    /// <summary>A session manifest path: the argument itself when it is a file, else the newest
    /// <c>session-*.json</c> in the directory (the stamps sort lexicographically = chronologically).</summary>
    public static string ResolveManifest(string pathOrDir)
    {
        if (File.Exists(pathOrDir)) return pathOrDir;
        string[] found = Directory.GetFiles(pathOrDir, "session-*.json");
        if (found.Length == 0) throw new FileNotFoundException($"no session-*.json manifest in {pathOrDir}");
        Array.Sort(found, StringComparer.Ordinal);
        return found[^1];
    }

    public static (ulong Seed, int? SizeOverridePx, int? SettlementsOverride, int? AiEmpiresOverride) Parse(string[] args)
    {
        ulong seed = 42;
        int? sizeOverride = null;
        int? settlementsOverride = null;
        int? aiEmpiresOverride = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--seed" && ulong.TryParse(args[i + 1],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong s)) seed = s;
            if (args[i] == "--size" && int.TryParse(args[i + 1],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int px)) sizeOverride = px;
            if (args[i] == "--settlements" && int.TryParse(args[i + 1],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 1)
                settlementsOverride = n;
            // ADR-033 D5: play against N AI-commanded Empires. Absent = worldgen.json's count (default 0 —
            // "turning it on is a measured decision, not a default"); the override is part of the world's
            // identity, recorded in the session manifest and the log name (…-aN.bin) so the log replays.
            if (args[i] == "--ai-empires" && int.TryParse(args[i + 1],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int ai) && ai >= 0)
                aiEmpiresOverride = ai;
        }
        return (seed, sizeOverride, settlementsOverride, aiEmpiresOverride);
    }
}
