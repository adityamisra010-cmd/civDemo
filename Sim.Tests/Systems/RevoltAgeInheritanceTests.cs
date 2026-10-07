using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// H2 (Director 2026-10-05 §8, RATIFIED in docs/d049-taxation-and-revolt-model.md) — A CIVILIZATION CREATED BY
/// REVOLT INHERITS ITS PARENT'S CURRENT AGE. It receives the completed research (D-048 ruling 1), no research
/// progress (2), no Eureka credit (3), no capital (4), researches at the normal rate (6), and the parent's final
/// settlement still cannot revolt away (5). The contradiction the ruling forbids — parent A5 → revolt → child A1
/// holding A5 knowledge — is pinned out here. Exercised through the real RevoltSystem, AgeTransitionSystem and
/// ResearchSystem in pipeline order.
/// </summary>
public class RevoltAgeInheritanceTests
{
    private static readonly ResearchContent Rig = Standard().Load();
    private static readonly SimConfig Cfg = TestConfigs.Sim() with { Research = Rig };
    private static readonly AgeContent Ages = TestConfigs.Ages();
    private static readonly PolityId Parent = new(Player);
    private static readonly PolityId Child = new(2);   // one above the roster: the polity the revolt founds
    private static readonly int[] ParentKnowledge = [1, 2, 3, 4, 5, 1001];

