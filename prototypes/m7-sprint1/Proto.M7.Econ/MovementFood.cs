using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Proto.M7.Econ;

/// <summary>
/// Every number the movement-food estimate uses, READ from the shipped configuration — none is typed in here.
/// Each property names the key it comes from (implementation-spec §6, "cite every number's key").
/// </summary>
public sealed record MovementParams(
    // sim.json military.upkeepGrainPerPersonYear (1.0, DERIVED: one grain unit = one adult's year of food).
    double RationGrainPerPersonYear,
    // sim.json trade.costPerBulkCostUnit (0.16 grain-value per bulk unit per 16-km cost unit, DERIVED).
    double HaulGrainPerBulkCostUnit,
    // goods.json grain bulkPerUnit (1.0).
    double GrainBulkPerUnit,
    // sim.json roads.baselineKmPerDay (40, TUNE placeholder per its own _doc).
    double BaselineKmPerDay,
    // Ideal-ground km per lattice cost unit of THIS world: worldgen kmPerPx 4.0 × lattice stride 4 = 16
    // (TransportQuery.KmPerCostUnit / LatticeGeometry.KmPerCostUnitOnIdealGround).
    double KmPerCostUnit,
    // The kernel's year, SimClock.YearDays = 360 (TransportQuery.DaysPerYear = 365.25 is a second, inconsistent
    // constant — recorded as an open item in the audit; the kernel clock that integrates upkeep is used here).
    long DaysPerYear,
    // sim.json transport.riverCostFactor (0.20) — the lattice the Pathfinder runs on is built with it.
    double RiverCostFactor)
{
    public static MovementParams From(SimConfig cfg, IReadOnlyWorldState world)
    {
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("SimConfig.Goods (goods.json) is required.");
        MilitaryConfig military = cfg.Military ?? throw new ArgumentException("sim.json military block is required.");
        return new MovementParams(
            military.UpkeepGrainPerPersonYear,
            cfg.Trade.CostPerBulkCostUnit,
            goods.ById(goods.GrainId).BulkPerUnit,
            (cfg.Roads ?? throw new ArgumentException("sim.json roads block is required.")).BaselineKmPerDay,
            TransportQuery.KmPerCostUnit(world),
            SimClock.YearDays,
            cfg.Transport.RiverCostFactor);
    }
}

/// <summary>How the haul (carriage) cost is charged on the grain a column carries. NO DEFAULT: the choice is a modelling
/// decision the repository has not made; both are reported. FullLoad charges the rations at their starting weight over the
/// whole route (an upper bound); MeanLoad charges half of it (rations are eaten at a constant rate on the way).</summary>
public enum HaulBasis
{
    FullLoad = 1,
    MeanLoad = 2,
}

/// <summary>One column's route and its food bill. Units: cost units (lattice), km, days, grain units (whole, rounded UP).</summary>
public readonly record struct MovementEstimate(
    bool Reachable,
    int PathNodes,
    double RouteCostUnits,
    double RouteKmEquivalent,
    double StraightLineKm,
    double TravelDays,
    long Personnel,
    long RationUnits,
    long HaulUnits)
{
    /// <summary>All grain the move consumes = rations eaten on the road + haul.</summary>
    public long TotalUnits => checked(RationUnits + HaulUnits);

    /// <summary>The part of the bill that is EXTRA relative to the column staying at home. The rations are not: a soldier
    /// already eats upkeepGrainPerPersonYear every year wherever the formation is (MilitarySystem.Upkeep), so moving does
    /// not change what he eats — only the carriage is new.</summary>
    public long IncrementalUnits => HaulUnits;

    /// <summary>Terrain penalty: route cost-equivalent km per straight-line km (1.0 = ideal flat ground in a straight line).</summary>
    public double TerrainFactor => StraightLineKm > 0.0 ? RouteKmEquivalent / StraightLineKm : 0.0;
}

/// <summary>
/// MOVEMENT FOOD over the REAL lattice. Pure functions over a founded WorldState: nothing is written.
/// <list type="number">
/// <item>Route: <see cref="Pathfinder.FindPath"/> on <c>TraversalLattice.Build(world.Terrain, transport.riverCostFactor)</c>
/// plus the world's network lanes (PathBuild dirt paths, travelled roads) — the same lattice and overlay CatchmentSystem's
/// pairwise SettlementDistances come from. Terrain enters ONLY through that lattice: node cost 1.0 + 60 × slope on land,
/// 25.0 on water (worldgen.json movement), river blend, roads as cheaper lanes. No forest/jungle class exists (BLOCKER).</item>
/// <item>Travel days = route cost × kmPerCostUnit ÷ roads.baselineKmPerDay — the definition TransportQuery.EstimateFreight
/// uses for TransitDays on the free baseline (DirtPath speedFactor 1.0). A hypothetical road class instead prices the
/// straight-line km × that class's speedFactor (RoadPerformance.TravelKm: LengthKm × cost factor), see
/// <see cref="RoadDays"/>.</item>
/// <item>Rations = ceil(personnel × upkeepGrainPerPersonYear × days ÷ DaysPerYear), evaluated exactly in decimal.</item>
/// <item>Haul = ceil(carried grain × grain bulk × route cost units × trade.costPerBulkCostUnit), evaluated exactly in decimal, carried grain per
/// <see cref="HaulBasis"/>.</item>
/// </list>
/// Rounding: every whole-unit amount rounds UP (a column cannot set out with a fraction short). Doubles are used only
/// for the rates the repository itself stores as doubles (Law 7); every returned stock is a long.
/// </summary>
public static class MovementFood
{
    /// <summary>The lattice the Pathfinder runs on for <paramref name="world"/>.</summary>
    public static TraversalLattice Lattice(IReadOnlyWorldState world, MovementParams p) =>
        TraversalLattice.Build(world.Terrain ?? throw new ArgumentException("the world has no terrain"), p.RiverCostFactor);

