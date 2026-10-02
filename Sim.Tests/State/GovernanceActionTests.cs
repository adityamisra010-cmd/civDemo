using System.Collections.Immutable;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Governance;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-033 D4 × D2/D5, the S1×S2 reconciliation: the tax edict joins the action surface and the AI producer
/// through the ONE research gate (<see cref="Governance.CanLevyTax"/>) that GovernanceSystem applies. Turn 1
/// offers no tax control; completing a node whose content unlocks a taxation capability makes exactly one
/// Governance descriptor appear; the AI valve's orders reach the session only through <see cref="AiOrders"/>,
/// only for AI Empires that can levy.
/// </summary>
public class GovernanceActionTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    /// <summary>The four nodes sim.json governance.taxationRequires names (their unlocks name a taxation capability).</summary>
    private static readonly string[] TaxationNodes = ["arithmetic_babylonian", "surveying", "standard_weights", "coinage_electrum"];

    private static readonly Lazy<WorldState> Canonical = new(() => WorldFounding.Found(TestConfigs.Worldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    private static WorldState Complete(WorldState w, PolityId polity, params string[] nodeIds)
    {
        foreach (string id in nodeIds)
        {
            int index = Research.IndexOfId(id);
            Assert.True(index >= 0, id);
            w.ResearchCompleted.Add(new ResearchCompletedRow(polity, Research.Nodes[index].Key));
        }
        return w;
    }

    private static ActionDescriptor[] GovernanceOf(ImmutableArray<ActionDescriptor> actions) =>
        actions.Where(a => a.Domain == ActionDomain.Governance).ToArray();

    [Fact]
    public void TurnOne_CanonicalFoundedWorld_OffersNoTaxEdict()
    {
        WorldState w = Canonical.Value;
        Assert.False(Governance.CanLevyTax(w, Cfg, Player));
        Assert.Empty(GovernanceOf(AvailableActionsQuery.For(w, Cfg, Player)));
        Assert.DoesNotContain(AvailableActionsQuery.For(w, Cfg, Player), a => a.Order == OrderKind.SetTaxRate);
    }

    [Fact]
    public void ATaxationNodeCompleted_ExactlyOneEdictAppears_WithItsProvenance()
    {
        WorldState w = Complete(Canonical.Value.Clone(), Player, "arithmetic_babylonian");
        ActionDescriptor[] gov = GovernanceOf(AvailableActionsQuery.For(w, Cfg, Player));

        ActionDescriptor edict = Assert.Single(gov);
        Assert.Equal(1, edict.Id);
        Assert.Equal(ActionKind.Order, edict.Kind);
        Assert.Equal(OrderKind.SetTaxRate, edict.Order);
        Assert.Equal("governance.tax-edict", edict.Key);
        Assert.Null(edict.Blocker);
        Assert.True(edict.Provenance.Researched);
        Assert.Equal([Research.Nodes[Research.IndexOfId("arithmetic_babylonian")].Key], edict.Provenance.Nodes.ToArray());
        Assert.StartsWith("no levy declared", edict.Detail);
    }

    /// <summary>One predicate, two callers: across every taxation node, an unrelated node and none, the
    /// descriptor is listed exactly when the gate GovernanceSystem applies holds.</summary>
    [Fact]
    public void TheEdictIsListed_ExactlyWhen_CanLevyTaxHolds()
    {
        var rigs = new List<string[]> { Array.Empty<string>(), new[] { "root_crop" } };
        foreach (string node in TaxationNodes) rigs.Add([node]);
        foreach (string[] nodes in rigs)
        {
            WorldState w = Complete(Canonical.Value.Clone(), Player, nodes);
            bool gate = Governance.CanLevyTax(w, Cfg, Player);
            Assert.Equal(nodes.Any(n => TaxationNodes.Contains(n)), gate);
            Assert.Equal(gate ? 1 : 0, GovernanceOf(AvailableActionsQuery.For(w, Cfg, Player)).Length);
        }
    }

    /// <summary>The AI tax valve reaches the session only through AiOrders, only for an AI Empire that can levy,
    /// and is exactly what AiGovernance decides for it; the player is never spoken for.</summary>
    [Fact]
    public void AiOrders_CarryTheValve_OnlyForAnAiEmpireThatCanLevy()
    {
        WorldState none = DevDuo.Value.Clone();
        Assert.DoesNotContain(AiOrders.For(none, Cfg), o => o.Kind == OrderKind.SetTaxRate);

        WorldState both = Complete(Complete(DevDuo.Value.Clone(), Rival, "arithmetic_babylonian"), Player, "arithmetic_babylonian");
        OrderRecord[] taxes = AiOrders.For(both, Cfg).Where(o => o.Kind == OrderKind.SetTaxRate).ToArray();
        OrderRecord[] expected = AiGovernance.OrdersFor(both, Cfg, Rival, both.Clock.Turn);
        Assert.NotEmpty(expected);   // anti-vacuity: at founding the rival's valve does want a rate
        Assert.Equal(expected, taxes);
        Assert.All(taxes, o => { Assert.Equal(Rival.Value, o.ActorId); Assert.Equal(Rival.Value, o.TargetId); });
        Assert.Empty(AiGovernance.OrdersFor(both, Cfg, Player, both.Clock.Turn));
    }
}
