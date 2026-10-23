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
/// (2026-10-06: the turns in this summary are the S3/S4 history. The current pins, with their dated re-pin history,
/// are asserted in the test bodies below: road 143/144, Age 2 decided 238 / in force 239, and the first levy 463/464
/// under the Taxation civic + Age III gate, with the stripped-Age control at 367/368.)
/// (2026-10-11, M5 S1c: the AI also EXPANDS through the player's Found Settlement order — AiFoundingPolicy, ADR-036 §8 —
/// which moves every pin after its first founding's delivery at turn 36; WithAiFoundingOff_EveryAiPinReturnsItsPreS1cValue
/// is the attribution control that returns every pre-S1c value with only the policy's switch off.)
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

    private static TurnExecutor Production(OrderLog orders, WorldgenConfig worldgen, SimConfig cfg)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen)), orders);
    }

    /// <summary>The AI world run until its first levy (or the horizon): the log, every turn's hash, and the
    /// measured turns — Age 2 in force, Age 3 in force (H2), the first positive rate, the first road row, the first
    /// completion and the first structure.</summary>
    private sealed record AiRun(OrderLog Log, List<string> Hashes, WorldState Final,
        long Age2At, long Age3At, long FirstTax, long FirstRoad, long FirstCompletion, long FirstStructure);

    /// <remarks>Batch 3 (D-054, W1 fix F2): the run founds, steps and orders with <paramref name="worldgen"/>. Both
    /// callers are ATTRIBUTION CONTROLS whose SimConfig twins (PreTaxAge, PreAiFounding) strip every newer sim layer
    /// (the R4 rule), so they pass <see cref="TestConfigs.PreD054Worldgen()"/> — the flat, pre-batch-3 world — and the
    /// worldgen layer is stripped with them; founded on the shipped cylinder their literals moved (measured: H2
    /// 337/338 -> 336/337, S1c (26, 9, 232, 233) -> (25, 5, 233, 234)), which is the cylinder, not the layer they
    /// attribute.</remarks>
    private static AiRun RunUntilFirstLevy(SimConfig cfg, WorldgenConfig worldgen)
    {
        WorldgenConfig wg = worldgen with { AiEmpires = 1 };
        var log = new OrderLog();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen)), log);
        var hashes = new List<string>();
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        long age2 = -1, age3 = -1, firstCompletion = -1, firstStructure = -1, firstRoad = -1, firstTax = -1;
        for (int t = 0; t < Horizon && (age2 < 0 || w.Clock.Turn <= age2 || firstTax < 0); t++)
        {
            AiOrders.Append(log, w, cfg, worldgen);
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
        AiRun run = RunUntilFirstLevy(TestConfigs.PreTaxAge(Cfg), TestConfigs.PreD054Worldgen());
        OrderRecord tax = Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i]).First(o => o.Kind == OrderKind.SetTaxRate);
        // ADR-035 RE-PIN (2026-10-07, the founding-turn harvest; MEASURED): 367/368 -> 362/363.
        // ADR-035 §6 RE-PIN (2026-10-07, P-F0: the founding death remainder seeded at 0.5; MEASURED): 362/363 -> 366/367.
        // ADR-035 §7 RE-PIN (2026-10-07, P-F2: founding cohort noise at demographic scale; MEASURED): 366/367 -> 365/366.
        // D-051 1(b) RE-PIN (2026-10-10, time-averaged decade weather; MEASURED): 365/366 -> 357/358.
        // S1c RE-PIN (2026-10-11, one cause: the AI founding policy — the AI founds through OrderKind 9 at turns 35,
        // 71, 107, 143, 179, 215 and 286 before this levy; which colony effect brings the Taxation closure forward is
        // not isolated here; MEASURED on this tree by the agent writing this line): 357/358 -> 337/338. ATTRIBUTION: with only
        // the switch off (TestConfigs.PreAiFounding) WithAiFoundingOff_EveryAiPinReturnsItsPreS1cValue returns 357/358.
        Assert.Equal((337L, 338L), (tax.Turn, run.FirstTax));
        Assert.Equal(-1L, run.Age3At);   // levied in A2: exactly the pre-H2 defect
    }

    /// <summary>
    /// S1c ATTRIBUTION CONTROL (2026-10-11; ADR-036 §8): every pin of this class moved for ONE cause, the AI founding
    /// policy (the AI's FoundSettlement orders and the colonies they found). With only sim.json
    /// colonization.aiFounding.enabled switched off (<see cref="TestConfigs.PreAiFounding"/>) the AI issues no
    /// Found Settlement order and every pre-S1c value returns exactly — the ADR-033 harness and the H2 control alike.
    /// Batch 3 (D-054): on the flat, pre-batch-3 world (<see cref="TestConfigs.PreD054Worldgen()"/>), the worldgen half
    /// of the R4 rule — the twin strips every newer layer, the cylinder included.
    /// </summary>
    [Fact]
    public void WithAiFoundingOff_EveryAiPinReturnsItsPreS1cValue()
    {
        AiRun run = RunUntilFirstLevy(TestConfigs.PreAiFounding(Cfg), TestConfigs.PreD054Worldgen());
        OrderRecord[] orders = Enumerable.Range(0, run.Log.Count).Select(i => run.Log[i]).ToArray();
        Assert.DoesNotContain(orders, o => o.Kind == OrderKind.FoundSettlement);
        OrderRecord[] targets = orders.Where(o => o.Kind == OrderKind.SetResearchTarget).ToArray();
        OrderRecord firstBuild = orders.First(o => o.Kind == OrderKind.EnqueueConstruction);
        OrderRecord advance = Assert.Single(orders, o => o.Kind == OrderKind.AdvanceAge && o.TargetId == 2);
        OrderRecord road = orders.First(o => o.Kind == OrderKind.DevelopRoads);
        OrderRecord tax = orders.First(o => o.Kind == OrderKind.SetTaxRate);
        Assert.Equal((26L, 9L, 232L, 233L), (targets[1].Turn, firstBuild.Turn, advance.Turn, run.Age2At));
        Assert.Equal((140L, 141L, 451L, 452L), (road.Turn, run.FirstRoad, tax.Turn, run.FirstTax));
        Assert.Equal(451L, run.Age3At);
        Assert.Equal(6, EmpireQuery.ControlledCount(run.Final, Rival));    // the turn-zero holdings, no colony

        AiRun stripped = RunUntilFirstLevy(TestConfigs.PreAiFounding(TestConfigs.PreTaxAge(Cfg)), TestConfigs.PreD054Worldgen());
        OrderRecord strippedTax = Enumerable.Range(0, stripped.Log.Count).Select(i => stripped.Log[i]).First(o => o.Kind == OrderKind.SetTaxRate);
        Assert.Equal((357L, 358L), (strippedTax.Turn, stripped.FirstTax));
        Assert.Equal(-1L, stripped.Age3At);
    }

    /// <summary>The pins <see cref="OneAiEmpire_OnTheCanonicalFoundedWorld_ResearchesBuildsAndAdvances_ThroughItsOrders_AndTheLogReplays"/>
    /// asserts (research target 2, first build, AdvanceAge decided / in force, first road order / row, first levy order /
    /// rate, A3 in force, the Found Settlement stamps, the polity's holdings) — measured by <see cref="RunCanonicalAi"/>.</summary>
    internal sealed record CanonicalAiPins(long Target1, long FirstBuild, long Advance, long AdvancedAt, long Road, long FirstRoad,
        long Tax, long FirstTax, long A3At, long[] Founds, int Controlled, string[]? FirstTargets = null);

    /// <summary>
    /// The canonical AI run (seed 42, 1024², one AI Empire, the production pipeline, AiOrders before every step, run until
    /// the first levy) with every STRUCTURAL assert of the test — every order the AI's, the research goal sequence, the
    /// granary first, AdvanceAge applied the next turn, road then tax, the levy only in A3, the log replays without the AI
    /// producer — and the turn pins returned for the caller to assert. <paramref name="worldgen"/> is the world founded,
    /// stepped and judged: the shipped config for the live test, TestConfigs.PreCylinderWorldgen for its batch-3
    /// attribution twin (<see cref="OneAiEmpire_WithTheCylinderOff_EveryPinReturnsItsBatch2Value"/>).
    /// </summary>
    internal static CanonicalAiPins RunCanonicalAi(WorldgenConfig worldgen, SimConfig? cfgOverride = null)
    {
        SimConfig cfg = cfgOverride ?? Cfg;
        ResearchContent research = cfg.Research!;   // ADR-045: the content the run uses (the pre-tranche twin, or the shipped)
        WorldgenConfig wg = worldgen with { AiEmpires = 1 };
        WorldState start = WorldFounding.Found(wg, cfg, 42);
        Assert.Equal(worldgen.Topology is { WrapX: true }, start.Terrain!.WrapX);
        Assert.True(EmpireQuery.TryGetCommandSource(start, Rival, out CommandSource source) && source == CommandSource.Ai);

        var log = new OrderLog();
        TurnExecutor executor = Production(log, worldgen, cfg);
        var hashes = new List<string>();
        WorldState w = start;
        long advancedAt = -1, a3At = -1, firstCompletion = -1, firstStructure = -1, firstRoad = -1, firstTax = -1;
        for (int t = 0; t < Horizon && (advancedAt < 0 || w.Clock.Turn <= advancedAt || firstTax < 0); t++)
        {
            AiOrders.Append(log, w, cfg, worldgen);
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

        // Research (goal-SEQUENTIAL, ADR-033 D5 follow-up): the first two targets, set at turn 0 and on the first completion,
        // are returned for the caller to pin (they depend on the research content; ADR-045).
        OrderRecord[] targets = orders.Where(o => o.Kind == OrderKind.SetResearchTarget).ToArray();
        Assert.Equal(0L, targets[0].Turn);
        string[] firstTargets = [research.Nodes[research.IndexOf(new ResearchNodeId(targets[0].TargetId))].Id,
                                 research.Nodes[research.IndexOf(new ResearchNodeId(targets[1].TargetId))].Id];
        Assert.True(firstCompletion > 0, "the AI completed no research node");
        Assert.True(ResearchQuery.IsCompleted(w, Rival, research.Nodes[research.IndexOfId("cereal_cultivation")].Key));

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
        Assert.True(ResearchQuery.IsCompleted(w, Rival, research.Nodes[research.IndexOfId("track_road")].Key));
        Assert.True(Governance.CanLevyTax(w, cfg, Rival));
        Assert.True(a3At > 0, "the AI never entered A3 before its first levy");
        Assert.True(tax.Turn >= a3At, "the AI levied before entering A3");   // the levy only once the AI is in A3
        OrderRecord[] founds = orders.Where(o => o.Kind == OrderKind.FoundSettlement).ToArray();

        // REPLAY: a fresh founding, a fresh executor, the same log — and no AI producer — reproduces every turn.
        WorldState replayed = WorldFounding.Found(wg, cfg, 42);
        TurnExecutor replay = Production(log, worldgen, cfg);
        OrderValidation.ValidateAgainstWorld(log, replayed);
        for (int t = 0; t < hashes.Count; t++)
        {
            replayed = replay.Step(replayed);
            Assert.Equal(hashes[t], WorldHash.ComputeHex(replayed));
        }
        return new CanonicalAiPins(targets[1].Turn, firstBuild.Turn, advance.Turn, advancedAt, road.Turn, firstRoad, tax.Turn,
            firstTax, a3At, founds.Select(o => o.Turn).ToArray(), EmpireQuery.ControlledCount(w, Rival), firstTargets);
    }

    [Fact]
    public void OneAiEmpire_OnTheCanonicalFoundedWorld_ResearchesBuildsAndAdvances_ThroughItsOrders_AndTheLogReplays()
    {
        CanonicalAiPins p = RunCanonicalAi(TestConfigs.Worldgen());
        // MEASURED (seed 42, canonical world, goal-sequential): research targets set at turns 0 and 26, the first
        // granary ordered at turn 9, the first road ordered at 141 (a RoadDevelopments row at 142), the advance
        // decided at turn 235 and in force at 236, the first levy ordered at 547 (a positive rate at 548).
        // S3 (goal union): 19, 9, road 246/247, Age 381/382, levy 305/306. S2 (core-only): 19, 9, no road, 141/142, no tax.
        // R4 RE-PIN (2026-10-04, the forager layer — wild-food harvests until farming is known slow the RP curve;
        // MEASURED on this tree by the agent writing this line): advance 235/236 -> 238/239; 26 and 9 unchanged.
        // ADR-035 RE-PIN (2026-10-07, one cause: the founding-turn harvest; MEASURED): advance 238/239 -> 233/234.
        // ADR-035 §6 RE-PIN (2026-10-07, P-F0: the founding death remainder seeded at 0.5; MEASURED): advance 233/234 -> 236/237.
        // ADR-035 §7 RE-PIN (2026-10-07, P-F2: founding cohort noise at demographic scale; MEASURED): advance 236/237 -> 235/236.
        // D-051 1(b) RE-PIN (2026-10-10, time-averaged decade weather; MEASURED): advance 235/236 -> 232/233; 26 and 9 unchanged.
        // S1c RE-PIN (2026-10-11, one cause: the AI founding policy — six AI colonies, ordered at 35, 71, 107, 143, 179
        // and 215, stand before the advance; which colony effect brings the Age closure forward is not isolated here;
        // MEASURED on this tree by the agent writing this line): advance 232/233 -> 221/222; 26 and 9 unchanged (before the first
        // founding's delivery at 36). ATTRIBUTION: WithAiFoundingOff_EveryAiPinReturnsItsPreS1cValue returns 232/233.
        // D-053 RE-PIN (integration, 2026-10-14; MEASURED on this tree by the agent writing this line, in-test harness):
        // first build 9 -> 4; targets 26, advance 221/222 and every later pin of this test (road 134/135, levy 423/424, A3
        // 423, the eight foundings, 14 settlements) unchanged. CAUSE: the construction purse (D-053 §1) — the AI orders its
        // first granary at turn 4 at a settlement whose Empire's purse holds the materials, where at turn 9 one settlement's
        // own stock first did. ATTRIBUTION: the batch off (TestConfigs.PreD053) returns 9; the purse off alone returns 9;
        // relief off alone and the granary effects off alone return 4 — the purse is the whole of the move.
        // BATCH 3 / D-054 RE-PIN (integration R1, 2026-10-14; MEASURED on this tree by the agent writing this line, in-test
        // harness): research target 2 26 -> 25, first build 4 -> 2; advance 221/222 unchanged. CAUSE: the CYLINDER alone
        // (worldgen.json topology.wrapX — the 1024² world regenerates x-periodic, so the AI's land, purse and research-point
        // curve differ from turn 0). ATTRIBUTION: OneAiEmpire_WithTheCylinderOff_EveryPinReturnsItsBatch2Value (the cylinder
        // off alone, TestConfigs.PreCylinderWorldgen, WS1/WS2 on) returns (26, 4, 221, 222) and every later batch-2 pin; the
        // sim half off alone on the cylinder (TestConfigs.PreD054Sim, MEASURED) returns the NEW values — incremental
        // construction and allocation ratios move nothing here (the AI's purse prudence finishes every head in its delivery
        // step; IncrementalConstructionAttributionTests' AI-world control).
        // M6 RE-PIN (2026-10-23, ONE re-pin naming every cause; MEASURED on the merged tree — Track CP, Track EX with the
        // coordinator's rulings, Track TREE — by the agent writing this line, in-test harness): research target 2 25 -> 0 (the second
        // target is the CIVIC lane's, set on turn 0 beside the first), first build 2 unchanged, advance 221/222 -> 111/112; the
        // first two targets grinding_stone and kin_council (were knapping_oldowan and ground_stone_early). CAUSES: (TREE, ADR-045) the
        // research-content tranche — cereal_cultivation re-scored 3,520 -> 880 RP makes the CORE goal the cheapest remainder at
        // turn 0 (AiPolicyTests), so the AI knows cultivation and meets the Neolithic far sooner; (CP, ADR-043) Civics Points
        // fund the civic side of the Age closure on their own lane; (EX, ADR-044) the AI founds only on KNOWN sites (after
        // Explore orders), prospects its settlements (grain), and its concealed ores are unworked until surveyed.
        // ATTRIBUTION: OneAiEmpire_WithTheWholeM6SliceOff_EveryPinReturnsItsBatch4Value (TestConfigs.PreM6) returns every
        // pre-M6 pin; OneAiEmpire_WithTheDiscoveryLayerAndTheTrancheOff_EveryPinReturnsItsTrackCpValue returns CP's alone.
        // ADR-046 RE-PIN (2026-10-23, the M6 review batch; MEASURED on this tree by the agent writing this line, in-test
        // harness): first build 2 -> 3; targets and the advance unchanged. CAUSE: the Prospect bill alone (ADR-046 §2 — the
        // AI's first survey now spends 10 stone, the material its first granary waited on). ATTRIBUTION:
        // OneAiEmpire_WithTheCheapestRouteOff_TheLevyAndA3ReturnToTheirPreFixTurns (TestConfigs.PreAiRoutes, the bill kept)
        // returns 3 here as well.
        Assert.Equal(["grinding_stone", "kin_council"], p.FirstTargets!);
        Assert.Equal((0L, 3L, 111L, 112L), (p.Target1, p.FirstBuild, p.Advance, p.AdvancedAt));
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
        // ADR-035 §6 RE-PIN (2026-10-07, P-F0: the founding death remainder seeded at 0.5; MEASURED): road 140/141 -> 141/142, levy 458/459 -> 462/463 (A3 entered 462).
        // ADR-035 §7 RE-PIN (2026-10-07, P-F2: founding cohort noise at demographic scale; MEASURED): levy 462/463 -> 461/462 (A3 entered 461).
        // D-051 1(b) RE-PIN (2026-10-10, time-averaged decade weather; MEASURED): road 141/142 -> 140/141, levy 461/462 -> 451/452.
        // S1c RE-PIN (2026-10-11, one cause: the AI founding policy; MEASURED on this tree by the agent writing this
        // line): road 140/141 -> 134/135, levy 451/452 -> 423/424 (still on the AI's first A3 turn: A3 451 -> 423).
        // ATTRIBUTION: WithAiFoundingOff_EveryAiPinReturnsItsPreS1cValue returns 140/141, 451/452 and A3 at 451.
        // BATCH 3 / D-054 RE-PIN (integration R1, 2026-10-14; MEASURED on this tree by the agent writing this line, in-test
        // harness): levy 423/424 -> 422/423 (still on the AI's first A3 turn: A3 423 -> 422); road 134/135 unchanged.
        // CAUSE: the cylinder alone; ATTRIBUTION as above (the twin returns 134/135, 423/424, A3 423).
        // ADR-043 RE-PIN (2026-10-22, D-062 Track CP; MEASURED on this tree by the agent writing this line, in-test harness):
        // levy 422/423 -> 440/441 (still on the AI's first A3 turn: A3 422 -> 440); road 134/135, targets, first build and
        // the advance unchanged. CAUSE: the two research lanes — the Taxation civic (and every civic in the Age closure) is
        // now funded by Civics Points on its own lane instead of by the one RP pool, so the civic side of the A3 closure
        // completes on CP pace. ATTRIBUTION: OneAiEmpire_WithTheCylinderOff_EveryPinReturnsItsBatch2Value now runs on the
        // pre-CP world (TestConfigs.PreTrackCp, the R4 rule) and returns 423/424; CivicsPointsAttributionTests proves Track
        // CP is the whole of the AI world's turn-300 move.
        // M6 RE-PIN (2026-10-23; MEASURED on the merged tree, the causes and controls as above): road 134/135 -> 166/167,
        // levy 422/423 (Track CP alone: 440/441) -> 579/580 (A3 422 -> 579).
        // ADR-046 RE-PIN (2026-10-23; MEASURED on this tree by the agent writing this line, in-test harness): levy 579/580 ->
        // 407/408, A3 579 -> 407 (still the AI's first A3 turn); road 166/167 unchanged. CAUSE: the cheapest-route goal
        // closures alone (ADR-046 §1 — the AI no longer researches every alternative of an OR its goal needs, so bronze and
        // the A3 closure come ~170 turns sooner). ATTRIBUTION: the routes-off twin returns 579/580 and A3 579 with the bill
        // kept (OneAiEmpire_WithTheCheapestRouteOff_TheLevyAndA3ReturnToTheirPreFixTurns).
        Assert.Equal((166L, 167L, 407L, 408L), (p.Road, p.FirstRoad, p.Tax, p.FirstTax));
        Assert.Equal(407L, p.A3At);                          // the AI entered A3 (ADR-035: 463 -> 458 -> 462 -> 461; D-051 1(b): 461 -> 451; S1c: 451 -> 423; batch 3: 423 -> 422; CP: 422 -> 440; M6 merged: 440 -> 579; ADR-046: 579 -> 407)

        // S1c (ADR-036 §8): the AI EXPANDS through the player's Found Settlement order — one per 350 years at most, each
        // a colony it controls. MEASURED on this tree: eight orders by the first levy, the polity holding 6 + 8 = 14
        // settlements (ages.json a4_expansion's 14 met from turn 358). Batch 3 (D-054): unchanged on the cylinder.
        // ADR-043 RE-PIN (2026-10-22, D-062 Track CP; MEASURED): the later first levy (440) lets a ninth founding (stamped
        // 428, the founding cadence unchanged) stand before it: 8 -> 9 orders, 14 -> 15 settlements.
        // M6 RE-PIN (2026-10-23; MEASURED on the merged tree, causes CP, EX and TREE as above): the foundings 35, 71, 107, 143,
        // 179, 215, 286, 357, 428 -> 38, 78, 114, 151, 189, 225, 296, 367, 438, 509 (ten orders, 16 settlements: the later levy lets a tenth
        // founding stand) — under the fog the AI founds only on a site it KNOWS: when its policy is due and
        // finds none it explores (OrderKind 14) and founds a few turns later; the run ends at the first levy (579).
        // ADR-046 RE-PIN (2026-10-23; MEASURED): the run ends at the first levy (407), so the foundings stamped 438 and 509 no
        // longer stand before it: ten orders -> eight (the cadence unchanged), 16 settlements -> 14.
        Assert.Equal([38L, 78L, 114L, 151L, 189L, 225L, 296L, 367L], p.Founds);
        Assert.Equal(14, p.Controlled);
    }

    /// <summary>ADR-046 (2026-10-23) — THE ATTRIBUTION CONTROL of the review batch's moves of the pins above: the same run with
    /// ONLY the cheapest-route goal closure off (TestConfigs.PreAiRoutes — the union-over-OR closures of ADR-033 B; the new
    /// Prospect bill kept) returns the pre-fix levy, A3, foundings and holdings, so the route fix is the whole of their
    /// move; its first build is 3, so the bill is the whole of that one.</summary>
    [Fact]
    public void OneAiEmpire_WithTheCheapestRouteOff_TheLevyAndA3ReturnToTheirPreFixTurns()
    {
        CanonicalAiPins p = RunCanonicalAi(TestConfigs.Worldgen(), TestConfigs.PreAiRoutes(Cfg));
        Assert.Equal(["grinding_stone", "kin_council"], p.FirstTargets!);
        Assert.Equal((0L, 3L, 111L, 112L), (p.Target1, p.FirstBuild, p.Advance, p.AdvancedAt));
        Assert.Equal((166L, 167L, 579L, 580L), (p.Road, p.FirstRoad, p.Tax, p.FirstTax));
        Assert.Equal(579L, p.A3At);
        Assert.Equal([38L, 78L, 114L, 151L, 189L, 225L, 296L, 367L, 438L, 509L], p.Founds);
        Assert.Equal(16, p.Controlled);
    }

    /// <summary>M6 (D-062; ADR-044, ADR-045) — THE ATTRIBUTION CONTROL of the Track-CP part of the re-pins above: the same
    /// run with the discovery layer AND the research-content tranche off (TestConfigs.PreDiscovery, TestConfigs.PreTreeTranche;
    /// Track CP on) returns every Track-CP value — so EX and TREE together are the rest of the M6 move of these pins.</summary>
    [Fact]
    public void OneAiEmpire_WithTheDiscoveryLayerAndTheTrancheOff_EveryPinReturnsItsTrackCpValue()
    {
        CanonicalAiPins p = RunCanonicalAi(TestConfigs.Worldgen(), TestConfigs.PreTreeTranche(TestConfigs.PreDiscovery(Cfg)));
        Assert.Equal((25L, 2L, 221L, 222L), (p.Target1, p.FirstBuild, p.Advance, p.AdvancedAt));
        Assert.Equal((134L, 135L, 440L, 441L), (p.Road, p.FirstRoad, p.Tax, p.FirstTax));
        Assert.Equal(440L, p.A3At);
        Assert.Equal([35L, 71L, 107L, 143L, 179L, 215L, 286L, 357L, 428L], p.Founds);
        Assert.Equal(15, p.Controlled);
    }

    /// <summary>
    /// BATCH 3 / D-054 ATTRIBUTION CONTROL (integration R1, 2026-10-14) for every pin of
    /// <see cref="OneAiEmpire_OnTheCanonicalFoundedWorld_ResearchesBuildsAndAdvances_ThroughItsOrders_AndTheLogReplays"/> the
    /// batch moved: the SAME run with ONLY the cylinder switched off (TestConfigs.PreCylinderWorldgen — the flat world; the
    /// sim half, WS1 incremental construction and WS2 allocation ratios, stays ON) returns every batch-2 value, and every
    /// structural assert of the run holds there too. So the cylinder is the whole of the move.
    /// </summary>
    [Fact]
    public void OneAiEmpire_WithTheCylinderOff_EveryPinReturnsItsBatch2Value()
    {
        // ADR-043 / ADR-045 / ADR-044 CAUSE BLOCK: the control runs on the pre-M6 world (TestConfigs.PreM6: Track CP, the
        // research-content tranche and the discovery layer off) — every M6 layer is newer than the cylinder, so by the R4
        // rule the older layer's twin strips them; the batch-2 values return unchanged.
        CanonicalAiPins p = RunCanonicalAi(TestConfigs.PreCylinderWorldgen(TestConfigs.Worldgen()), TestConfigs.PreM6(Cfg));
        Assert.Equal(["knapping_oldowan", "ground_stone_early"], p.FirstTargets!);
        Assert.Equal((26L, 4L, 221L, 222L), (p.Target1, p.FirstBuild, p.Advance, p.AdvancedAt));
        Assert.Equal((134L, 135L, 423L, 424L), (p.Road, p.FirstRoad, p.Tax, p.FirstTax));
        Assert.Equal(423L, p.A3At);
        Assert.Equal([35L, 71L, 107L, 143L, 179L, 215L, 286L, 357L], p.Founds);
        Assert.Equal(14, p.Controlled);
    }

    /// <summary>M6 ATTRIBUTION CONTROL (D-062; ADR-043, ADR-044, ADR-045) for every pin of
    /// <see cref="OneAiEmpire_OnTheCanonicalFoundedWorld_ResearchesBuildsAndAdvances_ThroughItsOrders_AndTheLogReplays"/> the
    /// M6 slice moved: the SAME run (the shipped cylinder) with the whole slice switched off (TestConfigs.PreM6 — the pre-M6
    /// research.json, so neither Track CP nor the tranche, and the discovery layer off) returns every batch-4 (pre-M6) value,
    /// and every structural assert of the run holds there too. So the slice is the whole of the move.</summary>
    [Fact]
    public void OneAiEmpire_WithTheWholeM6SliceOff_EveryPinReturnsItsBatch4Value()
    {
        CanonicalAiPins p = RunCanonicalAi(TestConfigs.Worldgen(), TestConfigs.PreM6Sim());
        Assert.Equal(["knapping_oldowan", "ground_stone_early"], p.FirstTargets!);
        Assert.Equal((25L, 2L, 221L, 222L), (p.Target1, p.FirstBuild, p.Advance, p.AdvancedAt));
        Assert.Equal((134L, 135L, 422L, 423L), (p.Road, p.FirstRoad, p.Tax, p.FirstTax));
        Assert.Equal(422L, p.A3At);
        Assert.Equal([35L, 71L, 107L, 143L, 179L, 215L, 286L, 357L], p.Founds);
        Assert.Equal(14, p.Controlled);
    }
}
