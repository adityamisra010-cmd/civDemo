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
    /// <param name="aiEmpiresOverride">ADR-033 D5: found this many AI-commanded Empires instead of
    /// worldgen.json's <c>aiEmpires</c> (whose default stays 0). Recorded in the session manifest so the
    /// session replays (`sim replay --ai-empires N`).</param>
    /// <param name="wrapXOverride">Batch 3 WS3: found the world with this east-west topology instead of
    /// worldgen.json's <c>topology.wrapX</c> — a TEST AND TOOL SEAM (the launcher never sets it). Its one use is a
    /// rig that needs a size the lattice stride does not divide, which only a flat world supports (the 250-px
    /// StrideWorldsFixture world, which exists to exercise the orphan terrain columns). The manifest does not record
    /// it, so a session founded with it is not resumable from its log — like <see cref="UiSession.StartFrom(WorldState, ulong, int?, int?)"/>,
    /// it is never written as a playable session.</param>
    /// <param name="goodsOverride">ADR-056: found with this goods roster instead of the shipped one — a session recorded
    /// before the economy packets is re-founded without their goods (UiSession.Resume). Null: the shipped roster.</param>
    public static WorldState Found(
        ulong seed, int? sizeOverridePx = null, int? settlementsOverride = null, int? aiEmpiresOverride = null,
        bool? wrapXOverride = null, bool withoutMilitary = false, Sim.Core.Systems.GoodsConfig? goodsOverride = null)
    {
        WorldgenConfig worldgenCfg;
        using (var stream = Sim.Data.DataFiles.OpenWorldgen())
        {
            worldgenCfg = WorldgenConfigLoader.Load(stream);
        }
        if (sizeOverridePx is { } sz) worldgenCfg = worldgenCfg with { SizePx = sz };
        if (aiEmpiresOverride is { } ai) worldgenCfg = worldgenCfg with { AiEmpires = ai };
        if (wrapXOverride is { } wrap) worldgenCfg = worldgenCfg with { Topology = new TopologyConfig(wrap) };

        // ADR-055: withoutMilitary founds with sim.json's military layer off (the warband the ADR-031 token that holds no
        // one) — UiSession.StartWith under a config whose military layer is off, so its turn-0 world is that config's.
        // ADR-056: goodsOverride founds with that goods roster (the economy packets add stock and deposit rows from turn 0).
        SimConfig cfg = ProductionConfig();
        if (withoutMilitary) cfg = RecruitmentQuery.WithoutMilitary(cfg);
        if (goodsOverride is not null) cfg = EconomyKnowledge.WithGoods(cfg, goodsOverride);
        return WorldFounding.Found(worldgenCfg, cfg, seed, settlementsOverride);
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
