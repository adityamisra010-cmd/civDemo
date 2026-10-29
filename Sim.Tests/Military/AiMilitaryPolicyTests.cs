using System.Collections.Immutable;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Military;
using Sim.Tests.TestUtil;
using static Sim.Tests.Military.MilitaryRigs;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 §7–§8 — THE AI'S LEGAL-ONLY RECRUITMENT (military.aiRecruitment) and THE ACTION SURFACE: the AI recruits one
/// formation when its next Age's formation milestone is unmet, through the player's predicate, choosing by the integer
/// composite key (identity Age, identity key, settlement id) — tie-dense tests; it reads no rival's state; the switch off
/// issues nothing. AvailableActionsQuery lists Recruit / Re-equip / Cancel / Disband with the predicates' refusals.
/// </summary>
public class AiMilitaryPolicyTests
{
    private static readonly PolityId Ai = new(2);

    private static WorldState AiReady()
    {
        WorldState w = World(aiEmpires: 1);
        KnowFor(w, Ai, "hafting", "bow_simple");
        for (int i = 0; i < w.Settlements.Count; i++)
            foreach (string g in new[] { "timber", "stone", "fiber" }) Endow(w, w.Settlements[i].Id, g, 300);
        return w;
    }

    private static int LowestSettlementOf(IReadOnlyWorldState w, PolityId p)
    {
        int best = int.MaxValue;
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, p, w.Settlements[i].Id)) best = Math.Min(best, w.Settlements[i].Id.Value);
        return best;
    }

    [Fact]
    public void TheAi_RecruitsTheLeastKeyLegalIdentity_ThatMeetsItsNextAgesFormationMilestone()
    {
        WorldState w = AiReady();
        OrderRecord order = AiMilitaryPolicy.Decide(w, Cfg, Ai) ?? throw new Xunit.Sdk.XunitException("no order");
        // A2 counts identities realized at A2 or later: spearmen (201), axe warriors (309), archers (402) are legal; the
        // least key at the least Age wins, at the AI's lowest settlement id.
        Assert.Equal((OrderKind.RecruitFormation, Ai.Value, LowestSettlementOf(w, Ai), 201.0, w.Clock.Turn),
            (order.Kind, order.ActorId, order.TargetId, order.Amount, order.Turn));
        Assert.True(RecruitmentQuery.CheckRecruit(w, Cfg, RecruitmentQuery.ViewOf(w), Ai, new SettlementId(order.TargetId), 201).Ok);
        Assert.Null(AiMilitaryPolicy.Decide(w, Cfg, Player));                       // the player is never driven
        Assert.Null(AiMilitaryPolicy.Decide(w, TestConfigs.PreAiRecruitment(Cfg), Ai));
        Assert.Null(AiMilitaryPolicy.Decide(w, TestConfigs.PreMilitary(Cfg), Ai));

        // LEGAL-ONLY: delivered, it raises the formation, and the milestone is then met — the AI asks no more.
        var log = new OrderLog();
        log.Append(order);
        WorldState w1 = MilitaryOnly(log).Step(w);
        Assert.Equal(w.MilitaryUnits.Count + 1, w1.MilitaryUnits.Count);
        Assert.Null(AiMilitaryPolicy.Decide(w1, Cfg, Ai));
    }

    [Fact]
    public void TieDense_EveryCandidateTiesOnAge_TheKeyThenTheSettlementIdDecide_WhateverTheTableOrder()
    {
        WorldState w = AiReady();
        int expectedSettlement = LowestSettlementOf(w, Ai);
        // Reverse the settlement table: the iteration order changes, the pick does not.
        WorldState reversed = w.Clone();
        var rows = Rows(w.Settlements);
        for (int i = 0; i < rows.Length; i++) reversed.Settlements[i] = rows[rows.Length - 1 - i];
        foreach (WorldState world in new[] { w, reversed })
        {
            OrderRecord o = AiMilitaryPolicy.Decide(world, Cfg, Ai)!.Value;
            Assert.Equal((expectedSettlement, 201.0), (o.TargetId, o.Amount));
        }
        // Spearmen made impossible (no stone): the next key at the same Age wins (archers 402 need no stone; axe warriors 309 do).
        WorldState noStone = w.Clone();
        Drain(noStone, "stone");
        Assert.Equal(402.0, AiMilitaryPolicy.Decide(noStone, Cfg, Ai)!.Value.Amount);

        // The composite key is a total order over integers: every permutation of a tie-dense set sorts to one sequence.
        (int, int, int)[] keys = [(2, 201, 3), (2, 201, 1), (2, 309, 1), (2, 402, 1), (3, 102, 1), (2, 201, 2)];
        (int, int, int)[] sorted = [.. keys];
        Array.Sort(sorted, (a, b) => AiMilitaryPolicy.Compare(a, b));
        Assert.Equal([(2, 201, 1), (2, 201, 2), (2, 201, 3), (2, 309, 1), (2, 402, 1), (3, 102, 1)], sorted);
        for (int s = 0; s < 50; s++)
        {
            (int, int, int)[] shuffled = [.. keys];
            for (int i = shuffled.Length - 1; i > 0; i--) { int j = (s * 7 + i * 13) % (i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
            Array.Sort(shuffled, (a, b) => AiMilitaryPolicy.Compare(a, b));
            Assert.Equal(sorted, shuffled);
        }
    }

    [Fact]
    public void TheAi_ReadsNoRivalState()
    {
        WorldState w = AiReady();
        OrderRecord before = AiMilitaryPolicy.Decide(w, Cfg, Ai)!.Value;
        WorldState rival = w.Clone();
        Know(rival, "arsenical_bronze", "sling", "crossbow");                    // the PLAYER's knowledge
        Endow(rival, Capital(rival), "bronze", 1000);                            // the PLAYER's stock
        RecruitmentTests.LeaveAdults(rival, Capital(rival), 25);
        Assert.Equal(before, AiMilitaryPolicy.Decide(rival, Cfg, Ai));
    }

    [Fact]
    public void AvailableActions_ListRecruitReequipDisband_WithThePredicatesRefusals()
    {
        WorldState w = Ready();
        SettlementId cap = Capital(w);
        int warband = Warband(w);
        ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, Cfg, Player);
        ActionDescriptor recruit = actions.Single(a => a.Domain == ActionDomain.Military && a.Id == AvailableActionsQuery.RecruitIdBase + cap.Value);
        Assert.Equal((ActionDomain.Military, OrderKind.RecruitFormation, (string?)null), (recruit.Domain, recruit.Order, recruit.Blocker));
        Assert.Null(recruit.Targets.Single(t => t.Id == Id("bronze_swordsmen").Key).Blocker);
        Assert.Contains("is the latest realizable", recruit.Targets.Single(t => t.Id == Id("axe_warriors").Key).Blocker);
        Assert.DoesNotContain(recruit.Targets, t => t.Id == Id("phalanx").Key);    // unknown: a locked action, not listed
        Assert.Contains("20 adults; 10 bronze, 5 timber, 5 hides", recruit.Targets.Single(t => t.Id == Id("bronze_swordsmen").Key).Detail);

        ActionDescriptor reequip = actions.Single(a => a.Domain == ActionDomain.Military && a.Id == AvailableActionsQuery.ReequipIdBase + warband);
        Assert.Equal(RecruitmentQuery.OD2Refusal, reequip.Targets.Single(t => t.Id == Id("bronze_swordsmen").Key).Blocker);
        Assert.Null(reequip.Targets.Single(t => t.Id == Id("spearmen").Key).Blocker);
        Assert.DoesNotContain(reequip.Targets, t => t.Id == Id("warband").Key);
        Assert.Single(actions, a => a.Domain == ActionDomain.Military && a.Id == AvailableActionsQuery.DisbandIdBase + warband && a.Order == OrderKind.DisbandFormation);

        // Queued this turn: the same recruitment is "already ordered".
        var queued = new ActionQueryContext([Recruit(0, cap, "bronze_swordsmen")]);
        ActionDescriptor again = AvailableActionsQuery.For(w, Cfg, Player, queued).Single(a => a.Domain == ActionDomain.Military && a.Id == AvailableActionsQuery.RecruitIdBase + cap.Value);
        Assert.Equal("already ordered this turn", again.Targets.Single(t => t.Id == Id("bronze_swordsmen").Key).Blocker);

        // Pending: the re-equip descriptor gives way to the cancel.
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "spearmen"));
        WorldState w1 = MilitaryOnly(log, dtYears: 0.25).Step(w);
        ImmutableArray<ActionDescriptor> pending = AvailableActionsQuery.For(w1, Cfg, Player);
        Assert.DoesNotContain(pending, a => a.Domain == ActionDomain.Military && a.Id == AvailableActionsQuery.ReequipIdBase + warband);
        Assert.Single(pending, a => a.Domain == ActionDomain.Military && a.Id == AvailableActionsQuery.CancelReequipIdBase + warband);

        // The layer off: no military order is offered.
        SimConfig off = TestConfigs.PreMilitary(Cfg);
        Assert.DoesNotContain(AvailableActionsQuery.For(World(off), off, Player), a => a.Domain == ActionDomain.Military && a.Kind == ActionKind.Order);
    }
}
