using System.Globalization;
using System.Security.Cryptography;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-056 (2026-10-18; the Director's mandate "Fix the game", §4) — THE LEGACY-CANCEL GUARD. A recorded cancel must never
/// silently remove a different entry. Pinned here:
/// <list type="bullet">
/// <item>kind 13 (CancelConstructionEntry): the payload's packing, its load-time range checks, a POPULATED log's bit-exact
///   round trip, and its resolution (ConstructionQuery.CancelTargetIndex) — by identity, the recorded slot preferred, no
///   redirect to an entry of another identity, an ambiguous identity refused at delivery;</item>
/// <item>the emitters issue kind 13, and in the recorded world it changes exactly what the legacy kind 11 changed (the
///   same worlds, hash for hash);</item>
/// <item>ReplayGuard: the readable-schema forms of a world's hash (cross-checked against the test-side projection), and
///   the check before a kind-11 batch (no trace, no row, a mismatch: refused; a match in any form: accepted);</item>
/// <item>every CLI path, each in its own process: the bare-log runners (`run --orders`, `run --founded --orders
///   --emit-session`, `replay --orders`, `replay --emit-session`, `run --from-save --orders`, `research --orders`) refuse a
///   kind-11 log BEFORE writing anything, and leave their inputs byte-identical; `sim inspect` accepts a compatible
///   historical session (its trace in v34 or v33 form) and refuses a diverged one with nothing written; `run --from-save S
///   --save S` is refused and S is untouched.</item>
/// </list>
/// The UI resume's half is Sim.Ui.Tests' LegacyCancelResumeTests.
/// </summary>
public class LegacyCancelGuardTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);
    private static readonly string[] Founded = ["--founded", "--seed", "42", "--size", "256", "--settlements", "4"];
    private const int Player = 1;
    private const int Granary = 1;
    private static readonly SettlementId S0 = new(0), S1 = new(1);

    // ================================================================== kind 13: the payload

    [Fact]
    public void Kind13Payload_RoundTripsAtTheFieldLimits_AndRefusesWhatItCannotEncode()
    {
        foreach (CancelTarget t in new[]
        {
            new CancelTarget(0, 0, -1), new CancelTarget(2, 1, 10), new CancelTarget(0, Granary, 126),
            new CancelTarget((1 << OrderLog.CancelSlotBits) - 1, (1 << OrderLog.CancelProjectBits) - 1, (1L << OrderLog.CancelTurnBits) - 2),
        })
        {
            double packed = OrderLog.PackCancelTarget(t);
            Assert.True(packed == Math.Floor(packed) && packed < 9007199254740992.0, "exact in a double");
            Assert.True(OrderLog.TryUnpackCancelTarget(packed, out CancelTarget back));
            Assert.Equal(t, back);
        }
        // The literal the CI generator writes (gen-sample-orders.py cancel_target) is this packing.
        Assert.Equal(2_181_843_648_512.0, OrderLog.PackCancelTarget(new CancelTarget(0, Granary, 126)));
        Assert.Equal(188_978_823_170.0, OrderLog.PackCancelTarget(new CancelTarget(2, Granary, 10)));

        foreach (CancelTarget bad in new[]
        {
            new CancelTarget(-1, 1, 0), new CancelTarget(1 << OrderLog.CancelSlotBits, 1, 0), new CancelTarget(0, -1, 0),
            new CancelTarget(0, 1 << OrderLog.CancelProjectBits, 0), new CancelTarget(0, 1, -2), new CancelTarget(0, 1, (1L << OrderLog.CancelTurnBits) - 1),
        })
            Assert.Throws<ArgumentOutOfRangeException>(() => OrderLog.PackCancelTarget(bad));

        foreach (double amount in new[] { double.NaN, -1.0, 0.5, double.PositiveInfinity, 9007199254740992.0 })
            Assert.False(OrderLog.TryUnpackCancelTarget(amount, out _), amount.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(-1, 0.0)]       // no settlement
    [InlineData(0, 0.5)]        // not whole
    [InlineData(0, -3.0)]       // negative
    [InlineData(0, double.NaN)]
    public void Kind13_IsRangeCheckedAtLoad(int target, double amount)
    {
        byte[] bytes = Bytes(new OrderRecord(4, Player, OrderKind.CancelConstructionEntry, target, amount));
        Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(new MemoryStream(bytes)));
    }

    [Fact]
    public void APopulatedKind13Log_RoundTripsBitExact()
    {
        OrderRecord[] records =
        [
            new(3, Player, OrderKind.EnqueueConstruction, 0, Granary),
            new(4, Player, OrderKind.CancelConstructionEntry, 0, OrderLog.PackCancelTarget(new CancelTarget(1, Granary, 3))),
            new(9, 2, OrderKind.CancelConstructionEntry, 7, OrderLog.PackCancelTarget(new CancelTarget(0, 15, -1))),
        ];
        byte[] bytes = Bytes(records);
        Assert.Equal(16 + 28 * records.Length, bytes.Length);                      // header 16, 28 per record
        OrderLog back = OrderLog.Load(new MemoryStream(bytes));
        Assert.Equal(records, Enumerable.Range(0, back.Count).Select(i => back[i]).ToArray());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), Convert.ToHexString(SHA256.HashData(Bytes(back))));
    }

    // ================================================================== kind 13: resolution

    /// <summary>s0: slot 0 granary (ordered at 3), slot 1 project 2 (at 4), slot 2 granary (at 4); s1: slot 0 granary (at 4);
    /// s0 slot 3: a granary with NO progress row (a migrated v32 entry: enqueue turn −1).</summary>
    private static (Table<ConstructionQueueRow> Queue, Table<ConstructionProgressRow> Progress) Rig()
    {
        var queue = new Table<ConstructionQueueRow>();
        var progress = new Table<ConstructionProgressRow>();
        void Add(SettlementId s, int slot, int project, long turn, bool row = true)
        {
            queue.Add(new ConstructionQueueRow(s, slot, project));
            if (row) progress.Add(new ConstructionProgressRow(s, slot, project, turn, 0));
        }
        Add(S0, 0, Granary, 3);
        Add(S0, 1, 2, 4);
        Add(S0, 2, Granary, 4);
        Add(S1, 0, Granary, 4);
        Add(S0, 3, Granary, -1, row: false);
        return (queue, progress);
    }

    private static OrderRecord Entry(SettlementId s, int slot, int project, long turn) =>
        new(5, Player, OrderKind.CancelConstructionEntry, s.Value, OrderLog.PackCancelTarget(new CancelTarget(slot, project, turn)));

    [Fact]
    public void Kind13_ResolvesByIdentity_PrefersTheRecordedSlot_AndNeverRedirectsToAnotherIdentity()
    {
        (Table<ConstructionQueueRow> q, Table<ConstructionProgressRow> p) = Rig();
        Assert.Equal(1, ConstructionQuery.CancelTargetIndex(q, p, Entry(S0, 1, 2, 4)));          // the entry at its slot
        Assert.Equal(2, ConstructionQuery.CancelTargetIndex(q, p, Entry(S0, 1, Granary, 4)));    // moved: the one granary of turn 4
        Assert.Equal(0, ConstructionQuery.CancelTargetIndex(q, p, Entry(S0, 9, Granary, 3)));    // no slot 9: the one of turn 3
        Assert.Equal(4, ConstructionQuery.CancelTargetIndex(q, p, Entry(S0, 3, Granary, -1)));   // a row-less entry reads −1
        Assert.Equal(3, ConstructionQuery.CancelTargetIndex(q, p, Entry(S1, 0, Granary, 4)));    // its own settlement only
        // THE DEFECT, side by side: slot 0 holds the granary of turn 3. A recorded "project 2 of turn 3" names nothing here
        // and changes nothing; the legacy slot-only order removes the granary.
        Assert.Equal(-1, ConstructionQuery.CancelTargetIndex(q, p, Entry(S0, 0, 2, 3)));
        Assert.Equal(0, ConstructionQuery.CancelTargetIndex(q, p, new OrderRecord(5, Player, OrderKind.CancelConstruction, 0, 0.0)));
        Assert.Equal(-1, ConstructionQuery.CancelTargetIndex(q, p, Entry(S0, 1, 2, 5)));         // another order turn: another entry
        Assert.Equal(-1, ConstructionQuery.CancelTargetIndex(q, p, new OrderRecord(5, Player, OrderKind.CancelConstructionEntry, 0, 0.5)));
        Assert.Equal(-1, ConstructionQuery.CancelTargetIndex(q, p, new OrderRecord(5, Player, OrderKind.EnqueueConstruction, 0, 1.0)));
    }

    [Fact]
    public void Kind13_AnAmbiguousIdentity_IsNoTarget_AndTheReplayIsRefusedAtDelivery()
    {
        var w = new WorldState(1);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Granary));
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 1, Granary));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S0, 0, Granary, 4, 0));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S0, 1, Granary, 4, 0));
        OrderRecord atSlot = Entry(S0, 1, Granary, 4), nowhere = Entry(S0, 7, Granary, 4);
        Assert.Equal(1, ConstructionQuery.CancelTargetIndex(w.ConstructionQueue, w.ConstructionProgress, atSlot));
        Assert.Equal(ConstructionQuery.CancelTargetAmbiguous,
            ConstructionQuery.CancelTargetIndex(w.ConstructionQueue, w.ConstructionProgress, nowhere));
        Assert.False(ConstructionQuery.CancelOrderValid(w, nowhere));

        var log = new OrderLog();
        log.Append(atSlot);
        OrderValidation.ValidateAtDelivery(log.BatchFor(5), w, w);                                // one entry: fine
        var ambiguous = new OrderLog();
        ambiguous.Append(nowhere);
        var e = Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAtDelivery(ambiguous.BatchFor(5), w, w));
        Assert.Contains("slot 7, project 1, enqueued on turn 4", e.Message);
        Assert.Contains("more than one entry with that identity", e.Message);
    }

    [Fact]
    public void TheCancelFactory_IsTotal_AnUnqueuedOrUnencodableSlotGivesAnOrderThatNamesNothing()
    {
        var w = new WorldState(1);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Granary));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S0, 0, Granary, 2, 0));
        foreach (int slot in new[] { 1, -1, int.MaxValue })
        {
            OrderRecord o = ConstructionQuery.CancelOrder(w, new PolityId(Player), S0, slot);
            Assert.Equal((OrderKind.CancelConstructionEntry, -1.0), (o.Kind, o.Amount));
            Assert.Equal(-1, ConstructionQuery.CancelTargetIndex(w.ConstructionQueue, w.ConstructionProgress, o));
            Assert.False(ConstructionQuery.CancelOrderValid(w, o));
        }
        OrderRecord head = ConstructionQuery.CancelOrder(w, new PolityId(Player), S0, 0);
        Assert.Equal(OrderLog.PackCancelTarget(new CancelTarget(0, Granary, 2)), head.Amount);
        Assert.True(ConstructionQuery.CancelOrderValid(w, head));
    }

    [Fact]
    public void TheGoodsLoader_RefusesAProjectIdTheCancelCannotCarry_AndAcceptsTheLargestItCan()
    {
        static string Json(int id) =>
            "{\"goods\":[{\"id\":1,\"name\":\"grain\",\"category\":\"food\",\"bulkPerUnit\":1.0,\"numeraire\":true},"
            + "{\"id\":2,\"name\":\"stone\",\"category\":\"raw\",\"bulkPerUnit\":1.0}],\"recipes\":[],"
            + "\"projects\":[{\"id\":" + id.ToString(CultureInfo.InvariantCulture)
            + ",\"name\":\"cairn\",\"inputs\":[{\"good\":\"stone\",\"qty\":1}],\"laborRequired\":1.0}]}";
        int largest = (1 << OrderLog.CancelProjectBits) - 1;
        Assert.NotNull(Sim.Core.Systems.GoodsConfigLoader.Load(Json(largest)).ProjectById(largest));
        var e = Assert.Throws<Sim.Core.Systems.GoodsConfigException>(() => Sim.Core.Systems.GoodsConfigLoader.Load(Json(largest + 1)));
        Assert.Contains("id must be below 65536, got 65536", e.Message);
    }

    // ================================================================== the recorded game, in process

    /// <summary>The recorded game: two granaries at s0 at turn 0 (slots 0 and 1) with s0's builders taken away in the same
    /// batch (nothing is built, both wait at p = 0), and at turn 3 a cancel of s0's slot 1 — <paramref name="legacy"/> the slot-only kind 11 an older build
    /// wrote, else the kind 13 this build's factory writes (ConstructionQuery.CancelOrder, read from the world it is issued
    /// in). <paramref name="noBuildersAt"/> moves the builders order (another log: another world).</summary>
    private static (OrderLog Log, WorldState[] Worlds) Record(bool legacy, int turns = 6, long noBuildersAt = 0, bool preDiscovery = false)
    {
        var log = new OrderLog();
        WorldState w = Sim.Cli.HeadlessFounding.Found(42, 256, 4);
        // M6 Track EX (2026-10-22): <paramref name="preDiscovery"/> — a session labelled with a pre-v35 schema is replayed
        // without the discovery layer (DiscoveryQuery.RecordedWithoutDiscovery), so it is recorded without it, as such a build
        // played it.
        Sim.Core.Kernel.SystemRegistration[] pipeline = Sim.Cli.CliRecipes.ProductionPipeline();
        if (preDiscovery)
            using (var pipe = Sim.Data.DataFiles.OpenPipeline())
                pipeline = Sim.Core.Kernel.PipelineLoader.Load(pipe, Sim.Core.SystemCatalog.All(TestConfigs.PreDiscovery(TestConfigs.Sim()), Sim.Cli.CliRecipes.Worldgen()));
        var executor = new TurnExecutor(Sim.Cli.CliRecipes.Era(), pipeline, log);
        var worlds = new List<WorldState> { w };
        for (int t = 0; t < turns; t++)
        {
            if (t == 0)
            {
                log.Append(ConstructionQuery.EnqueueOrder(w, new PolityId(Player), S0, Granary));
                log.Append(ConstructionQuery.EnqueueOrder(w, new PolityId(Player), S0, Granary));
            }
            if (t == noBuildersAt) log.Append(new OrderRecord(t, Player, OrderKind.SectorAllocation, (0 << 3) | Sectors.Construction, 0.0));
            if (t == 3)
                log.Append(legacy
                    ? new OrderRecord(3, Player, OrderKind.CancelConstruction, S0.Value, 1.0)
                    : ConstructionQuery.CancelOrder(w, new PolityId(Player), S0, 1));
            w = executor.Step(w);
            worlds.Add(w);
        }
        return (log, [.. worlds]);
    }

    [Fact]
    public void TheEmittersIssueKind13_WhichInTheRecordedWorldChangesExactlyWhatKind11Changed()
    {
        (OrderLog modern, WorldState[] m) = Record(legacy: false);
        (OrderLog _, WorldState[] l) = Record(legacy: true);
        OrderRecord cancel = Enumerable.Range(0, modern.Count).Select(i => modern[i]).Single(o => o.Turn == 3);
        Assert.Equal(OrderKind.CancelConstructionEntry, cancel.Kind);
        Assert.True(OrderLog.TryUnpackCancelTarget(cancel.Amount, out CancelTarget t));
        Assert.Equal(new CancelTarget(1, Granary, 0), t);                                       // slot 1, the granary of turn 0
        Assert.Equal(2, ConstructionQuery.Queue(m[3], S0).Length);
        Assert.Equal(new[] { 0 }, ConstructionQuery.Queue(m[4], S0).Select(r => r.Slot).ToArray());   // slot 1 gone, turn-exact
        for (int i = 0; i < m.Length; i++) Assert.Equal(WorldHash.ComputeHex(l[i]), WorldHash.ComputeHex(m[i]));
    }

    // ================================================================== ReplayGuard

    [Fact]
    public void MatchesRecorded_AcceptsEveryReadableFormOfTheSameTables_AndNothingElse()
    {
        var w = new WorldState(7);
        w.ConstructionQueue.Add(new ConstructionQueueRow(S0, 0, Granary));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S0, 0, Granary, 2, 400_000));
        w.ResourceAllocations.Add(new ResourceAllocationRow(new PolityId(Player), 30));
        w.ConstructionHolds.Add(new ConstructionHoldRow(1, 5, 6));
        string v34 = WorldHash.ComputeHex(w), v33 = WorldHash.ComputeHexAsSchema(w, 33), v32 = WorldHash.ComputeHexAsSchema(w, 32);
        Assert.Equal(v34, WorldHash.ComputeHexAsSchema(w, CanonicalSchema.Version));
        // The production prefix hash equals the test-side strip-and-reserialize projection (an independent construction).
        Assert.Equal(SchemaProjection.HashAtSchemaV33(w, out int holdsStripped), v33);
        Assert.Equal(SchemaProjection.HashAtSchemaV32(w, out int allStripped), v32);
        Assert.Equal((1, 3), (holdsStripped, allStripped));
        Assert.Equal(3, new[] { v34, v33, v32 }.Distinct().Count());
        foreach (string recorded in new[] { v34, v33, v32 }) Assert.True(ReplayGuard.MatchesRecorded(w, recorded));

        WorldState other = w.Clone();
        other.ConstructionQueue.Add(new ConstructionQueueRow(S0, 1, Granary));                    // a table every form serializes
        foreach (string recorded in new[] { v34, v33, v32 }) Assert.False(ReplayGuard.MatchesRecorded(other, recorded));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldHash.ComputeHexAsSchema(w, 31));
    }

    private static SessionTrace.Row TraceRow(WorldState w, string hash) => new(w.Clock.Turn, 0, 0, 0, 0, hash);

    [Fact]
    public void CheckBeforeDelivery_RefusesAKind11BatchUnlessTheTraceProvesTheWorld_AndIgnoresEveryOtherBatch()
    {
        (OrderLog legacy, WorldState[] w) = Record(legacy: true);
        (OrderLog modern, _) = Record(legacy: false);
        OrderBatch eleven = legacy.BatchFor(3), thirteen = modern.BatchFor(3), none = legacy.BatchFor(2);
        Assert.Equal((1, 0, 0), (ReplayGuard.LegacyCancels(eleven), ReplayGuard.LegacyCancels(thirteen), ReplayGuard.LegacyCancels(none)));
        SessionTrace.Row[] good = [TraceRow(w[3], WorldHash.ComputeHexAsSchema(w[3], 33))];
        SessionTrace.Row[] wrong = [TraceRow(w[3], WorldHash.ComputeHex(w[2]))];
        SessionTrace.Row[] other = [TraceRow(w[2], WorldHash.ComputeHex(w[2]))];

        ReplayGuard.CheckBeforeDelivery(eleven, w[3], good, "s", null);                          // proven: accepted
        ReplayGuard.CheckBeforeDelivery(thirteen, w[3], null, "s", null);                        // identity: no proof needed
        ReplayGuard.CheckBeforeDelivery(none, w[2], null, "s", null);
        string noTrace = Assert.Throws<OrderValidationException>(() => ReplayGuard.CheckBeforeDelivery(eleven, w[3], null, "s", "build x")).Message;
        Assert.Contains("has no trace", noTrace);
        Assert.Contains("open it with the build that recorded it (build x)", noTrace);
        Assert.Contains("Nothing was applied and no session file was changed", noTrace);
        Assert.Contains("has no row for turn 3", Assert.Throws<OrderValidationException>(() => ReplayGuard.CheckBeforeDelivery(eleven, w[3], other, "s", null)).Message);
        string diverged = Assert.Throws<OrderValidationException>(() => ReplayGuard.CheckBeforeDelivery(eleven, w[3], wrong, "s", null)).Message;
        Assert.Contains("does not reproduce that world at turn 3", diverged);
        Assert.Contains("could remove a different project", diverged);
    }

    // ================================================================== the CLI, each path in its own process

    private static (int Exit, string Out, string Err) RunSim(params string[] args) => CliProcess.Run(Limit, args);

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "d056-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static byte[] Bytes(params OrderRecord[] records)
    {
        var log = new OrderLog();
        foreach (OrderRecord r in records) log.Append(r);
        return Bytes(log);
    }

    private static byte[] Bytes(OrderLog log)
    {
        using var ms = new MemoryStream();
        log.Save(ms);
        return ms.ToArray();
    }

    private static string Write(string dir, string name, OrderLog log)
    {
        string path = Path.Combine(dir, name);
        File.WriteAllBytes(path, Bytes(log));
        return path;
    }

    private static void AssertRefusedBeforeWriting((int Exit, string Out, string Err) r, string dir, string[] inputs, params string[] outputs)
    {
        Assert.True(r.Exit == 2, $"expected exit 2, got {r.Exit}: {r.Err}");
        Assert.Contains("legacy slot-only cancel order (kind 11, CancelConstruction; the first on turn 3)", r.Err);
        Assert.Contains("nothing was replayed and no file was written", r.Err);
        foreach (string o in outputs) Assert.False(File.Exists(o) || Directory.Exists(o), $"{o} was written");
        Assert.Equal(inputs.Order(StringComparer.Ordinal).ToArray(),
            Directory.GetFileSystemEntries(dir).Order(StringComparer.Ordinal).ToArray());     // nothing else appeared
    }

    [Fact]
    public void EveryBareLogRunner_RefusesAKind11Log_BeforeWritingAnything_AndLeavesItsInputsUntouched()
    {
        string dir = TempDir();
        try
        {
            (OrderLog legacy, _) = Record(legacy: true);
            (OrderLog modern, _) = Record(legacy: false);
            string old = Write(dir, "legacy.bin", legacy), now = Write(dir, "modern.bin", modern);
            byte[] oldBytes = File.ReadAllBytes(old);

            // A kind-13 log runs (the control: the refusal is about kind 11, not the scenario).
            string save2 = Path.Combine(dir, "s2.civsnap");
            (int exit, _, string err) = RunSim(["run", .. Founded, "--turns", "2", "--orders", now, "--save-at", "2", "--save", save2]);
            Assert.True(exit == 0, err);
            byte[] saveBytes = File.ReadAllBytes(save2);
            string[] inputs = [old, now, save2];

            string save = Path.Combine(dir, "out.civsnap"), hashes = Path.Combine(dir, "h.log"), session = Path.Combine(dir, "session");
            string report = Path.Combine(dir, "r.jsonl"), trace = Path.Combine(dir, "t.csv");
            AssertRefusedBeforeWriting(RunSim(["run", .. Founded, "--turns", "5", "--orders", old, "--save-at", "5", "--save", save, "--hash-log", hashes]),
                dir, inputs, save, hashes);
            AssertRefusedBeforeWriting(RunSim(["run", .. Founded, "--turns", "5", "--orders", old, "--emit-session", session]), dir, inputs, session);
            AssertRefusedBeforeWriting(RunSim(["replay", .. Founded, "--turns", "5", "--orders", old, "--hash-log", hashes, "--report-jsonl", report]),
                dir, inputs, hashes, report);
            AssertRefusedBeforeWriting(RunSim(["replay", .. Founded, "--turns", "5", "--orders", old, "--emit-session", session]), dir, inputs, session);
            AssertRefusedBeforeWriting(RunSim(["replay", "--seed", "42", "--turns", "5", "--orders", old]), dir, inputs);
            AssertRefusedBeforeWriting(RunSim(["run", "--from-save", save2, "--size", "256", "--turns", "3", "--orders", old, "--save-at", "5", "--save", save, "--hash-log", hashes]),
                dir, inputs, save, hashes);
            AssertRefusedBeforeWriting(RunSim(["research", "--seed", "42", "--size", "256", "--settlements", "4", "--turns", "5", "--orders", old,
                "--trace-population", trace]), dir, inputs, trace);

            Assert.Equal(oldBytes, File.ReadAllBytes(old));
            Assert.Equal(saveBytes, File.ReadAllBytes(save2));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FromSave_RefusesToOverwriteTheSaveItContinues()
    {
        string dir = TempDir();
        try
        {
            (OrderLog modern, _) = Record(legacy: false);
            string now = Write(dir, "modern.bin", modern), save2 = Path.Combine(dir, "s2.civsnap");
            Assert.Equal(0, RunSim(["run", .. Founded, "--turns", "2", "--orders", now, "--save-at", "2", "--save", save2]).Exit);
            byte[] before = File.ReadAllBytes(save2);
            (int exit, _, string err) = RunSim(["run", "--from-save", save2, "--turns", "3", "--save-at", "5", "--save", save2]);
            Assert.NotEqual(0, exit);
            Assert.Contains("is the save being continued (--from-save): it would be overwritten", err);
            Assert.Equal(before, File.ReadAllBytes(save2));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A session record as an older build left it: <paramref name="log"/> as its orders, a trace of
    /// <paramref name="worlds"/> hashed by <paramref name="hash"/> (the recording schema's form), and a manifest labelled
    /// <paramref name="schema"/>.</summary>
    private static string Session(string dir, OrderLog log, WorldState[] worlds, int schema, Func<WorldState, string> hash)
    {
        const string stamp = "20261018-120000";
        File.WriteAllBytes(Path.Combine(dir, "orders-" + stamp + ".bin"), Bytes(log));
        var lines = new List<string> { SessionTrace.Header };
        foreach (WorldState w in worlds)
        {
            string[] cols = SessionTrace.Line(w, 0).Split(',');
            cols[^1] = hash(w);
            lines.Add(string.Join(',', cols));
        }
        File.WriteAllText(Path.Combine(dir, "trace-" + stamp + ".csv"), string.Join("\n", lines) + "\n");
        var manifest = new SessionManifest(42, 256, 4, schema, "older-build", "2026-10-15", "2026-10-18 12:00:00",
            "orders-" + stamp + ".bin", "chronicle-" + stamp + ".txt", "trace-" + stamp + ".csv");
        string path = Path.Combine(dir, "session-" + stamp + ".json");
        using (FileStream f = File.Create(path)) manifest.Write(f);
        return path;
    }

    [Fact]
    public void Inspect_ReplaysACompatibleHistoricalSession_InV34OrV33Form()
    {
        foreach ((int schema, Func<WorldState, string> hash) in new (int, Func<WorldState, string>)[]
        {
            (CanonicalSchema.Version, WorldHash.ComputeHex), (33, w => WorldHash.ComputeHexAsSchema(w, 33)),
        })
        {
            // M6 EX: the v33-labelled arm is recorded without the discovery layer (it replays without it).
            (OrderLog legacy, WorldState[] worlds) = Record(legacy: true, preDiscovery: schema < 35);
            string dir = TempDir();
            try
            {
                string manifest = Session(dir, legacy, worlds, schema, hash);
                string report = Path.Combine(dir, "r.jsonl");
                (int exit, string stdout, string err) = RunSim("inspect", "--manifest", manifest, "--report-jsonl", report);
                Assert.True(exit == 0, $"schema {schema}: exit {exit}: {err}");
                if (schema == CanonicalSchema.Version) Assert.Contains("reproduction VERIFIED: 7 turns", stdout);
                Assert.True(new FileInfo(report).Length > 0);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Inspect_RefusesADivergedHistoricalSession_AtItsKind11Turn_WithNothingWritten()
    {
        // The recorded world kept its builders until turn 2; the saved log takes them at turn 0: from turn 1 the replay is
        // another world (what another build's rules, or another platform, makes of the same log).
        (_, WorldState[] recorded) = Record(legacy: true, noBuildersAt: 2);
        (OrderLog saved, WorldState[] replayed) = Record(legacy: true);
        Assert.NotEqual(WorldHash.ComputeHex(recorded[3]), WorldHash.ComputeHex(replayed[3]));
        foreach (int schema in new[] { CanonicalSchema.Version, 33 })
        {
            string dir = TempDir();
            try
            {
                string manifest = Session(dir, saved, recorded, schema, w => WorldHash.ComputeHexAsSchema(w, schema));
                string[] before = Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray();
                byte[][] bytes = before.Select(File.ReadAllBytes).ToArray();
                string report = Path.Combine(dir, "r.jsonl"), telemetry = Path.Combine(dir, "t.jsonl");
                (int exit, _, string err) = RunSim("inspect", "--manifest", manifest, "--report-jsonl", report, "--telemetry", telemetry);
                Assert.True(exit == 2, $"schema {schema}: expected exit 2, got {exit}: {err}");
                Assert.Contains("the orders delivered on turn 3 include 1 legacy slot-only cancel order", err);
                Assert.Contains("does not reproduce that world at turn 3", err);
                Assert.Contains("open it with the build that recorded it (build older-build, 2026-10-15, schema v", err);
                Assert.Equal(before, Directory.GetFiles(dir).Order(StringComparer.Ordinal).ToArray());   // no report, no telemetry
                for (int i = 0; i < before.Length; i++) Assert.Equal(bytes[i], File.ReadAllBytes(before[i]));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
