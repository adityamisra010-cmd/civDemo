using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Actions;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M6 Track EX (D-062 D9; ADR-044) — THE ACTIONS MENU AND THE FOG ON THE SCREEN. The POLICY surface keeps its Construction
/// block (Build Granary) and gains a SEPARATE headed "Actions" block: per controlled settlement, Explore surroundings
/// (range r → r + 48 km, cost 60 grain) and Prospect (survey: 40 grain, 20 timber, 10 stone; the assay beside it, locked
/// until Ore prospecting — ADR-046), with the blocker the predicate gives (radius cap, already surveyed, which goods are
/// short, already ordered this turn); an order the step refused at delivery is explained after
/// the step. Its two buttons go through the session's
/// guarded emitters, which append exactly the order DiscoverySystem would apply and nothing else. The map draws UNKNOWN
/// land as blank parchment and REMEMBERED land under a darker wash; a prospect makes a hidden deposit appear in the
/// overlay's ore markers.
/// </summary>
public class DiscoveryActionsUiTests
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static UiSession Dev() => UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);

    private static int Capital(IReadOnlyWorldState w) => EmpireQuery.TryGetCapital(w, Me, out SettlementId c) ? c.Value : -1;

    private static ActionSurfaceModel Surface(UiSession s) =>
        ActionSurface.ForSession(s, UiSession.ProductionEra(), Capital(s.World), EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)));

    /// <summary>The session's world with the player's grain set to <paramref name="keep"/> in every settlement it holds.</summary>
    private static UiSession WithGrain(UiSession s, long keep)
    {
        WorldState w = s.World.Clone();
        GoodId g = new(s.Config.Goods!.GrainId);
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (w.GoodStocks[i].Good != g || !EmpireQuery.ControlsSettlement(w, Me, w.GoodStocks[i].Settlement)) continue;
            long delta = w.GoodStocks[i].Amount.Value - keep;
            if (delta == 0) continue;
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(g),
                ReasonIds.InitialEndowment, Math.Abs(delta), delta > 0 ? FlowDirection.Sink : FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return UiSession.StartFrom(w, 42, 256, 4);
    }

    /// <summary>ADR-046: the session's world with <paramref name="keep"/> of every NON-grain good of the survey bill (timber,
    /// stone) in every settlement the player holds — the founding store holds grain only, and stone arrives with the first
    /// extraction.</summary>
    private static UiSession WithSurveyMaterials(UiSession s, long keep = 100)
    {
        WorldState w = s.World.Clone();
        foreach ((GoodId g, long _) in DiscoveryQuery.ProspectBill(s.Config, ProspectMethod.Survey))
        {
            if (g.Value == s.Config.Goods!.GrainId) continue;
            for (int i = 0; i < w.GoodStocks.Count; i++)
            {
                if (w.GoodStocks[i].Good != g || !EmpireQuery.ControlsSettlement(w, Me, w.GoodStocks[i].Settlement)) continue;
                long delta = w.GoodStocks[i].Amount.Value - keep;
                if (delta == 0) continue;
                new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(g),
                    ReasonIds.InitialEndowment, Math.Abs(delta), delta > 0 ? FlowDirection.Sink : FlowDirection.Source, OverdrawPolicy.Throw);
            }
        }
        return UiSession.StartFrom(w, 42, 256, 4);
    }

    [Fact]
    public void TheActionsMenu_ListsExploreAndProspect_PerControlledSettlement_WithCostsAndBlockers_AndBuildingStillListsTheGranary()
    {
        UiSession s = WithSurveyMaterials(Dev());
        ActionSurfaceModel m = Surface(s);
        Assert.NotNull(m.Discovery);
        Assert.Contains(ActionDomain.Discovery, m.Domains);
        int controlled = 0;
        for (int i = 0; i < s.World.Settlements.Count; i++) if (EmpireQuery.ControlsSettlement(s.World, Me, s.World.Settlements[i].Id)) controlled++;
        Assert.Equal(controlled, m.Discovery!.Entries.Length);
        foreach (DiscoveryEntry e in m.Discovery.Entries)
        {
            Assert.Equal(OrderKind.Explore, e.Explore!.Order);
            Assert.Equal("range 48 km → 96 km, cost 60 grain", e.Explore.Detail);   // ADR-046: km, not lattice nodes
            Assert.Null(e.Explore.Blocker);                            // the founding store covers an expedition
            Assert.Equal(OrderKind.Prospect, e.Prospect!.Order);
            Assert.Equal("survey, cost 40 grain, 20 timber, 10 stone", e.Prospect.Detail);
            Assert.Null(e.Prospect.Blocker);                           // the bill is covered
            Assert.Equal(OrderKind.Prospect, e.Assay!.Order);          // ADR-046: the assay, listed and locked
            Assert.Equal("assay, cost 20 grain, 10 timber, 5 stone, 2 bronze", e.Assay.Detail);
            Assert.Equal("needs Ore prospecting", e.Assay.Blocker);
        }
        // Construction is its own block, unchanged: the granary is still listed there, not in Actions.
        Assert.NotNull(m.Construction);
        Assert.Contains(m.Construction!.Projects, p => p.Name.Contains("ranary", StringComparison.Ordinal));

        // Painted: an "Actions" heading, an Explore and a Prospect hit per settlement.
        var screen = new ActionSurfaceScreen { Theme = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)) };
        screen.Refresh(m, s.World, s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 360);
        bool heading = false;
        foreach (DrawCmd c in d.Commands) if (c is TextCmd t && t.Text.Contains("Actions", StringComparison.Ordinal)) heading = true;
        Assert.True(heading);
        int explore = 0, prospect = 0, assay = 0;
        foreach (ActionHit h in screen.Hits)
        {
            if (h.Kind == ActionHitKind.Explore) explore++;
            if (h.Kind == ActionHitKind.Prospect) prospect++;
            if (h.Kind == ActionHitKind.Assay) assay++;
        }
        Assert.Equal(controlled, explore);
        Assert.Equal(controlled, prospect);
        Assert.Equal(controlled, assay);
        // No order is shown without its full bill, and the founding world (no stone yet) says which goods are short.
        DiscoveryEntry founding = Surface(Dev()).Discovery!.Entries.First(x => x.Settlement.Value == Capital(s.World));
        Assert.StartsWith("not enough in store (", founding.Prospect!.Blocker);
        Assert.Contains("stone short", founding.Prospect.Blocker);
    }

    [Fact]
    public void TheEmitters_AppendOnlyWhatTheSystemApplies_OncePerSettlementPerTurn_AndTheLogReplays()
    {
        UiSession s = WithSurveyMaterials(Dev());
        int capital = Capital(s.World);
        // An empty purse: Prospect and Explore refused, nothing appended.
        UiSession poor = WithGrain(WithSurveyMaterials(Dev()), 0);
        Assert.False(poor.EmitProspect(capital));
        Assert.False(poor.EmitExplore(capital));
        Assert.False(poor.EmitProspect(capital, ProspectMethod.Assay));   // locked as well
        Assert.Equal(0, poor.Orders.Count);
        Assert.Equal("not enough in store (40 grain short)", Surface(poor).Discovery!.Entries.First(x => x.Settlement.Value == capital).Prospect!.Blocker);
        // Explore: admitted once; a second the same turn is refused (DiscoverySystem applies one) and the menu says why.
        Assert.True(s.EmitExplore(capital));
        Assert.Equal(OrderRecord.From(s.World.Clock.Turn, Me, OrderKind.Explore, capital, 0.0), s.Orders[^1]);
        Assert.False(s.EmitExplore(capital));
        Assert.Equal(1, s.Orders.Count);
        DiscoveryEntry e = Surface(s).Discovery!.Entries.First(x => x.Settlement.Value == capital);
        Assert.Equal("already ordered this turn", e.Explore!.Blocker);
        // A settlement the player does not control: refused.
        Assert.False(s.EmitExplore(9999));
        Assert.False(s.EmitProspect(9999));
        s.EndTurn();
        Assert.Equal(6, DiscoveryQuery.ExploredRadius(s.World, s.Config.Fog!, Me, new SettlementId(capital)));

        // A prospect is admitted once, then the settlement is surveyed and the menu says so.
        UiSession t = WithSurveyMaterials(Dev());
        Assert.True(t.EmitProspect(capital));
        Assert.Equal(0.0, t.Orders[^1].Amount);                    // the survey's method
        Assert.False(t.EmitProspect(capital));
        Assert.False(t.EmitProspect(capital, ProspectMethod.Assay)); // one Prospect per settlement per turn, any method
        t.EndTurn();
        Assert.True(DepositConcealment.HasSurveyed(t.World, Me, new SettlementId(capital)));
        Assert.Equal("already surveyed", Surface(t).Discovery!.Entries.First(x => x.Settlement.Value == capital).Prospect!.Blocker);
        Assert.False(t.EmitProspect(capital));

        // The log the UI wrote replays hash for hash.
        WorldState world = WithSurveyMaterials(UiSession.StartFrom(UiFounding.Found(42, 256, 4), 42, 256, 4)).World;
        OrderValidation.ValidateAgainstWorld(s.Orders, world);
        WorldState replay = UiSession.BuildProductionExecutor(s.Orders).Step(world);
        Assert.Equal(WorldHash.ComputeHex(s.World), WorldHash.ComputeHex(replay));
    }

    [Fact]
    public void TheButtons_DispatchThroughTheGuardedEmitters()
    {
        UiSession s = WithSurveyMaterials(Dev());
        int capital = Capital(s.World);
        var screen = new ActionSurfaceScreen { Theme = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)) };
        screen.Refresh(Surface(s), s.World, s.Config, Me, id => s.Names.Name(id));
        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        ActionHit explore = screen.Hits.First(h => h.Kind == ActionHitKind.Explore && h.A == capital);
        ActionCommand cmd = screen.Click(explore.Rect.CenterX, explore.Rect.CenterY);
        Assert.Equal(new ActionCommand(ActionCommandKind.Explore, capital), cmd);
        Assert.True(cmd.IsOrder);
        Assert.True(ActionDispatch.Apply(s, cmd));
        Assert.Equal(OrderKind.Explore, s.Orders[^1].Kind);
        ActionHit prospect = screen.Hits.First(h => h.Kind == ActionHitKind.Prospect && h.A == capital);
        ActionCommand p = screen.Click(prospect.Rect.CenterX, prospect.Rect.CenterY);
        Assert.Equal(ActionCommandKind.Prospect, p.Kind);
        Assert.True(ActionDispatch.Apply(s, p));    // the survey bill is covered
        Assert.Equal(OrderKind.Prospect, s.Orders[^1].Kind);
        Assert.False(ActionDispatch.Apply(s, p));   // once per settlement per turn: the emitter refuses, nothing appended
        Assert.Equal(2, s.Orders.Count);
        // ADR-046: the Assay button dispatches the assay command, and its emitter refuses while the technology is unknown.
        ActionHit assay = screen.Hits.First(h => h.Kind == ActionHitKind.Assay && h.A == capital);
        ActionCommand a = screen.Click(assay.Rect.CenterX, assay.Rect.CenterY);
        Assert.Equal(new ActionCommand(ActionCommandKind.Assay, capital), a);
        Assert.True(a.IsOrder);
        Assert.False(ActionDispatch.Apply(s, a));
        Assert.Equal(2, s.Orders.Count);
    }

    [Fact]
    public void TheMap_DrawsUnknownAsParchmentAndRememberedUnderAWash_AndTheSwitchOffDrawsNoFog()
    {
        UiSession s = Dev();
        WorldProjection p = WorldProjection.Build(s.World, s.Config, s.Names.Name, Me, knowledge: s.Knowledge);
        Assert.NotEmpty(p.Fog);
        Assert.Contains(p.Fog, f => f.Unknown);
        Assert.Contains(p.Fog, f => !f.Unknown);   // the seed disc beyond the settlement's own node: remembered
        // Every run covers nodes of its own state only.
        (int l, int stride) = WorldProjection.LatticeGrid(s.World.Terrain!.Size);
        foreach (FogRun f in p.Fog)
            for (double x = f.X; x < f.X + f.W - 0.5; x += stride)
            {
                int node = (int)(f.Y / stride) * l + (int)(x / stride);
                Assert.Equal(f.Unknown, !s.Knowledge.Known(node));
                Assert.False(s.Knowledge.Visible(node));
            }
        PlayerKnowledge all = PlayerKnowledge.Of(s.World, Me, TestFog.Off(s.Config));
        Assert.Empty(WorldProjection.Build(s.World, s.Config, s.Names.Name, Me, knowledge: all).Fog);
    }

    [Fact]
    public void AfterAProspect_TheDiscoveredDepositAppearsInTheOverlaysOreMarkers()
    {
        UiSession t = WithSurveyMaterials(Dev());
        // A player settlement with a concealed deposit whose good the overlay marks (a seeded spread).
        int at = -1, good = -1;
        for (int i = 0; i < t.World.Settlements.Count && at < 0; i++)
        {
            SettlementId id = t.World.Settlements[i].Id;
            if (!EmpireQuery.ControlsSettlement(t.World, Me, id)) continue;
            foreach (int g in DepositConcealment.ConcealedGoods(t.World.Deposits, t.World.Seed, t.Config, id))
                if (OreGood(t, g)) { at = id.Value; good = g; break; }
        }
        Assert.True(at >= 0, "no player settlement conceals a marked deposit on this world");
        bool Marked(UiSession x)
        {
            foreach (OreRoll r in x.Overlay!.Status(x.World, x.Config, x.Knowledge).Rolls) if (r.Settlement == at && r.Good == good) return true;
            return false;
        }
        Assert.False(Marked(t));
        Assert.True(t.EmitProspect(at));
        t.EndTurn();
        Assert.True(Marked(t));
    }

    private static bool OreGood(UiSession s, int good) => s.Config.Goods!.ById(good).DepositSpread > 0.0;

    /// <summary>ADR-044 §5: the preview judges affordability on the published stocks; the step draws from what its own
    /// production, meals and building left. An order the preview admitted but the step refused is EXPLAINED in the Actions
    /// block after the step — never a silent no-cost refusal. Rig: 45 grain in every player settlement (the preview admits a
    /// 40-grain survey); the stepped world is the one the step produced when its draw found the store empty (DiscoverySystem
    /// alone over the store emptied after the preview — the step-side refusal itself is pinned in Sim.Tests' DiscoveryTests),
    /// and the surface built on (previous, stepped, delivered) says so, painted.</summary>
    [Fact]
    public void AnOrderTheStepRefusesAtDelivery_IsExplainedInTheActionsBlockAfterTheStep()
    {
        UiSession s = WithGrain(WithSurveyMaterials(Dev()), 45);
        int capital = Capital(s.World);
        var cap = new SettlementId(capital);
        Assert.Null(Surface(s).Discovery!.Entries.Single(e => e.Settlement == cap).Prospect!.Blocker);   // the preview admits it
        Assert.True(s.EmitProspect(capital));
        OrderRecord order = Assert.Single(s.QueuedOrders());

        WorldState prev = s.World.Clone();
        WorldState drawn = WithGrain(s, 0).World.Clone();   // what the step's draw found: nothing
        var log = new OrderLog();
        log.Append(order);
        WorldState next = new TurnExecutor(UiSession.ProductionEra(), [SystemCatalog.Discovery(s.Config)], log).Step(drawn);
        Assert.False(DepositConcealment.HasSurveyed(next, Me, cap));   // refused at delivery, nothing paid

        ActionSurfaceModel m = ActionSurface.Build(new ActionSurfaceInput(next, prev, s.Config, UiSession.ProductionEra(), Me,
            capital, [], id => s.Names.Name(id), EraThemes.For(UiEras.Of(next, s.Config.Ages, Me)), [order]));
        string line = Assert.Single(m.Discovery!.Refused);
        Assert.Equal(DiscoveryQuery.RefusalText(OrderKind.Prospect, s.Names.Name(capital), "40 grain, 20 timber, 10 stone"), line);
        var screen = new ActionSurfaceScreen { Theme = EraThemes.For(UiEras.Of(next, s.Config.Ages, Me)) };
        screen.Refresh(m, next, s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 360);
        var painted = new List<string>();   // the blocker line paints word by word (its rich-word layout)
        foreach (DrawCmd c in d.Commands) if (c is TextCmd t) painted.Add(t.Text);
        string text = string.Join(" ", painted);
        Assert.Contains("last turn: " + line, text, StringComparison.Ordinal);

        // The live session: the step delivered the order (UiSession.LastDelivered) and, its draw covering the survey, applied
        // it — so nothing is explained; the step after, with nothing delivered, explains nothing either.
        s.EndTurn();
        Assert.Equal([order], s.LastDelivered);
        Assert.True(DepositConcealment.HasSurveyed(s.World, Me, cap));
        Assert.True(Surface(s).Discovery!.Refused.IsDefaultOrEmpty);
        s.EndTurn();
        Assert.Empty(s.LastDelivered);
        Assert.True(Surface(s).Discovery!.Refused.IsDefaultOrEmpty);
    }
}

/// <summary>M6 Track EX (2026-10-22): the discovery layer OFF for a UI test whose subject is an OLDER layer premised on the
/// all-known production view (P7–P9, S1d placement, the overlays): the Sim.Ui twin of Sim.Tests' TestConfigs.PreDiscovery,
/// used through <see cref="UiSession.StartWith"/> / <see cref="UiSession.StartFromWith"/>. The fogged production view has
/// its own tests (DiscoveryActionsUiTests, KnowledgeLeakTests' stub rigs keep running on the production config where they
/// do not assume all-known).</summary>
internal static class TestFog
{
    public static SimConfig Off(SimConfig cfg) =>
        cfg with { Fog = cfg.Fog! with { Enabled = false }, Discovery = cfg.Discovery! with { Enabled = false } };

    private static readonly Lazy<SimConfig> PreDiscoveryConfig = new(() => Off(UiFounding.ProductionConfig()));

    /// <summary>The production config with the discovery layer off.</summary>
    public static SimConfig PreDiscovery => PreDiscoveryConfig.Value;
}
