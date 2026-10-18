using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Tests;

/// <summary>
/// D-054 WS2 AT THE UI — "MATERIALS FOR BUILDING AND ROADS": the Empire's building/roads split on the POLICY surface,
/// between the Building and Roads blocks, shown exactly when the Roads block is (its descriptor, ActionDomain
/// .ResourceAllocation, is listed with road development); the era's own share control between "Building" and "Roads" (b = 0
/// and b = 100 both reachable); "Set" only while the draft differs; the default and queued lines; what building work may
/// use this turn (the construction block reads ONLY the queued road order); the roads preview paid from what building work
/// leaves, with the default split's warning; the EMPIRE view's read-only line; the guarded emitter. Never a percent sign in
/// the numeral-free eras.
///
/// THE RIG: the dev-size founded world (seed 42, 256 px, four settlements, the player alone) with the trackway known and one
/// End Turn played (the pairwise distances exist from turn 1), so road development is listed; stocks set exactly through
/// the Ledger where a test needs figures.
/// </summary>
public class ResourceAllocationUiTests
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private const int Granary = 1;
    private static readonly SimConfig Cfg = UiFounding.ProductionConfig();
    private static readonly int Timber = Cfg.Goods!.IdOf("timber"), Stone = Cfg.Goods!.IdOf("stone");

    private static readonly Lazy<WorldState> RoadWorld = new(() =>
    {
        WorldState w = UiFounding.Found(42, 256, 4);
        ResearchContent research = Cfg.Research!;
        foreach (string id in ActionSurfacePreview.WithAncestors(research, "track_road"))
            w.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId(id)].Key));
        UiSession s = UiSession.StartFrom(w, 42, 256, 4);
        s.EndTurn();
        return s.World.Clone();
    });

    private static UiSession Session(Action<WorldState>? rig = null, int? age = null)
    {
        WorldState w = RoadWorld.Value.Clone();
        if (age is int a) w = EraPreview.WorldAt(w, Cfg.Ages!, Me, a);
        rig?.Invoke(w);
        return UiSession.StartFrom(w, 42, 256, 4);
    }

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

    private static SettlementId Capital(IReadOnlyWorldState w) =>
        EmpireQuery.TryGetCapital(w, Me, out SettlementId c) ? c : throw new InvalidOperationException("no capital");

    /// <summary>The materials rig: every settlement of the player at 0 timber and 0 stone but the capital (timber
    /// <paramref name="timber"/>, stone 1,000), every one building, and two committed granaries — the capital's and the
    /// next settlement's — at progress 0.</summary>
    private static Action<WorldState> Materials(long timber) => w =>
    {
        SettlementId capital = Capital(w);
        SettlementId[] mine = LabourActivities.ControlledSettlements(w, Me);
        foreach (SettlementId s in mine)
        {
            Set(w, s, Timber, 0);
            Set(w, s, Stone, 0);
            var row = new SectorAllocationRow(s, 0.0, 0.0, 0.0, 0.0, 1.0);
            for (int i = 0; i < w.SectorAllocations.Count; i++) if (w.SectorAllocations[i].Settlement == s) w.SectorAllocations[i] = row;
        }
        Set(w, capital, Timber, timber);
        Set(w, capital, Stone, 1000);
        SettlementId other = mine[0] == capital ? mine[1] : mine[0];
        foreach (SettlementId s in new[] { capital, other })
        {
            int slot = ConstructionQuery.NextSlot(w.ConstructionQueue, s);
            w.ConstructionQueue.Add(new ConstructionQueueRow(s, slot, Granary));
            w.ConstructionProgress.Add(new ConstructionProgressRow(s, slot, Granary, 0, 0));
        }
    };

    /// <summary>The trackway plan's whole timber cost at 100% (the road order the tests queue).</summary>
    private static long RoadTimber(IReadOnlyWorldState w)
    {
        long c = 0;
        foreach (RoadDevelopmentStep step in RoadDevelopmentQuery.Plan(w, Cfg.Research!, Cfg.Roads!, Cfg.Goods!, Me, 100.0))
            foreach (RoadMaterialCost m in step.Cost) if (m.Good.Value == Timber) c += m.Units;
        return c;
    }

    private static (ActionSurfaceScreen Screen, ActionSurfaceModel Model, List<TextCmd> Texts) Paint(UiSession s, ActionSurfaceScreen? screen = null)
    {
        EraTheme th = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me));
        ActionSurfaceModel model = ActionSurface.ForSession(s, UiSession.ProductionEra(), Capital(s.World).Value, th);
        screen ??= new ActionSurfaceScreen { Theme = th };
        screen.Refresh(model, s.World, s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 452);
        return (screen, model, d.Commands.OfType<TextCmd>().ToList());
    }

    private static string All(List<TextCmd> texts) => string.Join(" ", texts.Select(t => t.Text));

    /// <summary>The texts painted in the Materials block: from its heading down to the Roads heading.</summary>
    private static List<TextCmd> MaterialsTexts(List<TextCmd> texts)
    {
        double top = texts.First(t => t.Text.Contains("Materials for building and roads", StringComparison.OrdinalIgnoreCase)).Y;
        double bottom = texts.First(t => t.Text.StartsWith("Roads - ", StringComparison.OrdinalIgnoreCase) || t.Text.StartsWith("ROADS - ", StringComparison.Ordinal)).Y;
        Assert.True(bottom > top, "the Materials block is painted above the Roads block");
        return [.. texts.Where(t => t.Y >= top && t.Y < bottom)];
    }

    private static ActionCommand ClickOn(ActionSurfaceScreen screen, ActionHit h) => screen.Click(h.Rect.CenterX, h.Rect.CenterY);

    private static T[] Rows<T>(IReadOnlyTable<T> table) where T : unmanaged
    {
        var rows = new T[table.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = table[i];
        return rows;
    }

    // ================================================================== visibility

    [Fact]
    public void TheMaterialsBlock_IsShownExactlyWhenTheRoadsBlockIs_AndNeverWithTheSwitchOff()
    {
        // Turn one, no road class: neither block, no materials control, no EMPIRE line (the A1 pins unchanged).
        UiSession fresh = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        (ActionSurfaceScreen s0, ActionSurfaceModel m0, List<TextCmd> t0) = Paint(fresh);
        Assert.Null(m0.Roads);
        Assert.Null(m0.Allocation);
        Assert.DoesNotContain(ActionDomain.ResourceAllocation, m0.Domains);
        Assert.DoesNotContain(m0.Actions, a => a.Domain == ActionDomain.ResourceAllocation);
        Assert.DoesNotContain(s0.Hits, h => h.Kind is ActionHitKind.AllocSlot or ActionHitKind.AllocTrack or ActionHitKind.AllocApply);
        Assert.DoesNotContain("Materials for building and roads", All(t0), StringComparison.OrdinalIgnoreCase);
        PlayerView v0 = PlayerViews.Empire(fresh.World, fresh.Config, Me, fresh.Names.Name, 3);
        Assert.DoesNotContain(v0.Blocks.SelectMany(b => b.Lines), l => l.StartsWith("Materials when roads are ordered", StringComparison.Ordinal));

        // A road class known: both blocks, the Materials block between Building and Roads.
        UiSession s = Session();
        (ActionSurfaceScreen screen, ActionSurfaceModel m, List<TextCmd> texts) = Paint(s);
        Assert.NotNull(m.Roads);
        AllocationBlock block = Assert.IsType<AllocationBlock>(m.Allocation);
        Assert.Equal(OrderKind.SetResourceAllocation, block.Action.Order);
        Assert.Contains(ActionDomain.ResourceAllocation, m.Domains);
        Assert.Contains(screen.Hits, h => h.Kind is ActionHitKind.AllocSlot or ActionHitKind.AllocTrack);
        Assert.NotEmpty(MaterialsTexts(texts));

        // The same world with the split switched off: the Roads block, no Materials block.
        SimConfig off = Cfg with { Construction = Cfg.Construction! with { AllocationRatios = false } };
        EraTheme th = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me));
        ActionSurfaceModel offModel = ActionSurface.Build(new ActionSurfaceInput(s.World, s.PreviousWorld, off, UiSession.ProductionEra(), Me,
            Capital(s.World).Value, s.QueuedOrders(), id => s.Names.Name(id), th));
        Assert.NotNull(offModel.Roads);
        Assert.Null(offModel.Allocation);
        Assert.Equal(InfoStatus.Inert, InfoQuery.Card(s.World, off, Me, InfoSubject.MaterialsSplit).Status);
        InfoCard card = InfoQuery.Card(s.World, s.Config, Me, InfoSubject.MaterialsSplit);
        Assert.Equal(InfoStatus.Available, card.Status);
        Assert.Equal(block.Action, card.Action);
    }

    // ================================================================== the control

    [Fact]
    public void TheTally_ReachesZeroAndAHundred_AndSetEmitsKind12_OnlyWhileTheDraftDiffers()
    {
        UiSession s = Session();
        Assert.Equal(UiEra.Prehistoric, UiEras.Of(s.World, s.Config.Ages, Me));            // A1: ten tallies, no numerals
        (ActionSurfaceScreen screen, ActionSurfaceModel m, _) = Paint(s);
        Assert.Equal(100, screen.AllocationDraft);                                            // the default, building first
        Assert.DoesNotContain(screen.Hits, h => h.Kind == ActionHitKind.AllocApply);         // nothing to set yet
        ActionHit[] slots = [.. screen.Hits.Where(h => h.Kind == ActionHitKind.AllocSlot).OrderBy(h => h.B)];
        Assert.Equal(10, slots.Length);

        // Down to ZERO: the topmost lit tally toggles off (100 → 90), the first sets 10, the first again turns it off.
        Assert.Equal(ActionCommandKind.None, ClickOn(screen, slots[9]).Kind);
        Assert.Equal(90, screen.AllocationDraft);
        ClickOn(screen, slots[0]);
        Assert.Equal(10, screen.AllocationDraft);
        ClickOn(screen, slots[0]);
        Assert.Equal(0, screen.AllocationDraft);
        (screen, _, List<TextCmd> drafted) = Paint(s, screen);
        ActionCommand set = ClickOn(screen, Assert.Single(screen.Hits, h => h.Kind == ActionHitKind.AllocApply));
        Assert.Equal((ActionCommandKind.SetAllocation, 0.0), (set.Kind, set.Percent));
        Assert.True(set.IsOrder);
        Assert.True(ActionDispatch.Apply(s, set));
        Assert.Equal(ResourceAllocation.Order(s.World, Me, 0), s.Orders[^1]);
        Assert.Contains("building work keeps 0 shares in 10, road works 10", All(drafted), StringComparison.Ordinal);

        // Up to A HUNDRED: the last tally. The split in force is now 0 (the queued Set), so Set shows again.
        (screen, m, _) = Paint(s, screen);
        Assert.Equal(0, m.Allocation!.BuildingsPercent);
        Assert.DoesNotContain(screen.Hits, h => h.Kind == ActionHitKind.AllocApply);         // the draft equals it
        ClickOn(screen, screen.Hits.Single(h => h.Kind == ActionHitKind.AllocSlot && h.B == 9));
        Assert.Equal(100, screen.AllocationDraft);
        (screen, _, _) = Paint(s, screen);
        ActionCommand up = ClickOn(screen, Assert.Single(screen.Hits, h => h.Kind == ActionHitKind.AllocApply));
        Assert.True(ActionDispatch.Apply(s, up));
        Assert.Equal(ResourceAllocation.Order(s.World, Me, 100), s.Orders[^1]);
        s.EndTurn();
        Assert.Equal(new ResourceAllocationRow(Me, 100), Assert.Single(Rows(s.World.ResourceAllocations)));   // the last Set won
    }

    [Fact]
    public void TheSlider_ReachesZeroAndAHundred_InTheNumeralEras()
    {
        UiSession s = Session(age: 7);
        (ActionSurfaceScreen screen, ActionSurfaceModel m, List<TextCmd> texts) = Paint(s);
        Assert.True(m.Control.IsSlider);
        Assert.Contains("Building 100% · Roads 0%", All(texts), StringComparison.Ordinal);
        ActionHit track = Assert.Single(screen.Hits, h => h.Kind == ActionHitKind.AllocTrack);
        screen.Click(track.Rect.X, track.Rect.CenterY);
        Assert.Equal(0, screen.AllocationDraft);
        screen.Release();
        screen.Click(track.Rect.Right - 0.01, track.Rect.CenterY);
        Assert.Equal(100, screen.AllocationDraft);
        screen.Drag(track.Rect.X + track.Rect.W * 0.3);
        Assert.Equal(30, screen.AllocationDraft);
        screen.Release();
        (screen, _, texts) = Paint(s, screen);
        Assert.Contains("Building 30% · Roads 70%", All(texts), StringComparison.Ordinal);
        Assert.True(ActionDispatch.Apply(s, ClickOn(screen, Assert.Single(screen.Hits, h => h.Kind == ActionHitKind.AllocApply))));
        Assert.Equal(ResourceAllocation.Order(s.World, Me, 30), s.Orders[^1]);
        (_, _, texts) = Paint(s, screen);
        Assert.Contains("Set this turn: building 30%, roads 70% - applies at End Turn, to this turn's work", All(texts), StringComparison.Ordinal);
    }

    // ================================================================== the words

    [Fact]
    public void TheDefault_TheQueuedLine_AndNoPercentInTheNumeralFreeEras()
    {
        UiSession s = Session();
        (_, ActionSurfaceModel m, List<TextCmd> texts) = Paint(s);
        Assert.True(m.Allocation!.IsDefault);
        string block = string.Join(" ", MaterialsTexts(texts).Select(t => t.Text));
        Assert.Contains("Building first (default): road works get only what building work leaves.", block, StringComparison.Ordinal);
        Assert.Contains("Whatever one side does not need, the other may use.", block, StringComparison.Ordinal);
        Assert.Contains("On turns without road works, building work may use all of it.", block, StringComparison.Ordinal);
        Assert.Contains("Dwellings take their own settlement's timber before either.", block, StringComparison.Ordinal);
        Assert.Contains(AllocationText.Goods(s.Config), block, StringComparison.Ordinal);   // the goods, from content

        Assert.True(s.EmitAllocationOrder(50));
        (_, m, texts) = Paint(s);
        List<TextCmd> materials = MaterialsTexts(texts);
        string set = string.Join(" ", materials.Select(t => t.Text));
        Assert.Equal((50, (int?)50, 100), (m.Allocation!.BuildingsPercent, m.Allocation.QueuedPercent, m.Allocation.CommittedPercent));
        Assert.DoesNotContain("Building first (default)", set, StringComparison.Ordinal);
        Assert.Contains("building work is guaranteed 5 shares in 10 of the empire's", set, StringComparison.Ordinal);
        Assert.Contains("building work keeps 5 shares in 10, road works 5", set, StringComparison.Ordinal);
        Assert.Contains("Set this turn: building 5, roads 5 - applies at End Turn, to this turn's work", set, StringComparison.Ordinal);
        Assert.DoesNotContain(materials, t => t.Text.Contains('%', StringComparison.Ordinal));      // A1: no percent sign
    }

    [Fact]
    public void TheConstructionBlock_SaysWhatBuildingWorkMayUse_FromTheQueuedRoadOrderOnly()
    {
        UiSession probe = Session(Materials(100));
        long road = RoadTimber(probe.World);
        Assert.True(road > 0, "anti-vacuity: the road order costs timber");

        UiSession s = Session(Materials(road + 40));
        Assert.Equal("With no road works ordered this turn, building work may use everything it needs.", Paint(s).Model.Construction!.MaterialsLine);
        Assert.True(s.EmitRoadOrder(100.0));
        Assert.Equal("Building first (default): this turn building work may use everything it needs, and road works get what it leaves.",
            Paint(s).Model.Construction!.MaterialsLine);
        Assert.True(s.EmitAllocationOrder(0));
        (_, ActionSurfaceModel m, List<TextCmd> texts) = Paint(s);
        // b = 0: building work may take only what the road does not need — the 40 over the road's cost.
        double dt = ActionQueryContext.ForNextStep(s.World, UiSession.ProductionEra()).NextDtYears!.Value;
        AllocationView view = ResourceAllocationQuery.ForEmpire(s.World, s.Config, Me, s.QueuedOrders(), dt)!;
        Assert.Equal(40L, view.Split.SplitOf(Timber)!.Value.BuildingCap);
        Assert.StartsWith("This turn building work may use up to: ", m.Construction!.MaterialsLine, StringComparison.Ordinal);
        Assert.Contains(" 40 timber", m.Construction.MaterialsLine, StringComparison.Ordinal);
        Assert.EndsWith("- the rest is held for road works under the split (an estimate on current stocks).", m.Construction.MaterialsLine, StringComparison.Ordinal);
        Assert.Contains(m.Construction.MaterialsLine!, All(texts), StringComparison.Ordinal);
    }

    [Fact]
    public void AHeadTheSplitHoldsBack_SaysRoadWorksHoldTheRest()
    {
        // The capital holds exactly the road's timber: at b = 0 with the road queued, building work's grant is 0, so the
        // first head waits — and its status names the split, not "the empire has none".
        UiSession probe = Session(Materials(100));
        long road = RoadTimber(probe.World);
        UiSession s = Session(Materials(road));
        Assert.True(s.EmitRoadOrder(100.0));
        Assert.True(s.EmitAllocationOrder(0));
        (_, ActionSurfaceModel m, List<TextCmd> texts) = Paint(s);
        QueueEntry head = m.Construction!.Queue.First(q => q.Head);
        Assert.Equal("waiting for timber: road works hold the rest of this turn's timber under the building/roads split", head.Status);
        Assert.Contains(head.Status!, All(texts), StringComparison.Ordinal);
        // The default split puts building first: the same head works.
        UiSession d = Session(Materials(road));
        Assert.True(d.EmitRoadOrder(100.0));
        Assert.DoesNotContain("road works hold", Paint(d).Model.Construction!.Queue.First(q => q.Head).Status ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void TheRoadsPreview_IsPaidFromWhatBuildingWorkLeaves_AndWarnsUnderTheDefault()
    {
        UiSession probe = Session(Materials(100));
        long road = RoadTimber(probe.World);
        UiSession s = Session(Materials(road + 40));
        (_, ActionSurfaceModel m, _) = Paint(s);
        double dt = ActionQueryContext.ForNextStep(s.World, UiSession.ProductionEra()).NextDtYears!.Value;
        Assert.Equal(80L, ResourceAllocationQuery.ForEmpire(s.World, s.Config, Me, s.QueuedOrders(), dt, 100.0)!.Split.SplitOf(Timber)!.Value.BuildingDemand);
        // Default (building first): building work takes its 80 first, so 40 short of the road is left.
        RoadPlanPreview p = ActionSurface.RoadPlan(s.World, s.Config, Me, 100.0, id => s.Names.Name(id), m.Allocation, numerals: false);
        Assert.Equal((double)(road - 40) / road, p.AfterBuilding!.Value, 12);
        Assert.StartsWith("after building work, about " + (road - 40).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " of the " + road.ToString(System.Globalization.CultureInfo.InvariantCulture) + " timber needed is left: about ", p.LeftLine, StringComparison.Ordinal);
        Assert.EndsWith(" of it would be paid this turn", p.LeftLine, StringComparison.Ordinal);
        Assert.DoesNotContain("%", p.LeftLine, StringComparison.Ordinal);                            // words, not numerals, at A1
        Assert.Equal("Building work may take up to 80 timber first under the current split - change it under Materials", p.Warning);
        RoadPlanPreview numerals = ActionSurface.RoadPlan(s.World, s.Config, Me, 100.0, id => s.Names.Name(id), m.Allocation, numerals: true);
        Assert.Contains("% of it would be paid this turn", numerals.LeftLine, StringComparison.Ordinal);

        // b = 0: building work gets only the 40 the road leaves, so the road is paid in full and nothing is warned.
        Assert.True(s.EmitAllocationOrder(0));
        (_, m, _) = Paint(s);
        RoadPlanPreview zero = ActionSurface.RoadPlan(s.World, s.Config, Me, 100.0, id => s.Names.Name(id), m.Allocation, numerals: false);
        Assert.Equal(1.0, zero.AfterBuilding);
        Assert.Equal("after building work, everything this order needs is left: all of it would be paid this turn", zero.LeftLine);
        Assert.Null(zero.Warning);

        // The painted Roads block carries the preview, the new caveat, and no "(affordable)".
        (_, _, List<TextCmd> texts) = Paint(s);
        string all = All(texts);
        Assert.Contains("after building work,", all, StringComparison.Ordinal);
        Assert.Contains("buildings draw before roads, within their share", all, StringComparison.Ordinal);
        Assert.DoesNotContain("(affordable)", all, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEmpireView_ShowsTheSplitReadOnly()
    {
        UiSession s = Session();
        static IEnumerable<string> Lines(UiSession x) =>
            PlayerViews.Empire(x.World, x.Config, Me, x.Names.Name, 3).Blocks.SelectMany(b => b.Lines);
        Assert.Contains("Materials when roads are ordered: building first (default) - road works get only what building work leaves", Lines(s));
        Assert.True(s.EmitAllocationOrder(70));
        Assert.Contains("Materials when roads are ordered: building first (default) - road works get only what building work leaves", Lines(s));   // queued, not in force
        s.EndTurn();
        Assert.Contains("Materials when roads are ordered: building work keeps 7 shares in 10, road works 3 (default: building first)", Lines(s));
        Assert.DoesNotContain(Lines(s), l => l.StartsWith("Materials when", StringComparison.Ordinal) && l.Contains('%', StringComparison.Ordinal));
    }

    // ================================================================== c2 verifier findings (F4, F5, F6) and UI pins

    private static readonly int Tools = Cfg.Goods!.IdOf("tools");

    /// <summary>A test content whose trackway also needs <paramref name="toolsPerKm"/> tools per km.</summary>
    private static SimConfig TrackwayWithTools(double toolsPerKm)
    {
        RoadClassConfig[] classes = [.. Cfg.Roads!.Classes];
        for (int i = 0; i < classes.Length; i++)
            if (classes[i].EdgeType == EdgeTypes.Trackway)
                classes[i] = classes[i] with { MaterialsPerKm = [new RoadMaterialConfig("timber", 1.0), new RoadMaterialConfig("tools", toolsPerKm)] };
        return Cfg with { Roads = Cfg.Roads with { Classes = classes } };
    }

    private static void NoToolsBut(WorldState w, SettlementId at, long tools)
    {
        foreach (SettlementId s in LabourActivities.ControlledSettlements(w, Me)) Set(w, s, Tools, 0);
        Set(w, at, Tools, tools);
    }

    [Fact]
    public void TheRoadsPreview_SaysWhatRoadDevelopmentWouldCharge_NotTheAffordableShareOfTheSum()
    {
        // F4. A test content whose trackway needs 0.001 tools per km — one tool per route. The capital keeps tools for every
        // route but, after building work's 80 timber (the default: building first), one timber short of the FIRST route.
        // ADR-032 then pays nothing: the first step's tool charge floors to 0 and the order ends there. The old preview read
        // the affordable share of the summed cost (more than nothing); the preview must say what road development charges.
        SimConfig cfg = TrackwayWithTools(0.001);
        RoadDevelopmentStep[] plan = RoadDevelopmentQuery.Plan(Session(Materials(100)).World, cfg.Research!, cfg.Roads!, cfg.Goods!, Me, 100.0);
        Assert.True(plan.Length >= 2, "anti-vacuity: a plan of several routes");
        long first = 0, totalTimber = 0, totalTools = 0;
        foreach (RoadMaterialCost c in plan[0].Cost) if (c.Good.Value == Timber) first = c.Units;
        foreach (RoadDevelopmentStep step in plan)
            foreach (RoadMaterialCost c in step.Cost)
            {
                if (c.Good.Value == Timber) totalTimber += c.Units;
                if (c.Good.Value == Tools) totalTools += c.Units;
            }
        Assert.True(first >= 2 && totalTools >= plan.Length, "anti-vacuity: the first route needs timber, every route a tool");

        UiSession s = Session(w => { Materials(80 + first - 1)(w); NoToolsBut(w, Capital(w), totalTools); });
        AllocationBlock block = Paint(s).Model.Allocation!;
        Assert.True(block.IsDefault);
        long leftTimber = first - 1;
        Assert.True(RoadDevelopmentQuery.AffordableFraction(
            [new RoadMaterialCost(new GoodId(Timber), totalTimber), new RoadMaterialCost(new GoodId(Tools), totalTools)],
            [leftTimber, totalTools]) > 0.0, "the summed cost's affordable share is not nothing");
        RoadPlanPreview p = ActionSurface.RoadPlan(s.World, cfg, Me, 100.0, id => s.Names.Name(id), block, numerals: false);
        Assert.Equal(0.0, p.AfterBuilding);
        Assert.Equal("after building work, about " + Inv(leftTimber) + " of the " + Inv(totalTimber)
            + " timber needed is left: none of it would be paid this turn", p.LeftLine);
        RoadPlanPreview numerals = ActionSurface.RoadPlan(s.World, cfg, Me, 100.0, id => s.Names.Name(id), block, numerals: true);
        Assert.EndsWith(": none of it would be paid this turn", numerals.LeftLine, StringComparison.Ordinal);

        // The figure is road development's own charge on what building work leaves (and that charge is the system's —
        // Sim.Tests ResourceAllocationTests pins SimulateCharges to the road system's real draws).
        var remainder = new long[Math.Max(Timber, Tools) + 1];
        remainder[Timber] = leftTimber;
        remainder[Tools] = totalTools;
        long[] charged = RoadDevelopmentQuery.SimulateCharges(s.World, cfg, Me, 100.0, remainder);
        Assert.Equal((0L, 0L), (charged[Timber], charged[Tools]));

        // One more timber pays the first route in full and stops at the second: the share is that route's.
        UiSession t = Session(w => { Materials(80 + first)(w); NoToolsBut(w, Capital(w), totalTools); });
        RoadPlanPreview one = ActionSurface.RoadPlan(t.World, cfg, Me, 100.0, id => t.Names.Name(id), Paint(t).Model.Allocation!, numerals: true);
        long toolsFirst = 0;
        foreach (RoadMaterialCost c in plan[0].Cost) if (c.Good.Value == Tools) toolsFirst = c.Units;
        double expected = Math.Min((double)first / totalTimber, (double)toolsFirst / totalTools);
        Assert.Equal(expected, one.AfterBuilding!.Value, 12);
        // Batch 3 R3 (2026-10-15, C2 review F9): the share is FLOORED and held to 1–99 % (AllocationText.SharePercent, from
        // the least-paid good's units), where it was rounded — the rule changed, so the expected figure is computed by it.
        (long part, long whole) = (double)first / totalTimber <= (double)toolsFirst / totalTools ? (first, totalTimber) : (toolsFirst, totalTools);
        Assert.Contains("about " + Inv(Math.Clamp(part * 100 / whole, 1L, 99L)) + "% of it would be paid this turn",
            one.LeftLine, StringComparison.Ordinal);
    }

    [Fact]
    public void TheConstructionLine_SaysTheRoadCouldPayNothing_WhenTheGuardWithholdsNothing()
    {
        // The split's starvation guard (pinned in Sim.Tests ResourceAllocationTests: a road order road development would
        // charge nothing on what building work leaves it withholds nothing) as the construction block words it: building
        // work may use everything, and the line says why — not that the road needs nothing building work would use.
        OrderRecord road = OrderRecord.From(0, Me, OrderKind.DevelopRoads, 0, 100.0);
        int[] goods = [Timber];
        var none = new long[Math.Max(Timber, Stone) + 1];
        var drawn = (long[])none.Clone();
        drawn[Timber] = 80;
        EmpireAllocation Split(bool guarded) => new(Me, 75, road, goods,
            [ResourceAllocation.Split(100, 80, guarded ? 0 : 60, 75)], null, drawn, none, guarded);
        AllocationView View(bool guarded) => new(Me, 75, 75, true, null, road, false, Split(guarded));
        Assert.Equal("The road works ordered this turn could pay nothing from their share: building work may use everything it needs.",
            AllocationText.ConstructionLine(View(guarded: true), Cfg));
        Assert.StartsWith("This turn building work may use up to: 75 timber", AllocationText.ConstructionLine(View(guarded: false), Cfg), StringComparison.Ordinal);
    }

    [Fact]
    public void TheConstructionLine_ShowsTheCap_NotTheGrant_WhenBuildingNeedsLessThanItMayUse()
    {
        // F6. One granary (40 timber, 20 stone) and the capital holding the road's timber R plus 40, at b = 50 with the road
        // queued: E_b = floor((R + 40) / 2) > 40 (R > 40), E_r − D_r = 40 − E_b < 0, so the cap is E_b and the grant D_b =
        // 40. The line says what building work MAY use — the cap — not what it will take.
        long road = RoadTimber(Session(Materials(100)).World);
        Assert.True(road > 41, "anti-vacuity: the cap exceeds the granary's need");
        UiSession s = Session(w =>
        {
            Materials(road + 40)(w);
            SettlementId capital = Capital(w);
            var kept = new List<ConstructionQueueRow>();
            for (int i = 0; i < w.ConstructionQueue.Count; i++) if (w.ConstructionQueue[i].Settlement == capital) kept.Add(w.ConstructionQueue[i]);
            var keptProgress = new List<ConstructionProgressRow>();
            for (int i = 0; i < w.ConstructionProgress.Count; i++) if (w.ConstructionProgress[i].Settlement == capital) keptProgress.Add(w.ConstructionProgress[i]);
            w.ConstructionQueue.Clear();
            w.ConstructionProgress.Clear();
            foreach (ConstructionQueueRow r in kept) w.ConstructionQueue.Add(r);
            foreach (ConstructionProgressRow r in keptProgress) w.ConstructionProgress.Add(r);
        });
        Assert.True(s.EmitRoadOrder(100.0));
        Assert.True(s.EmitAllocationOrder(50));
        long eb = (road + 40) * 50 / 100;
        double dt = ActionQueryContext.ForNextStep(s.World, UiSession.ProductionEra()).NextDtYears!.Value;
        AllocationSplit timber = ResourceAllocationQuery.ForEmpire(s.World, s.Config, Me, s.QueuedOrders(), dt)!.Split.SplitOf(Timber)!.Value;
        Assert.Equal((40L, eb, 40L), (timber.BuildingDemand, timber.BuildingCap, timber.BuildingGrant));
        string line = Paint(s).Model.Construction!.MaterialsLine!;
        Assert.StartsWith("This turn building work may use up to: ", line, StringComparison.Ordinal);
        Assert.Contains(" " + Inv(eb) + " timber", line, StringComparison.Ordinal);
        Assert.Contains(" 1000 stone", line, StringComparison.Ordinal);
        Assert.DoesNotContain(" 40 timber", line, StringComparison.Ordinal);                     // the grant is not the line
        Assert.DoesNotContain(" 20 stone", line, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultWarning_IsGivenOnlyUnderTheDefaultSplit()
    {
        // At b = 50 building work still takes timber the road needs first (its grant min(80, E_b) > 40 while R > 41) — but
        // the player chose that split, so the "change it under Materials" warning is the default's alone.
        long road = RoadTimber(Session(Materials(100)).World);
        Assert.True(road > 41, "anti-vacuity: at b = 50 building work's grant exceeds the 40 the road can spare");
        UiSession s = Session(Materials(road + 40));
        Assert.NotNull(ActionSurface.RoadPlan(s.World, s.Config, Me, 100.0, id => s.Names.Name(id), Paint(s).Model.Allocation, numerals: false).Warning);
        Assert.True(s.EmitAllocationOrder(50));
        AllocationBlock block = Paint(s).Model.Allocation!;
        RoadPlanPreview p = ActionSurface.RoadPlan(s.World, s.Config, Me, 100.0, id => s.Names.Name(id), block, numerals: false);
        Assert.True(p.AfterBuilding < 1.0, "building work does take timber the road needs at b = 50");
        Assert.Null(p.Warning);
    }

    [Fact]
    public void TheShareWords_AreTheNearestFifth_NeverAllOrNoneUnlessExact()
    {
        Assert.Equal(
            ["none of it", "about a fifth of it", "about a fifth of it", "about two-fifths of it", "about three-fifths of it",
             "about four-fifths of it", "about four-fifths of it", "all of it"],
            new[] { 0.0, 0.01, 0.1, 0.4, 0.6, 0.8, 0.99, 1.0 }.Select(AllocationText.ShareWords).ToArray());
    }

    [Fact]
    public void TheDraft_ResetsOnTurnChange_ToTheSplitInForce()
    {
        // A draft the player did not Set is this turn's only: the next turn's control starts from the split in force.
        UiSession s = Session();
        (ActionSurfaceScreen screen, _, _) = Paint(s);
        ClickOn(screen, screen.Hits.Single(h => h.Kind == ActionHitKind.AllocSlot && h.B == 9));   // 100 → 90, not Set
        Assert.Equal(90, screen.AllocationDraft);
        (screen, _, _) = Paint(s, screen);
        Assert.Equal(90, screen.AllocationDraft);                                                 // kept within the turn
        s.EndTurn();
        (screen, _, _) = Paint(s, screen);
        Assert.Equal(100, screen.AllocationDraft);                                                // reset to the split in force
        Assert.DoesNotContain(screen.Hits, h => h.Kind == ActionHitKind.AllocApply);
    }

    [Fact]
    public void TheQueuedLine_ShowsTheLastSetOfTheTurn()
    {
        UiSession s = Session();
        Assert.True(s.EmitAllocationOrder(40));
        Assert.True(s.EmitAllocationOrder(60));
        (_, ActionSurfaceModel m, List<TextCmd> texts) = Paint(s);
        Assert.Equal((60, (int?)60), (m.Allocation!.BuildingsPercent, m.Allocation.QueuedPercent));
        string block = string.Join(" ", MaterialsTexts(texts).Select(t => t.Text));
        Assert.Contains("Set this turn: building 6, roads 4 - applies at End Turn, to this turn's work", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Set this turn: building 4", block, StringComparison.Ordinal);
    }

    [Fact]
    public void EmitAllocationOrder_AppendsNothing_WhereTheDeliveryPredicateRefuses()
    {
        // F5. The emitter's own checks (the share's range, a no-op) cannot see what ConstructionQuery.AllocationOrderValid
        // sees; a world in which the player's Empire is not on the roster is one the predicate refuses (ConstructionSystem
        // would ignore the order at delivery), so the emitter must refuse it too, appending nothing.
        UiSession s = Session(w =>
        {
            var kept = new List<PolityRow>();
            for (int i = 0; i < w.Polities.Count; i++) if (w.Polities[i].Id != Me) kept.Add(w.Polities[i]);
            w.Polities.Clear();
            foreach (PolityRow r in kept) w.Polities.Add(r);
        });
        OrderRecord would = ResourceAllocation.Order(s.World, Me, 30);
        Assert.False(ConstructionQuery.AllocationOrderValid(s.World, s.Config, would));
        Assert.False(s.EmitAllocationOrder(30));
        Assert.Equal(0, s.Orders.Count);
    }

    private static string Inv(long v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // ================================================================== batch-3 integration R3 (UI residuals, 2026-10-15)

    /// <summary>The texts on the Materials block's control row: the "Building" and "Roads" labels' line, from the top of
    /// the labels to the readout below them.</summary>
    private static List<TextCmd> ControlRow(List<TextCmd> materials)
    {
        TextCmd building = materials.Single(t => t.Text == "Building" && t.Role == FontRole.Heading);
        TextCmd roads = materials.Single(t => t.Text == "Roads" && t.Role == FontRole.Heading);
        Assert.Equal(building.Y, roads.Y, 9);                                                   // one row: Building ... Roads
        Assert.True(roads.X > building.X, "the Roads label stands east of the Building label");
        TextCmd readout = materials.First(t => t.Y > building.Y + 1 && t.Text.StartsWith("Building ", StringComparison.Ordinal));
        return [.. materials.Where(t => t.Y >= building.Y - 2 && t.Y < readout.Y)];
    }

    /// <summary>
    /// R3 / C2 review F1 — in the numeral eras the split control painted the BUILDING share as an inline numeral at its
    /// right end, 8 px from the "Roads" label (MEASURED at age 4: "35%" right-aligned at x 305.3, "Roads" at 313.3, one row:
    /// "Building ▮▮▮▮▮▮▮ 35% Roads" while roads get 65%). The control row now carries no numeral at all (the Body readout
    /// below it says both shares); the road and tax share controls keep their inline numeral.
    /// </summary>
    [Theory]
    [InlineData(4)]   // notches with numerals
    [InlineData(7)]   // the slider
    public void InTheNumeralEras_NoNumeralOnTheSplitRow_ReadsTheBuildingShareBesideRoads(int age)
    {
        UiSession s = Session(age: age);
        Assert.True(s.EmitAllocationOrder(35));
        (ActionSurfaceScreen screen, ActionSurfaceModel m, List<TextCmd> texts) = Paint(s);
        Assert.True(m.Control.Numerals, "anti-vacuity: a numeral era");
        Assert.Equal(age >= 7, m.Control.IsSlider);
        Assert.Equal(35, screen.AllocationDraft);
        List<TextCmd> materials = MaterialsTexts(texts);
        List<TextCmd> row = ControlRow(materials);
        Assert.DoesNotContain(row, t => t.Text.Contains('%', StringComparison.Ordinal) || t.Text.Any(char.IsDigit));
        // The shares are read below the control, each beside its own name.
        Assert.Contains(materials, t => t.Text == "Building 35% · Roads 65%");

        // The other callers of the share control keep their inline numeral: the road order's "Modernize" row (its draft).
        TextCmd modernize = texts.Single(t => t.Text == "Modernize");
        Assert.Contains(texts, t => Math.Abs(t.Y - modernize.Y) < 4 && t.Align == TextAlign.Right
            && t.Text == screen.RoadDraft.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%");
    }

    /// <summary>
    /// R3 / C2 review F9 — the numeral eras' roads preview ROUNDED the share a partial payment pays: a road short by one
    /// timber of its whole cost (1534 of 1535) read "about 100% of it", contradicting the "all of it" wording. The share is
    /// floored and held to 1–99 % while the payment is partial (WS1's rule; ShareWords' 1–4 fifths in the numeral-free
    /// eras), from the paid and needed units themselves (no double rounding).
    /// </summary>
    [Fact]
    public void TheNumeralRoadsPreview_FloorsAPartialShare_AndNeverReadsAHundredOrNone()
    {
        Assert.Equal(
            ["none of it", "none of it", "about 1% of it", "about 1% of it", "about 12% of it", "about 29% of it", "about 99% of it",
             "about 99% of it", "all of it", "all of it"],
            new (long, long)[] { (0, 1000), (-3, 1000), (1, 1000), (19, 1000), (129, 1000), (29, 100), (999, 1000), (9999, 10000),
                (1000, 1000), (1500, 1000) }.Select(x => AllocationText.SharePercent(x.Item1, x.Item2)).ToArray());

        // On the road world: building work's 80 timber first (the default split) leaves the road one timber short.
        long road = RoadTimber(Session(Materials(100)).World);
        Assert.True(road > 200, "anti-vacuity: a road order of several hundred timber");
        UiSession s = Session(Materials(road + 79));
        AllocationBlock block = Paint(s).Model.Allocation!;
        RoadPlanPreview p = ActionSurface.RoadPlan(s.World, s.Config, Me, 100.0, id => s.Names.Name(id), block, numerals: true);
        Assert.InRange(p.AfterBuilding!.Value, 0.995, 1.0 - 1e-12);                             // a partial payment, ≥ 99.5 %
        Assert.EndsWith(": about 99% of it would be paid this turn", p.LeftLine, StringComparison.Ordinal);
        Assert.DoesNotContain("100%", p.LeftLine, StringComparison.Ordinal);
    }

    /// <summary>
    /// R3 / C2 review F10 — the EMPIRE view's "Materials when roads are ordered" line is shown exactly when the Materials
    /// block is: when AvailableActionsQuery lists the ResourceAllocation descriptor (road development is listed and the
    /// split's delivery predicate admits the Empire's order). Before, it was shown whenever a road class above the dirt path
    /// was known — also for an Empire that knows the trackway but has no route to develop, which then read the line with no
    /// control anywhere to change it.
    /// </summary>
    [Fact]
    public void TheEmpireLine_IsShownExactlyWhenTheMaterialsBlockIs()
    {
        static bool Line(UiSession x, SimConfig? cfg = null) =>
            PlayerViews.Empire(x.World, cfg ?? x.Config, Me, x.Names.Name, 3).Blocks.SelectMany(b => b.Lines)
                .Any(l => l.StartsWith("Materials when roads are ordered", StringComparison.Ordinal));
        static bool Listed(UiSession x, SimConfig? cfg = null) =>
            AvailableActionsQuery.For(x.World, cfg ?? x.Config, Me).Any(a => a.Domain == ActionDomain.ResourceAllocation);

        // The trackway known, but no developable route: a lone-settlement Empire (the founding's only settlement).
        WorldState lone = UiFounding.Found(42, 256, 1);
        ResearchContent research = Cfg.Research!;
        foreach (string id in ActionSurfacePreview.WithAncestors(research, "track_road"))
            lone.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId(id)].Key));
        UiSession single = UiSession.StartFrom(lone, 42, 256, 1);
        single.EndTurn();
        Assert.True(RoadDevelopmentQuery.BestKnownClass(single.World, research, Cfg.Roads!, Me) > EdgeTypes.DirtPath,
            "anti-vacuity: a road class above the dirt path is known");
        Assert.Empty(RoadDevelopmentQuery.Plan(single.World, research, Cfg.Roads!, Cfg.Goods!, Me, 100.0));
        Assert.Null(Paint(single).Model.Allocation);
        Assert.False(Listed(single));
        Assert.False(Line(single));

        // Both shown: the road world.
        UiSession roads = Session();
        Assert.NotNull(Paint(roads).Model.Allocation);
        Assert.True(Listed(roads));
        Assert.True(Line(roads));

        // Neither: turn one (no road class) and the road world with the split switched off.
        UiSession fresh = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        Assert.Null(Paint(fresh).Model.Allocation);
        Assert.False(Line(fresh));
        SimConfig off = Cfg with { Construction = Cfg.Construction! with { AllocationRatios = false } };
        Assert.False(Listed(roads, off));
        Assert.False(Line(roads, off));
    }

    /// <summary>The fills of the frame commands (rects and polygons) wholly inside <paramref name="r"/>.</summary>
    private static List<Rgba> FillsIn(DrawList d, RectD r)
    {
        RectD box = r.Inset(-1);
        var fills = new List<Rgba>();
        foreach (DrawCmd c in d.Commands)
        {
            if (c is RectCmd { Fill: { } f } rc && box.Contains(rc.Rect.X, rc.Rect.Y) && box.Contains(rc.Rect.Right, rc.Rect.Bottom)) fills.Add(f);
            if (c is PolygonCmd pc && pc.Points.All(q => box.Contains(q.X, q.Y))) fills.Add(pc.Fill);
        }
        return fills;
    }

    /// <summary>
    /// R3 / C2 review F6 + UX-MU20 — THE MATERIALS BLOCK'S READABILITY ROLES (era-ui §9a, UR-7; the WS1 UI rule): "Set" is the
    /// block's verb, drawn Primary (the accent-tinted plate, its label in the body role's heading weight) — never the
    /// Secondary plate; the readout is at the Body role in the body ink; the explanation is an explanation, at the Caption role
    /// in the soft ink.
    /// </summary>
    [Fact]
    public void TheMaterialsBlock_SetIsPrimary_TheReadoutIsBody_TheExplanationIsCaption()
    {
        UiSession s = Session();
        (ActionSurfaceScreen screen, _, _) = Paint(s);
        ClickOn(screen, screen.Hits.Single(h => h.Kind == ActionHitKind.AllocSlot && h.B == 9));   // a draft: Set is drawn
        EraTheme th = screen.Theme;
        ActionSurfaceModel model = ActionSurface.ForSession(s, UiSession.ProductionEra(), Capital(s.World).Value, th);
        screen.Refresh(model, s.World, s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 452);
        List<TextCmd> texts = d.Commands.OfType<TextCmd>().ToList();
        double body = th.Type.Size(TypeScale.Px(th, TypeRole.Body)), caption = th.Type.Size(TypeScale.Px(th, TypeRole.Caption));
        Assert.True(caption < body, "anti-vacuity: the Caption role is smaller than the Body role");

        ActionHit set = Assert.Single(screen.Hits, h => h.Kind == ActionHitKind.AllocApply);
        TextCmd label = Assert.Single(texts, t => t.Text == "Set" && set.Rect.Contains(t.X, t.Y + 2));
        Assert.Equal((FontRole.Heading, body, th.Ink.Text), (label.Role, label.Size, label.Color));
        List<Rgba> fills = FillsIn(d, set.Rect);
        Assert.Contains(ThemeColor.Mix(th.Material.PanelRaised, th.Material.Accent, 0.28), fills);   // the Primary plate
        Assert.DoesNotContain(th.Material.PanelRaised, fills);                                       // not the Secondary one

        List<TextCmd> materials = MaterialsTexts(texts);
        TextCmd readout = Assert.Single(materials, t => t.Text == AllocationText.Readout(90, numerals: false));
        Assert.Equal((body, th.Ink.Text), (readout.Size, readout.Color));
        string explanation = AllocationText.Explanation(100, AllocationText.Goods(s.Config));
        TextCmd first = materials.First(t => explanation.StartsWith(t.Text, StringComparison.Ordinal) && t.Text.Length > 20);
        Assert.Equal((caption, th.Ink.TextSoft), (first.Size, first.Color));
    }

    // ================================================================== the emitter

    [Fact]
    public void EmitAllocationOrder_IsGuardedByTheDeliveryPredicate_RefusesANoOp_AndTheLogReplays()
    {
        UiSession s = Session();
        Assert.False(s.EmitAllocationOrder(-1));
        Assert.False(s.EmitAllocationOrder(101));
        Assert.False(s.EmitAllocationOrder(100));                       // the default is already in force: nothing to set
        Assert.Empty(s.QueuedOrders());
        Assert.True(s.EmitAllocationOrder(40));
        Assert.Equal(ResourceAllocation.Order(s.World, Me, 40), s.Orders[^1]);
        Assert.False(s.EmitAllocationOrder(40));                        // already queued this turn
        Assert.True(s.EmitAllocationOrder(60));                         // a change of mind: the last Set wins
        var hashes = new List<string>();
        s.EndTurn();
        hashes.Add(WorldHash.ComputeHex(s.World));
        Assert.Equal(new ResourceAllocationRow(Me, 60), Assert.Single(Rows(s.World.ResourceAllocations)));
        Assert.True(s.EmitAllocationOrder(100));                        // back to building first: a real change now
        s.EndTurn();
        hashes.Add(WorldHash.ComputeHex(s.World));
        Assert.Equal(new ResourceAllocationRow(Me, 100), Assert.Single(Rows(s.World.ResourceAllocations)));

        // The log the UI wrote passes replay validation and reproduces hash for hash from the session's start.
        WorldState start = RoadWorld.Value.Clone();
        OrderValidation.ValidateAgainstWorld(s.Orders, start);
        TurnExecutor exec = UiSession.BuildProductionExecutor(s.Orders);
        WorldState w = start;
        for (int t = 0; t < hashes.Count; t++)
        {
            w = exec.Step(w);
            Assert.Equal(hashes[t], WorldHash.ComputeHex(w));
        }
    }
}
