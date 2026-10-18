using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Construction;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.Systems;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 4 — "Review partial construction, reservation state,
/// allocation settings, cancellation and re-enqueueing across save/load, schema-v32 migration, resumed UI sessions and
/// deterministic replay. Add or correct tests for any uncovered cases."; batch-4 design §4 P1; recon persistence-audit §7 P1)
/// — THE CONSTRUCTION LIFECYCLE ACROSS A SAVE. Each case builds a lifecycle state by the step and the orders, saves it at a
/// turn K, and runs THREE CONTINUATIONS with the same log and the same per-turn rig changes: the world never saved (a
/// clone), <see cref="Snapshot.Load"/> of its save, and <see cref="SaveFiles.Load(string, long)"/> — the CLI's loader, which
/// regenerates the terrain from the header (the rigs are founded on the shipped worldgen at 256 px, so `--size 256` is the
/// size). Every case asserts the SAME exact figures on the three, and the three hash-equal on every turn.
///
/// The cases (recon persistence-audit's rows): L1 a part-built head WAITING for materials (R1a); L2 waiting for builders
/// (R2); L3 the four structural stalls, the AI's cancel arriving turn-exact after the load (R3); L4 a HEAD cancel with
/// p &gt; 0 issued before the save, delivered after it (R4, kills M-B on the save path); L5 a re-enqueue (R5); L6 a
/// completion next to the save (R6); L7 a BINDING building/roads split at b = 0, 50 and an explicit 100 (R7); L8 a split
/// changed before delivery (R8, kills M-A on the save path); L9 a BINDING hold mid-wait, released turn-exact by a cancel
/// saved between issue and delivery; L10 the TAMPER test — a v34 save whose ConstructionHolds rows were altered or dropped
/// (still well-formed) loads, its world at K differs, and the next world equals the untampered continuation's: the table
/// is never an input to construction (kills "the walk seeded from PREV.ConstructionHolds"). L10's rigs run construction
/// only; in the shipped pipeline order road development also reads only this step's rebuild, and another order reads the
/// table as it stands (ADR-040, D-055 §11; pinned by ConstructionReservationTests.TheEdgeOrderIsData_…).
///
/// EVERY HOLD ASSERTION HAS A BINDING PREMISE (design §4, review L4-16): the same continuation under
/// TestConfigs.PreReservations differs, and the younger head's exact draw is pinned on the loaded world. Progress values
/// that matter are not multiples of 1,000 (the D-054 §8 quantising-mutant lesson) where the shipped content allows.
/// Rigs: IncrementalConstructionTests' (construction only, the founded dev world, stocks set exactly), plus a restated
/// copy of ResourceAllocationTests' road rig for L7/L8 (that class's rig is private; restating it keeps this track's diff
/// in a new file).
/// </summary>
public class ConstructionLifecyclePersistenceTests
{
    /// <summary>A second test-only project (timber 10, stone 7, labour 1): small odd costs, so a younger head's progress is
    /// never a multiple of 1,000 (floor(k × N / 7)).</summary>
    internal const int Hut = 91;
    private const long CliSizePx = 256;
    private static readonly int PlayerPurse = Reservations.PolityPurse(Player);

    // ================================================================== the three continuations

    /// <summary>The save of <paramref name="w"/> (Snapshot.Save: header, canonical stream, topology trailer).</summary>
    internal static byte[] SaveBytes(WorldState w)
    {
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        return ms.ToArray();
    }

    /// <summary><see cref="Snapshot.Load"/> of <paramref name="bytes"/> against <paramref name="terrain"/>.</summary>
    internal static WorldState ViaSnapshot(byte[] bytes, TerrainSet? terrain) => Snapshot.Load(new MemoryStream(bytes), terrain);

