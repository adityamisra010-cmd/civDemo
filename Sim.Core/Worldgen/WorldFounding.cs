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
                                birthRemainder: 0.0, deathRemainder: FoundingDeathRemainder,
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

        FoundInitialEmpire(world, cfg.AiEmpires);
        FoundInitialFormations(world, simCfg);
        return world;
    }

    /// <summary>
    /// ADR-031 (D-047 Parts 2-3; research.json baseline.basic_military): every founded
    /// Empire that holds a capital fields unit-families.json founding.formationsPerPolity
    /// formations of the founding identity (the warband), stationed at its capital, in roster
    /// order, with ids 1, 2, … A FORMATION TOKEN ONLY: it draws no people from the buckets
    /// and no goods from any stock, so founding conserves exactly what it did before (law 1).
    /// Position is the capital's site in terrain pixel coordinates (the continuous x/y frame of
    /// D-047 ruling 17). Without unit-family content nothing is founded, so worlds built from a
    /// config without unit-families.json are unchanged.
    /// </summary>
    private static void FoundInitialFormations(WorldState world, SimConfig simCfg)
    {
        if (simCfg.UnitFamilies is not { } families) return;
        Systems.Ages.UnitIdentity identity = families.FoundingIdentity;
        int size = world.Terrain?.Size ?? 1;
        int nextId = 1;
        for (int p = 0; p < world.Polities.Count; p++)
        {
            PolityId polity = world.Polities[p].Id;
            if (!EmpireQuery.TryGetCapital(world, polity, out SettlementId capital)) continue;
            int site = 0;
            for (int s = 0; s < world.Settlements.Count; s++)
                if (world.Settlements[s].Id.Value == capital.Value) site = world.Settlements[s].SiteCell;
            for (int k = 0; k < families.FormationsPerPolity; k++)
            {
                world.MilitaryUnits.Add(new MilitaryUnitRow(
                    nextId++, polity, identity.FamilyKey, identity.Key, capital,
                    site % size, site / size, Experience: 0.0, Army: 0));
            }
        }
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
    /// <summary>
    /// M4 §11 — found EXACTLY ONE player Empire and <paramref name="aiEmpires"/>
    /// AI ones, and divide the founded settlements between them.
    ///
    /// The player is always <c>PolityId(1)</c> (CR-011); AI Empires take 2, 3, …
    /// in order. Ids 0 and everything past the roster stay free.
    ///
    /// THE DIVISION IS ROUND-ROBIN over settlement rows, which is deliberately
    /// the dullest rule that works: it is deterministic, it needs no RNG stream
    /// (so it adds no serialized state and cannot shift any other draw), it gives
    /// every Empire a fair share without a "starting position quality" model that
    /// M4 has not earned, and it degenerates EXACTLY to the previous behaviour at
    /// <paramref name="aiEmpires"/> = 0 — one Empire, every settlement, capital
    /// on the first site. Who deserves the better ground is a real design
    /// question and this is not an answer to it; it is a placeholder honest
    /// enough to be replaced without anything else moving.
    ///
    /// Each Empire's capital is the FIRST settlement it receives, so a capital is
    /// always a place its owner actually holds. An Empire that receives none —
    /// possible when rivals outnumber settlements — is founded with no holdings
    /// and no capital, which is a representable state (M4-A) and immediately
    /// extinct by <see cref="EmpireQuery.IsExtinct"/> rather than an error.
    /// </summary>
    private static void FoundInitialEmpire(WorldState world, int aiEmpires)
    {
        int rivals = Math.Max(0, aiEmpires);
        int polities = rivals + 1;

        var player = new PolityId(1);   // CR-011 ruling — see the header. Not 0.
        world.Polities.Add(new PolityRow(player, CommandSource.Player));
        for (int a = 0; a < rivals; a++)
            world.Polities.Add(new PolityRow(new PolityId(2 + a), CommandSource.Ai));

        // Control first, in settlement order, so the table stays settlement-major
        // exactly as it was before rivals existed.
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            var holder = new PolityId(1 + (s % polities));
            // Strength 1.0: uncontested control of one's own founding. The field
            // is T4.3's SLOT; since ADR-033 D4 GovernanceSystem rewrites it as the
            // administrative reach on its first step (this 1.0 is turn 0's value).
            world.Controls.Add(new ControlRow(holder, world.Settlements[s].Id, 1.0));
        }

        // Then one capital per Empire that holds anything: its first settlement.
        // A capital-less Empire is representable (M4-A) and inventing a seat for
        // one that holds nothing would be a lie.
        for (int p = 0; p < polities; p++)
        {
            var id = new PolityId(1 + p);
            if (EmpireQuery.TryGetCapital(world, id, out _)) continue;
            for (int s = 0; s < world.Settlements.Count; s++)
            {
                if (!EmpireQuery.ControlsSettlement(world, id, world.Settlements[s].Id)) continue;
                world.Capitals.Add(new CapitalRow(id, world.Settlements[s].Id));
                break;
            }
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

    /// <summary>
    /// ADR-035 §6 (P-F0) — THE SEED VALUE OF A NEW BUCKET ROW'S D-004 DEATH ACCUMULATOR: 0.5, not 0.0. The
    /// remainder of a floored flow is, in its stationary state, spread over [0, 1) with mean 1/2; seeding it at 0
    /// makes the row's FIRST integer reconciliation a pure floor, which leaves about 8 people per settlement alive
    /// who died in the exact micro-state. They sit in the small high-mortality elder rows and die on turn 2 (the
    /// "D-004 warm-up" half of the turn-2 dip, measured −24 of seed 42's −94 by the population audit's skeptic).
    /// Seeded at its stationary mean the first reconciliation ROUNDS, deterministically, as every later one does
    /// on average. Only the death accumulator is seeded: the measured warm-up is the death remainder alone (birth-
    /// and aging-only arms do not move turn 2). Shared by ColonizationSystem for a colony's new rows, so turn-zero
    /// founding and frontier founding cannot drift.
    /// </summary>
    public const double FoundingDeathRemainder = 0.5;

    private static long Jittered(long baseUnits, double amp, ulong seed, int settlement, int slot)
    {
        if (amp <= 0.0 || baseUnits == 0) return baseUnits;
        // TWO components (ADR-035 §3 / P-F2):
        //
        // (1) a settlement-COMMON factor, amplitude amp (RC-1, ADR-017: the founding-SIZE reference class,
        //     Neolithic settlement sizes and village-fission founder groups, CV 0.4 = amp/√3): all of one
        //     settlement's slots scale together, so founding TOTALS spread by ±amp.
        //
        // (2) a per-cohort factor at DEMOGRAPHIC scale. Reference class: a founder group drawn from a
        //     population at its stable age structure — a multinomial draw of n founders over the stable
        //     shares p_c, whose cohort count has sd √(n·p_c·(1−p_c)) ≈ √n_c, i.e. a coefficient of
        //     variation CV_c ≈ 1/√n_c with n_c the cohort's EXPECTED count (Poisson limit). A uniform
        //     u ∈ [−1,1] realises CV = a/√3, so a_c = √(3/n_c), capped at 1 so the factor stays in [0, 2]
        //     and mean-preserving. Before ADR-035 this factor borrowed RC-1's ±0.69 (CV 0.4) for every
        //     cohort, a SIZE reference class applied to age COMPOSITION: a 57-person cohort varied ±39
        //     people, against ±7.5 for a real founder group, and the resulting off-stable pyramids caused
        //     the turn-2 world dip measured in the M5 polish population audit.
        //
        // Age structures and the food:people ratio still differ between settlements — at the scale real
        // founder groups differ, not at the scale settlement sizes differ.
        double us = U(seed, settlement, SettlementSlot);
        double uc = U(seed, settlement, slot);
        double expected = baseUnits * (1.0 + amp * us);
        double ampCohort = expected > 0.0 ? Math.Min(1.0, Math.Sqrt(3.0 / expected)) : 0.0;
        long jittered = ConservedMath.WholeUnits(
            Math.Round(expected * (1.0 + ampCohort * uc)),
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
