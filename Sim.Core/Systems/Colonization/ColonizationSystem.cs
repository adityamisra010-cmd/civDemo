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
    Table<SmoothedAttractivenessRow> Smoothed);

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
/// THE TRIGGER IS THE ONE ALREADY IN THE TREE. A settlement colonises when its PREV
/// `ConsumptionDeficitRow.DeficitRatio > 0` — it cannot feed the people it has. This is
/// the SAME trigger and the SAME row T4.5's AppropriationSystem uses for D-037 B3, whose
/// language is the sibling of B1's: subsistence failing is what makes people move. NO NEW
/// CONSTANT, no threshold to tune, no timer, and no RNG: a settlement that can feed itself
/// colonises nothing.
///
/// THE PARTY SIZE IS DERIVED THE SAME WAY. Per bucket, `WholeUnits(deficitRatio × count)`
/// — the fraction of its people the settlement cannot feed. A settlement 5 % short sends
/// 5 %; one that is fed sends nobody. Again no constant, and self-limiting by
/// construction. Buckets are drawn key-for-key so migrants keep their full identity, which
/// MigrationSystem requires of any destination (a key mismatch there refunds the move and
/// nobody can ever arrive).
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
/// It writes no claim or control row either, so the settlement is STATELESS exactly as
/// D-037 B1 permits — and, since nothing in the tree writes `Controls`, unavoidably so.
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

            double deficit = DeficitOf(prev, source);
            if (deficit <= 0.0) continue;            // feeds itself: colonises nothing

            int site = SettlementSiting.ChooseFrontierSite(
                _frontier, terrain, spacing, occupied, _worldgen.Siting.ScoreJitter, prev.Seed);
            if (site < 0) continue;                  // frontier full — an ordinary outcome

            if (!Found(ctx, prev, source, deficit, site, terrain)) continue;

            // Accepted: the new site now excludes its own neighbourhood, and is
            // itself occupied, for every later founder in THIS turn.
            SettlementSiting.AcceptFrontierSite(_frontier, spacing, site);
            var grown = new int[occupied.Length + 1];
            Array.Copy(occupied, grown, occupied.Length);
            grown[^1] = site;
            occupied = grown;
        }
    }

    /// <summary>PREV consumption deficit — the same row and the same read T4.5 uses.</summary>
    private static double DeficitOf(IReadOnlyWorldState prev, SettlementId settlement)
    {
        for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
            if (prev.ConsumptionDeficits[i].Settlement == settlement)
                return prev.ConsumptionDeficits[i].DeficitRatio;
        return 0.0;
    }

    /// <summary>
    /// Create the settlement and move the party into it. Returns false and writes
    /// NOTHING when the party would be empty — a settlement of nobody is not a
    /// settlement, and creating one would leave an inert row forever.
    /// </summary>
    private bool Found(
        SimContext<ColonizationTables> ctx, IReadOnlyWorldState prev,
        SettlementId source, double deficit, int siteCell, TerrainSet terrain)
    {
        Table<BucketRow> buckets = ctx.Owned.Buckets;
        Table<SettlementRow> settlements = ctx.Owned.Settlements;

        // --- 1. size the party from LIVE post-migration counts, key by key ----
        // Two passes: measure first, so an empty party costs no rows.
        long partyTotal = 0;
        for (int i = 0; i < buckets.Count; i++)
        {
            if (buckets[i].Settlement != source) continue;
            partyTotal += PartyFrom(buckets[i].Count.Value, deficit);
        }
        if (partyTotal <= 0) return false;

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
            long take = PartyFrom(buckets[i].Count.Value, deficit);
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

    /// <summary>The share of a bucket the settlement cannot feed. No constant.</summary>
    private static long PartyFrom(long count, double deficit)
    {
        if (count <= 0) return 0;
        double exact = count * deficit;
        long take = (long)Math.Floor(exact);
        return take > count ? count : take;
    }

    private void TransferProvisions(
        SimContext<ColonizationTables> ctx, SettlementId source, SettlementId dest, long party)
    {
        Table<GoodStockRow> stocks = ctx.Owned.Stocks;
        Table<BucketRow> buckets = ctx.Owned.Buckets;

        long sourcePop = 0;
        for (int i = 0; i < buckets.Count; i++)
            if (buckets[i].Settlement == source) sourcePop += buckets[i].Count.Value;
        // The party has already left the source's buckets, so the population it
        // is a share OF is the source plus the party.
        long before = sourcePop + party;
        if (before <= 0) return;

        int from = GoodStockIndex.IndexOf(stocks, source, _grain);
        int to = GoodStockIndex.IndexOf(stocks, dest, _grain);
        if (from < 0 || to < 0) return;

        long store = stocks[from].Amount.Value;
        if (store <= 0) return;                       // refugees leave with nothing
        long provisions = (long)Math.Floor(store * (party / (double)before));
        if (provisions <= 0) return;

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
