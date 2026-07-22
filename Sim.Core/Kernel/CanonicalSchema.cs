using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// THE canonical serialization chokepoint (§3.8, ADR-005). Every table and every
/// field of WorldState is written here, FIELD BY FIELD, in the fixed order listed
/// in this one reviewable file. No reflection (.NET guarantees no member order),
/// no raw struct memory / MemoryMarshal (padding bytes are a determinism hazard;
/// memcpy stays licensed for Clone() only — ADR-001). Doubles serialize as raw
/// IEEE-754 bits with NO normalization of -0.0/NaN: bit-exactness is the point.
/// BinaryWriter is explicitly little-endian for all primitives.
///
/// ADDING STATE? Three edits, same file: (1) write in Write, (2) read in Read,
/// (3) width in ExpectedLength. The anti-padding length test and the golden hash
/// will fail loudly until all three agree — that is their job.
/// </summary>
public static class CanonicalSchema
{
    /// <summary>Bumped on ANY schema change. Saves break between milestones (D-008).
    /// v2 (T1.1, ADR-008): terrain presence flag + terrain content hash after the clock.
    /// v3 (T1.3): NetworkNodes + NetworkEdges tables after LedgerFlows.
    /// v4 (T1.4): Settlements, NetworkMeta, CatchmentNodes, CatchmentSummaries after NetworkEdges.
    /// v5 (T1.5): PopBands, FoodStores, ConsumptionDeficits after CatchmentSummaries.
    /// v6 (T1.6): LaborAllocations, PathProgress after ConsumptionDeficits.</summary>
    public const int Version = 6;

    // Fixed field widths per row, in bytes — the anti-padding proof sums these.
    private const int CountPrefixWidth = 4;              // int row count per table
    private const int RegionRowWidth = 4;                // RegionId
    private const int RngStreamRowWidth = 4 + 4 + 8 + 8; // SystemId, RegionId, State, Inc
    private const int RainfallRowWidth = 4 + 8;          // RegionId, rainfall bits
    private const int BiomassRowWidth = 4 + 8 + 8;       // RegionId, stock, remainder bits
    private const int GoodsRowWidth = 4 + 8;             // RegionId, stock
    private const int LedgerFlowRowWidth = 4 + 4 + 8 + 8; // Quantity, Reason, sourced, sunk
    private const int NetworkNodeRowWidth = 4 + 4;        // Id, LatticeNode
    private const int NetworkEdgeRowWidth = 4 + 4 + 4 + 4 + 8; // Id, A, B, EdgeType, Cost bits
    private const int SettlementRowWidth = 4 + 4 + 8;          // Id, SiteCell, FoundedTurn
    private const int NetworkMetaRowWidth = 4;                 // Revision
    private const int CatchmentNodeRowWidth = 4 + 4 + 8;       // Settlement, LatticeNode, TravelCost bits
    private const int CatchmentSummaryRowWidth = 4 + 4 + 8 + 4 + 8; // Settlement, NodeCount, EffectiveFarmland bits, NetworkRevision, LastRecomputeTurn
    private const int PopBandRowWidth = 4 + 4 + 8 + 8 + 8 + 8 + 8;  // Settlement, Band, Count, 4 remainder bit-fields
    private const int FoodStoreRowWidth = 4 + 8 + 8 + 8;            // Settlement, Store, 2 remainder bit-fields
    private const int ConsumptionDeficitRowWidth = 4 + 8;           // Settlement, DeficitRatio bits
    private const int LaborAllocationRowWidth = 4 + 8;              // Settlement, FarmShare bits
    private const int PathProgressRowWidth = 4 + 8 + 4;             // Settlement, Banked bits, FrontierNode
    private const int SeedWidth = 8;
    private const int ClockWidth = 8 + 8 + 8;            // Turn, SimDays, DtDays

