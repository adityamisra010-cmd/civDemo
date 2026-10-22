using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// M6 Track EX (D-062; ADR-044) — SCHEMA v35, the four discovery sections (63 KnownExtents 16 bytes, 64 Explorations 12,
/// 65 SettlementSightings 16, 66 SettlementSurveys 16) appended after section 61 (section 62 is Track CP's and merges in
/// between); the readable set {32, 33, 34, 35}; the v34 → v35 migration (the four tables left EMPTY, the seed extent
/// re-derived by the first step); save / load / replay hash equality on a populated world. Constitution rule: every new
/// serialized row type ships a POPULATED-table test — exact ExpectedLength, bit-exact round trip, hash equality — with
/// extreme field values.
/// </summary>
public class DiscoverySchemaV35Tests
{
    private static WorldState Populated()
    {
        var w = new WorldState(35);
        w.KnownExtents.Add(new KnownExtentRow(new PolityId(1), 0, 1UL));
        w.KnownExtents.Add(new KnownExtentRow(new PolityId(1), 1023, ulong.MaxValue));
        w.KnownExtents.Add(new KnownExtentRow(new PolityId(int.MaxValue), int.MaxValue, 0x8000_0000_0000_0000UL));
        w.Explorations.Add(new ExplorationRow(new PolityId(1), new SettlementId(0), 6));
        w.Explorations.Add(new ExplorationRow(new PolityId(2), new SettlementId(int.MaxValue), int.MaxValue));
        w.SettlementSightings.Add(new SettlementSightingRow(new PolityId(1), new SettlementId(3), 0));
        w.SettlementSightings.Add(new SettlementSightingRow(new PolityId(1), new SettlementId(9), long.MaxValue));
        w.SettlementSurveys.Add(new SettlementSurveyRow(new PolityId(0), new SettlementId(5), 17));
        w.SettlementSurveys.Add(new SettlementSurveyRow(new PolityId(3), new SettlementId(1), long.MaxValue));
        return w;
    }

    private static byte[] Saved(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    [Fact]
    public void SchemaV35_PopulatedDiscoveryTables_ExpectedLengthBitExactRoundTripAndHashEquality()
    {
        Assert.Equal(35, CanonicalSchema.Version);
        WorldState world = Populated();
        Assert.Null(DiscoveryQuery.InvariantViolation(world));
        byte[] bytes = SchemaProjection.Serialize(world);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), bytes.Length);
        Assert.Equal(3 * 16 + 2 * 12 + 2 * 16 + 2 * 16, CanonicalSchema.ExpectedLength(world) - CanonicalSchema.ExpectedLength(new WorldState(35)));

