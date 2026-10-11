using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Core.State;

/// <summary>Why a Found Settlement order is, or is not, LEGAL (ADR-036). The first failing arm is reported, in the
/// order the arms are listed; <see cref="Legal"/> means every arm holds.</summary>
public enum FoundingVerdict
{
    Legal = 0,
    /// <summary>No settlement with the source id stands in the world the order is judged against.</summary>
    UnknownSource = 1,
    /// <summary>The issuer does not control the source (D-051 §5: "the player picks one of their settlements").</summary>
    NotControlled = 2,
    /// <summary>The site is not a cell of the map (or the world has no map at all).</summary>
    OffMap = 3,
    /// <summary>The site is water (water ≥ 0.5) — SettlementSiting.IsFrontierCandidate's first rule.</summary>
    Water = 4,
    /// <summary>The site is land but its UNJITTERED siting score is ≤ 0 or below the score floor — the rest of
    /// SettlementSiting.IsFrontierCandidate.</summary>
    PoorSite = 5,
    /// <summary>The site is a standing settlement's exact site cell (SettlementSiting.IsOccupied).</summary>
    Occupied = 6,
    /// <summary>The site's origin lattice node is inside a standing settlement's ADR-018 travel-cost spacing
    /// exclusion — the rest of SettlementSiting.IsUnobstructedFrontierCell.</summary>
    TooClose = 7,
    /// <summary>ADR-036 "unclaimed": the site's origin lattice node lies in a settlement's catchment
    /// (a CatchmentNodes row exists for it).</summary>
    Claimed = 8,
    /// <summary>"Enough people": the party would be empty — the content fraction of the source's population
    /// floors to nobody, or every person it would take is the last one in their bucket.</summary>
    NoParty = 9,
    /// <summary>"Enough food": the party's per-capita share of the source's own grain store floors to 0.</summary>
    NoProvisions = 10,
    /// <summary>"Reachable": no land route exists from the source to the site on the road-aware lattice
    /// (Pathfinder.FindPath finds none).</summary>
    Unreachable = 11,
}

/// <summary>
/// ADR-036 (PROPOSED) — THE FOUND SETTLEMENT PREDICATE (D-051 §5). Pure, read-only, no state, no cache: the
/// ConstructionQuery precedent of ONE predicate with several callers. <b>ColonizationSystem accepts a
/// FoundSettlement order iff <see cref="Check(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>
/// answers <see cref="FoundingVerdict.Legal"/> on PREV</b> (and the party can still depart — see DEPARTURE IS LIVE
/// below), and the later callers — the UI's placement mode (S1d) and the
/// AI founding policy (S1c) — ask the same question of the world they see, so an order the UI offers is an order
/// the simulation founds and an order nobody could have been offered is refused on the same rule.
///
/// THE ARMS, in the order they are tested (cheap first, the route search last):
/// <list type="number">
/// <item>the source stands in the judged world (PREV at delivery) and the issuer controls it (EmpireQuery);</item>
/// <item>the site is a map cell, land, its unjittered siting score is &gt; 0 and not below the floor —
///   <see cref="SettlementSiting.IsFrontierCandidate"/>, the turn-zero and emergent rule, unchanged;</item>
/// <item>the site is not a standing settlement's cell and is outside every spacing exclusion —
///   <see cref="SettlementSiting.IsUnobstructedFrontierCell"/>, unchanged;</item>
/// <item>UNCLAIMED: the site's origin lattice node is in no settlement's catchment (no CatchmentNodes row).
///   D-051 does not define the word and no land-level claim state exists (ClaimRow has no writer); the catchment
///   partition is the only land assignment the simulation holds. ADR-036 §2 asks the Director to confirm it;</item>
/// <item>ENOUGH PEOPLE: <see cref="AllocateParty"/> forms a party of at least one, with every non-empty source
///   bucket keeping at least one person;</item>
/// <item>ENOUGH FOOD: <see cref="ProvisionsShare"/> — the existing B1 per-capita share of the source's OWN grain,
///   the arithmetic the transfer then moves — is at least 1 grain (D-051 §6's central stockpile is deferred);</item>
/// <item>REACHABLE: <see cref="Pathfinder.FindPath"/> on the frontier lattice (the raw traversal lattice
///   CatchmentSystem also builds) plus the judged world's network overlay finds a route from the source's origin
///   node to the site's — binary connectivity, no travel budget, so no new constant.</item>
/// </list>
/// THE KNOWN-EXTENT ARM (D-040 B1: "settlement operates only within known extent") has no state to read: no
/// discovered-extent table exists until S2a. Every cell is known today; the arm belongs between 2 and 3 when the
/// state lands.
///
/// WHICH WORLD. Every arm is judged on the world passed as <c>world</c> — at delivery that is PREV, the state of
/// the turn the order was stamped in, which is exactly the world the issuer (UI or AI) judged it on. That is the
/// precedent of every consumer (ConstructionQuery.IsProjectAvailable, Governance.CanLevyTax and AgeQuery are all
/// applied on PREV) and it is what makes "offered" and "accepted" the same answer. The single exception is
/// occupancy and spacing: the explicit overload takes them as arrays, because a Found Settlement order earlier in
/// the SAME batch may already have founded next door (the system passes its grown arrays; the convenience overload
/// derives them from the world's settlements).
///
/// DEPARTURE IS LIVE. The people and the grain that actually leave are those the source holds when the party
/// departs — mid-step, after this step's harvest, meals and migration — so <see cref="Departure"/> applies the
/// SAME people and food arms (<see cref="AllocateParty"/>, <see cref="ProvisionsShare"/>) to the live tables and
/// sizes the party there. An order legal on PREV founds unless the step itself has, by the moment of departure,
/// reduced every source bucket to its last person or emptied the granary's per-capita share to nothing: then it
/// founds nothing (D-051 §5's "enough people and food" holds when people and grain move, not only when ordered).
/// </summary>
public static class FoundingQuery
{
    /// <summary>THE FoundSettlement order, stamped for the turn being played from <paramref name="world"/>:
    /// delivered on the step from that turn, founding in the state of the next (the stamping every other
    /// order constructor uses — ConstructionQuery.EnqueueOrder).</summary>
    public static OrderRecord FoundOrder(IReadOnlyWorldState world, PolityId issuer, SettlementId source, int siteCell) =>
        OrderRecord.From(world.Clock.Turn, issuer, OrderKind.FoundSettlement, source.Value, siteCell);

