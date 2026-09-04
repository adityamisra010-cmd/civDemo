using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Core.Systems.Colonization;

/// <summary>Tables owned by <see cref="ColonizationSystem"/> (T4.4). `Settlements` is a
/// NEW ownership grant: before this packet no system could append a settlement at all,
/// and the table was immutable for the whole simulation. Everything else here is the
/// Class-A state a settlement must have on the turn it exists (see the system doc).</summary>
public readonly record struct ColonizationTables(
    Table<SettlementRow> Settlements,
    Table<BucketRow> Buckets,
    Table<GoodStockRow> Stocks,
    Table<DepositRow> Deposits,
    Table<ClassStateRow> ClassStates,
    Table<GrievanceRow> Grievances,
    Table<SmoothedAttractivenessRow> Smoothed,
    Table<ControlRow> Controls);

/// <summary>
/// T4.4 — COLONIZATION FROM BELOW (D-037 B1). Population with nowhere viable to go
/// departs into unclaimed land and founds a new settlement.
///
/// THE RATIFIED SHAPE, not an invented one. D-037 B1: *"Migration currently runs
/// settlement-to-settlement, and ADR-012 rules that with no viable destination people die
/// at home. EXTEND IT: groups may depart into UNCLAIMED land and found new settlements. A
/// settlement founded by departing population NEED NOT belong to the polity they left —
/// refugee foundings may be stateless."* And CR-003 §5.2(a) states the purpose: *"how
/// population converts empty land into settled, worked land, and therefore how the
/// frontier eventually closes and Malthusian pressure legitimately emerges"* — the world
/// is ~1.2 % settled and, until this packet, a growing population had no way to take the
/// other 98.8 %.
///
/// WHY A SEPARATE SYSTEM AND NOT A BRANCH OF MIGRATION. A frontier site has no
/// SettlementDistances row, no deficit and no smoothed attractiveness, so a "virtual
/// destination" inside MigrationSystem's pair loop would have to SYNTHESISE damping,
/// viability and gap — three invented quantities inside the equation T2.8/T2.13
/// stabilised by director ruling. Running afterwards instead costs nothing and invents
/// nothing.
///
/// WHY IT READS **LIVE** BUCKETS AND NOT PREV. MigrationSystem's overdraw scaler already
/// caps each bucket's total outflow at its PREV count. A later system reading PREV would
/// DOUBLE-SPEND: it would offer people migration has already moved. Reading the live,
/// post-migration counts cannot — whoever left is gone — so this system draws only from
/// the people migration left behind, which is exactly D-037 B1's population.
///
/// THE TRIGGER IS MIGRATION'S OWN UNPLACED DEMAND — D-037 B1's condition, verbatim.
/// B1 reads: *"Migration currently runs settlement-to-settlement, and ADR-012 rules that
/// with no viable destination people die at home. EXTEND IT: groups may depart into
/// UNCLAIMED land and found new settlements."* The thing being extended is MIGRATION, and
/// the only condition B1 names is ADR-012's NO VIABLE DESTINATION. So this system founds
/// exactly when `BucketRow.UnplacedDeparture` is non-zero — the departure demand
/// MigrationSystem wrote because the source had no reachable, viable destination at all.
///
/// WHAT THIS REPLACED, AND WHY. The first T4.4 implementation triggered on the source's
/// PREV `ConsumptionDeficitRow.DeficitRatio > 0`, borrowed from T4.5/D-037 B3 — whose
/// subject is RAIDING, not colonization. B1 never mentions consumption deficit. That
/// trigger fragmented the population instead of settling the frontier (measured: 12 -> 178
/// settlements by turn 77 while total population FELL 4330 -> 3192; at turn 40, 7 of 7
/// settlements in deficit were ones this system had itself founded). It failed for two
/// structural reasons, both fixed here:
///   1. A DEFICIT RATIO IS SCALE-FREE. Removing `deficit x count` people removes their
///      demand too, so the ratio is essentially unchanged next turn and the same
///      settlement colonises again, every turn, forever. Emigration cannot clear a ratio.
///      A COUNT can be discharged: placing the party consumes the demand exactly.
///   2. `ConsumptionDeficitRow` CANNOT DISTINGUISH "cannot feed the people it has" from
///      "was founded three turns ago and is not producing yet". Both read as deficit.
///
/// THE CASCADE BRAKE, which is the whole point and is a MECHANISM, not a rule. A
/// settlement founded here receives provisions, so `store > 0` and ADR-012's own absolute
/// food gate makes it a VIABLE DESTINATION. Its founder therefore has a viable neighbour
/// next turn, `UnplacedDeparture` goes to zero, and the founder stops founding. The
/// newborn likewise sees its parent and does not found either. The two triggers have
/// OPPOSITE SIGNS on exactly the settlement that broke the first implementation: the old
/// one made a newborn EMIT a founding; this one makes a newborn ABSORB one. There is no
/// cooldown, no age threshold, no newborn immunity and no timer anywhere in this file.
///
/// THE PARTY SIZE IS THE DEMAND. Whole people floored out of `UnplacedDeparture`, with the
/// sub-person fraction banked in `UnplacedRemainder` exactly as migration banks its own
/// (D-004). No ratio, no rate, no population read, no constant, no RNG.
///
/// CONSERVATION (law 1). Every person and every grain moves by `Ledger.Transfer`, which
/// conserves by construction. NOTHING is sourced: no `InitialEndowment`, no minting. The
/// colonists carry a per-capita share of their home store as provisions — real food out of
/// a real granary, which is also T4.4's *clearing cost* in its minimal tree-native form.
/// Deposits are doubles, not stocks, so describing the ground the colonists walked onto
/// creates nothing (see `WorldFounding.AddDepositsForSite`).
///
/// THE ADR-012 HAZARD, and the one non-obvious line in this file. A new settlement has a
/// tiny population and a full catchment, so its instantaneous per-capita attractiveness
/// `A = R/P` is ENORMOUS — structurally the same profile as the food-less ruin that caused
/// the resurrection cycle (*"1,520 arrivals against 884 same-turn deaths in one turn"*).
/// Worse, the EMA does not damp it: migration's contract says *"a settlement's first
/// sighting initializes S = A (the filter starts converged)"*, so an unseeded frontier
/// settlement would arrive pre-converged on its own inflated signal and become an instant
/// world magnet. So this system SEEDS the new settlement's smoothed attractiveness with
/// the FOUNDING SOURCE's current smoothed value — the founders' expectation of the world
/// is the place they left. No new constant, no change to ADR-012, and the EMA then
/// converges to the truth over its normal window instead of in one turn.
///
/// WHAT THIS SYSTEM DELIBERATELY DOES NOT CREATE: catchment (appending a settlement makes
/// `CatchmentSystem.IsStale` true by count mismatch, so it recomputes next turn), housing
/// (`HousingSystem` materialises a missing row at ZERO — colonists start homeless and
/// build), prices, sector allocations, path progress, and every per-turn chronicle row.
/// CONTROL IS INHERITED FROM THE PARENT (director ruling, M4 completion §10). A colony
/// founded from a settlement Polity P controls is itself controlled by P, recorded as
/// one more `ControlRow` — the shape `WorldFounding`'s own contract always described.
/// The rule is stated as a CONDITIONAL on the parent, not as "colonies belong to the
/// player", and that is what makes it more than bookkeeping:
///
///   * a controlled parent yields a controlled colony — the Empire grows by settling,
///     which is what D-042 §2.2's "may acquire further settlements" means in practice;
///   * a STATELESS parent yields a STATELESS colony, because there is no controller to
///     inherit. D-037 B1's stateless founding survives as the propagating case rather
///     than as a special case, and it is the pathway by which uncontrolled settlements
///     continue to exist at all once one appears.
///
/// The control row is written only on a SUCCESSFUL founding, inside `Found`, after the
/// settlement row exists. `Found` returns false and writes nothing when the party would
/// be empty, so a failed founding creates neither settlement nor control row — no
/// orphan row can be left behind pointing at a settlement that was never created.
/// </summary>
public sealed class ColonizationSystem(SimConfig cfg, WorldgenConfig worldgen) : ISimSystem<ColonizationTables>
{
    public static readonly SystemId WellKnownId = new(18);
    public const string Name = "colonization";