        // Sections 63–66 are the LAST four blocks: (count, rows) each, little-endian, in declaration order.
        int surveys = bytes.Length - (4 + 2 * 16);
        int sightings = surveys - (4 + 2 * 16);
        int explorations = sightings - (4 + 2 * 12);
        int known = explorations - (4 + 3 * 16);
        Assert.Equal(3, BitConverter.ToInt32(bytes, known));
        Assert.Equal(1023, BitConverter.ToInt32(bytes, known + 4 + 16 + 4));
        Assert.Equal(ulong.MaxValue, BitConverter.ToUInt64(bytes, known + 4 + 16 + 8));
        Assert.Equal(2, BitConverter.ToInt32(bytes, explorations));
        Assert.Equal(int.MaxValue, BitConverter.ToInt32(bytes, explorations + 4 + 12 + 8));
        Assert.Equal(2, BitConverter.ToInt32(bytes, sightings));
        Assert.Equal(long.MaxValue, BitConverter.ToInt64(bytes, sightings + 4 + 16 + 8));
        Assert.Equal(2, BitConverter.ToInt32(bytes, surveys));
        Assert.Equal(17L, BitConverter.ToInt64(bytes, surveys + 4 + 8));
        Assert.Equal(long.MaxValue, BitConverter.ToInt64(bytes, bytes.Length - 8));

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            WorldState back = CanonicalSchema.Read(reader);
            Assert.True(WorldStates.StateEquals(world, back), "round trip drifted");
            Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
            Assert.Equal(bytes, SchemaProjection.Serialize(back));
        }
        WorldState loaded = Snapshot.Load(new MemoryStream(Saved(world)));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));

        // Every field of every table is hashed.
        string h = WorldHash.ComputeHex(world);
        var variants = new List<WorldState>();
        WorldState V() { WorldState c = world.Clone(); variants.Add(c); return c; }
        V().KnownExtents[0] = world.KnownExtents[0] with { Polity = new PolityId(0) };
        V().KnownExtents[1] = world.KnownExtents[1] with { Word = 1022 };
        V().KnownExtents[1] = world.KnownExtents[1] with { Bits = 2UL };
        V().Explorations[0] = world.Explorations[0] with { Polity = new PolityId(0) };
        V().Explorations[0] = world.Explorations[0] with { Settlement = new SettlementId(1) };
        V().Explorations[0] = world.Explorations[0] with { RadiusNodes = 9 };
        V().SettlementSightings[0] = world.SettlementSightings[0] with { Polity = new PolityId(0) };
        V().SettlementSightings[0] = world.SettlementSightings[0] with { Settlement = new SettlementId(4) };
        V().SettlementSightings[0] = world.SettlementSightings[0] with { LastSeenTurn = 1 };
        V().SettlementSurveys[0] = world.SettlementSurveys[0] with { Polity = new PolityId(1) };
        V().SettlementSurveys[0] = world.SettlementSurveys[0] with { Settlement = new SettlementId(6) };
        V().SettlementSurveys[0] = world.SettlementSurveys[0] with { Turn = 18 };
        foreach (WorldState v in variants) Assert.NotEqual(h, WorldHash.ComputeHex(v));
        Assert.Equal(6, world.Explorations[0].RadiusNodes);   // the clones are independent
    }

    [Fact]
    public void Load_RefusesDiscoveryTablesNoRuleWrites()
    {
        static string Refusal(WorldState w) => Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(new MemoryStream(Saved(w)))).Message;
        var a = new WorldState(5);
        a.KnownExtents.Add(new KnownExtentRow(new PolityId(1), 4, 1));
        a.KnownExtents.Add(new KnownExtentRow(new PolityId(1), 2, 1));
        Assert.Contains("KnownExtents", Refusal(a));
        var b = new WorldState(5);
        b.KnownExtents.Add(new KnownExtentRow(new PolityId(1), 4, 0));
        Assert.Contains("non-zero", Refusal(b));
        var c = new WorldState(5);
        c.Explorations.Add(new ExplorationRow(new PolityId(2), new SettlementId(0), 6));
        c.Explorations.Add(new ExplorationRow(new PolityId(1), new SettlementId(0), 6));
        Assert.Contains("Explorations", Refusal(c));
        var d = new WorldState(5);
        d.SettlementSightings.Add(new SettlementSightingRow(new PolityId(1), new SettlementId(2), 0));
        d.SettlementSightings.Add(new SettlementSightingRow(new PolityId(1), new SettlementId(2), 1));
        Assert.Contains("SettlementSightings", Refusal(d));
        var e = new WorldState(5);
        e.SettlementSurveys.Add(new SettlementSurveyRow(new PolityId(1), new SettlementId(2), -1));
        Assert.Contains("SettlementSurveys", Refusal(e));
    }

    [Fact]
    public void TheReadableSet_Is32To35_AndTheDiffLayoutAndMigrationNoteAgree()
    {
        foreach (int v in new[] { 32, 33, 34, 35 }) Assert.True(CanonicalSchema.IsReadable(v));
        foreach (int v in new[] { 31, 36, 0 }) Assert.False(CanonicalSchema.IsReadable(v));
        Assert.Equal(SnapshotDiff.Layout.Length, SnapshotDiff.LayoutFor(35).Length);
        Assert.Equal("SettlementSurveys", SnapshotDiff.Layout[^1].Name);
        Assert.Equal(SnapshotDiff.Layout.Length - 4, SnapshotDiff.LayoutFor(34).Length);
        Assert.Equal("ConstructionHolds", SnapshotDiff.LayoutFor(34)[^1].Name);
        Assert.Equal("ResourceAllocations", SnapshotDiff.LayoutFor(33)[^1].Name);
        Assert.Equal("TaxGrievance", SnapshotDiff.LayoutFor(32)[^1].Name);
        Assert.NotNull(SaveFiles.MigrationNote("x", 34));
        Assert.Contains("KnownExtents", SaveFiles.MigrationNote("x", 34)!);
        Assert.Null(SaveFiles.MigrationNote("x", 35));
        // A populated v35 stream walks to its end.
        byte[] bytes = SchemaProjection.Serialize(Populated());
        SnapshotDiff.Result self = SnapshotDiff.CompareStreams(bytes, 35, bytes, 35);
        Assert.True(self.Identical);
        Assert.Equal(bytes.Length, self.BytesWalkedA);
        Assert.Equal(2, self.Tables[^1].RowsA);
    }

    /// <summary>THE v34 → v35 MIGRATION on a real fogged world: a v34 save of it (the same header, the stream to section 61,
    /// the topology trailer) loads with the four tables EMPTY, hashes in its v34 form as the v34 build hashed it, and the
    /// first step re-derives each polity's seed extent — the polity knows its settlements' seed discs again, nothing it
    /// had explored beyond them.</summary>
    [Fact]
    public void AV34Save_LoadsWithTheDiscoveryTablesEmpty_AndTheFirstStepReDerivesTheSeed()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        var log = new OrderLog();
        log.Append(OrderRecord.From(0, new PolityId(1), OrderKind.Explore, w.Settlements[0].Id.Value, 0.0));
        TurnExecutor exec = Exec(cfg, log, wg);
        WorldState w2 = exec.Step(exec.Step(w));
        Assert.True(w2.KnownExtents.Count > 0 && w2.Explorations.Count == 1);

        // The v34 file: the v35 file with its version field 34 and the four trailing tables' bytes removed.
        byte[] v35 = Saved(w2);
        int tables = CanonicalSchema.BytesAppendedSince(w2, 34);
        byte[] v34 = new byte[v35.Length - tables];
        Array.Copy(v35, v34, v35.Length - tables - 4);
        Array.Copy(v35, v35.Length - 4, v34, v34.Length - 4, 4);   // the topology trailer
        BitConverter.GetBytes(34).CopyTo(v34, 8);
        TerrainSet terrain = Sim.Core.Worldgen.Worldgen.Generate(wg, 42);
        WorldState migrated = Snapshot.Load(new MemoryStream(v34), terrain);
        Assert.Equal(0, migrated.KnownExtents.Count + migrated.Explorations.Count + migrated.SettlementSightings.Count + migrated.SettlementSurveys.Count);
        Assert.Equal(WorldHash.ComputeHexAsSchema(w2, 34), WorldHash.ComputeHexAsSchema(migrated, 34));   // the v34 build's hash
        WorldState stripped = w2.Clone();
        SchemaProjection.StripV35(stripped);
        Assert.Equal(WorldHash.ComputeHex(stripped), WorldHash.ComputeHex(migrated));

        // Before any step the seam still knows the seed (rule E2, applied at query time), and the explored ring is gone.
        PolityKnowledge k = PolityKnowledge.Of(migrated, new PolityId(1), cfg);
        int node = DiscoveryQuery.NodeOfSettlement(migrated, w2.Settlements[0].Id);
        Assert.True(k.Known(node));
        WorldState stepped = Exec(cfg, new OrderLog(), wg).Step(migrated);
        Assert.True(stepped.KnownExtents.Count > 0);
        Assert.Equal(0, stepped.Explorations.Count);
        ulong[] seedOnly = DiscoveryQuery.KnownBits(migrated, cfg.Fog!, new PolityId(1))!;
        ulong[] stored = DiscoveryQuery.StoredExtent(stepped, new PolityId(1), seedOnly.Length);
        Assert.Equal(seedOnly, stored);
    }

    /// <summary>Save / load / replay: a fogged world with an expedition and a survey, saved mid-run, loads and continues to
    /// the hash the uninterrupted run reaches; the order log replays it hash for hash.</summary>
    [Fact]
    public void SaveLoadReplay_OnAPopulatedFoggedWorld_HashEqual()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        WorldState w0 = WorldFounding.Found(wg, cfg, 42);
        SettlementId s = w0.Settlements[0].Id;
        var log = new OrderLog();
        log.Append(OrderRecord.From(0, new PolityId(1), OrderKind.Explore, s.Value, 0.0));
        log.Append(OrderRecord.From(2, new PolityId(1), OrderKind.Explore, s.Value, 0.0));
        TurnExecutor exec = Exec(cfg, log, wg);
        WorldState w = w0;
        var hashes = new List<string>();
        for (int t = 0; t < 4; t++) { w = exec.Step(w); hashes.Add(WorldHash.ComputeHex(w)); }
        Assert.Equal(1, w.Explorations.Count);

        // Load the turn-2 save and continue.
        WorldState at2 = w0;
        TurnExecutor again = Exec(cfg, log, wg);
        for (int t = 0; t < 2; t++) at2 = again.Step(at2);
        WorldState loaded = Snapshot.Load(new MemoryStream(Saved(at2)), Sim.Core.Worldgen.Worldgen.Generate(wg, 42));
        Assert.Equal(hashes[1], WorldHash.ComputeHex(loaded));
        for (int t = 2; t < 4; t++) loaded = again.Step(loaded);
        Assert.Equal(hashes[3], WorldHash.ComputeHex(loaded));

        // Replay from the seed and the log.
        WorldState replay = WorldFounding.Found(wg, cfg, 42);
        TurnExecutor replayer = Exec(cfg, log, wg);
        for (int t = 0; t < 4; t++) { replay = replayer.Step(replay); Assert.Equal(hashes[t], WorldHash.ComputeHex(replay)); }
    }

    private static TurnExecutor Exec(SimConfig cfg, OrderLog log, WorldgenConfig wg)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), log);
    }
}
