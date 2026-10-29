using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.State;
using Sim.Tests.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 4 — "… across save/load … and deterministic replay"; batch-4
/// design §4 P4 and P7; recon persistence-audit §7 P4 and P7) — THE CLI ON POPULATED CONSTRUCTION TABLES, ACROSS PROCESSES.
/// Every command here is the REAL `sim` executable in its own process (CliProcess). The run is the v33 fixture's recipe
/// (`sim run --founded --seed 42 --size 256 --settlements 4 --ai-empires 1 --orders q.bin`, README-v33-fixture.md) under
/// THIS build, so its saves hold all three construction tables POPULATED — ConstructionProgress (s0 part-built and waiting
/// without builders, s2 younger), ResourceAllocations (the player's 30/70 split) and ConstructionHolds (s0's set-aside) —
/// which no CLI test held before (recon persistence-audit §3: column B uncovered in every row).
///  — `sim hash` of a populated save, in another process, prints the run's own hash-log line for that turn;
///  — `sim replay` of the run's emitted log reproduces the hash log, and `sim inspect` verifies the session;
///  — `sim diff` of two populated saves that differ ONLY in the split names ResourceAllocations as the first and only
///    difference; `sim diff` of the batch-3 build's v33 save and this build's save of the same world differs ONLY in the
///    v34 block, with zero rows on the v33 side; and of the v33 save and its own v34 re-save, nowhere;
///  — P7: a cancel (kind 13 since D-056; kind 11 before) naming a colony founded mid-run (an id above the turn-0 maximum, deferred to delivery,
///    ADR-034) is accepted by `sim run` and `sim replay` and removes the colony's entry turn-exact; with the founding
///    order removed from the log the run is refused at delivery with the ADR-034 message.
/// </summary>
public class CliConstructionLifecycleTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);
    private static readonly string[] Founded = ["--founded", "--seed", "42", "--size", "256", "--settlements", "4", "--ai-empires", "1"];
    private static readonly PolityId Player = new(1);
    private static readonly SettlementId S0 = new(0), S2 = new(2);
    private const int Granary = 1;

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-cli-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Q17Orders(string dir, params OrderRecord[] extra)
    {
        OrderLog q;
        using (FileStream f = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "v33-fixture-orders.bin"))) q = OrderLog.Load(f);
        foreach (OrderRecord o in extra) q.Append(o);
        string path = Path.Combine(dir, "q-" + Guid.NewGuid().ToString("N") + ".bin");
        using (FileStream f = File.Create(path)) q.Save(f);
        return path;
    }

    // M6 Track EX (2026-10-22; re-checked 2026-10-23 with stone no longer concealed, ADR-044 §12): run, replay and inspect
    // take `--without-discovery` — these rigs compare against sessions and saves RECORDED BEFORE M6 (the v33 fixture's order
    // log, trace and save) and premise the colony at 12, which the AI founds only on a known site under the fog. MEASURED
    // without the switch: the replay of the v33 session diverges from its trace at the first hash; `run` refuses the
    // turn-3 order naming settlement 12 ("none was" founded); the v33/v34 diff gains LedgerFlows rows (the expedition
    // sinks). The discovery twin (R4); `hash` and `diff` read files only.
    // ADR-056: and `--without-economy` — the same sessions and saves predate the economy packets (their goods add stock and
    // deposit rows from turn 0, so the v33/v34 diff would gain GoodStocks and Deposits rows and every hash would move).
    private static (int Exit, string Out, string Err) Sim(params string[] args) =>
        CliProcess.Run(Limit, args.Length > 0 && args[0] is "run" or "replay" or "inspect" ? [args[0], "--without-discovery", "--without-economy", .. args[1..]] : args);

    /// <summary>ADR-045 (D-062, 2026-10-22): `sim run` with <see cref="Founded"/> and `--orders <paramref name="ordersPath"/>`
    /// IN PROCESS under <paramref name="cfg"/> — the CLI's recipe (Sim.Cli Program: before each step the loaded log's rows for
    /// the turn, then the AI's orders; the production pipeline on the shipped worldgen). Returns worlds 0..turns. A CLI
    /// process always runs the SHIPPED research content, so a comparison with a session the v33 build recorded (before the
    /// research-content tranche — before M6) runs this twin under TestConfigs.PreM6 (M6 merge: the tranche, Track CP and the
    /// discovery layer off); the arm the CLI runs (the shipped content under `--without-discovery`: TestConfigs.PreDiscovery)
    /// is checked against the CLI's own hash log, which ties the twin to the process.</summary>
    private static WorldState[] InProcessRun(global::Sim.Core.Systems.SimConfig cfg, string ordersPath, int turns)
    {
        OrderLog q;
        using (FileStream f = File.OpenRead(ordersPath)) q = OrderLog.Load(f);
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = global::Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = global::Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era),
                PipelineLoader.Load(pipe, global::Sim.Core.SystemCatalog.All(cfg, TestConfigs.Worldgen())), log);
        var worlds = new WorldState[turns + 1];
        worlds[0] = global::Sim.Core.Worldgen.WorldFounding.Found(TestConfigs.Worldgen() with { SizePx = 256, AiEmpires = 1 }, cfg, 42, 4);
        for (int t = 1; t <= turns; t++)
        {
            OrderBatch b = q.BatchFor(worlds[t - 1].Clock.Turn);
            for (int i = 0; i < b.Count; i++) log.Append(b[i]);
            global::Sim.Core.AiOrders.Append(log, worlds[t - 1], cfg, TestConfigs.Worldgen());
            worlds[t] = exec.Step(worlds[t - 1]);
        }
        return worlds;
    }

    private static string[] Run(string orders, int turns, string? hashLog, int saveAt, string save, params string[] more)
    {
        var args = new List<string> { "run" };
        args.AddRange(Founded);
        args.AddRange(["--turns", turns.ToString(System.Globalization.CultureInfo.InvariantCulture), "--orders", orders,
            "--save-at", saveAt.ToString(System.Globalization.CultureInfo.InvariantCulture), "--save", save]);
        if (hashLog is not null) args.AddRange(["--hash-log", hashLog]);
        args.AddRange(more);
        (int exit, string stdout, string stderr) = Sim([.. args]);
        Assert.True(exit == 0, $"sim run exited {exit}: {stderr}");
        return CliProcess.Lines(stdout);
    }

    private static long Held(IReadOnlyWorldState w, int good)
    {
        long sum = 0;
        for (int i = 0; i < w.ConstructionHolds.Count; i++)
            if (w.ConstructionHolds[i].Purse == Reservations.PolityPurse(Player) && w.ConstructionHolds[i].Good == good) sum += w.ConstructionHolds[i].Units;
        return sum;
    }

    /// <summary>The per-table summary rows `sim diff` prints that are FLAGGED ("&lt;--"), as their table names.</summary>
    private static string[] Flagged(string stdout)
    {
        var tables = new List<string>();
        foreach (string line in CliProcess.Lines(stdout))
            if (line.EndsWith("  <--", StringComparison.Ordinal)) tables.Add(line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        return [.. tables];
    }

    private static string SummaryRow(string stdout, string table)
    {
        foreach (string line in CliProcess.Lines(stdout))
            if (line.TrimStart().StartsWith(table + " ", StringComparison.Ordinal)) return line;
        throw new InvalidOperationException("no summary row for " + table);
    }

    /// <summary>
    /// P4 — `sim hash` ON A POPULATED v34 SAVE, ACROSS PROCESSES. `sim run` (the Q17 recipe, 4 turns, --hash-log) saves world 3;
    /// a second process's `sim hash --size 256` of that save prints exactly the hash-log's turn-3 line. The save holds all three
    /// construction tables POPULATED (read back by the CLI's own loader): s0 at 700,000 and s2 at 150,000, both keyed 0; the
    /// player's (1, 30); and the set-aside of the player's purse — 6 stone (s0's Remaining(20, 700,000)) and 46 timber (s0's 12
    /// and s2's 34) — the figures this build's in-process replay of the same orders gives (ConstructionFixtureContinuationTests).
    /// </summary>
    [Fact]
    public void SimHash_OfAPopulatedV34Save_InAnotherProcess_IsTheRunsHashLogLine()
    {
        string dir = TempDir();
        try
        {
            string h = Path.Combine(dir, "h.txt"), s3 = Path.Combine(dir, "s3.civsnap");
            Run(Q17Orders(dir), 4, h, 3, s3);
            string[] log = File.ReadAllLines(h);
            Assert.Equal(4, log.Length);
            (int exit, string stdout, string stderr) = Sim("hash", s3, "--size", "256");
            Assert.Equal((0, ""), (exit, stderr));
            Assert.Equal(new[] { log[2] }, CliProcess.Lines(stdout));

            WorldState w = SaveFiles.Load(s3, 256, out string? note);
            Assert.Null(note);
            Assert.Equal(log[2], WorldHash.ComputeHex(w));
            ConstructionQueueRow q0 = new(S0, 0, Granary), q2 = new(S2, 0, Granary);
            Assert.Equal((700_000L, 150_000L, 0L, 0L), (ConstructionQuery.ProgressOf(w, q0), ConstructionQuery.ProgressOf(w, q2),
                ConstructionQuery.EnqueuedTurnOf(w.ConstructionProgress, q0), ConstructionQuery.EnqueuedTurnOf(w.ConstructionProgress, q2)));
            Assert.Equal(new ResourceAllocationRow(Player, 30), Assert.Single(TableRows.Rows(w.ResourceAllocations)));
            int timber = TestConfigs.Sim().Goods!.IdOf("timber"), stone = TestConfigs.Sim().Goods!.IdOf("stone");
            Assert.Equal((46L, 6L), (Held(w, timber), Held(w, stone)));
            Assert.Equal(CanonicalSchema.ExpectedLength(w), SchemaProjection.Serialize(w).LongLength);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// P4 — `sim replay` AND `sim inspect` OF A SESSION WITH POPULATED v34 TABLES. `sim run … --emit-session` writes the run's
    /// own log (the player's four orders and the AI's) and saves world 2; a second `sim run` of the same orders saves world 3
    /// and writes the same hash log as far as it goes. `sim replay` of the emitted log in another process reproduces the
    /// run's hash log line for line, and `sim inspect --manifest` reports the reproduction VERIFIED for all 7 recorded turns
    /// (0 to 6) — a v34 session is trace-checked, unlike the batch-3 v33 session (ConstructionHoldsV33FixtureTests.PremiseIV:
    /// NotChecked(33)).
    /// WHAT THE REPLAY REPRODUCED (D-055 fix round, 2026-10-17, T4-L1-T4V1-F3): the two saves ARE the replayed worlds 2 and 3
    /// (their hashes are the replayed hash log's lines 2 and 3). World 2 holds all three construction tables POPULATED (two
    /// progress rows, the (1, 30) split, hold rows), and its v33 projection — its hold rows stripped — IS the batch-3
    /// session's world 2: the set-asides are published but have bound nothing yet. World 3's v33 projection is NOT the batch-3
    /// session's world 3: in step 2 → 3 s0's set-aside binds, and s2 reaches 150,000 where the batch-3 rule gives it 450,000.
    /// With the hold rule off (M-OFF) world 2 publishes no hold row and world 3's projection is the batch-3 trace's, so this
    /// test fails. The raw trace hashes are NOT compared: a v34 hash differs from the v33 one at EVERY turn, from turn 0, by
    /// the v34 layout's extra empty count prefix alone, so they cannot show binding.
    /// </summary>
    [Fact]
    public void SimReplayAndInspect_OfASessionWithPopulatedTables_ReproduceItAcrossProcesses()
    {
        string dir = TempDir();
        try
        {
            string session = Path.Combine(dir, "session"), orders = Q17Orders(dir);
            string h = Path.Combine(dir, "h.txt"), h2 = Path.Combine(dir, "h2.txt"), h3 = Path.Combine(dir, "h3.txt");
            string s2 = Path.Combine(dir, "s2.civsnap"), s3 = Path.Combine(dir, "s3.civsnap");
            Run(orders, 6, h, 2, s2, "--emit-session", session);
            Run(orders, 3, h3, 3, s3);
            string[] hashes = File.ReadAllLines(h);
            Assert.Equal(6, hashes.Length);                                                  // line i is world i + 1
            Assert.Equal(hashes[..3], File.ReadAllLines(h3));
            string manifest = Assert.Single(Directory.GetFiles(session, "session-*.json"));
            string runLog = Assert.Single(Directory.GetFiles(session, "orders-*.bin"));

            var args = new List<string> { "replay" };
            args.AddRange(Founded);
            args.AddRange(["--orders", runLog, "--turns", "6", "--hash-log", h2]);
            (int exit, string stdout, string stderr) = Sim([.. args]);
            Assert.True(exit == 0, $"sim replay exited {exit}: {stderr}");
            Assert.Equal(File.ReadAllBytes(h), File.ReadAllBytes(h2));

            (exit, stdout, stderr) = Sim("inspect", "--manifest", manifest);
            Assert.Equal((0, ""), (exit, stderr));
            Assert.Contains("reproduction VERIFIED: 7 turns, hash-for-hash.", stdout, StringComparison.Ordinal);

            string[] v33Trace = File.ReadAllLines(Path.Combine(ConstructionHoldsV33FixtureTests.SessionDir(), "trace-" + ConstructionHoldsV33FixtureTests.Stamp + ".csv"));
            string[] v34Trace = File.ReadAllLines(Assert.Single(Directory.GetFiles(session, "trace-*.csv")));
            Assert.Equal(v33Trace[0], v34Trace[0]);
            static string TraceHash(string[] trace, int turn) => trace[1 + turn].Split(',')[^1];

            WorldState w2 = SaveFiles.Load(s2, 256), w3 = SaveFiles.Load(s3, 256);
            Assert.Equal((hashes[1], hashes[2]), (WorldHash.ComputeHex(w2), WorldHash.ComputeHex(w3)));   // the replayed worlds 2 and 3
            Assert.Equal((2, new ResourceAllocationRow(Player, 30)), (w2.ConstructionProgress.Count, Assert.Single(TableRows.Rows(w2.ResourceAllocations))));
            Assert.Equal(150_000L, ConstructionQuery.ProgressOf(w3, new ConstructionQueueRow(S2, 0, Granary)));
            // ADR-045 (D-062, 2026-10-22): the v33 trace was recorded before the research-content tranche, and from turn 0 the
            // AI targets different research (the tranche's re-scores), so the CLI's worlds no longer project onto it. The
            // in-process twin of this very run is checked against the CLI's hash log (the shipped content), and the SAME twin
            // under the pre-M6 content (TestConfigs.PreM6 — M6 merge: the tranche is newer than the v33 recording, and so are
            // Track CP and the discovery layer) carries the v33-trace comparison: world 2 projects to the v33 build's hash with
            // the purse's set-aside published, and the hold binds in step 2 → 3. The CLI arm runs `--without-discovery`.
            WorldState[] twin = InProcessRun(TestConfigs.PreDiscovery(TestConfigs.Sim()), orders, 3);
            Assert.Equal(hashes[..3], new[] { WorldHash.ComputeHex(twin[1]), WorldHash.ComputeHex(twin[2]), WorldHash.ComputeHex(twin[3]) });
            WorldState[] pre = InProcessRun(TestConfigs.PreM6Sim(), orders, 3);
            Assert.Equal(TraceHash(v33Trace, 2), SchemaProjection.HashAtSchemaV33(pre[2], out int holdRows2));
            Assert.True(holdRows2 > 0, "world 2 publishes the purse's set-aside (the populated v34 table)");
            Assert.NotEqual(TraceHash(v33Trace, 3), SchemaProjection.HashAtSchemaV33(pre[3], out _));      // the hold binds in step 2 → 3
            Assert.Equal(150_000L, ConstructionQuery.ProgressOf(pre[3], new ConstructionQueueRow(S2, 0, Granary)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// P4 — `sim diff` WALKS POPULATED v34 TABLES. Two runs that differ only by one more order — the player's split changed
    /// 30 → 50 at turn 2, which binds nothing (the player orders no road works) — saved at turn 3: `sim diff` (exit 1) names
    /// ResourceAllocations.BuildingsPercent, 30 against 50, as the FIRST divergent field, and it is the only table flagged; the
    /// populated ConstructionProgress and ConstructionHolds blocks are walked to equal row counts (2 and 2, no differing row).
    /// </summary>
    [Fact]
    public void SimDiff_OfTwoPopulatedSavesThatDifferOnlyInTheSplit_NamesResourceAllocationsFirstAndAlone()
    {
        string dir = TempDir();
        try
        {
            string a = Path.Combine(dir, "a.civsnap"), b = Path.Combine(dir, "b.civsnap");
            Run(Q17Orders(dir), 3, null, 3, a);
            Run(Q17Orders(dir, new OrderRecord(2, 1, OrderKind.SetResourceAllocation, 1, 50.0)), 3, null, 3, b);
            (int exit, string stdout, string stderr) = Sim("diff", a, b, "--size", "256");
            Assert.Equal((1, ""), (exit, stderr));
            string[] lines = CliProcess.Lines(stdout);
            int first = Array.IndexOf(lines, "FIRST DIVERGENT FIELD");
            Assert.True(first >= 0, stdout);
            Assert.Equal(["  table  ResourceAllocations", "  row    0", "  field  BuildingsPercent (Int32)", "  A      30", "  B      50"], lines[(first + 1)..(first + 6)]);
            Assert.Equal(["ResourceAllocations"], Flagged(stdout));
            Assert.Matches(@"^\s*ConstructionProgress\s+2\s+2\s+2\s+0\s+-$", SummaryRow(stdout, "ConstructionProgress"));
            Assert.Matches(@"^\s*ConstructionHolds\s+2\s+2\s+2\s+0\s+-$", SummaryRow(stdout, "ConstructionHolds"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// P4 — `sim diff` ACROSS THE SCHEMA STEP (design §4 P4: "shows the extra block with zero rows on the v33 side"). The batch-3
    /// build's v33 save of world 2 (the fixture) against THIS build's save of the same world (the same recipe, saved at 2):
    /// the first divergent field is ConstructionHolds' row count — 0 on the v33 side, 1 on the v34 side (the player's purse
    /// sets timber aside for both granaries from world 1) — and it is the only table flagged: everything else is the same world (X1 premise (ii):
    /// worlds 0–2 equal the batch-3 trace once their hold rows are stripped). The v33 file's migration note is printed once on
    /// stderr. Against its own v34 re-save the v33 save is identical (exit 0): the migration adds the empty block and nothing
    /// else.
    /// </summary>
    [Fact]
    public void SimDiff_OfTheV33FixtureAndThisBuildsSaveOfTheSameWorld_DiffersOnlyInTheV34Block_WithZeroRowsOnTheV33Side()
    {
        string dir = TempDir();
        try
        {
            string v33 = ConstructionHoldsV33FixtureTests.FixturePath(), v34 = Path.Combine(dir, "v34-t2.civsnap");
            // ADR-045 (D-062, 2026-10-22): "the same world" as the v33 fixture is the pre-tranche research content's — a CLI
            // process runs the shipped content, whose AI targets different research from turn 0 — so this build's save of
            // it is written by the in-process twin of `sim run` (InProcessRun, tied to the CLI by the replay test above)
            // under the pre-tranche content with the discovery layer off (TestConfigs.PreTreeTranche ∘ PreDiscovery — M6 merge:
            // the world this build's CLI wrote under `--without-discovery` before the tranche; Track CP stays on, so its
            // accounts are flagged below). The diff itself is still the CLI's.
            using (FileStream f = File.Create(v34))
                Snapshot.Save(InProcessRun(TestConfigs.PreTreeTranche(TestConfigs.PreDiscovery(TestConfigs.Sim())), Q17Orders(dir), 2)[2], f);
            (int exit, string stdout, string stderr) = Sim("diff", v33, v34, "--size", "256");
            Assert.Equal(1, exit);
            // ADR-043 + ADR-044 (2026-10-22) RE-PIN of the note: v35 adds CivicsPoints and the four discovery tables empty, and
            // the hash printed is v35's.
            Assert.Equal(new[] { "note: " + v33 + ": migrated from save schema v33: ConstructionHolds, CivicsPoints and the four M6 discovery tables (KnownExtents, Explorations, SettlementSightings, SettlementSurveys) were added empty; "
                + "the hash printed is the v39 hash (SchemaProjection.V33 gives the v33 build's). ResearchLevels (no repeatable research level existed before v36) was added empty too. ResearchReserves (no research reserve existed before v37) was added empty too. ResearchPopulations (no stored turn-start population existed before v38; each holder's first step reads its current population) was added empty too."
                + " The four military tables (FormationPersonnel, Reequips, ReequipEscrow, ReequipLog) were added empty (every saved formation stays a token that holds no one) and any formation of a rehomed identity was moved to its new family (ADR-055)." },
                CliProcess.Lines(stderr));   // ADR-049 (v36) RE-PIN of the note: ResearchLevels is added empty too; ADR-051 (v37): ResearchReserves too
            string[] lines = CliProcess.Lines(stdout);
            int first = Array.IndexOf(lines, "FIRST DIVERGENT FIELD");
            Assert.True(first >= 0, stdout);
            Assert.Equal(["  table  ConstructionHolds", "  row    (block-level)", "  field  RowCount (Int32)", "  A      0", "  B      1"], lines[(first + 1)..(first + 6)]);
            // ADR-043 (2026-10-22) CAUSE BLOCK: this build's save is v35 and its world carries the player's Civics Points account
            // (Track CP accrues every step), so the v35 block is flagged too — 0 rows on the v33 side, 2 on this build's (the
            // player's and the AI Empire's accounts). The first divergent field is unchanged (ConstructionHolds comes first).
            Assert.Equal(["ConstructionHolds", "CivicsPoints"], Flagged(stdout));
            Assert.Matches(@"^\s*ConstructionHolds\s+0\s+1\s+0\s+0\s+-\s+<--$", SummaryRow(stdout, "ConstructionHolds"));
            Assert.Matches(@"^\s*CivicsPoints\s+0\s+2\s+0\s+0\s+-\s+<--$", SummaryRow(stdout, "CivicsPoints"));

            string resave = Path.Combine(dir, "v33-resaved-as-v34.civsnap");
            WorldState loaded = SaveFiles.Load(v33, 256);
            using (FileStream f = File.Create(resave)) Snapshot.Save(loaded, f);
            (exit, stdout, stderr) = Sim("diff", v33, resave, "--size", "256");
            Assert.Equal(0, exit);
            Assert.Single(CliProcess.Lines(stderr));                                     // the v33 file's note, once
            Assert.StartsWith("identical: ", CliProcess.Lines(stdout)[2], StringComparison.Ordinal);
            Assert.Empty(Flagged(stdout));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ======================================================================== P7: a cancel naming a deferred colony id

    /// <summary>The determinism-xproc Found Settlement leg's first literal (FoundSettlementCiLegTests): turn 2, from
    /// settlement 0 to cell 349391 on the default founded world (seed 42) — it founds settlement 12, above the turn-0 maximum
    /// (11), in state 3.</summary>
    private static readonly OrderRecord FoundColony = new(2, 1, OrderKind.FoundSettlement, 0, 349391.0);
    private const int Colony = 12;
    private const int ColonyGranaries = 7;

    private static string ColonyOrders(string dir, bool found)
    {
        var log = new OrderLog();
        if (found) log.Append(FoundColony);
        // D-058 (2026-10-19) CAUSE BLOCK — was two granaries and a cancel of slot 1. Under the Director's "build one at a
        // time … 7 built, 1 under construction, 1 not started" ruling a step's capacity is no longer capped at one project:
        // MEASURED on this tree (in process, the CLI's founded world and content), the colony builds 5 granaries and a fifth
        // of a sixth in step 3 → 4, so with two both were built and world 4 held nothing to cancel (the batch-4 world 4 —
        // one built, slot 1 waiting — stands under TestConfigs.PreContinueWithinTurn). Seven granaries give world 4 the
        // Director's shape: 5 built, slot 5 part-built at 200,000, slot 6 not started — and slot 6 is cancelled.
        for (int i = 0; i < ColonyGranaries; i++) log.Append(new OrderRecord(3, 1, OrderKind.EnqueueConstruction, Colony, Granary));
        log.Append(new OrderRecord(4, 1, OrderKind.CancelConstructionEntry, Colony,
            OrderLog.PackCancelTarget(new CancelTarget(ColonyGranaries - 1, Granary, 3))));   // D-056: the last slot, ordered at 3
        string path = Path.Combine(dir, "colony-" + (found ? "found" : "forged") + ".bin");
        using (FileStream f = File.Create(path)) log.Save(f);
        return path;
    }

    /// <summary>
    /// P7 — A CANCEL TARGETING A DEFERRED COLONY ID, ACROSS PROCESSES (kind 11 until D-056; kind 13 since — a bare log
    /// carrying kind 11 is now refused by every CLI runner, LegacyCancelGuardTests). The log founds colony 12 at turn 2 (state 3), orders
    /// two granaries there at turn 3 and cancels the colony's slot 1 at turn 4 — every one of these orders names an id the
    /// turn-0 world does not hold, so `sim run`'s up-front validation defers it to delivery (ADR-034), where the colony
    /// exists. MEASURED (t4-out/probe-colony.txt): the colony's first granary is built in the step that enqueues it (the
    /// Empire purse is rich), so slot 1 waits at p = 0 in world 4. Saved at 4 and at 5 (two runs): world 4 holds slot 1 (key
    /// 3), world 5 does not — the cancel was delivered by step 4 → 5 — and the colony still has exactly the one granary. `sim replay` of the emitted run log reproduces the run's hash log in another process.
    /// With the founding order removed (the log a replay under other rules would meet if the colony were never founded) the
    /// run is REFUSED at delivery: exit 2 and the ADR-034 diagnostic naming the EnqueueConstruction and settlement 12.
    /// D-058 (2026-10-19): seven granaries now, the cancel naming the last; world 4 holds five built, slot 5 at 200,000 and
    /// slot 6 at p = 0; world 5 holds six built and no entry (the code's cause block).
    /// </summary>
    [Fact]
    public void ACancelTargetingADeferredColonyId_IsAcceptedByRunAndReplay_AndRemovesTheColonysEntryTurnExact_AndWithoutTheColonyIsRefused()
    {
        string dir = TempDir();
        try
        {
            string orders = ColonyOrders(dir, found: true), session = Path.Combine(dir, "session");
            string h = Path.Combine(dir, "h.txt"), h2 = Path.Combine(dir, "h2.txt");
            string s4 = Path.Combine(dir, "s4.civsnap"), s5 = Path.Combine(dir, "s5.civsnap");
            static string[] Cmd(string verb, params string[] rest) => [verb, "--founded", "--seed", "42", .. rest];
            (int exit, string stdout, string stderr) = Sim(Cmd("run", "--turns", "6", "--orders", orders, "--hash-log", h, "--save-at", "4", "--save", s4, "--emit-session", session));
            Assert.True(exit == 0, $"sim run exited {exit}: {stderr}");
            (exit, _, stderr) = Sim(Cmd("run", "--turns", "6", "--orders", orders, "--save-at", "5", "--save", s5));
            Assert.True(exit == 0, $"sim run exited {exit}: {stderr}");

            WorldState w4 = SaveFiles.Load(s4, -1), w5 = SaveFiles.Load(s5, -1);
            var entry = new ConstructionQueueRow(new SettlementId(Colony), ColonyGranaries - 1, Granary);
            var part = new ConstructionQueueRow(new SettlementId(Colony), ColonyGranaries - 2, Granary);
            Assert.True(w4.Settlements.Count > Colony && EmpireQuery.ControlsSettlement(w4, Player, new SettlementId(Colony)), "the colony stands at 4");
            Assert.Equal([part, entry], ConstructionQuery.Queue(w4, new SettlementId(Colony)));
            Assert.Equal((3L, 0L, 5L), (ConstructionQuery.EnqueuedTurnOf(w4.ConstructionProgress, entry), ConstructionQuery.ProgressOf(w4, entry),
                ConstructionQuery.Built(w4, new SettlementId(Colony), Granary)));
            Assert.Equal(200_000L, ConstructionQuery.ProgressOf(w4, part));     // under construction; the cancelled one not started
            Assert.Empty(ConstructionQuery.Queue(w5, new SettlementId(Colony)));
            Assert.Equal(-1, ConstructionQuery.ProgressIndex(w5.ConstructionProgress, entry));
            Assert.Equal(6L, ConstructionQuery.Built(w5, new SettlementId(Colony), Granary));   // the part-built one completed; the cancelled one never
            Assert.Equal(File.ReadAllLines(h)[4], WorldHash.ComputeHex(w5));

            string runLog = Assert.Single(Directory.GetFiles(session, "orders-*.bin"));
            (exit, _, stderr) = Sim(Cmd("replay", "--orders", runLog, "--turns", "6", "--hash-log", h2));
            Assert.True(exit == 0, $"sim replay exited {exit}: {stderr}");
            Assert.Equal(File.ReadAllBytes(h), File.ReadAllBytes(h2));

            (exit, stdout, stderr) = Sim(Cmd("run", "--turns", "6", "--orders", ColonyOrders(dir, found: false)));
            Assert.Equal(2, exit);
            Assert.Contains("EnqueueConstruction targets settlement 12, which does not exist when the order is delivered", stderr, StringComparison.Ordinal);
            Assert.Contains("(ADR-034)", stderr, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