    private readonly SimConfig _cfg = cfg;
    private readonly WorldgenConfig _worldgen = worldgen;
    private readonly GoodId _grain = new(cfg.Goods?.GrainId
        ?? throw new ArgumentException("ColonizationSystem requires SimConfig.Goods (goods.json).", nameof(cfg)));

    // Pure function of STATIC terrain (ADR-008), so built once and reused. The
    // PathBuildSystem precedent for a lazily-built lattice inside a system.
    private SettlementSiting.FrontierSiting? _frontier;

    /// <summary>Per-bucket party sizes for the settlement being founded, indexed by
    /// bucket row. Allocated once and reused (T4.13 F5 precedent: no per-settlement
    /// allocation inside the step loop).</summary>
    private long[] _party = [];

    public SystemId Id => WellKnownId;

    public void Step(SimContext<ColonizationTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        TerrainSet? terrain = prev.Terrain;
        if (terrain is null) return;                 // toy/hand-built worlds have no terrain
        if (_cfg.Goods is null) return;

        Table<SettlementRow> settlements = ctx.Owned.Settlements;
        int existing = settlements.Count;
        if (existing == 0) return;

        _frontier ??= SettlementSiting.PrepareFrontier(
            terrain, _worldgen.Siting, _cfg.Transport.RiverCostFactor);

        // Site cells of every settlement standing, ascending settlement order
        // (law 5: an array scan, never a dictionary walk).
        var occupied = new int[existing];
        for (int i = 0; i < existing; i++) occupied[i] = settlements[i].SiteCell;

        // The spacing exclusion, seeded from those sites. This is the ONE thing
        // turn-zero siting cannot do, and the whole of the difference.
        double[] spacing = SettlementSiting.SeedSpacing(_frontier, occupied);

        // Settlement-major ascending: the iteration order IS the resolution order
        // for simultaneous foundings, and each acceptance immediately grows the
        // exclusion field so a later founder this turn cannot crowd an earlier one.
        int settlementCountAtStart = existing;
        for (int s = 0; s < settlementCountAtStart; s++)
        {
            SettlementId source = settlements[s].Id;

            // THE TRIGGER (D-037 B1). Size the party from the departure demand
            // MigrationSystem could not place, banking the sub-person fraction
            // exactly as migration banks its own (D-004). This runs for EVERY
            // source every turn, whether or not a founding follows, so a demand
            // under one person accumulates instead of flooring to zero forever.
            long partyTotal = DrawParty(ctx.Owned.Buckets, source);
            if (partyTotal <= 0) continue;           // nobody was left unplaced

            // THE CLEARING COST IS BINDING: an expedition must be OUTFITTED.
            // This is the provisions share TransferProvisions already computes,
            // REQUIRED to be satisfiable rather than silently clamped to nothing.
            // Two things depend on it, and the second is the whole mechanism:
            //   1. A settlement with an empty granary cannot send colonists into
            //      wilderness. It has nothing to send them with.
            //   2. THE CASCADE BRAKE ONLY EXISTS IF THE DAUGHTER HAS FOOD. The
            //      brake is that a daughter holding provisions satisfies ADR-012's
            //      absolute food gate and so becomes a VIABLE DESTINATION, which
            //      zeroes its founder's demand. A daughter founded with zero
            //      provisions is non-viable, brakes nothing, and the founder keeps
            //      founding — measured on 4d11c02 in the FirstReign world, whose
            //      source granary was empty from turn 5: 16 consecutive foundings.
            // No new constant: the amount is the existing per-capita share, and the
            // only new thing asserted is that it is greater than zero.
            if (ProvisionsFor(ctx.Owned.Stocks, ctx.Owned.Buckets, source, partyTotal) <= 0) continue;

            int site = SettlementSiting.ChooseFrontierSite(
                _frontier, terrain, spacing, occupied, _worldgen.Siting.ScoreJitter, prev.Seed);
            // Frontier full — an ordinary outcome. The drawn whole units are NOT
            // banked forward: nobody moved, so nobody is owed a move. Only the
            // sub-person fraction carries, which is migration's own discipline.
            if (site < 0) continue;

            if (!Found(ctx, prev, source, partyTotal, site, terrain)) continue;

            // Accepted: the new site now excludes its own neighbourhood, and is
            // itself occupied, for every later founder in THIS turn.
            SettlementSiting.AcceptFrontierSite(_frontier, spacing, site);
            var grown = new int[occupied.Length + 1];
            Array.Copy(occupied, grown, occupied.Length);
            grown[^1] = site;
            occupied = grown;
        }
    }