    /// <summary>The site cell an order carries, or −1 when its Amount is not a whole number in [0, int.MaxValue]
    /// (load validation already refuses such a log; a hand-built batch reads as <see cref="FoundingVerdict.OffMap"/>).</summary>
    public static int SiteCellOf(in OrderRecord order)
    {
        double a = order.Amount;
        if (!(a >= 0.0) || a != Math.Floor(a) || a > int.MaxValue) return -1;
        return (int)a;
    }

    /// <summary>The terrain-invariant half of siting for <paramref name="world"/>'s map — the same
    /// <see cref="SettlementSiting.PrepareFrontier"/> call ColonizationSystem makes (siting config from worldgen,
    /// river cost factor from sim.json transport). Null for a world without terrain.</summary>
    public static SettlementSiting.FrontierSiting? Frontier(IReadOnlyWorldState world, SimConfig cfg, WorldgenConfig worldgen) =>
        world.Terrain is { } terrain
            ? SettlementSiting.PrepareFrontier(terrain, worldgen.Siting, cfg.Transport.RiverCostFactor)
            : null;

    /// <summary>The predicate's answer for a caller holding only a world (the UI, the AI): occupancy and spacing
    /// are those of every settlement standing in <paramref name="world"/>.</summary>
    public static FoundingVerdict Check(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting frontier,
        PolityId issuer, SettlementId source, int siteCell)
    {
        FoundingVerdict head = SourceAndSite(world, frontier, issuer, source, siteCell, out int sourceCell);
        if (head != FoundingVerdict.Legal) return head;
        var occupied = new int[world.Settlements.Count];
        for (int i = 0; i < occupied.Length; i++) occupied[i] = world.Settlements[i].SiteCell;
        double[] spacing = SettlementSiting.SeedSpacing(frontier, occupied);
        return Rest(world, cfg, frontier, spacing, occupied, source, sourceCell, siteCell);
    }

    /// <summary>THE predicate (see the header) with the standing sites explicit: <paramref name="world"/> supplies
    /// the source, control, the catchment, the network and the people and grain; <paramref name="spacing"/> /
    /// <paramref name="occupied"/> the settlements standing (SettlementSiting.SeedSpacing, grown by
    /// AcceptFrontierSite for each founding already made this step).</summary>
    public static FoundingVerdict Check(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting frontier,
        ReadOnlySpan<double> spacing, ReadOnlySpan<int> occupied,
        PolityId issuer, SettlementId source, int siteCell)
    {
        FoundingVerdict head = SourceAndSite(world, frontier, issuer, source, siteCell, out int sourceCell);
        if (head != FoundingVerdict.Legal) return head;
        return Rest(world, cfg, frontier, spacing, occupied, source, sourceCell, siteCell);
    }

