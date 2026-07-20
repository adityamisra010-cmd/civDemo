using System.Reflection;

namespace Sim.Data;

/// <summary>
/// Access to the JSON content files shipped in this assembly as embedded
/// resources. Callers (CLI, tests, kernel bootstrap) get Streams; Sim.Core's
/// loaders stay filesystem-free. All values in these files are TUNE parameters
/// (D-006) — edit freely, the loaders validate on load.
/// </summary>
public static class DataFiles
{
    /// <summary>The era-pacing table (D-006), consumed by EraTableLoader.</summary>
    public static Stream OpenEraPacing() => Open("Sim.Data.era-pacing.json");

    /// <summary>The turn-pipeline order (§3.3), consumed by PipelineLoader.</summary>
    public static Stream OpenPipeline() => Open("Sim.Data.pipeline.json");

    /// <summary>Worldgen tuning (D-015/D-022), consumed by WorldgenConfigLoader.</summary>
    public static Stream OpenWorldgen() => Open("Sim.Data.worldgen.json");

    private static Stream Open(string logicalName) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName)
        ?? throw new InvalidOperationException(
            $"embedded resource '{logicalName}' not found in Sim.Data — " +
            "check the csproj EmbeddedResource items.");
}
