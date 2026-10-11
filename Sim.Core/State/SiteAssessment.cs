using System.Collections.Immutable;
using Sim.Core.Pathing;
using Sim.Core.Systems;
using Sim.Core.Systems.Catchment;
using Sim.Core.Worldgen;

namespace Sim.Core.State;

/// <summary>
/// M5 map workstream P8 — THE SITE ASSESSMENT: what the simulation itself holds about ONE terrain cell, for the
/// player's tile inspection card (InfoKind.Cell). A QUERY, not a system (the InfoQuery / FoundingQuery precedent):
/// pure and read-only, no state written, nothing serialized, consulted by no system, outside the pipeline. Every
/// value is the simulation's OWN function's value, called — never restated:
/// <list type="bullet">
/// <item><b>THE COMPOSITE.</b> The simulation has exactly one composite settlement-suitability value: the UNJITTERED
///   siting score <see cref="SettlementSiting.CandidateScores"/> = region fertility (<see cref="SettlementSiting.BoxSum"/>
///   over ±fertilityRadiusPx) × water access (<see cref="SettlementSiting.WaterAccess"/> of
///   <see cref="SettlementSiting.WaterDistanceBfs"/>). Turn-zero siting, emergent colonization and the AI's founding
///   pick all rank by it, and FoundingQuery's floor arm reads it. The assessment reports it decomposed into exactly
///   those two factors, and its rank among the positive scores against the floor
///   (<see cref="SettlementSiting.ScoreFloor"/>'s population). No weight is invented and nothing else is folded in.</item>
/// <item><b>EVERYTHING ELSE IS SEPARATE</b> and is not part of that score: the terrain fields at the cell; the
///   occupancy, spacing and catchment-claim status; the hinterland means and the EXPECTED deposit per good
///   (<see cref="WorldFounding.HinterlandMeans"/> + <see cref="WorldFounding.DepositChannelValue"/>; a good's seeded spread
///   is shown as a range, because the roll is keyed to the would-be SETTLEMENT ID, not to the cell) and, when the
///   viewer knows every settlement, the exact roll a settlement founded NEXT would get
///   (<see cref="WorldFounding.AddDepositsForSite"/> into a scratch table with id maxId+1 — the call ColonizationSystem
///   makes); the would-be catchment's arable km² (<see cref="Pathfinder.Partition"/> with the site added, the
///   CatchmentSystem recompute's inputs); the nearest known settlements; the route and its cost from a chosen source
///   (<see cref="RouteQuery.Find"/>); each arm of THE founding predicate (<see cref="FoundingQuery"/>) and its own
///   verdict; the party and provisions a founding would take.</item>
/// </list>
///
/// <para><b>INFORMATION CLASS (P7).</b> A cell the viewer's <see cref="PolityKnowledge"/> does not know is REFUSED: the
/// report says so and nothing of the cell, the terrain or the world is read for it. The nearest-settlement list names
/// known settlements only. A KNOWN cell's values are the simulation's own, and several of them read beyond the cell —
/// the score's box and the water-access BFS, the hinterland disc, the floor and rank (every cell), the spacing,
/// occupancy and claim masks and the route (every settlement and the whole lattice): the same channel ADR-037 §6
/// records for the AI's valuation, which S2b/S2c decide. All-known today, so nothing is hidden. The deposit roll for the
/// next settlement id is shown only while the viewer knows every settlement, because the id counts all of them.</para>
///
/// <para><b>DATA GAPS (stated, not filled).</b> There is no per-cell resource quantity: deposits are written per
/// settlement, at founding. The nine deposit goods collapse onto three channels. D-051 §2 (ratified, not built) will
/// replace the deposit function; this query keeps calling that one function and never copies it.</para>
///
/// <para>Deterministic: arrays and table order only, no dictionary, no LINQ; the one ordering (nearest settlements) is
/// the composite INTEGER key (squared pixel distance, settlement id).</para>
/// </summary>
public static class SiteAssessment
{
    /// <summary>The terrain-invariant half of an assessment, a pure function of (terrain, configs): the frontier siting
    /// (scores, floor, lattice, spacing cap) and the two factor rasters. Built once per terrain by the caller (the UI
    /// keeps one per session — the TerrainBaker precedent); the query itself caches nothing.</summary>
    public sealed class Prepared
    {
        /// <summary>The terrain this was prepared from (reference identity: a world's TerrainSet is immutable and shared by
        /// every clone of that world).</summary>
        public required TerrainSet Terrain { get; init; }

