using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// D-055 (2026-10-16; schema v34, the batch-4 design X1 "Migration semantics" and "Fixtures") — THE v33 → v34 MIGRATION,
/// tested against REAL schema-v33 records written by the BATCH-3 build (base-b3 = de86ace + b3-final-v9, its Sim.Cli built
/// with -p:BuildSha=b3-final-v9-0f8f45c9 -p:BuildDate=2026-10-15; Fixtures/README-v33-fixture.md): the save
/// v33-seed42-turn2.civsnap and the session v33-session-q17/, both from ONE run of
/// `sim run --founded --seed 42 --size 256 --settlements 4 --ai-empires 1 --turns 6 --orders v33-fixture-orders.bin
/// --save-at 2 --save v33-seed42-turn2.civsnap --emit-session …`. The four order records give the player a 30/70 split,
/// granaries at s0 and s2 (turn 0), and take s0's builders away at turn 1 — the D-054 Q17 shape: s0's granary waits
/// part-built while s2's, younger in the building order, progresses.
///
/// ONLY ONE CLAIM IS EXACT (X1): IN THE SHIPPED PIPELINE ORDER (construction before roaddevelopment, pinned by
/// ConstructionHoldsSchemaV34Tests.TheShippedPipeline_RunsConstructionBeforeRoadDevelopment) section 61 is never read before
/// construction rebuilds it, and every comparator below steps with the shipped executor. (Until D-056 an order that ran road
/// development first read the table as loaded, so a migrated v33 world and a v34 save of the same state continued
/// differently; the executor now refuses that order — SystemCatalog.RoadDevelopment's ownership paragraph; pinned by
/// ConstructionReservationTests.TheV33Migration_IsExactInEveryPipelineThatComposes_TheRoadFirstOrderIsRefused.)
/// Therefore a migrated v33 world
/// equals the world a <see cref="TestConfigs.PreReservations"/> run reaches at the same turn K (PreReservations publishes
/// nothing), and stepped by the SHIPPED executor it continues exactly as that PreReservations world at K stepped by the
/// shipped executor. The comparators below are written that way, so they stay correct once the hold rule publishes holds
/// (the shipped executor then differs from PreReservations, but both arms step with the same executor from the same K).
///  (a) HashAtSchemaV33(loaded) == the v33 hash the batch-3 build recorded (`sim hash`, and the trace's turn-2 row);
///  (b) WorldHash(loaded) == WorldHash(the in-process PreReservations run at K), with the same orders;
///  (c) the loaded world stepped by the shipped executor == the PreReservations world at K stepped by the shipped
///      executor, turn by turn;
///  (d) re-saved as v34, reloaded and continued: the same.
///  (e) anti-vacuity: the shipped continuation (c) differs from the all-PreReservations continuation at a pinned turn W,
///      because a hold binds.
/// Premises (X1): (i) the session replayed under PreReservations reproduces every recorded trace hash through
/// SchemaProjection.V33; (ii) under the SHIPPED config the replay, projected to v33 (its hold rows stripped), first
/// differs from the trace at a pinned turn W, where s2's stone draw is lower by exactly s0's set-aside; (iv) `sim inspect
/// --manifest` (a real process) exits 0 and says so — D-057: it checks the trace in the v33 form, matching through turn
/// W − 1 and differing from W on (until D-057 it printed NotChecked(33): no trace check). Premise (iii) (the UI resume) is
/// Sim.Ui.Tests' V33SessionResumeTests.
/// </summary>
public class ConstructionHoldsV33FixtureTests
{
    /// <summary>The batch-3 build's hash of the save (`sim hash v33-seed42-turn2.civsnap --size 256`; README) — also the
    /// session trace's turn-2 row: the save is that run's turn-2 world.</summary>
    internal const string V33SaveHash = "705fb72b0c7d224f43d21b21783c4d35ebf72d648bcf7bea74ed3cc424484ce1";

    /// <summary>The session's stamp (its file names).</summary>
    internal const string Stamp = "f1cc70bce302a9a0";

    private const long K = 2;            // the save turn
    private const int LastTurn = 6;      // the session's last recorded turn
    private const int ContinueTo = 12;   // how far the continuations are compared

    private static readonly SettlementId S0 = new(0), S2 = new(2);

