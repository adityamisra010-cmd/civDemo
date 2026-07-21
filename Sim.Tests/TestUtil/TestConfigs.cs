using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Tests.TestUtil;

/// <summary>Canonical data-file configs, loaded fresh per call (records are immutable).</summary>
public static class TestConfigs
{
    public static SimConfig Sim()
    {
        using var stream = global::Sim.Data.DataFiles.OpenSim();
        return SimConfigLoader.Load(stream);
    }

    public static WorldgenConfig Worldgen()
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream);
    }

    /// <summary>The D-015 dev preset: canonical worldgen at 256² for fast tests.</summary>
    public static WorldgenConfig DevWorldgen() => Worldgen() with { SizePx = 256 };
}
