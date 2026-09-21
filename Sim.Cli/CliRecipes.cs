using System.Reflection;

namespace Sim.Cli;

/// <summary>
/// The CLI's loading recipes, named once. The forensic run record has to state
/// the era table, the pipeline ORDER and the worldgen configuration the run
/// actually used; reading them through the same functions the executor is built
/// from is what stops the record describing a different recipe than the one that
/// ran.
/// </summary>
public static class CliRecipes
{
    public static Sim.Core.Kernel.EraTable Era()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return Sim.Core.Kernel.EraTableLoader.Load(stream);
    }

    public static Sim.Core.Worldgen.WorldgenConfig Worldgen()
    {
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        return Sim.Core.Worldgen.WorldgenConfigLoader.Load(stream);
    }

    public static Sim.Core.Kernel.SystemRegistration[] ProductionPipeline()
    {
        Sim.Core.Systems.SimConfig cfg;
        using (var sim = Sim.Data.DataFiles.OpenSim())
        using (var needs = Sim.Data.DataFiles.OpenNeeds())
        using (var goods = Sim.Data.DataFiles.OpenGoods())
        {
            cfg = Sim.Core.Systems.SimConfigLoader.Load(sim, needs, goods);
        }
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return Sim.Core.Kernel.PipelineLoader.Load(
            pipe, Sim.Core.SystemCatalog.All(cfg, Worldgen()));
    }
}

/// <summary>
/// Build identity for the headless binary, on the same terms as Sim.Ui's: CI
/// stamps AssemblyMetadata, a local build falls back to "dev"/"local". The
/// forensic record reports the fallback as a fallback (buildShaRecorded:false)
/// rather than letting "dev" pass for an identity.
/// </summary>
public static class CliBuildInfo
{
    public static string Sha { get; } = Metadata("BuildSha") ?? "dev";
    public static string Date { get; } = Metadata("BuildDate") ?? "local";

    private static string? Metadata(string key)
    {
        foreach (AssemblyMetadataAttribute attr in
            typeof(CliBuildInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attr.Key == key && !string.IsNullOrEmpty(attr.Value)) return attr.Value;
        }
        return null;
    }
}
