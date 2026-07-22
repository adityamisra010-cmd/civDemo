using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

// T0.7 acceptance: save→load→continue equals the uninterrupted run hash-for-hash
// at every subsequent turn; structural anti-padding proof; pinned golden hash;
// version-mismatch rejection.
public class SnapshotTests
{
    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    private static TurnExecutor CanonicalExecutor(OrderLog? orders = null)
    {
        // Toy preset (T1.5): the golden lineage lives on the toy world + toy
        // systems; the production preset's behavior is pinned by the T1.5
        // population tests and gets its own golden at T1.9.
        using var stream = Sim.Data.DataFiles.OpenPipelineToy();
        var pipeline = PipelineLoader.Load(stream, SystemCatalog.All(TestUtil.TestConfigs.Sim()));
        return new TurnExecutor(CanonicalEra(), pipeline, orders);
    }

    // The canonical M0 test world: seed + two regions (world genesis, shared by
    // the golden-hash and replay tests — changing it changes the golden hash).
    private static WorldState Genesis(ulong seed)
    {
        var world = new WorldState(seed);
        world.Regions.Add(new RegionRow(new RegionId(0)));
        world.Regions.Add(new RegionRow(new RegionId(1)));
        return world;
    }

    [Fact]
    public void SaveLoadContinue_HashEqualsUninterruptedRun_AtEveryTurn()
    {
        const int k = 25, n = 75;
        var executor = CanonicalExecutor();

        // Uninterrupted run to turn k…
        WorldState uninterrupted = executor.Run(Genesis(42), k);

        // …saved and reloaded at turn k…
        using var buffer = new MemoryStream();
        Snapshot.Save(uninterrupted, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);

        Assert.True(WorldStates.StateEquals(uninterrupted, loaded));
        Assert.Equal(WorldHash.ComputeHex(uninterrupted), WorldHash.ComputeHex(loaded));

        // …must continue identically at EVERY turn k..N (RNG continuation
        // included by construction: stream states live in the state stream).
        for (int turn = k; turn < n; turn++)
        {
            uninterrupted = executor.Step(uninterrupted);
            loaded = executor.Step(loaded);
            Assert.Equal(WorldHash.ComputeHex(uninterrupted), WorldHash.ComputeHex(loaded));
        }
    }

    [Fact]
    public void CanonicalStream_LengthEqualsSchemaWidthSum_AntiPaddingProof()
    {
        // Any raw-memory shortcut in the serializer writes padded struct layouts
        // and fails this exact-length equality (e.g. BiomassRow pads 4→24 bytes in
        // memory; the schema writes exactly 20).
        var executor = CanonicalExecutor();
        WorldState world = executor.Run(Genesis(42), 10);

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }

