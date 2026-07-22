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
        // Buckets (T2.1, D-026/D-027): the FULL culture × religion × class ×
        // cohort cross product is instantiated in registry order (contiguous
        // ascending cohort runs per group — the deterministic layout every
        // consumer may rely on). The founding population belongs entirely to
        // the FIRST registered class (the always-on base class, D-027);
        // other classes found at zero, awaiting T2.2 mobility transfers.
        var ledger = new Ledger(world.LedgerFlows);
        FoundingConfig founding = simCfg.Founding;
        RegistriesConfig reg = simCfg.Registries;
        foreach (RegistryEntry culture in reg.Cultures)
        {
            foreach (RegistryEntry religion in reg.Religions)
            {
                for (int cls = 0; cls < reg.Classes.Length; cls++)
                {
                    for (int cohort = 0; cohort < Cohorts.Count; cohort++)
                    {
                        int row = world.Buckets.Add(new BucketRow(
                            settlement, new CultureId(culture.Id), new ReligionId(religion.Id),
                            new ClassId(reg.Classes[cls].Id), cohort, Conserved.Zero,
                            birthRemainder: 0.0, deathRemainder: 0.0,
                            starvationRemainder: 0.0, agingRemainder: 0.0));
                        long count = cls == 0 ? founding.CohortCounts[cohort] : 0;
                        if (count > 0)
                        {
                            ledger.Flow(
                                ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                                ReasonIds.InitialEndowment, count, FlowDirection.Source,
                                OverdrawPolicy.Throw);
                        }
                    }
                }
            }
        }

        // Class emergence latches (T2.2): base class active from the first
        // day; every other class starts dormant, awaiting its D-020 predicate.
        for (int cls = 0; cls < reg.Classes.Length; cls++)
        {
            world.ClassStates.Add(new ClassStateRow(
                settlement, new ClassId(reg.Classes[cls].Id), Active: cls == 0 ? 1 : 0));
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
