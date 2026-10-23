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
/// <para><b>INFORMATION CLASS (P7; the PLAYER's, ADR-037 §6/§7).</b> The card is a player output, so it is held to the
/// player's stricter class: what it shows is a function of what the viewer knows, never of what lies unseen. A cell the
/// viewer's <see cref="PolityKnowledge"/> does not know is REFUSED: the report says so and nothing of the cell, the
/// terrain or the world is read for it. For a KNOWN cell, while the viewer SEES THE WHOLE WORLD (<see cref="SeesAll"/>:
/// all-known, or every node visible — production today) every value is the simulation's own. Otherwise each value that
/// would read beyond what the viewer knows is WITHHELD — "not judged", never estimated: the score's factors when the
/// fertility box or the water-access ball (the 4-connected BFS's reach, capped at the cutoff) touches an unknown cell;
/// the floor, the rank and the floor arm (taken over every cell); spacing and the would-be catchment (every settlement,
/// the lattice); the route and access (the lattice and every road); the hinterland and deposits when the disc touches
/// an unknown cell; the occupancy and claim arms unless a settlement the viewer KNOWS decides them (or, for occupancy,
/// the cell is in sight, where no settlement can stand unseen); and the predicate's verdict when such an arm comes
/// before its first failure. A source settlement the viewer does not know names nothing. Each "withheld or shown" choice
/// is itself a function of the knowledge masks and known rows alone. The nearest-settlement list names known settlements
/// only. The deposit roll for the next settlement id is shown only while the viewer sees every settlement, because the
/// id counts all of them.</para>
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
            RegionFertility = SettlementSiting.BoxSum(terrain.Fertility, terrain.Size, worldgen.Siting.FertilityRadiusPx, terrain.WrapX),
            WaterDistancePx = SettlementSiting.WaterDistanceBfs(terrain.Water, terrain.Rivers, terrain.Size, terrain.WrapX),
        };
    }

    /// <summary>
    /// M5 S1d review — THE FOUNDING BATCH a place is judged against: the site cells standing for the coming step's founding
    /// orders — the world's settlements first (<see cref="StandingCount"/> of them, in table order), then each site an order
    /// already queued this turn will found, in log order — and the spacing exclusion they make. These are exactly the
    /// arrays FoundingQuery.Check's explicit overload takes (AiFoundingPolicy.Sites grown by each accepted order: the
    /// intake's own growth); <see cref="StandingSpacing"/> is the exclusion of the standing settlements alone, so a refusal
    /// that only an ORDERED site causes is told as such. A snapshot: the caller (the UI's
    /// FoundSettlementOrderFactory.Standing, which replays the queue through THE predicate) builds it once and nothing
    /// writes it afterwards. Read-only; nothing here is serialized or consulted by a system.
    /// </summary>
    public sealed class Batch
    {
        private readonly int[] _occupied;
        private readonly double[] _spacing;
        private readonly double[] _standingSpacing;

        /// <summary>The batch over <paramref name="frontier"/>: <paramref name="occupied"/> the site cells (the first
        /// <paramref name="standingCount"/> the world's settlements', the rest ordered this turn), <paramref name="spacing"/>
        /// their exclusion and <paramref name="standingSpacing"/> the standing settlements' alone (both over the frontier
        /// lattice's nodes, or both empty for a world without terrain). Copied.</summary>
        public Batch(SettlementSiting.FrontierSiting frontier, ReadOnlySpan<int> occupied, int standingCount,
            ReadOnlySpan<double> spacing, ReadOnlySpan<double> standingSpacing)
        {
            ArgumentNullException.ThrowIfNull(frontier);
            if ((uint)standingCount > (uint)occupied.Length) throw new ArgumentOutOfRangeException(nameof(standingCount));
            if (spacing.Length != standingSpacing.Length) throw new ArgumentException("the two exclusions must cover the same nodes", nameof(standingSpacing));
            Frontier = frontier;
            StandingCount = standingCount;
            _occupied = occupied.ToArray();
            _spacing = spacing.ToArray();
            _standingSpacing = standingSpacing.ToArray();
        }

        /// <summary>The frontier the batch was built over (the one the assessment's <see cref="Prepared"/> holds).</summary>
        public SettlementSiting.FrontierSiting Frontier { get; }

        /// <summary>The site cells: the world's settlements', then the ordered ones.</summary>
        public ReadOnlySpan<int> Occupied => _occupied;

        /// <summary>How many of <see cref="Occupied"/> (the first ones) are standing settlements' sites.</summary>
        public int StandingCount { get; }

        /// <summary>How many sites orders queued this turn will found.</summary>
        public int OrderedCount => _occupied.Length - StandingCount;

        /// <summary>The spacing exclusion of every site of the batch, per lattice node.</summary>
        public ReadOnlySpan<double> Spacing => _spacing;

        /// <summary>The spacing exclusion of the standing settlements alone, per lattice node.</summary>
        public ReadOnlySpan<double> StandingSpacing => _standingSpacing;

        /// <summary>Whether <paramref name="cell"/> is the site of an order queued this turn and of no standing settlement.</summary>
        public bool OrderedAt(int cell) =>
            !SettlementSiting.IsOccupied(Occupied[..StandingCount], cell) && SettlementSiting.IsOccupied(Occupied[StandingCount..], cell);

        /// <summary>Whether <paramref name="cell"/>'s origin lattice node lies inside the spacing of a site ordered this turn
        /// and outside every standing settlement's (IsUnobstructedFrontierCell's spacing test on each exclusion).</summary>
        public bool ExcludedByOrder(int cell)
        {
            if ((uint)cell >= (uint)Frontier.Scores.Length || _spacing.Length == 0) return false;
            int node = LatticeMap.OriginLatticeNode(Frontier.Lattice, Frontier.Size, cell);
            return _spacing[node] < Frontier.MinSpacingCostUnits && !(_standingSpacing[node] < Frontier.MinSpacingCostUnits);
        }

        /// <summary>Whether the refusal <paramref name="verdict"/> at <paramref name="cell"/> is caused by an order queued this
        /// turn alone: Occupied by an ordered site, or TooClose inside an ordered site's spacing only.</summary>
        public bool RefusesByOrder(FoundingVerdict verdict, int cell) => verdict switch
        {
            FoundingVerdict.Occupied => OrderedAt(cell),
            FoundingVerdict.TooClose => ExcludedByOrder(cell),
            _ => false,
        };
    }

    /// <summary>One arm of the founding predicate as judged here: <see cref="Holds"/> null when the arm could not be
    /// judged — its source does not stand, or an earlier arm it depends on decides it, or (<see cref="Unseen"/>) it
    /// depends on what the viewer has not seen (the player's information class).</summary>
    public readonly record struct FoundingArm(FoundingVerdict Arm, bool? Holds, bool Unseen = false);

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

        /// <summary>The viewer sees the whole world (<see cref="SiteAssessment.SeesAll"/>): every value below is the
        /// simulation's own. False: every value that reads beyond what the viewer knows is withheld — its flag or its
        /// "unavailable" reason says so — and its number is NaN / -1 / null, never an estimate (the player's class).</summary>
        public bool SeesAll { get; init; }

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
        /// <summary>SettlementSiting.CandidateScores at the cell (FrontierSiting.Scores); NaN when <see cref="ScoreUnseen"/>.</summary>
        public double Score { get; init; }
        /// <summary>The region-fertility factor (BoxSum); NaN when <see cref="RegionFertilityUnseen"/>.</summary>
        public double RegionFertility { get; init; }
        public int FertilityRadiusPx { get; init; }
        /// <summary>The fertility box (±<see cref="FertilityRadiusPx"/>) touches a cell the viewer does not know.</summary>
        public bool RegionFertilityUnseen { get; init; }
        /// <summary>BFS steps to the nearest water or river cell; int.MaxValue when none is reachable. When the viewer does
        /// not see the whole world: -1 when farther than the cutoff (beyond it the BFS reads ground the viewer may not have
        /// seen, and the factor is 0 either way), and -1 too when <see cref="WaterAccessUnseen"/>.</summary>
        public int WaterDistancePx { get; init; }
        public int WaterAccessCutoffPx { get; init; }
        /// <summary>The water-access factor (SettlementSiting.WaterAccess); NaN when <see cref="WaterAccessUnseen"/>.</summary>
        public double WaterAccess { get; init; }
        /// <summary>The cells the factor depends on — every cell within min(distance, cutoff) 4-connected steps, the BFS's
        /// reach — include one the viewer does not know.</summary>
        public bool WaterAccessUnseen { get; init; }
        /// <summary>Either factor is withheld, so the score is.</summary>
        public bool ScoreUnseen => RegionFertilityUnseen || WaterAccessUnseen;
        /// <summary>The score floor (FrontierSiting.Floor) and the percentile it is. The floor and the rank are taken over
        /// every cell of the map: withheld (NaN, 0, 0, Candidate false) unless <see cref="SeesAll"/>.</summary>
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
        /// <summary>The settlement whose site cell this is, or -1. Unless <see cref="SeesAll"/>: a settlement the viewer
        /// KNOWS, or -1.</summary>
        public int OccupiedBy { get; init; } = -1;
        /// <summary>Min travel cost from any standing site to the cell's node (SeedSpacing; +inf beyond the spacing cap) and
        /// the cap the spacing rule rejects under. NaN unless <see cref="SeesAll"/> (every settlement, the lattice).</summary>
        public double SpacingCost { get; init; }
        public double MinSpacingCost { get; init; }
        public double MinSpacingKm { get; init; }
        /// <summary>The settlement whose catchment holds the cell's node (a CatchmentNodes row), or -1. Unless
        /// <see cref="SeesAll"/>: a settlement the viewer KNOWS, or -1.</summary>
        public int ClaimedBy { get; init; } = -1;
        /// <summary>M5 S1d review: how many sites orders queued this turn will found, in the <see cref="Batch"/> the place was
        /// judged against (0 without one). With a batch the occupancy and spacing arms and the verdict are the batch's —
        /// FoundingQuery.Check's explicit overload, the answer the emitter's guard and the intake give.</summary>
        public int OrderedSites { get; init; }
        /// <summary>The cell is the site of a settlement an order queued this turn will found (no settlement stands there
        /// yet): <see cref="Batch.OrderedAt"/>. Unless <see cref="SeesAll"/>: the cell is one of the batch's ordered sites
        /// and no settlement the viewer knows stands there (the standing settlements are not read). False without a batch.</summary>
        public bool OccupiedByOrder { get; init; }
        /// <summary>The cell's node lies inside the spacing of a site ordered this turn and outside every standing
        /// settlement's: <see cref="Batch.ExcludedByOrder"/>. False without a batch, or unless <see cref="SeesAll"/>
        /// (spacing reads every settlement and the lattice).</summary>
        public bool TooCloseByOrder { get; init; }
        /// <summary>Min travel cost from any site of the batch — standing or ordered this turn — to the cell's node (the
        /// exclusion the spacing arm reads with a batch). NaN without a batch or unless <see cref="SeesAll"/>.</summary>
        public double BatchSpacingCost { get; init; } = double.NaN;
        /// <summary>Whether <see cref="Verdict"/> is a refusal that only an order queued this turn causes (its own words).</summary>
        public bool RefusedByOrder => Verdict switch
        {
            FoundingVerdict.Occupied => OccupiedByOrder,
            FoundingVerdict.TooClose => TooCloseByOrder,
            _ => false,
        };

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
        /// <summary>The source the route and the founding arms are judged from (-1: none; unless <see cref="SeesAll"/>, a
        /// source the viewer does not know is none).</summary>
        public int Source { get; init; } = -1;
        /// <summary>The route from the source; null when the source does not stand or, unless <see cref="SeesAll"/>, always
        /// (it is found over the whole lattice and every road: withheld, as is <see cref="AccessibleFrom"/>).</summary>
        public RouteQuery.Route? Route { get; init; }
        /// <summary>The route's cost as ideal-ground km (LatticeGeometry.IdealGroundKmForCostUnits — the reporting inverse of
        /// the one km→cost conversion; a cost unit is an UPPER bound on real ground).</summary>
        public double RouteKm { get; init; }
        /// <summary>A settlement the viewer controls with a land route to the cell (the source first, then table order), or -1.</summary>
        public int AccessibleFrom { get; init; } = -1;

        // ---- the founding predicate (not part of the score)
        /// <summary>FoundingQuery.Check's own answer for (viewer, source, cell). Unless <see cref="SeesAll"/>: the first
        /// failing arm when every arm before it is judged from what the viewer knows, else null — "not judged"
        /// (<see cref="FirstFailing"/>).</summary>
        public FoundingVerdict? Verdict { get; init; }
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
    /// <para>M5 S1d review: with a <paramref name="batch"/> (the placement mode's: this turn's queued founding orders replayed
    /// through THE predicate) the occupancy and spacing arms and the verdict are judged against the batch's sites —
    /// FoundingQuery.Check's explicit overload, the answer the emitter's guard and the intake give — and a refusal that only
    /// an ordered site causes is flagged (<see cref="Report.OccupiedByOrder"/>, <see cref="Report.TooCloseByOrder"/>).
    /// Without one, the standing settlements alone (P8, unchanged).</para>
    /// </summary>
    public static Report Assess(
        IReadOnlyWorldState world, SimConfig cfg, Prepared site, PolityKnowledge knowledge, PolityId viewer,
        int cell, int source = -1, int nearest = 5, Batch? batch = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(knowledge);
        if (knowledge.Polity.Value != viewer.Value) throw new ArgumentException("the knowledge must be the viewer's", nameof(knowledge));
        if (!site.IsFor(world.Terrain)) throw new ArgumentException("the prepared site half is for another terrain", nameof(site));
        if (batch is not null && !ReferenceEquals(batch.Frontier, site.Frontier))
            throw new ArgumentException("the batch is over another frontier than the prepared site half's", nameof(batch));
        TerrainSet t = site.Terrain;
        int size = t.Size;

        // THE REFUSAL (P7 seam): a cell off the map or not known to the viewer reads nothing.
        if ((uint)cell >= (uint)(size * size) || !knowledge.KnowsCell(cell))
            return new Report { Cell = cell, X = cell >= 0 ? cell % size : -1, Y = cell >= 0 ? cell / size : -1, Known = false };

        SettlementSiting.FrontierSiting f = site.Frontier;
        SitingConfig siting = site.Worldgen.Siting;
        int x = cell % size, y = cell / size;
        int n = world.Settlements.Count;
        int node = LatticeMap.OriginLatticeNode(f.Lattice, size, cell);
        bool water = t.Water[cell] >= 0.5;

        // THE PLAYER'S CLASS (ADR-037 §7): unless the viewer sees the whole world, every value below that would read beyond
        // what it knows is withheld, never estimated. Each test is of the knowledge masks and known rows alone, so whether a
        // value is shown tells nothing of what lies unseen either.
        bool all = SeesAll(knowledge);

        // ---- THE COMPOSITE and its two factors — each withheld when its own reach touches an unknown cell: the fertility
        // box, and the cells the 4-connected water BFS reads to settle min(distance, cutoff) (beyond the cutoff the factor is
        // 0 whatever the distance, so a farther distance is not shown).
        int cutoff = siting.WaterAccessCutoffPx;
        int waterDistance = site.WaterDistancePx[cell];
        bool fertilityUnseen = !all && !KnowsSquare(knowledge, size, x, y, siting.FertilityRadiusPx);
        bool waterUnseen = !all && !KnowsDiamond(knowledge, size, x, y, Math.Min(waterDistance, cutoff));
        double regionFertility = fertilityUnseen ? double.NaN : site.RegionFertility[cell];
        double access = waterUnseen ? double.NaN : SettlementSiting.WaterAccess(waterDistance, cutoff);
        double score = fertilityUnseen || waterUnseen ? double.NaN : f.Scores[cell];
        if (!all && (waterUnseen || waterDistance > cutoff)) waterDistance = -1;

        // ---- the floor and the rank: taken over every cell of the map, so only while the viewer sees it all.
        long positive = 0, below = 0;
        double floor = double.NaN;
        bool candidate = false;
        if (all)
        {
            for (int i = 0; i < f.Scores.Length; i++)
            {
                double s = f.Scores[i];
                if (!(s > 0.0)) continue;
                positive++;
                if (s < score) below++;
            }
            floor = f.Floor;
            candidate = SettlementSiting.IsFrontierCandidate(f, t, cell);
        }

        // ---- obstruction (the standing sites, as FoundingQuery.Check derives them — or, with a batch, the batch's sites, as the
        // explicit overload takes them), and claim. Unless the viewer sees the whole world: spacing is withheld (every
        // settlement, the lattice), and occupancy and claim name only a settlement the viewer KNOWS (or a site the viewer's
        // own queued order takes — the batch's ordered sites are the viewer's orders).
        int occupiedBy = -1, claimedBy = -1;
        bool isOccupied = false, tooClose = false, occupiedByOrder = false, tooCloseByOrder = false;
        double spacingCost = double.NaN, batchSpacingCost = double.NaN;
        if (all)
        {
            var occupied = new int[n];
            for (int i = 0; i < n; i++) occupied[i] = world.Settlements[i].SiteCell;
            spacingCost = SettlementSiting.SeedSpacing(f, occupied)[node];
            if (batch is null)
            {
                tooClose = spacingCost < f.MinSpacingCostUnits;
                isOccupied = SettlementSiting.IsOccupied(occupied, cell);
            }
            else
            {
                batchSpacingCost = batch.Spacing[node];
                tooClose = batchSpacingCost < f.MinSpacingCostUnits;
                isOccupied = SettlementSiting.IsOccupied(batch.Occupied, cell);
                occupiedByOrder = batch.OrderedAt(cell);
                tooCloseByOrder = !isOccupied && batch.ExcludedByOrder(cell);
            }
            for (int i = 0; i < n; i++) if (occupied[i] == cell) { occupiedBy = world.Settlements[i].Id.Value; break; }
            for (int i = 0; i < world.CatchmentNodes.Count; i++)
                if (world.CatchmentNodes[i].LatticeNode == node) { claimedBy = world.CatchmentNodes[i].Settlement.Value; break; }
        }
        else
        {
            for (int i = 0; i < n; i++)
                if (world.Settlements[i].SiteCell == cell && knowledge.KnowsSettlement(world.Settlements[i].Id)) { occupiedBy = world.Settlements[i].Id.Value; break; }
            // Only the ORDERED part of the batch is read here (the viewer's own orders), never the standing settlements'.
            occupiedByOrder = occupiedBy < 0 && batch is not null && SettlementSiting.IsOccupied(batch.Occupied[batch.StandingCount..], cell);
            for (int i = 0; i < world.CatchmentNodes.Count; i++)
                if (world.CatchmentNodes[i].LatticeNode == node && knowledge.KnowsSettlement(world.CatchmentNodes[i].Settlement))
                { claimedBy = world.CatchmentNodes[i].Settlement.Value; break; }
        }

        // ---- hinterland means and deposits.
        double radiusPx = WorldFounding.HinterlandRadiusPx(site.Worldgen, cfg);
        int nextId = -1;
        if (all)
        {
            int maxId = -1;
            for (int i = 0; i < n; i++) maxId = Math.Max(maxId, world.Settlements[i].Id.Value);
            nextId = maxId + 1;
        }
        double meanM = 0.0, meanE = 0.0;
        ImmutableArray<DepositPreview> deposits = [];
        string? depositsUnavailable = null, rollWithheld = null;
        // M5 playtest blocker (D-052): no size-mismatch refusal. WorldFounding.HinterlandMeans indexes the disc by the
        // TERRAIN's size, so on a map founded at a non-canonical size (`--size`, the 256-px dev worlds) the disc read
        // with the founding path's 1024-px config is this map's own, and the card shows exactly the deposits a colony
        // founded here is written.
        if (cfg.Goods is not { } goods) depositsUnavailable = "no goods content is loaded";
        else if (water) depositsUnavailable = "no settlement can stand on water";
        else if (!all && !KnowsDisc(knowledge, size, x, y, radiusPx))
            depositsUnavailable = "the hinterland a settlement here would draw on reaches ground you have not discovered";
        else
        {
            (meanM, meanE) = WorldFounding.HinterlandMeans(t, site.Worldgen, cell, radiusPx);
            Table<DepositRow>? roll = null;
            if (all)
            {
                roll = new Table<DepositRow>();   // scratch: the founding's own call, into a table nobody else holds
                WorldFounding.AddDepositsForSite(roll, t, site.Worldgen, cfg, goods, new SettlementId(nextId), cell, world.Seed, nextId);
            }
            else rollWithheld = "the next settlement id counts every settlement in the world, including ones you do not know";
            // M6 EX (D-062, ADR-044): a not-yet-founded site's CONCEALED rows are unknown — the classification of the roll a
            // settlement founded next would get (State.DepositConcealment over the scratch table, under the id it would get);
            // nobody has surveyed a settlement that does not exist, so its exact abundance is not shown (the range still is:
            // it is read from the terrain, which the viewer knows).
            int[] concealed = roll is not null
                ? DepositConcealment.ConcealedGoods(roll, world.Seed, cfg, new SettlementId(nextId)) : [];
            var list = ImmutableArray.CreateBuilder<DepositPreview>();
            foreach (GoodEntry g in goods.Goods)
            {
                if (g.DepositChannel is null) continue;
                double v = WorldFounding.DepositChannelValue(g.DepositChannel, meanM, meanE);
                double? exact = null;
                if (roll is not null && !DepositConcealment.Contains(concealed, g.Id))
                    for (int i = 0; i < roll.Count; i++)
                        if (roll[i].Good.Value == g.Id) { exact = roll[i].Abundance; break; }
                list.Add(new DepositPreview(new GoodId(g.Id), g.DepositChannel, v, g.DepositSpread,
                    Math.Max(0.0, v * (1.0 - g.DepositSpread)), Math.Max(0.0, v * (1.0 + g.DepositSpread)), exact,
                    LabourActivities.IsHerded(goods, g.Id)));
            }
            deposits = list.ToImmutable();
        }

        // ---- the would-be catchment: CatchmentSystem's partition inputs with the site appended last (the index a colony
        // takes: ColonizationSystem appends its row), budgets by each settlement's own size tier (a colony's is 0). It is
        // shared out against every settlement over the whole lattice: only while the viewer sees it all.
        double? arable = null;
        int arableNodes = 0;
        string? catchmentUnavailable = water ? "no settlement can stand on water"
            : !all ? "the land is shared out against every settlement in the world, over the whole lattice, and you do not see all of it"
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
            // WS3: the squared distance on the cylinder when the world wraps (the sim's own donor-order metric).
            long d2 = MapTopology.SquaredPx(row.SiteCell, cell, size, t.WrapX);
            near.Add(new Nearby(row.Id, d2, Math.Sqrt(d2) * t.KmPerPx));
        }
        Nearby[] sorted = [.. near];
        Array.Sort(sorted, CompareNearby);
        int take = Math.Min(Math.Max(0, nearest), sorted.Length);

        // ---- the source, the founding predicate and each of its arms. A source the viewer does not know names nothing.
        int src = source >= 0 ? source : DefaultSource(world, viewer);
        if (!all && src >= 0 && !knowledge.KnowsSettlement(new SettlementId(src))) src = -1;
        var srcId = new SettlementId(src);
        int srcCell = -1;
        for (int i = 0; i < n && src >= 0; i++) if (world.Settlements[i].Id.Value == src) { srcCell = world.Settlements[i].SiteCell; break; }
        bool stands = srcCell >= 0;

        long party = 0, provisions = 0;
        RouteQuery.Route? route = null;
        int ruler = -1;
        if (stands)
        {
            party = FoundingQuery.AllocateParty(world.Buckets, srcId, FoundingQuery.PartyFraction(cfg), null);
            if (cfg.Goods is { } gc)
                provisions = FoundingQuery.ProvisionsShare(world.GoodStocks, world.Buckets, srcId, new GoodId(gc.GrainId), party);
            if (all) route = RouteQuery.Find(f.Lattice, world, LatticeMap.OriginLatticeNode(f.Lattice, size, srcCell), node);
            if (EmpireQuery.TryGetController(world, srcId, out PolityId r)) ruler = r.Value;
        }

        ImmutableArray<FoundingArm> arms;
        FoundingVerdict? verdict;
        if (all)
        {
            verdict = batch is null
                ? FoundingQuery.Check(world, cfg, f, viewer, srcId, cell)
                : FoundingQuery.Check(world, cfg, f, batch.Spacing, batch.Occupied, viewer, srcId, cell);
            arms = ImmutableArray.Create(
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
        }
        else
        {
            // Judged from what the viewer knows, or Unseen: the floor (every cell); occupancy, unless a known settlement
            // stands here or the cell is in sight (no settlement stands unseen on a cell in sight); spacing (every settlement,
            // the lattice); the claim, unless a known settlement's catchment holds the node; the route (the lattice, every road).
            // A site the viewer's own order queued this turn takes is occupied in the batch: known to the viewer.
            arms = ImmutableArray.Create(
                new FoundingArm(FoundingVerdict.UnknownSource, stands),
                new FoundingArm(FoundingVerdict.NotControlled, stands ? EmpireQuery.ControlsSettlement(world, viewer, srcId) : null),
                new FoundingArm(FoundingVerdict.OffMap, f.Size == size),
                new FoundingArm(FoundingVerdict.Water, !water),
                new FoundingArm(FoundingVerdict.PoorSite, null, Unseen: !water),
                occupiedBy >= 0 || occupiedByOrder ? new FoundingArm(FoundingVerdict.Occupied, false)
                    : knowledge.SeesCell(cell) ? new FoundingArm(FoundingVerdict.Occupied, true)
                    : new FoundingArm(FoundingVerdict.Occupied, null, Unseen: true),
                new FoundingArm(FoundingVerdict.TooClose, null, Unseen: occupiedBy < 0 && !occupiedByOrder),
                claimedBy >= 0 ? new FoundingArm(FoundingVerdict.Claimed, false) : new FoundingArm(FoundingVerdict.Claimed, null, Unseen: true),
                new FoundingArm(FoundingVerdict.NoParty, stands ? party >= 1 : null),
                new FoundingArm(FoundingVerdict.NoProvisions, stands && party >= 1 ? provisions >= 1 : null),
                new FoundingArm(FoundingVerdict.Unreachable, null, Unseen: stands));
            verdict = FirstFailing(arms);
        }

        // ---- accessibility: a land route from a settlement the viewer controls (the source first) — over the whole lattice
        // and every road, so only while the viewer sees it all.
        int accessible = -1;
        if (all && route is { Found: true } && EmpireQuery.ControlsSettlement(world, viewer, srcId)) accessible = src;
        else if (all)
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
            Cell = cell, X = x, Y = y, Known = true, SeesAll = all,
            Elevation = t.Elevation[cell], SeaLevel = t.SeaLevel, Water = t.Water[cell], IsWater = water,
            TemperatureC = t.Temperature[cell], Moisture = t.Moisture[cell], Fertility = t.Fertility[cell],
            MovementCost = t.MovementCost[cell], River = t.Rivers[cell] >= 0.5, KmPerPx = t.KmPerPx,
            Score = score, RegionFertility = regionFertility, FertilityRadiusPx = siting.FertilityRadiusPx,
            RegionFertilityUnseen = fertilityUnseen,
            WaterDistancePx = waterDistance, WaterAccessCutoffPx = cutoff, WaterAccess = access, WaterAccessUnseen = waterUnseen,
            Floor = floor, FloorPercentile = siting.ScoreFloorPercentile, PositiveScores = positive, ScoresBelow = below,
            Candidate = candidate,
            SiteNode = node, OccupiedBy = occupiedBy, SpacingCost = spacingCost, MinSpacingCost = f.MinSpacingCostUnits,
            MinSpacingKm = siting.MinSpacingKm, ClaimedBy = claimedBy,
            OrderedSites = batch?.OrderedCount ?? 0, OccupiedByOrder = occupiedByOrder, TooCloseByOrder = tooCloseByOrder,
            BatchSpacingCost = batchSpacingCost,
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
    /// when the arms are its own (pinned by SiteAssessmentTests over a dense sweep). Null when an arm the viewer cannot
    /// judge (<see cref="FoundingArm.Unseen"/>) comes before any failure: the predicate's answer then depends on what the
    /// viewer has not seen.</summary>
    public static FoundingVerdict? FirstFailing(ImmutableArray<FoundingArm> arms)
    {
        foreach (FoundingArm a in arms)
        {
            if (a.Unseen) return null;
            if (a.Holds == false) return a.Arm;
        }
        return FoundingVerdict.Legal;
    }

    /// <summary>
    /// Whether <paramref name="knowledge"/> sees the WHOLE world: all-known, or every lattice node VISIBLE (Visible ⊆ Known,
    /// and a settlement whose site is in sight is known, so every settlement, its catchment and its roads are). A function
    /// of the knowledge masks alone — never of what lies unseen — so the choice it makes between a value and "not judged"
    /// tells nothing either. Production is all-known until S2a.
    /// </summary>
    public static bool SeesAll(PolityKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        if (knowledge.AllKnown) return true;
        int nodes = knowledge.LatticeSize * knowledge.LatticeSize;
        for (int v = 0; v < nodes; v++) if (!knowledge.Visible(v)) return false;
        return true;
    }

    /// <summary>Whether every cell of the square ±<paramref name="r"/> about (<paramref name="x"/>, <paramref name="y"/>),
    /// clamped to the map — BoxSum's own window — is known. M5 P9: public (no behaviour change) so the resource overlay
    /// withholds a region-fertility value by THE SAME reach rule as the place card. WS3: on a wrapped world
    /// (<see cref="PolityKnowledge.WrapX"/>) the window is BoxSum's modular one — it crosses the seam, each column once.</summary>
    public static bool KnowsSquare(PolityKnowledge k, int size, int x, int y, int r)
    {
        r = Math.Max(0, r);
        int y0 = Math.Max(0, y - r), y1 = Math.Min(size - 1, y + r);
        if (!k.WrapX)
        {
            int x0 = Math.Max(0, x - r), x1 = Math.Min(size - 1, x + r);
            for (int yy = y0; yy <= y1; yy++)
                for (int xx = x0; xx <= x1; xx++)
                    if (!k.KnowsCell(yy * size + xx)) return false;
            return true;
        }
        for (int yy = y0; yy <= y1; yy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int xx = MapTopology.NeighbourX(x, dx, size, wrap: true);
                if (MapTopology.Dx(x, xx, size, wrap: true) != dx) continue;   // a column once, at its shortest offset
                if (!k.KnowsCell(yy * size + xx)) return false;
            }
        return true;
    }

    /// <summary>Whether every cell within <paramref name="r"/> 4-connected steps of (<paramref name="x"/>,
    /// <paramref name="y"/>) is known: the cells WaterDistanceBfs reads to settle a distance up to r (its expansion is
    /// 4-connected over every cell, so a cell's distance is the Manhattan distance to the nearest water or river cell —
    /// none nearer than r lies inside, and none outside can come nearer). M5 P9: public (no behaviour change), for the
    /// resource overlay's water-access reach.</summary>
    public static bool KnowsDiamond(PolityKnowledge k, int size, int x, int y, int r)
    {
        bool wrap = k.WrapX;   // WS3: the BFS crosses the seam on a wrapped world, so its reach does too
        for (int dy = -r; dy <= r; dy++)
        {
            int yy = y + dy;
            if (yy < 0 || yy >= size) continue;
            int span = r - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                int xx = MapTopology.NeighbourX(x, dx, size, wrap);
                if (xx < 0) continue;
                if (wrap && MapTopology.Dx(x, xx, size, wrap) != dx) continue;
                if (!k.KnowsCell(yy * size + xx)) return false;
            }
        }
        return true;
    }

    /// <summary>Whether every cell of the hinterland disc about (<paramref name="x"/>, <paramref name="y"/>) is known — the
    /// membership WorldFounding.HinterlandMeans reads (offsets within ±(int)radius, dx² + dy² ≤ radius², on the map). M5 P9:
    /// public (no behaviour change), for the resource overlay's deposit-channel reach.</summary>
    public static bool KnowsDisc(PolityKnowledge k, int size, int x, int y, double radiusPx)
    {
        bool wrap = k.WrapX;   // WS3: HinterlandMeans' disc wraps the seam on a wrapped world — so does its reach
        int r = (int)radiusPx;
        double r2 = radiusPx * radiusPx;
        for (int dy = -r; dy <= r; dy++)
        {
            int yy = y + dy;
            if (yy < 0 || yy >= size) continue;
            for (int dx = -r; dx <= r; dx++)
            {
                int xx = MapTopology.NeighbourX(x, dx, size, wrap);
                if (xx < 0 || dx * dx + dy * dy > r2) continue;
                if (wrap && MapTopology.Dx(x, xx, size, wrap) != dx) continue;
                if (!k.KnowsCell(yy * size + xx)) return false;
            }
        }
        return true;
    }
}