    /// <summary>
    /// The PEOPLE and FOOD arms on the given tables — <see cref="FoundingVerdict.NoParty"/>,
    /// <see cref="FoundingVerdict.NoProvisions"/> or <see cref="FoundingVerdict.Legal"/> — with the party size in
    /// <paramref name="party"/> and, when <paramref name="perBucket"/> is given, its allocation by bucket row.
    /// <see cref="Check(IReadOnlyWorldState, SimConfig, SettlementSiting.FrontierSiting, ReadOnlySpan{double}, ReadOnlySpan{int}, PolityId, SettlementId, int)"/>
    /// calls it on the judged world's tables; ColonizationSystem calls it again on its LIVE tables at departure.
    /// </summary>
    public static FoundingVerdict Departure(
        IReadOnlyTable<BucketRow> buckets, IReadOnlyTable<GoodStockRow> stocks, SimConfig cfg,
        SettlementId source, long[]? perBucket, out long party)
    {
        party = AllocateParty(buckets, source, PartyFraction(cfg), perBucket);
        if (party < 1) return FoundingVerdict.NoParty;
        if (cfg.Goods is not { } goods
            || ProvisionsShare(stocks, buckets, source, new GoodId(goods.GrainId), party) < 1)
            return FoundingVerdict.NoProvisions;
        return FoundingVerdict.Legal;
    }

    /// <summary>Arms 1–2: the source and the static site rules.</summary>
    private static FoundingVerdict SourceAndSite(
        IReadOnlyWorldState world, SettlementSiting.FrontierSiting frontier,
        PolityId issuer, SettlementId source, int siteCell, out int sourceCell)
    {
        sourceCell = -1;
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id == source) { sourceCell = world.Settlements[i].SiteCell; break; }
        if (sourceCell < 0) return FoundingVerdict.UnknownSource;
        if (!EmpireQuery.ControlsSettlement(world, issuer, source)) return FoundingVerdict.NotControlled;

