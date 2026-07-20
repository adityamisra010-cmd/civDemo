using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.Kernel;

// T0.8 — THE DETERMINISM HARNESS: the permanent regression gate, run as the
// required "determinism" CI job on every push.
//
// DIVISION OF LABOR (ADR-006): scripts/check-banned-constructs.sh catches VISIBLE
// banned constructs (framework RNG, single-precision types, wall clock, …). THIS harness
// catches BEHAVIORAL divergence no grep can see — hash-bucket-ordered iteration,
// allocation-address dependence, hidden shared state, culture leaks. Teeth were
// proven at packet time: a grep-invisible Dictionary<object,·>-iteration-ordered
// state write injected into a toy system passed the grep and failed the twin-run.
//
// Every run here constructs EVERYTHING fresh — WorldState, system instances (via
// SystemCatalog), executor, era table, pipeline — so twin equality also proves
// system statelessness as a side effect.
[Trait("suite", "determinism")]
public class DeterminismHarnessTests
{
    private const int Turns = 1000;
    private const ulong Seed = 42;

    private static TurnExecutor FreshExecutor(OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        // SystemCatalog.All() constructs NEW system instances every call.
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All()),
            orders);
    }

    private static WorldState Genesis()
    {
        var world = new WorldState(Seed);
        world.Regions.Add(new RegionRow(new RegionId(0)));
        world.Regions.Add(new RegionRow(new RegionId(1)));
        return world;
    }

    // Synthetic order log: a rain bias on alternating regions every 50th turn —
    // enough order traffic to exercise the §3.9 pipe across the whole run.
    private static OrderLog SyntheticLog()
    {
        var log = new OrderLog();
        for (long turn = 0; turn < Turns; turn += 50)
        {
            log.Append(new OrderRecord(turn, ActorId: 1, OrderKind.SetRainBias,
                TargetId: (int)(turn / 50) % 2, Amount: 300.0 + turn));
        }
        return log;
    }

    [Fact]
    public void TwinRun_Orderless_PerTurnHashEquality_1000Turns()
    {
        var executorA = FreshExecutor();
        var executorB = FreshExecutor();
        WorldState a = Genesis(), b = Genesis();

        for (int turn = 1; turn <= Turns; turn++)
        {
            a = executorA.Step(a);
            b = executorB.Step(b);
            Assert.True(WorldHash.ComputeHex(a) == WorldHash.ComputeHex(b),
                $"twin runs diverged at turn {turn}");
        }
    }

    [Fact]
    public void TwinRun_WithIdenticalOrderLogs_PerTurnHashEquality_1000Turns()
    {
        // Two independently BUILT (identical) logs — the runs share no objects.
        var executorA = FreshExecutor(SyntheticLog());
        var executorB = FreshExecutor(SyntheticLog());
        WorldState a = Genesis(), b = Genesis();

        for (int turn = 1; turn <= Turns; turn++)
        {
            a = executorA.Step(a);
            b = executorB.Step(b);
            Assert.True(WorldHash.ComputeHex(a) == WorldHash.ComputeHex(b),
                $"ordered twin runs diverged at turn {turn}");
        }
    }

    [Fact]
    public void Replay_ReloadedLog_HashForHash_1000Turns()
    {
        // The ordered run, recording per-turn hashes…
        var log = SyntheticLog();
        var original = FreshExecutor(log);
        var hashes = new string[Turns];
        WorldState world = Genesis();
        for (int turn = 0; turn < Turns; turn++)
        {
            world = original.Step(world);
            hashes[turn] = WorldHash.ComputeHex(world);
        }

        // …then replay(seed, RELOADED log) — the D-008 recovery path — must
        // reproduce it hash-for-hash at every turn.
        using var buffer = new MemoryStream();
        log.Save(buffer);
        buffer.Position = 0;
        var replay = FreshExecutor(OrderLog.Load(buffer));
        WorldState replayWorld = Genesis();
        for (int turn = 0; turn < Turns; turn++)
        {
            replayWorld = replay.Step(replayWorld);
            Assert.True(hashes[turn] == WorldHash.ComputeHex(replayWorld),
                $"replay diverged at turn {turn + 1}");
        }
    }

    [Fact]
    public void Conservation_AuditedEveryTurn_1000Turns()
    {
        // Law 1, checked at EVERY turn of the full ordered run: all quantities,
        // exact identity, no epsilon.
        var executor = FreshExecutor(SyntheticLog());
        WorldState world = Genesis();
        for (int turn = 1; turn <= Turns; turn++)
        {
            world = executor.Step(world);
            Assert.True(ConservationAuditor.IsConserved(world, out string report),
                $"turn {turn}: {report}");
        }
    }
}