    /// <summary>The lattice node a settlement stands on (CatchmentSystem's origin rule, LatticeMap.OriginLatticeNode).</summary>
    public static int NodeOf(IReadOnlyWorldState world, TraversalLattice lattice, SettlementId settlement)
    {
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id == settlement)
                return LatticeMap.OriginLatticeNode(lattice, world.Terrain!.Size, world.Settlements[i].SiteCell);
        throw new ArgumentException("no settlement " + settlement.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>The site cell of a settlement.</summary>
    public static int SiteOf(IReadOnlyWorldState world, SettlementId settlement)
    {
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id == settlement) return world.Settlements[i].SiteCell;
        throw new ArgumentException("no settlement " + settlement.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>The food bill of moving <paramref name="personnel"/> people from one settlement to another.</summary>
    public static MovementEstimate Estimate(
        IReadOnlyWorldState world, MovementParams p, TraversalLattice lattice,
        SettlementId from, SettlementId to, long personnel, HaulBasis basis)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(personnel);
        int a = NodeOf(world, lattice, from), b = NodeOf(world, lattice, to);
        Pathfinder.PathResult path = Pathfinder.FindPath(lattice, world, a, b);
        GeographicDistance.TryIdealGroundCostUnits(world, SiteOf(world, from), SiteOf(world, to), p.KmPerCostUnit, out double straightUnits);
        double straightKm = straightUnits * p.KmPerCostUnit;
        if (!path.Found) return new MovementEstimate(false, 0, double.PositiveInfinity, double.PositiveInfinity, straightKm, double.PositiveInfinity, personnel, 0, 0);
        return FromRouteCost(p, path.Nodes.Length, path.TotalCost, straightKm, personnel, basis);
    }

    /// <summary>The bill for a route of <paramref name="routeCostUnits"/> (pure arithmetic; the tests pin it).</summary>
    public static MovementEstimate FromRouteCost(
        MovementParams p, int pathNodes, double routeCostUnits, double straightKm, long personnel, HaulBasis basis)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(personnel);
        if (!(routeCostUnits >= 0.0) || !double.IsFinite(routeCostUnits)) throw new ArgumentOutOfRangeException(nameof(routeCostUnits));
        double km = routeCostUnits * p.KmPerCostUnit;
        double days = km / p.BaselineKmPerDay;
        // The two whole-unit amounts are computed in System.Decimal (verification F2): the JSON parameters are decimal
        // literals (0.16 is not a binary fraction) and a chain of double products can land a hair ABOVE an exact integer
        // (225 × 17.6 / 360 = 11.000000000000002), which a ceiling turns into one unit too many. The decimal conversion of
        // each double keeps its 15 significant digits; the products are exact and the ONE division is last, so an
        // integer-valued bill stays an integer. Decimal arithmetic is software-implemented, hence platform-deterministic.
        decimal routeCost = (decimal)routeCostUnits;
        decimal rationAmount = personnel * (decimal)p.RationGrainPerPersonYear * routeCost * (decimal)p.KmPerCostUnit
            / ((decimal)p.BaselineKmPerDay * p.DaysPerYear);
        long rations = CeilUnits(rationAmount);
        decimal carried = basis switch
        {
            HaulBasis.FullLoad => rations,
            HaulBasis.MeanLoad => rations / 2m,
            _ => throw new ArgumentOutOfRangeException(nameof(basis)),
        };
        long haul = CeilUnits(carried * (decimal)p.GrainBulkPerUnit * routeCost * (decimal)p.HaulGrainPerBulkCostUnit);
        return new MovementEstimate(true, pathNodes, routeCostUnits, km, straightKm, days, personnel, rations, haul);
    }

    /// <summary>Travel days on a road of class <paramref name="edgeType"/> laid along the straight line (the price a built
    /// road lane carries: RoadPerformance.TravelKm = LengthKm × the class speedFactor; ADR-032). DirtPath (1) = 1.0.</summary>
    public static double RoadDays(SimConfig cfg, MovementParams p, double straightKm, int edgeType)
    {
        RoadClassConfig cls = cfg.Roads?.ClassOf(edgeType) ?? throw new ArgumentOutOfRangeException(nameof(edgeType));
        return straightKm * cls.SpeedFactor / p.BaselineKmPerDay;
    }

    /// <summary>Exact ceiling of a non-negative decimal amount to whole units (law 1: stocks are long).</summary>
    public static long CeilUnits(decimal amount)
    {
        if (amount < 0m) throw new ArgumentOutOfRangeException(nameof(amount));
        decimal c = decimal.Ceiling(amount);
        if (c > long.MaxValue / 2) throw new OverflowException("amount too large");
        return (long)c;
    }

    /// <summary>Exact ceiling of a non-negative finite double to whole units (law 1: stocks are long). Exact for the double
    /// it is given; it cannot undo rounding already in that double — see <see cref="FromRouteCost"/>.</summary>
    public static long CeilUnits(double amount)
    {
        if (!(amount >= 0.0) || !double.IsFinite(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
        double c = Math.Ceiling(amount);
        if (c > long.MaxValue / 2) throw new OverflowException("amount too large");
        return (long)c;
    }
}
