using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Tests.TestUtil;

/// <summary>Canonical data-file configs, loaded fresh per call (records are immutable).</summary>
public static class TestConfigs
{
    public static SimConfig Sim()
    {
        using var stream = global::Sim.Data.DataFiles.OpenSim();
        using var needs = global::Sim.Data.DataFiles.OpenNeeds();
        return SimConfigLoader.Load(stream, needs);
    }

    /// <summary>The raw canonical sim.json text (for loader-rejection tests).</summary>
    public static string SimJson()
    {
        using var stream = global::Sim.Data.DataFiles.OpenSim();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static WorldgenConfig Worldgen()
    {
        using var stream = global::Sim.Data.DataFiles.OpenWorldgen();
        return WorldgenConfigLoader.Load(stream);
    }

    /// <summary>The D-015 dev preset: canonical worldgen at 256², N = 4
    /// settlements (D-025 dev preset) for fast tests.</summary>
    public static WorldgenConfig DevWorldgen()
    {
        WorldgenConfig cfg = Worldgen();
        return cfg with { SizePx = 256, Siting = cfg.Siting with { SettlementCount = 4 } };
    }
}
