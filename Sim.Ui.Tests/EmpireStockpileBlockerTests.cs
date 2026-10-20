using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Actions;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-051 §6 applied to construction (the empire purse) AT THE UI: a settlement the player's Empire controls is told what
/// its EMPIRE holds ("needs 40 timber (empire has 10)") — the purse ConstructionSystem gates the head on and draws from —
/// and the action surface still sets every missing quantity in bold (UR-6), now after "(empire has" as after "(has".
/// </summary>
public class EmpireStockpileBlockerTests(CanonicalTurnOneFixture fx) : IClassFixture<CanonicalTurnOneFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static void Set(WorldState w, SettlementId s, int good, long qty)
    {
        var id = new GoodId(good);
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long have = w.GoodStocks[idx].Amount.Value;
        if (have == qty) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id), ReasonIds.InitialEndowment,
            Math.Abs(qty - have), qty > have ? FlowDirection.Source : FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    [Fact]
    public void TheCapitalsGranary_ReadsTheEmpiresHolding_AndTheMissingQuantitiesAreBold()
    {
        UiSession from = fx.Session;
        WorldState w = from.World.Clone();
        Assert.True(EmpireQuery.TryGetCapital(w, Me, out SettlementId capital));
        int timber = from.Config.Goods!.IdOf("timber"), stone = from.Config.Goods!.IdOf("stone");
        // Stone and timber held ELSEWHERE in the player's Empire, none at the capital.
        SettlementId[] mine = LabourActivities.ControlledSettlements(w, Me);
        SettlementId other = mine[0] == capital ? mine[1] : mine[0];
        Set(w, capital, timber, 0);
        Set(w, capital, stone, 0);
        Set(w, other, timber, 10);
        Set(w, other, stone, 6);
        long timberHeld = EmpireStockpile.Available(w.GoodStocks, ConstructionQuery.Holders(w, from.Config, capital), new GoodId(timber));
        long stoneHeld = EmpireStockpile.Available(w.GoodStocks, ConstructionQuery.Holders(w, from.Config, capital), new GoodId(stone));
        Assert.True(timberHeld >= 10 && timberHeld < 40 && stoneHeld >= 6 && stoneHeld < 20, $"fixture: purse {timberHeld} timber, {stoneHeld} stone");

        UiSession s = UiSession.StartFrom(w, from.World.Seed);
        EraTheme th = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me));
        ActionSurfaceModel model = ActionSurface.ForSession(s, UiSession.ProductionEra(), capital.Value, th);
        ProjectEntry granary = model.Construction!.Projects.Single(p => p.ProjectId == 1);
        string expected = $"needs 40 timber (empire has {timberHeld}), 20 stone (empire has {stoneHeld})";
        Assert.StartsWith(expected, granary.Blocker);

        var screen = new ActionSurfaceScreen { Theme = th };
        screen.Refresh(model, s.World, s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 452);
        List<TextCmd> texts = d.Commands.OfType<TextCmd>().ToList();
        foreach (string word in new[] { "40", "timber", "20", "stone" })
            Assert.Contains(texts, c => c.Text == word && c.Role == FontRole.Heading && c.Color == th.Ink.Text);
        Assert.Contains(texts, c => c.Text == "(empire" && c.Role == FontRole.Body);   // the holder is not emphasised
    }

    [Fact]
    public void AWorkshopWhoseToolsAreClaimed_IsLockedWithTheClaimNamed_AndAPendingWorkshopClaimsAtOnce()
    {
        // A-1: the empire HOLDS the ten tools (at another settlement), but a workshop queued there already claims them — the
        // locked line names the claim, not "<capital> cannot make yet". A-5: a workshop ordered THIS turn (pending, not yet
        // delivered) claims them the same way, so the capital's workshop is not offered beside it.
        UiSession from = fx.Session;
        WorldState w = from.World.Clone();
        Assert.True(EmpireQuery.TryGetCapital(w, Me, out SettlementId capital));
        int tools = from.Config.Goods!.IdOf("tools");
        SettlementId[] mine = LabourActivities.ControlledSettlements(w, Me);
        SettlementId other = mine[0] == capital ? mine[1] : mine[0];
        foreach (SettlementId s in mine) Set(w, s, tools, 0);
        Set(w, other, tools, 10);
        const int Workshop = 2;
        EraTheme th = EraThemes.For(UiEras.Of(w, from.Config.Ages, Me));
        ActionSurfaceModel Model(IReadOnlyWorldState world, IReadOnlyList<Sim.Core.Kernel.OrderRecord> queued) =>
            ActionSurface.Build(new ActionSurfaceInput(world, null, from.Config, UiSession.ProductionEra(), Me, capital.Value, queued,
                id => from.Names.Name(id), th));
        string expected = "needs 10 tools; the empire's 10 are already claimed by projects queued at " + from.Names.Name(other.Value);

        // Nothing claimed: the capital's workshop is listed, nothing locked for it.
        ActionSurfaceModel free = Model(w, []);
        Assert.Contains(free.Construction!.Projects, p => p.ProjectId == Workshop);
        Assert.DoesNotContain(free.Locked, l => l.Subject == InfoSubject.OfProject(Workshop, capital.Value));

        // A workshop QUEUED at the other settlement claims the ten.
        WorldState queued = w.Clone();
        queued.ConstructionQueue.Add(new ConstructionQueueRow(other, 0, Workshop));
        ActionSurfaceModel claimed = Model(queued, []);
        Assert.DoesNotContain(claimed.Construction?.Projects ?? [], p => p.ProjectId == Workshop);
        LockedDomain locked = Assert.Single(claimed.Locked, l => l.Subject == InfoSubject.OfProject(Workshop, capital.Value));
        Assert.Equal(expected, locked.Reason);
        Assert.DoesNotContain("cannot make yet", locked.Reason);

        // A workshop ORDERED this turn at the other settlement (pending) claims them at once — the same refusal, the same words.
        var pending = new[] { ConstructionQuery.EnqueueOrder(w, Me, other, Workshop) };
        ActionSurfaceModel ordered = Model(w, pending);
        Assert.DoesNotContain(ordered.Construction?.Projects ?? [], p => p.ProjectId == Workshop);
        Assert.Equal(expected, Assert.Single(ordered.Locked, l => l.Subject == InfoSubject.OfProject(Workshop, capital.Value)).Reason);
    }
}
