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
        using var stream = Sim.Data.DataFiles.OpenPipeline();
        var pipeline = PipelineLoader.Load(stream, SystemCatalog.All());
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
        const string golden = "1884a60b2b66e106503291131b91e9254e7ddf20b6e6a9926fddeedd1cf62e9e";

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
