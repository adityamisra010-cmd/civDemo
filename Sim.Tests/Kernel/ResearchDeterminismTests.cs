using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-029 / D-044 R19 — research is deterministic end to end on the PRODUCTION
/// pipeline. The worlds are the dev founded world (256², four settlements) driven by
/// the measurement driver: before each step, a polity with no target orders the
/// cheapest available node. What must hold:
/// a twin run is identical every turn; a replay of the recorded order log without the
/// driver is identical every turn, so the order pathway alone determines research;
/// and a save/load in mid-research keeps partial progress and continues bit-identically.
/// </summary>
[Trait("suite", "determinism")]
public class ResearchDeterminismTests
{
    // 320: under addendum A's content-derived costs the cheapest driver completes 20 nodes by
    // turn 300 on this world (measured), so 320 keeps the >= 20 anti-vacuity bar below unweakened.
    private const int Turns = 320;
    private static readonly PolityId Player = new(1);

    private static TurnExecutor Executor(OrderLog orders)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era),
            PipelineLoader.Load(pipe, SystemCatalog.All(TestConfigs.Sim(), TestConfigs.DevWorldgen())), orders);
    }

    private static WorldState Found() => WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);

    /// <summary>Runs with the driver appending orders; returns the per-turn hashes.</summary>
    private static List<string> Driven(OrderLog orders, out WorldState final, int turns = Turns)
    {
        ResearchContent content = TestConfigs.Research();
        TurnExecutor ex = Executor(orders);
        WorldState w = Found();
        var hashes = new List<string>(turns);
        for (int t = 0; t < turns; t++)
        {
            if (!ResearchQuery.TryGetTarget(w, Player, out _) && ResearchQuery.CheapestAvailable(w, content, Player) is { } pick)
                orders.Append(OrderRecord.From(w.Clock.Turn, Player, OrderKind.SetResearchTarget, pick.Value, 0.0));
            w = ex.Step(w);
            hashes.Add(WorldHash.ComputeHex(w));
        }
        final = w;
        return hashes;
    }

    [Fact]
    public void Research_TwinAndReplayAreIdenticalEveryTurn_TheOrderLogAloneDeterminesIt()
    {
        var logA = new OrderLog();
        List<string> a = Driven(logA, out WorldState finalA);
        var logB = new OrderLog();
        List<string> b = Driven(logB, out _);
        Assert.Equal(a, b);

        // Replay: the recorded log, reloaded from bytes, with NO driver.
        using var ms = new MemoryStream();
        logA.Save(ms);
        ms.Position = 0;
        OrderLog reloaded = OrderLog.Load(ms);
        Assert.Equal(logA.Count, reloaded.Count);
        TurnExecutor ex = Executor(reloaded);
        WorldState w = Found();
        for (int t = 0; t < Turns; t++)
        {
            w = ex.Step(w);
            Assert.Equal(a[t], WorldHash.ComputeHex(w));
        }

        // Anti-vacuity: research really happened on this run.
        ResearchContent content = TestConfigs.Research();
        Assert.True(ResearchQuery.CompletedNodes(finalA, content, Player).Length >= 20);
        Assert.True(logA.Count >= 20);
    }

    [Fact]
    public void Research_SaveLoadMidResearch_KeepsPartialProgress_AndContinuesIdentically()
    {
        var log = new OrderLog();
        List<string> uninterrupted = Driven(log, out _);

        TurnExecutor ex = Executor(log);
        WorldState w = Found();
        // Save at the first turn from the midpoint on that is IN THE MIDDLE of a node:
        // a target is set and holds partial progress (a turn that just completed its
        // target would test nothing about partial progress).
        int saveAt = 0;
        ResearchNodeId target = default;
        for (int t = 0; t < Turns; t++)
        {
            w = ex.Step(w);
            if (t + 1 >= Turns / 2 && ResearchQuery.TryGetTarget(w, Player, out target)
                && ResearchQuery.Progress(w, Player, target) > 0.0) { saveAt = t + 1; break; }
        }
        Assert.True(saveAt > 0 && saveAt < Turns, "no mid-node save point in the run");
        double partial = ResearchQuery.Progress(w, Player, target);

        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        ms.Position = 0;
        WorldState back = Snapshot.Load(ms, Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.DevWorldgen(), 42));
        Assert.True(WorldStates.StateEquals(w, back), "save/load drifted");
        Assert.Equal(partial, ResearchQuery.Progress(back, Player, target));

        for (int t = saveAt; t < Turns; t++)
        {
            back = ex.Step(back);
            Assert.Equal(uninterrupted[t], WorldHash.ComputeHex(back));
        }
    }

    [Fact]
    public void Research_TurnExactDelivery_OnTheFoundedWorld()
    {
        // The first driver order is stamped turn 0; the world at turn 1 already holds
        // exactly one turn of research on that node (ResearchPointPool on PREV — per turn, ADR-030).
        ResearchContent content = TestConfigs.Research();
        WorldState w0 = Found();
        ResearchNodeId pick = ResearchQuery.CheapestAvailable(w0, content, Player)!.Value;
        var orders = new OrderLog();
        orders.Append(OrderRecord.From(0, Player, OrderKind.SetResearchTarget, pick.Value, 0.0));
        TurnExecutor ex = Executor(orders);
        WorldState w1 = ex.Step(w0);
        double expected = Math.Min(
            ResearchQuery.ResearchPointPool(w0, content, Player),
            ResearchQuery.EffectiveCost(w0, content, Player, content.IndexOf(pick)));
        Assert.True(expected > 0.0);
        double eureka = ResearchQuery.CreditedBySource(w1, Player, pick, AccelerationSource.Eureka); // credited this step too
        double got = ResearchQuery.IsCompleted(w1, Player, pick)
            ? ResearchQuery.EffectiveCost(w0, content, Player, content.IndexOf(pick))
            : ResearchQuery.Progress(w1, Player, pick);
        Assert.Equal(Math.Min(expected + eureka, ResearchQuery.EffectiveCost(w0, content, Player, content.IndexOf(pick))), got);

        // An order stamped turn 1 does nothing to world 1.
        var late = new OrderLog();
        late.Append(OrderRecord.From(1, Player, OrderKind.SetResearchTarget, pick.Value, 0.0));
        WorldState l1 = Executor(late).Step(Found());
        Assert.False(ResearchQuery.TryGetTarget(l1, Player, out _));
    }
}
