using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

// T1.9 — the determinism harness extended to the M1 PRODUCTION preset on a
// founded 1024² world (the world the director actually plays). Twin,
// ordered-twin, replay and per-turn conservation legs; the toy-preset kernel
// tests remain untouched alongside.
//
// TURN COUNT = 200: at Neolithic dt (10y) that is 2,000 sim-years — through
// the labor-limited ramp, the famine window, and ≥5 Malthus-lite cycles, i.e.
// every M1 dynamic. Cost accounting for the ~3-minute determinism CI budget:
// a 1024² founding is ~2s and 200 founded turns ~1s (catchment recomputes
// included); these four legs construct 7 independent worlds ≈ 20–25s total,
// leaving the job comfortably inside budget (T0.8's toy legs are ~30s).
[Trait("suite", "determinism")]
public class FoundedHarnessTests
{
    private const ulong Seed = 42;
    private const int Turns = 200;

    private static TurnExecutor FreshExecutor(OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline(); // PRODUCTION preset
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(TestConfigs.Sim())),
            orders);
    }

    /// <summary>Fresh 1024² founded world — everything constructed anew (twin
    /// equality therefore also proves founding + system statelessness).</summary>
    private static WorldState FreshFounded() =>
        WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), Seed);

    /// <summary>Orders shaped like a real session: labor swings incl. both boundaries.</summary>
    private static OrderLog SessionLog()
    {
        var log = new OrderLog();
        double[] pcts = [60.0, 30.0, 80.0, 0.0, 100.0, 45.0];
        for (int i = 0; i < pcts.Length; i++)
            log.Append(new OrderRecord(3 + i * 30, ActorId: 1, OrderKind.LaborAllocation, 0, pcts[i]));
        return log;
    }

    [Fact]
    public void FoundedTwin_HashIdentical_EveryTurn()
    {
        TurnExecutor execA = FreshExecutor(), execB = FreshExecutor();
        WorldState a = FreshFounded(), b = FreshFounded();
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b)); // founding itself twins

        for (int t = 1; t <= Turns; t++)
        {
            a = execA.Step(a);
            b = execB.Step(b);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        }
    }

    [Fact]
    public void FoundedOrderedTwin_HashIdentical_EveryTurn()
    {
        TurnExecutor execA = FreshExecutor(SessionLog()), execB = FreshExecutor(SessionLog());
        WorldState a = FreshFounded(), b = FreshFounded();
        for (int t = 1; t <= Turns; t++)
        {
            a = execA.Step(a);
            b = execB.Step(b);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        }
        // Anti-vacuity (adversarial pass): prove the ORDERS actually fired —
        // the last SessionLog order (45%) must be the live allocation. Edges
        // alone can't prove it (path labor exists without orders too).
        Assert.Equal(0.45, a.LaborAllocations[0].FarmShare);
        Assert.True(a.NetworkEdges.Count > 0, "ordered run built nothing — vacuous twin");
    }

    [Fact]
    public void FoundedReplay_LogRoundTrip_ReproducesHashForHash()
    {
        // The D-008 recovery path on the production world: run once capturing
        // hashes, serialize the log to BYTES, reload, replay independently.
        var hashesA = new List<string>(Turns);
        TurnExecutor execA = FreshExecutor(SessionLog());
        WorldState a = FreshFounded();
        for (int t = 1; t <= Turns; t++)
        {
            a = execA.Step(a);
            hashesA.Add(WorldHash.ComputeHex(a));
        }

        using var buffer = new MemoryStream();
        SessionLog().Save(buffer);
        buffer.Position = 0;
        OrderLog reloaded = OrderLog.Load(buffer);

        TurnExecutor execB = FreshExecutor(reloaded);
        WorldState b = FreshFounded();
        for (int t = 1; t <= Turns; t++)
        {
            b = execB.Step(b);
            Assert.Equal(hashesA[t - 1], WorldHash.ComputeHex(b));
        }
    }

    [Fact]
    public void FoundedConservation_ExactEveryTurn()
    {
        TurnExecutor exec = FreshExecutor(SessionLog());
        WorldState world = FreshFounded();
        for (int t = 1; t <= Turns; t++)
        {
            world = exec.Step(world);
            Assert.True(ConservationAuditor.IsConserved(world, out string report),
                $"turn {t}: {report}");
        }
    }
}