    /// <summary>Writes the complete canonical state stream (schema order, declaration order).</summary>
    public static void Write(WorldState world, BinaryWriter writer)
    {
        // 1. Seed
        writer.Write(world.Seed);

        // 2. Clock
        writer.Write(world.Clock.Turn);
        writer.Write(world.Clock.SimDays);
        writer.Write(world.Clock.DtDays);

        // 2b. Terrain (ADR-008): the immutable rasters never serialize per-state;
        // their once-computed content hash is folded in, so worlds on different
        // terrain can never hash equal, and a save binds to its terrain.
        writer.Write(world.Terrain is not null);
        if (world.Terrain is not null)
            writer.Write(world.Terrain.ContentHash);

        // 3. Regions
        writer.Write(world.Regions.Count);
        for (int i = 0; i < world.Regions.Count; i++)
            writer.Write(world.Regions[i].Id.Value);

        // 4. RNG streams
        writer.Write(world.RngStreams.Count);
        for (int i = 0; i < world.RngStreams.Count; i++)
        {
            RngStreamRow row = world.RngStreams[i];
            writer.Write(row.System.Value);
            writer.Write(row.Region.Value);
            writer.Write(row.State);
            writer.Write(row.Inc);
        }

        // 5. Rainfall
        writer.Write(world.Rainfall.Count);
        for (int i = 0; i < world.Rainfall.Count; i++)
        {
            RainfallRow row = world.Rainfall[i];
            writer.Write(row.Region.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.RainfallMmPerYear));
        }

