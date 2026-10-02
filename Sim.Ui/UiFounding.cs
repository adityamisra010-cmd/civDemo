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

        return WorldFounding.Found(worldgenCfg, ProductionConfig(), seed, settlementsOverride);
    }

    /// <summary>
    /// ADR-031: the ONE config recipe the UI founds, steps and reads with — the six-stream load
    /// (sim, needs, goods, research, ages, unit families), the same recipe as Sim.Cli's
    /// <c>CliRecipes</c>. With the four-stream load the Age systems were inert and founding laid
    /// down no formations; every UI call site reads through here so the founded world, the
    /// executor's systems and the panels cannot disagree about which content is loaded.
    /// </summary>
    public static SimConfig ProductionConfig()
    {
        using var stream = Sim.Data.DataFiles.OpenSim();
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var goods = Sim.Data.DataFiles.OpenGoods();
        using var research = Sim.Data.DataFiles.OpenResearch();
        using var ages = Sim.Data.DataFiles.OpenAges();
        using var families = Sim.Data.DataFiles.OpenUnitFamilies();
        return SimConfigLoader.Load(stream, needs, goods, research, ages, families);
    }
}
