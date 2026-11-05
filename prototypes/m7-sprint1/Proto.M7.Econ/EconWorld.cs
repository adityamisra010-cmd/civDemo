using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Proto.M7.Econ;

/// <summary>
/// The SHIPPED configuration and the canonical founded world, built exactly as the repository builds them — read-only.
/// <list type="bullet">
/// <item>SimConfig: <c>SimConfigLoader.Load(sim, needs, goods, research, ages, unit-families)</c> — the recipe of
/// <c>Sim.Cli.HeadlessFounding.Found</c> (Sim.Cli/Program.cs:1503-1512), without any layer switch.</item>
/// <item>World: <c>WorldFounding.Found(worldgen, cfg, seed)</c> on the shipped worldgen.json (1024², N = 12).</item>
/// <item>Executor: era-pacing.json + pipeline.json over <c>SystemCatalog.All(cfg, worldgen)</c>, NO order log — the
/// recipe of <c>Sim.Tests/Kernel/SnapshotTests.RunFoundedGolden</c> (SnapshotTests.cs:1218-1233), whose seed-42
/// turn-300 hash is the pinned FOUNDED golden. <see cref="FoundedGoldenSeed42Turn300"/> repeats that pin so the
/// prototype can PROVE it steps the canonical world (a test asserts the equality).</item>
/// </list>
/// Nothing here writes to Sim.* state other than the prototype's own in-memory WorldState instances.
/// </summary>
public static class EconWorld
{
    /// <summary>The pinned FOUNDED golden (Sim.Tests/Kernel/SnapshotTests.cs FoundedGoldenHash; ci.yml FOUNDED_GOLDEN :266).</summary>
    public const string FoundedGoldenSeed42Turn300 = "d5ebd0b2c642ec5ce92fb6aade3efcffcd2eee1364f19d0548f5f5c306f93501";

    private static readonly Lazy<SimConfig> ShippedSim = new(LoadSim);
    private static readonly Lazy<WorldgenConfig> ShippedWorldgen = new(LoadWorldgen);

    public static SimConfig SimCfg => ShippedSim.Value;
    public static WorldgenConfig WorldgenCfg => ShippedWorldgen.Value;

    private static SimConfig LoadSim()
    {
        using var sim = global::Sim.Data.DataFiles.OpenSim();
        using var needs = global::Sim.Data.DataFiles.OpenNeeds();
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        using var research = global::Sim.Data.DataFiles.OpenResearch();
        using var ages = global::Sim.Data.DataFiles.OpenAges();
        using var families = global::Sim.Data.DataFiles.OpenUnitFamilies();
        return SimConfigLoader.Load(sim, needs, goods, research, ages, families);
    }

    private static WorldgenConfig LoadWorldgen()
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream);
    }

    /// <summary>The canonical founded world for <paramref name="seed"/> (turn 0).</summary>
    public static WorldState Found(ulong seed) => WorldFounding.Found(WorldgenCfg, SimCfg, seed);

    /// <summary>The canonical production pipeline; <paramref name="orders"/> null = the order-less golden recipe.</summary>
    public static TurnExecutor Executor(OrderLog? orders = null)
    {
        using var era = global::Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = global::Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(SimCfg, WorldgenCfg)), orders);
    }
}
