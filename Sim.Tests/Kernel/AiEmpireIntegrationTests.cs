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
///
/// ADR-033 B (S3) RE-DERIVATION: the AI's research goal is now the next Age's core closure UNION the
/// prerequisite closures of the capability-gated actions it uses (next road class, tax gate, university).
/// S2's core-only goal reached the Age at 142 and never a road class or the tax gate in 320 turns; the
/// union reaches the road (order 246) and the tax gate (order 305) BEFORE the Age, which now comes at 382.
/// ADR-033 D5 FOLLOW-UP (S4): the goals are worked SEQUENTIALLY — the goal with the cheapest remaining closure,
/// its cheapest available node — road order 141, Age 2 in force 236, levy order 547. MEASURED on this tree
/// (Release) by this harness and by `sim run --founded --seed 42 --turns 400 --ai-empires 1` (turn-400 hash
/// d0502692…).
/// </summary>
[Trait("suite", "determinism")]
public class AiEmpireIntegrationTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly AgeContent Ages = TestConfigs.Ages();
    private static readonly PolityId Human = new(1);
    private static readonly PolityId Rival = new(2);
    private const int Horizon = 600;

    private static TurnExecutor Production(OrderLog orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), orders);
    }

    /// <summary>The AI world run until its first levy (or the horizon): the log, every turn's hash, and the
    /// measured turns — Age 2 in force, Age 3 in force (H2), the first positive rate, the first road row, the first
    /// completion and the first structure.</summary>
    private sealed record AiRun(OrderLog Log, List<string> Hashes, WorldState Final,
        long Age2At, long Age3At, long FirstTax, long FirstRoad, long FirstCompletion, long FirstStructure);

    private static AiRun RunUntilFirstLevy(SimConfig cfg)
    {
        WorldgenConfig wg = TestConfigs.Worldgen() with { AiEmpires = 1 };
        var log = new OrderLog();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())), log);
        var hashes = new List<string>();
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        long age2 = -1, age3 = -1, firstCompletion = -1, firstStructure = -1, firstRoad = -1, firstTax = -1;
        for (int t = 0; t < Horizon && (age2 < 0 || w.Clock.Turn <= age2 || firstTax < 0); t++)
        {
            AiOrders.Append(log, w, cfg);
            WorldState prev = w;
            w = executor.Step(prev);
            hashes.Add(WorldHash.ComputeHex(w));
            if (firstCompletion < 0 && ResearchQuery.CompletedBetween(prev, w, Rival).Length > 0) firstCompletion = w.Clock.Turn;
            if (firstStructure < 0)
                for (int i = 0; i < w.Structures.Count; i++)
                    if (EmpireQuery.ControlsSettlement(w, Rival, w.Structures[i].Settlement)) firstStructure = w.Clock.Turn;
            if (age2 < 0 && AgeQuery.CurrentAge(w, Ages, Rival) == 2) age2 = w.Clock.Turn;
            if (age3 < 0 && AgeQuery.CurrentAge(w, Ages, Rival) == 3) age3 = w.Clock.Turn;
            if (firstRoad < 0)
                for (int i = 0; i < w.RoadDevelopments.Count; i++)
                    if (w.RoadDevelopments[i].Polity == Rival) { firstRoad = w.Clock.Turn; break; }
            if (firstTax < 0)
                for (int i = 0; i < w.TaxPolicies.Count; i++)
                    if (w.TaxPolicies[i].Polity == Rival && w.TaxPolicies[i].Rate > 0.0) { firstTax = w.Clock.Turn; break; }
        }
        return new AiRun(log, hashes, w, age2, age3, firstTax, firstRoad, firstCompletion, firstStructure);
    }

    /// <summary>
    /// H2 ATTRIBUTION CONTROL (2026-10-05): the levy pin moved 367/368 -> 463/464 for the A3 tax gate ALONE. With
    /// only sim.json governance.taxationMinAge removed (<see cref="TestConfigs.PreTaxAge"/>) the AI levies at the
    /// pre-H2 turns again — everything before the first levy is untaxed, so the H2 pressure model cannot reach it —
    /// and it does so while still in A2 (the defect the Age gate fixes, MEASURED in the playtest baseline §4.3).
    /// </summary>
    [Fact]
    public void WithTheTaxAgeGateStripped_TheAiLeviesAtThePreH2Turn_InA2()
    {
        AiRun run = RunUntilFirstLevy(TestConfigs.PreTaxAge(Cfg));
        OrderRecord tax = Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i]).First(o => o.Kind == OrderKind.SetTaxRate);
        // ADR-035 RE-PIN (2026-10-07, the founding-turn harvest; MEASURED): 367/368 -> 362/363.
        Assert.Equal((362L, 363L), (tax.Turn, run.FirstTax));
        Assert.Equal(-1L, run.Age3At);   // levied in A2: exactly the pre-H2 defect
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
        long advancedAt = -1, a3At = -1, firstCompletion = -1, firstStructure = -1, firstRoad = -1, firstTax = -1;
        for (int t = 0; t < Horizon && (advancedAt < 0 || w.Clock.Turn <= advancedAt || firstTax < 0); t++)
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
            if (a3At < 0 && AgeQuery.CurrentAge(w, Ages, Rival) == 3) a3At = w.Clock.Turn;
            if (firstRoad < 0)
                for (int i = 0; i < w.RoadDevelopments.Count; i++)
                    if (w.RoadDevelopments[i].Polity == Rival) { firstRoad = w.Clock.Turn; break; }
            if (firstTax < 0)
                for (int i = 0; i < w.TaxPolicies.Count; i++)
                    if (w.TaxPolicies[i].Polity == Rival && w.TaxPolicies[i].Rate > 0.0) { firstTax = w.Clock.Turn; break; }
        }

        // Every order in the log is the AI's; the human Empire was never touched.
        Assert.True(log.Count > 0);
        for (int i = 0; i < log.Count; i++) Assert.Equal(Rival.Value, log[i].ActorId);
        OrderRecord[] orders = Enumerable.Range(0, log.Count).Select(i => log[i]).ToArray();

        // Research (goal-SEQUENTIAL, ADR-033 D5 follow-up): the cheapest REMAINING goal at turn 0 is the track
        // road, so the first two targets are its ancestors knapping_oldowan and ground_stone_early (S3's union
        // started grinding_stone, knapping_oldowan; S2's core-only goal grinding_stone, cereal_cultivation).
        OrderRecord[] targets = orders.Where(o => o.Kind == OrderKind.SetResearchTarget).ToArray();
        Assert.Equal(("knapping_oldowan", 0L), (Research.Nodes[Research.IndexOf(new ResearchNodeId(targets[0].TargetId))].Id, targets[0].Turn));
        Assert.Equal("ground_stone_early", Research.Nodes[Research.IndexOf(new ResearchNodeId(targets[1].TargetId))].Id);
        Assert.True(firstCompletion > 0, "the AI completed no research node");
        Assert.True(ResearchQuery.IsCompleted(w, Rival, Research.Nodes[Research.IndexOfId("cereal_cultivation")].Key));

        // Construction: granaries first, built by ConstructionSystem from the AI's EnqueueConstruction orders.
        OrderRecord firstBuild = orders.First(o => o.Kind == OrderKind.EnqueueConstruction);
        Assert.Equal(1.0, firstBuild.Amount);   // the granary
        Assert.True(firstStructure > firstBuild.Turn, "no AI-ordered project was ever built");

        // The Age: an AdvanceAge ORDER, applied the next turn (AgeTransitionSystem; decision turn + 1).
        OrderRecord advance = Assert.Single(orders, o => o.Kind == OrderKind.AdvanceAge && o.TargetId == 2);
        Assert.Equal((2, (double)Ages.Surges[0].Key), (advance.TargetId, advance.Amount));
        Assert.Equal(advance.Turn + 1, advancedAt);
        Assert.Equal(1, AgeQuery.CurrentAge(w, Ages, Human));   // the player is never auto-advanced
        // Roads and taxation are no longer dead for the AI (ADR-033 B): it researches the track road's closure,
        // orders a road (DevelopRoads, applied the next turn), then reaches the tax gate and levies.
        OrderRecord road = orders.First(o => o.Kind == OrderKind.DevelopRoads);
        OrderRecord tax = orders.First(o => o.Kind == OrderKind.SetTaxRate);
        Assert.True(ResearchQuery.IsCompleted(w, Rival, Research.Nodes[Research.IndexOfId("track_road")].Key));
        Assert.True(Governance.CanLevyTax(w, Cfg, Rival));
        // MEASURED (seed 42, canonical world, goal-sequential): research targets set at turns 0 and 26, the first
        // granary ordered at turn 9, the first road ordered at 141 (a RoadDevelopments row at 142), the advance
        // decided at turn 235 and in force at 236, the first levy ordered at 547 (a positive rate at 548).
        // S3 (goal union): 19, 9, road 246/247, Age 381/382, levy 305/306. S2 (core-only): 19, 9, no road, 141/142, no tax.
        // R4 RE-PIN (2026-10-04, the forager layer — wild-food harvests until farming is known slow the RP curve;
        // MEASURED on this tree by the agent writing this line): advance 235/236 -> 238/239; 26 and 9 unchanged.
        // ADR-035 RE-PIN (2026-10-07, one cause: the founding-turn harvest; MEASURED): advance 238/239 -> 233/234.
        Assert.Equal((26L, 9L, 233L, 234L), (targets[1].Turn, firstBuild.Turn, advance.Turn, advancedAt));
        // R1 RE-PIN (2026-10-03; research-gated recipes — no pottery or bronze before their nodes — move the
        // population and research-point trajectory): levy 547/548 -> 537/538; targets, granary, road, Age unchanged.
        // R4 RE-PIN (2026-10-04, the forager layer; MEASURED): road 141/142 -> 143/144, levy 537/538 -> 546/547.
        // R5 RE-PIN (2026-10-04, the dedicated Taxation civic is the single tax gate; MEASURED on this tree by the
        // agent writing this line): levy 546/547 -> 367/368 — the Taxation closure (token_counting, stamp_seal,
        // proto_writing, taxation) is cheaper than the old cheapest alternative's; road, Age, targets unchanged.
        // H2 RE-PIN (2026-10-05, Director §7: the tax edict is operational only from A3, sim.json
        // governance.taxationMinAge; MEASURED on this tree by the agent writing this line, and identically by
        // `sim run --founded --seed 42 --turns 600 --ai-empires 1`): levy 367/368 -> 463/464. The AI completed the
        // Taxation civic in A2 as before but the edict is refused until it ENTERS A3 (AdvanceAge decided 462, in
        // force 463); it levies on its first A3 turn. Road, Age 2, targets and granary unchanged. The control
        // WithTheTaxAgeGateStripped_TheAiLeviesAtThePreH2Turn_InA2 returns 367/368 with the Age half stripped.
        // ADR-035 RE-PIN (2026-10-07, the founding-turn harvest; MEASURED): road 143/144 -> 140/141, levy 463/464 ->
        // 458/459 (still on the AI's first A3 turn).
        Assert.Equal((140L, 141L, 458L, 459L), (road.Turn, firstRoad, tax.Turn, firstTax));
        Assert.Equal(458L, a3At);                            // the AI entered A3 (decided 457; ADR-035: 463 -> 458) ...
        Assert.True(tax.Turn >= a3At, "the AI levied before entering A3");   // ... and levied only after it

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
