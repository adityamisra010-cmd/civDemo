using Sim.Core.Kernel;
using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// T4.19 lane A2 — NO TELEMETRY-INDUCED SIMULATION CHANGE. The explanations
/// are pure functions of read-only worlds: asking them changes no hash, and a
/// world that is explained after every step ends bit-identical to its
/// unobserved twin. The queries hold no reference a system could read — they
/// return records and keep nothing — so the twin test is the whole proof.
/// </summary>
public class ExplainPurityTests
{
    private static void ExplainEverything(WorldState prev, WorldState next, SimConfig cfg)
    {
        foreach ((SettlementId s, ClassId c) in ExplainRigs.GrievanceKeys(next))
        {
            GrievanceExplanation g = GrievanceExplanation.For(prev, next, cfg, s, c);
            foreach (NeedEntry need in cfg.Needs!.Needs) _ = CausalChain.ForNeed(prev, next, cfg, s, c, need.Id);
            _ = g.PrimaryChain;
        }
        for (int i = 0; i < next.Settlements.Count; i++)
        {
            _ = HappinessExplanation.For(next, cfg, next.Settlements[i].Id);
            _ = MigrationExplanation.For(prev, next, cfg, next.Settlements[i].Id);
        }
    }

    [Fact]
    public void ExplanationCalls_MutateNothing_HashEqualBeforeAndAfter()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(3);
        WorldState prev = worlds[2], next = worlds[3];
        string prevBefore = WorldHash.ComputeHex(prev), nextBefore = WorldHash.ComputeHex(next);

        ExplainEverything(prev, next, cfg);

        Assert.Equal(prevBefore, WorldHash.ComputeHex(prev));
        Assert.Equal(nextBefore, WorldHash.ComputeHex(next));
    }

    [Fact]
    public void ObservedAndUnobservedTwins_HashIdentical_EveryTurn()
    {
        const int MaxTurns = 16;
        SimConfig cfg = TestConfigs.Sim();
        // An ORDERED run (zero food for settlement 0), so the explained world is
        // the one with something to explain; stepped until it starves plus two
        // turns, hash-compared to its unobserved twin after every step.
        TurnExecutor observed = ExplainRigs.Executor(cfg, ExplainRigs.ZeroFoodOrders(ExplainRigs.Target));
        TurnExecutor unobserved = ExplainRigs.Executor(cfg, ExplainRigs.ZeroFoodOrders(ExplainRigs.Target));
        WorldState a = ExplainRigs.Found(cfg), b = ExplainRigs.Found(cfg);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));

        int starvedAt = -1, turns = 0;
        for (int t = 1; t <= MaxTurns; t++)
        {
            WorldState aPrev = a;
            a = observed.Step(a);
            ExplainEverything(aPrev, a, cfg);      // observed after EVERY step
            b = unobserved.Step(b);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
            turns = t;
            if (starvedAt < 0 && ExplainRigs.Deficit(a, ExplainRigs.Target) > 0.0) starvedAt = t;
            if (starvedAt > 0 && t >= starvedAt + 2) break;
        }
        // Anti-vacuity: the observed run actually did something worth observing.
        Assert.True(starvedAt > 0, $"vacuous: the observed twin never starved in {turns} turns");
    }
}