    /// <summary>The CLI's loader (<see cref="SaveFiles.Load(string, long, out string?)"/>, `sim hash --size 256`): the bytes
    /// written to a file, the terrain regenerated from the header and the topology trailer. A v34 save prints no migration
    /// note.</summary>
    internal static WorldState ViaSaveFiles(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), "civsim-lifecycle-" + Guid.NewGuid().ToString("N") + ".civsnap");
        try
        {
            File.WriteAllBytes(path, bytes);
            WorldState w = SaveFiles.Load(path, CliSizePx, out string? note);
            Assert.Null(note);
            return w;
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The three continuations' starting worlds for a save of <paramref name="atK"/>: never saved (a clone — the
    /// rig changes then never touch <paramref name="atK"/>), Snapshot.Load, SaveFiles.Load. Premise: all three are the same
    /// world at K (every table, and the hash).</summary>
    internal static (string Arm, WorldState World)[] Arms(WorldState atK) => ArmsOf(SaveBytes(atK), atK.Clone());

    /// <summary>The three continuations of the save <paramref name="bytes"/> whose in-memory twin is <paramref name="never"/>.</summary>
    internal static (string Arm, WorldState World)[] ArmsOf(byte[] bytes, WorldState never)
    {
        (string, WorldState)[] arms =
        [
            ("never saved", never),
            ("Snapshot.Load", ViaSnapshot(bytes, never.Terrain)),
            ("SaveFiles.Load", ViaSaveFiles(bytes)),
        ];
        string h = WorldHash.ComputeHex(never);
        for (int a = 1; a < arms.Length; a++)
        {
            Assert.True(WorldStates.StateEquals(never, arms[a].Item2), $"premise: the {arms[a].Item1} world differs from the saved one");
            Assert.Equal(h, WorldHash.ComputeHex(arms[a].Item2));
        }
        return arms;
    }

    /// <summary>A copy of <paramref name="log"/> (each continuation appends its own AI orders).</summary>
    internal static OrderLog Copy(OrderLog log)
    {
        var c = new OrderLog();
        for (int i = 0; i < log.Count; i++) c.Append(log[i]);
        return c;
    }

    /// <summary>Steps <paramref name="start"/> <paramref name="steps"/> times; before each step <paramref name="before"/>
    /// (the per-turn rig change, keyed by the turn about to be stepped) is applied to a CLONE of the world, so element k of
    /// the result is exactly the world the step k produced.</summary>
    internal static WorldState[] Continue(WorldState start, TurnExecutor exec, int steps, Action<WorldState>? before = null)
    {
        var worlds = new WorldState[steps + 1];
        worlds[0] = start;
        for (int k = 1; k <= steps; k++)
        {
            WorldState prev = worlds[k - 1];
            if (before is not null)
            {
                prev = prev.Clone();
                before(prev);
            }
            worlds[k] = exec.Step(prev);
        }
        return worlds;
    }

    /// <summary>
    /// Runs the three continuations of <paramref name="atK"/> (or of <paramref name="arms"/>), each with its own executor
    /// over its own copy of <paramref name="log"/>, and hands each arm's worlds to <paramref name="check"/>; then asserts the
    /// three are hash-identical on every turn. Returns the never-saved arm's worlds.
    /// </summary>
    internal static WorldState[] ThreeWays(
        (string Arm, WorldState World)[] arms, Func<OrderLog, TurnExecutor> executor, OrderLog log, int steps,
        Action<WorldState>? before, Action<string, WorldState[]> check)
    {
        var all = new WorldState[arms.Length][];
        for (int a = 0; a < arms.Length; a++)
        {
            all[a] = Continue(arms[a].World, executor(Copy(log)), steps, before);
            check(arms[a].Arm, all[a]);
        }
        for (int k = 0; k <= steps; k++)
        {
            string h = WorldHash.ComputeHex(all[0][k]);
            for (int a = 1; a < arms.Length; a++)
                Assert.True(h == WorldHash.ComputeHex(all[a][k]), $"the {arms[a].Arm} continuation differs from the never-saved one {k} step(s) after the save");
        }
        return all[0];
    }

    private static void Eq<T>(T expected, T actual, string arm, string what) =>
        Assert.True(EqualityComparer<T>.Default.Equals(expected, actual), $"{arm}: {what} — expected {expected}, got {actual}");

    private static HoldsWalkHead WalkHead(IReadOnlyWorldState w, SimConfig cfg, SettlementId s, double dt = 10.0)
    {
        HoldsWalkResult walk = ConstructionQuery.HoldsWalk(w, cfg, w.ConstructionQueue, w.ConstructionProgress, w.GoodStocks, dt, []);
        return walk.HeadOf(s) ?? throw new InvalidOperationException("no head walked at settlement " + s.Value);
    }

    private static long HeldOf(IReadOnlyWorldState w, int purse, int good)
    {
        long sum = 0;
        for (int i = 0; i < w.ConstructionHolds.Count; i++)
            if (w.ConstructionHolds[i].Purse == purse && w.ConstructionHolds[i].Good == good) sum += w.ConstructionHolds[i].Units;
        return sum;
    }

    // ================================================================== L1 (R1a) and L6 (R6): waiting for materials; completion

    /// <summary>The waiting rig: S0's works (timber 30, stone 7, labour 1 adult-year), all builders, 100 timber and 3
    /// stone: step 0 → 1 reaches floor(3 × N / 7) = 428,571 (3 stone and ceil(30 × 0.428571) = 13 timber in); step 1 → 2
    /// has no stone, so the head WAITS (Δp = 0) at state 2.</summary>
    private static (WorldState T2, TurnExecutor Exec, SimConfig Cfg, OrderLog Log) Waiting()
    {
        SimConfig cfg = WithWorks(Cfg, 1.0, Works, timber: 30, stone: 7);
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 3);
        OrderLog log = Log(Enqueue(0, Player, S0, Works));
        TurnExecutor exec = ConstructionOnly(cfg, log);
        WorldState t1 = exec.Step(w);
        Assert.Equal(428_571, P(t1, S0));
        Assert.Equal((87L, 0L), (Of(t1, S0, Timber), Of(t1, S0, Stone)));
        WorldState t2 = exec.Step(t1);
        Assert.Equal(428_571, P(t2, S0));                                     // premise: it waited in step 1 → 2
        Assert.Equal(0, t2.ConstructionLabor.Count);
        return (t2, exec, cfg, log);
    }

    /// <summary>
    /// L1 (R1a) — A PART-BUILT HEAD WAITING FOR MATERIALS, saved mid-wait (state 2). After the load, step 2 → 3 keeps
    /// p = 428,571, draws nothing (stocks and the Ledger's construction sinks unchanged) and publishes no labour; with
    /// 4 stone set at state 3 it completes in step 3 → 4 with exactly Remaining(7, 428,571) = 4 stone and
    /// Remaining(30, 428,571) = 17 timber, labour 1 × (N − 428,571) / N, built once — on all three continuations.
    /// </summary>
    [Fact]
    public void L1_APartBuiltHeadWaitingForMaterials_SavedMidWait_KeepsWaiting_ThenCompletesWithExactlyTheRemainder()
    {
        (WorldState t2, _, SimConfig cfg, OrderLog log) = Waiting();
        long sunkT = Sunk(t2, Timber), sunkS = Sunk(t2, Stone);
        ThreeWays(Arms(t2), l => ConstructionOnly(cfg, l), log, 2, w => { if (w.Clock.Turn == 3) Set(w, S0, Stone, 4); }, (arm, t) =>
        {
            Eq(428_571L, P(t[1], S0), arm, "p after the waiting step");
            Eq((87L, 0L), (Of(t[1], S0, Timber), Of(t[1], S0, Stone)), arm, "stocks after the waiting step");
            Eq((sunkT, sunkS), (Sunk(t[1], Timber), Sunk(t[1], Stone)), arm, "construction sinks after the waiting step");
            Eq(0, t[1].ConstructionLabor.Count, arm, "labour published by the waiting step");
            Eq(0L, Built(t[1], S0, Works), arm, "built while waiting");
            Eq(1L, Built(t[2], S0, Works), arm, "built in step 3 → 4");
            Eq((0, 0), (t[2].ConstructionQueue.Count, t[2].ConstructionProgress.Count), arm, "rows after completion");
            Eq((4L, 17L), (ConstructionQuery.Remaining(7, 428_571), ConstructionQuery.Remaining(30, 428_571)), arm, "the remainder");
            Eq((sunkT + 17, sunkS + 4), (Sunk(t[2], Timber), Sunk(t[2], Stone)), arm, "drawn by the completing step");
            Eq((70L, 0L), (Of(t[2], S0, Timber), Of(t[2], S0, Stone)), arm, "stocks after completion");
            Eq(1.0 * (N - 428_571) / N, Labour(t[2], S0), arm, "labour of the last piece");
            AssertConserved(t[2]);
        });
    }

    /// <summary>
    /// L6 (R6) — A COMPLETION NEXT TO THE SAVE. (b) Saved at state 3 (the waiting head, 4 stone set): after the load the
    /// head completes in exactly the next step, drawing Remaining(c, p) of each input. (a) Saved at the state the
    /// completing step produced (state 4): built = 1 and both rows gone; three more steps draw nothing, publish no
    /// labour and never count it again.
    /// </summary>
    [Fact]
    public void L6_ACompletionNextToTheSave_CompletesInTheNextStepExactlyOnce_AndASaveAfterItCountsItNeverAgain()
    {
        (WorldState t2, TurnExecutor exec, SimConfig cfg, OrderLog log) = Waiting();
        WorldState t3 = exec.Step(t2);
        Set(t3, S0, Stone, 4);
        long sunkT = Sunk(t3, Timber), sunkS = Sunk(t3, Stone);
        // (b) saved at the state before the completing step.
        ThreeWays(Arms(t3), l => ConstructionOnly(cfg, l), log, 1, null, (arm, t) =>
        {
            Eq(0L, Built(t[0], S0, Works), arm, "built at the save");
            Eq(1L, Built(t[1], S0, Works), arm, "built one step after the load");
            Eq((sunkT + 17, sunkS + 4), (Sunk(t[1], Timber), Sunk(t[1], Stone)), arm, "Remaining(c, p) drawn");
            Eq(1.0 * (N - 428_571) / N, Labour(t[1], S0), arm, "labour L × (N − p) / N");
        });
        // (a) saved at the state the completing step produced.
        WorldState t4 = exec.Step(t3);
        Assert.Equal(1, Built(t4, S0, Works));
        ThreeWays(Arms(t4), l => ConstructionOnly(cfg, l), log, 3, null, (arm, t) =>
        {
            for (int k = 0; k <= 3; k++)
            {
                Eq(1L, Built(t[k], S0, Works), arm, $"built {k} step(s) after the save");
                Eq((0, 0), (t[k].ConstructionQueue.Count, t[k].ConstructionProgress.Count), arm, "rows after completion");
                Eq((70L, 0L), (Of(t[k], S0, Timber), Of(t[k], S0, Stone)), arm, "nothing drawn after completion");
                Eq((sunkT + 17, sunkS + 4), (Sunk(t[k], Timber), Sunk(t[k], Stone)), arm, "no construction sink after completion");
                if (k > 0) Eq(0, t[k].ConstructionLabor.Count, arm, "no labour after completion");
            }
        });
    }

    // ================================================================== L2 (R2): waiting for builders

    /// <summary>
    /// L2 (R2) — A PART-BUILT HEAD WAITING FOR BUILDERS. S0's works (timber 30, stone 7) at a labour pace of exactly 1/3
    /// (L = 3 C): 333,333 at state 1; its construction share is then set to 0 (labour adults present — a valid wait), so it
    /// waits at state 2, where it is saved. After the load: p kept, nothing drawn, the resolver says Labour with labour
    /// adults present (not structural); with every builder restored at state 3 it resumes at the exact pace — 666,666,
    /// 999,999 — and completes in step 5 → 6.
    /// </summary>
    [Fact]
    public void L2_APartBuiltHeadWaitingForBuilders_SavedMidWait_ResumesAtTheExactPace_AndCompletesOnTheStatedTurn()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        SimConfig cfg = WithWorks(Cfg, 3.0 * C(w, S0, 10.0), Works, timber: 30, stone: 7);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 100);
        OrderLog log = Log(Enqueue(0, Player, S0, Works));
        TurnExecutor exec = ConstructionOnly(cfg, log);
        WorldState t1 = exec.Step(w);
        Assert.Equal(333_333, P(t1, S0));
        NoBuilders(t1, S0);
        WorldState t2 = exec.Step(t1);
        Assert.Equal(333_333, P(t2, S0));                                     // premise: waiting for builders at the save
        Assert.True(InstitutionStaffing.LabourAdults(t2, cfg, S0) > 0.0, "premise: labour adults present");
        long[] p = [333_333, 333_333, 666_666, 999_999];
        ThreeWays(Arms(t2), l => ConstructionOnly(cfg, l), log, 4, x => { if (x.Clock.Turn == 3) AllBuilders(x, S0); }, (arm, t) =>
        {
            HeadResolution r = ConstructionQuery.ResolveHead(t[0], cfg, S0, t[0].ConstructionQueue, t[0].ConstructionProgress, t[0].GoodStocks, 10.0, null)!;
            Eq((HeadLimitKind.Labour, false, false, 333_333L), (r.Limit, r.NoLabourAdults, r.StructurallyStalled, r.ProgressAfter), arm, "the loaded head's resolution");
            Eq(HoldRelease.None, ConstructionQuery.ReleaseOf(t[0], cfg, ConstructionQuery.Queue(t[0], S0)[0]), arm, "a builder-less head is a valid wait");
            Eq((Of(t[0], S0, Timber), Of(t[0], S0, Stone)), (Of(t[1], S0, Timber), Of(t[1], S0, Stone)), arm, "nothing drawn while waiting");
            for (int k = 1; k <= 3; k++)
            {
                Eq(p[k], P(t[k], S0), arm, $"p {k} step(s) after the load");
                Eq((100 - ConstructionQuery.Delivered(30, p[k]), 100 - ConstructionQuery.Delivered(7, p[k])),
                    (Of(t[k], S0, Timber), Of(t[k], S0, Stone)), arm, $"stocks {k} step(s) after the load (the lockstep)");
            }
            Eq(0L, Built(t[3], S0, Works), arm, "not built at state 5");
            Eq(1L, Built(t[4], S0, Works), arm, "built in step 5 → 6");
            Eq((70L, 93L), (Of(t[4], S0, Timber), Of(t[4], S0, Stone)), arm, "exactly the cost in all");
            AssertConserved(t[4]);
        });
    }

    // ================================================================== L3 (R3): the structural stalls

    /// <summary>
    /// L3 (i) — THE REAL PERSISTENCE CASE OF AN UNKNOWN PROJECT: content changed between save and load. Duo world; the
    /// test-only works (timber 30, stone 7) part-built to 428,571 at the AI's S1 and the player's S0 under content that
    /// defines it, saved at state 1, and continued under the SHIPPED content, which does not. After the load: both heads
    /// are UnknownProject (released: ReleaseOf = UnknownProject), keep p and draw nothing although stone and timber are
    /// there; the AI's kind-11 order for its own head appears at the first AI turn after the load and removes it in that
    /// step; the player's head is never cancelled.
    /// </summary>
    [Fact]
    public void L3i_AnUnknownProjectAfterAContentChange_WaitsKeepingP_AndTheAisCancelArrivesAtTheFirstAiTurnAfterTheLoad()
    {
        SimConfig withWorks = WithWorks(Cfg, 1.0, Works, timber: 30, stone: 7);
        WorldState w = Duo();
        AllBuilders(w, S0);
        AllBuilders(w, S1);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 3);
        Set(w, S1, Timber, 100);
        Set(w, S1, Stone, 3);
        WorldState t1 = ConstructionOnly(withWorks, Log(Enqueue(0, Player, S0, Works), Enqueue(0, Rival, S1, Works))).Step(w);
        Assert.Equal((428_571L, 428_571L), (P(t1, S0), P(t1, S1)));
        Assert.Null(Cfg.Goods!.ProjectById(Works));                           // premise: the shipped content has no works
        ThreeWays(Arms(t1), l => ConstructionOnly(Cfg, l), new OrderLog(), 1,
            x => { if (x.Clock.Turn == 1) { Set(x, S0, Stone, 10); Set(x, S1, Stone, 10); } }, (arm, t) =>
        {
            foreach (SettlementId s in new[] { S0, S1 })
            {
                HeadResolution r = ConstructionQuery.ResolveHead(t[0], Cfg, s, t[0].ConstructionQueue, t[0].ConstructionProgress, t[0].GoodStocks, 10.0, null)!;
                Eq((HeadLimitKind.UnknownProject, true), (r.Limit, r.StructurallyStalled), arm, $"S{s.Value} after the load");
                Eq(HoldRelease.UnknownProject, ConstructionQuery.ReleaseOf(t[0], Cfg, ConstructionQuery.Queue(t[0], s)[0]), arm, $"S{s.Value}'s release");
            }
        });
        // The AI's turn after the load: its producer runs on each loaded world, and its orders are delivered by the step.
        foreach ((string arm, WorldState loaded) in Arms(t1))
        {
            WorldState prev = loaded.Clone();
            Set(prev, S0, Stone, 10);
            Set(prev, S1, Stone, 10);
            OrderRecord[] ai = AiConstructionPolicy.Decide(prev, Cfg, Rival, 10.0);
            Assert.Contains(ConstructionQuery.CancelOrder(prev, Rival, S1, 0), ai);
            Assert.DoesNotContain(ai, o => o.Kind == OrderKind.CancelConstruction && o.TargetId == S0.Value);
            WorldState t2 = ConstructionOnly(Cfg, Log(ai)).Step(prev);
            Eq(0, ConstructionQuery.Queue(t2, S1).Length, arm, "the AI's head after its cancel");
            Eq(new ConstructionProgressRow(S0, 0, Works, 0, 428_571), ProgressRow(t2, S0), arm, "the player's head, kept");
            Eq((87L, 10L), (Of(t2, S0, Timber), Of(t2, S0, Stone)), arm, "nothing drawn by the unknown head");
            Eq(0.0, Labour(t2, S0), arm, "no labour");
            AssertConserved(t2);
        }
    }

    private static readonly int Tools = Cfg.Goods!.IdOf("tools");
    private const int Workshop = 2;          // goods.json: 60 timber, 40 stone, 10 tools, 8 adult-years

    /// <summary>
    /// L3 (ii) — AN UNOBTAINABLE CRAFTED INPUT, saved at the stall. The AI's workshop at S1 (queued at turn 0) is part-built
    /// by the step: 13 timber bound it at floor(13 × N / 60) = 216,666 (3 tools in), then with timber restored it reaches
    /// the edge of its third tool, 300,000, and needs a fourth that nothing in the Empire can make (toolmaking is an Age III
    /// chain). Saved there (state 2): after the load it is Unobtainable (structural for the AI, a VALID wait for the hold
    /// rule — ReleaseOf None), keeps p and draws nothing; the AI's cancel appears at the first AI turn after the load and is
    /// delivered in that step, with no refund.
    /// </summary>
    [Fact]
    public void L3ii_AnUnobtainableCraftedInput_SavedAtTheStall_TheAisCancelArrivesTurnExactAfterTheLoad()
    {
        WorldState w = Duo();
        w.ConstructionQueue.Add(new ConstructionQueueRow(S1, 0, Workshop));
        w.ConstructionProgress.Add(new ConstructionProgressRow(S1, 0, Workshop, 0, 0));   // an order's row (EnqueuedTurn 0, p 0)
        AllBuilders(w, S1);
        Set(w, S1, Timber, 13);
        Set(w, S1, Stone, 200);
        Set(w, S1, Tools, 0);
        Set(w, S3, Tools, 3);
        TurnExecutor exec = ConstructionOnly(Cfg, new OrderLog());
        WorldState t1 = exec.Step(w);
        Assert.Equal(216_666, P(t1, S1));
        Set(t1, S1, Timber, 200);
        WorldState t2 = exec.Step(t1);
        Assert.Equal(300_000, P(t2, S1));
        Assert.Equal(0, Of(t2, S1, Tools) + Of(t2, S3, Tools));
        StallThenAiCancel(t2, Cfg, S1, HeadLimitKind.Unobtainable, HoldRelease.None, Workshop, 300_000);
    }

    /// <summary>
    /// L3 (iii) — NO LABOUR ADULTS. The AI's granary at S1 part-built by the step (13 timber: floor(13 × N / 40) = 325,000),
    /// then S1 loses every working adult; saved there. After the load: Labour with no labour adults (structural; RELEASED —
    /// ReleaseOf NoLabourAdults), p kept, nothing drawn; the AI's cancel arrives at the first AI turn after the load.
    /// </summary>
    [Fact]
    public void L3iii_NoLabourAdults_SavedAtTheStall_TheAisCancelArrivesTurnExactAfterTheLoad()
    {
        WorldState w = Duo();
        AllBuilders(w, S1);
        Set(w, S1, Timber, 13);
        Set(w, S1, Stone, 200);
        WorldState t1 = ConstructionOnly(Cfg, Log(Enqueue(0, Rival, S1))).Step(w);
        Assert.Equal(325_000, P(t1, S1));
        NoAdults(t1, S1);
        Set(t1, S1, Timber, 200);
        Assert.Equal(0.0, InstitutionStaffing.LabourAdults(t1, Cfg, S1));
        StallThenAiCancel(t1, Cfg, S1, HeadLimitKind.Labour, HoldRelease.NoLabourAdults, Granary, 325_000);
    }

    /// <summary>
    /// L3 (iv) — A VIABILITY WAIT (the host's published food surplus below the founding threshold). The rig is
    /// IncrementalConstructionIntegrationTests' university rig, whose part-built head is WRITTEN as a row (p = 612,345; noted:
    /// stepping a university to part-built needs the whole institutions pipeline, and the subject here is the save, not the
    /// build). After the load: Viability (structural for the AI; a VALID wait for the hold rule — ReleaseOf None), p kept;
    /// the AI's cancel arrives at the first AI turn after the load and is delivered in that step.
    /// </summary>
    [Fact]
    public void L3iv_AViabilityWait_SavedAtTheStall_TheAisCancelArrivesTurnExactAfterTheLoad()
    {
        SimConfig cfg = UniversityRigs.Cfg(adultsPerUniversity: 100);
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, cfg, 42);
        UniversityRigs.Grant(w, TestConfigs.Research(), Rival);
        SettlementId s = LabourActivities.ControlledSettlements(w, Rival)[0];
        int university = InstitutionContent.ProjectOfType(cfg, 1)!.Id;
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, 0, university));
        w.ConstructionProgress.Add(new ConstructionProgressRow(s, 0, university, 0, 612_345));
        Share(w, s, 0.5);
        UniversityRigs.Materials(w, cfg, s);
        UniversityRigs.Surplus(w, s, cfg.Institutions!.Universities.FoundingFoodSurplusRatio - 0.1);
        Assert.Equal(ViabilityFailure.FoodSurplus, InstitutionViability.ToFound(w, cfg, s).Failure);
        StallThenAiCancel(w, cfg, s, HeadLimitKind.Viability, HoldRelease.None, university, 612_345);
    }

    /// <summary>The common half of L3 (ii)–(iv): the three continuations of <paramref name="atK"/>, where the AI's head at
    /// <paramref name="s"/> is structurally stalled: after the load it resolves to <paramref name="limit"/>, its release is
    /// <paramref name="release"/>, a step keeps p and draws nothing of its inputs; the AI's producer, run on the loaded
    /// world, orders the cancel, and the step that delivers it removes the entry and its row with no refund.</summary>
    private static void StallThenAiCancel(WorldState atK, SimConfig cfg, SettlementId s, HeadLimitKind limit, HoldRelease release, int project, long p)
    {
        foreach ((string arm, WorldState loaded) in Arms(atK))
        {
            HeadResolution r = ConstructionQuery.ResolveHead(loaded, cfg, s, loaded.ConstructionQueue, loaded.ConstructionProgress, loaded.GoodStocks, 10.0, null)!;
            Eq((limit, true, p, p), (r.Limit, r.StructurallyStalled, r.ProgressBefore, r.ProgressAfter), arm, "the loaded head's resolution");
            Eq(release, ConstructionQuery.ReleaseOf(loaded, cfg, ConstructionQuery.Queue(loaded, s)[0]), arm, "its release");

            // Waiting: a step with no order keeps p and draws none of its inputs.
            WorldState kept = ConstructionOnly(cfg, new OrderLog()).Step(loaded);
            Eq(new ConstructionProgressRow(s, 0, project, 0, p), ProgressRow(kept, s), arm, "the stalled head after a step");
            ConstructionProjectEntry entry = cfg.Goods!.ProjectById(project)!;
            foreach (ProjectInput input in entry.Inputs)
                Eq(Sunk(loaded, cfg.Goods.IdOf(input.Good)), Sunk(kept, cfg.Goods.IdOf(input.Good)), arm, input.Good + " sunk while stalled");

            // The AI's turn right after the load: its cancel is in its batch, and the step removes the entry.
            OrderRecord[] ai = AiConstructionPolicy.Decide(loaded, cfg, Rival, 10.0);
            OrderRecord cancel = ConstructionQuery.CancelOrder(loaded, Rival, s, 0);
            Assert.Contains(cancel, ai);
            Eq(loaded.Clock.Turn, cancel.Turn, arm, "the cancel's stamp (the load turn)");
            WorldState next = ConstructionOnly(cfg, Log(ai)).Step(loaded);
            Assert.DoesNotContain(next.ConstructionQueue.Rows(), q => q.Settlement == s && q.ProjectId == project);
            Assert.DoesNotContain(next.ConstructionProgress.Rows(), q => q.Settlement == s && q.ProjectId == project);
            foreach (ProjectInput input in entry.Inputs)
                Eq(Sunk(loaded, cfg.Goods.IdOf(input.Good)) <= Sunk(next, cfg.Goods.IdOf(input.Good)), true, arm, "no refund of " + input.Good);
            AssertConserved(next);
        }
    }

    private static void NoAdults(WorldState w, SettlementId s)
    {
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < w.Buckets.Count; i++)
        {
            BucketRow b = w.Buckets[i];
            if (b.Settlement != s || b.CohortIdx < Cohorts.FirstAdult || b.CohortIdx >= Cohorts.FirstElder || b.Count.Value == 0) continue;
            ledger.Flow(ref w.Buckets.Ref(i).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, b.Count.Value,
                FlowDirection.Sink, OverdrawPolicy.Throw);
        }
    }

    // ================================================================== L4 (R4): a HEAD cancel across the save

    /// <summary>The head-cancel rig: L1's works part-built at 428,571 (slot 0), a granary queued behind it at turn 1 (slot
    /// 1), and a kind-11 cancel of slot 0 stamped turn 2. 4 stone is set at state 2, so without the cancel the head WOULD
    /// complete in step 2 → 3.</summary>
    private static (WorldState T2, TurnExecutor Exec, SimConfig Cfg, OrderLog Log) HeadCancel(bool cancel = true)
    {
        SimConfig cfg = WithWorks(Cfg, 1.0, Works, timber: 30, stone: 7);
        WorldState w = Solo();
        AllBuilders(w, S0);
        Set(w, S0, Timber, 100);
        Set(w, S0, Stone, 3);
        OrderLog log = cancel
            ? Log(Enqueue(0, Player, S0, Works), Enqueue(1, Player, S0), Cancel(2, Player, S0, 0))
            : Log(Enqueue(0, Player, S0, Works), Enqueue(1, Player, S0));
        TurnExecutor exec = ConstructionOnly(cfg, log);
        WorldState t2 = exec.Run(w, 2);
        Assert.Equal(428_571, P(t2, S0));
        Assert.Equal(new ConstructionProgressRow(S0, 1, Granary, 1, 0), t2.ConstructionProgress[ConstructionQuery.ProgressIndex(t2.ConstructionProgress, new ConstructionQueueRow(S0, 1, Granary))]);
        Set(t2, S0, Stone, 4);
        return (t2, exec, cfg, log);
    }

    /// <summary>
    /// L4 (R4) — A HEAD CANCEL WITH p &gt; 0, ISSUED BEFORE THE SAVE AND DELIVERED AFTER IT (stamped 2, saved at 2), then
    /// saved again at 3 (delivered) and at 5 (later). At state 3, on every continuation: the works entry and its progress
    /// row are gone and NOTHING was built (the cancel precedes building work — the head would otherwise have completed on
    /// the 4 stone: the control without the cancel shows it); the 3 stone and 13 timber that went in stay sunk (no refund);
    /// slot 1, the granary, is the head with its OWN row (EnqueuedTurn 1) and in that same step draws the 4 stone:
    /// p = floor(4 × N / 20) = 200,000, 8 timber. With 16 stone set at state 3 it completes in step 3 → 4; no ghost row at
    /// states 3 to 5. This kills M-B (the kind-11 block moved after building work) on the save path.
    /// </summary>
    [Fact]
    public void L4_AHeadCancelStampedAtTheSaveTurn_IsAppliedBeforeBuildingWorkAfterTheLoad_NoRefund_TheNextSlotHeads()
    {
        (WorldState t2, TurnExecutor exec, SimConfig cfg, OrderLog log) = HeadCancel();
        // Control: without the cancel the head completes in step 2 → 3 on exactly those 4 stone.
        (WorldState c2, TurnExecutor cexec, _, _) = HeadCancel(cancel: false);
        Assert.Equal(1, Built(cexec.Step(c2), S0, Works));

        long sunkT = Sunk(t2, Timber), sunkS = Sunk(t2, Stone);
        Assert.Equal((13L, 3L), (sunkT - Sunk(Solo(), Timber), sunkS - Sunk(Solo(), Stone)));
        Action<WorldState> rig = x => { if (x.Clock.Turn == 3) Set(x, S0, Stone, 16); };
        void AtThree(string arm, WorldState t3)
        {
            Eq(0L, Built(t3, S0, Works), arm, "the cancelled works were never built");
            Assert.DoesNotContain(t3.ConstructionQueue.Rows(), q => q.Settlement == S0 && q.ProjectId == Works);
            Assert.DoesNotContain(t3.ConstructionProgress.Rows(), q => q.Settlement == S0 && q.ProjectId == Works);
            Eq(new ConstructionProgressRow(S0, 1, Granary, 1, 200_000), ProgressRow(t3, S0), arm, "slot 1 heads with its own row");
            Eq((sunkT + 8, sunkS + 4), (Sunk(t3, Timber), Sunk(t3, Stone)), arm, "the step drew only the granary's 8 timber and 4 stone (no refund)");
            Eq((87L - 8, 0L), (Of(t3, S0, Timber), Of(t3, S0, Stone)), arm, "stocks at 3");
        }
        void Later(string arm, WorldState[] t, int from)
        {
            for (int k = from; k < t.Length; k++)
            {
                Assert.DoesNotContain(t[k].ConstructionProgress.Rows(), q => q.ProjectId == Works);
                Eq(0L, Built(t[k], S0, Works), arm, "never built");
            }
        }
        // Saved at 2: the cancel issued (in the log), not delivered.
        WorldState[] never = ThreeWays(Arms(t2), l => ConstructionOnly(cfg, l), log, 3, rig, (arm, t) =>
        {
            Eq(new ConstructionProgressRow(S0, 0, Works, 0, 428_571), ProgressRow(t[0], S0), arm, "the head at the save");
            AtThree(arm, t[1]);
            Eq(1L, Built(t[2], S0), arm, "the granary completes in step 3 → 4");
            Eq(0, t[2].ConstructionProgress.Count, arm, "no ghost row at 4");
            Later(arm, t, 1);
        });
        // Saved at 3 (delivered) and at 5 (later): the same worlds onward.
        WorldState w3 = never[1], w5 = never[3];
        ThreeWays(Arms(w3), l => ConstructionOnly(cfg, l), log, 2, rig, (arm, t) =>
        {
            AtThree(arm, t[0]);
            Eq(WorldHash.ComputeHex(never[3]), WorldHash.ComputeHex(t[2]), arm, "state 5 from the save at 3");
            Later(arm, t, 0);
        });
        ThreeWays(Arms(w5), l => ConstructionOnly(cfg, l), log, 2, null, (arm, t) => Later(arm, t, 0));
    }

    // ================================================================== L5 (R5): a re-enqueue across the save

    /// <summary>The re-enqueue rig. S1's works (timber 30, stone 6) ordered at turn 0 at a labour pace of 1/6 (L = 6 C(S1):
    /// 166,666 units, one stone, a step); S0's hut (timber 10, stone 7, labour 1) ordered at turn 1 — younger. 200 timber at
    /// S2; stone set at S3 before each step: 1 (turn 0), 3 (turn 1), 6 (turn 2). At turn 2 ONE batch cancels S0's slot 0 and
    /// enqueues the hut again at S0.</summary>
    private static (WorldState W, SimConfig Cfg, OrderLog Log, Action<WorldState> Stone) ReEnqueue()
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        AllBuilders(w, S1);
        SimConfig cfg = WithWorks(WithWorks(Cfg, 6.0 * C(w, S1, 10.0), Works, timber: 30, stone: 6), 1.0, Hut, timber: 10, stone: 7);
        Set(w, S2, Timber, 200);
        OrderLog log = Log(Enqueue(0, Player, S1, Works), Enqueue(1, Player, S0, Hut), Cancel(2, Player, S0, 0), Enqueue(2, Player, S0, Hut));
        Action<WorldState> stone = x =>
        {
            long[] at = [1, 3, 6];
            if (x.Clock.Turn < at.Length) Set(x, S3, Stone, at[x.Clock.Turn]);
        };
        return (w, cfg, log, stone);
    }

    /// <summary>
    /// L5 (R5) — A RE-ENQUEUE (a cancel and an enqueue of the same project in one batch, stamped turn 2), saved with the
    /// batch issued (state 2) and again with it delivered (state 3). State 2: S1 (the older head) at 333,332 sets aside the
    /// 2 stone left (its remaining need is 4), so S0's first hut waited at p 0 (HeldForEarlier naming S1). Step 2 → 3 on
    /// every continuation: the cancel and the enqueue give S0 a NEW entry — slot 0, EnqueuedTurn 2, p 0 — which is resolved
    /// BEHIND the older head: S1 draws its one stone (→ 499,998) and sets aside min(3, 5) = 3, and the re-enqueued hut gets
    /// the 2 left: p = floor(2 × N / 7) = 285,714. BINDING PREMISE: the same continuation under PreReservations gives the hut
    /// all 5 (p = 714,285). The new key survives the save at 3 row for row, and the step after it equals the never-saved
    /// run's.
    /// </summary>
    [Fact]
    public void L5_AReEnqueueInOneBatch_SavedIssuedAndDelivered_IsANewEntryWithTheStampTurnKey_ServedBehindTheOlderHead()
    {
        (WorldState w, SimConfig cfg, OrderLog log, Action<WorldState> stone) = ReEnqueue();
        WorldState[] run = Continue(w, ConstructionOnly(cfg, log), 2, stone);
        WorldState t2 = run[2];
        Assert.Equal((333_332L, 0L), (P(t2, S1), P(t2, S0)));
        Assert.Equal(2, TotalOf(t2, Stone));
        HeadStatusReport waited = ConstructionQuery.HeadStatus(t2, cfg, S0, null, 10.0)!;
        Assert.Equal((MaterialCause.HeldForEarlier, new QueueEntryRef(S1, 0, Works)), (waited.Cause, waited.Holder));

        long sunk2 = Sunk(t2, Stone);
        WorldState[] never = ThreeWays(Arms(t2), l => ConstructionOnly(cfg, l), log, 2, stone, (arm, t) =>
        {
            Eq(new ConstructionProgressRow(S0, 0, Hut, 2, 285_714), ProgressRow(t[1], S0), arm, "the re-enqueued hut: slot 0, key 2");
            Eq(Hut, Assert.Single(ConstructionQuery.Queue(t[1], S0)).ProjectId, arm, "S0's queue");
            Eq(499_998L, P(t[1], S1), arm, "S1, the older head, served first");
            Eq((1L + 2, 3L), (Sunk(t[1], Stone) - sunk2, TotalOf(t[1], Stone)), arm, "the stone split: S1 1, the hut 2, 3 set aside");
            Eq(3L, HeldOf(t[1], PlayerPurse, Stone), arm, "S1's stone set aside");
            HeadStatusReport hut = ConstructionQuery.HeadStatus(t[1], cfg, S0, null, 10.0)!;
            Eq((MaterialCause.HeldForEarlier, (QueueEntryRef?)new QueueEntryRef(S1, 0, Works)), (hut.Cause, hut.Holder), arm, "the hut's next wait");
            AssertConserved(t[1]);
        });
        // BINDING: the same continuation under PreReservations gives the re-enqueued hut all 5 stone.
        SimConfig pre = TestConfigs.PreReservations(cfg);
        foreach ((string arm, WorldState loaded) in Arms(t2))
        {
            WorldState[] off = Continue(loaded, ConstructionOnly(pre, Copy(log)), 1, stone);
            Eq(new ConstructionProgressRow(S0, 0, Hut, 2, 714_285), ProgressRow(off[1], S0), arm, "PreReservations: the hut takes the 5");
        }
        // Saved again with the batch delivered: the new key survives row for row, and the next step is the never-saved one.
        ThreeWays(Arms(never[1]), l => ConstructionOnly(cfg, l), log, 1, stone, (arm, t) =>
        {
            Eq(new ConstructionProgressRow(S0, 0, Hut, 2, 285_714), ProgressRow(t[0], S0), arm, "the key after the load");
            Eq(WorldHash.ComputeHex(never[2]), WorldHash.ComputeHex(t[1]), arm, "the step after the load");
        });
    }

    // ================================================================== L7 / L8 (R7, R8): a binding split across the save

    private static readonly ResearchContent Research = TestConfigs.Research();
    /// <summary>The route's length: sim.json roads.maxRouteKm (600, the longest route a road order may modernize), one timber
    /// per km for the trackway — so within the horizon (at most 100 timber a step) road works can always use all that
    /// building work leaves (D_r ≥ the whole supply).</summary>
    private const double RouteKm = 600.0;
    private const long Supply = 100;

    /// <summary>ResourceAllocationTests' road rig, restated with a long route: the S0–S1 dirt path (a trackway route of
    /// <see cref="RouteKm"/>), the trackway known, every settlement all builders, 1000 stone at S3, and timber at S2 refilled
    /// to <see cref="Supply"/> before every step (the rig change, through the Ledger).</summary>
    private static WorldState RoadRig()
    {
        WorldState w = Solo();
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, S0, S1, EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, RouteKm, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(S0, S1, 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(S1, S0, 100.0));
        w.TradeFlows.Clear();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId("track_road")].Key));
        for (int s = 0; s < w.Settlements.Count; s++) AllBuilders(w, w.Settlements[s].Id);
        Set(w, S3, Stone, 1000);
        return w;
    }

    private static readonly Action<WorldState> Refill = x => Set(x, S2, Timber, Supply);

    private static OrderRecord Split(long turn, int buildings) =>
        new(turn, Player.Value, OrderKind.SetResourceAllocation, Player.Value, buildings);

    private static OrderRecord Roads(long turn) => OrderRecord.From(turn, Player, OrderKind.DevelopRoads, 0, 100.0);

    private static TurnExecutor BuildAndRoads(SimConfig cfg, OrderLog log) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.Construction(cfg), SystemCatalog.RoadDevelopment(cfg)], log);

    private static long RoadUnits(IReadOnlyWorldState w, long turn)
    {
        long u = 0;
        for (int i = 0; i < w.RoadDevelopments.Count; i++) if (w.RoadDevelopments[i].Turn == turn) u += w.RoadDevelopments[i].MaterialUnits;
        return u;
    }

    /// <summary>Four granaries (S0..S3, all ordered at turn 0, so served by settlement id), a road order every turn for
    /// <paramref name="turns"/> turns, and the split orders given.</summary>
    private static OrderLog SplitLog(int turns, params OrderRecord[] splits)
    {
        var log = new OrderLog();
        foreach (SettlementId s in new[] { S0, S1, S2, S3 }) log.Append(Enqueue(0, Player, s));
        for (int t = 0; t < turns; t++)
        {
            foreach (OrderRecord o in splits) if (o.Turn == t) log.Append(o);
            log.Append(Roads(t));
        }
        return log;
    }

    /// <summary>The closed form (D-054 WS2, ResourceAllocation.Split; restated, not read from the implementation): with the
    /// timber holding S, building work's claim D_b and road works' demand D_r (both ≥ S here: four granaries' 160 and a
    /// 600-km route), building work may take G_b = min(D_b, E_b + max(0, E_r − D_r)) with E_b = floor(S × b / 100) and
    /// E_r = S − E_b.</summary>
    private static long Grant(long s, long db, long dr, int b)
    {
        long eb = s * b / 100, er = s - eb;
        return Math.Min(db, eb + Math.Max(0, er - dr));
    }

    /// <summary>The granaries' remaining timber need, Σ Remaining(40, p), over the queue of <paramref name="w"/>.</summary>
    private static long TimberNeed(IReadOnlyWorldState w)
    {
        long need = 0;
        for (int i = 0; i < w.ConstructionQueue.Count; i++)
            need += ConstructionQuery.Remaining(40, ConstructionQuery.ProgressOf(w, w.ConstructionQueue[i]));
        return need;
    }

    /// <summary>
    /// L7 (R7) — A BINDING BUILDING/ROADS SPLIT ACROSS THE SAVE, at b = 0, 50 and an explicit 100. The split row is set by
    /// a kind-12 order at turn 0 and the world saved at state 1 with the row in force. In every one of the next three steps,
    /// on every continuation, building work draws exactly the grant G_b of the closed form (stone is plenty, so a granary's
    /// draw is its timber) and road works draw exactly what it leaves: S − G_b. The row persists. ANTI-VACUITY: the same
    /// continuation of the loaded world with the row removed (no split: building first) differs whenever b &lt; 100 — the
    /// split binds; the explicit 100 is the same as no row in its draws.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void L7_ABindingSplitInForceAtTheSave_GovernsEveryLaterStepExactly_OnEveryContinuation(int b)
    {
        WorldState w = RoadRig();
        OrderLog log = SplitLog(5, Split(0, b));
        Set(w, S2, Timber, Supply);
        WorldState t1 = BuildAndRoads(Cfg, log).Step(w);
        Assert.Equal(new ResourceAllocationRow(Player, b), Assert.Single(t1.ResourceAllocations.Rows()));
        ThreeWays(Arms(t1), l => BuildAndRoads(Cfg, l), log, 3, Refill, (arm, t) =>
        {
            for (int k = 1; k <= 3; k++)
            {
                long need = TimberNeed(t[k - 1]);
                long grant = Grant(Supply, Math.Min(Supply, need), Supply, b);
                // Road works sink under the same reason as construction (ResourceAllocationTests' "every unit leaves once").
                long built = Sunk(t[k], Timber) - Sunk(t[k - 1], Timber) - RoadUnits(t[k], t[k - 1].Clock.Turn);
                Eq(grant, built, arm, $"building work's timber in step {k} after the load (b = {b}, need {need})");
                Eq(Supply - grant, RoadUnits(t[k], t[k - 1].Clock.Turn), arm, $"road works' timber in step {k} after the load");
                Eq(new ResourceAllocationRow(Player, b), Assert.Single(t[k].ResourceAllocations.Rows()), arm, "the row persists");
                AssertConserved(t[k]);
            }
        });
        // ANTI-VACUITY: the loaded world with the row removed (building first) — differs exactly when the split binds.
        foreach ((string arm, WorldState loaded) in Arms(t1))
        {
            WorldState noRow = loaded.Clone();
            noRow.ResourceAllocations.Clear();
            WorldState[] withRow = Continue(loaded, BuildAndRoads(Cfg, Copy(log)), 1, Refill);
            WorldState[] without = Continue(noRow, BuildAndRoads(Cfg, Copy(log)), 1, Refill);
            long roadWith = RoadUnits(withRow[1], 1), roadWithout = RoadUnits(without[1], 1);
            Eq(Supply - Math.Min(Supply, TimberNeed(loaded)), roadWithout, arm, "no row: building first");
            Eq(b != 100, roadWith != roadWithout, arm, "the row changes the step exactly when b < 100");
        }
    }

    /// <summary>
    /// L8 (R8) — A SPLIT CHANGED BEFORE DELIVERY ACROSS THE SAVE. b = 0 in force from the start — the row WRITTEN BY THE RIG,
    /// so the setup step 0 → 1 does not depend on when a kind-12 order is applied (state 1: no granary has advanced; road
    /// works took all 100); a kind-12 order to 70 and a road order are stamped turn 1, and the world is saved at state 1 with
    /// them issued but not delivered. After the load, step 1 → 2 is governed by b = 70 — building work 70 (S0's granary
    /// completes, S1's reaches floor(30 × N / 40) = 750,000), road works 30 — and state 2 holds the 70 row, on every
    /// continuation. The control without the turn-1 order draws by b = 0 (building 0, roads 100). M-A (the kind-12 block
    /// moved after building work) fails the CONTINUATION assertion — step 1 → 2 is then governed by b = 0 — the one every
    /// continuation, the two loaded ones included, must meet (D-055 fix round, 2026-10-17, T4-L1-T4V1-F6: the turn-0 split
    /// was a kind-12 order, so M-A tripped the in-process setup check instead and never reached the loaded continuations).
    /// </summary>
    [Fact]
    public void L8_ASplitChangedAtTheSaveTurn_GovernsTheStepAfterTheLoad_TurnExact()
    {
        WorldState w = RoadRig();
        w.ResourceAllocations.Add(new ResourceAllocationRow(Player, 0));
        Set(w, S2, Timber, Supply);
        OrderLog log = SplitLog(3, Split(1, 70));
        WorldState t1 = BuildAndRoads(Cfg, log).Step(w);
        Assert.Equal(new ResourceAllocationRow(Player, 0), Assert.Single(t1.ResourceAllocations.Rows()));
        Assert.Equal((160L, Supply), (TimberNeed(t1), RoadUnits(t1, 0)));                   // premise: b = 0 bound at step 0 → 1
        ThreeWays(Arms(t1), l => BuildAndRoads(Cfg, l), log, 1, Refill, (arm, t) =>
        {
            Eq(new ResourceAllocationRow(Player, 0), Assert.Single(t[0].ResourceAllocations.Rows()), arm, "b = 0 at the save");
            Eq(new ResourceAllocationRow(Player, 70), Assert.Single(t[1].ResourceAllocations.Rows()), arm, "b = 70 at state 2");
            Eq((1L, 750_000L), (Built(t[1], S0), P(t[1], S1)), arm, "building work's 70: S0 completes, S1 at 750,000");
            Eq(30L, RoadUnits(t[1], 1), arm, "road works' 30");
        });
        OrderLog control = SplitLog(3);
        foreach ((string arm, WorldState loaded) in Arms(t1))
        {
            WorldState[] c = Continue(loaded, BuildAndRoads(Cfg, Copy(control)), 1, Refill);
            Eq((0L, 0L, Supply), (Built(c[1], S0), P(c[1], S1), RoadUnits(c[1], 1)), arm, "the control (b = 0 kept)");
        }
    }

    // ================================================================== L9: a binding hold mid-wait; L10: tamper

    /// <summary>
    /// The hold rig. S1's works (timber 30, stone 7, labour 1) ordered at turn 0 — the OLDER head; S0's hut (timber 10,
    /// stone 7, labour 1) ordered at turn 1 — the younger, in the same (the player's) purse; 200 timber at S2; 3 stone at S3.
    /// Step 0 → 1: S1 reaches 428,571 (3 stone in). S1 then loses its builders (labour adults present: a VALID wait, it
    /// keeps holding its remaining need). Step 1 → 2: no stone. 6 stone are set at state 2; step 2 → 3: S1 sets aside its
    /// remaining Remaining(7, 428,571) = 4 stone and the hut draws the 2 left: floor(2 × N / 7) = 285,714 (PreReservations:
    /// all 6, 857,142). Optionally a cancel of S1's head stamped turn <paramref name="cancelAt"/>.
    /// </summary>
    private static (WorldState T3, SimConfig Cfg, OrderLog Log) HoldRig(long? cancelAt = null)
    {
        WorldState w = Solo();
        AllBuilders(w, S0);
        AllBuilders(w, S1);
        SimConfig cfg = WithWorks(WithWorks(Cfg, 1.0, Works, timber: 30, stone: 7), 1.0, Hut, timber: 10, stone: 7);
        Set(w, S2, Timber, 200);
        Set(w, S3, Stone, 3);
        OrderLog log = Log(Enqueue(0, Player, S1, Works), Enqueue(1, Player, S0, Hut));
        if (cancelAt is { } c) log.Append(Cancel(c, Player, S1, 0));
        TurnExecutor exec = ConstructionOnly(cfg, log);
        WorldState t1 = exec.Step(w);
        Assert.Equal(428_571, P(t1, S1));
        NoBuilders(t1, S1);
        WorldState t2 = exec.Step(t1);
        Set(t2, S3, Stone, 6);
        WorldState t3 = exec.Step(t2);
        Assert.Equal((428_571L, 285_714L), (P(t3, S1), P(t3, S0)));
        Assert.Equal(4, TotalOf(t3, Stone));
        Assert.Equal(4, HeldOf(t3, PlayerPurse, Stone));
        return (t3, cfg, log);
    }

    /// <summary>
    /// L9 — A BINDING HOLD MID-WAIT, across the save. Saved at state 3: S1 (older, no builders) waits at 428,571 and sets
    /// aside its 4 remaining stone — all the purse holds; S0's hut (younger) is at 285,714. Step 3 → 4 on every continuation:
    /// S1 still waits and still sets aside the 4; the hut DRAWS 0 STONE (p stays 285,714), HeldForEarlier naming S1 with 4
    /// set aside ahead. BINDING PREMISE: the same continuation under PreReservations lets the hut draw all 4 (p =
    /// floor(6 × N / 7) = 857,142). Then the TURN-EXACT RELEASE AFTER A LOAD: a cancel of S1's head stamped turn 4 and the
    /// world saved at state 4 with it issued, not delivered — step 4 → 5 removes S1's head BEFORE the walk, and the hut draws
    /// the 4 released stone in that same step (857,142; 6 more timber), on every continuation.
    /// </summary>
    [Fact]
    public void L9_ABindingHoldMidWait_SurvivesTheSave_TheYoungerHeadDrawsNothing_AndACancelSavedBeforeDeliveryReleasesItTurnExact()
    {
        (WorldState t3, SimConfig cfg, OrderLog log) = HoldRig(cancelAt: 4);
        long sunkS = Sunk(t3, Stone), sunkT = Sunk(t3, Timber);
        WorldState[] never = ThreeWays(Arms(t3), l => ConstructionOnly(cfg, l), log, 2, null, (arm, t) =>
        {
            // Step 3 → 4: the hold binds.
            HeadStatusReport hut = ConstructionQuery.HeadStatus(t[0], cfg, S0, null, 10.0)!;
            Eq((MaterialCause.HeldForEarlier, (QueueEntryRef?)new QueueEntryRef(S1, 0, Works), 4L), (hut.Cause, hut.Holder, hut.SetAsideAhead), arm, "the hut's status at the load");
            Eq((428_571L, 285_714L), (P(t[1], S1), P(t[1], S0)), arm, "both wait in step 3 → 4");
            Eq((sunkS, 4L), (Sunk(t[1], Stone), TotalOf(t[1], Stone)), arm, "the hut drew 0 stone; the 4 stay set aside");
            Eq(4L, HeldOf(t[1], PlayerPurse, Stone), arm, "S1's 4 stone set aside");
            // Step 4 → 5: the cancel (issued at 4) releases the hold before the walk; the hut draws the 4 in that step.
            Assert.Empty(ConstructionQuery.Queue(t[2], S1));
            Eq(857_142L, P(t[2], S0), arm, "the hut on the released stone");
            Eq((sunkS + 4, sunkT + 6), (Sunk(t[2], Stone), Sunk(t[2], Timber)), arm, "the hut's draw in the release step");
            Eq(0L, HeldOf(t[2], PlayerPurse, Stone), arm, "no stone set aside after the release");
            AssertConserved(t[2]);
        });
        // BINDING PREMISE on the loaded worlds: under PreReservations the same step lets the hut take the 4.
        SimConfig pre = TestConfigs.PreReservations(cfg);
        foreach ((string arm, WorldState loaded) in Arms(t3))
        {
            WorldState off = ConstructionOnly(pre, Copy(log)).Step(loaded);
            Eq((428_571L, 857_142L, sunkS + 4), (P(off, S1), P(off, S0), Sunk(off, Stone)), arm, "PreReservations: the hut takes the 4");
        }
        // Saved at 4 with the cancel issued, not delivered: the release is turn-exact after the load.
        WorldState t4 = never[1];
        OrderBatch batch4 = log.BatchFor(4);
        Assert.Equal(Cancel(4, Player, S1, 0), Assert.Single(Enumerable.Range(0, batch4.Count).Select(i => batch4[i])));
        ThreeWays(Arms(t4), l => ConstructionOnly(cfg, l), log, 1, null, (arm, t) =>
        {
            Eq(428_571L, P(t[0], S1), arm, "S1 still queued at the save");
            Assert.Empty(ConstructionQuery.Queue(t[1], S1));
            Eq((857_142L, sunkS + 4), (P(t[1], S0), Sunk(t[1], Stone)), arm, "the release step after the load");
        });
    }

    /// <summary>
    /// L10 — THE TAMPER TEST: the published holds are NEVER AN INPUT TO CONSTRUCTION (design X1, review L4-05; in the
    /// shipped pipeline order road development also reads only this step's rebuild — ADR-040, D-055 §11 — which this
    /// construction-only rig does not exercise). The v34 save of L9's state 3
    /// (the hold binding: S1 sets aside 4 stone that the younger hut needs) is altered so that it is still a WELL-FORMED
    /// section 61 — sorted, Units &gt; 0, Good ≥ 0 — and Snapshot.Load accepts it: the stone row lowered to 1, raised to 400,
    /// both rows dropped, and a row added for a purse no head has and for a good the registry does not define. In every case
    /// the world at K differs (its hash), and the next world equals the untampered continuation's — every table and the hash
    /// — on all three continuations (the in-memory tampered world, Snapshot.Load and SaveFiles.Load of the tampered file):
    /// construction clears the table and the walk recomputes the holds from the queue, progress and stocks. The binding is
    /// L9's: the hut draws 0 there. This kills a walk seeded from PREV.ConstructionHolds.
    /// </summary>
    [Theory]
    [InlineData("lowered")]
    [InlineData("raised")]
    [InlineData("dropped")]
    [InlineData("foreign")]
    public void L10_ATamperedButWellFormedHoldsTable_LoadsAndDiffersAtK_ButTheNextWorldIsTheUntamperedOne(string tamper)
    {
        (WorldState t3, SimConfig cfg, OrderLog log) = HoldRig();
        WorldState untampered = ConstructionOnly(cfg, Copy(log)).Step(t3);
        Assert.Equal((285_714L, 4L), (P(untampered, S0), HeldOf(untampered, PlayerPurse, Stone)));   // the hold binds

        WorldState bad = t3.Clone();
        ConstructionHoldRow[] rows = t3.ConstructionHolds.Rows();
        Assert.Contains(new ConstructionHoldRow(PlayerPurse, Stone, 4), rows);
        bad.ConstructionHolds.Clear();
        var edited = new List<ConstructionHoldRow>();
        foreach (ConstructionHoldRow r in rows)
        {
            if (tamper == "dropped") continue;
            edited.Add(r.Good == Stone && tamper is "lowered" or "raised" ? r with { Units = tamper == "lowered" ? 1 : 400 } : r);
        }
        if (tamper == "foreign")
        {
            edited.Add(new ConstructionHoldRow(Reservations.SettlementPurse(S3), Stone, 9));   // a purse no head has
            edited.Add(new ConstructionHoldRow(PlayerPurse, 9_999, 5));                         // a good the registry lacks
        }
        edited.Sort(Reservations.CompareKeys);
        foreach (ConstructionHoldRow r in edited) bad.ConstructionHolds.Add(r);
        Assert.Null(Reservations.InvariantViolation(bad.ConstructionHolds));                   // well-formed: Load accepts it
        Assert.NotEqual(WorldHash.ComputeHex(t3), WorldHash.ComputeHex(bad));                  // the world at K differs

        string next = WorldHash.ComputeHex(untampered);
        foreach ((string arm, WorldState loaded) in ArmsOf(SaveBytes(bad), bad))
        {
            Assert.NotEqual(WorldHash.ComputeHex(t3), WorldHash.ComputeHex(loaded));
            WorldState stepped = ConstructionOnly(cfg, Copy(log)).Step(loaded);
            Assert.True(WorldStates.StateEquals(untampered, stepped), $"{arm} ({tamper}): the next world differs from the untampered continuation's");
            Eq(next, WorldHash.ComputeHex(stepped), arm, tamper + ": the next world's hash");
            Eq((285_714L, 4L), (P(stepped, S0), HeldOf(stepped, PlayerPurse, Stone)), arm, tamper + ": the hut still draws 0; S1 still sets aside 4");
            // BINDING PREMISE on the loaded world: the PreReservations step lets the hut draw the 4.
            Eq(857_142L, P(ConstructionOnly(TestConfigs.PreReservations(cfg), Copy(log)).Step(loaded), S0), arm, tamper + ": PreReservations");
        }
    }
}