        // 6. Biomass
        writer.Write(world.Biomass.Count);
        for (int i = 0; i < world.Biomass.Count; i++)
        {
            BiomassRow row = world.Biomass[i];
            writer.Write(row.Region.Value);
            writer.Write(row.Biomass.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.GrowthRemainder));
        }

        // 7. Goods
        writer.Write(world.Goods.Count);
        for (int i = 0; i < world.Goods.Count; i++)
        {
            GoodsRow row = world.Goods[i];
            writer.Write(row.Region.Value);
            writer.Write(row.Amount.Value);
        }

        // 8. Ledger flows
        writer.Write(world.LedgerFlows.Count);
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            writer.Write(row.Quantity.Value);
            writer.Write(row.Reason.Value);
            writer.Write(row.TotalSourced);
            writer.Write(row.TotalSunk);
        }

        // 9. Network nodes (v3)
        writer.Write(world.NetworkNodes.Count);
        for (int i = 0; i < world.NetworkNodes.Count; i++)
        {
            NetworkNodeRow row = world.NetworkNodes[i];
            writer.Write(row.Id.Value);
            writer.Write(row.LatticeNode);
        }

        // 10. Network edges (v3)
        writer.Write(world.NetworkEdges.Count);
        for (int i = 0; i < world.NetworkEdges.Count; i++)
        {
            NetworkEdgeRow row = world.NetworkEdges[i];
            writer.Write(row.Id.Value);
            writer.Write(row.A.Value);
            writer.Write(row.B.Value);
            writer.Write(row.EdgeType);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Cost));
        }

        // 11. Settlements (v4)
        writer.Write(world.Settlements.Count);
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            SettlementRow row = world.Settlements[i];
            writer.Write(row.Id.Value);
            writer.Write(row.SiteCell);
            writer.Write(row.FoundedTurn);
        }

        // 12. Network meta (v4)
        writer.Write(world.NetworkMeta.Count);
        for (int i = 0; i < world.NetworkMeta.Count; i++)
            writer.Write(world.NetworkMeta[i].Revision);

        // 13. Catchment nodes (v4)
        writer.Write(world.CatchmentNodes.Count);
        for (int i = 0; i < world.CatchmentNodes.Count; i++)
        {
            CatchmentNodeRow row = world.CatchmentNodes[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.LatticeNode);
            writer.Write(BitConverter.DoubleToInt64Bits(row.TravelCost));
        }

        // 14. Catchment summaries (v4)
        writer.Write(world.CatchmentSummaries.Count);
        for (int i = 0; i < world.CatchmentSummaries.Count; i++)
        {
            CatchmentSummaryRow row = world.CatchmentSummaries[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.NodeCount);
            writer.Write(BitConverter.DoubleToInt64Bits(row.EffectiveFarmland));
            writer.Write(row.NetworkRevision);
            writer.Write(row.LastRecomputeTurn);
        }

        // 15. Population bands (v5)
        writer.Write(world.PopBands.Count);
        for (int i = 0; i < world.PopBands.Count; i++)
        {
            PopBandRow row = world.PopBands[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Band);
            writer.Write(row.Count.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.BirthRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.DeathRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.StarvationRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.AgingRemainder));
        }

        // 16. Food stores (v5)
        writer.Write(world.FoodStores.Count);
        for (int i = 0; i < world.FoodStores.Count; i++)
        {
            FoodStoreRow row = world.FoodStores[i];
            writer.Write(row.Settlement.Value);
            writer.Write(row.Store.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.HarvestRemainder));
            writer.Write(BitConverter.DoubleToInt64Bits(row.EatenRemainder));
        }

        // 17. Consumption deficits (v5)
        writer.Write(world.ConsumptionDeficits.Count);
        for (int i = 0; i < world.ConsumptionDeficits.Count; i++)
        {
            ConsumptionDeficitRow row = world.ConsumptionDeficits[i];
            writer.Write(row.Settlement.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.DeficitRatio));
        }

        // 18. Labor allocations (v6)
        writer.Write(world.LaborAllocations.Count);
        for (int i = 0; i < world.LaborAllocations.Count; i++)
        {
            LaborAllocationRow row = world.LaborAllocations[i];
            writer.Write(row.Settlement.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.FarmShare));
        }

        // 19. Path progress (v6)
        writer.Write(world.PathProgress.Count);
        for (int i = 0; i < world.PathProgress.Count; i++)
        {
            PathProgressRow row = world.PathProgress[i];
            writer.Write(row.Settlement.Value);
            writer.Write(BitConverter.DoubleToInt64Bits(row.Banked));
            writer.Write(row.FrontierNode);
        }
    }

    /// <summary>Reads a state stream written by <see cref="Write"/> (same order, field by field).</summary>
    public static WorldState Read(BinaryReader reader) => Read(reader, out _);

    /// <summary>
    /// As <see cref="Read(BinaryReader)"/>; <paramref name="expectedTerrainHash"/>
    /// returns the terrain content hash the state was saved against (null if the
    /// world had no terrain). Terrain itself is not in the stream (ADR-008) — the
    /// caller regenerates it from seed + config and must match this hash
    /// (Snapshot.Load enforces it).
    /// </summary>
    public static WorldState Read(BinaryReader reader, out byte[]? expectedTerrainHash)
    {
        ulong seed = reader.ReadUInt64();
        var world = new WorldState(seed)
        {
            Clock = new SimClock(reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64()),
        };

        expectedTerrainHash = reader.ReadBoolean() ? reader.ReadBytes(32) : null;

        int regionCount = reader.ReadInt32();
        for (int i = 0; i < regionCount; i++)
            world.Regions.Add(new RegionRow(new RegionId(reader.ReadInt32())));

        int rngCount = reader.ReadInt32();
        for (int i = 0; i < rngCount; i++)
        {
            world.RngStreams.Add(new RngStreamRow(
                new SystemId(reader.ReadInt32()), new RegionId(reader.ReadInt32()),
                reader.ReadUInt64(), reader.ReadUInt64()));
        }

        int rainCount = reader.ReadInt32();
        for (int i = 0; i < rainCount; i++)
        {
            world.Rainfall.Add(new RainfallRow(
                new RegionId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int biomassCount = reader.ReadInt32();
        for (int i = 0; i < biomassCount; i++)
        {
            var region = new RegionId(reader.ReadInt32());
            long stock = reader.ReadInt64();
            double remainder = BitConverter.Int64BitsToDouble(reader.ReadInt64());
            world.Biomass.Add(new BiomassRow(region, Conserved.FromSnapshot(stock), remainder));
        }

        int goodsCount = reader.ReadInt32();
        for (int i = 0; i < goodsCount; i++)
        {
            var region = new RegionId(reader.ReadInt32());
            long stock = reader.ReadInt64();
            world.Goods.Add(new GoodsRow(region, Conserved.FromSnapshot(stock)));
        }

        int flowCount = reader.ReadInt32();
        for (int i = 0; i < flowCount; i++)
        {
            world.LedgerFlows.Add(new LedgerFlowRow(
                new ConservedQuantityId(reader.ReadInt32()), new ReasonId(reader.ReadInt32()),
                reader.ReadInt64(), reader.ReadInt64()));
        }

        int netNodeCount = reader.ReadInt32();
        for (int i = 0; i < netNodeCount; i++)
        {
            world.NetworkNodes.Add(new NetworkNodeRow(
                new NetworkNodeId(reader.ReadInt32()), reader.ReadInt32()));
        }

        int netEdgeCount = reader.ReadInt32();
        for (int i = 0; i < netEdgeCount; i++)
        {
            world.NetworkEdges.Add(new NetworkEdgeRow(
                new NetworkEdgeId(reader.ReadInt32()), new NetworkNodeId(reader.ReadInt32()),
                new NetworkNodeId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int settlementCount = reader.ReadInt32();
        for (int i = 0; i < settlementCount; i++)
        {
            world.Settlements.Add(new SettlementRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(), reader.ReadInt64()));
        }

        int netMetaCount = reader.ReadInt32();
        for (int i = 0; i < netMetaCount; i++)
            world.NetworkMeta.Add(new NetworkMetaRow(reader.ReadInt32()));

        int catchNodeCount = reader.ReadInt32();
        for (int i = 0; i < catchNodeCount; i++)
        {
            world.CatchmentNodes.Add(new CatchmentNodeRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int catchSummaryCount = reader.ReadInt32();
        for (int i = 0; i < catchSummaryCount; i++)
        {
            world.CatchmentSummaries.Add(new CatchmentSummaryRow(
                new SettlementId(reader.ReadInt32()), reader.ReadInt32(),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                reader.ReadInt32(), reader.ReadInt64()));
        }

        int popBandCount = reader.ReadInt32();
        for (int i = 0; i < popBandCount; i++)
        {
            var settlement = new SettlementId(reader.ReadInt32());
            int band = reader.ReadInt32();
            long count = reader.ReadInt64();
            world.PopBands.Add(new PopBandRow(
                settlement, band, Conserved.FromSnapshot(count),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int foodStoreCount = reader.ReadInt32();
        for (int i = 0; i < foodStoreCount; i++)
        {
            var settlement = new SettlementId(reader.ReadInt32());
            long store = reader.ReadInt64();
            world.FoodStores.Add(new FoodStoreRow(
                settlement, Conserved.FromSnapshot(store),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int deficitCount = reader.ReadInt32();
        for (int i = 0; i < deficitCount; i++)
        {
            world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(
                new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int allocCount = reader.ReadInt32();
        for (int i = 0; i < allocCount; i++)
        {
            world.LaborAllocations.Add(new LaborAllocationRow(
                new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64())));
        }

        int progressCount = reader.ReadInt32();
        for (int i = 0; i < progressCount; i++)
        {
            world.PathProgress.Add(new PathProgressRow(
                new SettlementId(reader.ReadInt32()),
                BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                reader.ReadInt32()));
        }

        return world;
    }

    /// <summary>
    /// Exact stream length from schema widths × row counts — the structural
    /// anti-padding proof: any raw-memory shortcut in Write breaks equality with
    /// this sum (struct layouts pad; the schema does not).
    /// </summary>
    public static long ExpectedLength(WorldState world) =>
        SeedWidth + ClockWidth
        + 1 + (world.Terrain is not null ? 32 : 0)   // terrain flag + content hash
        + CountPrefixWidth + (long)world.Regions.Count * RegionRowWidth
        + CountPrefixWidth + (long)world.RngStreams.Count * RngStreamRowWidth
        + CountPrefixWidth + (long)world.Rainfall.Count * RainfallRowWidth
        + CountPrefixWidth + (long)world.Biomass.Count * BiomassRowWidth
        + CountPrefixWidth + (long)world.Goods.Count * GoodsRowWidth
        + CountPrefixWidth + (long)world.LedgerFlows.Count * LedgerFlowRowWidth
        + CountPrefixWidth + (long)world.NetworkNodes.Count * NetworkNodeRowWidth
        + CountPrefixWidth + (long)world.NetworkEdges.Count * NetworkEdgeRowWidth
        + CountPrefixWidth + (long)world.Settlements.Count * SettlementRowWidth
        + CountPrefixWidth + (long)world.NetworkMeta.Count * NetworkMetaRowWidth
        + CountPrefixWidth + (long)world.CatchmentNodes.Count * CatchmentNodeRowWidth
        + CountPrefixWidth + (long)world.CatchmentSummaries.Count * CatchmentSummaryRowWidth
        + CountPrefixWidth + (long)world.PopBands.Count * PopBandRowWidth
        + CountPrefixWidth + (long)world.FoodStores.Count * FoodStoreRowWidth
        + CountPrefixWidth + (long)world.ConsumptionDeficits.Count * ConsumptionDeficitRowWidth
        + CountPrefixWidth + (long)world.LaborAllocations.Count * LaborAllocationRowWidth
        + CountPrefixWidth + (long)world.PathProgress.Count * PathProgressRowWidth;
}
