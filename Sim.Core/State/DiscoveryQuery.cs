using Sim.Core.Pathing;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>Why an Explore order (OrderKind 14) can or cannot be applied — one verdict for DiscoverySystem, the action
/// surface and the AI (one predicate, three callers).</summary>
public enum ExploreVerdict
{
    Ok = 0,
    /// <summary>The fog is off (sim.json fog absent or disabled), or the world has no terrain to explore.</summary>
    Disabled = 1,
    /// <summary>The issuer is not a registered Empire.</summary>
    NotEmpire = 2,
    /// <summary>The issuer does not control the origin settlement (or it does not exist).</summary>
    NotControlled = 3,
    /// <summary>The settlement's explored radius is already at fog.exploreMaxRadiusNodes.</summary>
    AtCap = 4,
    /// <summary>The empire purse cannot cover fog.exploreGrain grain beyond what construction set aside.</summary>
    Unaffordable = 5,
}

/// <summary>Why a Prospect order (OrderKind 15) can or cannot be applied — one verdict for DiscoverySystem, the action
/// surface and the AI.</summary>
public enum ProspectVerdict
{
    Ok = 0,
    /// <summary>Concealment is off (sim.json discovery absent or disabled).</summary>
    Disabled = 1,
    NotEmpire = 2,
    NotControlled = 3,
    /// <summary>The issuer has already prospected this settlement (once per polity — no repeat exploit).</summary>
    AlreadySurveyed = 4,
    /// <summary>The empire purse cannot cover the prospect's tools beyond what construction set aside.</summary>
    Unaffordable = 5,
}

/// <summary>
/// M6 Track EX (D-062; ADR-044 §6–§8) — THE FOG'S GEOMETRY AND THE TWO ORDERS' PREDICATES, read by DiscoverySystem (on PREV
/// and its NEXT stocks), by <see cref="PolityKnowledge.Of(IReadOnlyWorldState, PolityId, SimConfig?)"/>, by the action
/// surface and by the AI. Pure and read-only; integer geometry on the traversal lattice (<see cref="PolityKnowledge.Grid"/>:
/// node n = y × L + x, 16 km per node on the canonical map), east-west wrap on a cylinder world, hard edges north and south;
/// no dictionary, no LINQ, no float (law 5).
///
/// <list type="bullet">
/// <item>KNOWN (rule E1/E2/E3) = the polity's stored extent (<see cref="KnownExtentRow"/>, monotone) ∪ the seed disc of
///   radius fog.seedRadiusNodes around every settlement it controls NOW — so turn 0, a colony, a revolt-born polity and a
///   migrated save know their seed before DiscoverySystem first stores it; DiscoverySystem stores the same union each step.</item>
/// <item>VISIBLE (rule E4, derived, never stored) = the node of every settlement the polity controls ∪ the disc of radius
///   fog.formationSightNodes around every formation it owns. Visible ⊆ Known (the seed covers the settlement nodes; a
///   formation's disc is added to the known set too).</item>
/// </list>
/// </summary>
public static class DiscoveryQuery
{
    /// <summary>The first canonical schema whose build ran the discovery layer (M6 Track EX, v35).</summary>
    public const int FirstDiscoverySchema = 35;

    /// <summary>
    /// Whether a SESSION recorded under schema <paramref name="schemaVersion"/> was played without the discovery layer — every
    /// session before <see cref="FirstDiscoverySchema"/>. The rule of <see cref="Kernel.TopologyFlags.RecordedFlat"/>, one
    /// schema later: a recorded session (seed + order log) is replayed under the rules it was played by, so its Found
    /// Settlement orders on land the fog would hide, and its economy without concealed deposits, replay as recorded and the
    /// identity proof (ReplayProof, in the recorded schema's form) can hold. UiSession.Resume and `sim inspect` apply it. A
    /// v34 SNAPSHOT is different: it loads into this build's rules, fogged with the seed only (ADR-044 §7).
    /// </summary>
    public static bool RecordedWithoutDiscovery(int schemaVersion) => schemaVersion < FirstDiscoverySchema;