        if (world.Terrain is not { } terrain || frontier.Size != terrain.Size) return FoundingVerdict.OffMap;
        if ((uint)siteCell >= (uint)frontier.Scores.Length) return FoundingVerdict.OffMap;
        if (!SettlementSiting.IsFrontierCandidate(frontier, terrain, siteCell))
            return terrain.Water[siteCell] >= 0.5 ? FoundingVerdict.Water : FoundingVerdict.PoorSite;
        // D-040 B1 known-extent arm: inert until a discovered-extent state exists (S2a). Every cell is known.
        return FoundingVerdict.Legal;
    }

    /// <summary>Arms 3–7: obstruction, claim, people, food, route.</summary>
    private static FoundingVerdict Rest(
        IReadOnlyWorldState world, SimConfig cfg, SettlementSiting.FrontierSiting frontier,
        ReadOnlySpan<double> spacing, ReadOnlySpan<int> occupied,
        SettlementId source, int sourceCell, int siteCell)
    {
        if (!SettlementSiting.IsUnobstructedFrontierCell(frontier, spacing, occupied, siteCell))
            return SettlementSiting.IsOccupied(occupied, siteCell) ? FoundingVerdict.Occupied : FoundingVerdict.TooClose;

        int siteNode = LatticeMap.OriginLatticeNode(frontier.Lattice, frontier.Size, siteCell);
        if (IsClaimed(world, siteNode)) return FoundingVerdict.Claimed;

        FoundingVerdict departure = Departure(world.Buckets, world.GoodStocks, cfg, source, null, out _);
        if (departure != FoundingVerdict.Legal) return departure;

        int sourceNode = LatticeMap.OriginLatticeNode(frontier.Lattice, frontier.Size, sourceCell);
        if (!Pathfinder.FindPath(frontier.Lattice, world, sourceNode, siteNode).Found) return FoundingVerdict.Unreachable;
        return FoundingVerdict.Legal;
    }

    /// <summary>ADR-036 "unclaimed", negated: some settlement's catchment holds <paramref name="latticeNode"/>.</summary>
    public static bool IsClaimed(IReadOnlyWorldState world, int latticeNode)
    {
        for (int i = 0; i < world.CatchmentNodes.Count; i++)
            if (world.CatchmentNodes[i].LatticeNode == latticeNode) return true;
        return false;
    }

    /// <summary>The content party fraction (sim.json colonization.orderPartyFraction); 0 when the section is
    /// absent, which forms no party — the order is inert.</summary>
    public static double PartyFraction(SimConfig cfg) => cfg.Colonization?.OrderPartyFraction ?? 0.0;

    /// <summary>
    /// THE PARTY (ADR-036 §3): how many people a Found Settlement order takes from <paramref name="source"/>, and —
    /// when <paramref name="perBucket"/> is given (length ≥ buckets.Count, indexed by bucket ROW) — from which bucket.
    /// Integer arithmetic throughout; no ordering over doubles exists here.
    /// <list type="number">
    /// <item>N = the source's people; C = Σ (cᵢ − 1) over its non-empty buckets — what it can spare while every
    ///   non-empty bucket keeps one person;</item>
    /// <item>the party T = min(⌊fraction × N⌋, C);</item>
    /// <item>PROPORTIONAL FLOOR: bucket i gives ⌊T × cᵢ / N⌋ (exact long division; never more than cᵢ − 1, because
    ///   T &lt; N);</item>
    /// <item>REMAINDER, by BUCKET ROW: the T − Σ floors people still owed are taken one at a time from the source's
    ///   buckets in ascending row order, skipping a bucket down to its last person, cycling until none is owed.
    ///   The rule is a stable integer order — the lowest rows give first — so equal buckets cannot tie.</item>
    /// </list>
    /// Returns T (0 when no party can form). Nothing is written to the tables.
    /// </summary>
    public static long AllocateParty(
        IReadOnlyTable<BucketRow> buckets, SettlementId source, double fraction, long[]? perBucket)
    {
        if (perBucket is not null)
        {
            if (perBucket.Length < buckets.Count)
                throw new ArgumentException("perBucket must cover every bucket row", nameof(perBucket));
            Array.Clear(perBucket, 0, buckets.Count);
        }

        long population = 0, capacity = 0;
        for (int i = 0; i < buckets.Count; i++)
        {
            if (buckets[i].Settlement != source) continue;
            long c = buckets[i].Count.Value;
            if (c <= 0) continue;
            population += c;
            capacity += c - 1;
        }
        if (population <= 0 || !(fraction > 0.0)) return 0;

        long target = (long)Math.Floor(fraction * population);
        if (target > capacity) target = capacity;
        if (target <= 0) return 0;
        if (perBucket is null) return target;   // the total is exact by construction: target ≤ capacity

        long assigned = 0;
        for (int i = 0; i < buckets.Count; i++)
        {
            if (buckets[i].Settlement != source) continue;
            long c = buckets[i].Count.Value;
            if (c <= 0) continue;
            long take = target * c / population;               // floor; < c because target < population
            if (take > c - 1) take = c - 1;
            perBucket[i] = take;
            assigned += take;
        }

        long owed = target - assigned;
        while (owed > 0)
        {
            for (int i = 0; i < buckets.Count && owed > 0; i++)
            {
                if (buckets[i].Settlement != source) continue;
                long c = buckets[i].Count.Value;
                if (c <= 0 || perBucket[i] >= c - 1) continue;
                perBucket[i]++;
                owed--;
            }
        }
        return target;
    }

    /// <summary>
    /// The colonists' per-capita share of the source's grain store, in whole grain: ⌊store × party / before⌋,
    /// where <c>before</c> is the source's population with the party still counted. ONE arithmetic for every
    /// founding: emergent B1 (ColonizationSystem.ProvisionsFor delegates here, unchanged) and the order path, as
    /// both the "enough food" arm (before the party leaves) and the Ledger.Transfer that moves the grain (after,
    /// with <paramref name="partyHasLeft"/> — the population the share is OF is always the source plus the party).
    /// </summary>
    public static long ProvisionsShare(
        IReadOnlyTable<GoodStockRow> stocks, IReadOnlyTable<BucketRow> buckets,
        SettlementId source, GoodId grain, long party, bool partyHasLeft = false)
    {
        long sourcePop = 0;
        for (int i = 0; i < buckets.Count; i++)
            if (buckets[i].Settlement == source) sourcePop += buckets[i].Count.Value;
        long before = partyHasLeft ? sourcePop + party : sourcePop;
        if (before <= 0 || party <= 0) return 0;

        int from = GoodStockIndex.IndexOf(stocks, source, grain);
        if (from < 0) return 0;
        long store = stocks[from].Amount.Value;
        if (store <= 0) return 0;
        return (long)Math.Floor(store * (party / (double)before));
    }
}