        Assert.True(world.Biomass.Count > 0 && world.RngStreams.Count > 0); // non-vacuous
        Assert.Equal(CanonicalSchema.ExpectedLength(world), buffer.Length);
    }

    [Fact]
    public void GoldenHash_Seed42Turn200_MatchesPinnedConstant()
    {
        // FROZEN CONSTANT. This test breaks LOUDLY on ANY change to world state
        // content, schema order, field widths, RNG, system behavior, or the
        // canonical era/pipeline data — that is its job. Update it deliberately,
        // with a schema Version bump where appropriate; never casually.
        //
        // Update history:
        //   v1 (T0.7): 4cba3e716e5d770a93b13beb4ef7c44baaefaa36c83c94c3f85ef48285f47ce9
        //   v2 (T1.1, ADR-008): schema gained the terrain presence flag + hash
        //   after the clock; sim behavior unchanged (this world has no terrain —
        //   the stream grew exactly one 0x00 byte).
        //   v2 value: 34ad6f01a9b8aaa05eccc7f1265457bf6811a26e5760f4791c1ecf0d7ccea060
        //   v3 (T1.3): schema gained the empty NetworkNodes/NetworkEdges tables
        //   (two zero count prefixes, 8 bytes); sim behavior unchanged.
        //   v3 value: 1884a60b2b66e106503291131b91e9254e7ddf20b6e6a9926fddeedd1cf62e9e
        //   v4 (T1.4): schema gained four empty tables (Settlements, NetworkMeta,
        //   CatchmentNodes, CatchmentSummaries — four zero count prefixes, 16 bytes).
        //   The catchment system leads the pipeline but no-ops on this terrain-less
        //   genesis world, so sim behavior is unchanged; only the stream grew.
        //   v4 value: 64dff09f5e58a95966f9e7c6b2d921048d8595ad9d3183e9e5dc1152c9d235e2
        //   v5 (T1.5): schema gained three empty tables (PopBands, FoodStores,
        //   ConsumptionDeficits — three zero count prefixes, 12 bytes). This test
        //   also moved from the production pipeline to the retired-toy preset —
        //   behavior-identical here: the toy preset is exactly the systems that
        //   acted on this terrain-less world (catchment always no-oped, drew no
        //   RNG). Only the stream grew.
        //   v5 value: abf1ef9357f7cd7599895743e2687c31cb003d616bbb396b4e3de206ba05121c
        //   v6 (T1.6): schema gained the empty LaborAllocations + PathProgress
        //   tables (two zero count prefixes, 8 bytes) — forced by the labor
        //   order's persistent allocation state. Sim behavior on this toy world
        //   is unchanged (pathbuild is not in the toy preset); only the stream grew.
        const string golden = "8f3a1986afe9f6fd076e082c868ca36bd171c9da5932fb34c0975de0f38390e1";

        WorldState world = CanonicalExecutor().Run(Genesis(42), 200);
        Assert.Equal(golden, WorldHash.ComputeHex(world));
    }

    [Fact]
    public void SchemaV3_PopulatedNetworkTables_LengthAndRoundTripExact()
    {
        // Adversarial-review finding (T1.3, the T1.1 precedent repeated): the v3
        // network row widths and (de)serializers were only ever exercised with
        // EMPTY tables — a destroyed edge-cost write passed the whole suite.
        // This test pins the populated branch: exact length, bit-exact round
        // trip (including a negative-zero Cost), and hash equality.
        WorldState world = Genesis(7);
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(0), LatticeNode: 1234));
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(1), LatticeNode: 987));
        world.NetworkEdges.Add(new NetworkEdgeRow(
            new NetworkEdgeId(0), new NetworkNodeId(0), new NetworkNodeId(1),
            EdgeTypes.DirtPath, Cost: 123.456));
        world.NetworkEdges.Add(new NetworkEdgeRow(
            new NetworkEdgeId(1), new NetworkNodeId(1), new NetworkNodeId(0),
            EdgeTypes.DirtPath, Cost: -0.0));

        // Anti-padding: exact schema width sum with rows PRESENT.
        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        // Round trip: every field survives bit-exactly; hashes agree.
        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(1234, loaded.NetworkNodes[0].LatticeNode);
        Assert.Equal(123.456, loaded.NetworkEdges[0].Cost);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.NetworkEdges[1].Cost));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV4_PopulatedSettlementAndCatchmentTables_LengthAndRoundTripExact()
    {
        // Constitution rule (T1.4): every new serialized row type ships a
        // POPULATED-table test — exact ExpectedLength, bit-exact round trip, hash
        // equality. Empty-table coverage proves nothing (T1.1/T1.3 precedent).
        // Exercises all four v4 row types with negative-zero doubles present.
        WorldState world = Genesis(11);
        world.Settlements.Add(new SettlementRow(new SettlementId(0), SiteCell: 4242, FoundedTurn: 7));
        world.Settlements.Add(new SettlementRow(new SettlementId(1), SiteCell: 99, FoundedTurn: 13));
        world.NetworkMeta.Add(new NetworkMetaRow(Revision: 5));
        world.CatchmentNodes.Add(new CatchmentNodeRow(new SettlementId(0), LatticeNode: 321, TravelCost: 8.75));
        world.CatchmentNodes.Add(new CatchmentNodeRow(new SettlementId(1), LatticeNode: 654, TravelCost: -0.0));
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            new SettlementId(0), NodeCount: 1, EffectiveFarmland: 3.5,
            NetworkRevision: 5, LastRecomputeTurn: 42));
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            new SettlementId(1), NodeCount: 1, EffectiveFarmland: -0.0,
            NetworkRevision: 5, LastRecomputeTurn: 42));

        // Anti-padding: exact schema width sum with rows PRESENT.
        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        // Round trip: every field survives bit-exactly; hashes agree.
        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(4242, loaded.Settlements[0].SiteCell);
        Assert.Equal(13, loaded.Settlements[1].FoundedTurn);
        Assert.Equal(5, loaded.NetworkMeta[0].Revision);
        Assert.Equal(8.75, loaded.CatchmentNodes[0].TravelCost);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.CatchmentNodes[1].TravelCost));
        Assert.Equal(3.5, loaded.CatchmentSummaries[0].EffectiveFarmland);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.CatchmentSummaries[1].EffectiveFarmland));
        Assert.Equal(42, loaded.CatchmentSummaries[0].LastRecomputeTurn);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV5_PopulatedPopulationTables_LengthAndRoundTripExact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-table
        // test — exact ExpectedLength, bit-exact round trip, hash equality.
        // Exercises all three v5 row types with negative-zero doubles present.
        WorldState world = Genesis(17);
        var ledger = new Sim.Core.Kernel.Ledger(world.LedgerFlows);
        world.PopBands.Add(new PopBandRow(new SettlementId(0), PopBands.Children,
            Conserved.Zero, birthRemainder: 0.25, deathRemainder: -0.0,
            starvationRemainder: 0.5, agingRemainder: 0.125));
        world.PopBands.Add(new PopBandRow(new SettlementId(0), PopBands.Adults,
            Conserved.Zero, birthRemainder: 0.0, deathRemainder: 0.75,
            starvationRemainder: -0.0, agingRemainder: 0.9));
        ledger.Flow(ref world.PopBands.Ref(0).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 130, FlowDirection.Source, OverdrawPolicy.Throw);
        ledger.Flow(ref world.PopBands.Ref(1).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 200, FlowDirection.Source, OverdrawPolicy.Throw);
        world.FoodStores.Add(new FoodStoreRow(new SettlementId(0),
            Conserved.Zero, harvestRemainder: 0.375, eatenRemainder: -0.0));
        ledger.Flow(ref world.FoodStores.Ref(0).Store, ConservedQuantityIds.Food,
            ReasonIds.InitialEndowment, 6000, FlowDirection.Source, OverdrawPolicy.Throw);
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), 0.613));
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(1), -0.0));

        // Anti-padding: exact schema width sum with rows PRESENT.
        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        // Round trip: every field survives bit-exactly; hashes agree.
        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(130, loaded.PopBands[0].Count.Value);
        Assert.Equal(0.25, loaded.PopBands[0].BirthRemainder);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.PopBands[0].DeathRemainder));
        Assert.Equal(0.9, loaded.PopBands[1].AgingRemainder);
        Assert.Equal(6000, loaded.FoodStores[0].Store.Value);
        Assert.Equal(0.375, loaded.FoodStores[0].HarvestRemainder);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.FoodStores[0].EatenRemainder));
        Assert.Equal(0.613, loaded.ConsumptionDeficits[0].DeficitRatio);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.ConsumptionDeficits[1].DeficitRatio));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV6_PopulatedLaborTables_LengthAndRoundTripExact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test — exact ExpectedLength, bit-exact round trip, hash equality.
        WorldState world = Genesis(23);
        world.LaborAllocations.Add(new LaborAllocationRow(new SettlementId(0), 0.35));
        world.LaborAllocations.Add(new LaborAllocationRow(new SettlementId(1), -0.0));
        world.PathProgress.Add(new PathProgressRow(new SettlementId(0), Banked: 123.456, FrontierNode: 4321));
        world.PathProgress.Add(new PathProgressRow(new SettlementId(1), Banked: -0.0, FrontierNode: -1));

        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(0.35, loaded.LaborAllocations[0].FarmShare);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.LaborAllocations[1].FarmShare));
        Assert.Equal(123.456, loaded.PathProgress[0].Banked);
        Assert.Equal(4321, loaded.PathProgress[0].FrontierNode);
        Assert.Equal(-1, loaded.PathProgress[1].FrontierNode);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void VersionMismatch_FailsWithActionableMessage()
    {
        WorldState world = CanonicalExecutor().Run(Genesis(42), 3);
        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);

        // Corrupt the version field (bytes 8..12, after the 8-byte magic).
        byte[] bytes = buffer.ToArray();
        bytes[8] = 99;
        using var corrupted = new MemoryStream(bytes);

        var e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(corrupted));
        Assert.Contains("schema version 99", e.Message);
        Assert.Contains("saves break between milestones by design (D-008)", e.Message);
        Assert.Contains("replay", e.Message);
    }

    [Fact]
    public void BadMagic_FailsActionably()
    {
        using var junk = new MemoryStream("not a save file at all"u8.ToArray());
        var e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(junk));
        Assert.Contains("bad magic", e.Message);
    }

    [Fact]
    public void NegativeZeroAndNaN_SurviveBitExactly()
    {
        // The schema must NOT normalize special doubles — bit-exactness detects
        // divergence that value-equality would mask.
        var world = Genesis(1);
        world.Rainfall.Add(new RainfallRow(new RegionId(0), -0.0));
        world.Rainfall.Add(new RainfallRow(new RegionId(1), double.NaN));

        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);

        Assert.Equal(
            BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.Rainfall[0].RainfallMmPerYear));
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(double.NaN),
            BitConverter.DoubleToInt64Bits(loaded.Rainfall[1].RainfallMmPerYear));
    }
}
