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

    /// <summary>The M1 PRODUCTION turn pipeline (§3.3), consumed by PipelineLoader.</summary>
    public static Stream OpenPipeline() => Open("Sim.Data.pipeline.json");

    /// <summary>
    /// The retired T0.x toy pipeline (m1 spec §3): the kernel-invariant tests —
    /// golden/replay/harness on terrain-less toy worlds — and the M0-shaped CLI
    /// keep exercising the kernel through these systems until T1.9 extends the
    /// harness to founded production worlds.
    /// </summary>
    public static Stream OpenPipelineToy() => Open("Sim.Data.pipeline.toy.json");

    /// <summary>Worldgen tuning (D-015/D-022), consumed by WorldgenConfigLoader.</summary>
    public static Stream OpenWorldgen() => Open("Sim.Data.worldgen.json");

    /// <summary>Population/food-loop tuning (T1.5), consumed by SimConfigLoader.</summary>
    public static Stream OpenSim() => Open("Sim.Data.sim.json");

    /// <summary>The D-018 needs registry + grievance tuning (T2.6), consumed by
    /// NeedsConfigLoader (attached to SimConfig via SimConfigLoader.Load's
    /// two-stream overload).</summary>
    public static Stream OpenNeeds() => Open("Sim.Data.needs.json");

    private static Stream Open(string logicalName) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName)
        ?? throw new InvalidOperationException(
            $"embedded resource '{logicalName}' not found in Sim.Data — " +
            "check the csproj EmbeddedResource items.");
}
