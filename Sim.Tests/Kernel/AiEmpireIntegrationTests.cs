using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-033 D5 — THE AI ACTS THROUGH THE SAME ORDERS, end to end on the CANONICAL founded world (seed 42,
/// 1024², twelve settlements, worldgen aiEmpires overridden to 1; the default stays 0) and the production
/// pipeline. Before every step AiOrders appends the AI Empire's orders to the ONE order log the executor
/// delivers from — exactly what UiSession.EndTurn and `sim run` do — and nothing else feeds the world. The AI
/// sets research targets, completes nodes, enqueues and builds projects and advances an Age through the
/// AdvanceAge order; the player Empire is never given an order. Replaying the log from a fresh founding with a
/// fresh executor reproduces every turn's hash (the AI producer is NOT run again: its decisions are the log).
/// The measured milestones are pinned (MEASURED on this tree, Release, and identically by
/// `sim run --founded --seed 42 --ai-empires 1`, whose `sim replay` reproduced 320 turns hash for hash).
/// </summary>
[Trait("suite", "determinism")]
public class AiEmpireIntegrationTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly AgeContent Ages = TestConfigs.Ages();
    private static readonly PolityId Human = new(1);
    private static readonly PolityId Rival = new(2);
    private const int Horizon = 200;

    private static TurnExecutor Production(OrderLog orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), orders);
    }

    [Fact]
    public void OneAiEmpire_OnTheCanonicalFoundedWorld_ResearchesBuildsAndAdvances_ThroughItsOrders_AndTheLogReplays()
    {
        WorldgenConfig wg = TestConfigs.Worldgen() with { AiEmpires = 1 };
        WorldState start = WorldFounding.Found(wg, Cfg, 42);
        Assert.True(EmpireQuery.TryGetCommandSource(start, Rival, out CommandSource source) && source == CommandSource.Ai);

        var log = new OrderLog();
        TurnExecutor executor = Production(log);
        var hashes = new List<string>();
        WorldState w = start;
        long advancedAt = -1, firstCompletion = -1, firstStructure = -1;
        for (int t = 0; t < Horizon && (advancedAt < 0 || w.Clock.Turn <= advancedAt); t++)
        {
            AiOrders.Append(log, w, Cfg);
            WorldState prev = w;
            w = executor.Step(prev);
            hashes.Add(WorldHash.ComputeHex(w));
            if (firstCompletion < 0 && ResearchQuery.CompletedBetween(prev, w, Rival).Length > 0) firstCompletion = w.Clock.Turn;
            if (firstStructure < 0)
                for (int i = 0; i < w.Structures.Count; i++)
                    if (EmpireQuery.ControlsSettlement(w, Rival, w.Structures[i].Settlement)) firstStructure = w.Clock.Turn;
            if (advancedAt < 0 && AgeQuery.CurrentAge(w, Ages, Rival) == 2) advancedAt = w.Clock.Turn;
        }

        // Every order in the log is the AI's; the human Empire was never touched.
        Assert.True(log.Count > 0);
        for (int i = 0; i < log.Count; i++) Assert.Equal(Rival.Value, log[i].ActorId);
        OrderRecord[] orders = Enumerable.Range(0, log.Count).Select(i => log[i]).ToArray();

        // Research: its first target is A2's core ancestor, then the core itself (ages.json: cereal_cultivation).
        OrderRecord[] targets = orders.Where(o => o.Kind == OrderKind.SetResearchTarget).ToArray();
        Assert.Equal(("grinding_stone", 0L), (Research.Nodes[Research.IndexOf(new ResearchNodeId(targets[0].TargetId))].Id, targets[0].Turn));
        Assert.Equal("cereal_cultivation", Research.Nodes[Research.IndexOf(new ResearchNodeId(targets[1].TargetId))].Id);
        Assert.True(firstCompletion > 0, "the AI completed no research node");
        Assert.True(ResearchQuery.IsCompleted(w, Rival, Research.Nodes[Research.IndexOfId("cereal_cultivation")].Key));

        // Construction: granaries first, built by ConstructionSystem from the AI's EnqueueConstruction orders.
        OrderRecord firstBuild = orders.First(o => o.Kind == OrderKind.EnqueueConstruction);
        Assert.Equal(1.0, firstBuild.Amount);   // the granary
        Assert.True(firstStructure > firstBuild.Turn, "no AI-ordered project was ever built");

        // The Age: an AdvanceAge ORDER, applied the next turn (AgeTransitionSystem; decision turn + 1).
        OrderRecord advance = Assert.Single(orders, o => o.Kind == OrderKind.AdvanceAge);
        Assert.Equal((2, (double)Ages.Surges[0].Key), (advance.TargetId, advance.Amount));
        Assert.Equal(advance.Turn + 1, advancedAt);
        Assert.Equal(1, AgeQuery.CurrentAge(w, Ages, Human));   // the player is never auto-advanced
        // MEASURED (seed 42, canonical world): research targets set at turns 0 and 19, the first granary ordered
        // at turn 9, the advance decided at turn 141 and in force at 142.
        Assert.Equal((19L, 9L, 141L, 142L), (targets[1].Turn, firstBuild.Turn, advance.Turn, advancedAt));

        // REPLAY: a fresh founding, a fresh executor, the same log — and no AI producer — reproduces every turn.
        WorldState replayed = WorldFounding.Found(wg, Cfg, 42);
        TurnExecutor replay = Production(log);
        OrderValidation.ValidateAgainstWorld(log, replayed);
        for (int t = 0; t < hashes.Count; t++)
        {
            replayed = replay.Step(replayed);
            Assert.Equal(hashes[t], WorldHash.ComputeHex(replayed));
        }
    }
}
