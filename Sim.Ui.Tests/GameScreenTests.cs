using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// T4.18 workstream C at the seam: the section mechanism, and the property the
/// whole redesign has to hold on to — that looking at things changes nothing.
/// </summary>
public class GameScreenTests
{
    [Fact]
    public void TheGameOpensWithNOSectionOpen_WhichIsARealState()
    {
        // The old screen had no way to express "nothing open" — five windows
        // were always up. Section.None being the default IS the redesign.
        Assert.Equal(Section.None, default(Section));
        Assert.Equal("", GameSections.Label(Section.None));
    }

    [Fact]
    public void EverySectionHasALabelAndATitle_SoNoneCanShipUnnamed()
    {
        foreach (Section section in GameSections.Order)
        {
            Assert.False(string.IsNullOrWhiteSpace(GameSections.Label(section)), section.ToString());
            Assert.False(string.IsNullOrWhiteSpace(GameSections.Title(section)), section.ToString());
        }
    }

    [Fact]
    public void TheNavigationRosterCoversEverySectionExactlyOnce()
    {
        // A section that exists but is not in Order is unreachable in play —
        // the "nothing becomes inaccessible" rule, enforced rather than trusted.
        Section[] all = Enum.GetValues<Section>().Where(s => s != Section.None).ToArray();
        Assert.Equal(all.Length, GameSections.Order.Count);
        foreach (Section section in all) Assert.Contains(section, GameSections.Order);
        Assert.Equal(GameSections.Order.Count, GameSections.Order.Distinct().Count());
    }

    [Fact]
    public void PolicyLeadsTheRoster_BecauseItIsTheOnlyOneTheDirectorACTSIn()
    {
        Assert.Equal(Section.Policy, GameSections.Order[0]);
    }

    [Fact]
    public void ClickingTheOpenSectionCLOSESIt_SoAClearWorldIsOneClickAway()
    {
        Assert.Equal(Section.Policy, GameSections.Toggle(Section.None, Section.Policy));
        Assert.Equal(Section.None, GameSections.Toggle(Section.Policy, Section.Policy));
        // ...and clicking a different one switches rather than stacking: only
        // ever one panel, which is the mechanism the packet asked for.
        Assert.Equal(Section.Market, GameSections.Toggle(Section.Policy, Section.Market));
    }

    [Fact]
    public void OPENINGAndClosingEverySectionMutatesNOSimulationState()
    {
        // "No simulation mutation merely from rendering/re-rendering." Section
        // state is UI state: the world hash and the order log must be identical
        // after walking the entire navigation, twice.
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        for (int t = 0; t < 3; t++) session.EndTurn();

        string before = WorldHash.ComputeHex(session.World);
        int ordersBefore = session.Orders.Count;

        Section open = Section.None;
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (Section section in GameSections.Order)
            {
                open = GameSections.Toggle(open, section);   // open
                open = GameSections.Toggle(open, section);   // close
            }
        }

        Assert.Equal(Section.None, open);
        Assert.Equal(before, WorldHash.ComputeHex(session.World));
        Assert.Equal(ordersBefore, session.Orders.Count);
    }

    [Fact]
    public void BUILDINGEveryPanelsViewModelRepeatedlyMutatesNOSimulationState()
    {
        // The stronger form of the same rule, and the one that would actually
        // catch a defect: re-derive every panel's content — the same calls the
        // draw code makes each frame — and require the world to be untouched.
        // A view model that wrote through to state would show up here.
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        for (int t = 0; t < 3; t++) session.EndTurn();

        WorldState world = session.World;
        string before = WorldHash.ComputeHex(world);
        SettlementId first = world.Settlements[0].Id;

        Span<int> weights = stackalloc int[Sectors.Count];
        for (int frame = 0; frame < 5; frame++)
        {
            _ = HudModel.From(world, first.Value, null, session.Names.Name(first.Value));
            _ = MarketModel.Rows(world, first.Value, MarketPanelModelTests.SimCfg().Goods!);
            _ = TradeModel.Rows(world, MarketPanelModelTests.SimCfg().Goods!);
            _ = TradeModel.Flows(world, MarketPanelModelTests.SimCfg().Goods!);
            _ = session.History.World(HistoryBuffer.Metric.Population);
            _ = session.AnnalLines;

            SectorAllocationModel.FromShares(Allocation(world, first), weights);
        }

        Assert.Equal(before, WorldHash.ComputeHex(world));
    }

    private static SectorAllocationRow Allocation(WorldState world, SettlementId settlement)
    {
        for (int i = 0; i < world.SectorAllocations.Count; i++)
            if (world.SectorAllocations[i].Settlement == settlement) return world.SectorAllocations[i];
        return new SectorAllocationRow(settlement, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void ApplyingAPolicyStillGoesThroughTheORDERPathway()
    {
        // The redesign moved the control into a contextual panel; it must not
        // have moved the DECISION out of the order log. A balanced allocation
        // applied from the panel appends exactly one batch of five orders.
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int before = session.Orders.Count;

        int[] weights = [20, 20, 20, 20, 20];
        SectorAllocationModel.Rebalance(weights, Sectors.Farming, 44);
        Assert.True(SectorAllocationModel.IsBalanced(weights));

        Assert.True(session.EmitSectorOrders(weights, session.World.Settlements[0].Id.Value));
        Assert.Equal(before + Sectors.Count, session.Orders.Count);

        for (int i = before; i < session.Orders.Count; i++)
            Assert.Equal(OrderKind.SectorAllocation, session.Orders[i].Kind);
    }

    [Fact]
    public void ABalancedAllocationIsSTILLAcceptedByTheOrderFactory()
    {
        // The fixed-sum constraint must not have made a legal allocation
        // unsubmittable: the sigma > 0 guard and the 0..100 range both still
        // pass for anything the new model can produce.
        int[] weights = [20, 20, 20, 20, 20];
        foreach (int request in new[] { 0, 1, 50, 99, 100 })
        {
            SectorAllocationModel.Rebalance(weights, Sectors.Crafting, request);
            Assert.True(SectorOrderFactory.CanSubmit(weights),
                $"a balanced allocation was refused after moving crafting to {request}");
        }
    }
}