    /// <summary>D-055 step 3 (2026-10-16; MEASURED on this tree by the agent writing this line): the first turn at which a
    /// hold BINDS in the recorded run — K + 1, the first step after the save. Steps 0 → 1 and 1 → 2 publish holds (both
    /// granaries set timber aside) but no head is short, so worlds 1 and 2 equal the trace once their hold rows are
    /// stripped. In step 2 → 3 s0, part-built at 700,000 and without builders since the turn-1 order, sets aside its
    /// remaining Remaining(20, 700,000) = 6 stone, and s2, younger in the building order, draws 3 stone (p 0 → 150,000)
    /// where batch 3 drew 9 (0 → 450,000).</summary>
    private const long W = 3;
    private const long S0Progress = 700_000, S2ProgressAtW = 150_000, Batch3S2ProgressAtW = 450_000;
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);

    internal static string FixturePath() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "v33-seed42-turn2.civsnap");

    internal static byte[] FixtureFile() => File.ReadAllBytes(FixturePath());

    internal static string SessionDir() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "v33-session-q17");

    /// <summary>The fixture's terrain: the shipped CYLINDER at 256 px (its trailer bit 0 = 1), as Sim.Cli.SaveFiles
    /// regenerates a v33 save at `--size 256`.</summary>
    private static TerrainSet Terrain() => Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen() with { SizePx = 256 }, 42);

    private static WorldState LoadFixture() => Snapshot.Load(new MemoryStream(FixtureFile()), Terrain());

    private static OrderLog SessionOrders()
    {
        using FileStream f = File.OpenRead(Path.Combine(SessionDir(), "orders-" + Stamp + ".bin"));
        return OrderLog.Load(f);
    }

    /// <summary>The recorded run's world at turn 0 under <paramref name="cfg"/>: `--founded --seed 42 --size 256
    /// --settlements 4 --ai-empires 1` (Sim.Cli.HeadlessFounding with that config).</summary>
    private static WorldState Found(SimConfig cfg) =>
        WorldFounding.Found(TestConfigs.Worldgen() with { SizePx = 256, AiEmpires = 1 }, cfg, 42, 4);

    /// <summary>The CLI run's executor under <paramref name="cfg"/>: the production pipeline with the SHIPPED worldgen (as
    /// `sim run --founded` builds it, whatever --size), delivering <paramref name="orders"/> — the session's run log, which
    /// already holds the AI's orders, so no AI is appended live.</summary>
    private static TurnExecutor Executor(SimConfig cfg, OrderLog orders)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.Worldgen())), orders);
    }

    private static string[] TraceHashes()
    {
        string[] lines = File.ReadAllLines(Path.Combine(SessionDir(), "trace-" + Stamp + ".csv"));
        Assert.Equal("turn,year,population,settlements,food,hash", lines[0]);
        var hashes = new string[lines.Length - 1];
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cells = lines[i].Split(',');
            Assert.Equal((i - 1).ToString(System.Globalization.CultureInfo.InvariantCulture), cells[0]);
            hashes[i - 1] = cells[^1];
        }
        return hashes;
    }

    [Fact]
    public void TheFixtureOrderLogs_AreTheirDocumentedRecords()
    {
        using (FileStream f = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "v33-fixture-orders.bin")))
        {
            OrderLog q = OrderLog.Load(f);
            Assert.Equal(4, q.Count);
            Assert.Equal(new OrderRecord(0, 1, OrderKind.SetResourceAllocation, 1, 30.0), q[0]);
            Assert.Equal(new OrderRecord(0, 1, OrderKind.EnqueueConstruction, 0, 1.0), q[1]);
            Assert.Equal(new OrderRecord(0, 1, OrderKind.EnqueueConstruction, 2, 1.0), q[2]);
            Assert.Equal(new OrderRecord(1, 1, OrderKind.SectorAllocation, (0 << 3) | 4, 0.0), q[3]);
        }
        // The session's run log: the four player records and the AI Empire's own order (its research target at turn 0).
        OrderLog run = SessionOrders();
        Assert.Equal(5, run.Count);
        Assert.Equal(new OrderRecord(0, 2, OrderKind.SetResearchTarget, 2, 0.0), run[3]);
        Assert.Equal(new OrderRecord(1, 1, OrderKind.SectorAllocation, (0 << 3) | 4, 0.0), run[4]);
    }

    /// <summary>The fixture is what X1 says it is: a genuine v33 file (header 33, the topology trailer bit 0 = 1), whose
    /// turn-2 world holds the Q17 shape — s0's granary part-built and s2's at p = 0, both ordered at turn 0 — and the
    /// player's 30/70 split; it loads under v34 with ConstructionHolds empty.</summary>
    [Fact]
    public void TheV33Fixture_IsAGenuineV33Save_HoldingTheQ17Shape_AndLoadsWithConstructionHoldsEmpty()
    {
        byte[] file = FixtureFile();
        Assert.Equal(33, BitConverter.ToInt32(file, 8));
        Assert.Equal(TopologyFlags.WrapX, BitConverter.ToInt32(file, file.Length - 4));
        WorldState w = LoadFixture();
        Assert.True(w.Terrain!.WrapX);
        Assert.Equal(K, w.Clock.Turn);
        Assert.Equal(0, w.ConstructionHolds.Count);
        ConstructionQueueRow s0 = Assert.Single(ConstructionQuery.Queue(w, S0));
        ConstructionQueueRow s2 = Assert.Single(ConstructionQuery.Queue(w, S2));
        Assert.Equal((1, 1), (s0.ProjectId, s2.ProjectId));
        Assert.Equal((0L, 0L), (ConstructionQuery.EnqueuedTurnOf(w.ConstructionProgress, s0), ConstructionQuery.EnqueuedTurnOf(w.ConstructionProgress, s2)));
        Assert.Equal((700_000L, 0L), (ConstructionQuery.ProgressOf(w, s0), ConstructionQuery.ProgressOf(w, s2)));
        Assert.Equal(1, w.ResourceAllocations.Count);
        Assert.Equal(new ResourceAllocationRow(new PolityId(1), 30), w.ResourceAllocations[0]);
    }

    /// <summary>(a) The loaded world's v33 projection is the hash the batch-3 build recorded for the save — both its
    /// `sim hash` and its trace's turn-2 row.</summary>
    [Fact]
    public void MigrationA_TheLoadedWorldsV33Projection_IsTheHashTheBatch3BuildRecorded()
    {
        WorldState loaded = LoadFixture();
        Assert.Equal(V33SaveHash, SchemaProjection.V33(loaded));
        Assert.Equal(V33SaveHash, TraceHashes()[K]);
        Assert.NotEqual(V33SaveHash, WorldHash.ComputeHex(loaded));        // the v34 hash: one empty count prefix more
    }

    /// <summary>(b) The loaded world IS the world an in-process PreReservations run of the same orders reaches at K —
    /// every table, the empty ConstructionHolds included (WorldHash covers the whole v34 stream).</summary>
    [Fact]
    public void MigrationB_TheLoadedWorld_IsThePreReservationsRunsWorldAtK()
    {
        SimConfig off = TestConfigs.PreReservations(TestConfigs.Sim());
        WorldState run = Executor(off, SessionOrders()).Run(Found(off), (int)K);
        WorldState loaded = LoadFixture();
        Assert.True(WorldStates.StateEquals(run, loaded), "the migrated world differs from the PreReservations run at K");
        Assert.Equal(WorldHash.ComputeHex(run), WorldHash.ComputeHex(loaded));
    }

    /// <summary>(c) and (d): the loaded world, and the loaded world re-saved as v34 and reloaded, stepped by the SHIPPED
    /// executor, equal the PreReservations run's world at K stepped by the SHIPPED executor — every turn to
    /// <see cref="ContinueTo"/>. The v34 re-save is the v33 file with the header version 34 and one empty count prefix
    /// inserted before the trailer, byte for byte.</summary>
    [Fact]
    public void MigrationCD_TheLoadedAndTheResavedWorlds_ContinueAsThePreReservationsWorldAtK_UnderTheShippedExecutor()
    {
        SimConfig shipped = TestConfigs.PreDiscovery(TestConfigs.PrePerTurnCivics(TestConfigs.Sim())), off = TestConfigs.PreReservations(shipped);
        OrderLog orders = SessionOrders();
        WorldState atK = Executor(off, orders).Run(Found(off), (int)K);

        byte[] v33 = FixtureFile();
        WorldState loaded = Snapshot.Load(new MemoryStream(v33), Terrain());
        byte[] v34;
        using (var ms = new MemoryStream())
        {
            Snapshot.Save(loaded, ms);
            v34 = ms.ToArray();
        }
        // (d) the re-save's layout: header 34, the v33 stream, ONE empty section-61 prefix, the same trailer.
        Assert.Equal(CanonicalSchema.Version, BitConverter.ToInt32(v34, 8));
        // ADR-043 (v35, 2026-10-22) CAUSE BLOCK: this build saves v35 — TWO empty prefixes (sections 61 and 62) inserted
        // before the trailer (was one). The layout alone.
        // M6 merge (2026-10-22): and the four EMPTY M6 EX discovery tables (sections 63–66) — SIX empty prefixes in all.
        // ADR-049 (v36): and the EMPTY ResearchLevels (section 67) — SEVEN empty prefixes in all.
        // ADR-051 (v37): and the EMPTY ResearchReserves (section 68) — EIGHT empty prefixes in all.
        // ADR-054 (v38): and the EMPTY ResearchPopulations (section 69) — NINE empty prefixes in all.
        Assert.Equal(v33.Length + 52, v34.Length);   // ADR-055 (v39): + sections 70–73
        byte[] expected = [.. v33[..8], .. BitConverter.GetBytes(CanonicalSchema.Version), .. v33[12..^4], .. new byte[52], .. v33[^4..]];
        Assert.Equal(expected, v34);
        WorldState reloaded = Snapshot.Load(new MemoryStream(v34), Terrain());

        TurnExecutor exec = Executor(shipped, orders);
        WorldState a = atK, b = loaded, c = reloaded;
        for (long t = K + 1; t <= ContinueTo; t++)
        {
            a = exec.Step(a);
            b = exec.Step(b);
            c = exec.Step(c);
            string h = WorldHash.ComputeHex(a);
            Assert.True(h == WorldHash.ComputeHex(b), $"(c) the loaded continuation differs at turn {t}");
            Assert.True(h == WorldHash.ComputeHex(c), $"(d) the re-saved continuation differs at turn {t}");
        }
    }

    /// <summary>Premise (i): the session replayed under PreReservations reproduces EVERY hash its trace recorded (turns 0
    /// to 6), through SchemaProjection.V33 — the batch-3 build's world, byte for byte, plus the v34 layout.</summary>
    [Fact]
    public void PremiseI_TheSessionReplayedUnderPreReservations_ReproducesEveryTraceHash_ThroughTheV33Projection()
    {
        SimConfig off = TestConfigs.PreReservations(TestConfigs.Sim());
        string[] trace = TraceHashes();
        Assert.Equal(LastTurn + 1, trace.Length);
        TurnExecutor exec = Executor(off, SessionOrders());
        WorldState w = Found(off);
        Assert.True(w.Terrain!.WrapX);
        Assert.Equal(trace[0], SchemaProjection.V33(w));
        for (int t = 1; t <= LastTurn; t++)
        {
            w = exec.Step(w);
            Assert.True(trace[t] == SchemaProjection.V33(w), $"the PreReservations replay differs from the batch-3 trace at turn {t}");
        }
    }

    private static long Stone => TestConfigs.Sim().Goods!.IdOf("stone");

    /// <summary>The units of stone a granary (20 stone) at progress <paramref name="p"/> has had delivered (the lockstep).</summary>
    private static long StoneIn(long p) => 20 - ConstructionQuery.Remaining(20, p);

    private static long ProgressAt(IReadOnlyWorldState w, SettlementId s)
    {
        ConstructionQueueRow[] q = ConstructionQuery.Queue(w, s);
        return q.Length == 0 ? 0 : ConstructionQuery.ProgressOf(w, q[0]);
    }

    /// <summary>
    /// Premise (ii) (D-055 step 3, 2026-10-16): under the SHIPPED config, the session's replay — projected to v33 with its
    /// hold rows stripped (SchemaProjection.HashAtSchemaV33) — equals the batch-3 trace at turns 0 to W − 1 and first
    /// differs at the pinned turn W = 3, where s2's stone draw is lower by exactly s0's published set-aside: s0 sets aside
    /// its remaining 6 stone, s2 draws 3, and 3 + 6 = 9 is what batch 3 drew. Holds are published from world 1 on, so the
    /// equality at turns 1 and 2 also shows that a hold that binds nothing changes nothing but its own table.
    /// </summary>
    [Fact]
    public void PremiseII_TheShippedReplay_FirstDiffersFromTheTraceAtW_WhereS2DrawsLessStoneByExactlySzerosSetAside()
    {
        SimConfig shipped = TestConfigs.PreDiscovery(TestConfigs.PrePerTurnCivics(TestConfigs.Sim()));   // M6 EX; ADR-054 off by the R4 rule: the D-055 shipped world (discovery off, R4)
        string[] trace = TraceHashes();
        TurnExecutor exec = Executor(shipped, SessionOrders());
        WorldState w = Found(shipped), before = w;
        int held = 0;
        for (long t = 0; t < W; t++)
        {
            if (t > 0) w = exec.Step(w);
            Assert.True(trace[t] == SchemaProjection.HashAtSchemaV33(w, out int removed), $"the shipped replay differs from the trace at turn {t}, before W");
            held += removed;
            before = w;
        }
        Assert.True(held > 0, "anti-vacuity: the shipped replay publishes holds before W (they bind nothing yet)");
        WorldState atW = exec.Step(before);
        Assert.NotEqual(trace[W], SchemaProjection.HashAtSchemaV33(atW, out _));

        // The difference is the hold binding: s0 waits part-built and sets aside its remaining stone; s2 draws less by
        // exactly that amount. The batch-3 figures are the PreReservations replay's (premise (i): it IS the trace).
        WorldState pre = Executor(TestConfigs.PreReservations(shipped), SessionOrders()).Run(Found(TestConfigs.PreReservations(shipped)), (int)W);
        Assert.Equal(trace[W], SchemaProjection.V33(pre));
        Assert.Equal((S0Progress, S0Progress), (ProgressAt(before, S0), ProgressAt(atW, S0)));
        Assert.Equal((S2ProgressAtW, Batch3S2ProgressAtW), (ProgressAt(atW, S2), ProgressAt(pre, S2)));
        long setAside = 0;
        for (int i = 0; i < atW.ConstructionHolds.Count; i++)
            if (atW.ConstructionHolds[i].Purse == Reservations.PolityPurse(new PolityId(1)) && atW.ConstructionHolds[i].Good == Stone)
                setAside += atW.ConstructionHolds[i].Units;
        Assert.Equal(ConstructionQuery.Remaining(20, S0Progress), setAside);
        Assert.Equal((3L, 9L), (StoneIn(S2ProgressAtW), StoneIn(Batch3S2ProgressAtW)));
        Assert.Equal(StoneIn(Batch3S2ProgressAtW), StoneIn(S2ProgressAtW) + setAside);
    }

    /// <summary>
    /// (e) ANTI-VACUITY (D-055 step 3, 2026-10-16): the comparators (c) and (d) prove the loaded world continues as the
    /// PreReservations world at K does under the SHIPPED executor — which would hold vacuously if no hold ever bound after
    /// K. So: the loaded world stepped by the shipped executor and the same world stepped by the all-PreReservations
    /// executor (whose turns K + 1 to 6 are the batch-3 trace's) first differ at the pinned turn W = K + 1 = 3, and differ
    /// there in more than the hold table (the holds-stripped v33 projections differ), because s2's stone draw is lower by
    /// exactly s0's set-aside.
    /// </summary>
    [Fact]
    public void MigrationE_TheShippedContinuation_DiffersFromTheAllPreReservationsContinuationAtW_BecauseAHoldBinds()
    {
        SimConfig shipped = TestConfigs.PreDiscovery(TestConfigs.PrePerTurnCivics(TestConfigs.Sim())), off = TestConfigs.PreReservations(shipped);
        OrderLog orders = SessionOrders();
        string[] trace = TraceHashes();
        WorldState loaded = LoadFixture();
        Assert.Equal(K + 1, W);
        WorldState c = Executor(shipped, orders).Step(loaded);
        TurnExecutor offExec = Executor(off, orders);
        WorldState allOff = offExec.Step(loaded);
        Assert.Equal(trace[W], SchemaProjection.V33(allOff));                      // the all-off arm is batch 3's record
        Assert.NotEqual(SchemaProjection.HashAtSchemaV33(allOff, out _), SchemaProjection.HashAtSchemaV33(c, out int rows));
        Assert.True(rows > 0, "the shipped continuation publishes the set-aside");
        Assert.Equal((S2ProgressAtW, Batch3S2ProgressAtW), (ProgressAt(c, S2), ProgressAt(allOff, S2)));
        Assert.Equal((S0Progress, S0Progress), (ProgressAt(c, S0), ProgressAt(allOff, S0)));
        long setAside = 0;
        for (int i = 0; i < c.ConstructionHolds.Count; i++)
            if (c.ConstructionHolds[i].Good == Stone) setAside += c.ConstructionHolds[i].Units;
        Assert.Equal(StoneIn(Batch3S2ProgressAtW) - StoneIn(S2ProgressAtW), setAside);
        // ... and the all-off arm goes on being the trace to its last recorded turn.
        for (long t = W + 1; t <= LastTurn; t++)
        {
            allOff = offExec.Step(allOff);
            Assert.True(trace[t] == SchemaProjection.V33(allOff), $"the all-PreReservations continuation differs from the trace at turn {t}");
        }
    }

    /// <summary>Premise (iv): `sim inspect --manifest` of the v33 session (a real process) exits 0 and — D-057 — checks the
    /// trace in the v33 form: the replay matches the batch-3 build's rows through turn W − 1 and differs from W on, premise
    /// (ii)'s turn, found here by the CLI from the recorded hashes alone. This schema's verdicts (a reproduction, a failure)
    /// are not printed. (Until D-057 the line was NotChecked(33), "no replayed hash can equal a recorded one" — no longer
    /// true once the build hashes in the v33 form.)</summary>
    [Fact]
    public void PremiseIV_SimInspect_OfTheV33Session_ChecksItInTheV33Form_MatchingThroughWMinus1_AndExitsZero()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-v33inspect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (string f in Directory.GetFiles(SessionDir())) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
            string manifest = Path.Combine(dir, "session-" + Stamp + ".json");
            (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "inspect", "--manifest", manifest);
            Assert.Equal((0, ""), (exit, stderr));
            // ADR-054 (2026-10-27) RE-PIN: the CLI replays under the SHIPPED content, where the AI's Research Points read the
            // average population from each holder's second step (tuning.populationAverageTurns 2) — so the replay now first
            // differs at turn 2, one turn before W (premise (ii)'s reservation turn, which premise (ii) still pins on the
            // ADR-054-off world). The check is still made in the v33 form; only the first divergent turn moved.
            Assert.Contains(SessionSchemaNotice.CheckedInRecordedForm(33, W - 2, W - 1, LastTurn), CliProcess.Lines(stdout));
            Assert.Contains("the replay matches the recorded game through turn 1 and differs from turn 2 on", stdout, StringComparison.Ordinal);
            Assert.DoesNotContain(SessionSchemaNotice.NotChecked(33), stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("reproduction VERIFIED", stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("REPRODUCTION FAILED", stdout, StringComparison.Ordinal);
            // The recipe is the recording build's, named by its stamped identity (the fixture's manifest).
            Assert.Contains("  reproduce with: the build that recorded the session (b3-final-v9-0f8f45c9, 2026-10-15): "
                + "sim replay --founded --seed 42 --size 256 --settlements 4 --ai-empires 1 --orders orders-" + Stamp
                + ".bin --turns 6", CliProcess.Lines(stdout));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>X1 (g) — the v33 migration note: `sim hash` of the REAL v33 save prints the v34 hash alone on stdout and
    /// the note once on stderr; the note is the X1 text.</summary>
    [Fact]
    public void SimHash_OfTheV33Save_PrintsTheV34HashOnStdout_AndTheV33MigrationNoteOnStderr()
    {
        string fixture = FixturePath();
        WorldState loaded = SaveFiles.Load(fixture, 256, out string? note);
        // ADR-043 + ADR-044 (2026-10-22) RE-PIN of the note: v35 adds CivicsPoints and the four discovery tables empty.
        Assert.Equal("note: " + fixture + ": migrated from save schema v33: ConstructionHolds, CivicsPoints and the four M6 discovery tables (KnownExtents, Explorations, SettlementSightings, SettlementSurveys) were added empty; "
            + "the hash printed is the v39 hash (SchemaProjection.V33 gives the v33 build's). ResearchLevels (no repeatable research level existed before v36) was added empty too. ResearchReserves (no research reserve existed before v37) was added empty too. ResearchPopulations (no stored turn-start population existed before v38; each holder's first step reads its current population) was added empty too."
            + " The four military tables (FormationPersonnel, Reequips, ReequipEscrow, ReequipLog) were added empty (every saved formation stays a token that holds no one) and any formation of a rehomed identity was moved to its new family (ADR-055).", note);   // ADR-049 (v36), ADR-051 (v37), ADR-054 (v38), ADR-055 (v39) re-pins
        Assert.Equal(V33SaveHash, SchemaProjection.V33(loaded));       // what the note claims

        (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "hash", fixture, "--size", "256");
        Assert.Equal(0, exit);
        Assert.Equal(new[] { WorldHash.ComputeHex(loaded) }, CliProcess.Lines(stdout));
        Assert.Equal(new[] { note }, CliProcess.Lines(stderr));
    }
}
