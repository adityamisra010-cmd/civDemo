using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.Observability;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>The class's shared runs (the slow part, made once): the small AI world's uninterrupted runs and saves.</summary>
public sealed class FromSaveRuns : IDisposable
{
    internal static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);

    /// <summary>The small founded world every test continues: seed 42 at 256 px, 4 settlements, one AI Empire (the player
    /// rules settlements 0 and 2). Fast (well under a second for 16 steps), and its AI acts inside the continued window: it
    /// enqueues granaries stamped 7 and 14 (measured; asserted below from the emitted run log).</summary>
    internal static readonly string[] World = ["--founded", "--seed", "42", "--size", "256", "--settlements", "4", "--ai-empires", "1"];

    internal const long K = 6;

    internal string Dir { get; } = Path.Combine(Path.GetTempPath(), "civsim-fromsave-" + Guid.NewGuid().ToString("N"));

    /// <summary>The player's log L: at turn K the split b = 30 and granaries at settlements 0 and 2 (so the first resumed
    /// step delivers orders, and the report has heads that build).</summary>
    internal string Orders => Path.Combine(Dir, "L.bin");

    internal string[] Full { get; }

    /// <summary>The uninterrupted run's saves at K and at K + 4, and its run log.</summary>
    internal string SaveK => Path.Combine(Dir, "s6.civsnap");
    internal string SaveK4 => Path.Combine(Dir, "s10.civsnap");
    internal OrderLog RunLog { get; }

    public FromSaveRuns()
    {
        Directory.CreateDirectory(Dir);
        WriteLog(Orders, new OrderRecord(K, 1, OrderKind.SetResourceAllocation, 1, 30.0),
            new OrderRecord(K, 1, OrderKind.EnqueueConstruction, 0, 1.0), new OrderRecord(K, 1, OrderKind.EnqueueConstruction, 2, 1.0));
        Run([.. World, "--turns", "16", "--orders", Orders, "--hash-log", P("full.log"), "--save-at", "6", "--save", SaveK,
            "--emit-session", P("session")]);
        Run([.. World, "--turns", "16", "--orders", Orders, "--hash-log", P("full2.log"), "--save-at", "10", "--save", SaveK4]);
        Full = File.ReadAllLines(P("full.log"));
        Assert.Equal(Full, File.ReadAllLines(P("full2.log")));
        string log = Directory.GetFiles(P("session"), "orders-*.bin").Single();
        using FileStream f = File.OpenRead(log);
        RunLog = OrderLog.Load(f);
    }

    internal string P(string name) => Path.Combine(Dir, name);

    internal static (int Exit, string Out, string Err) Run(params string[] args)
    {
        // M6 Track EX (2026-10-22): `--without-discovery` — these rigs' premises (the AI enqueues granaries at 7 and 14, the
        // colony and refusal turns) were measured before the discovery layer; under the shipped concealment the dev AI holds
        // no stone for a granary until turn 68 (MEASURED: its run log's only orders before 68 are its turn-0 research
        // target), so the CLI runs the discovery twin (R4) and the in-process loops below use TestConfigs.PreDiscovery.
        (int exit, string stdout, string stderr) = CliProcess.Run(Limit, ["run", "--without-discovery", .. args]);
        return (exit, stdout, stderr);
    }

    internal static void WriteLog(string path, params OrderRecord[] records)
    {
        var log = new OrderLog();
        foreach (OrderRecord r in records) log.Append(r);
        using FileStream f = File.Create(path);
        log.Save(f);
    }

    public void Dispose()
    {
        if (Directory.Exists(Dir)) Directory.Delete(Dir, recursive: true);
    }
}

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — `sim run --from-save SAVE`, AS A SCRIPT RUNS IT (CliProcess: the real executable, its
/// own process). A save written by one process is continued by another and gives the uninterrupted run's hashes and saves;
/// orders stamped before the save's turn are dropped unread and an order stamped at it is delivered in the first resumed
/// step (turn-exact pins on the continued run); turn zero is the regenerated founding world (an order naming a colony
/// founded before the save is deferred exactly as in the uninterrupted run); the batch-2 (v32) and batch-3 (v33) fixtures
/// continue migrated, with their stderr note, and equal their in-process continuations; every refusal of the design (§3)
/// exits non-zero with its reason; and `--split-report` changes no hash and no save, in a run from founding and in a
/// continued one.
/// </summary>
public class RunFromSaveCliTests(FromSaveRuns runs) : IClassFixture<FromSaveRuns>
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>M6 EX: the CLI's production pipeline under `--without-discovery` (TestConfigs.PreDiscovery).</summary>
    private static Sim.Core.Kernel.SystemRegistration[] PreDiscoveryPipeline()
    {
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return Sim.Core.Kernel.PipelineLoader.Load(pipe, Sim.Core.SystemCatalog.All(TestConfigs.PreDiscovery(TestConfigs.Sim()), CliRecipes.Worldgen()));
    }

    // ================================================================== the continuation is the uninterrupted run

    [Fact]
    public void AContinuedRun_IsTheUninterruptedRunsTail_ItsSaveIsByteIdentical_AndTheAiKeepsPlaying()
    {
        // Premise: the AI acts inside the continued window (turns 7..16) — its granary orders are stamped 7 and 14.
        long[] aiTurns = Enumerable.Range(0, runs.RunLog.Count).Select(i => runs.RunLog[i])
            .Where(r => r.ActorId == 2 && r.Turn > FromSaveRuns.K).Select(r => r.Turn).ToArray();
        Assert.Equal([7L, 14L], aiTurns);

        (int exit, string stdout, string stderr) = FromSaveRuns.Run("--from-save", runs.SaveK, "--size", "256", "--turns", "10",
            "--orders", runs.Orders, "--hash-log", runs.P("tail.log"), "--save-at", "10", "--save", runs.P("s10c.civsnap"));
        Assert.True(exit == 0, stderr);
        Assert.Equal("", stderr);                                              // a v34 save: no migration note
        Assert.Equal($"run complete: seed 42, 10 turns from turn 6, hash {runs.Full[15]}", CliProcess.Lines(stdout).Single());
        Assert.Equal(runs.Full[6..16], File.ReadAllLines(runs.P("tail.log")));
        Assert.Equal(File.ReadAllBytes(runs.SaveK4), File.ReadAllBytes(runs.P("s10c.civsnap")));
    }

    [Fact]
    public void ATwiceContinuedRun_IsStillTheUninterruptedRun()
    {
        // The save at 10 that a CONTINUED process wrote, continued again by a third process.
        (int exit, _, string stderr) = FromSaveRuns.Run("--from-save", runs.SaveK4, "--size", "256", "--turns", "6",
            "--orders", runs.Orders, "--hash-log", runs.P("tail10.log"));
        Assert.True(exit == 0, stderr);
        Assert.Equal(runs.Full[10..16], File.ReadAllLines(runs.P("tail10.log")));
    }

    // ================================================================== order delivery, turn-exact

    [Fact]
    public void OrdersStampedBeforeTheSave_AreDroppedUnread_AndAnOrderStampedAtItsTurn_IsDeliveredInTheFirstResumedStep()
    {
        string[] orderless = Continue(runs.P("orderless.log"), null);

        // Stamped K: delivered in the step K -> K + 1 — the very first hash moves.
        string atK = runs.P("atK.bin");
        FromSaveRuns.WriteLog(atK, new OrderRecord(FromSaveRuns.K, 1, OrderKind.SetResourceAllocation, 1, 30.0));
        string[] delivered = Continue(runs.P("atK.log"), atK);
        Assert.NotEqual(orderless[0], delivered[0]);

        // Stamped K − 1 (after the save's turn was reached, the world already holds whatever it did): never delivered again —
        // and a record stamped 0 that the uninterrupted run would REFUSE (a granary where the player does not rule) is never
        // even validated. The continuation is the orderless one, hash for hash.
        string before = runs.P("before.bin");
        FromSaveRuns.WriteLog(before, new OrderRecord(0, 1, OrderKind.EnqueueConstruction, 1, 1.0),
            new OrderRecord(FromSaveRuns.K - 1, 1, OrderKind.SetResourceAllocation, 1, 30.0));
        Assert.Equal(orderless, Continue(runs.P("before.log"), before));

        // Premise: that turn-0 record IS refused by a run that reads it.
        (int exit, _, string stderr) = FromSaveRuns.Run([.. FromSaveRuns.World, "--turns", "1", "--orders", before]);
        Assert.Equal(2, exit);
        Assert.Contains("EnqueueConstruction targets settlement 1, which polity 1 does not control", stderr);
    }

    private string[] Continue(string hashLog, string? orders)
    {
        string[] args = ["--from-save", runs.SaveK, "--size", "256", "--turns", "4", "--hash-log", hashLog];
        if (orders is not null) args = [.. args, "--orders", orders];
        (int exit, _, string stderr) = FromSaveRuns.Run(args);
        Assert.True(exit == 0, stderr);
        return File.ReadAllLines(hashLog);
    }

    // ================================================================== turn zero is the founding world

    [Fact]
    public void TurnZeroIsTheRegeneratedFoundingWorld_AnOrderNamingAColonyFoundedBeforeTheSave_IsDeferredAsInTheUninterruptedRun()
    {
        // The AI founds settlement 4 from settlement 3 (its order stamped 71; the colony exists from turn 72). The player's
        // order at turn 80 names it: above the FOUNDING world's highest id (3), so ADR-034 defers it to delivery, where it
        // exists — accepted, and it changes nothing (the player does not rule there). Validated against the world AT the save
        // (highest id ≥ 4), the same order would be checked up front and refused for control.
        string log = runs.P("colony.bin");
        FromSaveRuns.WriteLog(log, new OrderRecord(80, 1, OrderKind.EnqueueConstruction, 4, 1.0));
        string s80 = runs.P("s80.civsnap");
        (int exit, _, string stderr) = FromSaveRuns.Run([.. FromSaveRuns.World, "--turns", "84", "--orders", log,
            "--hash-log", runs.P("u84.log"), "--save-at", "80", "--save", s80]);
        Assert.True(exit == 0, stderr);

        WorldState at80 = SaveFiles.Load(s80, 256);
        SettlementRow colony = at80.Settlements.Rows().Single(s => s.Id.Value == 4);
        Assert.Equal(72L, colony.FoundedTurn);
        Assert.True(EmpireQuery.TryGetController(at80, colony.Id, out PolityId ruler) && ruler.Value == 2, "the colony is the AI's");

        (exit, _, stderr) = FromSaveRuns.Run("--from-save", s80, "--size", "256", "--turns", "4", "--orders", log,
            "--hash-log", runs.P("c84.log"));
        Assert.True(exit == 0, stderr);
        Assert.Equal(File.ReadAllLines(runs.P("u84.log"))[80..84], File.ReadAllLines(runs.P("c84.log")));
    }

    // ================================================================== migrated fixtures

    [Theory]
    [InlineData("v32-seed42-turn40.civsnap", -1, 32)]
    [InlineData("v33-seed42-turn2.civsnap", 256, 33)]
    public void AnOlderSchemaFixture_ContinuesMigrated_WithItsNote_AndEqualsItsInProcessContinuation(string name, int size, int version)
    {
        string path = Fixture(name);
        string[] args = ["--from-save", path, "--turns", "3", "--hash-log", runs.P(name + ".log")];
        if (size > 0) args = [.. args, "--size", size.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        (int exit, string stdout, string stderr) = FromSaveRuns.Run(args);
        Assert.True(exit == 0, stderr);
        Assert.Equal(SaveFiles.MigrationNote(path, version), CliProcess.Lines(stderr).Single());
        string[] cli = File.ReadAllLines(runs.P(name + ".log"));

        // In process: the same loader, then the CLI's own loop — the AI's orders appended to the run log, then the step.
        WorldState w = SaveFiles.Load(path, size);
        long k = w.Clock.Turn;
        byte[] bytes = File.ReadAllBytes(path);
        int? trailer = TopologyFlags.RecordedFlat(version) ? null : BitConverter.ToInt32(bytes, bytes.Length - 4);
        Sim.Core.Worldgen.WorldgenConfig aiWorldgen = SaveFiles.WorldgenFor(version, CliRecipes.Worldgen(), size, trailer);
        SimConfig cfg = TestConfigs.PreDiscovery(TestConfigs.Sim());   // M6 EX: the CLI's --without-discovery
        var log = new OrderLog();
        var exec = new TurnExecutor(CliRecipes.Era(), PreDiscoveryPipeline(), log);
        var inProcess = new List<string>();
        for (int t = 0; t < 3; t++)
        {
            AiOrders.Append(log, w, cfg, aiWorldgen);
            w = exec.Step(w);
            inProcess.Add(WorldHash.ComputeHex(w));
        }
        Assert.Equal(inProcess, cli);
        Assert.Equal(k + 3, w.Clock.Turn);
        Assert.Equal($"run complete: seed 42, 3 turns from turn {k}, hash {inProcess[2]}", CliProcess.Lines(stdout).Single());
    }

    // ================================================================== refusals

    [Theory]
    [InlineData("seed", 1, "--seed 43 contradicts the save, whose seed is 42 (--from-save takes the seed from the save)")]
    // Round 3 (finding T3-L2-T3V2-L2-F3): --ai-empires is the FOUNDING count — this world was founded with one AI Empire.
    [InlineData("ai-empires", 1, "--ai-empires 0 contradicts the save, which was founded with 1 AI Empire")]
    [InlineData("save-at at the save's turn", 1, "--save-at must be in 7..9 (the save's turn 6 plus 1..3), got 6")]
    [InlineData("save-at past the run", 1, "--save-at must be in 7..9 (the save's turn 6 plus 1..3), got 10")]
    [InlineData("emit-session", 1, "--from-save writes no session record (a session record starts at founding): --emit-session is refused")]
    [InlineData("settlements", 1, "--from-save takes the settlements from the save: --settlements is refused")]
    [InlineData("save-at without save", 1, "--save-at requires --save PATH")]
    [InlineData("save without save-at", 1, "--save requires --save-at K")]
    [InlineData("an AI's order", 2, "--from-save takes a PLAYER input log: record 0 (turn 7, EnqueueConstruction) is issued by polity 2, not the player (polity 1).")]
    [InlineData("no size", 2, "terrain mismatch: ")]
    // Fixer round (finding T3-L2-T3-V2-F1): the boundary — another polity's record stamped AT the save's turn K is kept (it
    // would be delivered in the first resumed step) and so refused; see AnAiOrderStampedAtTheSavesTurn_… for why.
    [InlineData("an AI's order at the save's turn", 2, "--from-save takes a PLAYER input log: record 0 (turn 6, EnqueueConstruction) is issued by polity 2, not the player (polity 1).")]
    public void AFoundedSave_RefusesWhatContradictsIt(string what, int expectedExit, string reason)
    {
        string[] common = ["--from-save", runs.SaveK, "--turns", "3"];
        string[] sized = [.. common, "--size", "256"];
        string[] args = what switch
        {
            "seed" => [.. sized, "--seed", "43"],
            "ai-empires" => [.. sized, "--ai-empires", "0"],
            "save-at at the save's turn" => [.. sized, "--save-at", "6", "--save", runs.P("x.civsnap")],
            "save-at past the run" => [.. sized, "--save-at", "10", "--save", runs.P("x.civsnap")],
            "emit-session" => [.. sized, "--emit-session", runs.P("x-session")],
            "save-at without save" => [.. sized, "--save-at", "7"],
            "save without save-at" => [.. sized, "--save", runs.P("x.civsnap")],
            "settlements" => [.. sized, "--settlements", "4"],
            "an AI's order" => [.. sized, "--orders", AiOrderLog()],
            "an AI's order at the save's turn" => [.. sized, "--orders", AiOrderAtK()],
            "no size" => common,
            _ => throw new ArgumentOutOfRangeException(nameof(what)),
        };
        (int exit, string stdout, string stderr) = FromSaveRuns.Run(args);
        Assert.Equal((expectedExit, ""), (exit, stdout));
        Assert.StartsWith("error: " + reason, CliProcess.Lines(stderr)[0]);
        if (what == "no size")
            Assert.EndsWith("a save made at another size needs --size PX (the size it was founded at).", CliProcess.Lines(stderr)[0]);
        Assert.False(File.Exists(runs.P("x.civsnap")));
    }

    /// <summary>
    /// Round 3 (finding T3-L2-T3V2-L2-F1) — UP-FRONT VALIDATION ON THE CONTINUED RUN: a player record stamped at or after the
    /// save's turn K that the founding world refuses (a granary at settlement 1, the AI's) is refused by `--from-save` before
    /// any step — exit 2, nothing on stdout, no hash log — with exactly the message the uninterrupted run prints for the same
    /// log. Stamped 7 and stamped K = 6 (the boundary: a record at K is kept and read). Without the up-front check the
    /// continuation silently dropped the order (its hash log the orderless one).
    /// </summary>
    [Theory]
    [InlineData(7L)]
    [InlineData(FromSaveRuns.K)]
    public void APlayerRecordTheFoundingWorldRefuses_StampedAtOrAfterTheSavesTurn_IsRefusedUpFront_AsByTheUninterruptedRun(long turn)
    {
        string bad = runs.P($"upfront{turn}.bin");
        string hashLog = runs.P($"upfront{turn}.log");
        FromSaveRuns.WriteLog(bad, new OrderRecord(turn, 1, OrderKind.EnqueueConstruction, 1, 1.0));
        (int exit, string stdout, string stderr) = FromSaveRuns.Run("--from-save", runs.SaveK, "--size", "256", "--turns", "3",
            "--orders", bad, "--hash-log", hashLog);
        Assert.Equal((2, ""), (exit, stdout));
        Assert.StartsWith($"error: order[0] (turn {turn}): EnqueueConstruction targets settlement 1, which polity 1 does not control",
            CliProcess.Lines(stderr)[0]);
        Assert.False(File.Exists(hashLog), "refused up front: no step ran");

        (int uExit, string uStdout, string uStderr) = FromSaveRuns.Run([.. FromSaveRuns.World, "--turns", "10", "--orders", bad]);
        Assert.Equal((2, ""), (uExit, uStdout));
        Assert.Equal(CliProcess.Lines(uStderr)[0], CliProcess.Lines(stderr)[0]);
    }

    /// <summary>
    /// Round 3 (finding T3-L2-T3V2-L2-F2) — THE K − 1 BOUNDARY OF AN EMITTED RUN LOG, turn-exact on the real log: the class's
    /// emitted run log (the player's records at 6, the AI's up to 14) continued from the uninterrupted run's save at 15 — its
    /// last AI record stamped K − 1 — is ACCEPTED (those records are dropped unread) and gives the uninterrupted world 16;
    /// the same log continued from the save at 14 — the AI record stamped K — is REFUSED, naming that record.
    /// </summary>
    [Fact]
    public void AnEmittedRunLog_WhoseAiRecordsAllPrecedeTheSavesTurn_IsAccepted_ToTheUninterruptedTail_ButNotOneStampedAtIt()
    {
        string emitted = Directory.GetFiles(runs.P("session"), "orders-*.bin").Single();
        int lastAi = -1;
        long lastTurn = -1;
        for (int i = 0; i < runs.RunLog.Count; i++)
        {
            lastTurn = Math.Max(lastTurn, runs.RunLog[i].Turn);
            if (runs.RunLog[i].ActorId == 2) lastAi = i;
        }
        Assert.True(lastAi >= 0);
        Assert.Equal((14L, 14L), (runs.RunLog[lastAi].Turn, lastTurn));   // premise: nothing in the log is stamped after 14
        int firstAtFourteen = -1;
        for (int i = 0; i < runs.RunLog.Count && firstAtFourteen < 0; i++)
            if (runs.RunLog[i].Turn >= 14) firstAtFourteen = i;
        Assert.Equal(2, runs.RunLog[firstAtFourteen].ActorId);

        string s14 = runs.P("s14.civsnap"), s15 = runs.P("s15.civsnap");
        Assert.Equal(0, FromSaveRuns.Run([.. FromSaveRuns.World, "--turns", "16", "--orders", runs.Orders, "--save-at", "14", "--save", s14]).Exit);
        Assert.Equal(0, FromSaveRuns.Run([.. FromSaveRuns.World, "--turns", "16", "--orders", runs.Orders, "--save-at", "15", "--save", s15]).Exit);

        // K = 15: every AI record precedes K — accepted, and the step 15 -> 16 is the uninterrupted one.
        (int exit, string stdout, string stderr) = FromSaveRuns.Run("--from-save", s15, "--size", "256", "--turns", "1",
            "--orders", emitted, "--hash-log", runs.P("emitted15.log"));
        Assert.True(exit == 0, stderr);
        Assert.Equal("", stderr);
        Assert.Equal(runs.Full[15..16], File.ReadAllLines(runs.P("emitted15.log")));
        Assert.Equal($"run complete: seed 42, 1 turns from turn 15, hash {runs.Full[15]}", CliProcess.Lines(stdout).Single());

        // K = 14: the AI record stamped 14 is at K — refused.
        (exit, stdout, stderr) = FromSaveRuns.Run("--from-save", s14, "--size", "256", "--turns", "2", "--orders", emitted);
        Assert.Equal((2, ""), (exit, stdout));
        Assert.StartsWith($"error: --from-save takes a PLAYER input log: record {firstAtFourteen} (turn 14, " +
                          $"{runs.RunLog[firstAtFourteen].Kind}) is issued by polity 2, not the player (polity 1).", CliProcess.Lines(stderr)[0]);
    }

    [Fact]
    public void TheSameSave_IsContinued_WhenTheSeedTheKindAndTheAiCountGivenAgreeWithIt()
    {
        (int exit, string stdout, string stderr) = FromSaveRuns.Run("--from-save", runs.SaveK, "--turns", "3", "--size", "256",
            "--seed", "42", "--founded", "--ai-empires", "1", "--save-at", "9", "--save", runs.P("agree9.civsnap"));
        Assert.True(exit == 0, stderr);
        Assert.Equal($"run complete: seed 42, 3 turns from turn 6, hash {Orderless(3)}", CliProcess.Lines(stdout).Single());
        Assert.True(File.Exists(runs.P("agree9.civsnap")));
    }

    /// <summary>The orderless continuation's hash at the save's turn + <paramref name="steps"/>.</summary>
    private string Orderless(int steps)
    {
        string log = runs.P($"orderless{steps}.log");
        (int exit, _, string stderr) = FromSaveRuns.Run("--from-save", runs.SaveK, "--size", "256", "--turns",
            steps.ToString(System.Globalization.CultureInfo.InvariantCulture), "--hash-log", log);
        Assert.True(exit == 0, stderr);
        return File.ReadAllLines(log)[^1];
    }

    private string AiOrderAtK()
    {
        // An AI record stamped exactly at the save's turn (K = 6): the AI's granary at its own settlement 1.
        string path = runs.P("ai-at-k.bin");
        FromSaveRuns.WriteLog(path, new OrderRecord(FromSaveRuns.K, 2, OrderKind.EnqueueConstruction, 1, 1.0));
        return path;
    }

    /// <summary>
    /// WHY ANOTHER POLITY'S RECORD STAMPED AT THE SAVE'S TURN IS REFUSED (fixer round, finding T3-L2-T3-V2-F1) — turn-exact,
    /// in process, with the CLI's own loop (the loaded log's batch, then the AI's orders, then the step): the continuation's
    /// AI producer issues the AI's turn-K order ITSELF — the very record an emitted run log carries — so a log that carried it
    /// too would deliver it twice. K = 7, where the AI enqueues a granary: fed only the AI producer, the step K → K + 1
    /// gives the uninterrupted world K + 1; fed the emitted record as well, that same first step differs, with the granary
    /// enqueued a second time.
    /// </summary>
    [Fact]
    public void AnAiOrderStampedAtTheSavesTurn_IsIssuedAgainByTheContinuation_SoALogCarryingIt_WouldDeliverItTwice()
    {
        const long k = 7;
        OrderRecord emitted = Enumerable.Range(0, runs.RunLog.Count).Select(i => runs.RunLog[i]).Single(r => r.ActorId == 2 && r.Turn == k);
        Assert.Equal(OrderKind.EnqueueConstruction, emitted.Kind);

        byte[] bytes = File.ReadAllBytes(runs.SaveK);
        int version = BitConverter.ToInt32(bytes, 8);
        int? trailer = TopologyFlags.RecordedFlat(version) ? null : BitConverter.ToInt32(bytes, bytes.Length - 4);
        Sim.Core.Worldgen.WorldgenConfig aiWorldgen = SaveFiles.WorldgenFor(version, CliRecipes.Worldgen(), 256, trailer);
        SimConfig cfg = TestConfigs.PreDiscovery(TestConfigs.Sim());   // M6 EX: the CLI's --without-discovery
        OrderLog player;
        using (FileStream f = File.OpenRead(runs.Orders)) player = OrderLog.Load(f);

        // The world at K: the save at 6, stepped once by the loop with the player's log L — the uninterrupted world 7.
        WorldState w6 = SaveFiles.Load(runs.SaveK, 256);
        var log6 = new OrderLog();
        OrderBatch batch6 = player.BatchFor(FromSaveRuns.K);
        for (int i = 0; i < batch6.Count; i++) log6.Append(batch6[i]);
        AiOrders.Append(log6, w6, cfg, aiWorldgen);
        WorldState w7 = new TurnExecutor(CliRecipes.Era(), PreDiscoveryPipeline(), log6).Step(w6);
        Assert.Equal((k, runs.Full[6]), (w7.Clock.Turn, WorldHash.ComputeHex(w7)));

        // Fed only the AI producer (what --from-save does, given the player's log, which has nothing at 7): the producer
        // issues the emitted record itself, and the first step is the uninterrupted one.
        var aiOnly = new OrderLog();
        AiOrders.Append(aiOnly, w7, cfg, aiWorldgen);
        Assert.Equal(1, Count(aiOnly.BatchFor(k), emitted));
        WorldState once = new TurnExecutor(CliRecipes.Era(), PreDiscoveryPipeline(), aiOnly).Step(w7.Clone());
        Assert.Equal(runs.Full[7], WorldHash.ComputeHex(once));

        // Fed the emitted record as well (what accepting it would do): the record twice in the batch, and the very first
        // step differs — the granary enqueued a second time.
        var twice = new OrderLog();
        twice.Append(emitted);
        AiOrders.Append(twice, w7, cfg, aiWorldgen);
        Assert.Equal(2, Count(twice.BatchFor(k), emitted));
        WorldState doubled = new TurnExecutor(CliRecipes.Era(), PreDiscoveryPipeline(), twice).Step(w7.Clone());
        Assert.NotEqual(runs.Full[7], WorldHash.ComputeHex(doubled));
        Assert.Equal(once.ConstructionQueue.Count + 1, doubled.ConstructionQueue.Count);
    }

    private static int Count(OrderBatch batch, OrderRecord record)
    {
        int n = 0;
        for (int i = 0; i < batch.Count; i++) if (batch[i] == record) n++;
        return n;
    }

    private string AiOrderLog()
    {
        // The AI's own order, as an emitted run log carries it (its granary, stamped 7).
        string path = runs.P("ai.bin");
        FromSaveRuns.WriteLog(path, Enumerable.Range(0, runs.RunLog.Count).Select(i => runs.RunLog[i]).First(r => r.ActorId == 2 && r.Turn == 7));
        return path;
    }

    [Theory]
    [InlineData("founded", "--founded contradicts the save, which is of the M0 toy world (it has no terrain)")]
    [InlineData("size", "--size requires a founded save")]
    [InlineData("ai-empires", "--ai-empires requires a founded save")]
    [InlineData("split-report", "--split-report requires a founded world (the save is of the M0 toy world)")]
    public void AToySave_RefusesWhatOnlyAFoundedWorldTakes_AndOtherwiseContinuesAsTheToyRun(string what, string reason)
    {
        string toy = runs.P("toy.civsnap");
        if (!File.Exists(toy))
            Assert.Equal(0, FromSaveRuns.Run("--seed", "7", "--turns", "6", "--save-at", "3", "--save", toy, "--hash-log", runs.P("toy.log")).Exit);
        string[] args = what switch
        {
            "founded" => ["--from-save", toy, "--turns", "3", "--founded"],
            "size" => ["--from-save", toy, "--turns", "3", "--size", "256"],
            "ai-empires" => ["--from-save", toy, "--turns", "3", "--ai-empires", "1"],
            _ => ["--from-save", toy, "--turns", "3", "--split-report", runs.P("toy.jsonl")],
        };
        (int exit, string stdout, string stderr) = FromSaveRuns.Run(args);
        Assert.Equal((1, ""), (exit, stdout));
        Assert.StartsWith("error: " + reason, CliProcess.Lines(stderr)[0]);

        (exit, _, stderr) = FromSaveRuns.Run("--from-save", toy, "--turns", "3", "--hash-log", runs.P("toy-tail.log"));
        Assert.True(exit == 0, stderr);
        Assert.Equal(File.ReadAllLines(runs.P("toy.log"))[3..6], File.ReadAllLines(runs.P("toy-tail.log")));
    }

    [Fact]
    public void ASaveWhoseFoundingCannotBeRegenerated_IsRefused_NotValidatedAgainstAnotherWorld()
    {
        // Round 3 (finding T3-L2-T3V2-L2-F3): the doctored founding is now a CAPITAL founding never made — the AI Empire's
        // seat moved from settlement 1 (its first settlement at founding) to settlement 3 (also its own). The founding
        // regenerated from the seed seats it at 1, so the save is refused, with that capital named. (Until round 3 this test
        // appended an AI polity founding never made, as a revolt does; such a roster is now a founding plus a revolt-born
        // polity — ARevoltBornPolity_IsNotAFoundingEmpire_… and ASaveMadeAfterARealRevolt_… below.)
        WorldState w = SaveFiles.Load(runs.SaveK, 256);
        Assert.Equal((2, 2, 1), (w.Capitals.Count, w.Capitals[1].Polity.Value, w.Capitals[1].Place.Value));
        Assert.True(EmpireQuery.ControlsSettlement(w, new PolityId(2), new SettlementId(3)), "settlement 3 is the AI Empire's own");
        CapitalRow player = w.Capitals[0];
        w.Capitals.Clear();
        w.Capitals.Add(player);
        w.Capitals.Add(new CapitalRow(new PolityId(2), new SettlementId(3)));
        string doctored = runs.P("doctored.civsnap");
        using (FileStream f = File.Create(doctored)) Snapshot.Save(w, f);

        (int exit, string stdout, string stderr) = FromSaveRuns.Run("--from-save", doctored, "--size", "256", "--turns", "1");
        Assert.Equal((2, ""), (exit, stdout));
        Assert.StartsWith($"error: --from-save cannot regenerate the founding world of {doctored} from its seed: founding capital 1 " +
                          "(polity 2 at settlement 1) is not the save's.", CliProcess.Lines(stderr)[0]);
    }

    /// <summary>
    /// Round 3 (finding T3-L2-T3V2-L2-F3) — A POLITY FOUNDING NEVER MADE, HOLDING NO CAPITAL, IS NOT A FOUNDING EMPIRE: the
    /// save's roster gains AI polity 3 with no Capitals row (as a revolt-born polity has none). The founding count stays 1
    /// (the AI polity holding a capital, fewer than the 4 founding settlements less the player's), so `--ai-empires 2` — the
    /// roster's AI polity count — is refused naming the polity it does not count, and `--ai-empires 1` is not refused for
    /// the count (the run is then judged on the regenerated founding, which matches).
    /// </summary>
    [Fact]
    public void ARevoltBornPolity_IsNotAFoundingEmpire_AiEmpiresIsTheFoundingCount()
    {
        WorldState w = SaveFiles.Load(runs.SaveK, 256);
        w.Polities.Add(new PolityRow(new PolityId(3), CommandSource.Ai));
        string doctored = runs.P("revoltlike.civsnap");
        using (FileStream f = File.Create(doctored)) Snapshot.Save(w, f);

        (int exit, string stdout, string stderr) = FromSaveRuns.Run("--from-save", doctored, "--size", "256", "--turns", "1", "--ai-empires", "2");
        Assert.Equal((1, ""), (exit, stdout));
        Assert.StartsWith("error: --ai-empires 2 contradicts the save, which was founded with 1 AI Empire (--ai-empires is the founding " +
                          "count: the save's 1 revolt-born AI polity is not counted)", CliProcess.Lines(stderr)[0]);
    }

    /// <summary>
    /// Round 3 (finding T3-L2-T3V2-L2-F3) — A SAVE MADE AFTER A REAL REVOLT CONTINUES, TO THE UNINTERRUPTED RUN'S TAIL. The rig
    /// is CliRevoltPolityProducerTests' (seed 42, canonical world, no AI Empire at founding; settlement 0, unfed and unhoused,
    /// revolts at 37 and a new AI polity rules it) plus three player records stamped 41 — a granary at the revolted
    /// settlement 0 (the player's at founding, not at delivery), a granary at settlement 1 and a labour order there — so the
    /// continued window delivers player orders that turn zero validates. The save at 40 holds the revolt-born polity (no
    /// capital); turn zero is regenerated with the FOUNDING count, 0, and the continuation is the uninterrupted tail, hash for
    /// hash; its orders are delivered (the orderless continuation differs). `--ai-empires 0`, the founding value, is accepted;
    /// `--ai-empires 1` is refused naming the revolt-born polity. Until round 3 the save was refused ("the founding world has
    /// 2 capitals, the save 1") and so was `--ai-empires 0`.
    /// </summary>
    [Fact]
    public void ASaveMadeAfterARealRevolt_ContinuesToTheUninterruptedTail_AndAiEmpiresIsTheFoundingCount()
    {
        string log = runs.P("revolt-player.bin");
        using (FileStream f = File.Create(log)) RevoltPlayerLog().Save(f);
        string s40 = runs.P("revolt40.civsnap");
        (int exit, string stdout, string stderr) = FromSaveRuns.Run("--founded", "--seed", "42", "--turns", "43", "--orders", log,
            "--hash-log", runs.P("revolt-u43.log"), "--save-at", "40", "--save", s40);
        Assert.True(exit == 0, stderr);
        string[] full = File.ReadAllLines(runs.P("revolt-u43.log"));
        Assert.Equal(43, full.Length);

        // Premise: the save at 40 holds the polity the revolt founded — AI, ruling settlement 0, with no capital — beside the
        // player, the one Empire of the founding (one capital).
        WorldState w40 = SaveFiles.Load(s40, -1);
        Assert.Equal((2, 1, CommandSource.Player, 2, CommandSource.Ai),
            (w40.Polities.Count, w40.Polities[0].Id.Value, w40.Polities[0].Source, w40.Polities[1].Id.Value, w40.Polities[1].Source));
        Assert.Equal((1, 1, 0), (w40.Capitals.Count, w40.Capitals[0].Polity.Value, w40.Capitals[0].Place.Value));
        Assert.True(EmpireQuery.TryGetController(w40, new SettlementId(0), out PolityId ruler) && ruler.Value == 2, "settlement 0 revolted");

        (exit, stdout, stderr) = FromSaveRuns.Run("--from-save", s40, "--turns", "3", "--orders", log, "--hash-log", runs.P("revolt-c43.log"));
        Assert.True(exit == 0, stderr);
        Assert.Equal("", stderr);
        Assert.Equal(full[40..43], File.ReadAllLines(runs.P("revolt-c43.log")));
        Assert.Equal($"run complete: seed 42, 3 turns from turn 40, hash {full[42]}", CliProcess.Lines(stdout).Single());

        // The records stamped 41 were delivered: without them the continuation differs.
        (exit, _, stderr) = FromSaveRuns.Run("--from-save", s40, "--turns", "3", "--hash-log", runs.P("revolt-n43.log"));
        Assert.True(exit == 0, stderr);
        Assert.NotEqual(full[40..43], File.ReadAllLines(runs.P("revolt-n43.log")));

        // --ai-empires is the founding count: 0 is accepted (the same run), 1 — the roster's AI polity count — is refused.
        (exit, stdout, stderr) = FromSaveRuns.Run("--from-save", s40, "--turns", "3", "--orders", log, "--ai-empires", "0");
        Assert.True(exit == 0, stderr);
        Assert.Equal($"run complete: seed 42, 3 turns from turn 40, hash {full[42]}", CliProcess.Lines(stdout).Single());
        (exit, stdout, stderr) = FromSaveRuns.Run("--from-save", s40, "--turns", "3", "--ai-empires", "1");
        Assert.Equal((1, ""), (exit, stdout));
        Assert.StartsWith("error: --ai-empires 1 contradicts the save, which was founded with 0 AI Empires (--ai-empires is the founding " +
                          "count: the save's 1 revolt-born AI polity is not counted)", CliProcess.Lines(stderr)[0]);
    }

    /// <summary>CliRevoltPolityProducerTests' player log (FoundedHarnessTests.SessionPcts' labour swings on settlement 0 and,
    /// with its 0 % swing, every other settlement's food labour abandoned), plus three player records stamped 41: a granary at
    /// settlement 0, a granary at settlement 1, settlement 1's labour at 50 %.</summary>
    private static OrderLog RevoltPlayerLog()
    {
        WorldState founded = HeadlessFounding.Found(42);
        var records = new List<OrderRecord>();
        double[] pcts = FoundedHarnessTests.SessionPcts;
        for (int i = 0; i < pcts.Length; i++)
        {
            int turn = 3 + i * 30;
            records.Add(new OrderRecord(turn, 1, OrderKind.LaborAllocation, 0, pcts[i]));
            if (pcts[i] != 0.0) continue;
            double[] housedUnfed = [0.0, 0.0, 50.0, 0.0, 50.0];
            for (int s = 0; s < founded.Settlements.Count; s++)
            {
                int id = founded.Settlements[s].Id.Value;
                if (id == 0) continue;
                for (int sector = 0; sector < housedUnfed.Length; sector++)
                    records.Add(new OrderRecord(turn, 1, OrderKind.SectorAllocation, id * 8 + sector, housedUnfed[sector]));
            }
        }
        records.Add(new OrderRecord(41, 1, OrderKind.EnqueueConstruction, 0, 1.0));
        records.Add(new OrderRecord(41, 1, OrderKind.EnqueueConstruction, 1, 1.0));
        records.Add(new OrderRecord(41, 1, OrderKind.LaborAllocation, 1, 50.0));
        var log = new OrderLog();
        foreach (OrderRecord r in records.OrderBy(r => r.Turn)) log.Append(r);   // stable: the rig's order within a turn
        return log;
    }

    /// <summary>
    /// Round 3 (finding T3-L2-T3V2-L2-F3) — A FOUNDING WITH MORE EMPIRES THAN SETTLEMENTS: 4 Empires on 2 settlements, so two
    /// AI Empires are founded with nothing and no capital. The founding count is NOT the AI polities holding a capital (1)
    /// there — every founding settlement is a capital — but the roster's AI count (3): `--ai-empires 3` continues to the
    /// uninterrupted tail and `--ai-empires 1` is refused.
    /// </summary>
    [Fact]
    public void AFoundingWithMoreEmpiresThanSettlements_KeepsItsCapitalLessEmpires_InTheFoundingCount()
    {
        string[] world = ["--founded", "--seed", "42", "--size", "256", "--settlements", "2", "--ai-empires", "3"];
        string s4 = runs.P("crowded4.civsnap");
        (int exit, _, string stderr) = FromSaveRuns.Run([.. world, "--turns", "8", "--hash-log", runs.P("crowded8.log"), "--save-at", "4", "--save", s4]);
        Assert.True(exit == 0, stderr);
        WorldState w4 = SaveFiles.Load(s4, 256);
        Assert.Equal((4, 2), (w4.Polities.Count, w4.Capitals.Count));

        (exit, _, stderr) = FromSaveRuns.Run("--from-save", s4, "--size", "256", "--turns", "4", "--ai-empires", "3", "--hash-log", runs.P("crowded-c8.log"));
        Assert.True(exit == 0, stderr);
        Assert.Equal(File.ReadAllLines(runs.P("crowded8.log"))[4..8], File.ReadAllLines(runs.P("crowded-c8.log")));
        (exit, string stdout, stderr) = FromSaveRuns.Run("--from-save", s4, "--size", "256", "--turns", "4", "--ai-empires", "1");
        Assert.Equal((1, ""), (exit, stdout));
        Assert.StartsWith("error: --ai-empires 1 contradicts the save, which was founded with 3 AI Empires", CliProcess.Lines(stderr)[0]);
    }

    // ================================================================== the split report changes nothing

    [Fact]
    public void TheSplitReport_ChangesNoHashAndNoSave_FromFoundingAndContinued_AndItsLiveRowsKeepThePerRowProperties()
    {
        // From founding: the same run as the class's, with the report.
        (int exit, _, string stderr) = FromSaveRuns.Run([.. FromSaveRuns.World, "--turns", "16", "--orders", runs.Orders,
            "--hash-log", runs.P("rep.log"), "--save-at", "10", "--save", runs.P("rep10.civsnap"), "--split-report", runs.P("rep.jsonl")]);
        Assert.True(exit == 0, stderr);
        Assert.Equal(runs.Full, File.ReadAllLines(runs.P("rep.log")));
        Assert.Equal(File.ReadAllBytes(runs.SaveK4), File.ReadAllBytes(runs.P("rep10.civsnap")));

        // Continued: with and without the report.
        (exit, _, stderr) = FromSaveRuns.Run("--from-save", runs.SaveK, "--size", "256", "--turns", "10", "--orders", runs.Orders,
            "--hash-log", runs.P("crep.log"), "--save-at", "10", "--save", runs.P("crep10.civsnap"), "--split-report", runs.P("crep.jsonl"));
        Assert.True(exit == 0, stderr);
        Assert.Equal(runs.Full[6..16], File.ReadAllLines(runs.P("crep.log")));
        Assert.Equal(File.ReadAllBytes(runs.SaveK4), File.ReadAllBytes(runs.P("crep10.civsnap")));

        // The reports: one row per Empire per step (two Empires), the continued one the tail of the uninterrupted one, and the
        // per-row properties hold on what the CLI wrote — at turn 6 the player's two granaries build within b = 30 (the
        // batch's own enqueues), at 7 and 14 the AI's (the AI's orders in the run log's batch).
        SplitReportRow[] whole = SplitReport.Parse(File.ReadAllText(runs.P("rep.jsonl")));
        SplitReportRow[] tail = SplitReport.Parse(File.ReadAllText(runs.P("crep.jsonl")));
        Assert.Equal(32, whole.Length);
        Assert.Equal(File.ReadAllLines(runs.P("rep.jsonl"))[12..32], File.ReadAllLines(runs.P("crep.jsonl")));
        Assert.Equal(20, tail.Length);
        SplitReportRow r6 = whole.Single(r => r.Turn == 6 && r.Polity == 1);
        Assert.Equal((30, 2), (r6.BuildingsPercent, r6.Heads.Length));
        SplitGoodFigures timber6 = r6.GoodOf(Sim.Tests.Systems.IncrementalConstructionTests.Timber)!;
        Assert.Equal((80L, 80L), (timber6.Build, timber6.ForecastBuild));
        // Each granary completes in the step (0 -> 1,000,000): its need is what it still needed when the step BEGAN.
        Assert.All(r6.Heads, h => Assert.Equal((0L, 1_000_000L, 40L, 40L), (h.Before, h.After,
            h.GoodOf(Sim.Tests.Systems.IncrementalConstructionTests.Timber)!.Need, h.GoodOf(Sim.Tests.Systems.IncrementalConstructionTests.Timber)!.Draw)));
        Assert.Single(whole.Single(r => r.Turn == 7 && r.Polity == 2).Heads);
        Assert.Single(whole.Single(r => r.Turn == 14 && r.Polity == 2).Heads);
        SplitCheckResult check = SplitReportCheck.Check(whole);
        // T3 round 2: P11 (the split re-derived) and P12 (the walk re-derived; here b = 30 AND the AI's b = 100 rows) too.
        foreach (string p in new[] { "P1", "P2", "P3", "P11", "P12" }) Assert.False(check.Failed(p), string.Join(" | ", check.Failures));
    }
}