    /// <summary><paramref name="cfg"/> with the discovery layer off (sim.json discovery and fog disabled) — the config a
    /// session <see cref="RecordedWithoutDiscovery"/> replays under.</summary>
    public static SimConfig WithoutDiscovery(SimConfig cfg)
    {
        if (cfg.Discovery is { } d) cfg = cfg with { Discovery = d with { Enabled = false } };
        if (cfg.Fog is { } f) cfg = cfg with { Fog = f with { Enabled = false } };
        return cfg;
    }

    /// <summary>Discs up to this radius are precomputed (offset tables); larger ones are built on demand.</summary>
    public const int PrecomputedRadius = 32;

    private static readonly (int Dx, int Dy)[][] Discs = BuildDiscs();

    private static (int Dx, int Dy)[][] BuildDiscs()
    {
        var all = new (int, int)[PrecomputedRadius + 1][];
        for (int r = 0; r <= PrecomputedRadius; r++) all[r] = BuildDisc(r);
        return all;
    }

    private static (int Dx, int Dy)[] BuildDisc(int r)
    {
        var list = new List<(int, int)>();
        long r2 = (long)r * r;
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if ((long)dx * dx + (long)dy * dy <= r2) list.Add((dx, dy));
        return [.. list];
    }

    /// <summary>The integer offsets of the disc of radius <paramref name="radius"/> (dx² + dy² ≤ r²), row-major.</summary>
    public static ReadOnlySpan<(int Dx, int Dy)> DiscOffsets(int radius) =>
        radius < 0 ? [] : radius <= PrecomputedRadius ? Discs[radius] : BuildDisc(radius);

    /// <summary>Whether the fog is in force for <paramref name="cfg"/>.</summary>
    public static bool FogOn(SimConfig? cfg) => cfg?.Fog is { Enabled: true };

    /// <summary>The knowledge lattice of <paramref name="world"/>: nodes per side, block px, terrain px, wrap; L = 0 without terrain.</summary>
    public static (int L, int Block, int Size, bool Wrap) Lattice(IReadOnlyWorldState world)
    {
        if (world.Terrain is not { } t) return (0, 0, 0, false);
        (int l, int b) = PolityKnowledge.Grid(t.Size);
        return (l, b, t.Size, t.WrapX);
    }

    /// <summary>The 64-bit words of a known-extent bitset over an L × L lattice.</summary>
    public static int WordCount(int latticeSize) => (latticeSize * latticeSize + 63) >> 6;

    /// <summary>The lattice node of terrain cell <paramref name="cell"/>, or −1.</summary>
    public static int NodeOfCell(IReadOnlyWorldState world, int cell)
    {
        (int l, int b, int size, _) = Lattice(world);
        if (l == 0 || cell < 0 || cell >= size * size) return -1;
        return MapTopology.NodeOfCell(cell, size, l, b);
    }

