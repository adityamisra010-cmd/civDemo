using Sim.Cli;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// D-054 / ADR-039 — SCHEMA v33: section 59 ConstructionProgress (Settlement, Slot, ProjectId, EnqueuedTurn, Progress;
/// 28 bytes) and section 60 ResourceAllocations (Polity, BuildingsPercent; 8 bytes), appended after TaxGrievances; the
/// snapshot's int32 TOPOLOGY trailer after the canonical stream; and THE FIRST EXPLICIT MIGRATION — Snapshot.Load reads a
/// v32 save (its stream stops after section 58; no trailer; flat). Constitution rule: every new serialized row type ships
/// a POPULATED-table test (exact ExpectedLength, bit-exact round trip, hash equality); populated out of key order, with
/// int.MaxValue keys and a negative EnqueuedTurn. The migration is tested against a REAL v32 save written by the batch-2
/// build (Fixtures/README-v32-fixture.md).
/// </summary>
public class ConstructionSchemaV33Tests
{
    /// <summary>The v32 build's reported hash of the fixture (README-v32-fixture.md).</summary>
    internal const string V32FixtureHash = "b000b2ee26cc02310e6e379f9b7c4bad2b9568e41e6080be85056b3ebdb92361";

    private static WorldState Populated()
    {
        var w = new WorldState(33);
        // Queue rows for each progress row (the v33 invariant Snapshot.Load asserts), out of key order.
        w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(7), 2, 1));
        w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(0), 0, 11));
        w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(int.MaxValue), int.MaxValue, int.MaxValue));
        w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(7), 2, 1, 41, 999_999));
        w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(0), 0, 11, -1, 3));
        w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(int.MaxValue), int.MaxValue, int.MaxValue, long.MaxValue, 0));
        w.ResourceAllocations.Add(new ResourceAllocationRow(new PolityId(3), 70));
        w.ResourceAllocations.Add(new ResourceAllocationRow(new PolityId(int.MaxValue), 0));
        w.ResourceAllocations.Add(new ResourceAllocationRow(new PolityId(1), 100));
        return w;
    }

    private static byte[] Stream(WorldState w) => SchemaProjection.Serialize(w);

    [Fact]
    public void SchemaV33_PopulatedProgressAndAllocations_LengthRoundTripAndHashExact()
    {
        // D-055 (2026-10-16) CAUSE BLOCK: Version 33 -> 34, and every tail offset below moves by the 4-byte count prefix of
        // the EMPTY section 61 (ConstructionHolds) that v34 appends after ResourceAllocations — the layout alone; no v33
        // row changed width (ExpectedLength's row arithmetic is unchanged). `end` is where the v33 stream ended.
        Assert.Equal(34, CanonicalSchema.Version);
        Assert.Equal(32, CanonicalSchema.OldestReadableVersion);
        WorldState world = Populated();
        byte[] bytes = Stream(world);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), bytes.Length);
        // The rows' own contribution: 3 queue rows × 12, 3 progress rows × 28, 3 allocation rows × 8.
        Assert.Equal(3 * 12 + 3 * 28 + 3 * 8, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(33)));

        // The empty v34 block is the LAST 4 bytes; before it, ResourceAllocations, and ConstructionProgress before that.
        Assert.Equal(0, BitConverter.ToInt32(bytes, bytes.Length - 4));                    // ConstructionHolds count prefix: 0
        int end = bytes.Length - 4;
        Assert.Equal(100, BitConverter.ToInt32(bytes, end - 4));
        Assert.Equal(1, BitConverter.ToInt32(bytes, end - 8));
        Assert.Equal(3, BitConverter.ToInt32(bytes, end - 4 - 3 * 8));                     // allocation count prefix
        int progressEnd = end - 4 - 3 * 8;
        Assert.Equal(0L, BitConverter.ToInt64(bytes, progressEnd - 8));                    // the last progress row's Progress
        Assert.Equal(long.MaxValue, BitConverter.ToInt64(bytes, progressEnd - 16));        // its EnqueuedTurn
        Assert.Equal(int.MaxValue, BitConverter.ToInt32(bytes, progressEnd - 20));         // its ProjectId
        Assert.Equal(3, BitConverter.ToInt32(bytes, progressEnd - 4 - 3 * 28));            // progress count prefix
        Assert.Equal(-1L, BitConverter.ToInt64(bytes, progressEnd - 4 - 3 * 28 + 4 + 28 + 12));   // row 1's EnqueuedTurn −1

        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
        for (int i = 0; i < world.ConstructionProgress.Count; i++) Assert.Equal(world.ConstructionProgress[i], back.ConstructionProgress[i]);   // order kept
        for (int i = 0; i < world.ResourceAllocations.Count; i++) Assert.Equal(world.ResourceAllocations[i], back.ResourceAllocations[i]);
    }

    [Fact]
    public void SchemaV33_EveryFieldIsHashed_AndAnEmptyWorldEndsWithTwoZeroPrefixes()
    {
        WorldState a = Populated();
        string h = WorldHash.ComputeHex(a);
        WorldState[] variants = new WorldState[7];
        for (int i = 0; i < variants.Length; i++) variants[i] = a.Clone();
        variants[0].ConstructionProgress[0] = variants[0].ConstructionProgress[0] with { Progress = 999_998 };
        variants[1].ConstructionProgress[0] = variants[1].ConstructionProgress[0] with { EnqueuedTurn = 40 };
        variants[2].ConstructionProgress[1] = variants[2].ConstructionProgress[1] with { Slot = 1 };
        variants[3].ConstructionProgress[1] = variants[3].ConstructionProgress[1] with { ProjectId = 12 };
        variants[4].ConstructionProgress[1] = variants[4].ConstructionProgress[1] with { Settlement = new SettlementId(1) };
        variants[5].ResourceAllocations[0] = variants[5].ResourceAllocations[0] with { BuildingsPercent = 71 };
        variants[6].ResourceAllocations[0] = variants[6].ResourceAllocations[0] with { Polity = new PolityId(4) };
        foreach (WorldState v in variants) Assert.NotEqual(h, WorldHash.ComputeHex(v));

        byte[] empty = Stream(new WorldState(33));
        Assert.All(empty[^8..], x => Assert.Equal(0, x));
        Assert.Equal(CanonicalSchema.ExpectedLength(new WorldState(33)), empty.Length);
    }

    [Fact]
    public void SchemaV33_SnapshotSaveLoad_WritesTheTopologyTrailer_AndClonesAreIndependent()
    {
        WorldState world = Populated();
        using var ms = new MemoryStream();
        Snapshot.Save(world, ms);
        byte[] file = ms.ToArray();
        Assert.Equal(8 + 4 + 8 + 8 + CanonicalSchema.ExpectedLength(world) + 4, file.Length);   // header | stream | trailer
        Assert.Equal(0, BitConverter.ToInt32(file, file.Length - 4));                           // a terrain-less world is flat
        WorldState back = Snapshot.Load(new MemoryStream(file));
        Assert.True(WorldStates.StateEquals(world, back));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));

        WorldState clone = world.Clone();
        clone.ConstructionProgress[0] = clone.ConstructionProgress[0] with { Progress = 1 };
        clone.ResourceAllocations[0] = clone.ResourceAllocations[0] with { BuildingsPercent = 1 };
        Assert.False(WorldStates.StateEquals(world, clone));
        Assert.Equal(999_999, world.ConstructionProgress[0].Progress);
        Assert.Equal(70, world.ResourceAllocations[0].BuildingsPercent);
    }

    [Fact]
    public void Load_RefusesATruncatedTrailer_UnknownTopologyBits_AndAWrappingSaveWithoutTerrain()
    {
        WorldState world = Populated();
        using var ms = new MemoryStream();
        Snapshot.Save(world, ms);
        byte[] file = ms.ToArray();

        byte[] truncated = file[..^2];
        Assert.Contains("topology trailer", Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(truncated))).Message);

        byte[] unknown = (byte[])file.Clone();
        BitConverter.GetBytes(0x0000_0002).CopyTo(unknown, unknown.Length - 4);
        Assert.Contains("unknown topology bits", Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(unknown))).Message);

        byte[] wraps = (byte[])file.Clone();
        BitConverter.GetBytes(TopologyFlags.WrapX).CopyTo(wraps, wraps.Length - 4);
        Assert.Contains("only a world with terrain can wrap", Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(wraps))).Message);
    }

    /// <summary>
    /// THE TRAILER AGAINST REAL TERRAIN (batch-3 integration, 2026-10-14: TopologyFlags reads TerrainSet.WrapX). A world
    /// founded on the shipped cylinder writes bit 0 = 1; the same world founded flat (the topology switch off) writes 0.
    /// Each loads against its own terrain. A trailer that disagrees with the supplied terrain — either way round — is
    /// refused with the actionable message (the content hashes agree, so only the trailer check can see it).
    /// </summary>
    [Fact]
    public void Load_RefusesAWrapFlagThatDisagreesWithTheSuppliedTerrain_Actionably()
    {
        WorldState wrapped = Sim.Core.Worldgen.WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        WorldState flat = Sim.Core.Worldgen.WorldFounding.Found(TestConfigs.PreD054Worldgen(TestConfigs.DevWorldgen()), TestConfigs.Sim(), 42);
        Assert.True(wrapped.Terrain!.WrapX);                                     // the shipped worldgen is a cylinder
        Assert.False(flat.Terrain!.WrapX);

        byte[] wrappedFile = Saved(wrapped), flatFile = Saved(flat);
        Assert.Equal(TopologyFlags.WrapX, BitConverter.ToInt32(wrappedFile, wrappedFile.Length - 4));   // wrapped: bit 0 = 1
        Assert.Equal(0, BitConverter.ToInt32(flatFile, flatFile.Length - 4));                            // flat: 0
        Assert.True(WorldStates.StateEquals(wrapped, Snapshot.Load(new MemoryStream(wrappedFile), wrapped.Terrain)));
        Assert.True(WorldStates.StateEquals(flat, Snapshot.Load(new MemoryStream(flatFile), flat.Terrain)));

        // A wrapped world's save whose trailer says flat, loaded against its own (wrapping) terrain.
        BitConverter.GetBytes(0).CopyTo(wrappedFile, wrappedFile.Length - 4);
        SnapshotFormatException e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(wrappedFile), wrapped.Terrain));
        Assert.Contains("topology mismatch: the save was made on a FLAT world", e.Message);
        Assert.Contains("the provided terrain wraps east-west", e.Message);
        Assert.Contains("topology.wrapX", e.Message);

        // A flat world's save whose trailer says it wraps, loaded against its own (flat) terrain.
        BitConverter.GetBytes(TopologyFlags.WrapX).CopyTo(flatFile, flatFile.Length - 4);
        e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(flatFile), flat.Terrain));
        Assert.Contains("topology mismatch: the save was made on an east-west WRAPPING world but the provided terrain is flat", e.Message);
        Assert.Contains("topology.wrapX", e.Message);
    }

    private static byte[] Saved(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// The constitution's POPULATED-table rule on a WRAPPED world (batch-3 integration): the shipped cylinder's dev world
    /// with both v33 tables populated (out of key order, int.MaxValue keys, a negative EnqueuedTurn) saves to exactly
    /// header + ExpectedLength + the trailer (bit 0 = 1), loads against its regenerated cylinder bit-exactly (StateEquals,
    /// hash equality, rows in order), and re-saves to the identical bytes.
    /// </summary>
    [Fact]
    public void SchemaV33_PopulatedRoundTrip_OnAWrappedWorld_TrailerBitOne_BitExact()
    {
        WorldState w = Sim.Core.Worldgen.WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        Assert.True(w.Terrain!.WrapX);
        int s0 = w.Settlements[0].Id.Value, s1 = w.Settlements[^1].Id.Value;
        w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(s1), 3, 2));
        w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(s0), 0, 1));
        w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(int.MaxValue), int.MaxValue, int.MaxValue));
        w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(s1), 3, 2, 17, 999_999));
        w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(s0), 0, 1, -1, 1));
        w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(int.MaxValue), int.MaxValue, int.MaxValue, long.MaxValue, 0));
        w.ResourceAllocations.Add(new ResourceAllocationRow(new PolityId(int.MaxValue), 0));
        w.ResourceAllocations.Add(new ResourceAllocationRow(new PolityId(1), 37));

        byte[] file = Saved(w);
        Assert.Equal(8 + 4 + 8 + 8 + CanonicalSchema.ExpectedLength(w) + 4, file.Length);   // header | stream | trailer
        Assert.Equal(CanonicalSchema.ExpectedLength(w), Stream(w).Length);
        Assert.Equal(TopologyFlags.WrapX, BitConverter.ToInt32(file, file.Length - 4));

        TerrainSet regenerated = Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.DevWorldgen(), 42);
        Assert.True(regenerated.WrapX);
        WorldState back = Snapshot.Load(new MemoryStream(file), regenerated);
        Assert.True(WorldStates.StateEquals(w, back), "wrapped round trip drifted");
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(back));
        Assert.True(back.Terrain!.WrapX);
        for (int i = 0; i < w.ConstructionProgress.Count; i++) Assert.Equal(w.ConstructionProgress[i], back.ConstructionProgress[i]);
        for (int i = 0; i < w.ResourceAllocations.Count; i++) Assert.Equal(w.ResourceAllocations[i], back.ResourceAllocations[i]);
        Assert.Equal(file, Saved(back));                                                     // bit-exact re-save
    }

    // ================================================================== a save no rule produces is refused (C1 review F1)

    /// <summary>A solo founded world holding <paramref name="queue"/> and <paramref name="progress"/>, saved.</summary>
    private static (byte[] File, TerrainSet Terrain) SavedWith(ConstructionQueueRow[] queue, ConstructionProgressRow[] progress)
    {
        WorldState w = IncrementalConstructionTests.Solo();
        foreach (ConstructionQueueRow q in queue) w.ConstructionQueue.Add(q);
        foreach (ConstructionProgressRow p in progress) w.ConstructionProgress.Add(p);
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return (ms.ToArray(), w.Terrain!);
    }

    private static readonly SettlementId S0 = new(0);
    private const int Granary = IncrementalConstructionTests.Granary;

    /// <summary>
    /// Progress lives in [0, N − 1] in every state a rule produces: an entry reaching N is completed and removed in that same
    /// step. Without this refusal (MEASURED, C1 review F1) a saved p = N loaded and the head never completed (the labour bound
    /// at N is 0, so it read as a Labour wait — not structural, so not even the AI would cancel it), and a saved p = −N loaded
    /// and the granary drew 79 timber and 39 stone for a cost of 40 and 20 (Delivered of a negative progress is negative).
    /// </summary>
    [Theory]
    [InlineData(1_000_000L)]
    [InlineData(1_000_001L)]
    [InlineData(-1L)]
    [InlineData(-1_000_000L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void Load_RefusesAProgressOutsideZeroToNMinusOne(long progress)
    {
        (byte[] file, TerrainSet terrain) = SavedWith(
            [new ConstructionQueueRow(S0, 0, Granary)], [new ConstructionProgressRow(S0, 0, Granary, 0, progress)]);
        SnapshotFormatException e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(file), terrain));
        Assert.Contains("save is corrupt", e.Message);
        Assert.Contains("outside [0, 999999]", e.Message);
    }

    [Fact]
    public void Load_AcceptsTheProgressBounds_ZeroAndNMinusOne()
    {
        foreach (long progress in new[] { 0L, ConstructionQuery.ProgressUnits - 1 })
        {
            (byte[] file, TerrainSet terrain) = SavedWith(
                [new ConstructionQueueRow(S0, 0, Granary)], [new ConstructionProgressRow(S0, 0, Granary, 0, progress)]);
            WorldState w = Snapshot.Load(new MemoryStream(file), terrain);
            Assert.Equal(progress, ConstructionQuery.ProgressOf(w, w.ConstructionQueue[0]));
        }
    }

    /// <summary>Two progress rows for one entry: MEASURED before this refusal (C1 review F1), the save loaded and the next step
    /// threw mid-turn ("progress … has no matching queue entry") because a removal takes one row. Load refuses it now.</summary>
    [Fact]
    public void Load_RefusesTwoProgressRowsForOneEntry()
    {
        (byte[] file, TerrainSet terrain) = SavedWith(
            [new ConstructionQueueRow(S0, 0, Granary)],
            [new ConstructionProgressRow(S0, 0, Granary, 0, 10), new ConstructionProgressRow(S0, 0, Granary, 0, 20)]);
        SnapshotFormatException e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(file), terrain));
        Assert.Contains("two construction progress rows for settlement 0, slot 0", e.Message);
    }

    /// <summary>Two queue entries at one (settlement, slot): no rule writes them (NextSlot is one past the highest queued
    /// slot), and the head, the cancel and the progress match would each pick one of them by table position.</summary>
    [Fact]
    public void Load_RefusesTwoQueueEntriesAtOneSlot()
    {
        (byte[] file, TerrainSet terrain) = SavedWith(
            [new ConstructionQueueRow(S0, 0, Granary), new ConstructionQueueRow(S0, 0, 2)], []);
        SnapshotFormatException e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(file), terrain));
        Assert.Contains("two construction queue entries of settlement 0, slot 0", e.Message);
    }

    /// <summary>D-055 (2026-10-16) CAUSE BLOCK: was "…ButThirtyTwoAndThirtyThree…" with the list {31, 34, 0, 27}; v34 is
    /// this build's schema now (CanonicalSchema.IsReadable: {32, 33, 34}), so the list is {31, 35, 0, 27} — the version
    /// above the readable set takes 34's place. Re-rigged, not re-pinned: the refused set's shape is unchanged.</summary>
    [Fact]
    public void Load_RefusesEveryVersionButThirtyTwoThirtyThreeAndThirtyFour_WithTheD008Message()
    {
        using var ms = new MemoryStream();
        Snapshot.Save(new WorldState(5), ms);
        foreach (int v in new[] { 31, 35, 0, 27 })
        {
            byte[] bytes = ms.ToArray();
            BitConverter.GetBytes(v).CopyTo(bytes, 8);
            SnapshotFormatException e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(bytes)));
            Assert.Contains("schema version " + v.ToString(System.Globalization.CultureInfo.InvariantCulture), e.Message);
            Assert.Contains("(D-008)", e.Message);
        }
    }

    // ================================================================== THE v32 → v33 MIGRATION (a real v32 save)

    private static byte[] Fixture() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-seed42-turn40.civsnap"));

    /// <summary>The order log that produced the v32 fixture (Fixtures/README-v32-fixture.md) still loads at the current IO
    /// version, and is exactly the three documented records: settlement 0's construction share to 0, then two granaries.</summary>
    [Fact]
    public void TheV32FixturesOrderLog_LoadsAtTheCurrentIoVersion_AsItsThreeDocumentedRecords()
    {
        using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-fixture-orders.bin"));
        OrderLog log = OrderLog.Load(stream);
        Assert.Equal(3, log.Count);
        Assert.Equal(new OrderRecord(0, 1, OrderKind.SectorAllocation, (0 << 3) | 4, 0.0), log[0]);
        Assert.Equal(new OrderRecord(1, 1, OrderKind.EnqueueConstruction, 0, 1.0), log[1]);
        Assert.Equal(new OrderRecord(2, 1, OrderKind.EnqueueConstruction, 0, 1.0), log[2]);
    }

    /// <summary>The fixture's terrain: every v32 world is FLAT (batch-3 integration, 2026-10-14) — regenerated with the
    /// topology switch off (TestConfigs.PreD054Worldgen), as the CLI does for a v32 save (Sim.Cli.SaveFiles).</summary>
    private static TerrainSet FixtureTerrain() => Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.PreD054Worldgen(), 42);

    private static string FixturePath() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-seed42-turn40.civsnap");

    /// <summary>
    /// THE CLI PATH (`sim hash` / `sim diff` load through Sim.Cli.SaveFiles; batch-3 integration). MEASURED before the
    /// fix on the merged tree: `sim hash` of the v32 fixture exited 2 with "terrain mismatch: the provided TerrainSet's
    /// content hash does not equal the hash this save was made against" — the CLI regenerated the shipped CYLINDER for a
    /// flat v32 save. The loader now regenerates a v32 save with the topology switch off: the fixture loads, flat, and its
    /// v32 projection is the hash the v32 build reported. A v33 save of the shipped cylinder still loads with the shipped
    /// worldgen (trailer bit 0 = 1).
    /// </summary>
    [Fact]
    public void TheCliSaveLoader_RegeneratesAV32SaveFlat_AndAV33SaveWithTheShippedWorldgen()
    {
        WorldgenConfig shipped = TestConfigs.Worldgen();
        Assert.True(shipped.Topology is { WrapX: true });
        Assert.Null(SaveFiles.WorldgenFor(32, shipped, -1).Topology);                         // v32: the switch off
        Assert.Equal(shipped, SaveFiles.WorldgenFor(33, shipped, -1));                        // v33: as shipped
        Assert.Equal(256, SaveFiles.WorldgenFor(33, shipped, 256).SizePx);

        WorldState v32 = SaveFiles.Load(FixturePath(), -1);
        Assert.False(v32.Terrain!.WrapX);
        Assert.Equal(40, v32.Clock.Turn);
        Assert.Equal(V32FixtureHash, SchemaProjection.V32(v32));

        string dir = Path.Combine(Path.GetTempPath(), "civsim-savefiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WorldState wrapped = Sim.Core.Worldgen.WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
            string path = Path.Combine(dir, "wrapped-v33.civsnap");
            File.WriteAllBytes(path, Saved(wrapped));
            WorldState back = SaveFiles.Load(path, 256);
            Assert.True(back.Terrain!.WrapX);
            Assert.Equal(WorldHash.ComputeHex(wrapped), WorldHash.ComputeHex(back));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TheRealV32Fixture_Loads_ItsTwoV33TablesEmpty_Flat_AndItsV32ProjectionIsTheHashTheV32BuildReported()
    {
        byte[] file = Fixture();
        Assert.Equal(32, BitConverter.ToInt32(file, 8));                        // a genuine v32 header
        WorldState w = Snapshot.Load(new MemoryStream(file), FixtureTerrain());
        Assert.Equal(40, w.Clock.Turn);
        Assert.Equal((0, 0), (w.ConstructionProgress.Count, w.ResourceAllocations.Count));
        Assert.Equal(V32FixtureHash, SchemaProjection.V32(w));
        // The queue the v32 save held (two granaries at settlement 0, no builders there): migrated at p = 0, ordered as
        // EnqueuedTurn −1 (exact: v32 had no partial state).
        ConstructionQueueRow[] q = ConstructionQuery.Queue(w, new SettlementId(0));
        Assert.Equal(2, q.Length);
        Assert.Equal((0, 1, 1, 1), (q[0].Slot, q[1].Slot, q[0].ProjectId, q[1].ProjectId));
        Assert.Equal((0L, -1L), (ConstructionQuery.ProgressOf(w, q[0]), ConstructionQuery.EnqueuedTurnOf(w.ConstructionProgress, q[0])));
        // Re-saved, it is a file of THIS build's schema whose v32 projection is unchanged. D-055 (2026-10-16) CAUSE BLOCK:
        // the header was the literal 33 — this build's schema then; it is v34 now (CanonicalSchema.Version), the layout
        // alone (the v32 projection below, which also drops the empty section 61, is unchanged).
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        WorldState again = Snapshot.Load(new MemoryStream(ms.ToArray()), FixtureTerrain());
        Assert.Equal(CanonicalSchema.Version, BitConverter.ToInt32(ms.ToArray(), 8));
        Assert.Equal(34, CanonicalSchema.Version);
        Assert.Equal(V32FixtureHash, SchemaProjection.V32(again));
    }

    [Fact]
    public void TheRealV32Fixture_ContinuesUnderV33_TheMigratedHeadsProgressOnceTheyHaveBuilders()
    {
        WorldState w = Snapshot.Load(new MemoryStream(Fixture()), FixtureTerrain());
        var head = ConstructionQuery.Queue(w, new SettlementId(0))[0];
        // Give settlement 0 builders and the materials, then run construction alone one step.
        IncrementalConstructionTests.AllBuilders(w, new SettlementId(0));
        IncrementalConstructionTests.Set(w, new SettlementId(0), IncrementalConstructionTests.Timber, 1000);
        IncrementalConstructionTests.Set(w, new SettlementId(0), IncrementalConstructionTests.Stone, 1000);
        WorldState next = IncrementalConstructionTests.ConstructionOnly(TestConfigs.Sim(), null).Step(w);
        Assert.Equal(1L, ConstructionQuery.Built(next, new SettlementId(0), head.ProjectId) - ConstructionQuery.Built(w, new SettlementId(0), head.ProjectId));
        Assert.Single(ConstructionQuery.Queue(next, new SettlementId(0)));
    }

    /// <summary>
    /// D-055 (2026-10-16) CAUSE BLOCK (X1 (b)): the v32 walk is `Layout.Length − 3 + 1` tables (was − 2 + 1: v34 appended a
    /// third block a v32 stream lacks), and the cross-version walk no longer compares the v32 stream with a stream THIS
    /// build serialized (that is v34 now) labelled 33 — the v33 stream is the REAL v33 fixture the batch-3 build wrote
    /// (Fixtures/README-v33-fixture.md). Each stream is walked to its own end by its own header version; a v32 or v33
    /// stream and its migrated v34 stream differ only by the blocks the newer schema appended (empty on the old side).
    /// </summary>
    [Fact]
    public void SnapshotDiff_WalksAV32StreamAndAV33Stream_EachByItsHeaderVersion()
    {
        byte[] file = Fixture();
        int version = BitConverter.ToInt32(file, 8);
        byte[] v32Stream = file[(8 + 4 + 8 + 8)..];                            // a v32 file has no trailer
        SnapshotDiff.Result self = SnapshotDiff.CompareStreams(v32Stream, version, v32Stream, version);
        Assert.True(self.Identical);
        Assert.Equal(v32Stream.Length, self.BytesWalkedA);
        Assert.Equal(SnapshotDiff.Layout.Length - 3 + 1, self.Tables.Count);

        WorldState migrated = Snapshot.Load(new MemoryStream(file), FixtureTerrain());
        byte[] v34Stream = SchemaProjection.Serialize(migrated);
        SnapshotDiff.Result cross = SnapshotDiff.CompareStreams(v32Stream, 32, v34Stream, 34);
        Assert.True(cross.First is null, "a v32 stream and its migrated v34 stream differ only by the three empty appended blocks");
        Assert.Equal(v34Stream.Length, cross.BytesWalkedB);
        Assert.Equal(v32Stream.Length, cross.BytesWalkedA);

        // The REAL v33 stream (the batch-3 build's save): walked to its end as v33, against the v32 stream as v32.
        byte[] v33File = ConstructionHoldsV33FixtureTests.FixtureFile();
        Assert.Equal(33, BitConverter.ToInt32(v33File, 8));
        byte[] v33Stream = v33File[(8 + 4 + 8 + 8)..^4];                       // header | stream | topology trailer
        SnapshotDiff.Result v32v33 = SnapshotDiff.CompareStreams(v32Stream, 32, v33Stream, 33);
        Assert.Equal(v32Stream.Length, v32v33.BytesWalkedA);
        Assert.Equal(v33Stream.Length, v32v33.BytesWalkedB);
        Assert.Equal(SnapshotDiff.Layout.Length - 1 + 1, v32v33.Tables.Count);   // the v33 walk, the longer of the two
        Assert.False(v32v33.Identical);                                          // two different worlds (flat turn 40, cylinder turn 2)
        Assert.Throws<ArgumentOutOfRangeException>(() => SnapshotDiff.LayoutFor(31));
        Assert.Throws<ArgumentOutOfRangeException>(() => SnapshotDiff.LayoutFor(35));
    }
}
