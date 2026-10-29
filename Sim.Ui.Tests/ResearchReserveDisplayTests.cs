using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-051 (PROPOSED; research.json tuning.researchReserve) — THE RESERVE ON THE RESEARCH SURFACES (proposal §5): the Glass
/// Box research card, the action surface's research line, the status band and the progression screen show the reserve
/// ("reserve Y of Z RP"), what a lane funding nothing does with its RP ("X RP go to the reserve (Y of Z)" / "reserve full - X
/// RP unused this turn") and a turn's draw ("+W RP from the reserve this turn"). Every figure is ResearchQuery.Reserve's, and
/// the next step is measured to do what the line says. The input is the stepped real world (SteppedWorldFixture) under the
/// shipped content with the reserve switched on (capTurns 3, drawTurns 1); with the switch off no surface carries a reserve.
/// </summary>
public class ResearchReserveDisplayTests(SteppedWorldFixture fx) : IClassFixture<SteppedWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static readonly EraTable Era = UiSession.ProductionEra();

    private static readonly Lazy<ResearchContent> On = new(() =>
    {
        using var stream = Sim.Data.DataFiles.OpenResearch();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(stream)!.AsObject();
        doc["tuning"]!["researchReserve"] = new System.Text.Json.Nodes.JsonObject { ["capTurns"] = 3.0, ["drawTurns"] = 1.0 };
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return ResearchContentLoader.Load(doc.ToJsonString(), Sim.Core.Systems.GoodsConfigLoader.Load(goods));
    });

    private SimConfig Cfg => fx.Session.Config with { Research = On.Value };

    /// <summary>The stepped world with every Technology target of the player removed (no assist on the stepped world: its
    /// civic target's route needs no technology — asserted), and the player's reserve set to <paramref name="reserveMilli"/>.</summary>
    private WorldState Idle(long reserveMilli)
    {
        ResearchContent c = On.Value;
        WorldState w = ((WorldState)fx.Session.World).Clone();
        var keep = new List<ResearchTargetRow>();
        for (int r = 0; r < w.ResearchTargets.Count; r++)
            if (!(w.ResearchTargets[r].Polity.Value == Me.Value && c.IndexOf(w.ResearchTargets[r].Node) is int i and >= 0
                && c.Nodes[i].Tree == ResearchTree.Technology))
                keep.Add(w.ResearchTargets[r]);
        w.ResearchTargets.Clear();
        foreach (ResearchTargetRow row in keep) w.ResearchTargets.Add(row);
        if (reserveMilli > 0) w.ResearchReserves.Add(new ResearchReserveRow(Me, reserveMilli));
        Assert.Equal(LaneActivity.Idle, ResearchQuery.Activity(ResearchQuery.Route(w, c, Me, ResearchTree.Technology)));
        return w;
    }

    private WorldState Funding(long reserveMilli)
    {
        WorldState w = ((WorldState)fx.Session.World).Clone();
        if (reserveMilli > 0) w.ResearchReserves.Add(new ResearchReserveRow(Me, reserveMilli));
        Assert.Equal(LaneActivity.Researching, ResearchQuery.Activity(ResearchQuery.Route(w, On.Value, Me, ResearchTree.Technology)));
        return w;
    }

    private string Card(IReadOnlyWorldState w) =>
        InfoQuery.Card(w, Cfg, Me, InfoSubject.Research, ActionQueryContext.ForNextStep(w, Era)).StatusLine;

    private ActionSurfaceModel Surface(IReadOnlyWorldState w) =>
        ActionSurface.Build(new ActionSurfaceInput(w, null, Cfg, Era, Me, -1, [], id => fx.Session.Names.Name(id),
            EraThemes.For(UiEras.Of(w, Cfg.Ages, Me))));

    private ProgressionScreen Screen(IReadOnlyWorldState w)
    {
        var s = new ProgressionScreen(On.Value, Me) { Config = Cfg };
        s.Refresh(w);
        return s;
    }

    private static IEnumerable<string> Texts(DrawList d)
    {
        foreach (object cmd in d.Commands) if (cmd is TextCmd t) yield return t.Text;
    }

    private WorldState Step(IReadOnlyWorldState w) => new TurnExecutor(Era, [SystemCatalog.Research(Cfg)]).Step((WorldState)w);

    [Fact]
    public void AnIdleLane_EveryResearchSurfaceSaysItsRpGoToTheReserve_AndTheNextStepDepositsThem()
    {
        ResearchContent c = On.Value;
        WorldState w = Idle(0);
        ReserveStatus rs = Assert.IsType<ReserveStatus>(ResearchQuery.Reserve(w, c, Me));
        Assert.Equal(ReserveAction.Deposit, rs.Action);
        double pool = ResearchQuery.ResearchPointPool(w, c, Me);
        Assert.Equal(ResearchQuery.ToMilliRp(pool), rs.AmountMilli);
        Assert.Equal(ResearchQuery.ToMilliRp(3.0 * pool), rs.CapMilli);
        Assert.Contains(" RP go to the reserve (0 of ", rs.Unused, StringComparison.Ordinal);

        Assert.StartsWith("Idle: " + rs.Unused + "; ", Card(w));
        ResearchBlock block = Assert.IsType<ResearchBlock>(Surface(w).Research);
        Assert.Equal(rs, block.Reserve);
        Assert.True(block.Idle);
        ResearchFigure band = StatusFigures.Research(block);
        Assert.Equal("Tech idle - choose [K] - reserve 0/" + Math.Floor(rs.CapMilli / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture), band.Text);
        ProgressionScreen screen = Screen(w);
        Assert.Equal(rs, screen.Snapshot!.Reserve);
        List<string> painted = [.. Texts(screen.Paint(1600, 1000, ApproxTextMeasure.Instance))];
        Assert.Contains("Research reserve", painted);
        Assert.Contains(painted, t => t.StartsWith("Research idle - choose a node; ", StringComparison.Ordinal));

        // Measured: the next step deposits exactly the amount shown.
        Assert.Equal(rs.AmountMilli, ResearchQuery.ReserveMilli(Step(w), Me));
    }

    [Fact]
    public void AFullReserve_SaysReserveFull_AndTheNextStepLeavesItAtTheCap()
    {
        ResearchContent c = On.Value;
        long cap = ResearchQuery.ReserveCapMilli(Idle(0), c, Me);
        WorldState w = Idle(cap);
        ReserveStatus rs = Assert.IsType<ReserveStatus>(ResearchQuery.Reserve(w, c, Me));
        Assert.Equal(0L, rs.AmountMilli);
        Assert.StartsWith("reserve full - ", rs.Unused);
        Assert.EndsWith(" RP unused this turn", rs.Unused);
        Assert.StartsWith("Idle: reserve full - ", Card(w));
        Assert.Equal(cap, ResearchQuery.ReserveMilli(Step(w), Me));
    }

    [Fact]
    public void AFundingLane_ShowsTheDraw_AndTheNextStepDrawsIt()
    {
        ResearchContent c = On.Value;
        long cap = ResearchQuery.ReserveCapMilli(fx.Session.World, c, Me);
        WorldState w = Funding(cap);
        ReserveStatus rs = Assert.IsType<ReserveStatus>(ResearchQuery.Reserve(w, c, Me));
        Assert.Equal(ReserveAction.Draw, rs.Action);
        Assert.Equal(ResearchQuery.ReserveDrawMilli(w, c, Me), rs.AmountMilli);
        Assert.True(rs.AmountMilli > 0);
        Assert.StartsWith("+", rs.Funding);
        Assert.Contains(" RP from the reserve this turn · reserve ", rs.Funding, StringComparison.Ordinal);
        Assert.Contains("; " + rs.Funding + ".", Card(w), StringComparison.Ordinal);
        ResearchBlock block = Assert.IsType<ResearchBlock>(Surface(w).Research);
        Assert.Equal(rs, block.Reserve);
        Assert.EndsWith(" reserve", StatusFigures.Research(block).Text);
        Assert.Equal(cap - rs.AmountMilli, ResearchQuery.ReserveMilli(Step(w), Me));
    }

    [Fact]
    public void WithTheSwitchOff_NoSurfaceCarriesAReserve()
    {
        IReadOnlyWorldState w = fx.Session.World;
        Assert.Null(fx.Content.Tuning.Reserve);
        Assert.Null(ResearchQuery.Reserve(w, fx.Content, Me));
        ActionSurfaceModel m = ActionSurface.Build(new ActionSurfaceInput(w, null, fx.Session.Config, Era, Me, -1, [],
            id => fx.Session.Names.Name(id), EraThemes.For(UiEras.Of(w, fx.Session.Config.Ages, Me))));
        Assert.Null(Assert.IsType<ResearchBlock>(m.Research).Reserve);
        var s = new ProgressionScreen(fx.Content, Me) { Config = fx.Session.Config };
        s.Refresh(w);
        Assert.Null(s.Snapshot!.Reserve);
        Assert.DoesNotContain("reserve", InfoQuery.Card(w, fx.Session.Config, Me, InfoSubject.Research,
            ActionQueryContext.ForNextStep(w, Era)).StatusLine, StringComparison.Ordinal);
    }
}
