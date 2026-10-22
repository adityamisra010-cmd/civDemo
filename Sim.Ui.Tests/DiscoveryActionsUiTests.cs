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
/// (radius r → r + 3 nodes, cost 60 grain) and Prospect (cost 4 tools), with the blocker the predicate gives (radius cap,
/// already surveyed, not enough tools or grain, already ordered this turn). Its two buttons go through the session's
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

    private static UiSession WithTools(UiSession s, long tools)
    {
        WorldState w = s.World.Clone();
        GoodId t = new(s.Config.Goods!.IdOf("tools"));
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (w.GoodStocks[i].Good != t || !EmpireQuery.ControlsSettlement(w, Me, w.GoodStocks[i].Settlement)) continue;
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(t),
                ReasonIds.InitialEndowment, tools, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return UiSession.StartFrom(w, 42, 256, 4);
    }

    [Fact]
    public void TheActionsMenu_ListsExploreAndProspect_PerControlledSettlement_WithCostsAndBlockers_AndBuildingStillListsTheGranary()
    {
        UiSession s = Dev();
        ActionSurfaceModel m = Surface(s);
        Assert.NotNull(m.Discovery);
        Assert.Contains(ActionDomain.Discovery, m.Domains);
        int controlled = 0;
        for (int i = 0; i < s.World.Settlements.Count; i++) if (EmpireQuery.ControlsSettlement(s.World, Me, s.World.Settlements[i].Id)) controlled++;
        Assert.Equal(controlled, m.Discovery!.Entries.Length);
        foreach (DiscoveryEntry e in m.Discovery.Entries)
        {
            Assert.Equal(OrderKind.Explore, e.Explore!.Order);
            Assert.Equal("radius 3 → 6 nodes, cost 60 grain", e.Explore.Detail);
            Assert.Null(e.Explore.Blocker);                            // the founding store covers an expedition
            Assert.Equal(OrderKind.Prospect, e.Prospect!.Order);
            Assert.Equal("cost 4 tools", e.Prospect.Detail);
            Assert.Equal("not enough tools (needs 4)", e.Prospect.Blocker);   // no tools at the founding (ADR-044 §5)
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
        int explore = 0, prospect = 0;
        foreach (ActionHit h in screen.Hits) { if (h.Kind == ActionHitKind.Explore) explore++; if (h.Kind == ActionHitKind.Prospect) prospect++; }
        Assert.Equal(controlled, explore);
        Assert.Equal(controlled, prospect);
    }

    [Fact]
    public void TheEmitters_AppendOnlyWhatTheSystemApplies_OncePerSettlementPerTurn_AndTheLogReplays()
    {
        UiSession s = Dev();
        int capital = Capital(s.World);
        // Prospect without tools: refused, nothing appended.
        Assert.False(s.EmitProspect(capital));
        Assert.Equal(0, s.Orders.Count);
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

        // With tools: a prospect is admitted once, then the settlement is surveyed and the menu says so.
        UiSession t = WithTools(Dev(), 1_000);
        Assert.True(t.EmitProspect(capital));
        Assert.False(t.EmitProspect(capital));
        t.EndTurn();
        Assert.True(DepositConcealment.HasSurveyed(t.World, Me, new SettlementId(capital)));
        Assert.Equal("already surveyed", Surface(t).Discovery!.Entries.First(x => x.Settlement.Value == capital).Prospect!.Blocker);
        Assert.False(t.EmitProspect(capital));

        // The log the UI wrote replays hash for hash.
        WorldState world = UiFounding.Found(42, 256, 4);
        OrderValidation.ValidateAgainstWorld(s.Orders, world);
        WorldState replay = UiSession.BuildProductionExecutor(s.Orders).Step(world);
        Assert.Equal(WorldHash.ComputeHex(s.World), WorldHash.ComputeHex(replay));
    }

    [Fact]
    public void TheButtons_DispatchThroughTheGuardedEmitters()
    {
        UiSession s = Dev();
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
        Assert.False(ActionDispatch.Apply(s, p));   // no tools: the emitter refuses, nothing appended
        Assert.Equal(1, s.Orders.Count);
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
        UiSession t = WithTools(Dev(), 1_000);
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
