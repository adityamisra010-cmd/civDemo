using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Core.Worldgen;

/// <summary>
/// World founding (T1.4/T1.5): the pre-turn-0 assembly of a playable WorldState —
/// terrain (ADR-008), the founding settlement at the deterministic siting argmax,
/// the network-revision counter at 0, and the founding population + food store.
/// NOT a turn system: runs once, pure in (configs, seed); the founding twin-test
/// pins byte-identical output.
///
/// The endowment enters through Ledger.Flow with reason InitialEndowment (law 1),
/// so the person-exact reconciliation holds from the flow table alone:
/// InitialEndowment + Births − Deaths − Starvation = current population, exactly.
///
/// M1 founds exactly ONE settlement, but nothing here or downstream assumes
/// one row — every table and system iterates all settlement rows.
/// </summary>
public static class WorldFounding
{
    public static WorldState Found(WorldgenConfig cfg, SimConfig simCfg, ulong seed)
    {
        var world = new WorldState(seed)
        {
            Terrain = Worldgen.Generate(cfg, seed),
        };

        int site = SettlementSiting.ChooseSite(world.Terrain!, cfg.Siting);
        var settlement = new SettlementId(0);
        world.Settlements.Add(new SettlementRow(settlement, site, FoundedTurn: 0));

        // Network revision 0 (D-016): PathBuild takes over incrementing at T1.6;
        // CatchmentSystem recomputes only when this counter moves.
        world.NetworkMeta.Add(new NetworkMetaRow(Revision: 0));

        // Founding endowment — people and food are conserved from the first row.
        var ledger = new Ledger(world.LedgerFlows);
        FoundingConfig founding = simCfg.Founding;
        Span<long> bandCounts = [founding.Children, founding.Adults, founding.Elders];
        for (int band = 0; band < PopBands.Count; band++)
        {
            int row = world.PopBands.Add(new PopBandRow(
                settlement, band, Conserved.Zero,
                birthRemainder: 0.0, deathRemainder: 0.0,
                starvationRemainder: 0.0, agingRemainder: 0.0));
            ledger.Flow(
                ref world.PopBands.Ref(row).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, bandCounts[band], FlowDirection.Source,
                OverdrawPolicy.Throw);
        }

        int storeRow = world.FoodStores.Add(new FoodStoreRow(
            settlement, Conserved.Zero, harvestRemainder: 0.0, eatenRemainder: 0.0));
        ledger.Flow(
            ref world.FoodStores.Ref(storeRow).Store, ConservedQuantityIds.Food,
            ReasonIds.InitialEndowment, founding.FoodStore, FlowDirection.Source,
            OverdrawPolicy.Throw);

        return world;
    }
}
