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

    /// <summary>The identity string used verbatim in title and panel.</summary>
    public static string Describe() => $"civ-sim M2 ({Sha}, {Date})";

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
