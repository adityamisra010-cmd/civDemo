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
        int[] sites = SettlementSiting.ChooseSites(
            world.Terrain!, cfg.Siting, count, simCfg.Transport.RiverCostFactor, seed);
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

            // T3.8 HOUSING: a founded settlement arrives HOUSED — its people
            // did not sleep in fields before turn 1. Dwellings for the
            // realised population enter via InitialEndowment (the same
            // out-of-nothing convention as the founding food store; build
            // materials are not retro-sunk). Round, not ceil: the sub-dwelling
            // fraction is within one household of full housing either way and
            // round is the convention every founding quantity uses.
            {
                long jitteredPopTotal = 0;
                for (int i = 0; i < world.Buckets.Count; i++)
                    if (world.Buckets[i].Settlement == settlement)
                        jitteredPopTotal += world.Buckets[i].Count.Value;
                long dwellings0 = Math.Max(0L, ConservedMath.WholeUnits(
                    Math.Round(jitteredPopTotal / simCfg.Housing.PersonsPerDwelling),
                    $"founding housing (settlement {s})"));
                int hRow = world.Housing.Add(new HousingRow(
                    settlement, Conserved.Zero, 0.0, 0.0, 1.0, 0.0));
                if (dwellings0 > 0)
                {
                    ledger.Flow(ref world.Housing.Ref(hRow).Dwellings,
                        ConservedQuantityIds.Dwellings, ReasonIds.InitialEndowment,
                        dwellings0, FlowDirection.Source, OverdrawPolicy.Throw);
                }
            }

            // T3.2 DEPOSITS: the founding roll, per deposit-bearing good, in
            // goods-registry order. Abundance = the good's terrain channel at
            // the site × the seeded spread — what makes settlements DIFFERENT
            // (the comparative-advantage precondition). Doubles, not stocks:
            // deposits scale extraction RATES (T3.3); units enter the world
            // only through production Ledger flows.
            // T4.1b/ADR-018 — THE CHANNEL IS SAMPLED OVER THE HINTERLAND, NOT
            // AT THE SITE CELL (director ruling, option (b)). The site-cell
            // reading was a MEASURED defect, exposed rather than caused by the
            // spacing change: siting selects for water access, the moisture
            // channel is "1 at the shore", and so moisture at the site read
            // EXACTLY 1.0000 at eleven of twelve settlements even at 480 km
            // spacing. The comparative-advantage precondition had been passing
            // on ONE settlement 2.4% below the ceiling since T3.2, which is
            // ADR-015 §7.5 (never assert on a quantity resting against its own
            // limit). Four goods — livestock, timber, fiber, hides — were
            // effectively constant across the founded world for two milestones.
            //
            // STATISTIC: AREA-WEIGHTED MEAN over the hinterland footprint, land
            // cells only. Derived from the DIMENSION, not swept: a deposit
            // abundance is an INTENSITY (it multiplies extraction rates), so it
            // averages; EffectiveArableKm2 is an EXTENT (km²), so it sums.
            // Summing an intensity over a catchment would make abundance grow
            // with catchment SIZE — the CR-002 error in a new costume. Equal-area
            // pixels make the plain pixel mean the area-weighted mean (this stops
            // being true for anyone who samples stride-4 lattice blocks instead).
            // Same radius the catchment uses, so deposit and arable describe the
            // SAME territory — the inconsistency between the two models was
            // arguably the underlying defect, and one of them had to be wrong.
            AddDepositsForSite(world.Deposits, world.Terrain!, cfg, simCfg, goods,
                settlement, world.Settlements[s].SiteCell, seed, s);
        }

        FoundInitialEmpire(world);
        return world;
    }

    /// <summary>
    /// M4-C (D-042): the founded world's ONE player-commanded Empire, created in
    /// the same operation that creates its settlements — so `Found` never returns
    /// a playable world whose settlements answer to nobody.
    ///
    /// Identity is the EXISTING D-037 <see cref="PolityId"/>; no Empire container,
    /// no owner field on <see cref="SettlementRow"/>, no second identity.
    ///
    /// THE ID IS 1, AND THAT IS A DIRECTOR RULING (CR-011) — DO NOT "CORRECT" IT
    /// TO 0. Every other id table in this file starts at 0, and this one does not,
    /// which looks like an oversight and is not. The convention that actually
    /// governs an order's actor is the ORDER CORPUS, which has stamped
    /// `ActorId = 1` since M1: the UI factories, roughly eight in-code order logs,
    /// and — decisively — both BINARY replay fixtures, whose bytes cannot be
    /// re-derived from source. Once M4-C populates the roster, M4-B's
    /// actor-existence check goes live and an id of 0 would reject that entire
    /// corpus, including the director's own first reign. The id is arbitrary; the
    /// frozen replay pin is not, so the id moved. Ids 0 and 2.. remain free for
    /// later AI Empires.
    ///
    /// Membership is the CONTROL relation, one row per founded settlement — so a
    /// multi-settlement founding yields ONE Empire holding N settlements, not N
    /// Empires, and later colonization extends the same Empire by appending one
    /// more control row. The capital is a DESIGNATION on the first founded
    /// settlement: one relation row, never a flag or an id on the settlement.
    /// </summary>
    private static void FoundInitialEmpire(WorldState world)
    {
        var player = new PolityId(1);   // CR-011 ruling — see the header. Not 0.
        world.Polities.Add(new PolityRow(player, CommandSource.Player));

        for (int s = 0; s < world.Settlements.Count; s++)
        {
            // Strength 1.0: uncontested control of one's own founding. The field
            // is T4.3's SLOT and no system computes or decays it yet.
            world.Controls.Add(new ControlRow(player, world.Settlements[s].Id, 1.0));
        }

        // The first founded site is the seat. Exactly one row, and only if there
        // is a settlement to seat it in — a capital-less Empire is representable
        // (M4-A) and inventing a seat for an empty world would be a lie.
        if (world.Settlements.Count > 0)
        {
            world.Capitals.Add(new CapitalRow(player, world.Settlements[0].Id));
        }
    }

    /// <summary>
    /// T4.1b: the two deposit channels averaged over a settlement's hinterland
    /// footprint — LAND CELLS ONLY (ocean is not hinterland; including it would
    /// make a coastal deposit a function of how much sea a disc happens to
    /// contain, a siting artefact rather than geography).
    ///
    /// Deterministic by construction: a fixed row-major scan of a fixed disc,
    /// summed in index order. No dictionary iteration, no RNG, no LINQ. The
    /// site cell itself is inside the disc, so a settlement whose hinterland is
    /// entirely water (impossible for a sited settlement, which requires land)
    /// would still divide by at least one.
    /// </summary>
    private static (double Moisture, double Elevation) HinterlandMeans(
        TerrainSet t, WorldgenConfig cfg, int site, double radiusPx)
    {
        int size = cfg.SizePx;
        int cx = site % size, cy = site / size;
        int r = (int)radiusPx;
        double r2 = radiusPx * radiusPx;
        double sumM = 0.0, sumE = 0.0;
        long n = 0;
        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= size) continue;
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx;
                if (x < 0 || x >= size) continue;
                if (dx * dx + dy * dy > r2) continue;
                int cell = y * size + x;
                double land = t.Elevation[cell] - t.SeaLevel;
                if (land <= 0.0) continue; // ocean is not hinterland
                sumM += t.Moisture[cell];
                sumE += land;
                n++;
            }
        }
        return n == 0
            ? (t.Moisture[site], Math.Max(0.0, t.Elevation[site] - t.SeaLevel))
            : (sumM / n, sumE / n);
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

    /// <summary>
    /// The deposit endowment for ONE site — extracted at T4.4 so turn-zero
    /// founding and dynamic frontier founding derive deposits from ONE
    /// algorithm rather than two that could drift. Behaviour is unchanged for
    /// the turn-zero caller: same channel selection, same hinterland statistic,
    /// same seeded spread, same registry order.
    ///
    /// Deposits are DOUBLES, not conserved stocks (T3.2): they scale extraction
    /// RATES, and units enter the world only through production's Ledger flows.
    /// So endowing a frontier settlement with deposits mints nothing — it
    /// describes the ground the colonists walked onto, which was always there.
    /// </summary>
    public static void AddDepositsForSite(
        Table<DepositRow> deposits, TerrainSet terrain, WorldgenConfig cfg,
        Systems.SimConfig simCfg, Systems.GoodsConfig goods,
        SettlementId settlement, int siteCell, ulong seed, int variationSlot)
    {
        double radiusPx = simCfg.Catchment.HinterlandRadiusKm / cfg.KmPerPx;
        (double meanMoisture, double meanElevation) = HinterlandMeans(terrain, cfg, siteCell, radiusPx);
        foreach (GoodEntry g in goods.Goods)
        {
            if (g.DepositChannel is null) continue;
            double channel = g.DepositChannel switch
            {
                "moisture" => meanMoisture,
                "elevation" => meanElevation,
                // water proximity: 1 at the shore, fading with the same
                // moisture curve (clay pits, fish grounds hug the water).
                _ => meanMoisture * meanMoisture,
            };
            double spread = 1.0 + g.DepositSpread * U(seed, variationSlot, DepositSlotBase + g.Id);
            deposits.Add(new DepositRow(
                settlement, new GoodId(g.Id), Math.Max(0.0, channel * spread)));
        }
    }
}
