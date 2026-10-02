using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Worldgen;
using Sim.Ui.Ages;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>A real seed-42 session (256 px, 4 settlements) played through the real order pathway
/// until the player is eligible for Age II (<see cref="AgePreview.PlayToEligible"/>), plus the same
/// session's turn-12 world (not eligible). Shared read-only by the class; tests that mutate build their own.</summary>
public sealed class EligibleSessionFixture
{
    public UiSession Session { get; }
    public WorldState Early { get; }
    public int TurnsToEligible { get; }

    public EligibleSessionFixture()
    {
        Session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        for (int t = 0; t < 12; t++)
        {
            if (!Sim.Core.State.ResearchQuery.TryGetTarget(Session.World, UiPlayer.Empire, out _)
                && AgePreview.NextNode(Session.World, Session.Config.Research!, Session.Config.Ages!, UiPlayer.Empire) is { } n)
                Session.EmitResearchOrder(n);
            Session.EndTurn();
        }
        Early = Session.World;
        TurnsToEligible = AgePreview.PlayToEligible(Session);
    }
}

/// <summary>
/// ADR-031 UI (docs/architecture/age-and-world-ui.md): the capital Age panel's three states on real
/// stepped worlds, the advance flow's order and its read-from-content lists, the absence of numeric
/// surge values, the AI order path, read-only rendering, and the zoom-dependent world lens.
/// </summary>
public class AgeAndWorldUiTests(EligibleSessionFixture fx) : IClassFixture<EligibleSessionFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private AgeContent Ages => fx.Session.Config.Ages!;
    private UnitFamilyContent Families => fx.Session.Config.UnitFamilies!;

    private static List<TextCmd> Texts(DrawList d)
    {
        var r = new List<TextCmd>();
        foreach (DrawCmd c in d.Commands) if (c is TextCmd t) r.Add(t);
        return r;
    }

    // ------------------------------------------------------------------ wiring

    [Fact]
    public void Session_LoadsAgesAndUnitFamilies_AndFoundsTheWarband()
    {
        Assert.NotNull(fx.Session.Config.Ages);
        Assert.NotNull(fx.Session.Config.UnitFamilies);
        Assert.NotEmpty(MilitaryQuery.Units(fx.Early, Me));
        // The Age systems ran: the eligibility table is rebuilt every step.
        Assert.NotEqual(0, fx.Session.World.AgeEligibility.Count);
    }

    // ------------------------------------------------------------------ panel states

    [Fact]
    public void Panel_NotEligible_ShowsProgressOnly_NoActiveAdvanceButton()
    {
        Assert.False(AgeQuery.IsEligible(fx.Early, Ages, Me));
        AgePanelModel p = AgePanelModel.Build(fx.Early, Ages, [], Me);
        Assert.Equal(AgePanelState.NotEligible, p.State);
        Assert.False(p.CanAdvance);
        Assert.Equal(1, p.CurrentAge);
        Assert.Equal("Prehistoric / Stone Age", p.CurrentAgeName);
        Assert.Equal("Neolithic / Agricultural", p.NextAgeName);
        AgeEligibilityReport r = AgeQuery.Evaluate(fx.Early, Ages, Me)!;
        Assert.Equal(r.Core.Count, p.Core.Count);
        for (int i = 0; i < r.Core.Count; i++) Assert.Equal((r.Core[i].Milestone.Id, r.Core[i].Met), (p.Core[i].Id, p.Core[i].Met));
        Assert.Equal(5, p.Categories.Count);
        for (int k = 0; k < 5; k++) Assert.Equal((r.CategoryMask & (1 << k)) != 0, p.Categories[k].Covered);
        Assert.Equal(r.Remaining, p.Remaining);
        Assert.NotEmpty(p.Remaining);

        var screen = new AgeScreen(Me);
        screen.Refresh(fx.Early, Ages, Families, []);
        var d = new DrawList();
        screen.PaintPanel(d, ApproxTextMeasure.Instance, new RectD(0, 0, 500, 900), "Capital");
        Assert.DoesNotContain(screen.Hits, h => h.Kind == AgeHit.OpenAdvance);
        Assert.DoesNotContain(Texts(d), t => t.Text == "ADVANCE AGE");
        Assert.Contains(Texts(d), t => t.Text == "Not yet eligible");
        screen.OpenFlow();
        Assert.False(screen.FlowOpen);
    }

    [Fact]
    public void Panel_Eligible_ShowsAdvanceButton_AndOptionalState()
    {
        Assert.True(AgeQuery.IsEligible(fx.Session.World, Ages, Me), $"not eligible after {fx.TurnsToEligible} turns");
        AgePanelModel p = AgePanelModel.Build(fx.Session.World, Ages, fx.Session.QueuedOrders(), Me);
        Assert.Equal(AgePanelState.Eligible, p.State);
        Assert.True(p.CanAdvance);
        Assert.Empty(p.Remaining);
        var screen = new AgeScreen(Me);
        screen.Refresh(fx.Session.World, Ages, Families, fx.Session.QueuedOrders());
        var d = new DrawList();
        screen.PaintPanel(d, ApproxTextMeasure.Instance, new RectD(0, 0, 500, 900), "Capital");
        AgeHitRegion btn = Assert.Single(screen.Hits, h => h.Kind == AgeHit.OpenAdvance);
        Assert.Contains(Texts(d), t => t.Text == "Advancement available - optional");
        Assert.Contains(Texts(d), t => t.Text == "ADVANCE AGE");
        screen.Click(btn.Rect.CenterX, btn.Rect.CenterY);
        Assert.True(screen.FlowOpen);
    }

    [Fact]
    public void Confirm_DispatchesExactlyAdvanceOrder_WithChosenSurge_ThenPending_ThenNextTurnInNewAge()
    {
        // A fresh session played to the same eligible state (this test mutates its log).
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        AgePreview.PlayToEligible(s);
        Assert.True(AgeQuery.IsEligible(s.World, Ages, Me));
        string hash = WorldHash.ComputeHex(s.World);

        var screen = new AgeScreen(Me);
        screen.Refresh(s.World, Ages, Families, s.QueuedOrders());
        screen.OpenFlow();
        Assert.Equal(AgeCommand.None, screen.Confirm());       // no surge chosen: nothing
        int surge = Ages.Surges[^1].Key;
        var d = new DrawList();
        screen.PaintFlow(d, ApproxTextMeasure.Instance, 1600, 1000);
        AgeHitRegion card = Assert.Single(screen.Hits, h => h.Kind == AgeHit.Surge && h.Arg == surge);
        screen.Click(card.Rect.CenterX, card.Rect.CenterY);
        Assert.Equal(surge, screen.SelectedSurge);
        screen.PaintFlow(d = new DrawList(), ApproxTextMeasure.Instance, 1600, 1000);
        AgeHitRegion confirm = Assert.Single(screen.Hits, h => h.Kind == AgeHit.Confirm);
        AgeCommand cmd = screen.Click(confirm.Rect.CenterX, confirm.Rect.CenterY);

        OrderRecord expected = AgeQuery.AdvanceOrder(s.World, Me, 2, surge);
        Assert.Equal(expected, cmd.Order);
        Assert.Equal(hash, WorldHash.ComputeHex(s.World));     // the click wrote nothing

        int before = s.Orders.Count;
        Assert.True(s.EmitAdvanceAge(cmd.Order!.Value.TargetId, cmd.SurgeKey));
        Assert.Equal(before + 1, s.Orders.Count);
        Assert.Equal(expected, s.Orders[before]);
        Assert.False(s.EmitAdvanceAge(2, surge));               // already pending: nothing more
        Assert.Equal(before + 1, s.Orders.Count);

        AgePanelModel pending = AgePanelModel.Build(s.World, Ages, s.QueuedOrders(), Me);
        Assert.Equal(AgePanelState.Pending, pending.State);
        Assert.False(pending.CanAdvance);
        Assert.Equal(Ages.SurgeByKey(surge)!.Name, pending.PendingSurgeName);
        screen.Refresh(s.World, Ages, Families, s.QueuedOrders());
        screen.PaintPanel(d = new DrawList(), ApproxTextMeasure.Instance, new RectD(0, 0, 500, 900), "Capital");
        Assert.Contains(Texts(d), t => t.Text.StartsWith("Advancing to Neolithic / Agricultural next turn", StringComparison.Ordinal));
        Assert.DoesNotContain(screen.Hits, h => h.Kind == AgeHit.OpenAdvance);

        long decided = s.World.Clock.Turn;
        s.EndTurn();
        Assert.Equal(2, AgeQuery.CurrentAge(s.World, Ages, Me));
        AgeStateRow row = AgeQuery.StateRow(s.World, Me)!.Value;
        Assert.Equal((decided + 1, surge), (row.EnteredTurn, row.Surge));
        Assert.Equal(AgePanelState.NotEligible, AgePanelModel.Build(s.World, Ages, s.QueuedOrders(), Me).State);
    }

    [Fact]
    public void Player_IsNeverAutoAdvanced_WhileEligible()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        AgePreview.PlayToEligible(s);
        for (int t = 0; t < 3; t++) s.EndTurn();
        Assert.Equal(1, AgeQuery.CurrentAge(s.World, Ages, Me));
        for (int i = 0; i < s.Orders.Count; i++) Assert.NotEqual(OrderKind.AdvanceAge, s.Orders[i].Kind);
    }

    // ------------------------------------------------------------------ advance flow content

    [Fact]
    public void ModernizationPreview_EqualsMilitaryQuery_AndFamilyLinesEqualTheGraph()
    {
        WorldState w = fx.Session.World;
        AdvanceFlowModel f = AdvanceFlowModel.Build(w, Ages, Families, Me)!;
        UnitConversionPlan[] plan = MilitaryQuery.ModernizationPreview(w, Ages, Families, Me);
        ConversionSummary[] sum = MilitaryQuery.Summarize(Families, plan);
        Assert.Equal(plan.Length, f.Formations);
        Assert.Equal(sum.Length, f.Modernization.Count);
        Assert.NotEmpty(sum);
        for (int i = 0; i < sum.Length; i++)
            Assert.Equal((sum[i].From.Name, sum[i].To.Name, sum[i].Outcome, sum[i].Count),
                (f.Modernization[i].From, f.Modernization[i].To, f.Modernization[i].Outcome, f.Modernization[i].Count));
        FamilyLineChange[] lines = MilitaryQuery.FamilyLineChanges(Families, 1, 2);
        Assert.Equal(lines.Length, f.FamilyChanges.Count);
        for (int i = 0; i < lines.Length; i++)
            Assert.Equal((lines[i].Family.Name, lines[i].From?.Name, lines[i].To?.Name, lines[i].Outcome),
                (f.FamilyChanges[i].FamilyName, f.FamilyChanges[i].From, f.FamilyChanges[i].To, f.FamilyChanges[i].Outcome));
        // Surges are the content's, in key order.
        Assert.Equal(Ages.Surges.Count, f.Surges.Count);
        for (int i = 0; i < f.Surges.Count; i++) Assert.Equal((Ages.Surges[i].Key, Ages.Surges[i].Name), (f.Surges[i].Key, f.Surges[i].Name));
    }

    [Fact]
    public void SurgeCards_DisplayNoNumericValues()
    {
        var screen = new AgeScreen(Me);
        screen.Refresh(fx.Session.World, Ages, Families, fx.Session.QueuedOrders());
        screen.OpenFlow();
        screen.SelectedSurge = Ages.Surges[0].Key;
        var d = new DrawList();
        screen.PaintFlow(d, ApproxTextMeasure.Instance, 1600, 1000);
        var cards = new List<RectD>();
        foreach (AgeHitRegion h in screen.Hits) if (h.Kind == AgeHit.Surge) cards.Add(h.Rect);
        Assert.Equal(Ages.Surges.Count, cards.Count);
        int inside = 0;
        foreach (TextCmd t in Texts(d))
        {
            bool inCard = false;
            foreach (RectD c in cards) if (c.Contains(t.X, t.Y)) inCard = true;
            if (!inCard) continue;
            inside++;
            // Decision references such as "D-011" are names, not values; strip them, then no digit may remain.
            string scrubbed = System.Text.RegularExpressions.Regex.Replace(t.Text, @"\b[A-Z]{1,4}-\d+\b", "");
            Assert.False(scrubbed.Any(char.IsDigit), $"numeric surge text: '{t.Text}'");
            Assert.DoesNotContain("%", t.Text);
        }
        Assert.True(inside >= Ages.Surges.Count * 4);
    }

    // ------------------------------------------------------------------ AI orders

    [Fact]
    public void EndTurn_AppendsAiAdvanceOrders_FromThePolicy_BeforeTheStep()
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        WorldgenConfig wg;
        using (var st = global::Sim.Data.DataFiles.OpenWorldgen()) wg = WorldgenConfigLoader.Load(st);
        WorldState w = WorldFounding.Found(wg with { SizePx = 256, AiEmpires = 1 }, cfg, 42, 4);
        PolityId ai = default;
        bool found = false;
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Ai) { ai = w.Polities[i].Id; found = true; }
        Assert.True(found);
        // Constructed eligibility (the Sim.Tests EligibleForA2 precedent): core + one Technological supporting.
        foreach (string id in new[] { "cereal_cultivation", "pottery_open_fired" })
            w.ResearchCompleted.Add(new ResearchCompletedRow(ai, cfg.Research!.Nodes[cfg.Research.IndexOfId(id)].Key));
        Assert.True(AgeQuery.IsEligible(w, cfg.Ages!, ai));

        UiSession s = UiSession.StartFrom(w, 42, 256, 4);
        OrderRecord[] expected = AgeAdvancePolicy.OrdersForAi(s.World, cfg.Ages!);
        Assert.Single(expected);
        s.EndTurn();
        var appended = new List<OrderRecord>();
        for (int i = 0; i < s.Orders.Count; i++) if (s.Orders[i].Kind == OrderKind.AdvanceAge) appended.Add(s.Orders[i]);
        Assert.Equal(expected, appended);
        Assert.Equal(2, AgeQuery.CurrentAge(s.World, cfg.Ages!, ai));
        Assert.Equal(1, AgeQuery.CurrentAge(s.World, cfg.Ages!, Me));   // the player is never advanced by the policy
    }

    // ------------------------------------------------------------------ read-only

    [Fact]
    public void UiSurfaces_NeverChangeTheWorldHash()
    {
        WorldState w = fx.Session.World;
        string hash = WorldHash.ComputeHex(w);
        var screen = new AgeScreen(Me);
        screen.Refresh(w, Ages, Families, fx.Session.QueuedOrders());
        var d = new DrawList();
        screen.PaintPanel(d, ApproxTextMeasure.Instance, new RectD(0, 0, 500, 900), "Capital");
        screen.OpenFlow();
        screen.SelectedSurge = Ages.Surges[0].Key;
        screen.PaintFlow(d, ApproxTextMeasure.Instance, 1600, 1000);
        screen.Confirm();   // returns an order; does not log or apply it
        WorldProjection p = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me);
        foreach (WorldZoom z in new[] { WorldZoom.World, WorldZoom.Regional, WorldZoom.Settlement })
            WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, p, z, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024));
        var prog = new ProgressionScreen(fx.Session.Config.Research!, Me) { Age = AgePanelModel.Build(w, Ages, [], Me) };
        prog.Refresh(w);
        prog.Paint(1600, 1000, ApproxTextMeasure.Instance);
        Assert.Equal(hash, WorldHash.ComputeHex(w));
    }

    // ------------------------------------------------------------------ Knowledge screen Age header

    [Fact]
    public void KnowledgeScreen_AgeChip_ShowsState_AndOpensTheAgeSurface()
    {
        var prog = new ProgressionScreen(fx.Session.Config.Research!, Me)
        {
            Age = AgePanelModel.Build(fx.Session.World, Ages, fx.Session.QueuedOrders(), Me),
        };
        prog.Refresh(fx.Session.World);
        DrawList d = prog.Paint(1600, 1000, ApproxTextMeasure.Instance);
        HitRegion chip = Assert.Single(prog.Hits, h => h.Kind == HitKind.AgeOpen);
        Assert.Contains(Texts(d), t => t.Text == "ADVANCE AGE available");
        ProgressionCommand cmd = prog.Click(chip.Rect.CenterX, chip.Rect.CenterY);
        Assert.True(cmd.OpenAge);
        Assert.Null(cmd.Order);
    }

    // ------------------------------------------------------------------ world lens

    [Theory]
    [InlineData(1.0, WorldZoom.World)]
    [InlineData(7.0, WorldZoom.World)]       // span 0.558
    [InlineData(7.2, WorldZoom.Regional)]    // span 0.543
    [InlineData(24.0, WorldZoom.Regional)]   // span 0.163
    [InlineData(25.0, WorldZoom.Settlement)] // span 0.156
    public void ZoomLevel_IsAPureFunctionOfTheVisibleSpan(double zoom, WorldZoom expected)
    {
        // 1000 px viewport over a 256 px world: span = 1000 / zoom / 256.
        Assert.Equal(expected, WorldLens.LevelFor(zoom, 1600, 1000, 256));
        Assert.Equal(WorldLens.LevelFor(zoom, 1600, 1000, 256), WorldLens.LevelFor(zoom, 1600, 1000, 256));
    }

    [Fact]
    public void Layers_PerZoom_AreTheRuledInformationDensity()
    {
        IReadOnlyList<WorldLayer> world = WorldLens.LayersFor(WorldZoom.World);
        IReadOnlyList<WorldLayer> region = WorldLens.LayersFor(WorldZoom.Regional);
        IReadOnlyList<WorldLayer> town = WorldLens.LayersFor(WorldZoom.Settlement);
        Assert.Equal([WorldLayer.Territories, WorldLayer.MajorInfrastructure, WorldLayer.MajorSettlements, WorldLayer.MilitaryFormations, WorldLayer.AgeBanners], world);
        Assert.Contains(WorldLayer.Roads, region);
        Assert.Contains(WorldLayer.SettlementMorphology, region);
        Assert.Contains(WorldLayer.InstitutionalPresence, region);
        Assert.Contains(WorldLayer.MilitaryFormations, region);
        Assert.DoesNotContain(WorldLayer.InstitutionTypes, region);
        foreach (WorldLayer l in new[] { WorldLayer.InstitutionTypes, WorldLayer.InfrastructureDensity, WorldLayer.PopulationScale, WorldLayer.ProductionSignals })
            Assert.Contains(l, town);
        Assert.DoesNotContain(WorldLayer.InstitutionTypes, world);
        Assert.Same(WorldLens.LayersFor(WorldZoom.Regional), WorldLens.LayersFor(WorldZoom.Regional));
    }

    [Fact]
    public void Lens_DrawsOneTokenPerMilitaryUnitsRow_AtEveryZoom_InTableOrder()
    {
        WorldState w = fx.Session.World;
        WorldProjection p = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me);
        var ids = new List<int>();
        for (int i = 0; i < w.MilitaryUnits.Count; i++) ids.Add(w.MilitaryUnits[i].Id);
        Assert.NotEmpty(ids);
        foreach (WorldZoom z in new[] { WorldZoom.World, WorldZoom.Regional, WorldZoom.Settlement })
        {
            LensFrame f = WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, p, z, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024));
            Assert.Equal(ids, f.UnitTokens);
            Assert.Equal(z, f.Zoom);
        }
        // Each token carries the row's family and the content's identity name.
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
        {
            Assert.Equal(w.MilitaryUnits[i].Family, p.Units[i].FamilyKey);
            Assert.Equal(Families.IdentityByKey(w.MilitaryUnits[i].Identity)!.Name, p.Units[i].IdentityName);
        }
    }

    [Fact]
    public void Lens_Projection_IsRealState_AndDeterministic()
    {
        WorldState w = fx.Session.World;
        WorldProjection a = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me);
        WorldProjection b = WorldProjection.Build(w, fx.Session.Config, id => fx.Session.Names.Name(id), Me);
        Assert.Equal(w.Settlements.Count, a.Settlements.Count);
        Assert.Equal(w.NetworkEdges.Count, a.Roads.Count);
        Assert.Equal(w.CatchmentNodes.Count, a.Territory.Count);
        for (int i = 0; i < a.Settlements.Count; i++)
        {
            SettlementLensView s = a.Settlements[i];
            long pop = 0;
            for (int k = 0; k < w.Buckets.Count; k++) if (w.Buckets[k].Settlement.Value == s.Id) pop += w.Buckets[k].Count.Value;
            Assert.Equal(pop, s.Population);
            Assert.Equal(s with { Sectors = null, Structures = [], Institutions = [] }, b.Settlements[i] with { Sectors = null, Structures = [], Institutions = [] });
            Assert.Equal(AgeQuery.CurrentAge(w, Ages, Me), s.Age);
        }
        DrawList d1 = new(), d2 = new();
        WorldLens.Paint(d1, ApproxTextMeasure.Instance, a, WorldZoom.Settlement, (x, y) => (x * 9, y * 9), 9, new RectD(0, 0, 2400, 2400));
        WorldLens.Paint(d2, ApproxTextMeasure.Instance, b, WorldZoom.Settlement, (x, y) => (x * 9, y * 9), 9, new RectD(0, 0, 2400, 2400));
        Assert.Equal(SvgWriter.Write(d1, 2400, 2400, null), SvgWriter.Write(d2, 2400, 2400, null));
    }

    [Fact]
    public void AgeTransition_ChangesTheDrawnCivilizationState()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        AgePreview.PlayToEligible(s);
        WorldProjection before = WorldProjection.Build(s.World, s.Config, id => s.Names.Name(id), Me);
        Assert.True(s.EmitAdvanceAge(2, Ages.Surges[0].Key));
        s.EndTurn();
        WorldProjection after = WorldProjection.Build(s.World, s.Config, id => s.Names.Name(id), Me);
        Assert.Equal(1, before.PlayerAge);
        Assert.Equal(2, after.PlayerAge);
        foreach (SettlementLensView v in after.Settlements) if (v.IsPlayer) Assert.Equal(2, v.Age);
        static List<string> Banners(WorldProjection p)
        {
            var d = new DrawList();
            WorldLens.Paint(d, ApproxTextMeasure.Instance, p, WorldZoom.Regional, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024));
            var r = new List<string>();
            foreach (DrawCmd c in d.Commands) if (c is TextCmd t && (t.Text == "I" || t.Text == "II")) r.Add(t.Text);
            return r;
        }
        Assert.All(Banners(before), b => Assert.Equal("I", b));
        Assert.Contains("II", Banners(after));
        var screen = new AgeScreen(Me);
        screen.ShowTransition(2, Ages.Age(2).Name, Ages.Surges[0].Name, 0);
        Assert.True(screen.ToastVisible);
        var toast = new DrawList();
        screen.PaintToast(toast, ApproxTextMeasure.Instance, 1600, 60);
        Assert.Contains(Texts(toast), t => t.Text == "A NEW AGE BEGINS - AGE II");
    }
}
