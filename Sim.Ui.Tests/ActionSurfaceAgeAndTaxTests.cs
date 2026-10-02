using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Ages;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// The Age on the action surface (directive: the capital Age panel must not cover the map while "not yet
/// eligible"; the advance flow appears when eligible, on the unchanged order path), and the M5 tax burden
/// rendered where happiness is explained (S1 note: those views showed only food and housing).
/// </summary>
public class ActionSurfaceAgeAndTaxTests(EligibleSessionFixture fx) : IClassFixture<EligibleSessionFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static readonly EraTable Era = UiSession.ProductionEra();

    private static int Capital(IReadOnlyWorldState w) => EmpireQuery.TryGetCapital(w, Me, out SettlementId c) ? c.Value : -1;

    private static ActionSurfaceModel Surface(UiSession s) =>
        ActionSurface.ForSession(s, Era, Capital(s.World), EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)));

    [Fact]
    public void TheAdvanceAppearsOnlyWhenEligible_AndOpensTheUnchangedOrderPath()
    {
        // Not yet eligible (the same session's turn-12 world): no Age action on the surface; the status band's
        // compact indicator carries the full Age name and says "not yet".
        UiSession early = UiSession.StartFrom(fx.Early.Clone(), 42, 256, 4);
        Assert.Null(Surface(early).Age);
        AgeFigure notYet = StatusFigures.Age(AgePanelModel.Build(early.World, early.Config.Ages, [], Me));
        Assert.Equal(AgePanelState.NotEligible, notYet.State);
        Assert.False(notYet.Eligible);

        // Eligible: the surface lists the advance (the query's age.advance descriptor) and the band draws the eye.
        UiSession s = UiSession.StartFrom(fx.Session.World.Clone(), 42, 256, 4);
        ActionSurfaceModel m = Surface(s);
        AgeBlock age = m.Age!;
        Assert.Equal(OrderKind.AdvanceAge, age.Advance.Order);
        Assert.Equal(2, age.NextAge);
        Assert.Equal(s.Config.Ages!.Age(2).Name, age.NextName);
        Assert.True(StatusFigures.Age(AgePanelModel.Build(s.World, s.Config.Ages, s.QueuedOrders(), Me)).Eligible);

        // The surface's advance asks the host to open the Age panel's flow; the order itself is the flow's
        // confirm — exactly AgeQuery.AdvanceOrder through UiSession.EmitAdvanceAge (unchanged path).
        var screen = new ActionSurfaceScreen { Theme = EraThemes.For(m.Era) };
        screen.Refresh(m, s.World, s.Config, Me, id => s.Names.Name(id));
        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        ActionHit advance = screen.Hits.Single(h => h.Kind == ActionHitKind.AgeAdvance);
        ActionCommand cmd = screen.Click(advance.Rect.CenterX, advance.Rect.CenterY);
        Assert.Equal(ActionCommandKind.AdvanceAge, cmd.Kind);
        Assert.False(cmd.IsOrder);
        int surge = s.Config.Ages.Surges[0].Key;
        Assert.True(s.EmitAdvanceAge(age.NextAge, surge));
        Assert.Equal(AgeQuery.AdvanceOrder(s.World, Me, age.NextAge, surge), s.Orders[^1]);

        // Ordered: the surface no longer offers it (the query omits a pending advance); the band says so.
        Assert.Null(Surface(s).Age);
        Assert.Equal(AgePanelState.Pending, StatusFigures.Age(AgePanelModel.Build(s.World, s.Config.Ages, s.QueuedOrders(), Me)).State);
    }

    [Fact]
    public void TheTaxBurden_IsRenderedWhereHappinessIsExplained_OnceALevyIsDeclared()
    {
        UiSession s0 = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        ResearchContent research = s0.Config.Research!;
        WorldState w = s0.World.Clone();
        w.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId("arithmetic_babylonian")].Key));
        UiSession s = UiSession.StartFrom(w, 42, 256, 4);
        int capital = Capital(s.World);

        // Never legislated: no burden row (nothing to explain), only the two provision factors.
        s.EndTurn();
        GrievanceView untaxed = ScreenModels.Grievance(s.PreviousWorld!, s.World, s.Config, new SettlementId(capital));
        Assert.Equal(2, untaxed.Factors.Count);
        Assert.DoesNotContain(ScreenModels.Settlement(s, capital)!.Overview, l => l.StartsWith("tax:", StringComparison.Ordinal));

        // A 40 % levy: in force from the next turn, so the burden is explained from then on.
        Assert.True(s.EmitTaxOrder(40));
        s.EndTurn();
        GrievanceView taxed = ScreenModels.Grievance(s.PreviousWorld!, s.World, s.Config, new SettlementId(capital));
        Assert.Equal(3, taxed.Factors.Count);
        HappinessFactorRow burden = taxed.Factors[2];
        TaxBurdenReading reading = TaxBurdenReading.Of(s.World, s.Config, new SettlementId(capital));
        Assert.StartsWith("x tax burden " + reading.Scale.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), burden.Line);
        Assert.Equal(GrievanceViewModel.TaxLever, burden.Lever.Text);
        Assert.False(burden.Lever.IsNone);   // the lever opens POLICY, where the edict is
        Assert.Equal(3, burden.Chain.Count);
        Assert.Contains("declared rate  40%", burden.Chain[0].Text);

        IReadOnlyList<string> overview = ScreenModels.Settlement(s, capital)!.Overview;
        Assert.Contains(overview, l => l.Contains(" x tax burden ", StringComparison.Ordinal));
        string tax = overview.Single(l => l.StartsWith("tax:", StringComparison.Ordinal));
        Assert.Contains("declared 40%", tax);
        // The capital administers itself in full: it collects the whole declared rate.
        Assert.Contains("x reach 100% = collects 40%", tax);
    }
}
