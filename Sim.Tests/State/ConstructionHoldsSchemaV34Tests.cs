using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 1; the batch-4 design X1 and X2) — SCHEMA v34: section 61
/// ConstructionHolds (Purse, Good, Units; 16 bytes) appended after ResourceAllocations; the readable SET {32, 33, 34}
/// through the one helper CanonicalSchema.IsReadable; the Snapshot.Load refusals of a table no rule writes; the switch
/// sim.json construction.reservations and its loader refusal; construction CLEARING the table every step whatever the
/// switch; and the SHIPPED pipeline order that lets road development read this step's set-asides (construction before
/// roaddevelopment — order is data; no kernel change, T1a round 2).
/// Constitution rule: a new serialized row type ships a POPULATED-table test (exact ExpectedLength, a bit-exact round trip,
/// hash equality) — rows with negative AND positive purses, int.MaxValue / long.MaxValue fields.
/// </summary>
public class ConstructionHoldsSchemaV34Tests
{
    /// <summary>A world holding three hold rows in the table's order — a single-settlement purse (−1 − 2 = −3), an Empire
    /// purse (1) with two goods — plus extreme values in a fourth row.</summary>
    private static WorldState Populated()
    {
        var w = new WorldState(34);
        w.ConstructionHolds.Add(new ConstructionHoldRow(int.MinValue, 0, 1));
        w.ConstructionHolds.Add(new ConstructionHoldRow(Reservations.SettlementPurse(new SettlementId(2)), 7, 12));
        w.ConstructionHolds.Add(new ConstructionHoldRow(Reservations.PolityPurse(new PolityId(1)), 3, 40));
        w.ConstructionHolds.Add(new ConstructionHoldRow(1, int.MaxValue, long.MaxValue));
        return w;
    }