    /// <summary>
    /// Create the settlement and move the party into it. Returns false and writes
    /// NOTHING when the party would be empty — a settlement of nobody is not a
    /// settlement, and creating one would leave an inert row forever.
    /// </summary>
    private bool Found(
        SimContext<ColonizationTables> ctx, IReadOnlyWorldState prev,
        SettlementId source, long partyTotal, int siteCell, TerrainSet terrain)
    {
        Table<BucketRow> buckets = ctx.Owned.Buckets;
        Table<SettlementRow> settlements = ctx.Owned.Settlements;

        // --- 2. allocate the id: dense and ascending -------------------------
        // MigrationSystem indexes `new int[maxId + 1]`, so ids must stay small
        // and non-negative. max+1 keeps them dense and makes the pick order the
        // id order, which is also the deterministic resolution order.
        int maxId = -1;
        for (int i = 0; i < settlements.Count; i++)
            if (settlements[i].Id.Value > maxId) maxId = settlements[i].Id.Value;
        var newId = new SettlementId(maxId + 1);

        // --- 3. Class-A state, created BEFORE any population arrives ---------
        // The invariant is that a settlement is never partially visible: every
        // row a downstream system would look for exists before it holds anyone.
        settlements.Add(new SettlementRow(newId, siteCell, prev.Clock.Turn + 1));

        // --- 3a. CONTROL, inherited from the parent (M4 completion §10) -------
        // Read from PREV, like every other signal in this system: the controller
        // of the settlement the colonists left. Absence is meaningful and is
        // propagated rather than defaulted — a stateless parent founds a
        // stateless colony, which is D-037 B1's case and the only way an
        // uncontrolled settlement can persist in a live world.
        //
        // Strength 1.0 matches founding's own uncontested value; T4.3 owns the
        // field and no system computes or decays it yet.
        if (EmpireQuery.TryGetController(prev, source, out PolityId parent))
        {
            ctx.Owned.Controls.Add(new ControlRow(parent, newId, 1.0));
        }

        Table<GoodStockRow> stocks = ctx.Owned.Stocks;
        foreach (GoodEntry g in _cfg.Goods!.Goods)
            stocks.Add(new GoodStockRow(newId, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));

        WorldFounding.AddDepositsForSite(
            ctx.Owned.Deposits, terrain, _worldgen, _cfg, _cfg.Goods,
            newId, siteCell, prev.Seed, newId.Value);

        Table<ClassStateRow> classStates = ctx.Owned.ClassStates;
        Table<GrievanceRow> grievances = ctx.Owned.Grievances;
        for (int c = 0; c < _cfg.Registries!.Classes.Length; c++)
        {
            var cls = new ClassId(_cfg.Registries.Classes[c].Id);
            // The base class is active on arrival; specialists must emerge, exactly
            // as at founding — a frontier hamlet has no artisans on day one.
            classStates.Add(new ClassStateRow(newId, cls, c == 0 ? 1 : 0));
            grievances.Add(new GrievanceRow(newId, cls, 0.0));
        }

        // --- 4. the people: Ledger.Transfer, key for key ---------------------
        // Buckets are appended while we iterate the source's rows, so the source
        // row indices are captured first — and the destination layout mirrors the
        // source's ascending key order, which is what MigrationSystem's positional
        // shortcut expects of any settlement.
        int bucketCountAtStart = buckets.Count;
        for (int i = 0; i < bucketCountAtStart; i++)
        {
            BucketRow b = buckets[i];
            if (b.Settlement != source) continue;
            int dst = buckets.Add(new BucketRow(
                newId, b.Culture, b.Religion, b.Class, b.CohortIdx,
                Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            long take = _party[i];
            if (take <= 0) continue;
            ctx.Ledger.Transfer(
                ref buckets.Ref(i).Count, ref buckets.Ref(dst).Count,
                take, OverdrawPolicy.ClampToAvailable);
        }

        // --- 5. provisions: the colonists' per-capita share of the home store -
        // A TRANSFER, never a source. This is T4.4's clearing cost in its minimal
        // tree-native form: leaving costs the parent settlement real food.
        TransferProvisions(ctx, source, newId, partyTotal);

        // --- 6. the ADR-012 guard -------------------------------------------
        SeedAttractiveness(ctx, prev, source, newId);
        return true;
    }

    /// <summary>
    /// Convert this source's UNPLACED DEPARTURE DEMAND into whole people, per bucket,
    /// and return the total. The per-bucket counts are left in <see cref="_party"/>
    /// indexed by bucket row so the transfer loop draws exactly what was measured —
    /// measure once, move once.
    ///
    /// NOTHING IS DERIVED HERE. The demand was written by MigrationSystem; this
    /// method only floors it into people and banks the sub-person fraction in the
    /// D-004 accumulator, the same two operations migration performs on its own
    /// outflow. There is no deficit read, no population read, no ratio, no rate and
    /// no constant on this path.
    ///
    /// The demand is consumed (zeroed) as it is drawn, so one turn's demand can
    /// found at most one settlement even if this method were called twice.
    /// </summary>
    private long DrawParty(Table<BucketRow> buckets, SettlementId source)
    {
        if (_party.Length < buckets.Count) _party = new long[buckets.Count];
        long total = 0;
        for (int i = 0; i < buckets.Count; i++)
        {
            _party[i] = 0;
            if (buckets[i].Settlement != source) continue;

            ref BucketRow b = ref buckets.Ref(i);
            double exact = b.UnplacedDeparture + b.UnplacedRemainder;
            long take = ConservedMath.WholeUnits(exact, $"colonization party (bucket {i})");

            // THE BANK IS THE SUB-PERSON FRACTION AND NOTHING ELSE. Taken before
            // the availability clamp on purpose: banking `exact - take` AFTER the
            // clamp would carry whole people forward, so a bucket whose desire far
            // exceeds its population would accumulate hundreds of person-units of
            // unmet desire and discharge them as a huge party later. Measured on
            // 4d11c02: a source with desire 500 and 25 people banked 475. Desire
            // that the population cannot satisfy is DROPPED — nobody moved, so
            // nobody is owed a move, which is migration's own discipline.
            b.UnplacedRemainder = exact - take;   // < 1 by construction of floor()
            b.UnplacedDeparture = 0.0;            // consumed

            // Never promise more people than the bucket still holds after migration
            // took its share: the live count is the ceiling (the ClampToAvailable
            // backstop would otherwise silently shrink a party already counted).
            long live = b.Count.Value;
            if (take > live) take = live < 0 ? 0 : live;
            _party[i] = take;
            total += take;
        }
        return total;
    }

    /// <summary>
    /// The colonists' per-capita share of the home granary, in whole grain. ONE
    /// arithmetic, called twice: once as the founding precondition (before any row
    /// exists) and once to perform the transfer. Sharing it is what makes "an
    /// expedition must be outfitted" and "this is what it leaves with" the same
    /// statement rather than two that can drift apart.
    ///
    /// <paramref name="partyHasLeft"/> says whether the party is still counted in
    /// the source's buckets, because the population the share is OF is always the
    /// source PLUS the party.
    /// </summary>
    private long ProvisionsFor(
        Table<GoodStockRow> stocks, Table<BucketRow> buckets,
        SettlementId source, long party, bool partyHasLeft = false)
    {
        long sourcePop = 0;
        for (int i = 0; i < buckets.Count; i++)
            if (buckets[i].Settlement == source) sourcePop += buckets[i].Count.Value;
        long before = partyHasLeft ? sourcePop + party : sourcePop;
        if (before <= 0 || party <= 0) return 0;

        int from = GoodStockIndex.IndexOf(stocks, source, _grain);
        if (from < 0) return 0;
        long store = stocks[from].Amount.Value;
        if (store <= 0) return 0;
        return (long)Math.Floor(store * (party / (double)before));
    }

    private void TransferProvisions(
        SimContext<ColonizationTables> ctx, SettlementId source, SettlementId dest, long party)
    {
        Table<GoodStockRow> stocks = ctx.Owned.Stocks;
        long provisions = ProvisionsFor(stocks, ctx.Owned.Buckets, source, party, partyHasLeft: true);
        if (provisions <= 0) return;

        int from = GoodStockIndex.IndexOf(stocks, source, _grain);
        int to = GoodStockIndex.IndexOf(stocks, dest, _grain);
        if (from < 0 || to < 0) return;

        ctx.Ledger.Transfer(
            ref stocks.Ref(from).Amount, ref stocks.Ref(to).Amount,
            provisions, OverdrawPolicy.ClampToAvailable);
    }

    /// <summary>
    /// ADR-012: seed the new settlement's EMA from its SOURCE rather than letting
    /// migration initialise it from the new settlement's own first observation,
    /// which would be a tiny population against a full catchment — the exact
    /// magnet profile the resurrection cycle was made of.
    /// </summary>
    private static void SeedAttractiveness(
        SimContext<ColonizationTables> ctx, IReadOnlyWorldState prev,
        SettlementId source, SettlementId dest)
    {
        Table<SmoothedAttractivenessRow> smoothed = ctx.Owned.Smoothed;
        for (int i = 0; i < smoothed.Count; i++)
            if (smoothed[i].Settlement == dest) return;   // already seeded

        double seed = 0.0;
        for (int i = 0; i < prev.SmoothedAttractiveness.Count; i++)
            if (prev.SmoothedAttractiveness[i].Settlement == source)
            { seed = prev.SmoothedAttractiveness[i].Value; break; }

        smoothed.Add(new SmoothedAttractivenessRow(dest, seed));
    }
}
