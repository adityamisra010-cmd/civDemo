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
        int[] sites = SettlementSiting.ChooseSites(world.Terrain!, cfg.Siting, count, seed);
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
        GoodsConfig goods = simCfg.Goods
            ?? throw new ArgumentException(
                "WorldFounding requires SimConfig.Goods (goods.json) — the founding food store is a grain stock at M3.");
        var grain = new GoodId(goods.GrainId);
        long configPop = 0;
        try
        {
            foreach (long c in founding.CohortCounts) configPop = checked(configPop + c);
        }
        catch (OverflowException)
        {
            throw new FlowOverflowException(
                "founding.cohortCounts total overflows Int64 — an absurd endowment config " +
                "(realistic founding populations are < 1e6; long.MaxValue ~9.22e18).");
        }
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId settlement = world.Settlements[s].Id;
            long jitteredPop = 0;
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
                            long endowed = cls == 0
                                ? Jittered(founding.CohortCounts[cohort],
                                    founding.EndowmentJitter, seed, s, slot: cohort)
                                : 0;
                            if (endowed > 0)
                            {
                                ledger.Flow(
                                    ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                                    ReasonIds.InitialEndowment, endowed, FlowDirection.Source,
                                    OverdrawPolicy.Throw);
                                // endowed ≤ MaxWholeUnits per slot; 16 slots — the
                                // checked sum cannot realistically overflow, but if
                                // it ever does the named exception is the contract.
                                try { jitteredPop = checked(jitteredPop + endowed); }
                                catch (OverflowException)
                                {
                                    throw new FlowOverflowException(
                                        $"founding population total for settlement {s} overflows Int64.");
                                }
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

            // T3.2: one stock row per (settlement, good), goods-registry
            // order — the deterministic layout consumers may rely on. The M2
            // FoodStore MIGRATED into grain's row: the food endowment TRACKS
            // THE REALIZED POPULATION (same settlement-common factor via the
            // ratio), with only a SMALL independent per-capita wobble (amp/3).
            // MEASURED FINDING: fully independent food-vs-people jitter swung
            // founding food-per-capita ±31%, and badly-mismatched colonies
            // crashed 90%+ during the catchment warm-up — firstCrashTurn 5
            // against the Malthus corridor's [400, 800]. Founding variation
            // is meant to vary SIZE and COMPOSITION, not survival odds.
            double popScale = configPop > 0 ? jitteredPop / (double)configPop : 1.0;
            double perCapita = 1.0 + (founding.EndowmentJitter / 3.0) * U(seed, s, FoodSlot);
            long food = Math.Max(0L, ConservedMath.WholeUnits(
                Math.Round(founding.FoodStore * popScale * perCapita),
                $"founding food endowment (settlement {s})"));
            foreach (GoodEntry g in goods.Goods)
            {
                int stockRow = world.GoodStocks.Add(new GoodStockRow(
                    settlement, new GoodId(g.Id), Conserved.Zero,
                    produceRemainder: 0.0, consumeRemainder: 0.0));
                if (g.Id == grain.Value)
                {
                    ledger.Flow(
                        ref world.GoodStocks.Ref(stockRow).Amount,
                        ConservedQuantityIds.OfGood(grain),
                        ReasonIds.InitialEndowment, food,
                        FlowDirection.Source, OverdrawPolicy.Throw);
                }
            }

            // T3.2 DEPOSITS: the founding roll, per deposit-bearing good, in
            // goods-registry order. Abundance = the good's terrain channel at
            // the site × the seeded spread — what makes settlements DIFFERENT
            // (the comparative-advantage precondition). Doubles, not stocks:
            // deposits scale extraction RATES (T3.3); units enter the world
            // only through production Ledger flows.
            TerrainSet t = world.Terrain!;
            int site = world.Settlements[s].SiteCell;
            foreach (GoodEntry g in goods.Goods)
            {
                if (g.DepositChannel is null) continue;
                double channel = g.DepositChannel switch
                {
                    "moisture" => t.Moisture[site],
                    "elevation" => Math.Max(0.0, t.Elevation[site] - t.SeaLevel),
                    // water proximity: 1 at the shore, fading with the same
                    // moisture curve (clay pits, fish grounds hug the water).
                    _ => t.Moisture[site] * t.Moisture[site],
                };
                double spread = 1.0 + g.DepositSpread * U(seed, s, DepositSlotBase + g.Id);
                world.Deposits.Add(new DepositRow(
                    settlement, new GoodId(g.Id), Math.Max(0.0, channel * spread)));
            }
        }

        return world;
    }

    /// <summary>Slot base for per-good deposit rolls (cohorts 0..15, food 100,
    /// settlement factor 200; deposits 300+goodId).</summary>
    private const int DepositSlotBase = 300;

    /// <summary>Salt keeping the endowment-jitter hash space disjoint from the
    /// siting jitter and every worldgen noise salt.</summary>
    private const ulong EndowSalt = 0x454E444F_00000004UL;

    /// <summary>Slot index for the food-store endowment (cohorts use 0..15).</summary>
    private const int FoodSlot = 100;

    /// <summary>
    /// T3.1(c) FOUNDING VARIATION: the endowment jitter — baseUnits ×
    /// (1 + amp·u) with u ∈ [−1,1] from a SplitMix64 hash of (seed,
    /// settlement, slot), rounded to whole units and floored at 0. Settlements
    /// stop founding as identical 400-person copies; twin worlds stay
    /// byte-identical (pure hash, no RNG stream). The double→long conversion
    /// goes through ConservedMath.WholeUnits — an absurd endowment config
    /// throws the named FlowOverflowException instead of wrapping (M3
    /// overflow discipline).
    /// </summary>
    /// <summary>Slot index of the settlement-COMMON endowment factor.</summary>
    private const int SettlementSlot = 200;

    private static long Jittered(long baseUnits, double amp, ulong seed, int settlement, int slot)
    {
        if (amp <= 0.0 || baseUnits == 0) return baseUnits;
        // TWO components, both amplitude amp: a settlement-COMMON factor (all
        // of one settlement's slots scale together, so founding TOTALS spread
        // by ±amp — independent per-cohort noise alone cancels to ±amp/√16
        // in the total, which is no variation at all) and a per-slot factor
        // (age structures and the food:people ratio differ too).
        double us = U(seed, settlement, SettlementSlot);
        double uc = U(seed, settlement, slot);
        long jittered = ConservedMath.WholeUnits(
            Math.Round(baseUnits * (1.0 + amp * us) * (1.0 + amp * uc)),
            $"founding endowment (settlement {settlement}, slot {slot})");
        return Math.Max(0L, jittered);
    }

    private static double U(ulong seed, int settlement, int slot)
    {
        ulong h = SplitMix64.Mix(
            seed ^ EndowSalt ^ ((ulong)(uint)settlement << 32) ^ (ulong)(uint)slot);
        return (h >> 11) * (1.0 / (1UL << 53)) * 2.0 - 1.0;
    }
}
