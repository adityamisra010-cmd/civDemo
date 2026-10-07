using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-031 (D-047 ruling 13) — the AI advances through EXACTLY the player's order pathway, proven on
/// the REAL pipeline: a world founded by WorldFounding with worldgen.aiEmpires = 1, the production
/// pipeline (pipeline.json, every system), research reached only through SetResearchTarget ORDERS
/// for both polities (no state injected), and the AI's AdvanceAge emitted by AgeAdvancePolicy into
/// the same OrderLog the player's order goes into. Asserted: the AI's order is a plain
/// OrderKind.AdvanceAge record identical in shape to the player's (AgeQuery.AdvanceOrder), it
/// passes the same CheckAdvance, and the Age changes on the NEXT turn (decision turn t, effective
/// turn t + 1) with modernization applied — the same next-turn rule as the player's.
/// </summary>
public class AiAgeAdvancementIntegrationTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly AgeContent Ages = TestConfigs.Ages();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly UnitFamilyContent Families = TestConfigs.UnitFamilies();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);
    private const int TurnCap = 600;

    private static TurnExecutor Production(OrderLog orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.DevWorldgen() with { AiEmpires = 1 })), orders);
    }

    /// <summary>The goal nodes and every node they transitively name as a prerequisite (index set).</summary>
    private static bool[] Closure(params string[] goals)
    {
        var inSet = new bool[Research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string g in goals) stack.Push(Research.IndexOfId(g));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (inSet[i]) continue;
            inSet[i] = true;
            foreach (int p in Research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        return inSet;
    }

    /// <summary>A research ORDER toward the goal set: the lowest-index available node of the closure,
    /// issued only when the polity has no target. Null when nothing is needed.</summary>
    private static OrderRecord? ResearchOrder(WorldState w, PolityId polity, bool[] closure)
    {
        if (ResearchQuery.TryGetTarget(w, polity, out _)) return null;
        bool[] available = ResearchQuery.AvailableMask(Research, ResearchQuery.CompletedMask(w, Research, polity));
        for (int i = 0; i < available.Length; i++)
            if (closure[i] && available[i])
                return OrderRecord.From(w.Clock.Turn, polity, OrderKind.SetResearchTarget, Research.Nodes[i].Key.Value, 0.0);
        return null;
    }

    [Fact]
    public void FoundedAiEmpire_OnTheRealPipeline_AdvancesViaAnAdvanceAgeOrder_IdenticalInShapeToThePlayers_AppliedNextTurn()
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42);
        Assert.True(EmpireQuery.TryGetCommandSource(w, Rival, out CommandSource rivalSource));
        Assert.Equal(CommandSource.Ai, rivalSource);
        Assert.True(EmpireQuery.TryGetCommandSource(w, Player, out CommandSource playerSource));
        Assert.NotEqual(CommandSource.Ai, playerSource);

        bool[] closure = Closure("cereal_cultivation", "pottery_open_fired");
        var orders = new OrderLog();
        TurnExecutor ex = Production(orders);
        int surge = Ages.Surges[0].Key;
        OrderRecord? aiOrder = null, playerOrder = null;
        WorldState? aiDecided = null, playerDecided = null;

        for (int t = 0; t < TurnCap && (aiOrder is null || playerOrder is null || AgeQuery.CurrentAge(w, Ages, Player) < 2
                                         || AgeQuery.CurrentAge(w, Ages, Rival) < 2); t++)
        {
            foreach (PolityId p in new[] { Player, Rival })
                if (ResearchOrder(w, p, closure) is { } r) orders.Append(r);

            // The AI: whatever the policy returns goes into the SAME log, unaltered.
            OrderRecord[] ai = AgeAdvancePolicy.OrdersForAi(w, Ages);
            foreach (OrderRecord o in ai)
            {
                Assert.Equal(Rival.Value, o.ActorId); // the player is never auto-advanced
                orders.Append(o);
                if (aiOrder is null) { aiOrder = o; aiDecided = w; }
            }
            // The player: the click path builds its order with AgeQuery.AdvanceOrder.
            if (playerOrder is null && AgeQuery.IsEligible(w, Ages, Player))
            {
                OrderRecord o = AgeQuery.AdvanceOrder(w, Player, AgeQuery.CurrentAge(w, Ages, Player) + 1, surge);
                orders.Append(o);
                playerOrder = o;
                playerDecided = w;
            }
            w = ex.Step(w);
        }

        Assert.NotNull(aiOrder);
        Assert.NotNull(playerOrder);
        OrderRecord a = aiOrder!.Value, pl = playerOrder!.Value;

        // Identical in shape: kind, target (next Age), payload (surge key); only the actor differs.
        Assert.Equal(OrderKind.AdvanceAge, a.Kind);
        Assert.Equal((pl.Kind, pl.TargetId, pl.Amount), (a.Kind, a.TargetId, a.Amount));
        Assert.Equal(AgeQuery.AdvanceOrder(aiDecided!, Rival, 2, surge), a);
        Assert.Equal(aiDecided!.Clock.Turn, a.Turn);
        Assert.True(a.Turn > 0, "founded eligible: research orders did nothing");
        // MEASURED on the dev world, seed 42: the player is eligible at turn 275, the AI at 346.
        // R1 RE-PIN (2026-10-03; research-gated recipes move the AI's trajectory): AI 346 -> 345, player 275 unchanged.
        // M5 R2b RE-PIN (2026-10-03; Director decision 11 — the founding warband no longer satisfies the
        // Neolithic's military milestone, so A2 needs a second NON-military category): AI 345 -> 385,
        // player 275 -> 338 (MEASURED). Both still reach A2; the delay is the cost of no longer counting the
        // founding line as new military realization.
        // R4 RE-PIN (2026-10-04, the forager layer; MEASURED): AI 385 -> 394, player 338 -> 346. Both still reach A2.
        // ADR-035 RE-PIN (2026-10-07, the founding-turn harvest; MEASURED): AI 394 -> 393, player 346 -> 344.
        // ADR-035 §6 RE-PIN (2026-10-07, P-F0: the founding death remainder seeded at 0.5; MEASURED): AI 393 -> 396, player 344 -> 347.
        Assert.Equal((396L, 347L), (a.Turn, pl.Turn));
        // Same validation, same answer.
        Assert.Equal(AdvanceRejection.None, AgeQuery.CheckAdvance(aiDecided!, Ages, a));
        Assert.Equal(AdvanceRejection.None, AgeQuery.CheckAdvance(playerDecided!, Ages, pl));

        // The AI's order is in the log the replay reads.
        bool inLog = false;
        for (int i = 0; i < orders.Count; i++) if (orders[i].Equals(a)) inLog = true;
        Assert.True(inLog);

        // Applied on the NEXT turn, by the same transition row shape.
        AgeTransitionRow aiRow = default, playerRow = default;
        int found = 0;
        for (int i = 0; i < w.AgeTransitions.Count; i++)
        {
            if (w.AgeTransitions[i].Polity.Value == Rival.Value) { aiRow = w.AgeTransitions[i]; found |= 1; }
            if (w.AgeTransitions[i].Polity.Value == Player.Value) { playerRow = w.AgeTransitions[i]; found |= 2; }
        }
        Assert.Equal(3, found);
        Assert.Equal((1, 2, surge, a.Turn, a.Turn + 1), (aiRow.FromAge, aiRow.ToAge, aiRow.Surge, aiRow.DecisionTurn, aiRow.EffectiveTurn));
        Assert.Equal((1, 2, surge, pl.Turn, pl.Turn + 1), (playerRow.FromAge, playerRow.ToAge, playerRow.Surge, playerRow.DecisionTurn, playerRow.EffectiveTurn));
        Assert.Equal(2, AgeQuery.CurrentAge(w, Ages, Rival));
        Assert.Equal(2, AgeQuery.CurrentAge(w, Ages, Player));

        // Free modernization applied identically: both founding warbands are now axe warriors.
        int axe = Families.IdentityById("axe_warriors")!.Key;
        int owned = 0;
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow u = w.MilitaryUnits[i];
            if (u.Owner.Value != Rival.Value && u.Owner.Value != Player.Value) continue;
            Assert.Equal(axe, u.Identity);
            owned++;
        }
        Assert.Equal(2, owned);
    }
}