    private static byte[] Saved(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    // ================================================================== the row, populated

    [Fact]
    public void SchemaV34_PopulatedHolds_ExpectedLengthBitExactRoundTripAndHashEquality()
    {
        Assert.Equal(35, CanonicalSchema.Version);   // v35 (ADR-043 CivicsPoints, 2026-10-22): the schema bump alone
        WorldState world = Populated();
        Assert.Null(Reservations.InvariantViolation(world.ConstructionHolds));   // a table a rule could write
        byte[] bytes = SchemaProjection.Serialize(world);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), bytes.Length);
        Assert.Equal(4 * 16, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(34)));   // 16 bytes a row

        // Section 61 was the LAST block at v34: its count prefix, then each row as Purse, Good, Units (little-endian).
        // ADR-043 (v35, 2026-10-22) CAUSE BLOCK: section 62 (CivicsPoints, EMPTY here) follows it — every offset below moves
        // by its 4-byte zero count prefix (`tail`). The layout alone.
        const int tail = 4;
        Assert.Equal(0, BitConverter.ToInt32(bytes, bytes.Length - tail));
        int start = bytes.Length - tail - 4 - 4 * 16;
        Assert.Equal(4, BitConverter.ToInt32(bytes, start));
        Assert.Equal(int.MinValue, BitConverter.ToInt32(bytes, start + 4));
        Assert.Equal(-3, BitConverter.ToInt32(bytes, start + 4 + 16));             // −1 − settlement 2
        Assert.Equal(7, BitConverter.ToInt32(bytes, start + 4 + 16 + 4));
        Assert.Equal(12L, BitConverter.ToInt64(bytes, start + 4 + 16 + 8));
        Assert.Equal(long.MaxValue, BitConverter.ToInt64(bytes, bytes.Length - tail - 8));

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            WorldState back = CanonicalSchema.Read(reader);
            Assert.True(WorldStates.StateEquals(world, back), "round trip drifted");
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
            for (int i = 0; i < world.ConstructionHolds.Count; i++) Assert.Equal(world.ConstructionHolds[i], back.ConstructionHolds[i]);
            Assert.Equal(bytes, SchemaProjection.Serialize(back));                  // bit-exact re-serialization
        }

        // Every field is hashed.
        string h = WorldHash.ComputeHex(world);
        WorldState[] variants = [world.Clone(), world.Clone(), world.Clone()];
        variants[0].ConstructionHolds[1] = variants[0].ConstructionHolds[1] with { Purse = -4 };
        variants[1].ConstructionHolds[1] = variants[1].ConstructionHolds[1] with { Good = 8 };
        variants[2].ConstructionHolds[1] = variants[2].ConstructionHolds[1] with { Units = 13 };
        foreach (WorldState v in variants) Assert.NotEqual(h, WorldHash.ComputeHex(v));
        // The clone is independent.
        Assert.Equal(12L, world.ConstructionHolds[1].Units);
        Assert.False(WorldStates.StateEquals(world, variants[2]));
    }

    /// <summary>The populated table through a real SNAPSHOT of the shipped cylinder's dev world: header | stream to section
    /// 61 | trailer (bit 0 = 1); it loads against its regenerated cylinder bit-exactly and re-saves to the identical bytes.</summary>
    [Fact]
    public void SchemaV34_PopulatedHolds_SnapshotRoundTrip_OnAWrappedWorld_BitExact()
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        Assert.True(w.Terrain!.WrapX);
        w.ConstructionHolds.Add(new ConstructionHoldRow(Reservations.SettlementPurse(new SettlementId(3)), 2, 5));
        w.ConstructionHolds.Add(new ConstructionHoldRow(Reservations.PolityPurse(new PolityId(1)), 2, 999_999));
        byte[] file = Saved(w);
        Assert.Equal(CanonicalSchema.Version, BitConverter.ToInt32(file, 8));   // ADR-043: this build saves v35 (was the literal 34)
        Assert.Equal(8 + 4 + 8 + 8 + CanonicalSchema.ExpectedLength(w) + 4, file.Length);
        Assert.Equal(TopologyFlags.WrapX, BitConverter.ToInt32(file, file.Length - 4));
        WorldState back = Snapshot.Load(new MemoryStream(file), Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.DevWorldgen(), 42));
        Assert.True(WorldStates.StateEquals(w, back));
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(back));
        Assert.Equal(file, Saved(back));
    }

    [Fact]
    public void ThePurseKeys_PolityIdsAreNonNegative_SettlementPursesAreMinusOneMinusTheId_AndTheRangesNeverMeet()
    {
        Assert.Equal(0, Reservations.PolityPurse(new PolityId(0)));
        Assert.Equal(int.MaxValue, Reservations.PolityPurse(new PolityId(int.MaxValue)));
        Assert.Equal(-1, Reservations.SettlementPurse(new SettlementId(0)));
        Assert.Equal(int.MinValue, Reservations.SettlementPurse(new SettlementId(int.MaxValue)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reservations.PolityPurse(new PolityId(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reservations.SettlementPurse(new SettlementId(-1)));
    }

    // ================================================================== Snapshot.Load refuses a table no rule writes

    /// <summary>A save of a terrain-less world holding <paramref name="rows"/> in that order.</summary>
    private static byte[] SavedWith(params ConstructionHoldRow[] rows)
    {
        var w = new WorldState(5);
        foreach (ConstructionHoldRow r in rows) w.ConstructionHolds.Add(r);
        return Saved(w);
    }

    private static string Refusal(byte[] file) =>
        Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(file))).Message;

    [Fact]
    public void Load_RefusesUnsortedHoldRows_ByPurse_AndByGoodWithinAPurse()
    {
        string byPurse = Refusal(SavedWith(new ConstructionHoldRow(1, 0, 5), new ConstructionHoldRow(-1, 0, 5)));
        Assert.Contains("save is corrupt: construction hold rows out of order at row 1", byPurse);
        Assert.Contains("(schema v34 invariant)", byPurse);
        string byGood = Refusal(SavedWith(new ConstructionHoldRow(1, 4, 5), new ConstructionHoldRow(1, 3, 5)));
        Assert.Contains("construction hold rows out of order at row 1: (purse 1, good 4) before (purse 1, good 3)", byGood);
        // The same rows sorted load (the positive control).
        WorldState ok = Snapshot.Load(new MemoryStream(SavedWith(new ConstructionHoldRow(-1, 0, 5), new ConstructionHoldRow(1, 3, 5), new ConstructionHoldRow(1, 4, 5))));
        Assert.Equal(3, ok.ConstructionHolds.Count);
    }

    [Fact]
    public void Load_RefusesADuplicatePurseAndGood()
    {
        string e = Refusal(SavedWith(new ConstructionHoldRow(2, 3, 5), new ConstructionHoldRow(2, 3, 9)));
        Assert.Contains("save is corrupt: two construction hold rows for purse 2, good 3", e);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void Load_RefusesAHoldOfNoPositiveAmount(long units)
    {
        string e = Refusal(SavedWith(new ConstructionHoldRow(-1, 0, 5), new ConstructionHoldRow(1, 3, units)));
        Assert.Contains("save is corrupt: construction hold row 1 (purse 1, good 3) sets aside", e);
        Assert.Contains("a hold row exists only for a positive amount", e);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Load_RefusesANegativeGood(int good)
    {
        string e = Refusal(SavedWith(new ConstructionHoldRow(1, good, 5)));
        Assert.Contains("save is corrupt: construction hold row 0 (purse 1) names good", e);
        Assert.Contains("a good id is never negative", e);
    }

    // ================================================================== the readable set {32, 33, 34}

    [Fact]
    public void TheReadableSet_Is32_33_34_ThroughOneHelper_AndEveryReaderAgrees()
    {
        // ADR-043 (2026-10-22) CAUSE BLOCK: v35 joined the set ({32, 33, 34, 35}; CivicsPointsSchemaV35Tests pins it), so the
        // version above the set is 36 (was 35) and LayoutFor(34) is the layout less the v35 block. Re-rigged, not re-pinned.
        foreach (int v in new[] { 32, 33, 34 }) Assert.True(CanonicalSchema.IsReadable(v));
        foreach (int v in new[] { 31, 36, 0, 27, -1, int.MaxValue }) Assert.False(CanonicalSchema.IsReadable(v));

        // CanonicalSchema.Read: the readable ones read; the rest are refused.
        foreach (int v in new[] { 31, 36, 0, 27 })
            Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalSchema.Read(new BinaryReader(new MemoryStream(new byte[64])), v, out _));

        // Snapshot.Load: v32 (the batch-2 fixture), v33 (the batch-3 fixture) and v34 (a save of this build) load.
        WorldState v32 = Snapshot.Load(new MemoryStream(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "v32-seed42-turn40.civsnap"))),
            Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.PreD054Worldgen(), 42));
        Assert.Equal(0, v32.ConstructionHolds.Count);
        WorldState v33 = Snapshot.Load(new MemoryStream(ConstructionHoldsV33FixtureTests.FixtureFile()),
            Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen() with { SizePx = 256 }, 42));
        Assert.Equal(0, v33.ConstructionHolds.Count);
        WorldState v34 = Snapshot.Load(new MemoryStream(Saved(Populated())));
        Assert.Equal(4, v34.ConstructionHolds.Count);

        // SnapshotDiff.LayoutFor: v34 the layout less the v35 block, v33 less two, v32 less four; the rest refused.
        Assert.Equal(SnapshotDiff.Layout.Length - 1, SnapshotDiff.LayoutFor(34).Length);
        Assert.Equal("ConstructionHolds", SnapshotDiff.LayoutFor(34)[^1].Name);
        Assert.Equal(SnapshotDiff.Layout.Length - 2, SnapshotDiff.LayoutFor(33).Length);
        Assert.Equal("ResourceAllocations", SnapshotDiff.LayoutFor(33)[^1].Name);
        Assert.Equal(SnapshotDiff.Layout.Length - 4, SnapshotDiff.LayoutFor(32).Length);
        Assert.Equal("TaxGrievance", SnapshotDiff.LayoutFor(32)[^1].Name);
        foreach (int v in new[] { 31, 36, 0, 27 })
            Assert.Throws<ArgumentOutOfRangeException>(() => SnapshotDiff.LayoutFor(v));

        // SaveFiles.MigrationNote: a note for each older member, none for this build's or any refused version.
        Assert.NotNull(SaveFiles.MigrationNote("x", 32));
        Assert.NotNull(SaveFiles.MigrationNote("x", 33));
        Assert.NotNull(SaveFiles.MigrationNote("x", 34));
        foreach (int v in new[] { 35, 31, 36, 0, 27 }) Assert.Null(SaveFiles.MigrationNote("x", v));
    }

    /// <summary>The ConstructionHolds block's layout widths sum to the row width: a populated v34 stream walks to EXACTLY
    /// its length, with the holds table's row count read back.</summary>
    [Fact]
    public void SnapshotDiff_WalksAPopulatedV34Stream_ToItsEnd_AndAV33StreamAgainstItsMigratedV34Stream_DiffersOnlyInTheEmptyBlock()
    {
        // ADR-043 (2026-10-22) CAUSE BLOCK: this build's stream is v35 — walked as v35, the holds block is second to last
        // (CivicsPoints, empty, follows it), and the migrated stream is 8 bytes longer than the v33 one (was 4).
        byte[] bytes = SchemaProjection.Serialize(Populated());
        SnapshotDiff.Result self = SnapshotDiff.CompareStreams(bytes, CanonicalSchema.Version, bytes, CanonicalSchema.Version);
        Assert.True(self.Identical);
        Assert.Equal(bytes.Length, self.BytesWalkedA);
        Assert.Equal(4, self.Tables[^2].RowsA);
        Assert.Equal("ConstructionHolds", self.Tables[^2].Table);

        byte[] v33File = ConstructionHoldsV33FixtureTests.FixtureFile();
        byte[] v33Stream = v33File[(8 + 4 + 8 + 8)..^4];
        WorldState migrated = Snapshot.Load(new MemoryStream(v33File), Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen() with { SizePx = 256 }, 42));
        byte[] v34Stream = SchemaProjection.Serialize(migrated);
        SnapshotDiff.Result cross = SnapshotDiff.CompareStreams(v33Stream, 33, v34Stream, CanonicalSchema.Version);
        Assert.True(cross.First is null, "a v33 stream and its migrated v34 stream differ only by the empty v34 block");
        Assert.Equal(v33Stream.Length, cross.BytesWalkedA);
        Assert.Equal(v34Stream.Length, cross.BytesWalkedB);
        Assert.Equal((0, 0), (cross.Tables[^1].RowsA, cross.Tables[^1].RowsB));
        Assert.Equal(v33Stream.Length + 8, v34Stream.Length);
    }

    // ================================================================== the switch

    private static System.Text.Json.Nodes.JsonNode ShippedSimJson()
    {
        string json;
        using (var reader = new StreamReader(Sim.Data.DataFiles.OpenSim())) json = reader.ReadToEnd();
        return System.Text.Json.Nodes.JsonNode.Parse(json)!;
    }

    [Fact]
    public void TheShippedSimJson_SwitchesReservationsOn()
    {
        Assert.True((bool)ShippedSimJson()["construction"]!["reservations"]!);
        Assert.True(TestConfigs.Sim().Construction!.Reservations);
    }

    [Fact]
    public void TheLoader_RefusesReservationsWithoutIncrementalConstruction_AndDoesNotRequireTheEmpirePurse()
    {
        var doc = ShippedSimJson();
        doc["construction"]!["incremental"] = false;
        doc["construction"]!["allocationRatios"] = false;                        // the split's own refusal out of the way
        var e = Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(doc.ToJsonString()));
        Assert.Contains("construction.reservations requires construction.incremental to be true (D-055)", e.Message);
        Assert.Contains("got incremental false", e.Message);

        // D-058 (2026-10-19) CAUSE BLOCK: the shipped block now also carries continueWithinTurn true, which the loader refuses
        // without incremental (SimConfigLoader, D-058) — so the "same block loads" arm switches it off as well. The
        // reservations refusal above is unchanged (it is checked first).
        doc["construction"]!["continueWithinTurn"] = false;
        doc["construction"]!["reservations"] = false;                            // the same block without reservations loads
        Assert.False(SimConfigLoader.Load(doc.ToJsonString()).Construction!.Reservations);

        var purseOff = ShippedSimJson();                                         // incremental on, the Empire purse off
        purseOff["construction"]!["empireStockpile"] = false;
        purseOff["construction"]!["allocationRatios"] = false;
        SimConfig loaded = SimConfigLoader.Load(purseOff.ToJsonString());
        Assert.True(loaded.Construction!.Reservations);
        Assert.True(Reservations.Enabled(loaded));

        var absent = ShippedSimJson();                                           // absent: off (the record default)
        absent["construction"]!.AsObject().Remove("reservations");
        Assert.False(Reservations.Enabled(SimConfigLoader.Load(absent.ToJsonString())));
    }

    // ================================================================== construction clears the table every step

    /// <summary>
    /// X1 — THE TABLE IS CLEARED AT THE START OF EVERY CONSTRUCTION STEP, WHATEVER THE SWITCH, and is never an input to
    /// construction (road development, after it in the shipped order, reads what it publishes; ConstructionReservationTests
    /// #22 pins what another order reads): a world
    /// carrying hold rows (well-formed, as a save could carry them) steps to exactly the next world of the same state without
    /// the rows — with reservations on, off (PreReservations), and with incremental construction off altogether
    /// (PreIncrementalConstruction: the batch-2 whole-or-nothing gate, which never publishes a hold); with the queue empty
    /// and with a head waiting. Where the step publishes nothing the stale rows are gone (0 rows). Where it publishes — the
    /// waiting head's purse with the rule on — it publishes in the ENQUEUE step: S0's granary (no builders) sets aside the
    /// purse's 30 timber and 10 stone, and the PREV row on that very purse and good (purse 1, timber, 30) must not lower
    /// what it sets aside (a walk whose budget read PREV's rows would set aside 0 timber).
    /// Fix round 2026-10-16 (D-055; findings T1a-L2-V2-01 and T1a-L2-V2-02): the waiting arm's purse now holds stock below
    /// the granary's need, so it publishes and the "never reads them" half has teeth; the incremental-off arms pin "whatever
    /// the switch" beyond the reservations switch.
    /// </summary>
    [Theory]
    [InlineData("on", false)]
    [InlineData("off", false)]
    [InlineData("noIncremental", false)]
    [InlineData("on", true)]
    [InlineData("off", true)]
    [InlineData("noIncremental", true)]
    public void Construction_ClearsTheHoldsEveryStep_WhateverTheSwitch_AndNeverReadsThem(string rule, bool waitingHead)
    {
        SimConfig cfg = rule switch
        {
            "on" => TestConfigs.Sim(),
            "off" => TestConfigs.PreReservations(TestConfigs.Sim()),
            _ => TestConfigs.PreIncrementalConstruction(TestConfigs.Sim()),
        };
        Assert.Equal(rule == "on", Reservations.Enabled(cfg));
        Assert.Equal(rule != "noIncremental", ConstructionQuery.Incremental(cfg));
        WorldState clean = IncrementalConstructionTests.Solo();
        OrderLog? log = null;
        if (waitingHead)
        {
            IncrementalConstructionTests.NoBuilders(clean, IncrementalConstructionTests.S0);       // a head that waits
            IncrementalConstructionTests.Set(clean, IncrementalConstructionTests.S0, IncrementalConstructionTests.Timber, 30);
            IncrementalConstructionTests.Set(clean, IncrementalConstructionTests.S0, IncrementalConstructionTests.Stone, 10);
            log = IncrementalConstructionTests.Log(IncrementalConstructionTests.Enqueue(0, IncrementalConstructionTests.Player, IncrementalConstructionTests.S0));
        }
        WorldState held = clean.Clone();
        held.ConstructionHolds.Add(new ConstructionHoldRow(Reservations.SettlementPurse(IncrementalConstructionTests.S2), IncrementalConstructionTests.Stone, 7));
        held.ConstructionHolds.Add(new ConstructionHoldRow(Reservations.PolityPurse(IncrementalConstructionTests.Player), IncrementalConstructionTests.Timber, 30));

        TurnExecutor exec = IncrementalConstructionTests.ConstructionOnly(cfg, log);
        WorldState next = exec.Step(held), control = exec.Step(clean);
        Assert.True(WorldStates.StateEquals(control, next), "a hold row in PREV changed the next world");
        Assert.Equal(WorldHash.ComputeHex(control), WorldHash.ComputeHex(next));
        if (rule == "on" && waitingHead)
        {
            int purse = Reservations.PolityPurse(IncrementalConstructionTests.Player);             // PREMISE: it publishes
            Assert.Equal([new ConstructionHoldRow(purse, IncrementalConstructionTests.Timber, 30),
                          new ConstructionHoldRow(purse, IncrementalConstructionTests.Stone, 10)], control.ConstructionHolds.Rows());
            Assert.Equal(control.ConstructionHolds.Rows(), next.ConstructionHolds.Rows());
        }
        else
            Assert.Equal(0, next.ConstructionHolds.Count);                                          // cleared
        if (waitingHead) Assert.Single(ConstructionQuery.Queue(next, IncrementalConstructionTests.S0));   // the premise: a head waits
    }

    // ================================================================== order is data: the shipped pipeline runs construction first

    private static SystemRegistration[] ShippedPipeline(SimConfig cfg)
    {
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.Worldgen()));
    }

    /// <summary>
    /// L3-07, restated in T1a round 2 (2026-10-16: the kernel is frozen — no kernel edge; ORDER IS DATA, the GoodStocks
    /// share's pattern). Road development is handed construction's NEXT ConstructionHolds read-only (SystemCatalog) and reads
    /// it as it stands when it runs, so whether road works see THIS step's set-asides is decided by pipeline.json alone. THE
    /// SHIPPED pipeline.json — loaded through PipelineLoader with the shipped catalogue, exactly as the game builds its
    /// executor — runs construction before roaddevelopment (today the 10th and 22nd systems). What a non-shipped order reads is
    /// pinned by ConstructionReservationTests #22.
    /// </summary>
    [Fact]
    public void TheShippedPipeline_RunsConstructionBeforeRoadDevelopment()
    {
        SystemRegistration[] shipped = ShippedPipeline(TestConfigs.Sim());
        int c = Array.FindIndex(shipped, x => x.Name == Sim.Core.Systems.Construction.ConstructionSystem.Name);
        int r = Array.FindIndex(shipped, x => x.Name == Sim.Core.Systems.Roads.RoadDevelopmentSystem.Name);
        Assert.True(c >= 0, "the shipped pipeline runs construction");
        Assert.True(r >= 0, "the shipped pipeline runs road development");
        Assert.True(r > c, $"pipeline.json must run construction before roaddevelopment (construction {c}, roaddevelopment {r})");
    }
}
