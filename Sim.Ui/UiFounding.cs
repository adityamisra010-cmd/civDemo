using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Ui;

/// <summary>
/// THE UI founding recipe (T1.9): the exact world Sim.Ui plays on — canonical
/// worldgen.json + sim.json + WorldFounding. Public and pure so the founding-
/// equivalence test can pin it against the CLI's recipe: UI-session replay is
/// only real if both apps found IDENTICAL worlds from the same seed. Any drift
/// here (config source, override handling, founding order) breaks that test,
/// not a played session.
/// </summary>
public static class UiFounding
{
    public static WorldState Found(
        ulong seed, int? sizeOverridePx = null, int? settlementsOverride = null)
    {
        WorldgenConfig worldgenCfg;
        using (var stream = Sim.Data.DataFiles.OpenWorldgen())
        {
            worldgenCfg = WorldgenConfigLoader.Load(stream);
        }
        if (sizeOverridePx is { } sz) worldgenCfg = worldgenCfg with { SizePx = sz };

        SimConfig simCfg;
        using (var stream = Sim.Data.DataFiles.OpenSim())
        {
            simCfg = SimConfigLoader.Load(stream);
        }

        return WorldFounding.Found(worldgenCfg, simCfg, seed, settlementsOverride);
    }
}
