using System.Reflection;

namespace Sim.Ui;

/// <summary>
/// Build identity (T1.10): the CI publish stamps commit sha + build date into
/// assembly metadata (csproj AssemblyMetadata from -p:BuildSha/-p:BuildDate);
/// local builds fall back to "dev". Shown in the window title AND the debug
/// panel — the director must always know which build he is holding (the same
/// UX principle as the stamped order-log filenames).
/// </summary>
public static class BuildInfo
{
    public static string Sha { get; } = Metadata("BuildSha") ?? "dev";
    public static string Date { get; } = Metadata("BuildDate") ?? "local";

    /// <summary>The milestone the build belongs to: the CURRENT milestone (M5 Governing Gameplay, in progress —
    /// docs/milestones.md "Roadmap rebase 2026-10-03"). Bumped together with BuildInfoTests when the current
    /// milestone changes. History: M2 → M3 at T3.12; M3 → M4 at the M4 exit paperwork (b40fad6); M4 → M5 on 2026-10-05 (M5 hardening,
    /// Director §13: the M5 playtest builds still read "civ-sim M4").</summary>
    public const string Milestone = "M5";

    /// <summary>The identity string used verbatim in title and panel.</summary>
    public static string Describe() => $"civ-sim {Milestone} ({Sha}, {Date})";

    private static string? Metadata(string key)
    {
        foreach (AssemblyMetadataAttribute attr in
            typeof(BuildInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attr.Key == key && !string.IsNullOrEmpty(attr.Value)) return attr.Value;
        }
        return null;
    }
}