    /// <summary>Two settlements of 10 000 adults under the parent; settlement 1 is destitute (unfed and unhoused — the
    /// deprivation corner that revolts it), settlement 0 comfortable. The parent knows <see cref="ParentKnowledge"/>,
    /// has in-progress research and a fired Eureka, holds settlement 0 as its capital and is in Age
    /// <paramref name="parentAge"/> (no row for the founding Age).</summary>
    private static WorldState Split(int parentAge, long enteredTurn = 7, int surge = 2)
    {
        WorldState w = World([Player], [Player, Player], 10_000);
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < 2; i++)
        {
            var s = new SettlementId(i);
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, i == 1 ? 1.0 : 0.0, 10_000));
            w.Housing.Add(new HousingRow(s, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (i == 0)
                ledger.Flow(ref w.Housing.Ref(i).Dwellings, ConservedQuantityIds.Dwellings,
                    ReasonIds.InitialEndowment, 5_000, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        w.Capitals.Add(new CapitalRow(Parent, new SettlementId(0)));
        WithCompleted(w, ParentKnowledge);
        w.ResearchProgress.Add(new ResearchProgressRow(Parent, Key(6), 700.0));
        w.ResearchEurekas.Add(new ResearchEurekaRow(Parent, Key(6), 0));
        if (parentAge != Ages.FoundingAge) w.AgeStates.Add(new AgeStateRow(Parent, parentAge, enteredTurn, surge, enteredTurn));
        return w;
    }

    /// <summary>Revolt, Age transition and research in their pipeline order.</summary>
    private static TurnExecutor Pipeline(OrderLog? orders = null) =>
        new(FlatEra(10.0), [SystemCatalog.Revolt(Cfg), SystemCatalog.Research(Cfg), SystemCatalog.AgeTransition(Cfg)], orders);

    [Fact]
    public void AParentInA5_RevoltsAChildInA5_NotA1_WithItsKnowledge_AndNothingElse()
    {
        WorldState before = Split(parentAge: 5);
        WorldState w = Pipeline().Step(before);

        Assert.True(EmpireQuery.TryGetController(w, new SettlementId(1), out PolityId founded));
        Assert.Equal(Child, founded);
        // THE RULING: the child is in its parent's CURRENT Age.
        Assert.Equal(5, AgeQuery.CurrentAge(w, Ages, Child));
        Assert.Equal(5, AgeQuery.CurrentAge(w, Ages, Parent));   // the parent keeps its Age
        AgeStateRow row = AgeQuery.StateRow(w, Child)!.Value;
        // Its first state in that Age is the state the revolt produced (the AgeStateRow contract); the parent's
        // surge emphasis is carried, its entry date is not (the child did not live it).
        Assert.Equal(new AgeStateRow(Child, 5, before.Clock.Turn + 1, 2, before.Clock.Turn + 1), row);
        Assert.Empty(AgeQuery.Transitions(w, Child));            // inherited, not a transition

        // D-048 rulings 1–4 hold alongside: complete knowledge, no progress, no Eureka, no capital.
        bool[] parent = ResearchQuery.CompletedMask(w, Rig, Parent);
        bool[] child = ResearchQuery.CompletedMask(w, Rig, Child);
        Assert.Equal(parent, child);
        Assert.Equal(0.0, Progress(w, 6, Child.Value));
        Assert.Equal(700.0, Progress(w, 6, Parent.Value));
        for (int i = 0; i < w.ResearchEurekas.Count; i++) Assert.NotEqual(Child, w.ResearchEurekas[i].Polity);
        Assert.False(EmpireQuery.TryGetCapital(w, Child, out _));
        Assert.True(EmpireQuery.TryGetCommandSource(w, Child, out CommandSource source) && source == CommandSource.Ai);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    public void EveryInheritedAge_IsTheParentsAge(int parentAge)
    {
        WorldState w = Pipeline().Step(Split(parentAge));
        Assert.Equal(parentAge, AgeQuery.CurrentAge(w, Ages, Child));
    }

    [Fact]
    public void AFoundingAgeParent_RevoltsAFoundingAgeChild_AndWritesNoAgeRow()
    {
        WorldState w = Pipeline().Step(Split(Ages.FoundingAge));
        Assert.True(EmpireQuery.TryGetController(w, new SettlementId(1), out PolityId founded));
        Assert.Equal(Child, founded);
        Assert.Equal(Ages.FoundingAge, AgeQuery.CurrentAge(w, Ages, Child));
        Assert.Null(AgeQuery.StateRow(w, Child));
        Assert.Equal(0, w.AgeStates.Count);   // nothing is written for the founding Age (absence of a row IS it)
    }

    /// <summary>The child keeps the inherited Age through later steps (AgeTransitionSystem never rewrites a row
    /// without an order). G10 (M5 polish): a child born in A3+ that inherited Taxation has the knowledge and the Age
    /// but NO SEAT (no capital, D-048 ruling 4), so the ONE predicate refuses it — <see cref="Governance.GateOf"/> is
    /// NeedsSeat — and every caller agrees: the available-actions query lists no edict, the AI valve writes no tax
    /// order, and GovernanceSystem applies no policy row for an order the child issues anyway.</summary>
    [Fact]
    public void TheInheritedAge_Persists_ButASeatlessChild_CannotLevyTax_AtAnyCaller()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState start = Split(parentAge: GovernanceRigs.TaxAge);
        // The canonical content's Taxation civic, completed for the parent (the canonical research rides with cfg).
        ResearchContent research = cfg.Research!;
        start.ResearchCompleted.Add(new ResearchCompletedRow(Parent, research.Nodes[research.IndexOfId(GovernanceRigs.TaxationNode)].Key));
        var ex = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Revolt(cfg), SystemCatalog.AgeTransition(cfg)]);
        WorldState w = ex.Step(start);
        for (int t = 0; t < 3; t++) w = ex.Step(w);
        Assert.Equal(GovernanceRigs.TaxAge, AgeQuery.CurrentAge(w, cfg.Ages!, Child));
        Assert.True(Governance.KnowsTaxation(w, cfg, Child));                // knowledge inherited
        Assert.True(Governance.MeetsTaxationAge(w, cfg, Child));             // Age inherited
        Assert.False(EmpireQuery.TryGetCapital(w, Child, out _));            // no seat

        // THE PREDICATE.
        Assert.Equal(TaxGate.NeedsSeat, Governance.GateOf(w, cfg, Child));
        Assert.False(Governance.CanLevyTax(w, cfg, Child));
        Assert.Equal(TaxGate.Open, Governance.GateOf(w, cfg, Parent));       // the seated parent still can (control arm)

        // CALLER 1 — the available-actions query (the UI's action list): no edict for the child, one for the parent.
        var childActions = new List<ActionDescriptor>();
        AvailableActionsQuery.Governance(w, cfg, Child, childActions);
        Assert.Empty(childActions);
        var parentActions = new List<ActionDescriptor>();
        AvailableActionsQuery.Governance(w, cfg, Parent, parentActions);
        Assert.Single(parentActions);

        // CALLER 2 — the AI valve: the child is AI-commanded, yet writes no tax row.
        Assert.True(EmpireQuery.TryGetCommandSource(w, Child, out CommandSource source) && source == CommandSource.Ai);
        Assert.Empty(Sim.Core.Systems.Governance.AiGovernance.OrdersFor(w, cfg, Child, w.Clock.Turn + 1));

        // CALLER 3 — GovernanceSystem: a SetTaxRate the child issues anyway (a hand-built or replayed order) is
        // refused on PREV; no policy row is written for it.
        var orders = new OrderLog();
        orders.Append(Governance.TaxOrder(w.Clock.Turn + 1, Child, 40.0));
        WorldState after = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Governance(cfg)], orders).Step(w);
        Assert.False(Governance.HasPolicy(after, Child));
        Assert.Equal(0.0, Governance.EffectiveTaxRate(after, new SettlementId(1), cfg));
    }

    /// <summary>G11 (M5 polish) — D-048 ruling 6, "the child researches at the NORMAL rate", pinned directly: after the
    /// revolt, the child's per-turn Research Points on its target equal the content's RP formula
    /// (coefficient × P^exponent, <see cref="ResearchQuery.ResearchPoints"/>) of ITS OWN population — not the
    /// city-state fraction, not the parent's pool, with no progress carried and no Eureka credit — and the parent,
    /// researching the same node from its own population, gets its own formula value in the same step.</summary>
    [Fact]
    public void ARevoltBornChild_ResearchesAtTheFormulaRate_ForItsOwnPopulation()
    {
        WorldState born = Pipeline().Step(Split(parentAge: 3));
        Assert.True(EmpireQuery.TryGetController(born, new SettlementId(1), out PolityId founded));
        Assert.Equal(Child, founded);
        Assert.Equal(0.0, Progress(born, 6, Child.Value));                 // nothing inherited (ruling 2)

        var orders = new OrderLog();
        orders.Append(Target(born.Clock.Turn, 6, Child.Value));
        WorldState w = Executor(Rig, orders).Step(born);

        double childPop = ResearchQuery.Population(born, Child);   // long → double
        Assert.Equal(10_000.0, childPop);                                // settlement 1's people
        double rp = ResearchQuery.ResearchPoints(Rig.Tuning, childPop);
        double cost = ResearchQuery.EffectiveCost(born, Rig, Child, Rig.IndexOf(Key(6)));
        Assert.True(rp > 0.0 && rp < cost, $"rp {rp} vs cost {cost}: the step would complete the node, rig vacuous");
        Assert.Equal(rp, Progress(w, 6, Child.Value));                     // EXACT: one turn of the formula
        Assert.Equal(rp, ResearchQuery.ResearchPointPool(born, Rig, Child));
        Assert.NotEqual(Rig.Tuning.CityStatePaceFraction * rp, Progress(w, 6, Child.Value));
    }

    /// <summary>D-048 ruling 5 still holds with Ages: a parent whose only place is destitute keeps it — no child, no
    /// Age row.</summary>
    [Fact]
    public void TheFinalSettlement_StillCannotRevoltAway()
    {
        WorldState w = World([Player], [Player], 10_000);
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), 1.0, 10_000));
        w.Housing.Add(new HousingRow(new SettlementId(0), Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        w.AgeStates.Add(new AgeStateRow(Parent, 5, 3, 1, 3));
        Assert.True(SettlementHappiness.IsRevoltReady(w, new SettlementId(0), Cfg));
        WorldState next = Pipeline().Step(w);
        Assert.True(EmpireQuery.ControlsSettlement(next, Parent, new SettlementId(0)));
        Assert.Equal(1, next.Polities.Count);
        Assert.Equal(1, next.AgeStates.Count);
    }

    /// <summary>Save/load and replay: the inherited Age row is ordinary serialized state — a snapshot at the revolt
    /// turn round-trips bit-exactly and two runs agree hash for hash.</summary>
    [Fact]
    public void TheInheritedAge_SurvivesSaveLoad_AndReplaysExactly()
    {
        WorldState a = Pipeline().Step(Split(parentAge: 4));
        WorldState b = Pipeline().Step(Split(parentAge: 4));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        using var ms = new MemoryStream();
        Snapshot.Save(a, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms, a.Terrain);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(loaded));
        Assert.Equal(4, AgeQuery.CurrentAge(loaded, Ages, Child));
    }
}