        /// <summary>The worldgen config the founding path holds (ColonizationSystem's): siting, and the hinterland disc.</summary>
        public required WorldgenConfig Worldgen { get; init; }

        /// <summary>FoundingQuery.Frontier — SettlementSiting.PrepareFrontier, exactly as ColonizationSystem builds it.</summary>
        public required SettlementSiting.FrontierSiting Frontier { get; init; }

        /// <summary>The region-fertility factor raster: SettlementSiting.BoxSum(fertility, size, fertilityRadiusPx).</summary>
        public required double[] RegionFertility { get; init; }

        /// <summary>The water-access input raster: SettlementSiting.WaterDistanceBfs(water, rivers, size), in px steps.</summary>
        public required int[] WaterDistancePx { get; init; }

        /// <summary>Whether this was prepared for <paramref name="terrain"/>.</summary>
        public bool IsFor(TerrainSet? terrain) => terrain is not null && ReferenceEquals(Terrain, terrain);
    }

    /// <summary>The assessment's terrain half for <paramref name="world"/>'s map, or null for a world without terrain.
    /// <paramref name="worldgen"/> must be the config the world's founding path holds for the deposit preview to be the
    /// founding's own values (ColonizationSystem's; the UI session's <c>_worldgen</c>).</summary>
    public static Prepared? Prepare(IReadOnlyWorldState world, SimConfig cfg, WorldgenConfig worldgen)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(worldgen);
        if (world.Terrain is not { } terrain) return null;
        SettlementSiting.FrontierSiting frontier = FoundingQuery.Frontier(world, cfg, worldgen)!;
        return new Prepared
        {
            Terrain = terrain,
            Worldgen = worldgen,
            Frontier = frontier,
            RegionFertility = SettlementSiting.BoxSum(terrain.Fertility, terrain.Size, worldgen.Siting.FertilityRadiusPx),
            WaterDistancePx = SettlementSiting.WaterDistanceBfs(terrain.Water, terrain.Rivers, terrain.Size),
        };
    }

    /// <summary>One arm of the founding predicate as judged here: <see cref="Holds"/> null when the arm could not be
    /// judged (its source does not stand, or an earlier arm it depends on decides it).</summary>
    public readonly record struct FoundingArm(FoundingVerdict Arm, bool? Holds);

    /// <summary>A settlement near the cell: its id and the straight-line distance (squared terrain px, exact; and km).</summary>
    public readonly record struct Nearby(SettlementId Id, long DistanceSquaredPx, double Km);

    /// <summary>
    /// One deposit good a settlement founded here would get. <see cref="Channel"/> is goods.json's depositChannel and
    /// <see cref="ChannelValue"/> its value at this site (WorldFounding.DepositChannelValue of the hinterland means) — the
    /// EXPECTED abundance (the seeded spread's centre). <see cref="Low"/>..<see cref="High"/> is the spread's range,
    /// max(0, value × (1 ∓ spread)), the high end exclusive; equal to the value when the good has no spread.
    /// <see cref="IfFoundedNext"/> is the exact abundance a settlement founded next would be written (null when not
    /// shown). <see cref="Herded"/>: worked by Herding/fishing (LabourActivities.IsHerded), else by Extraction.
    /// </summary>
    public readonly record struct DepositPreview(
        GoodId Good, string Channel, double ChannelValue, double Spread, double Low, double High, double? IfFoundedNext, bool Herded);

    /// <summary>The assessment of one cell. When <see cref="Known"/> is false the cell was REFUSED and every other value
    /// is its default — nothing was read.</summary>
    public sealed class Report
    {
        public required int Cell { get; init; }
        public required int X { get; init; }
        public required int Y { get; init; }

        /// <summary>The viewer knows the cell (PolityKnowledge.KnowsCell). False: refused, nothing read.</summary>
        public required bool Known { get; init; }

        // ---- the terrain fields at the cell (TerrainSet)
        public double Elevation { get; init; }
        public double SeaLevel { get; init; }
        public double Water { get; init; }
        public bool IsWater { get; init; }
        public double TemperatureC { get; init; }
        public double Moisture { get; init; }
        public double Fertility { get; init; }
        public double MovementCost { get; init; }
        public bool River { get; init; }
        public double KmPerPx { get; init; }

        // ---- THE COMPOSITE: the unjittered siting score and its two factors
        /// <summary>SettlementSiting.CandidateScores at the cell (FrontierSiting.Scores).</summary>
        public double Score { get; init; }
        /// <summary>The region-fertility factor (BoxSum).</summary>
        public double RegionFertility { get; init; }
        public int FertilityRadiusPx { get; init; }
        /// <summary>BFS steps to the nearest water or river cell; int.MaxValue when none is reachable.</summary>
        public int WaterDistancePx { get; init; }
        public int WaterAccessCutoffPx { get; init; }
        /// <summary>The water-access factor (SettlementSiting.WaterAccess).</summary>
        public double WaterAccess { get; init; }
        /// <summary>The score floor (FrontierSiting.Floor) and the percentile it is.</summary>
        public double Floor { get; init; }
        public double FloorPercentile { get; init; }
        /// <summary>How many cells have a positive score (ScoreFloor's population) and how many of them score strictly less
        /// than this cell.</summary>
        public long PositiveScores { get; init; }
        public long ScoresBelow { get; init; }
        /// <summary>SettlementSiting.IsFrontierCandidate: land, score &gt; 0, not below the floor.</summary>
        public bool Candidate { get; init; }

        // ---- obstruction and claim (not part of the score)
        /// <summary>The lattice node the cell belongs to (LatticeMap.OriginLatticeNode).</summary>
        public int SiteNode { get; init; } = -1;
        /// <summary>The settlement whose site cell this is, or -1.</summary>
        public int OccupiedBy { get; init; } = -1;
        /// <summary>Min travel cost from any standing site to the cell's node (SeedSpacing; +inf beyond the spacing cap) and
        /// the cap the spacing rule rejects under.</summary>
        public double SpacingCost { get; init; }
        public double MinSpacingCost { get; init; }
        public double MinSpacingKm { get; init; }
        /// <summary>The settlement whose catchment holds the cell's node (a CatchmentNodes row), or -1.</summary>
        public int ClaimedBy { get; init; } = -1;

        // ---- hinterland and deposits (not part of the score)
        public double HinterlandRadiusPx { get; init; }
        public double MeanMoisture { get; init; }
        public double MeanElevation { get; init; }
        /// <summary>Every deposit good in registry order; empty when the deposits could not be evaluated (see
        /// <see cref="DepositsUnavailable"/>).</summary>
        public ImmutableArray<DepositPreview> Deposits { get; init; } = [];
        public string? DepositsUnavailable { get; init; }
        /// <summary>The id a settlement founded next would take (maxId + 1, ColonizationSystem's allocation).</summary>
        public int NextSettlementId { get; init; } = -1;
        /// <summary>Why the next-founding roll is not shown, or null when it is.</summary>
        public string? RollWithheld { get; init; }

        // ---- the would-be catchment (not part of the score)
        /// <summary>Fertility-weighted arable km² a settlement founded here would work, and how many lattice nodes it would
        /// win; null when not evaluated (<see cref="CatchmentUnavailable"/> says why).</summary>
        public double? WouldBeArableKm2 { get; init; }
        public int WouldBeNodes { get; init; }
        public string? CatchmentUnavailable { get; init; }

        // ---- nearest known settlements (not part of the score)
        public ImmutableArray<Nearby> Nearest { get; init; } = [];

        // ---- source, route, access (not part of the score)
        /// <summary>The source the route and the founding arms are judged from (-1: none).</summary>
        public int Source { get; init; } = -1;
        public RouteQuery.Route? Route { get; init; }
        /// <summary>The route's cost as ideal-ground km (LatticeGeometry.IdealGroundKmForCostUnits — the reporting inverse of
        /// the one km→cost conversion; a cost unit is an UPPER bound on real ground).</summary>
        public double RouteKm { get; init; }
        /// <summary>A settlement the viewer controls with a land route to the cell (the source first, then table order), or -1.</summary>
        public int AccessibleFrom { get; init; } = -1;

        // ---- the founding predicate (not part of the score)
        /// <summary>FoundingQuery.Check's own answer for (viewer, source, cell).</summary>
        public FoundingVerdict Verdict { get; init; }
        /// <summary>Each arm, judged separately with the predicate's own public parts, in the predicate's order.</summary>
        public ImmutableArray<FoundingArm> Arms { get; init; } = [];
        /// <summary>The party FoundingQuery.AllocateParty would form and the grain FoundingQuery.ProvisionsShare would send.</summary>
        public long Party { get; init; }
        public long Provisions { get; init; }
        /// <summary>The source's controller (a colony takes its source's ruler, ColonizationSystem.Found), or -1 for none.</summary>
        public int ColonyRuler { get; init; } = -1;
        /// <summary>EXTRACTABILITY for the viewer: the source stands and the viewer may allocate its labour
        /// (LabourActivities.CanAllocate — the OrderKind 3 predicate). A colony takes its source's ruler, so its
        /// Herding/fishing and Extraction labour — the sectors that work deposits, with no research gate in production —
        /// would be the viewer's to set exactly when this holds.</summary>
        public bool ColonyWorkable { get; init; }
    }

    /// <summary>The source an assessment is judged from when none is asked for: the viewer's capital, else its first
    /// controlled settlement in table order, else -1 (InfoQuery's default settlement).</summary>
    public static int DefaultSource(IReadOnlyWorldState world, PolityId viewer)
    {
        if (EmpireQuery.TryGetCapital(world, viewer, out SettlementId cap)) return cap.Value;
        SettlementId[] mine = LabourActivities.ControlledSettlements(world, viewer);
        return mine.Length > 0 ? mine[0].Value : -1;
    }

    /// <summary>
    /// The assessment of <paramref name="cell"/> for <paramref name="viewer"/> (whose knowledge is
    /// <paramref name="knowledge"/>), judged from <paramref name="source"/> (-1: <see cref="DefaultSource"/>), listing up to
    /// <paramref name="nearest"/> known settlements. Reads only; writes nothing anywhere.
    /// </summary>
    public static Report Assess(
        IReadOnlyWorldState world, SimConfig cfg, Prepared site, PolityKnowledge knowledge, PolityId viewer,
        int cell, int source = -1, int nearest = 5)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(knowledge);
        if (knowledge.Polity.Value != viewer.Value) throw new ArgumentException("the knowledge must be the viewer's", nameof(knowledge));
        if (!site.IsFor(world.Terrain)) throw new ArgumentException("the prepared site half is for another terrain", nameof(site));
        TerrainSet t = site.Terrain;
        int size = t.Size;

        // THE REFUSAL (P7 seam): a cell off the map or not known to the viewer reads nothing.
        if ((uint)cell >= (uint)(size * size) || !knowledge.KnowsCell(cell))
            return new Report { Cell = cell, X = cell >= 0 ? cell % size : -1, Y = cell >= 0 ? cell / size : -1, Known = false };

        SettlementSiting.FrontierSiting f = site.Frontier;
        SitingConfig siting = site.Worldgen.Siting;
        int x = cell % size, y = cell / size;

        // ---- THE COMPOSITE and its two factors.
        double score = f.Scores[cell];
        int waterDistance = site.WaterDistancePx[cell];
        double access = SettlementSiting.WaterAccess(waterDistance, siting.WaterAccessCutoffPx);
        long positive = 0, below = 0;
        for (int i = 0; i < f.Scores.Length; i++)
        {
            double s = f.Scores[i];
            if (!(s > 0.0)) continue;
            positive++;
            if (s < score) below++;
        }

        // ---- obstruction (the standing sites, as FoundingQuery.Check derives them), and claim.
        int n = world.Settlements.Count;
        var occupied = new int[n];
        for (int i = 0; i < n; i++) occupied[i] = world.Settlements[i].SiteCell;
        double[] spacing = SettlementSiting.SeedSpacing(f, occupied);
        int node = LatticeMap.OriginLatticeNode(f.Lattice, size, cell);
        int occupiedBy = -1;
        for (int i = 0; i < n; i++) if (occupied[i] == cell) { occupiedBy = world.Settlements[i].Id.Value; break; }
        int claimedBy = -1;
        for (int i = 0; i < world.CatchmentNodes.Count; i++)
            if (world.CatchmentNodes[i].LatticeNode == node) { claimedBy = world.CatchmentNodes[i].Settlement.Value; break; }

        // ---- hinterland means and deposits.
        double radiusPx = WorldFounding.HinterlandRadiusPx(site.Worldgen, cfg);
        int maxId = -1;
        for (int i = 0; i < n; i++) maxId = Math.Max(maxId, world.Settlements[i].Id.Value);
        int nextId = maxId + 1;
        double meanM = 0.0, meanE = 0.0;
        ImmutableArray<DepositPreview> deposits = [];
        string? depositsUnavailable = null, rollWithheld = null;
        bool water = t.Water[cell] >= 0.5;
        if (cfg.Goods is not { } goods) depositsUnavailable = "no goods content is loaded";
        else if (water) depositsUnavailable = "no settlement can stand on water";
        else if (site.Worldgen.SizePx != size)
            depositsUnavailable = "the founding path's worldgen config describes a " + Inv(site.Worldgen.SizePx) + "-px map and this map is "
                + Inv(size) + " px, so its deposit function cannot be evaluated here";
        else
        {
            (meanM, meanE) = WorldFounding.HinterlandMeans(t, site.Worldgen, cell, radiusPx);
            Table<DepositRow>? roll = null;
            if (knowledge.AllKnown)
            {
                roll = new Table<DepositRow>();   // scratch: the founding's own call, into a table nobody else holds
                WorldFounding.AddDepositsForSite(roll, t, site.Worldgen, cfg, goods, new SettlementId(nextId), cell, world.Seed, nextId);
            }
            else rollWithheld = "the next settlement id counts every settlement in the world, including ones you do not know";
            var list = ImmutableArray.CreateBuilder<DepositPreview>();
            foreach (GoodEntry g in goods.Goods)
            {
                if (g.DepositChannel is null) continue;
                double v = WorldFounding.DepositChannelValue(g.DepositChannel, meanM, meanE);
                double? exact = null;
                if (roll is not null)
                    for (int i = 0; i < roll.Count; i++)
                        if (roll[i].Good.Value == g.Id) { exact = roll[i].Abundance; break; }
                list.Add(new DepositPreview(new GoodId(g.Id), g.DepositChannel, v, g.DepositSpread,
                    Math.Max(0.0, v * (1.0 - g.DepositSpread)), Math.Max(0.0, v * (1.0 + g.DepositSpread)), exact,
                    LabourActivities.IsHerded(goods, g.Id)));
            }
            deposits = list.ToImmutable();
        }

        // ---- the would-be catchment: CatchmentSystem's partition inputs with the site appended last (the index a colony
        // takes: ColonizationSystem appends its row), budgets by each settlement's own size tier (a colony's is 0).
        double? arable = null;
        int arableNodes = 0;
        string? catchmentUnavailable = water ? "no settlement can stand on water"
            : n == 0 || world.NetworkMeta.Count == 0 ? "no catchment is computed in this world" : null;
        if (catchmentUnavailable is null)
        {
            var origins = new int[n + 1];
            var budgets = new double[n + 1];
            for (int s = 0; s < n; s++)
            {
                origins[s] = LatticeMap.OriginLatticeNode(f.Lattice, size, world.Settlements[s].SiteCell);
                budgets[s] = CatchmentSystem.TierBudget(cfg, f.Lattice, CatchmentSystem.TierOf(world, cfg, world.Settlements[s].Id));
            }
            origins[n] = node;
            budgets[n] = CatchmentSystem.TierBudget(cfg, f.Lattice, CatchmentSystem.TierOf(world, cfg, new SettlementId(nextId)));
            Pathfinder.PartitionResult part = Pathfinder.Partition(f.Lattice, world, origins, budgets);
            double sum = 0.0;   // ascending node order: CatchmentSystem's summation order
            for (int k = 0; k < part.Owner.Length; k++)
            {
                if (part.Owner[k] != n) continue;
                sum += LatticeMap.BlockArableKm2(t, f.Lattice, k);
                arableNodes++;
            }
            arable = sum;
        }

        // ---- nearest KNOWN settlements: composite integer key (squared px distance, id).
        var near = new List<Nearby>();
        for (int i = 0; i < n; i++)
        {
            SettlementRow row = world.Settlements[i];
            if (!knowledge.KnowsSettlement(row.Id)) continue;
            long dx = row.SiteCell % size - x, dy = row.SiteCell / size - y;
            long d2 = dx * dx + dy * dy;
            near.Add(new Nearby(row.Id, d2, Math.Sqrt(d2) * t.KmPerPx));
        }
        Nearby[] sorted = [.. near];
        Array.Sort(sorted, CompareNearby);
        int take = Math.Min(Math.Max(0, nearest), sorted.Length);

        // ---- the source, the founding predicate and each of its arms.
        int src = source >= 0 ? source : DefaultSource(world, viewer);
        var srcId = new SettlementId(src);
        int srcCell = -1;
        for (int i = 0; i < n && src >= 0; i++) if (world.Settlements[i].Id.Value == src) { srcCell = world.Settlements[i].SiteCell; break; }
        bool stands = srcCell >= 0;
        FoundingVerdict verdict = FoundingQuery.Check(world, cfg, f, viewer, srcId, cell);

        long party = 0, provisions = 0;
        RouteQuery.Route? route = null;
        int ruler = -1;
        if (stands)
        {
            party = FoundingQuery.AllocateParty(world.Buckets, srcId, FoundingQuery.PartyFraction(cfg), null);
            if (cfg.Goods is { } gc)
                provisions = FoundingQuery.ProvisionsShare(world.GoodStocks, world.Buckets, srcId, new GoodId(gc.GrainId), party);
            route = RouteQuery.Find(f.Lattice, world, LatticeMap.OriginLatticeNode(f.Lattice, size, srcCell), node);
            if (EmpireQuery.TryGetController(world, srcId, out PolityId r)) ruler = r.Value;
        }

        bool candidate = SettlementSiting.IsFrontierCandidate(f, t, cell);
        bool isOccupied = SettlementSiting.IsOccupied(occupied, cell);
        bool tooClose = spacing[node] < f.MinSpacingCostUnits;
        var arms = ImmutableArray.Create(
            new FoundingArm(FoundingVerdict.UnknownSource, stands),
            new FoundingArm(FoundingVerdict.NotControlled, stands ? EmpireQuery.ControlsSettlement(world, viewer, srcId) : null),
            new FoundingArm(FoundingVerdict.OffMap, f.Size == size),
            new FoundingArm(FoundingVerdict.Water, !water),
            new FoundingArm(FoundingVerdict.PoorSite, water ? null : candidate),
            new FoundingArm(FoundingVerdict.Occupied, !isOccupied),
            new FoundingArm(FoundingVerdict.TooClose, isOccupied ? null : !tooClose),
            new FoundingArm(FoundingVerdict.Claimed, !FoundingQuery.IsClaimed(world, node)),
            new FoundingArm(FoundingVerdict.NoParty, stands ? party >= 1 : null),
            new FoundingArm(FoundingVerdict.NoProvisions, stands && party >= 1 ? provisions >= 1 : null),
            new FoundingArm(FoundingVerdict.Unreachable, stands ? route!.Found : null));

        // ---- accessibility: a land route from a settlement the viewer controls (the source first).
        int accessible = -1;
        if (route is { Found: true } && EmpireQuery.ControlsSettlement(world, viewer, srcId)) accessible = src;
        else
            foreach (SettlementId mine in LabourActivities.ControlledSettlements(world, viewer))
            {
                if (mine.Value == src) continue;
                int from = -1;
                for (int i = 0; i < n; i++) if (world.Settlements[i].Id == mine) { from = world.Settlements[i].SiteCell; break; }
                if (from < 0) continue;
                if (Pathfinder.FindPath(f.Lattice, world, LatticeMap.OriginLatticeNode(f.Lattice, size, from), node).Found) { accessible = mine.Value; break; }
            }

        return new Report
        {
            Cell = cell, X = x, Y = y, Known = true,
            Elevation = t.Elevation[cell], SeaLevel = t.SeaLevel, Water = t.Water[cell], IsWater = water,
            TemperatureC = t.Temperature[cell], Moisture = t.Moisture[cell], Fertility = t.Fertility[cell],
            MovementCost = t.MovementCost[cell], River = t.Rivers[cell] >= 0.5, KmPerPx = t.KmPerPx,
            Score = score, RegionFertility = site.RegionFertility[cell], FertilityRadiusPx = siting.FertilityRadiusPx,
            WaterDistancePx = waterDistance, WaterAccessCutoffPx = siting.WaterAccessCutoffPx, WaterAccess = access,
            Floor = f.Floor, FloorPercentile = siting.ScoreFloorPercentile, PositiveScores = positive, ScoresBelow = below,
            Candidate = candidate,
            SiteNode = node, OccupiedBy = occupiedBy, SpacingCost = spacing[node], MinSpacingCost = f.MinSpacingCostUnits,
            MinSpacingKm = siting.MinSpacingKm, ClaimedBy = claimedBy,
            HinterlandRadiusPx = radiusPx, MeanMoisture = meanM, MeanElevation = meanE, Deposits = deposits,
            DepositsUnavailable = depositsUnavailable, NextSettlementId = nextId, RollWithheld = rollWithheld,
            WouldBeArableKm2 = arable, WouldBeNodes = arableNodes, CatchmentUnavailable = catchmentUnavailable,
            Nearest = ImmutableArray.Create(sorted, 0, take),
            Source = src, Route = route,
            RouteKm = route is { Found: true } r2 ? LatticeGeometry.IdealGroundKmForCostUnits(f.Lattice, r2.TotalCost) : 0.0,
            AccessibleFrom = accessible,
            Verdict = verdict, Arms = arms, Party = party, Provisions = provisions, ColonyRuler = ruler,
            ColonyWorkable = stands && LabourActivities.CanAllocate(world, viewer, srcId),
        };
    }

    /// <summary>THE nearest-settlement order: the composite integer key (squared px distance ASC, settlement id ASC) — a
    /// total order, so equal distances never tie.</summary>
    public static int CompareNearby(Nearby a, Nearby b)
    {
        int c = a.DistanceSquaredPx.CompareTo(b.DistanceSquaredPx);
        return c != 0 ? c : a.Id.Value.CompareTo(b.Id.Value);
    }

    /// <summary>The first arm that does not hold, in the predicate's order, or Legal — what FoundingQuery.Check answers
    /// when the arms are its own (pinned by SiteAssessmentTests over a dense sweep).</summary>
    public static FoundingVerdict FirstFailing(ImmutableArray<FoundingArm> arms)
    {
        foreach (FoundingArm a in arms) if (a.Holds == false) return a.Arm;
        return FoundingVerdict.Legal;
    }

    private static string Inv(long v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
