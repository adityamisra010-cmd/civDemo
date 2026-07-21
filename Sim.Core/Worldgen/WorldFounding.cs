using Sim.Core.State;

namespace Sim.Core.Worldgen;

/// <summary>
/// World founding (T1.4): the pre-turn-0 assembly of a playable WorldState —
/// terrain (ADR-008), the founding settlement at the deterministic siting argmax,
/// and the network-revision counter at 0. NOT a turn system: runs once, pure in
/// (config, seed); the founding twin-test pins byte-identical output.
///
/// M1 founds exactly ONE settlement, but nothing here or downstream assumes
/// one row — the Settlements table and CatchmentSystem iterate all rows.
/// </summary>
public static class WorldFounding
{
    public static WorldState Found(WorldgenConfig cfg, ulong seed)
    {
        var world = new WorldState(seed)
        {
            Terrain = Worldgen.Generate(cfg, seed),
        };

        int site = SettlementSiting.ChooseSite(world.Terrain!, cfg.Siting);
        world.Settlements.Add(new SettlementRow(new SettlementId(0), site, FoundedTurn: 0));

        // Network revision 0 (D-016): PathBuild takes over incrementing at T1.6;
        // CatchmentSystem recomputes only when this counter moves.
        world.NetworkMeta.Add(new NetworkMetaRow(Revision: 0));

        return world;
    }
}