    /// <summary>The lattice node of settlement <paramref name="settlement"/>'s site, or −1 (no such settlement, no terrain).</summary>
    public static int NodeOfSettlement(IReadOnlyWorldState world, SettlementId settlement)
    {
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id.Value == settlement.Value) return NodeOfCell(world, world.Settlements[i].SiteCell);
        return -1;
    }

    /// <summary>Sets bit <paramref name="node"/>.</summary>
    public static void Set(ulong[] bits, int node) => bits[node >> 6] |= 1UL << (node & 63);

    /// <summary>Whether bit <paramref name="node"/> is set (false off the lattice).</summary>
    public static bool Has(ulong[]? bits, int node) =>
        bits is not null && node >= 0 && (node >> 6) < bits.Length && (bits[node >> 6] & (1UL << (node & 63))) != 0;

    /// <summary>ORs the disc of radius <paramref name="radius"/> around node <paramref name="center"/> into <paramref name="bits"/>
    /// (wrap-aware in x when <paramref name="wrap"/>; clipped at the lattice's edges otherwise and always in y).</summary>
    public static void OrDisc(ulong[] bits, int latticeSize, bool wrap, int center, int radius)
    {
        if (center < 0 || latticeSize <= 0) return;
        int cy = center / latticeSize, cx = center - cy * latticeSize;
        foreach ((int dx, int dy) in DiscOffsets(radius))
        {
            int y = cy + dy;
            if (y < 0 || y >= latticeSize) continue;   // north and south are hard edges
            int col = MapTopology.NeighbourX(cx, dx, latticeSize, wrap);   // the seam (wrapped) or the edge (flat: −1)
            if (col < 0) continue;
            Set(bits, y * latticeSize + col);
        }
    }

    /// <summary>Whether node <paramref name="node"/> lies in the disc of radius <paramref name="radius"/> around
    /// <paramref name="center"/> (the same wrap rule as <see cref="OrDisc"/>).</summary>
    public static bool InDisc(int latticeSize, bool wrap, int center, int radius, int node)
    {
        if (center < 0 || node < 0 || latticeSize <= 0) return false;
        int ny = node / latticeSize, cy = center / latticeSize;
        int dx = MapTopology.AbsDx(node - ny * latticeSize, center - cy * latticeSize, latticeSize, wrap);
        int dy = ny - cy;
        return (long)dx * dx + (long)dy * dy <= (long)radius * radius;
    }

    /// <summary>The polity's STORED extent (its <see cref="KnownExtentRow"/>s) as a bitset of <paramref name="words"/> words.</summary>
    public static ulong[] StoredExtent(IReadOnlyWorldState world, PolityId polity, int words)
    {
        var bits = new ulong[words];
        for (int i = 0; i < world.KnownExtents.Count; i++)
        {
            KnownExtentRow row = world.KnownExtents[i];
            if (row.Polity.Value == polity.Value && (uint)row.Word < (uint)words) bits[row.Word] |= row.Bits;
        }
        return bits;
    }

    /// <summary>The settlements <paramref name="polity"/> controls, in Settlements-table order.</summary>
    public static SettlementId[] Controlled(IReadOnlyWorldState world, PolityId polity)
    {
        var list = new List<SettlementId>();
        for (int i = 0; i < world.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(world, polity, world.Settlements[i].Id)) list.Add(world.Settlements[i].Id);
        return [.. list];
    }

    /// <summary>KNOWN: the stored extent ∪ the seed discs of the polity's controlled settlements ∪ its formations' sight
    /// discs (see the type's remarks). Null without terrain.</summary>
    public static ulong[]? KnownBits(IReadOnlyWorldState world, FogConfig fog, PolityId polity)
    {
        (int l, _, _, bool wrap) = Lattice(world);
        if (l == 0) return null;
        ulong[] bits = StoredExtent(world, polity, WordCount(l));
        foreach (SettlementId s in Controlled(world, polity))
            OrDisc(bits, l, wrap, NodeOfSettlement(world, s), fog.SeedRadiusNodes);
        OrFormationSight(world, fog, polity, bits, l, wrap);
        return bits;
    }

    /// <summary>VISIBLE: the node of every settlement the polity controls ∪ the sight disc of every formation it owns. Null
    /// without terrain.</summary>
    public static ulong[]? VisibleBits(IReadOnlyWorldState world, FogConfig fog, PolityId polity)
    {
        (int l, _, _, bool wrap) = Lattice(world);
        if (l == 0) return null;
        var bits = new ulong[WordCount(l)];
        foreach (SettlementId s in Controlled(world, polity))
        {
            int node = NodeOfSettlement(world, s);
            if (node >= 0) Set(bits, node);
        }
        OrFormationSight(world, fog, polity, bits, l, wrap);
        return bits;
    }

    private static void OrFormationSight(IReadOnlyWorldState world, FogConfig fog, PolityId polity, ulong[] bits, int l, bool wrap)
    {
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow unit = world.MilitaryUnits[i];
            if (unit.Owner.Value != polity.Value) continue;
            OrDisc(bits, l, wrap, FormationNode(world, unit), fog.FormationSightNodes);
        }
    }

    /// <summary>Where a formation stands: its station's node when stationed in a settlement, else the node under its
    /// position (wrapped in x on a cylinder); −1 when off the map.</summary>
    public static int FormationNode(IReadOnlyWorldState world, in MilitaryUnitRow unit)
    {
        if (unit.Location.Value >= 0) return NodeOfSettlement(world, unit.Location);
        // The seam's own position rule (PolityKnowledge.NodeAt: wrapped columns normalised, a flat map's edge refused).
        return PolityKnowledge.Of(world, unit.Owner, null).NodeAt(unit.X, unit.Y);
    }

    /// <summary>Whether a sighting row (<paramref name="polity"/>, <paramref name="settlement"/>) exists.</summary>
    public static bool HasSighting(IReadOnlyWorldState world, PolityId polity, SettlementId settlement)
    {
        for (int i = 0; i < world.SettlementSightings.Count; i++)
        {
            SettlementSightingRow row = world.SettlementSightings[i];
            if (row.Polity.Value == polity.Value && row.Settlement.Value == settlement.Value) return true;
        }
        return false;
    }

    /// <summary>The explored radius around <paramref name="settlement"/> for <paramref name="polity"/>: its
    /// <see cref="ExplorationRow"/>, else fog.seedRadiusNodes.</summary>
    public static int ExploredRadius(IReadOnlyWorldState world, FogConfig fog, PolityId polity, SettlementId settlement)
    {
        for (int i = 0; i < world.Explorations.Count; i++)
        {
            ExplorationRow row = world.Explorations[i];
            if (row.Polity.Value == polity.Value && row.Settlement.Value == settlement.Value) return row.RadiusNodes;
        }
        return fog.SeedRadiusNodes;
    }

    /// <summary>The radius one more Explore from <paramref name="settlement"/> would reach (capped).</summary>
    public static int NextRadius(IReadOnlyWorldState world, FogConfig fog, PolityId polity, SettlementId settlement) =>
        Math.Min(fog.ExploreMaxRadiusNodes, ExploredRadius(world, fog, polity, settlement) + fog.ExploreStepNodes);

    /// <summary>The grain one Explore sinks (fog.exploreGrain; 0 with the fog off).</summary>
    public static long ExploreCost(SimConfig cfg) => cfg.Fog?.ExploreGrain ?? 0;

    /// <summary>The tools one Prospect by <paramref name="polity"/> sinks: discovery.prospectTools, HALVED (integer) once the
    /// polity's completed knowledge meets discovery.prospectDiscountRequires (the ore-recognition technology).</summary>
    public static long ProspectCost(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Discovery is not { } d) return 0;
        return HasProspectDiscount(world, cfg, polity) ? d.ProspectTools / 2 : d.ProspectTools;
    }

    /// <summary>Whether <paramref name="polity"/> holds the prospecting discount technology.</summary>
    public static bool HasProspectDiscount(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Discovery?.ProspectDiscountRequires is not { } req || cfg.Research is not { } research) return false;
        Systems.ClassMobility.Predicate requirement = ResearchContentLoader.ParseRequirement(research, req, "sim.json discovery.prospectDiscountRequires");
        return ResearchQuery.RequirementMet(research, requirement, ResearchQuery.CompletedMask(world, research, polity));
    }

    /// <summary>
    /// What the purse of <paramref name="settlement"/> (EmpireStockpile.Holders: the settlement first, then its controller's
    /// other settlements nearest-first) holds of <paramref name="good"/> BEYOND what construction set aside
    /// (<paramref name="holds"/>; RoadDevelopmentQuery.SetAsideWithin — the floors road works honour, so an expedition or a
    /// prospect never takes what a queued project holds). <paramref name="keep"/> is the per-holder floor for the draw.
    /// </summary>
    public static long FreeHolding(
        IReadOnlyWorldState prev, SimConfig cfg, IReadOnlyTable<GoodStockRow> stocks, IReadOnlyTable<ConstructionHoldRow> holds,
        SettlementId[] holders, GoodId good, out long[]? keep)
    {
        keep = null;
        long have = EmpireStockpile.Available(stocks, holders, good);
        return Math.Max(0, have - RoadDevelopmentQuery.SetAsideWithin(prev, cfg, holds, holders, good, ref keep));
    }

    /// <summary>THE EXPLORE PREDICATE on <paramref name="world"/> (PREV in the system), with the purse read from
    /// <paramref name="stocks"/> less <paramref name="holds"/>.</summary>
    public static ExploreVerdict CheckExplore(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, SettlementId settlement,
        IReadOnlyTable<GoodStockRow> stocks, IReadOnlyTable<ConstructionHoldRow> holds)
    {
        if (cfg.Fog is not { Enabled: true } fog || world.Terrain is null || cfg.Goods is null) return ExploreVerdict.Disabled;
        if (world.Polities.Count > 0 && !EmpireQuery.TryGetCommandSource(world, polity, out _)) return ExploreVerdict.NotEmpire;
        if (NodeOfSettlement(world, settlement) < 0 || !EmpireQuery.ControlsSettlement(world, polity, settlement))
            return ExploreVerdict.NotControlled;
        if (ExploredRadius(world, fog, polity, settlement) >= fog.ExploreMaxRadiusNodes) return ExploreVerdict.AtCap;
        SettlementId[] holders = EmpireStockpile.Holders(world, settlement);
        if (FreeHolding(world, cfg, stocks, holds, holders, new GoodId(cfg.Goods.GrainId), out _) < ExploreCost(cfg))
            return ExploreVerdict.Unaffordable;
        return ExploreVerdict.Ok;
    }

    /// <summary>THE EXPLORE PREDICATE against the world's own published stocks and set-asides (the UI and the AI).</summary>
    public static ExploreVerdict CheckExplore(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, SettlementId settlement) =>
        CheckExplore(world, cfg, polity, settlement, world.GoodStocks, world.ConstructionHolds);

    /// <summary>THE PROSPECT PREDICATE on <paramref name="world"/>, with the purse read from <paramref name="stocks"/> less
    /// <paramref name="holds"/>.</summary>
    public static ProspectVerdict CheckProspect(
        IReadOnlyWorldState world, SimConfig cfg, PolityId polity, SettlementId settlement,
        IReadOnlyTable<GoodStockRow> stocks, IReadOnlyTable<ConstructionHoldRow> holds, long? cost = null)
    {
        if (!DepositConcealment.Enabled(cfg)) return ProspectVerdict.Disabled;
        if (world.Polities.Count > 0 && !EmpireQuery.TryGetCommandSource(world, polity, out _)) return ProspectVerdict.NotEmpire;
        bool exists = false;
        for (int i = 0; i < world.Settlements.Count; i++) if (world.Settlements[i].Id.Value == settlement.Value) { exists = true; break; }
        if (!exists || !EmpireQuery.ControlsSettlement(world, polity, settlement)) return ProspectVerdict.NotControlled;
        if (DepositConcealment.HasSurveyed(world, polity, settlement)) return ProspectVerdict.AlreadySurveyed;
        int tools = cfg.Goods!.IdOf(DepositConcealment.ProspectGood);
        SettlementId[] holders = EmpireStockpile.Holders(world, settlement);
        if (tools < 0 || FreeHolding(world, cfg, stocks, holds, holders, new GoodId(tools), out _) < (cost ?? ProspectCost(world, cfg, polity)))
            return ProspectVerdict.Unaffordable;
        return ProspectVerdict.Ok;
    }

    /// <summary>THE PROSPECT PREDICATE against the world's own published stocks and set-asides (the UI and the AI).</summary>
    public static ProspectVerdict CheckProspect(IReadOnlyWorldState world, SimConfig cfg, PolityId polity, SettlementId settlement,
        long? cost = null) =>
        CheckProspect(world, cfg, polity, settlement, world.GoodStocks, world.ConstructionHolds, cost);

    /// <summary>The player-facing reason an Explore verdict blocks, or null for <see cref="ExploreVerdict.Ok"/>.</summary>
    public static string? ExploreBlocker(ExploreVerdict v, SimConfig cfg) => v switch
    {
        ExploreVerdict.Ok => null,
        ExploreVerdict.AtCap => "explored to the limit (" + Inv(cfg.Fog?.ExploreMaxRadiusNodes ?? 0) + " nodes)",
        ExploreVerdict.Unaffordable => "not enough grain (needs " + Inv(ExploreCost(cfg)) + ")",
        ExploreVerdict.NotControlled => "not your settlement",
        ExploreVerdict.NotEmpire => "not an Empire",
        _ => "exploration is off",
    };

    /// <summary>The player-facing reason a Prospect verdict blocks, or null for <see cref="ProspectVerdict.Ok"/>.</summary>
    public static string? ProspectBlocker(ProspectVerdict v, long cost) => v switch
    {
        ProspectVerdict.Ok => null,
        ProspectVerdict.AlreadySurveyed => "already surveyed",
        ProspectVerdict.Unaffordable => "not enough tools (needs " + Inv(cost) + ")",
        ProspectVerdict.NotControlled => "not your settlement",
        ProspectVerdict.NotEmpire => "not an Empire",
        _ => "prospecting is off",
    };

    /// <summary>
    /// The structural invariant of the four v35 tables, as only DiscoverySystem writes them (Snapshot.Load refuses a save
    /// breaking it): each table in strictly ascending key order — KnownExtents (Polity, Word) with Word ≥ 0 and Bits ≠ 0;
    /// Explorations, SettlementSightings and SettlementSurveys (Polity, Settlement) — radii ≥ 0, turns ≥ 0. Null when it holds.
    /// </summary>
    public static string? InvariantViolation(IReadOnlyWorldState world) =>
        InvariantViolation(world.KnownExtents, world.Explorations, world.SettlementSightings, world.SettlementSurveys);

    /// <summary>As <see cref="InvariantViolation(IReadOnlyWorldState)"/>, over the four tables given.</summary>
    public static string? InvariantViolation(
        IReadOnlyTable<KnownExtentRow> known, IReadOnlyTable<ExplorationRow> explorations,
        IReadOnlyTable<SettlementSightingRow> sightings, IReadOnlyTable<SettlementSurveyRow> surveys)
    {
        for (int i = 0; i < known.Count; i++)
        {
            KnownExtentRow r = known[i];
            if (r.Word < 0 || r.Bits == 0) return $"KnownExtents row {i} has word {r.Word} and bits {r.Bits} (a stored word is ≥ 0 and non-zero)";
            if (i > 0)
            {
                KnownExtentRow p = known[i - 1];
                if (p.Polity.Value > r.Polity.Value || (p.Polity.Value == r.Polity.Value && p.Word >= r.Word))
                    return $"KnownExtents rows {i - 1} and {i} are not in strictly ascending (Polity, Word)";
            }
        }
        for (int i = 0; i < explorations.Count; i++)
        {
            ExplorationRow r = explorations[i];
            if (r.RadiusNodes < 0) return $"Explorations row {i} has a negative radius";
            if (i > 0 && !Ascending(explorations[i - 1].Polity, explorations[i - 1].Settlement, r.Polity, r.Settlement))
                return $"Explorations rows {i - 1} and {i} are not in strictly ascending (Polity, Settlement)";
        }
        for (int i = 0; i < sightings.Count; i++)
        {
            SettlementSightingRow r = sightings[i];
            if (r.LastSeenTurn < 0) return $"SettlementSightings row {i} has a negative turn";
            if (i > 0 && !Ascending(sightings[i - 1].Polity, sightings[i - 1].Settlement, r.Polity, r.Settlement))
                return $"SettlementSightings rows {i - 1} and {i} are not in strictly ascending (Polity, Settlement)";
        }
        for (int i = 0; i < surveys.Count; i++)
        {
            SettlementSurveyRow r = surveys[i];
            if (r.Turn < 0) return $"SettlementSurveys row {i} has a negative turn";
            if (i > 0 && !Ascending(surveys[i - 1].Polity, surveys[i - 1].Settlement, r.Polity, r.Settlement))
                return $"SettlementSurveys rows {i - 1} and {i} are not in strictly ascending (Polity, Settlement)";
        }
        return null;
    }

    private static bool Ascending(PolityId ap, SettlementId @as, PolityId bp, SettlementId bs) =>
        ap.Value < bp.Value || (ap.Value == bp.Value && @as.Value < bs.Value);

    private static string Inv(long v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
