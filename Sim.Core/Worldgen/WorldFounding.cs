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
    /// <param name="settlementsOverride">D-029 (T2.3): overrides the config's
    /// siting.settlementCount — the `--settlements N` flag on both founding
    /// recipes; the first-reign fixture replays at N = 1 through it.</param>
    public static WorldState Found(
        WorldgenConfig cfg, SimConfig simCfg, ulong seed, int? settlementsOverride = null)
    {
        var world = new WorldState(seed)
        {
            Terrain = Worldgen.Generate(cfg, seed),
        };

        int count = settlementsOverride ?? cfg.Siting.SettlementCount;
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(settlementsOverride),
            $"settlement count must be >= 1, got {count}");
        int[] sites = SettlementSiting.ChooseSites(world.Terrain!, cfg.Siting, count);
        for (int s = 0; s < sites.Length; s++)
            world.Settlements.Add(new SettlementRow(new SettlementId(s), sites[s], FoundedTurn: 0));

        // Network revision 0 (D-016): PathBuild takes over incrementing at T1.6;
        // CatchmentSystem recomputes only when this counter moves.
        world.NetworkMeta.Add(new NetworkMetaRow(Revision: 0));

        // Founding endowment — people and food are conserved from the first
        // row, PER SETTLEMENT (T2.3, director ruling): every settlement gets
        // the same 400-person cohort profile and food store — the equal-split
        // policy, TUNE-noted as PROVISIONAL (per-site endowments are a later
        // ruling if the calibration battery wants them).
        // Buckets (T2.1, D-026/D-027): the FULL culture × religion × class ×
        // cohort cross product is instantiated in registry order (contiguous
        // ascending cohort runs per group — the deterministic layout every
        // consumer may rely on). The founding population belongs entirely to
        // the FIRST registered class (the always-on base class, D-027);
        // other classes found at zero, awaiting T2.2 mobility transfers.
        var ledger = new Ledger(world.LedgerFlows);
        FoundingConfig founding = simCfg.Founding;
        RegistriesConfig reg = simCfg.Registries;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
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
                            long endowed = cls == 0 ? founding.CohortCounts[cohort] : 0;
                            if (endowed > 0)
                            {
                                ledger.Flow(
                                    ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                                    ReasonIds.InitialEndowment, endowed, FlowDirection.Source,
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

            // Grievance stocks (T2.6, D-018): one row per (settlement, class),
            // founded at zero — nobody starts aggrieved. Settlement-major,
            // class-registry order: the deterministic layout NeedsGrievance
            // relies on. (Satisfaction rows are per-turn derived state, not
            // founded.)
            for (int cls = 0; cls < reg.Classes.Length; cls++)
            {
                world.Grievances.Add(new GrievanceRow(
                    settlement, new ClassId(reg.Classes[cls].Id), Value: 0.0));
            }

            int storeRow = world.FoodStores.Add(new FoodStoreRow(
                settlement, Conserved.Zero, harvestRemainder: 0.0, eatenRemainder: 0.0));
            ledger.Flow(
                ref world.FoodStores.Ref(storeRow).Store, ConservedQuantityIds.Food,
                ReasonIds.InitialEndowment, founding.FoodStore, FlowDirection.Source,
                OverdrawPolicy.Throw);
        }

        return world;
    }
}
