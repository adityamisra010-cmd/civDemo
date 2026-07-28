namespace Sim.Core.Pathing;

/// <summary>
/// THE denomination chokepoint (T3.2b, CR-002). Every conversion between
/// LATTICE units (nodes, cost units) and PHYSICAL units (km, km²) lives here
/// and NOWHERE else — see the gate in scripts/check-banned-constructs.sh, which
/// fails the build if <see cref="TraversalLattice.KmPerNode"/> is read outside
/// this file.
///
/// WHY THIS FILE EXISTS. Before T3.2b, <c>CatchmentSummaryRow.EffectiveFarmland</c>
/// held a sum of per-node MEAN fertilities — a count of fertility-weighted
/// NODES, not an area. Two consumers read the same field in two different
/// denominations: AutoplayMetrics multiplied by the 256 km² block area to report
/// arable km²; FarmingSystem multiplied by yieldPerFarmlandPerYear WITHOUT
/// converting, so that constant was silently per-NODE. The land side of the
/// Leontief harvest was therefore denominated 256× coarser than anybody reading
/// the tuning file would assume, which is how a yield of 28 survived three
/// milestones while implying ~9 km² of land per person. The fault was not the
/// number; it was that a denominated quantity travelled through the state
/// tables under a name that did not say what it was denominated in.
///
/// THE RULE THIS FILE ENFORCES, in two halves:
///   1. Every denominated identifier carries its unit in its NAME —
///      EffectiveArableKm2, YieldPerArableKm2PerYear, HinterlandRadiusKm,
///      KmPerNode, BlockAreaKm2. A bare "farmland" or "budget" is a bug.
///   2. Every conversion between the two unit systems goes through a named
///      method HERE, so a reviewer can enumerate the conversion sites by
///      reading one file rather than grepping the tree.
///
/// THE TWO UNIT SYSTEMS, stated once.
///
/// AREA. A lattice node covers a stride×stride terrain block =
/// <see cref="TraversalLattice.KmPerNode"/> km on a side. Terrain fertility is a
/// dimensionless agronomic-suitability index in [0,1]; a node's mean fertility
/// is therefore also dimensionless, and only becomes an AREA when multiplied by
/// the block area. "Fertility-weighted km²" (arable km²) means: the land content
/// of a hypothetical ideal-suitability (f = 1.0) km², summed over the block.
///
/// DISTANCE. Pathfinding costs are in COST UNITS: one cardinal lattice step
/// costs mean(nodeCost) × 1, so a step across ideal (movement-cost 1.0) ground
/// costs exactly 1.0 and covers KmPerNode km. Hence 1 cost unit = KmPerNode km
/// ON IDEAL GROUND, and strictly less on real ground — the canonical map's
/// median passable node cost is ≈1.11, so a cost unit buys ≈14.4 km rather than
/// 16 km there, and far less through mountain or marsh. Built paths and river
/// corridors cut node cost, so the same budget reaches FURTHER along them. That
/// asymmetry is the mechanism (law 2), not a modifier: a travel budget is stated
/// as an ideal-ground radius and geography decides what it actually buys.
/// </summary>
public static class LatticeGeometry
{
    /// <summary>Area of one lattice node's terrain block, km².</summary>
    public static double BlockAreaKm2(TraversalLattice lattice) =>
        lattice.KmPerNode * lattice.KmPerNode;

    /// <summary>
    /// The one node→area conversion: a node's dimensionless MEAN fertility
    /// (agronomic-suitability index, [0,1]) becomes fertility-weighted km².
    /// </summary>
    public static double ArableKm2(double blockMeanFertility, TraversalLattice lattice) =>
        blockMeanFertility * BlockAreaKm2(lattice);

    /// <summary>
    /// Kilometres bought by one cost unit ON IDEAL GROUND (movement cost 1.0).
    /// An UPPER bound on real ground: rough terrain costs more per km, built
    /// paths and rivers cost less.
    /// </summary>
    public static double KmPerCostUnitOnIdealGround(TraversalLattice lattice) =>
        lattice.KmPerNode;

    /// <summary>
    /// The one km→cost conversion: a travel budget stated as an IDEAL-GROUND
    /// radius in km becomes the pathfinder's cost budget. Real reach is shorter
    /// through difficult country and longer along fast lanes — by construction.
    /// </summary>
    public static double CostUnitsForIdealGroundKm(TraversalLattice lattice, double km) =>
        km / KmPerCostUnitOnIdealGround(lattice);

    /// <summary>Inverse of <see cref="CostUnitsForIdealGroundKm"/> — for reporting.</summary>
    public static double IdealGroundKmForCostUnits(TraversalLattice lattice, double costUnits) =>
        costUnits * KmPerCostUnitOnIdealGround(lattice);
}
